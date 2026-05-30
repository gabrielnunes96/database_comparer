using System.Text.RegularExpressions;

namespace DatabaseTool.Core.Conversion;

public class QueryConverter
{
    private readonly TypeMappingConfig _mapping;

    public QueryConverter(TypeMappingConfig? mapping = null)
    {
        _mapping = mapping ?? TypeMappingConfig.Default;
    }

    public QueryConversionResult ConvertSqlServerToPostgreSQL(string query)
    {
        var warnings = new List<string>();
        var result = query;

        result = Regex.Replace(result, @"\bGETDATE\s*\(\s*\)", "NOW()", RegexOptions.IgnoreCase);
        result = Regex.Replace(result, @"\bGETUTCDATE\s*\(\s*\)", "NOW() AT TIME ZONE 'UTC'", RegexOptions.IgnoreCase);
        result = Regex.Replace(result, @"\bISNULL\s*\(", "COALESCE(", RegexOptions.IgnoreCase);
        result = Regex.Replace(result, @"\bLEN\s*\(", "LENGTH(", RegexOptions.IgnoreCase);
        result = Regex.Replace(result, @"\bCHARINDEX\s*\(([^,]+),\s*([^,)]+)\)", "POSITION($1 IN $2)", RegexOptions.IgnoreCase);
        result = Regex.Replace(result, @"\bNEWID\s*\(\s*\)", "gen_random_uuid()", RegexOptions.IgnoreCase);
        result = Regex.Replace(result, @"\bDATEADD\s*\(\s*\w+\s*,\s*([^,]+),\s*([^)]+)\)", "$2 + INTERVAL '$1'", RegexOptions.IgnoreCase);
        result = Regex.Replace(result, @"\bDATEDIFF\s*\(", "-- DATEDIFF(", RegexOptions.IgnoreCase);
        result = Regex.Replace(result, @"\bCONVERT\s*\(\s*([^,]+),\s*([^)]+)\)", "CAST($2 AS $1)", RegexOptions.IgnoreCase);
        result = Regex.Replace(result, @"\bNVARCHAR\b", "VARCHAR", RegexOptions.IgnoreCase);
        result = Regex.Replace(result, @"\bDATETIME2?\b", "TIMESTAMP", RegexOptions.IgnoreCase);
        result = Regex.Replace(result, @"\bUNIQUEIDENTIFIER\b", "UUID", RegexOptions.IgnoreCase);
        result = Regex.Replace(result, @"\bTOP\s+(\d+)\s+", "", RegexOptions.IgnoreCase);
        result = Regex.Replace(result, @"\[([^\]]+)\]", "\"$1\"");
        result = Regex.Replace(result, @"\bN'", "'", RegexOptions.IgnoreCase);

        if (Regex.IsMatch(result, @"\bDATEDIFF\b|\bFORMAT\s*\(|\bSTUFF\s*\(", RegexOptions.IgnoreCase))
            warnings.Add("Some SQL Server functions (DATEDIFF, FORMAT, STUFF) were not fully converted and need manual review.");

        if (Regex.IsMatch(query, @"\bTOP\s+\d+\b", RegexOptions.IgnoreCase))
            warnings.Add("TOP clause removed - add LIMIT n at the end of the query.");

        return new QueryConversionResult { ConvertedQuery = result, Warnings = warnings };
    }

    public QueryConversionResult ConvertPostgreSQLToSqlServer(string query)
    {
        var warnings = new List<string>();
        var result = query;

        result = Regex.Replace(result, @"\bNOW\s*\(\s*\)", "GETDATE()", RegexOptions.IgnoreCase);
        result = Regex.Replace(result, @"\bCURRENT_TIMESTAMP\b", "GETDATE()", RegexOptions.IgnoreCase);
        result = Regex.Replace(result, @"\bCOALESCE\s*\(", "ISNULL(", RegexOptions.IgnoreCase);
        result = Regex.Replace(result, @"\bLENGTH\s*\(", "LEN(", RegexOptions.IgnoreCase);
        result = Regex.Replace(result, @"\bPOSITION\s*\(([^)]+)\s+IN\s+([^)]+)\)", "CHARINDEX($1, $2)", RegexOptions.IgnoreCase);
        result = Regex.Replace(result, @"\bgen_random_uuid\s*\(\s*\)", "NEWID()", RegexOptions.IgnoreCase);
        result = Regex.Replace(result, @"\bBOOLEAN\b", "BIT", RegexOptions.IgnoreCase);
        result = Regex.Replace(result, @"\bTIMESTAMP\b", "DATETIME2", RegexOptions.IgnoreCase);
        result = Regex.Replace(result, @"\bUUID\b", "UNIQUEIDENTIFIER", RegexOptions.IgnoreCase);
        result = Regex.Replace(result, @"\bINTEGER\b", "INT", RegexOptions.IgnoreCase);
        result = Regex.Replace(result, @"\bTEXT\b", "NVARCHAR(MAX)", RegexOptions.IgnoreCase);
        result = Regex.Replace(result, @"\bLIMIT\s+(\d+)\b", "", RegexOptions.IgnoreCase);
        result = Regex.Replace(result, "\"([^\"]+)\"", "[$1]");
        result = Regex.Replace(result, @"::\w+", "", RegexOptions.IgnoreCase);

        if (Regex.IsMatch(query, @"\bLIMIT\s+\d+\b", RegexOptions.IgnoreCase))
            warnings.Add("LIMIT clause removed - add TOP n at the beginning of SELECT.");

        if (Regex.IsMatch(result, @"\|\|", RegexOptions.IgnoreCase))
        {
            result = result.Replace("||", "+");
            warnings.Add("String concatenation || replaced with + (verify string context).");
        }

        return new QueryConversionResult { ConvertedQuery = result, Warnings = warnings };
    }
}

public class QueryConversionResult
{
    public string ConvertedQuery { get; set; } = string.Empty;
    public List<string> Warnings { get; set; } = new();
}
