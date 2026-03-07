namespace GomokuSandbox.Service.Data;

public class GameMove
{
    public int Id { get; set; }
    public int GameId { get; set; }
    public int X { get; set; }
    public int Y { get; set; }
    public string Color { get; set; } = "";
    public int Sequence { get; set; }
}
