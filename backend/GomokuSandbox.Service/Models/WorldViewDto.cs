using GomokuSandbox.Spec.Models;

namespace GomokuSandbox.Service.Models;

public class WorldViewDto
{
    public WorldSnapshotDto Snapshot { get; set; } = new();
    public WorldRulesDto Rules { get; set; } = new();
    public string Direction { get; set; } = "";
    public PlayerViewDto? BlackPlayer { get; set; }
    public PlayerViewDto? WhitePlayer { get; set; }
    public IReadOnlyList<NarrativeEntryDto> Narrative { get; set; } = Array.Empty<NarrativeEntryDto>();
    /// <summary>当有人落子后待裁判判定时，为该棋手颜色（Black/White）；否则为 null。用于 UI 显示「我赢了没？」/「等待裁判」。</summary>
    public string? PendingRefereeBy { get; set; }
}

public class PlayerViewDto
{
    public int Id { get; set; }
    public string Color { get; set; } = "";
    public int Intelligence { get; set; }
    public int Score { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class PlayerDto
{
    public int Id { get; set; }
    public string Color { get; set; } = "";
    public int Intelligence { get; set; }
    public int Score { get; set; }
}
