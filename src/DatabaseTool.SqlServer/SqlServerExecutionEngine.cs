using System.Diagnostics;
using DatabaseTool.Core.Execution;
using DatabaseTool.Core.Interfaces;
using DatabaseTool.Core.Models;
using Microsoft.Data.SqlClient;

namespace DatabaseTool.SqlServer;

public class SqlServerExecutionEngine : IExecutionEngine
{
    public async Task<ExecutionReport> DryRunAsync(
        string connectionString,
        string script,
        DatabaseDialect dialect,
        CancellationToken cancellationToken = default)
    {
        return await RunAsync(connectionString, script, isDryRun: true, cancellationToken);
    }

    public async Task<ExecutionReport> ExecuteAsync(
        string connectionString,
        string script,
        DatabaseDialect dialect,
        CancellationToken cancellationToken = default)
    {
        return await RunAsync(connectionString, script, isDryRun: false, cancellationToken);
    }

    private static async Task<ExecutionReport> RunAsync(
        string connectionString,
        string script,
        bool isDryRun,
        CancellationToken ct)
    {
        var report = new ExecutionReport
        {
            IsDryRun = isDryRun,
            ExecutedAt = DateTime.UtcNow
        };

        var statements = SqlExecutionEngine.SplitStatements(script, DatabaseDialect.SqlServer);
        var totalSw = Stopwatch.StartNew();

        await using var conn = new SqlConnection(connectionString);
        await conn.OpenAsync(ct);
        await using var transaction = conn.BeginTransaction();

        try
        {
            for (var i = 0; i < statements.Count; i++)
            {
                ct.ThrowIfCancellationRequested();
                var stmtResult = await ExecuteStatementAsync(conn, transaction, statements[i], i, ct);
                report.Statements.Add(stmtResult);

                if (!stmtResult.Success && !isDryRun)
                    break;
            }
        }
        finally
        {
            // Always rollback on dry-run; rollback on failure in real execution
            if (isDryRun || report.HasErrors)
            {
                try { transaction.Rollback(); } catch { /* already rolled back */ }
            }
            else
            {
                try { transaction.Commit(); } catch (Exception ex)
                {
                    report.Statements.Add(new StatementResult
                    {
                        Index = report.Statements.Count,
                        Statement = "COMMIT",
                        Success = false,
                        ErrorMessage = ex.Message
                    });
                    try { transaction.Rollback(); } catch { /* best effort */ }
                }
            }
        }

        totalSw.Stop();
        report.Duration = totalSw.Elapsed;
        return report;
    }

    private static async Task<StatementResult> ExecuteStatementAsync(
        SqlConnection conn,
        SqlTransaction transaction,
        string statement,
        int index,
        CancellationToken ct)
    {
        var sw = Stopwatch.StartNew();
        try
        {
            await using var cmd = new SqlCommand(statement, conn, transaction);
            cmd.CommandTimeout = 300;
            var rows = await cmd.ExecuteNonQueryAsync(ct);
            sw.Stop();
            return new StatementResult
            {
                Index = index,
                Statement = TruncateStatement(statement),
                Success = true,
                Duration = sw.Elapsed,
                RowsAffected = rows
            };
        }
        catch (SqlException ex)
        {
            sw.Stop();
            return new StatementResult
            {
                Index = index,
                Statement = TruncateStatement(statement),
                Success = false,
                ErrorMessage = ex.Message,
                ErrorNumber = ex.Number,
                Duration = sw.Elapsed
            };
        }
    }

    private static string TruncateStatement(string stmt) =>
        stmt.Length > 500 ? stmt[..500] + "..." : stmt;
}
