namespace GomokuSandbox.Service.Data;

public class NarrativeEntry
{
    public int Id { get; set; }
    public string Role { get; set; } = "";
    public string Message { get; set; } = "";
    public DateTime At { get; set; }
}
