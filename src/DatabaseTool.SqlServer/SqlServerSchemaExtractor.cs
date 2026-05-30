using DatabaseTool.Core.Interfaces;
using DatabaseTool.Core.Models;
using Microsoft.Data.SqlClient;

namespace DatabaseTool.SqlServer;

public class SqlServerSchemaExtractor : ISchemaExtractor
{
    public async Task<bool> TestConnectionAsync(string connectionString, CancellationToken cancellationToken = default)
    {
        try
        {
            await using var conn = new SqlConnection(connectionString);
            await conn.OpenAsync(cancellationToken);
            return true;
        }
        catch
        {
            return false;
        }
    }

    public async Task<IReadOnlyList<string>> ListTablesAsync(string connectionString, CancellationToken cancellationToken = default)
    {
        await using var conn = new SqlConnection(connectionString);
        await conn.OpenAsync(cancellationToken);
        return await QueryStringsAsync(conn,
            "SELECT TABLE_NAME FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_TYPE = 'BASE TABLE' ORDER BY TABLE_NAME",
            cancellationToken);
    }

    public async Task<IReadOnlyList<string>> ListViewsAsync(string connectionString, CancellationToken cancellationToken = default)
    {
        await using var conn = new SqlConnection(connectionString);
        await conn.OpenAsync(cancellationToken);
        return await QueryStringsAsync(conn,
            "SELECT TABLE_NAME FROM INFORMATION_SCHEMA.VIEWS ORDER BY TABLE_NAME",
            cancellationToken);
    }

    public async Task<IReadOnlyList<string>> ListProceduresAsync(string connectionString, CancellationToken cancellationToken = default)
    {
        await using var conn = new SqlConnection(connectionString);
        await conn.OpenAsync(cancellationToken);
        return await QueryStringsAsync(conn,
            "SELECT ROUTINE_NAME FROM INFORMATION_SCHEMA.ROUTINES WHERE ROUTINE_TYPE = 'PROCEDURE' ORDER BY ROUTINE_NAME",
            cancellationToken);
    }

    public async Task<IReadOnlyList<string>> ListFunctionsAsync(string connectionString, CancellationToken cancellationToken = default)
    {
        await using var conn = new SqlConnection(connectionString);
        await conn.OpenAsync(cancellationToken);
        return await QueryStringsAsync(conn,
            "SELECT ROUTINE_NAME FROM INFORMATION_SCHEMA.ROUTINES WHERE ROUTINE_TYPE = 'FUNCTION' ORDER BY ROUTINE_NAME",
            cancellationToken);
    }

    public async Task<SchemaModel> ExtractAsync(string connectionString, ExtractionOptions options, CancellationToken cancellationToken = default)
    {
        await using var conn = new SqlConnection(connectionString);
        await conn.OpenAsync(cancellationToken);

        var model = new SchemaModel
        {
            DatabaseName = conn.Database,
            Dialect = DatabaseDialect.SqlServer,
            SourceDescription = $"SQL Server: {conn.DataSource}/{conn.Database}"
        };

        if (options.IncludeTables)
            model.Tables = await ExtractTablesAsync(conn, options, cancellationToken);

        if (options.IncludeViews)
            model.Views = await ExtractViewsAsync(conn, options, cancellationToken);

        if (options.IncludeProcedures)
            model.Procedures = await ExtractProceduresAsync(conn, options, cancellationToken);

        if (options.IncludeFunctions)
            model.Functions = await ExtractFunctionsAsync(conn, options, cancellationToken);

        if (options.IncludeTriggers)
            model.Triggers = await ExtractTriggersAsync(conn, cancellationToken);

        if (options.IncludeSequences)
            model.Sequences = await ExtractSequencesAsync(conn, cancellationToken);

        return model;
    }

