namespace DatabaseTool.Core.Conversion;

public class TypeMappingConfig
{
    public Dictionary<string, string> SqlServerToPostgreSQL { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    public Dictionary<string, string> PostgreSQLToSqlServer { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    public static TypeMappingConfig Default => new()
    {
        SqlServerToPostgreSQL = new(StringComparer.OrdinalIgnoreCase)
        {
            ["int"] = "integer",
            ["bigint"] = "bigint",
            ["smallint"] = "smallint",
            ["tinyint"] = "smallint",
            ["bit"] = "boolean",
            ["decimal"] = "numeric",
            ["numeric"] = "numeric",
            ["money"] = "numeric(19,4)",
            ["smallmoney"] = "numeric(10,4)",
            ["float"] = "double precision",
            ["real"] = "real",
            ["datetime"] = "timestamp",
            ["datetime2"] = "timestamp",
            ["smalldatetime"] = "timestamp",
            ["date"] = "date",
            ["time"] = "time",
            ["datetimeoffset"] = "timestamptz",
            ["char"] = "char",
            ["varchar"] = "varchar",
            ["nchar"] = "char",
            ["nvarchar"] = "varchar",
            ["text"] = "text",
            ["ntext"] = "text",
            ["binary"] = "bytea",
            ["varbinary"] = "bytea",
            ["image"] = "bytea",
            ["uniqueidentifier"] = "uuid",
            ["xml"] = "xml",
            ["sql_variant"] = "text",
            ["rowversion"] = "bytea",
            ["timestamp"] = "bytea",
            ["geography"] = "text",
            ["geometry"] = "text",
            ["hierarchyid"] = "text",
        },
        PostgreSQLToSqlServer = new(StringComparer.OrdinalIgnoreCase)
        {
            ["integer"] = "int",
            ["int4"] = "int",
            ["int8"] = "bigint",
            ["int2"] = "smallint",
            ["bigint"] = "bigint",
            ["smallint"] = "smallint",
            ["boolean"] = "bit",
            ["bool"] = "bit",
            ["numeric"] = "decimal",
            ["decimal"] = "decimal",
            ["double precision"] = "float",
            ["float8"] = "float",
            ["float4"] = "real",
            ["real"] = "real",
            ["timestamp"] = "datetime2",
            ["timestamp without time zone"] = "datetime2",
            ["timestamp with time zone"] = "datetimeoffset",
            ["timestamptz"] = "datetimeoffset",
            ["date"] = "date",
            ["time"] = "time",
            ["time without time zone"] = "time",
            ["char"] = "nchar",
            ["character"] = "nchar",
            ["varchar"] = "nvarchar",
            ["character varying"] = "nvarchar",
            ["text"] = "nvarchar(max)",
            ["bytea"] = "varbinary(max)",
            ["uuid"] = "uniqueidentifier",
            ["xml"] = "xml",
            ["json"] = "nvarchar(max)",
            ["jsonb"] = "nvarchar(max)",
            ["serial"] = "int identity(1,1)",
            ["bigserial"] = "bigint identity(1,1)",
            ["smallserial"] = "smallint identity(1,1)",
        }
    };

    public string MapSqlServerToPostgreSQL(string sqlServerType) =>
        SqlServerToPostgreSQL.TryGetValue(sqlServerType, out var mapped) ? mapped : sqlServerType;

    public string MapPostgreSQLToSqlServer(string postgresType) =>
        PostgreSQLToSqlServer.TryGetValue(postgresType, out var mapped) ? mapped : postgresType;
}
