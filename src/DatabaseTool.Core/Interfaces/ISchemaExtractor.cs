using DatabaseTool.Core.Models;

namespace DatabaseTool.Core.Interfaces;

public interface ISchemaExtractor
{
    Task<SchemaModel> ExtractAsync(string connectionString, ExtractionOptions options, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<string>> ListTablesAsync(string connectionString, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<string>> ListViewsAsync(string connectionString, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<string>> ListProceduresAsync(string connectionString, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<string>> ListFunctionsAsync(string connectionString, CancellationToken cancellationToken = default);
    Task<bool> TestConnectionAsync(string connectionString, CancellationToken cancellationToken = default);
}
