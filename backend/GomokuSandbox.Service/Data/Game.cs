namespace GomokuSandbox.Service.Data;

public class Game
{
    public int Id { get; set; }
    public int BlackPlayerId { get; set; }
    public int WhitePlayerId { get; set; }
    public string Status { get; set; } = "Playing";
    public DateTime CreatedAt { get; set; }
    public DateTime? FinishedAt { get; set; }
}
