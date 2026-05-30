namespace DatabaseTool.Core.Models;

public class SchemaModel
{
    public string DatabaseName { get; set; } = string.Empty;
    public DatabaseDialect Dialect { get; set; }
    public string SourceDescription { get; set; } = string.Empty;

    public List<TableModel> Tables { get; set; } = new();
    public List<ViewModel> Views { get; set; } = new();
    public List<ProcedureModel> Procedures { get; set; } = new();
    public List<FunctionModel> Functions { get; set; } = new();
    public List<TriggerModel> Triggers { get; set; } = new();
    public List<SequenceModel> Sequences { get; set; } = new();
}

public enum DatabaseDialect
{
    SqlServer,
    PostgreSQL
}
