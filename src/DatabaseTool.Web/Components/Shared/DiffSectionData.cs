namespace DatabaseTool.Web.Components.Shared;

public class DiffSectionData
{
    public List<string> OnlyInSource { get; set; } = new();
    public List<string> OnlyInTarget { get; set; } = new();
    public List<string> Different { get; set; } = new();
}
