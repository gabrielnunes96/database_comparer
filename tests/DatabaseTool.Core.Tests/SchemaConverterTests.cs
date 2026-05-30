using DatabaseTool.Core.Conversion;
using DatabaseTool.Core.Interfaces;
using DatabaseTool.Core.Models;

namespace DatabaseTool.Core.Tests;

/// <summary>
/// Tests for bidirectional schema converters.
/// Verifies DDL output for tables, columns, identity, primary keys, and foreign keys.
/// </summary>
public class SchemaConverterTests
{
    private static SchemaModel MakeSqlServerSchema(params TableModel[] tables)
    {
        var m = new SchemaModel { DatabaseName = "test", Dialect = DatabaseDialect.SqlServer };
        m.Tables.AddRange(tables);
        return m;
    }

    private static SchemaModel MakePostgresSchema(params TableModel[] tables)
    {
        var m = new SchemaModel { DatabaseName = "test", Dialect = DatabaseDialect.PostgreSQL };
        m.Tables.AddRange(tables);
        return m;
    }

    private static TableModel SimpleTable(string name, string schema = "dbo")
    {
        var t = new TableModel { Name = name, Schema = schema };
        t.Columns.Add(new ColumnModel { Name = "Id", DataType = "int", IsIdentity = true, IsNullable = false });
        t.Columns.Add(new ColumnModel { Name = "Name", DataType = "nvarchar", MaxLength = 200, IsNullable = false });
        t.Columns.Add(new ColumnModel { Name = "CreatedAt", DataType = "datetime", IsNullable = true });
        t.PrimaryKey = new PrimaryKeyModel { Name = "PK_" + name, Columns = new List<string> { "Id" } };
        return t;
    }

    private static ConversionOptions DefaultOptions(string targetSchema = "public") => new()
    {
        TargetSchema = targetSchema,
        UseIfNotExists = false,
        IncludeDropStatements = false,
        TypeMapping = TypeMappingConfig.Default
    };

    // ─── SQL Server → PostgreSQL ─────────────────────────────────────────────

