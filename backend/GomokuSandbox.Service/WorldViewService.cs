using GomokuSandbox.Service.Data;
using GomokuSandbox.Service.Models;
using Microsoft.EntityFrameworkCore;

namespace GomokuSandbox.Service;

public class WorldViewService : IWorldViewService
{
    private readonly IWorldState _world;
    private readonly INarrativeService _narrative;
    private readonly AppDbContext _db;

    public WorldViewService(IWorldState world, INarrativeService narrative, AppDbContext db)
    {
        _world = world;
        _narrative = narrative;
        _db = db;
    }

    public async Task<WorldViewDto> GetViewAsync(CancellationToken ct = default)
    {
        var snapshot = _world.GetSnapshot();
        var rules = _world.GetRules();
        var narrative = _narrative.GetEntries();
        Player? black = null;
        Player? white = null;
        if (snapshot.BlackPlayerId.HasValue) black = await _db.Players.FindAsync(new object[] { snapshot.BlackPlayerId.Value }, ct);
        if (snapshot.WhitePlayerId.HasValue) white = await _db.Players.FindAsync(new object[] { snapshot.WhitePlayerId.Value }, ct);
        return new WorldViewDto
        {
            Snapshot = snapshot,
            Rules = rules,
            Direction = rules.Direction,
            BlackPlayer = black == null ? null : new PlayerViewDto { Id = black.Id, Color = black.Color, Intelligence = black.Intelligence, Score = black.Score, CreatedAt = black.CreatedAt },
            WhitePlayer = white == null ? null : new PlayerViewDto { Id = white.Id, Color = white.Color, Intelligence = white.Intelligence, Score = white.Score, CreatedAt = white.CreatedAt },
            Narrative = narrative
        };
    }
}
