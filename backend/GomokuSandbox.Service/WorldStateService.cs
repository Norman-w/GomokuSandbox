using System.Linq;
using GomokuSandbox.Service.Data;
using GomokuSandbox.Spec.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace GomokuSandbox.Service;

public class WorldStateService : IWorldState
{
    private const int Size = 15;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly INarrativeService _narrative;
    private readonly object _lock = new();
    private int[,] _board = new int[Size, Size];
    private string _currentTurn = "Black";
    private int _moveCount;
    private string _gameStatus = "Playing";
    private int? _blackPlayerId;
    private int? _whitePlayerId;
    private int? _currentGameId;
    private WorldRulesDto _rules = new();
    private int? _lastMoveX;
    private int? _lastMoveY;

    public WorldStateService(IServiceScopeFactory scopeFactory, INarrativeService narrative)
    {
        _scopeFactory = scopeFactory;
        _narrative = narrative;
    }

    public WorldSnapshotDto GetSnapshot()
    {
        lock (_lock)
        {
            LoadCurrentGameFromDbIfNeeded();
            var board = new int[Size][];
            for (var i = 0; i < Size; i++)
            {
                board[i] = new int[Size];
                for (var j = 0; j < Size; j++) board[i][j] = _board[i, j];
            }
            return new WorldSnapshotDto
            {
                BoardSize = Size,
                Board = board,
                CurrentTurn = _currentTurn,
                MoveCount = _moveCount,
                GameStatus = _gameStatus,
                BlackPlayerId = _blackPlayerId,
                WhitePlayerId = _whitePlayerId,
                Winner = _gameStatus == "BlackWon" ? "Black" : _gameStatus == "WhiteWon" ? "White" : null,
                LastMoveX = _lastMoveX,
                LastMoveY = _lastMoveY
            };
        }
    }

    public WorldRulesDto GetRules() => new() { MinMovesBeforeWin = _rules.MinMovesBeforeWin, BlackAdvantage = _rules.BlackAdvantage, Direction = _rules.Direction };

    public void SetRules(WorldRulesDto rules) { lock (_lock) { _rules = rules; } }

    /// <summary>
    /// 若内存中无当前对局，则从数据库加载最近一场未结束的对局，使多次 CLI 调用共享同一盘棋。
    /// </summary>
    private void LoadCurrentGameFromDbIfNeeded()
    {
        if (_currentGameId.HasValue) return;
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var game = db.Games.OrderByDescending(g => g.Id).FirstOrDefault(g => g.Status == "Playing");
        if (game == null) return;
        _currentGameId = game.Id;
        _blackPlayerId = game.BlackPlayerId;
        _whitePlayerId = game.WhitePlayerId;
        _board = new int[Size, Size];
        _moveCount = 0;
        var moves = db.GameMoves.Where(m => m.GameId == game.Id).OrderBy(m => m.Sequence).ToList();
        foreach (var m in moves)
        {
            _board[m.X, m.Y] = m.Color == "Black" ? 1 : 2;
            _moveCount++;
        }
        _lastMoveX = moves.Count > 0 ? moves.Last().X : (int?)null;
        _lastMoveY = moves.Count > 0 ? moves.Last().Y : (int?)null;
        _currentTurn = _moveCount % 2 == 0 ? "Black" : "White";
        _gameStatus = "Playing";
    }

    public AiNextTurnDto GetAiNextTurn(bool afterRefereeCheck = false, bool refereeRequested = false)
    {
        lock (_lock)
        {
            LoadCurrentGameFromDbIfNeeded();
            var snapshot = GetSnapshot();
            var rules = GetRules();
            string nextRole;
            object? roleContext = null;

            if (!_currentGameId.HasValue)
            {
                var entries = _narrative.GetEntries();
                int playerCount = 0;
                using (var scope = _scopeFactory.CreateScope())
                {
                    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                    playerCount = db.Players.Count(p => p.Color == "Black" || p.Color == "White");
                }
                if (!entries.Any(e => e.Role == "Commander"))
                {
                    nextRole = "Commander";
                    roleContext = new { message = "请统领者发话（SetRules），设定方向与规则。payload 需含 direction（字符串）。" };
                }
                else if (!entries.Any(e => e.Role == "Creator"))
                {
                    nextRole = "Creator";
                    roleContext = new { message = "请造人者造人（CreatePlayer），造出黑方与白方。payload 需含 color（Black/White）、可选 intelligence。" };
                }
                else if (playerCount < 2)
                {
                    nextRole = "Creator";
                    roleContext = new { message = "请继续造人（CreatePlayer），造满两名选手后由统领者开新局。当前已造人数：" + playerCount + "。" };
                }
                else
                {
                    nextRole = "Commander";
                    roleContext = new { message = "两人已造好，请统领者开新局（开新局）。无需 payload。" };
                }
                return new AiNextTurnDto { NextRole = nextRole, RoleContext = roleContext, WorldSnapshot = snapshot, Rules = rules, Direction = rules.Direction };
            }

            if (_gameStatus != "Playing")
            {
                nextRole = "Commander";
                roleContext = _gameStatus == "Idle" ? new { message = "世界已重置，请统领者发话或开新局。" } : new { message = "本局已结束，可由统领开新局或调整规则。" };
            }
            else if (afterRefereeCheck) { nextRole = _currentTurn; roleContext = new { color = nextRole, currentTurn = _currentTurn, moveCount = _moveCount }; }
            else if (refereeRequested) { nextRole = "Referee"; roleContext = new { message = "棋手自认获胜，请判定是否五连。", playerClaimedWin = true }; }
            else { nextRole = _currentTurn; roleContext = null; }
            if (nextRole == "Referee" && roleContext == null) roleContext = new { message = "请根据规则判定是否已有五连或违规。" };
            if (nextRole == "Black" || nextRole == "White")
            {
                var playerId = nextRole == "Black" ? _blackPlayerId : _whitePlayerId;
                Player? player = null;
                if (playerId.HasValue)
                {
                    using var scope = _scopeFactory.CreateScope();
                    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                    player = db.Players.Find(playerId.Value);
                }
                var startHint = _moveCount == 0 ? "请落子开始对局。" : null;
                roleContext = new { color = nextRole, currentTurn = _currentTurn, moveCount = _moveCount, message = startHint, player = player == null ? null : new { player!.Id, player.Color, player.Intelligence, player.Score } };
            }
            return new AiNextTurnDto { NextRole = nextRole, RoleContext = roleContext, WorldSnapshot = snapshot, Rules = rules, Direction = rules.Direction };
        }
    }

