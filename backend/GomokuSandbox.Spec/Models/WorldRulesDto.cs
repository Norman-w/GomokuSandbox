namespace GomokuSandbox.Spec.Models;

public class WorldRulesDto
{
    public int MinMovesBeforeWin { get; set; } = 8;
    public double BlackAdvantage { get; set; } = 0;
    public string Direction { get; set; } = "公平对局，先五连者胜";
}
