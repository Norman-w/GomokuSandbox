namespace GomokuSandbox.Service.Data;

public class Player
{
    public int Id { get; set; }
    public string Color { get; set; } = "";
    public int Intelligence { get; set; }
    public int Score { get; set; }
    public DateTime CreatedAt { get; set; }
}
