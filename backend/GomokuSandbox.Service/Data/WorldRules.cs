namespace GomokuSandbox.Service.Data;

/// <summary>单行配置表，持久化世界规则。</summary>
public class WorldRules
{
    public int Id { get; set; }
    public int MinMovesBeforeWin { get; set; } = 5;
    public double BlackAdvantage { get; set; }
    public string Direction { get; set; } = "";
}
