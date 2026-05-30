using System.Text;
using DatabaseTool.Core.Models;

namespace DatabaseTool.Core.Diff;

/// <summary>
/// Generates a SQL script to bring the target schema in sync with the source schema,
/// based on the diff report. Works for same-dialect comparisons.
/// For cross-dialect comparisons, use the converter instead.
/// </summary>
public class SyncScriptGenerator
{
    public string Generate(DiffReport report)
    {
        var sb = new StringBuilder();
        var dialect = report.Source.Dialect;

        sb.AppendLine("-- Sync Script: Apply changes from source to target");
        sb.AppendLine($"-- Source: {report.Source.SourceDescription}");
        sb.AppendLine($"-- Target: {report.Target.SourceDescription}");
        sb.AppendLine($"-- Generated: {report.GeneratedAt:yyyy-MM-dd HH:mm:ss} UTC");
        sb.AppendLine($"-- Dialect: {dialect}");
        sb.AppendLine();

        if (!report.HasDifferences)
        {
            sb.AppendLine("-- No differences found. Schemas are in sync.");
            return sb.ToString();
        }

        // Tables only in source (missing in target) → CREATE
        foreach (var tableName in report.Tables.OnlyInSource)
        {
            var table = report.Source.Tables.FirstOrDefault(t =>
                string.Equals(t.Name, tableName, StringComparison.OrdinalIgnoreCase));
            if (table is null) continue;

            sb.AppendLine($"-- Table '{tableName}' exists in source but not in target");
            sb.AppendLine(GenerateCreateTable(table, dialect));
        }

        // Tables only in target (extra in target) → DROP (with warning)
        foreach (var tableName in report.Tables.OnlyInTarget)
        {
            sb.AppendLine($"-- WARNING: Table '{tableName}' exists in target but not in source");
            sb.AppendLine(dialect == DatabaseDialect.SqlServer
                ? $"-- DROP TABLE [dbo].[{tableName}]; -- Uncomment to drop"
                : $"-- DROP TABLE IF EXISTS public.{tableName}; -- Uncomment to drop");
            sb.AppendLine();
        }

        // Tables with differences → ALTER
        foreach (var tableDiff in report.Tables.Different)
        {
            var sourceTable = report.Source.Tables.FirstOrDefault(t =>
                string.Equals(t.Name, tableDiff.TableName, StringComparison.OrdinalIgnoreCase));
            if (sourceTable is null) continue;

            sb.AppendLine($"-- Altering table '{tableDiff.TableName}'");
            foreach (var colDiff in tableDiff.ColumnDiffs)
            {
                sb.AppendLine(GenerateAlterColumn(colDiff, tableDiff.TableName, sourceTable, dialect));
            }
            sb.AppendLine();
        }

        // Views only in source
        foreach (var viewName in report.Views.OnlyInSource)
        {
            var view = report.Source.Views.FirstOrDefault(v =>
                string.Equals(v.FullName, viewName, StringComparison.OrdinalIgnoreCase));
            if (view is null) continue;
            sb.AppendLine($"-- View '{viewName}' exists in source but not in target");
            sb.AppendLine(GenerateCreateView(view, dialect));
        }

        // Views with definition changes
        foreach (var viewDiff in report.Views.Different)
        {
            var view = report.Source.Views.FirstOrDefault(v =>
                string.Equals(v.FullName, viewDiff.ObjectName, StringComparison.OrdinalIgnoreCase));
            if (view is null) continue;
            sb.AppendLine($"-- View '{viewDiff.ObjectName}' definition changed");
            sb.AppendLine(GenerateReplaceView(view, dialect));
        }

        // Procedures
        foreach (var procName in report.Procedures.OnlyInSource)
        {
            var proc = report.Source.Procedures.FirstOrDefault(p =>
                string.Equals(p.FullName, procName, StringComparison.OrdinalIgnoreCase));
            if (proc is null) continue;
            sb.AppendLine($"-- Procedure '{procName}' exists in source but not in target");
            sb.AppendLine(proc.Definition);
            sb.AppendLine(dialect == DatabaseDialect.SqlServer ? "GO" : string.Empty);
            sb.AppendLine();
        }

        foreach (var procDiff in report.Procedures.Different)
        {
            var proc = report.Source.Procedures.FirstOrDefault(p =>
                string.Equals(p.FullName, procDiff.ObjectName, StringComparison.OrdinalIgnoreCase));
            if (proc is null) continue;
            sb.AppendLine($"-- Procedure '{procDiff.ObjectName}' definition changed");
            sb.AppendLine(proc.Definition);
            sb.AppendLine(dialect == DatabaseDialect.SqlServer ? "GO" : string.Empty);
            sb.AppendLine();
        }

        // Functions
        foreach (var funcName in report.Functions.OnlyInSource)
        {
            var func = report.Source.Functions.FirstOrDefault(f =>
                string.Equals(f.FullName, funcName, StringComparison.OrdinalIgnoreCase));
            if (func is null) continue;
            sb.AppendLine($"-- Function '{funcName}' exists in source but not in target");
            sb.AppendLine(func.Definition);
            sb.AppendLine(dialect == DatabaseDialect.SqlServer ? "GO" : string.Empty);
            sb.AppendLine();
        }

        foreach (var funcDiff in report.Functions.Different)
        {
            var func = report.Source.Functions.FirstOrDefault(f =>
                string.Equals(f.FullName, funcDiff.ObjectName, StringComparison.OrdinalIgnoreCase));
            if (func is null) continue;
            sb.AppendLine($"-- Function '{funcDiff.ObjectName}' definition changed");
            sb.AppendLine(func.Definition);
            sb.AppendLine(dialect == DatabaseDialect.SqlServer ? "GO" : string.Empty);
            sb.AppendLine();
        }

        return sb.ToString();
    }

