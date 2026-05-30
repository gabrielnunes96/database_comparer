namespace DatabaseTool.Core.Models;

public class TableModel
{
    public string Schema { get; set; } = "dbo";
    public string Name { get; set; } = string.Empty;
    public string FullName => string.IsNullOrEmpty(Schema) ? Name : $"{Schema}.{Name}";

    public List<ColumnModel> Columns { get; set; } = new();
    public PrimaryKeyModel? PrimaryKey { get; set; }
    public List<ForeignKeyModel> ForeignKeys { get; set; } = new();
    public List<IndexModel> Indexes { get; set; } = new();
    public List<CheckConstraintModel> CheckConstraints { get; set; } = new();
    public List<UniqueConstraintModel> UniqueConstraints { get; set; } = new();
}

public class ColumnModel
{
    public string Name { get; set; } = string.Empty;
    public string DataType { get; set; } = string.Empty;
    public int? MaxLength { get; set; }
    public int? Precision { get; set; }
    public int? Scale { get; set; }
    public bool IsNullable { get; set; } = true;
    public bool IsIdentity { get; set; }
    public long? IdentitySeed { get; set; }
    public long? IdentityIncrement { get; set; }
    public string? DefaultValue { get; set; }
    public bool IsComputed { get; set; }
    public string? ComputedExpression { get; set; }
    public int OrdinalPosition { get; set; }
}

public class PrimaryKeyModel
{
    public string Name { get; set; } = string.Empty;
    public List<string> Columns { get; set; } = new();
    public bool IsClustered { get; set; } = true;
}

public class ForeignKeyModel
{
    public string Name { get; set; } = string.Empty;
    public List<string> Columns { get; set; } = new();
    public string ReferencedSchema { get; set; } = string.Empty;
    public string ReferencedTable { get; set; } = string.Empty;
    public List<string> ReferencedColumns { get; set; } = new();
    public ForeignKeyAction OnDelete { get; set; } = ForeignKeyAction.NoAction;
    public ForeignKeyAction OnUpdate { get; set; } = ForeignKeyAction.NoAction;
}

public enum ForeignKeyAction
{
    NoAction,
    Cascade,
    SetNull,
    SetDefault,
    Restrict
}

public class IndexModel
{
    public string Name { get; set; } = string.Empty;
    public List<string> Columns { get; set; } = new();
    public List<string> IncludedColumns { get; set; } = new();
    public bool IsUnique { get; set; }
    public bool IsClustered { get; set; }
    public string? FilterExpression { get; set; }
}

public class CheckConstraintModel
{
    public string Name { get; set; } = string.Empty;
    public string Expression { get; set; } = string.Empty;
}

public class UniqueConstraintModel
{
    public string Name { get; set; } = string.Empty;
    public List<string> Columns { get; set; } = new();
}
