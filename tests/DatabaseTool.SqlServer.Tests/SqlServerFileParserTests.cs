using DatabaseTool.Core.Models;
using DatabaseTool.SqlServer;

namespace DatabaseTool.SqlServer.Tests;

/// <summary>
/// Tests for SqlServerFileParser — verifies that T-SQL DDL files are parsed
/// correctly into the internal SchemaModel using ScriptDom.
/// </summary>
public class SqlServerFileParserTests
{
    private readonly SqlServerFileParser _parser = new();

    private static Task<SchemaModel> Parse(string sql) =>
        new SqlServerFileParser().ParseAsync(sql, ExtractionOptions.All);

    // ─── Tables ──────────────────────────────────────────────────────────────

    [Fact]
    public async Task Parse_SimpleCreateTable_ExtractsTable()
    {
        const string sql = """
            CREATE TABLE dbo.Orders (
                Id       INT         NOT NULL,
                Name     NVARCHAR(200) NOT NULL,
                Total    DECIMAL(18,2) NULL
            );
            """;

        var model = await Parse(sql);

        var table = Assert.Single(model.Tables);
        Assert.Equal("Orders", table.Name);
        Assert.Equal(3, table.Columns.Count);
    }

    [Fact]
    public async Task Parse_CreateTable_ColumnsHaveCorrectTypes()
    {
        const string sql = """
            CREATE TABLE dbo.Products (
                Id       INT           NOT NULL,
                Name     NVARCHAR(100) NOT NULL,
                Price    MONEY         NULL,
                Active   BIT           NOT NULL
            );
            """;

        var model = await Parse(sql);
        var cols = model.Tables[0].Columns.ToDictionary(c => c.Name, StringComparer.OrdinalIgnoreCase);

        Assert.Equal("int",      cols["Id"].DataType,     StringComparer.OrdinalIgnoreCase);
        Assert.Equal("nvarchar", cols["Name"].DataType,   StringComparer.OrdinalIgnoreCase);
        Assert.Equal("money",    cols["Price"].DataType,  StringComparer.OrdinalIgnoreCase);
        Assert.Equal("bit",      cols["Active"].DataType, StringComparer.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Parse_CreateTable_NullabilityExtracted()
    {
        const string sql = """
            CREATE TABLE dbo.Items (
                Required NVARCHAR(50) NOT NULL,
                Optional NVARCHAR(50) NULL
            );
            """;

        var model = await Parse(sql);
        var cols = model.Tables[0].Columns.ToDictionary(c => c.Name, StringComparer.OrdinalIgnoreCase);

        Assert.False(cols["Required"].IsNullable);
        Assert.True(cols["Optional"].IsNullable);
    }

    [Fact]
    public async Task Parse_CreateTable_IdentityColumnDetected()
    {
        const string sql = """
            CREATE TABLE dbo.Users (
                Id   INT IDENTITY(1,1) NOT NULL,
                Name NVARCHAR(100)     NOT NULL
            );
            """;

        var model = await Parse(sql);
        var idCol = model.Tables[0].Columns.First(c => c.Name == "Id");

        Assert.True(idCol.IsIdentity);
    }

    [Fact]
    public async Task Parse_CreateTable_PrimaryKeyConstraint()
    {
        const string sql = """
            CREATE TABLE dbo.Orders (
                Id   INT NOT NULL,
                CONSTRAINT PK_Orders PRIMARY KEY (Id)
            );
            """;

        var model = await Parse(sql);
        Assert.NotNull(model.Tables[0].PrimaryKey);
        Assert.Contains("Id", model.Tables[0].PrimaryKey!.Columns);
    }

    [Fact]
    public async Task Parse_CreateTable_ForeignKey()
    {
        const string sql = """
            CREATE TABLE dbo.OrderItems (
                Id      INT NOT NULL,
                OrderId INT NOT NULL,
                CONSTRAINT FK_Items_Orders FOREIGN KEY (OrderId) REFERENCES dbo.Orders (Id)
            );
            """;

        var model = await Parse(sql);
        Assert.Single(model.Tables[0].ForeignKeys);
        var fk = model.Tables[0].ForeignKeys[0];
        Assert.Equal("OrderId", fk.Columns[0]);
        Assert.Contains("Orders", fk.ReferencedTable, StringComparison.OrdinalIgnoreCase);
    }

    // ─── Multiple tables ─────────────────────────────────────────────────────

    [Fact]
    public async Task Parse_MultipleTables_AllExtracted()
    {
        const string sql = """
            CREATE TABLE dbo.Customers (Id INT NOT NULL);
            GO
            CREATE TABLE dbo.Orders (Id INT NOT NULL, CustomerId INT NOT NULL);
            """;

        var model = await Parse(sql);
        Assert.Equal(2, model.Tables.Count);
    }

    // ─── Views ───────────────────────────────────────────────────────────────

    [Fact]
    public async Task Parse_CreateView_ExtractsView()
    {
        const string sql = """
            CREATE VIEW dbo.vw_ActiveOrders AS
            SELECT Id, Total FROM dbo.Orders WHERE IsActive = 1;
            """;

        var model = await Parse(sql);
        var view = Assert.Single(model.Views);
        Assert.Equal("vw_ActiveOrders", view.Name);
        Assert.False(string.IsNullOrEmpty(view.Definition));
    }

    // ─── Stored Procedures ───────────────────────────────────────────────────

    [Fact]
    public async Task Parse_CreateProcedure_ExtractsProcedure()
    {
        const string sql = """
            CREATE PROCEDURE dbo.GetOrder
                @OrderId INT
            AS
            BEGIN
                SELECT * FROM dbo.Orders WHERE Id = @OrderId;
            END
            """;

        var model = await Parse(sql);
        var proc = Assert.Single(model.Procedures);
        Assert.Equal("GetOrder", proc.Name);
        Assert.False(string.IsNullOrEmpty(proc.Definition));
    }

    // ─── Empty input ─────────────────────────────────────────────────────────

    [Fact]
    public async Task Parse_EmptyInput_ReturnsEmptyModel()
    {
        var model = await Parse(string.Empty);
        Assert.Empty(model.Tables);
        Assert.Empty(model.Views);
        Assert.Empty(model.Procedures);
    }

    [Fact]
    public async Task Parse_WhitespaceOnly_ReturnsEmptyModel()
    {
        var model = await Parse("   \n\t\n   ");
        Assert.Empty(model.Tables);
    }
}
