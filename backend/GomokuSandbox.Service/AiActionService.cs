using System.Text.Json;
using GomokuSandbox.Service.Data;
using GomokuSandbox.Spec.Models;
using Microsoft.EntityFrameworkCore;

namespace GomokuSandbox.Service;

public sealed class AiActionService : IAiActionService
{
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    private readonly IWorldState _world;
    private readonly INarrativeService _narrative;
    private readonly AppDbContext _db;

    public AiActionService(IWorldState world, INarrativeService narrative, AppDbContext db)
    {
        _world = world;
        _narrative = narrative;
        _db = db;
    }

    public async Task<(AiNextTurnDto? Result, string? Error)> ExecuteAsync(string role, string action, JsonElement? payload, CancellationToken ct = default)
    {
        role = NormalizeRole(role) ?? "";
        action = action?.Trim() ?? "";

        if ((role == "Black" || role == "White") && action.Equals("Place", StringComparison.OrdinalIgnoreCase) && payload.HasValue)
        {
            var p = JsonSerializer.Deserialize<PlacePayload>(payload.Value, JsonOptions);
            if (p == null) return (null, "Place payload 无效");
            return await PlaceAsync(role, p);
        }
        if (role == "Referee" && action.Equals("Check", StringComparison.OrdinalIgnoreCase))
        {
            if (!payload.HasValue) return (null, "Referee Check 需 AI 提供 payload：{ \"winner\": \"Black\" | \"White\" | null }，表示你判定谁赢或未赢。");
            var p = JsonSerializer.Deserialize<RefereeCheckPayload>(payload.Value, JsonOptions);
            if (p == null) return (null, "Referee Check payload 无效");
            return RefereeCheck(p);
        }
        if (role == "Commander" && (action == "SetRules" || action == "盘活") && payload.HasValue)
        {
            var p = JsonSerializer.Deserialize<CommanderRulesPayload>(payload.Value, JsonOptions);
            if (p == null) return (null, "SetRules payload 无效");
            return CommanderSetRules(p);
        }
        if (role == "Commander" && (action == "开新局" || action.Equals("NewGame", StringComparison.OrdinalIgnoreCase))) return CommanderNewGame();
        if (role == "Creator" && (action == "造人" || action.Equals("CreatePlayer", StringComparison.OrdinalIgnoreCase)) && payload.HasValue)
        {
            var p = JsonSerializer.Deserialize<CreatePlayerPayload>(payload.Value, JsonOptions);
            if (p == null || string.IsNullOrWhiteSpace(p.Color)) return (null, "造人 payload 需含 color");
            return await CreatorCreatePlayerAsync(p, ct);
        }
        return (_world.GetAiNextTurn(), null);
    }

    private Task<(AiNextTurnDto? Result, string? Error)> PlaceAsync(string role, PlacePayload p)
    {
        var (ok, err) = _world.PlacePiece(role, p.X, p.Y);
        if (!ok) return Task.FromResult<(AiNextTurnDto?, string?)>((null, err));
        var result = _world.CheckResult();
        if (result == "Draw" || result == "BlackWon" || result == "WhiteWon")
        {
            if (result == "Draw") { _world.SetGameOver(result); return Task.FromResult<(AiNextTurnDto?, string?)>((_world.GetAiNextTurn(), null)); }
            // 后端算法已算出有人五连，不直接结束对局，交由裁判在 Check 时结合 AI 判定后宣布
            var dto = _world.GetAiNextTurn(refereeRequested: true);
            dto.LastPlaceClaimWin = true;
            return Task.FromResult<(AiNextTurnDto?, string?)>((dto, null));
        }
        var next = _world.GetAiNextTurn(refereeRequested: false);
        next.LastPlaceClaimWin = false;
        return Task.FromResult<(AiNextTurnDto?, string?)>((next, null));
    }

    private (AiNextTurnDto? Result, string? Error) RefereeCheck(RefereeCheckPayload p)
    {
        var algorithmResult = _world.CheckResult();
        var winner = string.IsNullOrWhiteSpace(p.Winner) ? null : p.Winner.Trim();
        if (winner == "Black" || winner == "White")
        {
            var expected = winner + "Won";
            if (algorithmResult == expected)
            {
                _world.SetGameOver(algorithmResult);
                return (_world.GetAiNextTurn(), null);
            }
        }
        return (_world.GetAiNextTurn(afterRefereeCheck: true), null);
    }

    private (AiNextTurnDto? Result, string? Error) CommanderSetRules(CommanderRulesPayload payload)
    {
        var rules = _world.GetRules();
        if (!string.IsNullOrWhiteSpace(payload.Direction)) rules.Direction = payload.Direction;
        if (payload.MinMovesBeforeWin.HasValue) rules.MinMovesBeforeWin = payload.MinMovesBeforeWin.Value;
        if (payload.BlackAdvantage.HasValue) rules.BlackAdvantage = payload.BlackAdvantage.Value;
        _world.SetRules(rules);
        if (!string.IsNullOrWhiteSpace(payload.Direction)) _narrative.AppendCommander(payload.Direction);
        return (_world.GetAiNextTurn(), null);
    }

    private (AiNextTurnDto? Result, string? Error) CommanderNewGame()
    {
        var entries = _narrative.GetEntries();
        if (!entries.Any(e => e.Role == "Commander")) return (null, "请先由统领者发话后再开新局。");
        if (!entries.Any(e => e.Role == "Creator")) return (null, "请先由造人者造人后再开新局。");
        _world.EnsureGameExists();
        return (_world.GetAiNextTurn(), null);
    }

    private async Task<(AiNextTurnDto? Result, string? Error)> CreatorCreatePlayerAsync(CreatePlayerPayload payload, CancellationToken ct)
    {
        var colorNorm = NormalizeColor(payload.Color.Trim());
        if (colorNorm == null) return (null, "造人 color 必须为 Black 或 White（大小写不限）。");
        var p = new Player { Color = colorNorm, Intelligence = payload.Intelligence is >= 0 and <= 100 ? payload.Intelligence : 50, Score = 0, CreatedAt = DateTime.UtcNow };
        _db.Players.Add(p);
        await _db.SaveChangesAsync(ct);
        var msg = $"造了{p.Color}，智商{p.Intelligence}";
        if (!string.IsNullOrWhiteSpace(payload.Thought)) msg += "。想法：" + payload.Thought.Trim();
        _narrative.AppendCreator(msg);
        return (_world.GetAiNextTurn(), null);
    }

    private static string? NormalizeRole(string? r)
    {
        if (string.IsNullOrWhiteSpace(r)) return null;
        var s = r.Trim();
        if (s.Equals("Black", StringComparison.OrdinalIgnoreCase)) return "Black";
        if (s.Equals("White", StringComparison.OrdinalIgnoreCase)) return "White";
        if (s.Equals("Referee", StringComparison.OrdinalIgnoreCase)) return "Referee";
        if (s.Equals("Commander", StringComparison.OrdinalIgnoreCase)) return "Commander";
        if (s.Equals("Creator", StringComparison.OrdinalIgnoreCase)) return "Creator";
        return s;
    }

    private static string? NormalizeColor(string? c)
    {
        if (string.IsNullOrWhiteSpace(c)) return null;
        if (c.Equals("Black", StringComparison.OrdinalIgnoreCase)) return "Black";
        if (c.Equals("White", StringComparison.OrdinalIgnoreCase)) return "White";
        return null;
    }
}
