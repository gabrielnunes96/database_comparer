using DatabaseTool.Core.Models;

namespace DatabaseTool.Core.Interfaces;

public interface ISqlFileParser
{
    Task<SchemaModel> ParseAsync(string sqlContent, ExtractionOptions options, CancellationToken cancellationToken = default);
    DatabaseDialect SupportedDialect { get; }
}
