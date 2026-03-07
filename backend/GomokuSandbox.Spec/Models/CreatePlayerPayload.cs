namespace GomokuSandbox.Spec.Models;

public class CreatePlayerPayload
{
    public string Color { get; set; } = "";
    public int Intelligence { get; set; } = 50;
    public string? Thought { get; set; }
}
