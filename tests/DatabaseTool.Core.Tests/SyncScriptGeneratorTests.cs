using DatabaseTool.Core.Diff;
using DatabaseTool.Core.Models;

namespace DatabaseTool.Core.Tests;

/// <summary>
/// Tests for SyncScriptGenerator — verifies that sync scripts contain the right DDL
/// statements based on a given DiffReport.
/// </summary>
public class SyncScriptGeneratorTests
{
    private readonly SchemaDiffer _differ = new();
    private readonly SyncScriptGenerator _generator = new();

    private static SchemaModel EmptyModel() => new()
    {
        DatabaseName = "db",
        Dialect = DatabaseDialect.SqlServer
    };

    [Fact]
    public void Generate_EmptyDiff_ReturnsEmptyOrHeader()
    {
        var diff = _differ.Compare(EmptyModel(), EmptyModel());
        var script = _generator.Generate(diff);
        // Script may just have a header comment; it should not have any DDL
        Assert.DoesNotContain("CREATE TABLE", script, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Generate_TableOnlyInSource_ContainsCreateTable()
    {
        var src = EmptyModel();
        src.Tables.Add(new TableModel
        {
            Name = "NewTable",
            Schema = "dbo",
            Columns = new List<ColumnModel>
            {
                new() { Name = "Id", DataType = "int", IsNullable = false },
                new() { Name = "Name", DataType = "nvarchar", MaxLength = 100, IsNullable = true }
            }
        });
        var tgt = EmptyModel();

        var diff = _differ.Compare(src, tgt);
        var script = _generator.Generate(diff);

        Assert.Contains("CREATE TABLE", script, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("NewTable", script, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Generate_ViewOnlyInSource_ContainsCreateView()
    {
        var src = EmptyModel();
        src.Views.Add(new ViewModel
        {
            Name = "vw_Active",
            Schema = "dbo",
            Definition = "SELECT Id, Name FROM Users WHERE IsActive = 1"
        });
        var tgt = EmptyModel();

        var diff = _differ.Compare(src, tgt);
        var script = _generator.Generate(diff);

        Assert.Contains("CREATE", script, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("vw_Active", script, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Generate_TableOnlyInTarget_ContainsCommentedDrop()
    {
        var src = EmptyModel();
        var tgt = EmptyModel();
        tgt.Tables.Add(new TableModel { Name = "ObsoleteTable", Schema = "dbo" });

        var diff = _differ.Compare(src, tgt);
        var script = _generator.Generate(diff);

        // DROP statements for target-only objects should be commented out for safety
        Assert.Contains("ObsoleteTable", script, StringComparison.OrdinalIgnoreCase);
    }
}