    private static async Task<List<TableModel>> ExtractTablesAsync(SqlConnection conn, ExtractionOptions options, CancellationToken ct)
    {
        var tables = new Dictionary<string, TableModel>(StringComparer.OrdinalIgnoreCase);

        const string tableQuery = @"
            SELECT t.TABLE_SCHEMA, t.TABLE_NAME
            FROM INFORMATION_SCHEMA.TABLES t
            WHERE t.TABLE_TYPE = 'BASE TABLE'
            ORDER BY t.TABLE_SCHEMA, t.TABLE_NAME";

        await using (var cmd = new SqlCommand(tableQuery, conn))
        await using (var reader = await cmd.ExecuteReaderAsync(ct))
        {
            while (await reader.ReadAsync(ct))
            {
                var schema = reader.GetString(0);
                var name = reader.GetString(1);
                if (!options.IsTableIncluded(name)) continue;

                tables[name] = new TableModel { Schema = schema, Name = name };
            }
        }

        if (tables.Count == 0) return new List<TableModel>();

        await ExtractColumnsAsync(conn, tables, ct);
        await ExtractPrimaryKeysAsync(conn, tables, ct);
        await ExtractForeignKeysAsync(conn, tables, ct);
        if (options.IncludeIndexes)
            await ExtractIndexesAsync(conn, tables, ct);

        return tables.Values.ToList();
    }

    private static async Task ExtractColumnsAsync(SqlConnection conn, Dictionary<string, TableModel> tables, CancellationToken ct)
    {
        const string colQuery = @"
            SELECT
                c.TABLE_NAME,
                c.COLUMN_NAME,
                c.DATA_TYPE,
                c.CHARACTER_MAXIMUM_LENGTH,
                c.NUMERIC_PRECISION,
                c.NUMERIC_SCALE,
                c.IS_NULLABLE,
                c.COLUMN_DEFAULT,
                c.ORDINAL_POSITION,
                COLUMNPROPERTY(OBJECT_ID(c.TABLE_SCHEMA + '.' + c.TABLE_NAME), c.COLUMN_NAME, 'IsIdentity') AS IS_IDENTITY,
                IDENT_SEED(c.TABLE_SCHEMA + '.' + c.TABLE_NAME) AS IDENTITY_SEED,
                IDENT_INCR(c.TABLE_SCHEMA + '.' + c.TABLE_NAME) AS IDENTITY_INCREMENT,
                COLUMNPROPERTY(OBJECT_ID(c.TABLE_SCHEMA + '.' + c.TABLE_NAME), c.COLUMN_NAME, 'IsComputed') AS IS_COMPUTED
            FROM INFORMATION_SCHEMA.COLUMNS c
            WHERE c.TABLE_NAME IN (SELECT name FROM sys.tables)
            ORDER BY c.TABLE_NAME, c.ORDINAL_POSITION";

        await using var cmd = new SqlCommand(colQuery, conn);
        await using var reader = await cmd.ExecuteReaderAsync(ct);

        while (await reader.ReadAsync(ct))
        {
            var tableName = reader.GetString(0);
            if (!tables.TryGetValue(tableName, out var table)) continue;

            var col = new ColumnModel
            {
                Name = reader.GetString(1),
                DataType = reader.GetString(2),
                MaxLength = reader.IsDBNull(3) ? null : reader.GetInt32(3),
                Precision = reader.IsDBNull(4) ? null : (int?)reader.GetByte(4),
                Scale = reader.IsDBNull(5) ? null : reader.GetInt32(5),
                IsNullable = reader.GetString(6) == "YES",
                DefaultValue = reader.IsDBNull(7) ? null : reader.GetString(7),
                OrdinalPosition = reader.GetInt32(8),
                IsIdentity = reader.GetInt32(9) == 1,
                IdentitySeed = reader.IsDBNull(10) ? null : Convert.ToInt64(reader.GetDecimal(10)),
                IdentityIncrement = reader.IsDBNull(11) ? null : Convert.ToInt64(reader.GetDecimal(11)),
                IsComputed = reader.GetInt32(12) == 1
            };

            table.Columns.Add(col);
        }
    }

