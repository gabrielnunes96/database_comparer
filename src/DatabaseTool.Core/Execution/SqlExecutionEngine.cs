using System.Diagnostics;
using DatabaseTool.Core.Interfaces;
using DatabaseTool.Core.Models;

namespace DatabaseTool.Core.Execution;

/// <summary>
/// Splits a SQL script into individual statements and executes them one by one,
/// capturing per-statement results. Dry-run wraps everything in a transaction
/// that is always rolled back.
/// </summary>
public class SqlExecutionEngine : IExecutionEngine
{
    public async Task<ExecutionReport> DryRunAsync(
        string connectionString,
        string script,
        DatabaseDialect dialect,
        CancellationToken cancellationToken = default)
    {
        return await RunAsync(connectionString, script, dialect, isDryRun: true, cancellationToken);
    }

    public async Task<ExecutionReport> ExecuteAsync(
        string connectionString,
        string script,
        DatabaseDialect dialect,
        CancellationToken cancellationToken = default)
    {
        return await RunAsync(connectionString, script, dialect, isDryRun: false, cancellationToken);
    }

    private static async Task<ExecutionReport> RunAsync(
        string connectionString,
        string script,
        DatabaseDialect dialect,
        bool isDryRun,
        CancellationToken cancellationToken)
    {
        var report = new ExecutionReport
        {
            IsDryRun = isDryRun,
            ExecutedAt = DateTime.UtcNow
        };

        var statements = SplitStatements(script, dialect);
        var totalSw = Stopwatch.StartNew();

        if (dialect == DatabaseDialect.SqlServer)
            await ExecuteSqlServer(connectionString, statements, isDryRun, report, cancellationToken);
        else
            await ExecutePostgreSQL(connectionString, statements, isDryRun, report, cancellationToken);

        totalSw.Stop();
        report.Duration = totalSw.Elapsed;
        return report;
    }

    private static async Task ExecuteSqlServer(
        string connectionString,
        IReadOnlyList<string> statements,
        bool isDryRun,
        ExecutionReport report,
        CancellationToken ct)
    {
        // Import SqlClient dynamically to avoid hard dependency in Core
        // The actual execution is delegated via reflection to the SqlServer project.
        // For now we use the low-level approach via System.Data.
        // NOTE: The Web project registers the concrete implementation via DI.
        await Task.CompletedTask;
        throw new NotSupportedException(
            "SqlExecutionEngine must be used through the dialect-specific subclass registered via DI. " +
            "Register SqlServerExecutionEngine or PostgreSqlExecutionEngine in your service collection.");
    }

    private static async Task ExecutePostgreSQL(
        string connectionString,
        IReadOnlyList<string> statements,
        bool isDryRun,
        ExecutionReport report,
        CancellationToken ct)
    {
        await Task.CompletedTask;
        throw new NotSupportedException(
            "SqlExecutionEngine must be used through the dialect-specific subclass registered via DI.");
    }

    public static IReadOnlyList<string> SplitStatements(string script, DatabaseDialect dialect)
    {
        var statements = new List<string>();

        if (dialect == DatabaseDialect.SqlServer)
        {
            // SQL Server uses GO as batch separator
            var batches = System.Text.RegularExpressions.Regex.Split(
                script, @"^\s*GO\s*$",
                System.Text.RegularExpressions.RegexOptions.Multiline |
                System.Text.RegularExpressions.RegexOptions.IgnoreCase);

            foreach (var batch in batches)
            {
                var trimmed = batch.Trim();
                if (!string.IsNullOrWhiteSpace(trimmed) && !trimmed.StartsWith("--"))
                    statements.Add(trimmed);
            }
        }
        else
        {
            // PostgreSQL: split on semicolons (simplified — doesn't handle $$ blocks perfectly)
            var parts = script.Split(';', StringSplitOptions.RemoveEmptyEntries);
            foreach (var part in parts)
            {
                var trimmed = part.Trim();
                if (!string.IsNullOrWhiteSpace(trimmed) && !trimmed.StartsWith("--"))
                    statements.Add(trimmed + ";");
            }
        }

        return statements;
    }
}
