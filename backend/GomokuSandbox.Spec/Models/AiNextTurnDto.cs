namespace GomokuSandbox.Spec.Models;

public class AiNextTurnDto
{
    public string NextRole { get; set; } = "";
    public object? RoleContext { get; set; }
    public WorldSnapshotDto WorldSnapshot { get; set; } = new();
    public WorldRulesDto Rules { get; set; } = new();
    public string Direction { get; set; } = "";
    public int DelaySeconds { get; set; }
    public bool? LastPlaceClaimWin { get; set; }
}
