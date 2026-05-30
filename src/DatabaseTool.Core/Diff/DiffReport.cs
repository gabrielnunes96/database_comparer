using DatabaseTool.Core.Models;

namespace DatabaseTool.Core.Diff;

public class DiffReport
{
    public SchemaModel Source { get; set; } = null!;
    public SchemaModel Target { get; set; } = null!;
    public DateTime GeneratedAt { get; set; } = DateTime.UtcNow;

    public SectionDiff<TableDiff> Tables { get; set; } = new();
    public SectionDiff<ObjectDiff> Views { get; set; } = new();
    public SectionDiff<ObjectDiff> Procedures { get; set; } = new();
    public SectionDiff<ObjectDiff> Functions { get; set; } = new();
    public SectionDiff<ObjectDiff> Triggers { get; set; } = new();

    public bool HasDifferences =>
        Tables.HasDifferences ||
        Views.HasDifferences ||
        Procedures.HasDifferences ||
        Functions.HasDifferences ||
        Triggers.HasDifferences;

    public int TotalDifferences =>
        Tables.TotalCount +
        Views.TotalCount +
        Procedures.TotalCount +
        Functions.TotalCount +
        Triggers.TotalCount;
}

public class SectionDiff<T>
{
    public List<string> OnlyInSource { get; set; } = new();
    public List<string> OnlyInTarget { get; set; } = new();
    public List<T> Different { get; set; } = new();

    public bool HasDifferences =>
        OnlyInSource.Count > 0 ||
        OnlyInTarget.Count > 0 ||
        Different.Count > 0;

    public int TotalCount =>
        OnlyInSource.Count +
        OnlyInTarget.Count +
        Different.Count;
}

public class TableDiff
{
    public string TableName { get; set; } = string.Empty;
    public List<ColumnDiff> ColumnDiffs { get; set; } = new();
    public List<string> IndexDiffs { get; set; } = new();
    public List<string> ConstraintDiffs { get; set; } = new();
    public bool PrimaryKeyChanged { get; set; }
}

public class ColumnDiff
{
    public string ColumnName { get; set; } = string.Empty;
    public DiffType DiffType { get; set; }
    public string? SourceValue { get; set; }
    public string? TargetValue { get; set; }
    public List<string> Changes { get; set; } = new();
}

public class ObjectDiff
{
    public string ObjectName { get; set; } = string.Empty;
    public bool DefinitionChanged { get; set; }
}

public enum DiffType
{
    Added,
    Removed,
    Modified
}