    private static string GenerateCreateTable(TableModel table, DatabaseDialect dialect)
    {
        var sb = new StringBuilder();

        if (dialect == DatabaseDialect.SqlServer)
        {
            sb.AppendLine($"CREATE TABLE [{table.Schema}].[{table.Name}] (");
            var cols = table.Columns.OrderBy(c => c.OrdinalPosition)
                .Select(c => $"    [{c.Name}] {c.DataType}{GetLengthSuffix(c)}{(c.IsIdentity ? $" IDENTITY({c.IdentitySeed ?? 1},{c.IdentityIncrement ?? 1})" : string.Empty)}{(c.IsNullable ? " NULL" : " NOT NULL")}");

            if (table.PrimaryKey is not null)
            {
                var pkCols = string.Join(", ", table.PrimaryKey.Columns.Select(c => $"[{c}]"));
                sb.AppendLine(string.Join(",\n", cols) + ",");
                sb.AppendLine($"    CONSTRAINT [{table.PrimaryKey.Name}] PRIMARY KEY ({pkCols})");
            }
            else
            {
                sb.AppendLine(string.Join(",\n", cols));
            }

            sb.AppendLine(");");
            sb.AppendLine("GO");
        }
        else
        {
            var pgSchema = table.Schema == "dbo" ? "public" : table.Schema.ToLowerInvariant();
            sb.AppendLine($"CREATE TABLE IF NOT EXISTS {pgSchema}.{table.Name.ToLowerInvariant()} (");
            var cols = table.Columns.OrderBy(c => c.OrdinalPosition)
                .Select(c => $"    {c.Name.ToLowerInvariant()} {c.DataType.ToUpperInvariant()}{GetLengthSuffix(c)}{(c.IsIdentity ? " GENERATED ALWAYS AS IDENTITY" : string.Empty)}{(c.IsNullable ? " NULL" : " NOT NULL")}");

            if (table.PrimaryKey is not null)
            {
                var pkCols = string.Join(", ", table.PrimaryKey.Columns.Select(c => c.ToLowerInvariant()));
                sb.AppendLine(string.Join(",\n", cols) + ",");
                sb.AppendLine($"    CONSTRAINT {table.PrimaryKey.Name.ToLowerInvariant()} PRIMARY KEY ({pkCols})");
            }
            else
            {
                sb.AppendLine(string.Join(",\n", cols));
            }

            sb.AppendLine(");");
        }

        sb.AppendLine();
        return sb.ToString();
    }

