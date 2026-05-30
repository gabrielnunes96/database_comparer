namespace DatabaseTool.Core.Models;

public class ViewModel
{
    public string Schema { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string FullName => string.IsNullOrEmpty(Schema) ? Name : $"{Schema}.{Name}";
    public string Definition { get; set; } = string.Empty;
    public bool IsIndexed { get; set; }
}

public class ProcedureModel
{
    public string Schema { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string FullName => string.IsNullOrEmpty(Schema) ? Name : $"{Schema}.{Name}";
    public string Definition { get; set; } = string.Empty;
    public List<RoutineParameterModel> Parameters { get; set; } = new();
}

public class FunctionModel
{
    public string Schema { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string FullName => string.IsNullOrEmpty(Schema) ? Name : $"{Schema}.{Name}";
    public string Definition { get; set; } = string.Empty;
    public List<RoutineParameterModel> Parameters { get; set; } = new();
    public string ReturnType { get; set; } = string.Empty;
    public FunctionType FunctionType { get; set; }
}

public enum FunctionType
{
    Scalar,
    TableValued,
    InlineTableValued,
    Aggregate
}

public class RoutineParameterModel
{
    public string Name { get; set; } = string.Empty;
    public string DataType { get; set; } = string.Empty;
    public ParameterDirection Direction { get; set; } = ParameterDirection.Input;
    public string? DefaultValue { get; set; }
}

public enum ParameterDirection
{
    Input,
    Output,
    InputOutput
}

public class TriggerModel
{
    public string Schema { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string FullName => string.IsNullOrEmpty(Schema) ? Name : $"{Schema}.{Name}";
    public string TableName { get; set; } = string.Empty;
    public string TableSchema { get; set; } = string.Empty;
    public TriggerTiming Timing { get; set; }
    public List<TriggerEvent> Events { get; set; } = new();
    public string Definition { get; set; } = string.Empty;
    public bool IsEnabled { get; set; } = true;
}

public enum TriggerTiming
{
    Before,
    After,
    InsteadOf
}

public enum TriggerEvent
{
    Insert,
    Update,
    Delete
}

public class SequenceModel
{
    public string Schema { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string FullName => string.IsNullOrEmpty(Schema) ? Name : $"{Schema}.{Name}";
    public string DataType { get; set; } = "bigint";
    public long StartValue { get; set; } = 1;
    public long Increment { get; set; } = 1;
    public long? MinValue { get; set; }
    public long? MaxValue { get; set; }
    public bool Cycle { get; set; }
}