    [Fact]
    public async Task SsToPostgreSQL_GeneratesCreateTable()
    {
        var converter = new SqlServerToPostgreSqlConverter();
        var source = MakeSqlServerSchema(SimpleTable("Orders"));

        var result = await converter.ConvertAsync(source, DefaultOptions(), CancellationToken.None);

        Assert.False(result.HasErrors);
        Assert.Contains("CREATE TABLE", result.Script, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("orders", result.Script, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task SsToPostgreSQL_IdentityColumn_ConvertedToGenerated()
    {
        var converter = new SqlServerToPostgreSqlConverter();
        var source = MakeSqlServerSchema(SimpleTable("Orders"));

        var result = await converter.ConvertAsync(source, DefaultOptions(), CancellationToken.None);

        // int IDENTITY → integer GENERATED ALWAYS AS IDENTITY
        Assert.Contains("GENERATED ALWAYS AS IDENTITY", result.Script, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task SsToPostgreSQL_NVarcharColumn_ConvertedToVarchar()
    {
        var converter = new SqlServerToPostgreSqlConverter();
        var source = MakeSqlServerSchema(SimpleTable("Orders"));

        var result = await converter.ConvertAsync(source, DefaultOptions(), CancellationToken.None);

        Assert.Contains("varchar", result.Script, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("nvarchar", result.Script, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task SsToPostgreSQL_DatetimeColumn_ConvertedToTimestamp()
    {
        var converter = new SqlServerToPostgreSqlConverter();
        var source = MakeSqlServerSchema(SimpleTable("Orders"));

        var result = await converter.ConvertAsync(source, DefaultOptions(), CancellationToken.None);

        Assert.Contains("timestamp", result.Script, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task SsToPostgreSQL_PrimaryKey_Included()
    {
        var converter = new SqlServerToPostgreSqlConverter();
        var source = MakeSqlServerSchema(SimpleTable("Orders"));

        var result = await converter.ConvertAsync(source, DefaultOptions(), CancellationToken.None);

        Assert.Contains("PRIMARY KEY", result.Script, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task SsToPostgreSQL_UseIfNotExists_AppendsClause()
    {
        var converter = new SqlServerToPostgreSqlConverter();
        var source = MakeSqlServerSchema(SimpleTable("Orders"));
        var options = DefaultOptions();
        options.UseIfNotExists = true;

        var result = await converter.ConvertAsync(source, options, CancellationToken.None);

        Assert.Contains("IF NOT EXISTS", result.Script, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task SsToPostgreSQL_CustomTargetSchema_Applied()
    {
        var converter = new SqlServerToPostgreSqlConverter();
        var source = MakeSqlServerSchema(SimpleTable("Orders"));
        var options = DefaultOptions("myschema");

        var result = await converter.ConvertAsync(source, options, CancellationToken.None);

        Assert.Contains("myschema.", result.Script, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task SsToPostgreSQL_ForeignKey_Included()
    {
        var converter = new SqlServerToPostgreSqlConverter();
        var table = SimpleTable("OrderItems");
        table.ForeignKeys.Add(new ForeignKeyModel
        {
            Name = "FK_OrderItems_Orders",
            Columns = new List<string> { "OrderId" },
            ReferencedTable = "Orders",
            ReferencedColumns = new List<string> { "Id" }
        });
        table.Columns.Add(new ColumnModel { Name = "OrderId", DataType = "int", IsNullable = false });

        var source = MakeSqlServerSchema(table);
        var result = await converter.ConvertAsync(source, DefaultOptions(), CancellationToken.None);

        Assert.Contains("REFERENCES", result.Script, StringComparison.OrdinalIgnoreCase);
    }

    // ─── PostgreSQL → SQL Server ─────────────────────────────────────────────

    [Fact]
    public async Task PgToSqlServer_GeneratesCreateTable()
    {
        var converter = new PostgreSqlToSqlServerConverter();
        var table = new TableModel { Name = "products", Schema = "public" };
        table.Columns.Add(new ColumnModel { Name = "id", DataType = "integer", IsNullable = false, IsIdentity = true });
        table.Columns.Add(new ColumnModel { Name = "name", DataType = "varchar", MaxLength = 255, IsNullable = false });
        table.Columns.Add(new ColumnModel { Name = "price", DataType = "numeric", IsNullable = true });
        table.PrimaryKey = new PrimaryKeyModel { Name = "pk_products", Columns = new List<string> { "id" } };

        var source = MakePostgresSchema(table);
        var options = new ConversionOptions
        {
            TargetSchema = "dbo",
            TypeMapping = TypeMappingConfig.Default
        };

        var result = await converter.ConvertAsync(source, options, CancellationToken.None);

        Assert.False(result.HasErrors);
        Assert.Contains("CREATE TABLE", result.Script, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("products", result.Script, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task PgToSqlServer_BooleanColumn_ConvertedToBit()
    {
        var converter = new PostgreSqlToSqlServerConverter();
        var table = new TableModel { Name = "flags", Schema = "public" };
        table.Columns.Add(new ColumnModel { Name = "active", DataType = "boolean", IsNullable = false });
        var source = MakePostgresSchema(table);

        var result = await converter.ConvertAsync(source, new ConversionOptions
        {
            TargetSchema = "dbo",
            TypeMapping = TypeMappingConfig.Default
        }, CancellationToken.None);

        Assert.Contains("bit", result.Script, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task PgToSqlServer_UuidColumn_ConvertedToUniqueIdentifier()
    {
        var converter = new PostgreSqlToSqlServerConverter();
        var table = new TableModel { Name = "items", Schema = "public" };
        table.Columns.Add(new ColumnModel { Name = "item_id", DataType = "uuid", IsNullable = false });
        var source = MakePostgresSchema(table);

        var result = await converter.ConvertAsync(source, new ConversionOptions
        {
            TargetSchema = "dbo",
            TypeMapping = TypeMappingConfig.Default
        }, CancellationToken.None);

        Assert.Contains("uniqueidentifier", result.Script, StringComparison.OrdinalIgnoreCase);
    }

    // ─── Empty input ─────────────────────────────────────────────────────────

    [Fact]
    public async Task SsToPostgreSQL_EmptySchema_ReturnsEmptyScript()
    {
        var converter = new SqlServerToPostgreSqlConverter();
        var source = MakeSqlServerSchema();

        var result = await converter.ConvertAsync(source, DefaultOptions(), CancellationToken.None);

        Assert.False(result.HasErrors);
        Assert.True(string.IsNullOrWhiteSpace(result.Script) || !result.Script.Contains("CREATE TABLE"));
    }
}
