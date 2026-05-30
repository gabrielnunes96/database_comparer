using DatabaseTool.Core.Interfaces;
using DatabaseTool.Core.Models;

namespace DatabaseTool.Core.Diff;

public class SchemaDiffer : ISchemaDiffer
{
    public DiffReport Compare(SchemaModel source, SchemaModel target)
    {
        var report = new DiffReport
        {
            Source = source,
            Target = target,
            GeneratedAt = DateTime.UtcNow
        };

        report.Tables = CompareTables(source.Tables, target.Tables);
        report.Views = CompareObjects(
            source.Views.Select(v => (v.FullName, v.Definition)),
            target.Views.Select(v => (v.FullName, v.Definition)));
        report.Procedures = CompareObjects(
            source.Procedures.Select(p => (p.FullName, p.Definition)),
            target.Procedures.Select(p => (p.FullName, p.Definition)));
        report.Functions = CompareObjects(
            source.Functions.Select(f => (f.FullName, f.Definition)),
            target.Functions.Select(f => (f.FullName, f.Definition)));
        report.Triggers = CompareObjects(
            source.Triggers.Select(t => (t.FullName, t.Definition)),
            target.Triggers.Select(t => (t.FullName, t.Definition)));

        return report;
    }

    private static SectionDiff<TableDiff> CompareTables(List<TableModel> sourceTables, List<TableModel> targetTables)
    {
        var diff = new SectionDiff<TableDiff>();

        var sourceMap = sourceTables.ToDictionary(t => t.Name, StringComparer.OrdinalIgnoreCase);
        var targetMap = targetTables.ToDictionary(t => t.Name, StringComparer.OrdinalIgnoreCase);

        foreach (var name in sourceMap.Keys.Except(targetMap.Keys, StringComparer.OrdinalIgnoreCase))
            diff.OnlyInSource.Add(name);

        foreach (var name in targetMap.Keys.Except(sourceMap.Keys, StringComparer.OrdinalIgnoreCase))
            diff.OnlyInTarget.Add(name);

        foreach (var name in sourceMap.Keys.Intersect(targetMap.Keys, StringComparer.OrdinalIgnoreCase))
        {
            var tableDiff = CompareTable(sourceMap[name], targetMap[name]);
            if (tableDiff is not null)
                diff.Different.Add(tableDiff);
        }

        return diff;
    }

    private static TableDiff? CompareTable(TableModel source, TableModel target)
    {
        var diff = new TableDiff { TableName = source.Name };
        var hasChanges = false;

        var sourceColMap = source.Columns.ToDictionary(c => c.Name, StringComparer.OrdinalIgnoreCase);
        var targetColMap = target.Columns.ToDictionary(c => c.Name, StringComparer.OrdinalIgnoreCase);

        foreach (var colName in sourceColMap.Keys.Except(targetColMap.Keys, StringComparer.OrdinalIgnoreCase))
        {
            diff.ColumnDiffs.Add(new ColumnDiff { ColumnName = colName, DiffType = DiffType.Added });
            hasChanges = true;
        }

        foreach (var colName in targetColMap.Keys.Except(sourceColMap.Keys, StringComparer.OrdinalIgnoreCase))
        {
            diff.ColumnDiffs.Add(new ColumnDiff { ColumnName = colName, DiffType = DiffType.Removed });
            hasChanges = true;
        }

        foreach (var colName in sourceColMap.Keys.Intersect(targetColMap.Keys, StringComparer.OrdinalIgnoreCase))
        {
            var colDiff = CompareColumn(colName, sourceColMap[colName], targetColMap[colName]);
            if (colDiff is not null)
            {
                diff.ColumnDiffs.Add(colDiff);
                hasChanges = true;
            }
        }

        // PK
        var sourcePk = source.PrimaryKey?.Columns ?? new List<string>();
        var targetPk = target.PrimaryKey?.Columns ?? new List<string>();
        if (!sourcePk.SequenceEqual(targetPk, StringComparer.OrdinalIgnoreCase))
        {
            diff.PrimaryKeyChanged = true;
            hasChanges = true;
        }

        // Indexes
        var sourceIdxNames = source.Indexes.Select(i => i.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var targetIdxNames = target.Indexes.Select(i => i.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (var idx in sourceIdxNames.Except(targetIdxNames))
        {
            diff.IndexDiffs.Add($"Only in source: {idx}");
            hasChanges = true;
        }
        foreach (var idx in targetIdxNames.Except(sourceIdxNames))
        {
            diff.IndexDiffs.Add($"Only in target: {idx}");
            hasChanges = true;
        }

        return hasChanges ? diff : null;
    }

    private static ColumnDiff? CompareColumn(string colName, ColumnModel source, ColumnModel target)
    {
        var changes = new List<string>();

        if (!string.Equals(source.DataType, target.DataType, StringComparison.OrdinalIgnoreCase))
            changes.Add($"DataType: {source.DataType} → {target.DataType}");

        if (source.IsNullable != target.IsNullable)
            changes.Add($"Nullable: {source.IsNullable} → {target.IsNullable}");

        if (source.MaxLength != target.MaxLength)
            changes.Add($"MaxLength: {source.MaxLength} → {target.MaxLength}");

        if (!string.Equals(source.DefaultValue, target.DefaultValue, StringComparison.OrdinalIgnoreCase))
            changes.Add($"Default: {source.DefaultValue ?? "NULL"} → {target.DefaultValue ?? "NULL"}");

        if (source.IsIdentity != target.IsIdentity)
            changes.Add($"Identity: {source.IsIdentity} → {target.IsIdentity}");

        if (changes.Count == 0) return null;

        return new ColumnDiff
        {
            ColumnName = colName,
            DiffType = DiffType.Modified,
            Changes = changes
        };
    }

    private static SectionDiff<ObjectDiff> CompareObjects(
        IEnumerable<(string Name, string Definition)> sourceObjects,
        IEnumerable<(string Name, string Definition)> targetObjects)
    {
        var diff = new SectionDiff<ObjectDiff>();

        var sourceMap = sourceObjects.ToDictionary(o => o.Name, o => o.Definition, StringComparer.OrdinalIgnoreCase);
        var targetMap = targetObjects.ToDictionary(o => o.Name, o => o.Definition, StringComparer.OrdinalIgnoreCase);

        foreach (var name in sourceMap.Keys.Except(targetMap.Keys, StringComparer.OrdinalIgnoreCase))
            diff.OnlyInSource.Add(name);

        foreach (var name in targetMap.Keys.Except(sourceMap.Keys, StringComparer.OrdinalIgnoreCase))
            diff.OnlyInTarget.Add(name);

        foreach (var name in sourceMap.Keys.Intersect(targetMap.Keys, StringComparer.OrdinalIgnoreCase))
        {
            var sourceDef = NormalizeDefinition(sourceMap[name]);
            var targetDef = NormalizeDefinition(targetMap[name]);
            if (!string.Equals(sourceDef, targetDef, StringComparison.OrdinalIgnoreCase))
                diff.Different.Add(new ObjectDiff { ObjectName = name, DefinitionChanged = true });
        }

        return diff;
    }

    private static string NormalizeDefinition(string definition) =>
        System.Text.RegularExpressions.Regex.Replace(definition.Trim(), @"\s+", " ");
}
