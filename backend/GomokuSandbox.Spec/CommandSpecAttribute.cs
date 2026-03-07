namespace GomokuSandbox.Spec;

[AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
public sealed class CommandSpecAttribute : Attribute
{
    public string Name { get; }
    public string Api { get; set; } = "";
    public string Args { get; set; } = "";
    public string Description { get; set; } = "";
    public string Example { get; set; } = "";
    public string Returns { get; set; } = "";

    public CommandSpecAttribute(string name)
    {
        Name = name;
    }
}
