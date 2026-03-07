namespace GomokuSandbox.Spec.Models;

public class CommanderRulesPayload
{
    public string? Direction { get; set; }
    public int? MinMovesBeforeWin { get; set; }
    public double? BlackAdvantage { get; set; }
}
