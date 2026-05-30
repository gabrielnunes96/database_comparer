using DatabaseTool.Core.Conversion;
using DatabaseTool.Core.Models;

namespace DatabaseTool.Core.Interfaces;

public interface ISchemaConverter
{
    ConversionDirection Direction { get; }
    Task<ConversionResult> ConvertAsync(SchemaModel source, ConversionOptions options, CancellationToken cancellationToken = default);
}

public enum ConversionDirection
{
    SqlServerToPostgreSQL,
    PostgreSQLToSqlServer
}

public class ConversionOptions
{
    public bool IncludeDropStatements { get; set; }
    public bool UseIfNotExists { get; set; } = true;
    public string TargetSchema { get; set; } = "public";
    public TypeMappingConfig TypeMapping { get; set; } = new();
}

public class ConversionResult
{
    public string Script { get; set; } = string.Empty;
    public List<ConversionWarning> Warnings { get; set; } = new();
    public bool HasErrors => Warnings.Any(w => w.Severity == WarningSeverity.Error);
}

public class ConversionWarning
{
    public string ObjectName { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public WarningSeverity Severity { get; set; }
    public string? OriginalCode { get; set; }
}

public enum WarningSeverity
{
    Info,
    Warning,
    Error
}
