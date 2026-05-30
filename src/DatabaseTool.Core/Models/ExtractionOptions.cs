namespace DatabaseTool.Core.Models;

public class ExtractionOptions
{
    public bool IncludeTables { get; set; } = true;
    public List<string>? TableFilter { get; set; }

    public bool IncludeViews { get; set; } = true;
    public List<string>? ViewFilter { get; set; }

    public bool IncludeProcedures { get; set; } = true;
    public List<string>? ProcedureFilter { get; set; }

    public bool IncludeFunctions { get; set; } = true;
    public List<string>? FunctionFilter { get; set; }

    public bool IncludeTriggers { get; set; } = true;
    public bool IncludeIndexes { get; set; } = true;
    public bool IncludeSequences { get; set; } = true;

    public static ExtractionOptions All => new();

    public bool IsTableIncluded(string tableName) =>
        IncludeTables && (TableFilter is null || TableFilter.Contains(tableName, StringComparer.OrdinalIgnoreCase));

    public bool IsViewIncluded(string viewName) =>
        IncludeViews && (ViewFilter is null || ViewFilter.Contains(viewName, StringComparer.OrdinalIgnoreCase));

    public bool IsProcedureIncluded(string procName) =>
        IncludeProcedures && (ProcedureFilter is null || ProcedureFilter.Contains(procName, StringComparer.OrdinalIgnoreCase));

    public bool IsFunctionIncluded(string funcName) =>
        IncludeFunctions && (FunctionFilter is null || FunctionFilter.Contains(funcName, StringComparer.OrdinalIgnoreCase));
}
