namespace DatabaseTool.Web.Components.Shared;

public class MappingRow
{
    public MappingRow() { }
    public MappingRow(string source, string target)
    {
        Source = source;
        Target = target;
    }

    public string Source { get; set; } = string.Empty;
    public string Target { get; set; } = string.Empty;
}
