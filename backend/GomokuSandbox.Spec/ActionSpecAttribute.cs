namespace GomokuSandbox.Spec;

[AttributeUsage(AttributeTargets.Method, AllowMultiple = true)]
public sealed class ActionSpecAttribute : Attribute
{
    public string Role { get; }
    public string[] ActionNames { get; }
    public Type? PayloadType { get; set; }
    public string Description { get; set; } = "";
    public string PayloadSchema { get; set; } = "";
    public string ExampleInvoke { get; set; } = "";

    public ActionSpecAttribute(string role, params string[] actionNames)
    {
        Role = role;
        ActionNames = actionNames.Length > 0 ? actionNames : Array.Empty<string>();
    }
}
