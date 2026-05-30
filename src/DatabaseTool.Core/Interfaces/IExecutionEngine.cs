using DatabaseTool.Core.Execution;
using DatabaseTool.Core.Models;

namespace DatabaseTool.Core.Interfaces;

public interface IExecutionEngine
{
    Task<ExecutionReport> DryRunAsync(string connectionString, string script, DatabaseDialect dialect, CancellationToken cancellationToken = default);
    Task<ExecutionReport> ExecuteAsync(string connectionString, string script, DatabaseDialect dialect, CancellationToken cancellationToken = default);
}
