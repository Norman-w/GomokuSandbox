using System.Linq;
using GomokuSandbox.Service.Data;
using GomokuSandbox.Spec.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace GomokuSandbox.Service;

/// <summary>完全无内存状态：每次 API/CLI 调用都从数据库读取并拼接数据，写操作只落库。</summary>
public class WorldStateService : IWorldState
{
    private const int Size = 15;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly INarrativeService _narrative;

    public WorldStateService(IServiceScopeFactory scopeFactory, INarrativeService narrative)
    {
        _scopeFactory = scopeFactory;
        _narrative = narrative;
    }

    public WorldSnapshotDto GetSnapshot()
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var game = GetLatestGame(db);
        if (game == null)
            return BuildEmptySnapshot();
        return BuildSnapshotFromGame(db, game);
    }

    public WorldRulesDto GetRules()
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var row = db.WorldRules.FirstOrDefault();
        if (row == null)
            return new WorldRulesDto { MinMovesBeforeWin = 5, BlackAdvantage = 0, Direction = "" };
        return new WorldRulesDto { MinMovesBeforeWin = row.MinMovesBeforeWin, BlackAdvantage = row.BlackAdvantage, Direction = row.Direction ?? string.Empty };
    }

    public void SetRules(WorldRulesDto rules)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var row = db.WorldRules.FirstOrDefault();
        if (row == null)
        {
            row = new WorldRules { MinMovesBeforeWin = rules.MinMovesBeforeWin, BlackAdvantage = rules.BlackAdvantage, Direction = rules.Direction ?? "" };
            db.WorldRules.Add(row);
        }
        else
        {
            row.MinMovesBeforeWin = rules.MinMovesBeforeWin;
            row.BlackAdvantage = rules.BlackAdvantage;
            row.Direction = rules.Direction ?? "";
        }
        db.SaveChanges();
    }

    public AiNextTurnDto GetAiNextTurn(bool afterRefereeCheck = false, bool refereeRequested = false)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var entries = _narrative.GetEntries();
        var hasCommander = entries.Any(e => e.Role == "Commander");
        var hasCreator = entries.Any(e => e.Role == "Creator");
        var latestGame = GetLatestGame(db);
        // 仅当叙事里已有统领者+造人者时，才把对局当作本世界的有效对局；否则视为未开局，必须先造人再下子
        if (latestGame != null && (!hasCommander || !hasCreator))
            latestGame = null;
        var game = latestGame != null && latestGame.Status == "Playing" ? latestGame : null;
        var snapshot = latestGame == null ? BuildEmptySnapshot() : BuildSnapshotFromGame(db, latestGame);
        var rules = GetRules();
        string nextRole;
        object? roleContext = null;

        if (game == null)
        {
            var playerCount = db.Players.Count(p => p.Color == "Black" || p.Color == "White");
            if (!hasCommander)
            {
                nextRole = "Commander";
                roleContext = new { message = "请统领者发话（SetRules），设定方向与规则。payload 需含 direction（字符串）。" };
            }
            else if (!hasCreator)
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
                roleContext = snapshot.GameStatus == "BlackWon" || snapshot.GameStatus == "WhiteWon"
                    ? new { message = "本局已结束，可由统领开新局或调整规则。" }
                    : new { message = "两人已造好，请统领者开新局（开新局）。无需 payload。" };
            }
            return new AiNextTurnDto { NextRole = nextRole, RoleContext = roleContext, WorldSnapshot = snapshot, Rules = rules, Direction = rules.Direction };
        }

        var moveCount = db.GameMoves.Count(m => m.GameId == game.Id);
        var currentTurn = moveCount % 2 == 0 ? "Black" : "White";
        var gameStatus = game.Status;

        if (gameStatus != "Playing")
        {
            nextRole = "Commander";
            roleContext = gameStatus == "Idle" ? new { message = "世界已重置，请统领者发话或开新局。" } : new { message = "本局已结束，可由统领开新局或调整规则。" };
        }
        else if (afterRefereeCheck) { nextRole = currentTurn; roleContext = new { color = nextRole, currentTurn, moveCount }; }
        else if (refereeRequested) { nextRole = "Referee"; roleContext = new { message = "后端算法已检测到可能五连，请裁判（你）根据棋盘判定是否真的赢，并提交 Check 的 payload：{ \"winner\": \"Black\" | \"White\" | null }。", playerClaimedWin = true }; }
        else { nextRole = currentTurn; roleContext = null; }
        if (nextRole == "Referee" && roleContext == null) roleContext = new { message = "请根据规则判定是否已有五连，并提交 payload：{ \"winner\": \"Black\" | \"White\" | null }。" };
        if (nextRole == "Black" || nextRole == "White")
        {
            var playerId = nextRole == "Black" ? game.BlackPlayerId : game.WhitePlayerId;
            var player = db.Players.Find(playerId);
            var startHint = moveCount == 0 ? "请落子开始对局。" : null;
            roleContext = new { color = nextRole, currentTurn, moveCount, message = startHint, player = player == null ? null : new { player.Id, player.Color, player.Intelligence, player.Score } };
        }
        return new AiNextTurnDto { NextRole = nextRole, RoleContext = roleContext, WorldSnapshot = snapshot, Rules = rules, Direction = rules.Direction };
    }

    public (bool Ok, string Error) PlacePiece(string color, int x, int y)
    {
        if (x < 0 || x >= Size || y < 0 || y >= Size) return (false, "坐标越界");
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var game = GetCurrentPlayingGame(db);
        if (game == null) return (false, "无进行中对局");
        var moves = db.GameMoves.Where(m => m.GameId == game.Id).OrderBy(m => m.Sequence).ToList();
        var moveCount = moves.Count;
        var currentTurn = moveCount % 2 == 0 ? "Black" : "White";
        if (color != currentTurn) return (false, $"当前轮到 {currentTurn}");
        var board = BuildBoard(moves);
        if (board[x, y] != 0) return (false, "该位置已有子");
        var piece = color == "Black" ? 1 : 2;
        db.GameMoves.Add(new GameMove { GameId = game.Id, X = x, Y = y, Color = color, Sequence = moveCount + 1 });
        db.SaveChanges();
        return (true, "");
    }

    public string? CheckResult()
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var game = GetCurrentPlayingGame(db);
        if (game == null) return null;
        var moves = db.GameMoves.Where(m => m.GameId == game.Id).OrderBy(m => m.Sequence).ToList();
        var board = BuildBoard(moves);
        for (var i = 0; i < Size; i++)
            for (var j = 0; j < Size; j++)
            {
                var v = board[i, j];
                if (v == 0) continue;
                if (CountInDir(board, i, j, 1, 0, v) >= 5 || CountInDir(board, i, j, 0, 1, v) >= 5 || CountInDir(board, i, j, 1, 1, v) >= 5 || CountInDir(board, i, j, 1, -1, v) >= 5)
                    return v == 1 ? "BlackWon" : "WhiteWon";
            }
        if (moves.Count >= Size * Size) return "Draw";
        return null;
    }

    private static int CountInDir(int[,] board, int si, int sj, int di, int dj, int v)
    {
        var c = 0; var i = si; var j = sj;
        while (i >= 0 && i < Size && j >= 0 && j < Size && board[i, j] == v) { c++; i += di; j += dj; }
        return c;
    }

    public void EnsureGameExists()
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        if (GetCurrentPlayingGame(db) != null) return;
        var black = db.Players.FirstOrDefault(p => p.Color == "Black") ?? EnsurePlayer("Black", 50);
        var white = db.Players.FirstOrDefault(p => p.Color == "White") ?? EnsurePlayer("White", 50);
        if (black.Id == 0) { db.Players.Add(black); db.SaveChanges(); }
        if (white.Id == 0) { db.Players.Add(white); db.SaveChanges(); }
        var game = new Game { BlackPlayerId = black.Id, WhitePlayerId = white.Id, Status = "Playing", CreatedAt = DateTime.UtcNow };
        db.Games.Add(game);
        db.SaveChanges();
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
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var game = GetCurrentPlayingGame(db);
        if (game == null) return;
        game.Status = status;
        game.FinishedAt = DateTime.UtcNow;
        db.SaveChanges();
        if (status == "BlackWon") UpdatePlayerScore(game.BlackPlayerId, 1);
        if (status == "WhiteWon") UpdatePlayerScore(game.WhitePlayerId, 1);
    }

    /// <summary>重置世界：删除本世界在库中的全部关联数据（对局、棋步、玩家、叙事、规则），相当于世界毁灭，之后 AI 会从统领者发话、造人重新开始。</summary>
    public void ResetWorld()
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        db.GameMoves.RemoveRange(db.GameMoves);
        db.Games.RemoveRange(db.Games);
        db.Players.RemoveRange(db.Players);
        db.NarrativeEntries.RemoveRange(db.NarrativeEntries);
        db.WorldRules.RemoveRange(db.WorldRules);
        db.SaveChanges();
    }

    private static Game? GetCurrentPlayingGame(AppDbContext db) =>
        db.Games.OrderByDescending(g => g.Id).FirstOrDefault(g => g.Status == "Playing");

    /// <summary>取最近一局（按 Id 最大），不论进行中或已结束；用于对局结束后仍展示棋盘与胜者，不重置世界。</summary>
    private static Game? GetLatestGame(AppDbContext db) =>
        db.Games.OrderByDescending(g => g.Id).FirstOrDefault();

    private static WorldSnapshotDto BuildEmptySnapshot() => new()
    {
        BoardSize = Size,
        Board = Enumerable.Range(0, Size).Select(_ => new int[Size]).ToArray(),
        CurrentTurn = "",
        MoveCount = 0,
        GameStatus = "Idle",
        BlackPlayerId = null,
        WhitePlayerId = null,
        Winner = null,
        LastMoveX = null,
        LastMoveY = null,
        GameStartedAt = null,
        GameFinishedAt = null
    };

    private static int[,] BuildBoard(List<GameMove> moves)
    {
        var board = new int[Size, Size];
        foreach (var m in moves)
            board[m.X, m.Y] = m.Color == "Black" ? 1 : 2;
        return board;
    }

    private static WorldSnapshotDto BuildSnapshotFromGame(AppDbContext db, Game game)
    {
        var moves = db.GameMoves.Where(m => m.GameId == game.Id).OrderBy(m => m.Sequence).ToList();
        var board = new int[Size][];
        for (var i = 0; i < Size; i++)
        {
            board[i] = new int[Size];
            for (var j = 0; j < Size; j++) board[i][j] = 0;
        }
        foreach (var m in moves)
            board[m.X][m.Y] = m.Color == "Black" ? 1 : 2;
        var moveCount = moves.Count;
        var last = moves.LastOrDefault();
        return new WorldSnapshotDto
        {
            BoardSize = Size,
            Board = board,
            CurrentTurn = game.Status == "Playing" ? (moveCount % 2 == 0 ? "Black" : "White") : "",
            MoveCount = moveCount,
            GameStatus = game.Status,
            BlackPlayerId = game.BlackPlayerId,
            WhitePlayerId = game.WhitePlayerId,
            Winner = game.Status == "BlackWon" ? "Black" : game.Status == "WhiteWon" ? "White" : null,
            LastMoveX = last != null ? last.X : (int?)null,
            LastMoveY = last != null ? last.Y : (int?)null,
            GameStartedAt = game.CreatedAt,
            GameFinishedAt = game.FinishedAt
        };
    }
}
