using System.Text;
using DatabaseTool.Core.Models;

namespace DatabaseTool.Core.Execution;

public class FileGenerator
{
    /// <summary>
    /// Generates a UTF-8 encoded .sql file content with a metadata header.
    /// </summary>
    public string Generate(
        string script,
        string sourceDescription,
        string targetDescription,
        DatabaseDialect targetDialect,
        IEnumerable<string>? includedObjects = null)
    {
        var sb = new StringBuilder();

        sb.AppendLine("-- ============================================================");
        sb.AppendLine("-- Database Schema Tool - Generated Script");
        sb.AppendLine($"-- Date: {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC");
        sb.AppendLine($"-- Source: {sourceDescription}");
        sb.AppendLine($"-- Target: {targetDescription}");
        sb.AppendLine($"-- Dialect: {targetDialect}");

        if (includedObjects?.Any() == true)
        {
            sb.AppendLine("-- Objects:");
            foreach (var obj in includedObjects)
                sb.AppendLine($"--   {obj}");
        }

        sb.AppendLine("-- ============================================================");
        sb.AppendLine();
        sb.Append(script);

        return sb.ToString();
    }

    /// <summary>
    /// Writes the script to a file and returns the file path.
    /// </summary>
    public async Task<string> WriteToFileAsync(
        string script,
        string outputDirectory,
        string? fileNamePrefix = null,
        CancellationToken cancellationToken = default)
    {
        Directory.CreateDirectory(outputDirectory);
        var prefix = fileNamePrefix ?? "schema_script";
        var fileName = $"{prefix}_{DateTime.UtcNow:yyyyMMdd_HHmmss}.sql";
        var filePath = Path.Combine(outputDirectory, fileName);

        await File.WriteAllTextAsync(filePath, script, Encoding.UTF8, cancellationToken);
        return filePath;
    }
}
