using System.Text.Json;
using GomokuSandbox.Service;
using GomokuSandbox.Service.Data;
using GomokuSandbox.Service.Models;
using GomokuSandbox.Spec;
using GomokuSandbox.Spec.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace GomokuSandbox.Api.Controllers;

/// <summary>
/// 唯一 UI 入口：页面通过本 Controller 调 Service；所有对世界的写操作由 Service 落库，读操作由 Service 从持久化提供。
/// CLI 直接引用 Service，不请求本 API。AI 只能通过 CLI 操作。
/// </summary>
[ApiController]
[Route("api/UI")]
public class UIController : ControllerBase
{
    private readonly IWorldState _world;
    private readonly INarrativeService _narrative;
    private readonly IAiActionService _actionService;
    private readonly IWorldViewService _viewService;
    private readonly AppDbContext _db;

    public UIController(IWorldState world, INarrativeService narrative, IAiActionService actionService, IWorldViewService viewService, AppDbContext db)
    {
        _world = world;
        _narrative = narrative;
        _actionService = actionService;
        _viewService = viewService;
        _db = db;
    }

    [HttpGet("next")]
    public AiNextTurnDto GetNext() => _world.GetAiNextTurn();

    [HttpPost("act")]
    public async Task<ActionResult<AiNextTurnDto>> Act([FromBody] ActRequest req, CancellationToken ct)
    {
        var (result, error) = await _actionService.ExecuteAsync(req.Role, req.Action ?? "", req.Payload, ct);
        if (error != null) return BadRequest(error);
        return Ok(result);
    }

    [HttpGet("info")]
    [Produces("application/json")]
    public ContentResult GetInfo()
    {
        var json = SpecBuilder.Build(typeof(AiActionSpecs));
        return Content(json, "application/json; charset=utf-8");
    }

    [HttpGet("help")]
    [Produces("text/plain")]
    public ContentResult GetHelp()
    {
        var text = SpecBuilder.BuildHelpText(typeof(AiActionSpecs), typeof(CliCommandSpecs));
        return Content(text, "text/plain; charset=utf-8");
    }

    [HttpPost("ensure")]
    public IActionResult EnsureGame()
    {
        var entries = _narrative.GetEntries();
        if (!entries.Any(e => e.Role == "Commander")) return BadRequest("请先由统领者发话后再开局。");
        if (!entries.Any(e => e.Role == "Creator")) return BadRequest("请先由造人者造人后再开局。");
        _world.EnsureGameExists();
        return Ok(_world.GetSnapshot());
    }

    [HttpGet("state")]
    public IActionResult GetState() => Ok(_world.GetSnapshot());

    [HttpPost("place")]
    public IActionResult Place([FromBody] PlaceRequest req)
    {
        var color = string.IsNullOrWhiteSpace(req.Color) ? "" : req.Color.Trim().Equals("Black", StringComparison.OrdinalIgnoreCase) ? "Black" : req.Color.Trim().Equals("White", StringComparison.OrdinalIgnoreCase) ? "White" : req.Color;
        if (color != "Black" && color != "White") return BadRequest("color 须为 Black 或 White。");
        var (ok, err) = _world.PlacePiece(color, req.X, req.Y);
        if (!ok) return BadRequest(err);
        var result = _world.CheckResult();
        if (!string.IsNullOrEmpty(result)) _world.SetGameOver(result);
        return Ok(new { success = true, snapshot = _world.GetSnapshot(), result });
    }

    [HttpPost("referee/check")]
    public IActionResult RefereeCheck()
    {
        var result = _world.CheckResult();
        if (!string.IsNullOrEmpty(result)) _world.SetGameOver(result);
        return Ok(new { result, snapshot = _world.GetSnapshot() });
    }

    [HttpGet("snapshot")]
    public WorldSnapshotDto GetSnapshot() => _world.GetSnapshot();

    [HttpGet("rules")]
    public WorldRulesDto GetRules() => _world.GetRules();

    [HttpPut("rules")]
    public void SetRules([FromBody] WorldRulesDto rules) => _world.SetRules(rules);

    [HttpGet("direction")]
    public string GetDirection() => _world.GetRules().Direction;

    [HttpPost("reset")]
    [HttpGet("reset")]
    public async Task<ActionResult<WorldViewDto>> ResetWorld(CancellationToken ct)
    {
        _world.ResetWorld();
        var view = await _viewService.GetViewAsync(ct);
        return Ok(view);
    }

    [HttpGet("view")]
    public async Task<WorldViewDto> GetView(CancellationToken ct) => await _viewService.GetViewAsync(ct);

    [HttpGet("players")]
    public async Task<ActionResult<List<PlayerDto>>> ListPlayers() =>
        Ok(await _db.Players.Select(p => new PlayerDto { Id = p.Id, Color = p.Color, Intelligence = p.Intelligence, Score = p.Score }).ToListAsync());

    [HttpPost("players")]
    public async Task<ActionResult<PlayerDto>> CreatePlayer([FromBody] CreatePlayerRequest req)
    {
        var p = new Player { Color = req.Color, Intelligence = req.Intelligence, Score = 0, CreatedAt = DateTime.UtcNow };
        _db.Players.Add(p);
        await _db.SaveChangesAsync();
        var msg = $"造了{req.Color}，智商{req.Intelligence}";
        if (!string.IsNullOrWhiteSpace(req.Thought)) msg += "。想法：" + req.Thought.Trim();
        _narrative.AppendCreator(msg);
        return Ok(new PlayerDto { Id = p.Id, Color = p.Color, Intelligence = p.Intelligence, Score = p.Score });
    }

    [HttpPatch("players/{id}/score")]
    public async Task<IActionResult> UpdatePlayerScore(int id, [FromBody] int delta)
    {
        var p = await _db.Players.FindAsync(id);
        if (p == null) return NotFound();
        p.Score += delta;
        await _db.SaveChangesAsync();
        return Ok(new PlayerDto { Id = p.Id, Color = p.Color, Intelligence = p.Intelligence, Score = p.Score });
    }
}

public class PlaceRequest
{
    public string Color { get; set; } = "";
    public int X { get; set; }
    public int Y { get; set; }
}

public class ActRequest
{
    public string Role { get; set; } = "";
    public string Action { get; set; } = "";
    public JsonElement? Payload { get; set; }
}

public class CreatePlayerRequest
{
    public string Color { get; set; } = "";
    public int Intelligence { get; set; } = 50;
    public string? Thought { get; set; }
}
