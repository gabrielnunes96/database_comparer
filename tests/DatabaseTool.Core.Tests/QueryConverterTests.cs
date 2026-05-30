using DatabaseTool.Core.Conversion;

namespace DatabaseTool.Core.Tests;

/// <summary>
/// Tests for QueryConverter — verifies common function/syntax replacement in both directions.
/// </summary>
public class QueryConverterTests
{
    private readonly QueryConverter _converter = new();

    // ─── SQL Server → PostgreSQL ─────────────────────────────────────────────

    [Theory]
    [InlineData("SELECT GETDATE()",        "SELECT NOW()")]
    [InlineData("SELECT GETUTCDATE()",     "SELECT NOW() AT TIME ZONE 'UTC'")]
    [InlineData("SELECT LEN(col)",         "SELECT LENGTH(col)")]
    [InlineData("SELECT NEWID()",          "SELECT gen_random_uuid()")]
    [InlineData("SELECT ISNULL(a, b)",     "SELECT COALESCE(a, b)")]
    public void SsToPostgreSQL_SimpleReplacements(string input, string expected)
    {
        var result = _converter.ConvertSqlServerToPostgreSQL(input);
        Assert.Equal(expected, result.ConvertedQuery);
    }

    [Fact]
    public void SsToPostgreSQL_BracketIdentifiers_ConvertedToDoubleQuotes()
    {
        var result = _converter.ConvertSqlServerToPostgreSQL("SELECT [MyColumn] FROM [MyTable]");
        Assert.Contains("\"MyColumn\"", result.ConvertedQuery);
        Assert.Contains("\"MyTable\"", result.ConvertedQuery);
    }

    [Fact]
    public void SsToPostgreSQL_NPrefix_Removed()
    {
        var result = _converter.ConvertSqlServerToPostgreSQL("WHERE Name = N'Alice'");
        Assert.Contains("'Alice'", result.ConvertedQuery);
        Assert.DoesNotContain("N'", result.ConvertedQuery);
    }

    [Fact]
    public void SsToPostgreSQL_TopClause_Removed_WithWarning()
    {
        var result = _converter.ConvertSqlServerToPostgreSQL("SELECT TOP 10 * FROM Orders");
        Assert.DoesNotContain("TOP 10", result.ConvertedQuery);
        Assert.NotEmpty(result.Warnings);
    }

    [Fact]
    public void SsToPostgreSQL_NVarchar_ConvertedToVarchar()
    {
        var result = _converter.ConvertSqlServerToPostgreSQL("Name NVARCHAR(100)");
        Assert.Contains("VARCHAR(100)", result.ConvertedQuery, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void SsToPostgreSQL_UniqueIdentifier_ConvertedToUuid()
    {
        var result = _converter.ConvertSqlServerToPostgreSQL("Id UNIQUEIDENTIFIER");
        Assert.Contains("UUID", result.ConvertedQuery, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void SsToPostgreSQL_DATEDIFF_AddsWarning()
    {
        var result = _converter.ConvertSqlServerToPostgreSQL("SELECT DATEDIFF(day, StartDate, EndDate)");
        Assert.NotEmpty(result.Warnings);
    }

    // ─── PostgreSQL → SQL Server ─────────────────────────────────────────────

    [Theory]
    [InlineData("SELECT NOW()",             "SELECT GETDATE()")]
    [InlineData("SELECT CURRENT_TIMESTAMP", "SELECT GETDATE()")]
    [InlineData("SELECT LENGTH(col)",       "SELECT LEN(col)")]
    [InlineData("SELECT gen_random_uuid()", "SELECT NEWID()")]
    [InlineData("SELECT COALESCE(a, b)",    "SELECT ISNULL(a, b)")]
    public void PgToSqlServer_SimpleReplacements(string input, string expected)
    {
        var result = _converter.ConvertPostgreSQLToSqlServer(input);
        Assert.Equal(expected, result.ConvertedQuery);
    }

    [Fact]
    public void PgToSqlServer_DoubleQuoteIdentifiers_ConvertedToBrackets()
    {
        var result = _converter.ConvertPostgreSQLToSqlServer("SELECT \"my_column\" FROM \"my_table\"");
        Assert.Contains("[my_column]", result.ConvertedQuery);
        Assert.Contains("[my_table]", result.ConvertedQuery);
    }

    [Fact]
    public void PgToSqlServer_LIMIT_Removed_WithWarning()
    {
        var result = _converter.ConvertPostgreSQLToSqlServer("SELECT * FROM orders LIMIT 10");
        Assert.DoesNotContain("LIMIT 10", result.ConvertedQuery);
        Assert.NotEmpty(result.Warnings);
    }

    [Fact]
    public void PgToSqlServer_StringConcatenation_Replaced_WithWarning()
    {
        var result = _converter.ConvertPostgreSQLToSqlServer("SELECT first || ' ' || last");
        Assert.Contains("+", result.ConvertedQuery);
        Assert.NotEmpty(result.Warnings);
    }

    [Fact]
    public void PgToSqlServer_TypeCast_Removed()
    {
        var result = _converter.ConvertPostgreSQLToSqlServer("SELECT '2024-01-01'::date");
        Assert.DoesNotContain("::", result.ConvertedQuery);
    }

    [Fact]
    public void BothDirections_EmptyInput_ReturnsEmpty()
    {
        var r1 = _converter.ConvertSqlServerToPostgreSQL("");
        var r2 = _converter.ConvertPostgreSQLToSqlServer("");
        Assert.Equal("", r1.ConvertedQuery);
        Assert.Equal("", r2.ConvertedQuery);
    }
}