    private static string GenerateAlterColumn(ColumnDiff colDiff, string tableName, TableModel sourceTable, DatabaseDialect dialect)
    {
        var col = sourceTable.Columns.FirstOrDefault(c =>
            string.Equals(c.Name, colDiff.ColumnName, StringComparison.OrdinalIgnoreCase));

        if (colDiff.DiffType == DiffType.Added)
        {
            if (col is null) return string.Empty;
            return dialect == DatabaseDialect.SqlServer
                ? $"ALTER TABLE [{sourceTable.Schema}].[{tableName}] ADD [{col.Name}] {col.DataType}{GetLengthSuffix(col)}{(col.IsNullable ? " NULL" : " NOT NULL")};"
                : $"ALTER TABLE {tableName.ToLowerInvariant()} ADD COLUMN {col.Name.ToLowerInvariant()} {col.DataType.ToUpperInvariant()}{GetLengthSuffix(col)}{(col.IsNullable ? "" : " NOT NULL")};";
        }

        if (colDiff.DiffType == DiffType.Removed)
        {
            return dialect == DatabaseDialect.SqlServer
                ? $"-- ALTER TABLE [{sourceTable.Schema}].[{tableName}] DROP COLUMN [{colDiff.ColumnName}]; -- Uncomment to drop column"
                : $"-- ALTER TABLE {tableName.ToLowerInvariant()} DROP COLUMN {colDiff.ColumnName.ToLowerInvariant()}; -- Uncomment to drop column";
        }

        if (col is null) return string.Empty;

        // Modified
        return dialect == DatabaseDialect.SqlServer
            ? $"ALTER TABLE [{sourceTable.Schema}].[{tableName}] ALTER COLUMN [{col.Name}] {col.DataType}{GetLengthSuffix(col)}{(col.IsNullable ? " NULL" : " NOT NULL")};"
            : $"ALTER TABLE {tableName.ToLowerInvariant()} ALTER COLUMN {col.Name.ToLowerInvariant()} TYPE {col.DataType.ToUpperInvariant()}{GetLengthSuffix(col)};";
    }

    private static string GenerateCreateView(ViewModel view, DatabaseDialect dialect)
    {
        return dialect == DatabaseDialect.SqlServer
            ? $"CREATE VIEW [{view.Schema}].[{view.Name}] AS\n{view.Definition}\nGO\n"
            : $"CREATE OR REPLACE VIEW {view.Schema}.{view.Name} AS\n{view.Definition}\n";
    }

    private static string GenerateReplaceView(ViewModel view, DatabaseDialect dialect)
    {
        return dialect == DatabaseDialect.SqlServer
            ? $"CREATE OR ALTER VIEW [{view.Schema}].[{view.Name}] AS\n{view.Definition}\nGO\n"
            : $"CREATE OR REPLACE VIEW {view.Schema}.{view.Name} AS\n{view.Definition}\n";
    }

    private static string GetLengthSuffix(ColumnModel col)
    {
        if (col.MaxLength == -1) return "(MAX)";
        if (col.MaxLength.HasValue && col.MaxLength > 0)
        {
            if (col.Scale.HasValue) return $"({col.MaxLength},{col.Scale})";
            return $"({col.MaxLength})";
        }
        if (col.Precision.HasValue)
        {
            if (col.Scale.HasValue) return $"({col.Precision},{col.Scale})";
            return $"({col.Precision})";
        }
        return string.Empty;
    }
}
