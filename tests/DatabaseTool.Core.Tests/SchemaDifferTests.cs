using DatabaseTool.Core.Diff;
using DatabaseTool.Core.Models;

namespace DatabaseTool.Core.Tests;

/// <summary>
/// Tests for SchemaDiffer — verifies detection of added, removed, and modified objects.
/// </summary>
public class SchemaDifferTests
{
    private readonly SchemaDiffer _differ = new();

    private static SchemaModel EmptyModel(string name = "A") => new()
    {
        DatabaseName = name,
        Dialect = DatabaseDialect.SqlServer
    };

    private static TableModel MakeTable(string name, params (string col, string type)[] columns)
    {
        var t = new TableModel { Name = name };
        foreach (var (col, type) in columns)
            t.Columns.Add(new ColumnModel { Name = col, DataType = type });
        return t;
    }

    // ─── Identical schemas ───────────────────────────────────────────────────

    [Fact]
    public void Compare_IdenticalEmptySchemas_HasNoDifferences()
    {
        var report = _differ.Compare(EmptyModel(), EmptyModel());
        Assert.False(report.HasDifferences);
        Assert.Equal(0, report.TotalDifferences);
    }

    [Fact]
    public void Compare_IdenticalTables_HasNoDifferences()
    {
        var src = EmptyModel();
        src.Tables.Add(MakeTable("Orders", ("Id", "int"), ("Total", "decimal")));

        var tgt = EmptyModel();
        tgt.Tables.Add(MakeTable("Orders", ("Id", "int"), ("Total", "decimal")));

        var report = _differ.Compare(src, tgt);
        Assert.False(report.HasDifferences);
    }

    // ─── Table presence ──────────────────────────────────────────────────────

    [Fact]
    public void Compare_TableOnlyInSource_AppearsInOnlyInSource()
    {
        var src = EmptyModel(); src.Tables.Add(MakeTable("NewTable"));
        var tgt = EmptyModel();

        var report = _differ.Compare(src, tgt);
        Assert.Contains("NewTable", report.Tables.OnlyInSource);
        Assert.Empty(report.Tables.OnlyInTarget);
    }

    [Fact]
    public void Compare_TableOnlyInTarget_AppearsInOnlyInTarget()
    {
        var src = EmptyModel();
        var tgt = EmptyModel(); tgt.Tables.Add(MakeTable("OldTable"));

        var report = _differ.Compare(src, tgt);
        Assert.Contains("OldTable", report.Tables.OnlyInTarget);
        Assert.Empty(report.Tables.OnlyInSource);
    }

    // ─── Column differences ──────────────────────────────────────────────────

    [Fact]
    public void Compare_AddedColumn_DetectedAsDifferent()
    {
        var src = EmptyModel();
        src.Tables.Add(MakeTable("Orders", ("Id", "int"), ("NewCol", "varchar")));

        var tgt = EmptyModel();
        tgt.Tables.Add(MakeTable("Orders", ("Id", "int")));

        var report = _differ.Compare(src, tgt);
        Assert.Single(report.Tables.Different);
        var diff = report.Tables.Different[0];
        Assert.Equal("Orders", diff.TableName);
        Assert.Contains(diff.ColumnDiffs, c => c.ColumnName == "NewCol" && c.DiffType == DiffType.Added);
    }

    [Fact]
    public void Compare_RemovedColumn_DetectedAsDifferent()
    {
        var src = EmptyModel();
        src.Tables.Add(MakeTable("Orders", ("Id", "int")));

        var tgt = EmptyModel();
        tgt.Tables.Add(MakeTable("Orders", ("Id", "int"), ("OldCol", "nvarchar")));

        var report = _differ.Compare(src, tgt);
        var diff = report.Tables.Different[0];
        Assert.Contains(diff.ColumnDiffs, c => c.ColumnName == "OldCol" && c.DiffType == DiffType.Removed);
    }

    [Fact]
    public void Compare_TypeChanged_DetectedAsModified()
    {
        var src = EmptyModel();
        src.Tables.Add(MakeTable("Orders", ("Amount", "int")));

        var tgt = EmptyModel();
        tgt.Tables.Add(MakeTable("Orders", ("Amount", "decimal")));

        var report = _differ.Compare(src, tgt);
        var colDiff = report.Tables.Different[0].ColumnDiffs
            .FirstOrDefault(c => c.ColumnName == "Amount");
        Assert.NotNull(colDiff);
        Assert.Equal(DiffType.Modified, colDiff.DiffType);
    }

    // ─── Views / Procedures / Functions / Triggers ───────────────────────────

    [Fact]
    public void Compare_ViewOnlyInSource()
    {
        var src = EmptyModel();
        src.Views.Add(new ViewModel { Name = "vw_Active", Schema = "dbo", Definition = "SELECT 1" });
        var tgt = EmptyModel();

        var report = _differ.Compare(src, tgt);
        Assert.Contains("dbo.vw_Active", report.Views.OnlyInSource);
    }

    [Fact]
    public void Compare_ViewDefinitionChanged()
    {
        var src = EmptyModel();
        src.Views.Add(new ViewModel { Name = "vw_Active", Schema = "dbo", Definition = "SELECT 1 FROM A" });
        var tgt = EmptyModel();
        tgt.Views.Add(new ViewModel { Name = "vw_Active", Schema = "dbo", Definition = "SELECT 2 FROM A" });

        var report = _differ.Compare(src, tgt);
        Assert.Single(report.Views.Different);
        Assert.True(report.Views.Different[0].DefinitionChanged);
    }

    [Fact]
    public void Compare_TotalDifferences_SumsAllSections()
    {
        var src = EmptyModel();
        src.Tables.Add(MakeTable("T1"));
        src.Views.Add(new ViewModel { Name = "V1", Schema = "dbo", Definition = "X" });

        var tgt = EmptyModel();

        var report = _differ.Compare(src, tgt);
        Assert.Equal(2, report.TotalDifferences);
        Assert.True(report.HasDifferences);
    }

    // ─── Case insensitivity ──────────────────────────────────────────────────

    [Fact]
    public void Compare_TableNames_AreCaseInsensitive()
    {
        var src = EmptyModel();
        src.Tables.Add(MakeTable("orders"));

        var tgt = EmptyModel();
        tgt.Tables.Add(MakeTable("ORDERS"));

        var report = _differ.Compare(src, tgt);
        Assert.Empty(report.Tables.OnlyInSource);
        Assert.Empty(report.Tables.OnlyInTarget);
    }
}
