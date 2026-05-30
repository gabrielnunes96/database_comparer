using DatabaseTool.Core.Models;
using DatabaseTool.PostgreSQL;

namespace DatabaseTool.PostgreSQL.Tests;

/// <summary>
/// Tests for PostgreSqlFileParser — verifies that PostgreSQL DDL files are parsed
/// correctly into the internal SchemaModel using pgsqlparser.
/// </summary>
public class PostgreSqlFileParserTests
{
    private static Task<SchemaModel> Parse(string sql) =>
        new PostgreSqlFileParser().ParseAsync(sql, ExtractionOptions.All);

    // ─── Tables ──────────────────────────────────────────────────────────────

    [Fact]
    public async Task Parse_SimpleCreateTable_ExtractsTable()
    {
        const string sql = """
            CREATE TABLE public.orders (
                id      INTEGER   NOT NULL,
                name    VARCHAR(200) NOT NULL,
                total   NUMERIC(18,2)
            );
            """;

        var model = await Parse(sql);

        var table = Assert.Single(model.Tables);
        Assert.Equal("orders", table.Name);
        Assert.Equal(3, table.Columns.Count);
    }

    [Fact]
    public async Task Parse_CreateTable_ColumnsHaveCorrectTypes()
    {
        const string sql = """
            CREATE TABLE public.products (
                id      INTEGER       NOT NULL,
                label   VARCHAR(100)  NOT NULL,
                price   NUMERIC       NULL,
                active  BOOLEAN       NOT NULL
            );
            """;

        var model = await Parse(sql);
        var cols = model.Tables[0].Columns.ToDictionary(c => c.Name, StringComparer.OrdinalIgnoreCase);

        Assert.True(cols.ContainsKey("id"));
        Assert.True(cols.ContainsKey("label"));
        Assert.True(cols.ContainsKey("price"));
        Assert.True(cols.ContainsKey("active"));
    }

    [Fact]
    public async Task Parse_CreateTable_NullabilityExtracted()
    {
        const string sql = """
            CREATE TABLE public.items (
                required VARCHAR(50) NOT NULL,
                optional VARCHAR(50)
            );
            """;

        var model = await Parse(sql);
        var cols = model.Tables[0].Columns.ToDictionary(c => c.Name, StringComparer.OrdinalIgnoreCase);

        Assert.False(cols["required"].IsNullable);
        // PostgreSQL columns without NOT NULL are nullable by default
        Assert.True(cols["optional"].IsNullable);
    }

    [Fact]
    public async Task Parse_CreateTable_PrimaryKeyConstraint()
    {
        const string sql = """
            CREATE TABLE public.users (
                id   INTEGER NOT NULL,
                CONSTRAINT pk_users PRIMARY KEY (id)
            );
            """;

        var model = await Parse(sql);
        Assert.NotNull(model.Tables[0].PrimaryKey);
        Assert.Contains("id", model.Tables[0].PrimaryKey!.Columns, StringComparer.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Parse_CreateTable_ForeignKeyConstraint()
    {
        const string sql = """
            CREATE TABLE public.order_items (
                id       INTEGER NOT NULL,
                order_id INTEGER NOT NULL,
                CONSTRAINT fk_items_orders FOREIGN KEY (order_id) REFERENCES public.orders (id)
            );
            """;

        var model = await Parse(sql);
        Assert.NotEmpty(model.Tables[0].ForeignKeys);
        var fk = model.Tables[0].ForeignKeys[0];
        Assert.Contains("order_id", fk.Columns, StringComparer.OrdinalIgnoreCase);
    }

    // ─── Multiple tables ─────────────────────────────────────────────────────

    [Fact]
    public async Task Parse_MultipleTables_AllExtracted()
    {
        const string sql = """
            CREATE TABLE public.customers (id INTEGER NOT NULL);
            CREATE TABLE public.orders (id INTEGER NOT NULL, customer_id INTEGER NOT NULL);
            """;

        var model = await Parse(sql);
        Assert.Equal(2, model.Tables.Count);
    }

    // ─── Views ───────────────────────────────────────────────────────────────

    [Fact]
    public async Task Parse_CreateView_ExtractsView()
    {
        const string sql = """
            CREATE VIEW public.vw_active_orders AS
            SELECT id, total FROM public.orders WHERE is_active = TRUE;
            """;

        var model = await Parse(sql);
        var view = Assert.Single(model.Views);
        Assert.Equal("vw_active_orders", view.Name);
        Assert.False(string.IsNullOrEmpty(view.Definition));
    }

    // ─── Functions ───────────────────────────────────────────────────────────

    [Fact]
    public async Task Parse_CreateFunction_ExtractsFunction()
    {
        const string sql = """
            CREATE FUNCTION public.get_total(p_id INTEGER)
            RETURNS NUMERIC
            LANGUAGE sql
            AS $$
                SELECT total FROM public.orders WHERE id = p_id;
            $$;
            """;

        var model = await Parse(sql);
        var fn = Assert.Single(model.Functions);
        Assert.Equal("get_total", fn.Name);
    }

    // ─── Empty input ─────────────────────────────────────────────────────────

    [Fact]
    public async Task Parse_EmptyInput_ReturnsEmptyModel()
    {
        var model = await Parse(string.Empty);
        Assert.Empty(model.Tables);
        Assert.Empty(model.Views);
        Assert.Empty(model.Functions);
    }

    [Fact]
    public async Task Parse_WhitespaceOnly_ReturnsEmptyModel()
    {
        var model = await Parse("   \n\t   ");
        Assert.Empty(model.Tables);
    }
}
