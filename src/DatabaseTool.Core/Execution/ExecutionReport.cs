namespace DatabaseTool.Core.Execution;

public class ExecutionReport
{
    public bool IsDryRun { get; set; }
    public DateTime ExecutedAt { get; set; } = DateTime.UtcNow;
    public TimeSpan Duration { get; set; }
    public List<StatementResult> Statements { get; set; } = new();

    public int SuccessCount => Statements.Count(s => s.Success);
    public int ErrorCount => Statements.Count(s => !s.Success);
    public bool AllSucceeded => Statements.All(s => s.Success);
    public bool HasErrors => Statements.Any(s => !s.Success);
}

public class StatementResult
{
    public int Index { get; set; }
    public string Statement { get; set; } = string.Empty;
    public bool Success { get; set; }
    public string? ErrorMessage { get; set; }
    public int? ErrorNumber { get; set; }
    public TimeSpan Duration { get; set; }
    public int RowsAffected { get; set; }
}
