using DatabaseTool.Core.Conversion;
using DatabaseTool.Core.Diff;
using DatabaseTool.Core.Execution;
using DatabaseTool.Core.Interfaces;
using DatabaseTool.Core.Models;
using Microsoft.Extensions.DependencyInjection;

namespace DatabaseTool.Web.Services;

public class SchemaService
{
    private readonly IServiceProvider _services;
    private readonly ISchemaDiffer _differ;
    private readonly SyncScriptGenerator _syncScriptGenerator;
    private readonly FileGenerator _fileGenerator;

    public SchemaService(
        IServiceProvider services,
        ISchemaDiffer differ,
        SyncScriptGenerator syncScriptGenerator,
        FileGenerator fileGenerator)
    {
        _services = services;
        _differ = differ;
        _syncScriptGenerator = syncScriptGenerator;
        _fileGenerator = fileGenerator;
    }

    public async Task<SchemaModel> ExtractFromConnectionAsync(
        string connectionString,
        DatabaseDialect dialect,
        ExtractionOptions options,
        CancellationToken ct = default)
    {
        var key = dialect == DatabaseDialect.SqlServer ? "sqlserver" : "postgresql";
        var extractor = _services.GetRequiredKeyedService<ISchemaExtractor>(key);
        return await extractor.ExtractAsync(connectionString, options, ct);
    }

    public async Task<SchemaModel> ExtractFromFileAsync(
        string sqlContent,
        DatabaseDialect dialect,
        ExtractionOptions options,
        CancellationToken ct = default)
    {
        var key = dialect == DatabaseDialect.SqlServer ? "sqlserver" : "postgresql";
        var parser = _services.GetRequiredKeyedService<ISqlFileParser>(key);
        return await parser.ParseAsync(sqlContent, options, ct);
    }

    public async Task<bool> TestConnectionAsync(
        string connectionString,
        DatabaseDialect dialect,
        CancellationToken ct = default)
    {
        var key = dialect == DatabaseDialect.SqlServer ? "sqlserver" : "postgresql";
        var extractor = _services.GetRequiredKeyedService<ISchemaExtractor>(key);
        return await extractor.TestConnectionAsync(connectionString, ct);
    }

    public async Task<IReadOnlyList<string>> ListObjectsAsync(
        string connectionString,
        DatabaseDialect dialect,
        string objectType,
        CancellationToken ct = default)
    {
        var key = dialect == DatabaseDialect.SqlServer ? "sqlserver" : "postgresql";
        var extractor = _services.GetRequiredKeyedService<ISchemaExtractor>(key);

        return objectType.ToLower() switch
        {
            "tables" => await extractor.ListTablesAsync(connectionString, ct),
            "views" => await extractor.ListViewsAsync(connectionString, ct),
            "procedures" => await extractor.ListProceduresAsync(connectionString, ct),
            "functions" => await extractor.ListFunctionsAsync(connectionString, ct),
            _ => Array.Empty<string>()
        };
    }

    public async Task<ConversionResult> ConvertAsync(
        SchemaModel source,
        ConversionDirection direction,
        ConversionOptions options,
        CancellationToken ct = default)
    {
        ISchemaConverter converter = direction == ConversionDirection.SqlServerToPostgreSQL
            ? _services.GetRequiredService<SqlServerToPostgreSqlConverter>()
            : _services.GetRequiredService<PostgreSqlToSqlServerConverter>();

        return await converter.ConvertAsync(source, options, ct);
    }

    public DiffReport Compare(SchemaModel source, SchemaModel target) =>
        _differ.Compare(source, target);

    public string GenerateSyncScript(DiffReport report) =>
        _syncScriptGenerator.Generate(report);

    public async Task<ExecutionReport> DryRunAsync(
        string connectionString,
        DatabaseDialect dialect,
        string script,
        CancellationToken ct = default)
    {
        var key = dialect == DatabaseDialect.SqlServer ? "sqlserver" : "postgresql";
        var engine = _services.GetRequiredKeyedService<IExecutionEngine>(key);
        return await engine.DryRunAsync(connectionString, script, dialect, ct);
    }

    public async Task<ExecutionReport> ExecuteAsync(
        string connectionString,
        DatabaseDialect dialect,
        string script,
        CancellationToken ct = default)
    {
        var key = dialect == DatabaseDialect.SqlServer ? "sqlserver" : "postgresql";
        var engine = _services.GetRequiredKeyedService<IExecutionEngine>(key);
        return await engine.ExecuteAsync(connectionString, script, dialect, ct);
    }

    public string GenerateFileContent(
        string script,
        string sourceDescription,
        string targetDescription,
        DatabaseDialect targetDialect) =>
        _fileGenerator.Generate(script, sourceDescription, targetDescription, targetDialect);
}