    private static async Task ExtractPrimaryKeysAsync(SqlConnection conn, Dictionary<string, TableModel> tables, CancellationToken ct)
    {
        const string pkQuery = @"
            SELECT
                kcu.TABLE_NAME,
                tc.CONSTRAINT_NAME,
                kcu.COLUMN_NAME,
                kcu.ORDINAL_POSITION,
                i.type_desc
            FROM INFORMATION_SCHEMA.TABLE_CONSTRAINTS tc
            JOIN INFORMATION_SCHEMA.KEY_COLUMN_USAGE kcu
                ON tc.CONSTRAINT_NAME = kcu.CONSTRAINT_NAME AND tc.TABLE_NAME = kcu.TABLE_NAME
            LEFT JOIN sys.indexes i
                ON i.object_id = OBJECT_ID(tc.TABLE_SCHEMA + '.' + tc.TABLE_NAME)
                AND i.is_primary_key = 1
            WHERE tc.CONSTRAINT_TYPE = 'PRIMARY KEY'
            ORDER BY kcu.TABLE_NAME, kcu.ORDINAL_POSITION";

        await using var cmd = new SqlCommand(pkQuery, conn);
        await using var reader = await cmd.ExecuteReaderAsync(ct);

        var pkMap = new Dictionary<string, PrimaryKeyModel>(StringComparer.OrdinalIgnoreCase);

        while (await reader.ReadAsync(ct))
        {
            var tableName = reader.GetString(0);
            if (!tables.ContainsKey(tableName)) continue;

            if (!pkMap.TryGetValue(tableName, out var pk))
            {
                pk = new PrimaryKeyModel
                {
                    Name = reader.GetString(1),
                    IsClustered = !reader.IsDBNull(4) && reader.GetString(4) == "CLUSTERED"
                };
                pkMap[tableName] = pk;
            }

            pk.Columns.Add(reader.GetString(2));
        }

        foreach (var (tableName, pk) in pkMap)
            if (tables.TryGetValue(tableName, out var table))
                table.PrimaryKey = pk;
    }

    private static async Task ExtractForeignKeysAsync(SqlConnection conn, Dictionary<string, TableModel> tables, CancellationToken ct)
    {
        const string fkQuery = @"
            SELECT
                fk.name AS FK_NAME,
                OBJECT_NAME(fk.parent_object_id) AS TABLE_NAME,
                COL_NAME(fkc.parent_object_id, fkc.parent_column_id) AS COLUMN_NAME,
                OBJECT_NAME(fk.referenced_object_id) AS REF_TABLE,
                COL_NAME(fkc.referenced_object_id, fkc.referenced_column_id) AS REF_COLUMN,
                OBJECT_SCHEMA_NAME(fk.referenced_object_id) AS REF_SCHEMA,
                fk.delete_referential_action_desc,
                fk.update_referential_action_desc
            FROM sys.foreign_keys fk
            JOIN sys.foreign_key_columns fkc ON fk.object_id = fkc.constraint_object_id
            ORDER BY fk.name, fkc.constraint_column_id";

        await using var cmd = new SqlCommand(fkQuery, conn);
        await using var reader = await cmd.ExecuteReaderAsync(ct);

        var fkMap = new Dictionary<string, ForeignKeyModel>(StringComparer.OrdinalIgnoreCase);
        var fkTableMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        while (await reader.ReadAsync(ct))
        {
            var fkName = reader.GetString(0);
            var tableName = reader.GetString(1);
            if (!tables.ContainsKey(tableName)) continue;

            if (!fkMap.TryGetValue(fkName, out var fk))
            {
                fk = new ForeignKeyModel
                {
                    Name = fkName,
                    ReferencedTable = reader.GetString(3),
                    ReferencedSchema = reader.GetString(5),
                    OnDelete = ParseFkAction(reader.GetString(6)),
                    OnUpdate = ParseFkAction(reader.GetString(7))
                };
                fkMap[fkName] = fk;
                fkTableMap[fkName] = tableName;
            }

            fk.Columns.Add(reader.GetString(2));
            fk.ReferencedColumns.Add(reader.GetString(4));
        }

        foreach (var (fkName, fk) in fkMap)
        {
            if (fkTableMap.TryGetValue(fkName, out var tableName) && tables.TryGetValue(tableName, out var table))
                table.ForeignKeys.Add(fk);
        }
    }

