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
