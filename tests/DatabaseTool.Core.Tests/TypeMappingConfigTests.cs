using DatabaseTool.Core.Conversion;

namespace DatabaseTool.Core.Tests;

/// <summary>
/// Tests for TypeMappingConfig — verifies default mappings and custom overrides.
/// </summary>
public class TypeMappingConfigTests
{
    [Theory]
    [InlineData("int",              "integer")]
    [InlineData("bigint",           "bigint")]
    [InlineData("bit",              "boolean")]
    [InlineData("nvarchar",         "varchar")]
    [InlineData("datetime",         "timestamp")]
    [InlineData("datetime2",        "timestamp")]
    [InlineData("uniqueidentifier", "uuid")]
    [InlineData("money",            "numeric(19,4)")]
    [InlineData("varbinary",        "bytea")]
    [InlineData("image",            "bytea")]
    [InlineData("xml",              "xml")]
    public void Default_SqlServerToPostgreSQL_MapsCorrectly(string sqlType, string expectedPg)
    {
        var config = TypeMappingConfig.Default;
        Assert.Equal(expectedPg, config.MapSqlServerToPostgreSQL(sqlType));
    }

    [Theory]
    [InlineData("integer",    "int")]
    [InlineData("boolean",    "bit")]
    [InlineData("uuid",       "uniqueidentifier")]
    [InlineData("timestamp",  "datetime2")]
    [InlineData("bytea",      "varbinary(max)")]
    [InlineData("text",       "nvarchar(max)")]
    [InlineData("json",       "nvarchar(max)")]
    [InlineData("jsonb",      "nvarchar(max)")]
    [InlineData("serial",     "int identity(1,1)")]
    [InlineData("bigserial",  "bigint identity(1,1)")]
    public void Default_PostgreSQLToSqlServer_MapsCorrectly(string pgType, string expectedSql)
    {
        var config = TypeMappingConfig.Default;
        Assert.Equal(expectedSql, config.MapPostgreSQLToSqlServer(pgType));
    }

    [Fact]
    public void MapSqlServerToPostgreSQL_UnknownType_ReturnsInput()
    {
        var config = TypeMappingConfig.Default;
        Assert.Equal("MYTYPE", config.MapSqlServerToPostgreSQL("MYTYPE"));
    }

    [Fact]
    public void MapPostgreSQLToSqlServer_UnknownType_ReturnsInput()
    {
        var config = TypeMappingConfig.Default;
        Assert.Equal("mytype", config.MapPostgreSQLToSqlServer("mytype"));
    }

    [Fact]
    public void Mapping_IsCaseInsensitive()
    {
        var config = TypeMappingConfig.Default;
        Assert.Equal("integer", config.MapSqlServerToPostgreSQL("INT"));
        Assert.Equal("integer", config.MapSqlServerToPostgreSQL("int"));
        Assert.Equal("integer", config.MapSqlServerToPostgreSQL("Int"));
    }

    [Fact]
    public void CustomConfig_OverridesDefaultMapping()
    {
        var config = new TypeMappingConfig();
        config.SqlServerToPostgreSQL["int"] = "INT4";

        Assert.Equal("INT4", config.MapSqlServerToPostgreSQL("int"));
    }
}