    private static async Task ExtractIndexesAsync(SqlConnection conn, Dictionary<string, TableModel> tables, CancellationToken ct)
    {
        const string idxQuery = @"
            SELECT
                t.name AS TABLE_NAME,
                i.name AS INDEX_NAME,
                i.is_unique,
                i.type_desc,
                c.name AS COLUMN_NAME,
                ic.is_included_column,
                ic.key_ordinal,
                i.filter_definition
            FROM sys.indexes i
            JOIN sys.tables t ON i.object_id = t.object_id
            JOIN sys.index_columns ic ON i.object_id = ic.object_id AND i.index_id = ic.index_id
            JOIN sys.columns c ON ic.object_id = c.object_id AND ic.column_id = c.column_id
            WHERE i.is_primary_key = 0 AND i.type > 0
            ORDER BY t.name, i.name, ic.key_ordinal";

        await using var cmd = new SqlCommand(idxQuery, conn);
        await using var reader = await cmd.ExecuteReaderAsync(ct);

        var idxMap = new Dictionary<string, IndexModel>(StringComparer.OrdinalIgnoreCase);
        var idxTableMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        while (await reader.ReadAsync(ct))
        {
            var tableName = reader.GetString(0);
            if (!tables.ContainsKey(tableName)) continue;

            var idxName = reader.GetString(1);

            if (!idxMap.TryGetValue(idxName, out var idx))
            {
                idx = new IndexModel
                {
                    Name = idxName,
                    IsUnique = reader.GetBoolean(2),
                    IsClustered = reader.GetString(3) == "CLUSTERED",
                    FilterExpression = reader.IsDBNull(7) ? null : reader.GetString(7)
                };
                idxMap[idxName] = idx;
                idxTableMap[idxName] = tableName;
            }

            var colName = reader.GetString(4);
            var isIncluded = reader.GetBoolean(5);
            if (isIncluded)
                idx.IncludedColumns.Add(colName);
            else
                idx.Columns.Add(colName);
        }

        foreach (var (idxName, idx) in idxMap)
        {
            if (idxTableMap.TryGetValue(idxName, out var tableName) && tables.TryGetValue(tableName, out var table))
                table.Indexes.Add(idx);
        }
    }

    private static async Task<List<ViewModel>> ExtractViewsAsync(SqlConnection conn, ExtractionOptions options, CancellationToken ct)
    {
        const string query = @"
            SELECT v.TABLE_SCHEMA, v.TABLE_NAME, m.definition
            FROM INFORMATION_SCHEMA.VIEWS v
            JOIN sys.sql_modules m ON m.object_id = OBJECT_ID(v.TABLE_SCHEMA + '.' + v.TABLE_NAME)
            ORDER BY v.TABLE_NAME";

        return await ExecuteRoutineQuery(conn, query, reader =>
        {
            var name = reader.GetString(1);
            if (!options.IsViewIncluded(name)) return null;
            return new ViewModel
            {
                Schema = reader.GetString(0),
                Name = name,
                Definition = reader.GetString(2)
            };
        }, ct);
    }

    private static async Task<List<ProcedureModel>> ExtractProceduresAsync(SqlConnection conn, ExtractionOptions options, CancellationToken ct)
    {
        const string query = @"
            SELECT r.ROUTINE_SCHEMA, r.ROUTINE_NAME, m.definition
            FROM INFORMATION_SCHEMA.ROUTINES r
            JOIN sys.sql_modules m ON m.object_id = OBJECT_ID(r.ROUTINE_SCHEMA + '.' + r.ROUTINE_NAME)
            WHERE r.ROUTINE_TYPE = 'PROCEDURE'
            ORDER BY r.ROUTINE_NAME";

        return await ExecuteRoutineQuery(conn, query, reader =>
        {
            var name = reader.GetString(1);
            if (!options.IsProcedureIncluded(name)) return null;
            return new ProcedureModel
            {
                Schema = reader.GetString(0),
                Name = name,
                Definition = reader.GetString(2)
            };
        }, ct);
    }

    private static async Task<List<FunctionModel>> ExtractFunctionsAsync(SqlConnection conn, ExtractionOptions options, CancellationToken ct)
    {
        const string query = @"
            SELECT r.ROUTINE_SCHEMA, r.ROUTINE_NAME, m.definition, r.DATA_TYPE
            FROM INFORMATION_SCHEMA.ROUTINES r
            JOIN sys.sql_modules m ON m.object_id = OBJECT_ID(r.ROUTINE_SCHEMA + '.' + r.ROUTINE_NAME)
            WHERE r.ROUTINE_TYPE = 'FUNCTION'
            ORDER BY r.ROUTINE_NAME";

        return await ExecuteRoutineQuery(conn, query, reader =>
        {
            var name = reader.GetString(1);
            if (!options.IsFunctionIncluded(name)) return null;
            return new FunctionModel
            {
                Schema = reader.GetString(0),
                Name = name,
                Definition = reader.GetString(2),
                ReturnType = reader.IsDBNull(3) ? string.Empty : reader.GetString(3)
            };
        }, ct);
    }

