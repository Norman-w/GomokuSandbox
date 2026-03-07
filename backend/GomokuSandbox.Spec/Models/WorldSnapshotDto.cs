namespace GomokuSandbox.Spec.Models;

public class WorldSnapshotDto
{
    public int BoardSize { get; set; } = 15;
    public int[][] Board { get; set; } = Array.Empty<int[]>();
    public string CurrentTurn { get; set; } = "Black";
    public int MoveCount { get; set; }
    public string GameStatus { get; set; } = "Playing";
    public int? BlackPlayerId { get; set; }
    public int? WhitePlayerId { get; set; }
    public string? Winner { get; set; }
    public int? LastMoveX { get; set; }
    public int? LastMoveY { get; set; }
}