    public (bool Ok, string Error) PlacePiece(string color, int x, int y)
    {
        lock (_lock)
        {
            LoadCurrentGameFromDbIfNeeded();
            if (_gameStatus != "Playing") return (false, "对局已结束");
            if (color != _currentTurn) return (false, $"当前轮到 {_currentTurn}");
            if (x < 0 || x >= Size || y < 0 || y >= Size) return (false, "坐标越界");
            if (_board[x, y] != 0) return (false, "该位置已有子");
            var piece = color == "Black" ? 1 : 2;
            _board[x, y] = piece;
            _lastMoveX = x; _lastMoveY = y;
            _moveCount++;
            _currentTurn = _currentTurn == "Black" ? "White" : "Black";
            using var scope = _scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            if (_currentGameId.HasValue)
            {
                db.GameMoves.Add(new GameMove { GameId = _currentGameId.Value, X = x, Y = y, Color = color, Sequence = _moveCount });
                db.SaveChanges();
            }
            return (true, "");
        }
    }

    public string? CheckResult()
    {
        lock (_lock)
        {
            for (var i = 0; i < Size; i++)
                for (var j = 0; j < Size; j++)
                {
                    var v = _board[i, j];
                    if (v == 0) continue;
                    if (CountInDir(i, j, 1, 0, v) >= 5 || CountInDir(i, j, 0, 1, v) >= 5 || CountInDir(i, j, 1, 1, v) >= 5 || CountInDir(i, j, 1, -1, v) >= 5)
                        return v == 1 ? "BlackWon" : "WhiteWon";
                }
            if (_moveCount >= Size * Size) return "Draw";
            return null;
        }
    }

    private int CountInDir(int si, int sj, int di, int dj, int v)
    {
        var c = 0; var i = si; var j = sj;
        while (i >= 0 && i < Size && j >= 0 && j < Size && _board[i, j] == v) { c++; i += di; j += dj; }
        return c;
    }

    public void EnsureGameExists()
    {
        lock (_lock)
        {
            if (_currentGameId.HasValue && _gameStatus == "Playing") return;
            using var scope = _scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var black = db.Players.FirstOrDefault(p => p.Color == "Black") ?? EnsurePlayer("Black", 50);
            var white = db.Players.FirstOrDefault(p => p.Color == "White") ?? EnsurePlayer("White", 50);
            if (black.Id == 0) { db.Players.Add(black); db.SaveChanges(); }
            if (white.Id == 0) { db.Players.Add(white); db.SaveChanges(); }
            var game = new Game { BlackPlayerId = black.Id, WhitePlayerId = white.Id, Status = "Playing", CreatedAt = DateTime.UtcNow };
            db.Games.Add(game);
            db.SaveChanges();
            _currentGameId = game.Id; _blackPlayerId = black.Id; _whitePlayerId = white.Id;
            _board = new int[Size, Size]; _currentTurn = "Black"; _moveCount = 0; _gameStatus = "Playing"; _lastMoveX = null; _lastMoveY = null;
        }
    }

    public Player EnsurePlayer(string color, int intelligence = 50)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var p = db.Players.FirstOrDefault(x => x.Color == color);
        if (p != null) return p;
        p = new Player { Color = color, Intelligence = intelligence, Score = 0, CreatedAt = DateTime.UtcNow };
        db.Players.Add(p);
        db.SaveChanges();
        return p;
    }

    public void UpdatePlayerScore(int playerId, int delta)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var p = db.Players.Find(playerId);
        if (p != null) { p.Score += delta; db.SaveChanges(); }
    }

    public void SetGameOver(string status)
    {
        lock (_lock)
        {
            _gameStatus = status;
            if (!_currentGameId.HasValue) return;
            using var scope = _scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var g = db.Games.Find(_currentGameId.Value);
            if (g != null) { g.Status = status; g.FinishedAt = DateTime.UtcNow; db.SaveChanges(); }
            if (status == "BlackWon" && _blackPlayerId.HasValue) UpdatePlayerScore(_blackPlayerId.Value, 1);
            if (status == "WhiteWon" && _whitePlayerId.HasValue) UpdatePlayerScore(_whitePlayerId.Value, 1);
        }
    }

    public void ResetWorld()
    {
        lock (_lock)
        {
            _board = new int[Size, Size]; _currentTurn = "Black"; _moveCount = 0; _gameStatus = "Idle";
            _blackPlayerId = null; _whitePlayerId = null; _currentGameId = null; _lastMoveX = null; _lastMoveY = null;
        }
    }
}