    private static async Task<List<TriggerModel>> ExtractTriggersAsync(SqlConnection conn, CancellationToken ct)
    {
        const string query = @"
            SELECT
                s.name AS SCHEMA_NAME,
                tr.name AS TRIGGER_NAME,
                OBJECT_NAME(tr.parent_id) AS TABLE_NAME,
                OBJECTPROPERTY(tr.object_id, 'ExecIsInsertTrigger') AS IS_INSERT,
                OBJECTPROPERTY(tr.object_id, 'ExecIsUpdateTrigger') AS IS_UPDATE,
                OBJECTPROPERTY(tr.object_id, 'ExecIsDeleteTrigger') AS IS_DELETE,
                OBJECTPROPERTY(tr.object_id, 'ExecIsInsteadOfTrigger') AS IS_INSTEAD_OF,
                OBJECTPROPERTY(tr.object_id, 'ExecIsAfterTrigger') AS IS_AFTER,
                m.definition,
                tr.is_disabled
            FROM sys.triggers tr
            JOIN sys.sql_modules m ON m.object_id = tr.object_id
            JOIN sys.schemas s ON s.schema_id = OBJECTPROPERTY(tr.parent_id, 'SchemaId')
            WHERE tr.parent_class = 1
            ORDER BY tr.name";

        var results = new List<TriggerModel>();
        await using var cmd = new SqlCommand(query, conn);
        await using var reader = await cmd.ExecuteReaderAsync(ct);

        while (await reader.ReadAsync(ct))
        {
            var trigger = new TriggerModel
            {
                Schema = reader.GetString(0),
                Name = reader.GetString(1),
                TableName = reader.GetString(2),
                Definition = reader.GetString(8),
                IsEnabled = !reader.GetBoolean(9),
                Timing = reader.GetInt32(6) == 1 ? TriggerTiming.InsteadOf : TriggerTiming.After
            };

            if (reader.GetInt32(3) == 1) trigger.Events.Add(TriggerEvent.Insert);
            if (reader.GetInt32(4) == 1) trigger.Events.Add(TriggerEvent.Update);
            if (reader.GetInt32(5) == 1) trigger.Events.Add(TriggerEvent.Delete);

            results.Add(trigger);
        }

        return results;
    }

    private static async Task<List<SequenceModel>> ExtractSequencesAsync(SqlConnection conn, CancellationToken ct)
    {
        const string query = @"
            SELECT
                SCHEMA_NAME(schema_id) AS SCHEMA_NAME,
                name,
                TYPE_NAME(user_type_id) AS DATA_TYPE,
                start_value,
                increment,
                minimum_value,
                maximum_value,
                is_cycling
            FROM sys.sequences
            ORDER BY name";

        var results = new List<SequenceModel>();
        await using var cmd = new SqlCommand(query, conn);
        await using var reader = await cmd.ExecuteReaderAsync(ct);

        while (await reader.ReadAsync(ct))
        {
            results.Add(new SequenceModel
            {
                Schema = reader.GetString(0),
                Name = reader.GetString(1),
                DataType = reader.GetString(2),
                StartValue = Convert.ToInt64(reader.GetValue(3)),
                Increment = Convert.ToInt64(reader.GetValue(4)),
                MinValue = reader.IsDBNull(5) ? null : Convert.ToInt64(reader.GetValue(5)),
                MaxValue = reader.IsDBNull(6) ? null : Convert.ToInt64(reader.GetValue(6)),
                Cycle = reader.GetBoolean(7)
            });
        }

        return results;
    }

    private static async Task<List<T>> ExecuteRoutineQuery<T>(
        SqlConnection conn, string query,
        Func<SqlDataReader, T?> mapper,
        CancellationToken ct) where T : class
    {
        var results = new List<T>();
        await using var cmd = new SqlCommand(query, conn);
        await using var reader = await cmd.ExecuteReaderAsync(ct);

        while (await reader.ReadAsync(ct))
        {
            var item = mapper(reader);
            if (item is not null) results.Add(item);
        }

        return results;
    }

    private static async Task<List<string>> QueryStringsAsync(SqlConnection conn, string query, CancellationToken ct)
    {
        var results = new List<string>();
        await using var cmd = new SqlCommand(query, conn);
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
            results.Add(reader.GetString(0));
        return results;
    }

    private static ForeignKeyAction ParseFkAction(string action) => action switch
    {
        "CASCADE" => ForeignKeyAction.Cascade,
        "SET_NULL" => ForeignKeyAction.SetNull,
        "SET_DEFAULT" => ForeignKeyAction.SetDefault,
        "NO_ACTION" => ForeignKeyAction.NoAction,
        _ => ForeignKeyAction.NoAction
    };
}
