using DatabaseTool.Core.Interfaces;
using DatabaseTool.Core.Models;
using Npgsql;

namespace DatabaseTool.PostgreSQL;

public class PostgreSqlSchemaExtractor : ISchemaExtractor
{
    public async Task<bool> TestConnectionAsync(string connectionString, CancellationToken cancellationToken = default)
    {
        try
        {
            await using var conn = new NpgsqlConnection(connectionString);
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
        await using var conn = new NpgsqlConnection(connectionString);
        await conn.OpenAsync(cancellationToken);
        return await QueryStringsAsync(conn,
            "SELECT table_name FROM information_schema.tables WHERE table_schema NOT IN ('pg_catalog','information_schema') AND table_type = 'BASE TABLE' ORDER BY table_name",
            cancellationToken);
    }

    public async Task<IReadOnlyList<string>> ListViewsAsync(string connectionString, CancellationToken cancellationToken = default)
    {
        await using var conn = new NpgsqlConnection(connectionString);
        await conn.OpenAsync(cancellationToken);
        return await QueryStringsAsync(conn,
            "SELECT table_name FROM information_schema.views WHERE table_schema NOT IN ('pg_catalog','information_schema') ORDER BY table_name",
            cancellationToken);
    }

    public async Task<IReadOnlyList<string>> ListProceduresAsync(string connectionString, CancellationToken cancellationToken = default)
    {
        await using var conn = new NpgsqlConnection(connectionString);
        await conn.OpenAsync(cancellationToken);
        return await QueryStringsAsync(conn,
            "SELECT routine_name FROM information_schema.routines WHERE routine_type = 'PROCEDURE' AND routine_schema NOT IN ('pg_catalog','information_schema') ORDER BY routine_name",
            cancellationToken);
    }

    public async Task<IReadOnlyList<string>> ListFunctionsAsync(string connectionString, CancellationToken cancellationToken = default)
    {
        await using var conn = new NpgsqlConnection(connectionString);
        await conn.OpenAsync(cancellationToken);
        return await QueryStringsAsync(conn,
            "SELECT routine_name FROM information_schema.routines WHERE routine_type = 'FUNCTION' AND routine_schema NOT IN ('pg_catalog','information_schema') ORDER BY routine_name",
            cancellationToken);
    }

    public async Task<SchemaModel> ExtractAsync(string connectionString, ExtractionOptions options, CancellationToken cancellationToken = default)
    {
        await using var conn = new NpgsqlConnection(connectionString);
        await conn.OpenAsync(cancellationToken);

        var model = new SchemaModel
        {
            DatabaseName = conn.Database,
            Dialect = DatabaseDialect.PostgreSQL,
            SourceDescription = $"PostgreSQL: {conn.Host}/{conn.Database}"
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

    private static async Task<List<TableModel>> ExtractTablesAsync(NpgsqlConnection conn, ExtractionOptions options, CancellationToken ct)
    {
        var tables = new Dictionary<string, TableModel>(StringComparer.OrdinalIgnoreCase);

        const string tableQuery = @"
            SELECT table_schema, table_name
            FROM information_schema.tables
            WHERE table_schema NOT IN ('pg_catalog','information_schema')
              AND table_type = 'BASE TABLE'
            ORDER BY table_schema, table_name";

        await using (var cmd = new NpgsqlCommand(tableQuery, conn))
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

    private static async Task ExtractColumnsAsync(NpgsqlConnection conn, Dictionary<string, TableModel> tables, CancellationToken ct)
    {
        const string colQuery = @"
            SELECT
                c.table_name,
                c.column_name,
                c.udt_name,
                c.character_maximum_length,
                c.numeric_precision,
                c.numeric_scale,
                c.is_nullable,
                c.column_default,
                c.ordinal_position,
                c.is_identity,
                c.identity_start,
                c.identity_increment
            FROM information_schema.columns c
            WHERE c.table_schema NOT IN ('pg_catalog','information_schema')
            ORDER BY c.table_name, c.ordinal_position";

        await using var cmd = new NpgsqlCommand(colQuery, conn);
        await using var reader = await cmd.ExecuteReaderAsync(ct);

        while (await reader.ReadAsync(ct))
        {
            var tableName = reader.GetString(0);
            if (!tables.TryGetValue(tableName, out var table)) continue;

            var isIdentity = !reader.IsDBNull(9) && reader.GetString(9) == "YES";
            var col = new ColumnModel
            {
                Name = reader.GetString(1),
                DataType = reader.GetString(2),
                MaxLength = reader.IsDBNull(3) ? null : reader.GetInt32(3),
                Precision = reader.IsDBNull(4) ? null : reader.GetInt32(4),
                Scale = reader.IsDBNull(5) ? null : reader.GetInt32(5),
                IsNullable = reader.GetString(6) == "YES",
                DefaultValue = reader.IsDBNull(7) ? null : reader.GetString(7),
                OrdinalPosition = reader.GetInt32(8),
                IsIdentity = isIdentity,
                IdentitySeed = isIdentity && !reader.IsDBNull(10) ? (long?)Convert.ToInt64(reader.GetString(10)) : null,
                IdentityIncrement = isIdentity && !reader.IsDBNull(11) ? (long?)Convert.ToInt64(reader.GetString(11)) : null,
            };

            table.Columns.Add(col);
        }
    }

    private static async Task ExtractPrimaryKeysAsync(NpgsqlConnection conn, Dictionary<string, TableModel> tables, CancellationToken ct)
    {
        const string pkQuery = @"
            SELECT
                tc.table_name,
                tc.constraint_name,
                kcu.column_name,
                kcu.ordinal_position
            FROM information_schema.table_constraints tc
            JOIN information_schema.key_column_usage kcu
                ON tc.constraint_name = kcu.constraint_name AND tc.table_schema = kcu.table_schema
            WHERE tc.constraint_type = 'PRIMARY KEY'
              AND tc.table_schema NOT IN ('pg_catalog','information_schema')
            ORDER BY tc.table_name, kcu.ordinal_position";

        await using var cmd = new NpgsqlCommand(pkQuery, conn);
        await using var reader = await cmd.ExecuteReaderAsync(ct);

        var pkMap = new Dictionary<string, PrimaryKeyModel>(StringComparer.OrdinalIgnoreCase);

        while (await reader.ReadAsync(ct))
        {
            var tableName = reader.GetString(0);
            if (!tables.ContainsKey(tableName)) continue;

            if (!pkMap.TryGetValue(tableName, out var pk))
            {
                pk = new PrimaryKeyModel { Name = reader.GetString(1) };
                pkMap[tableName] = pk;
            }

            pk.Columns.Add(reader.GetString(2));
        }

        foreach (var (tableName, pk) in pkMap)
            if (tables.TryGetValue(tableName, out var table))
                table.PrimaryKey = pk;
    }

    private static async Task ExtractForeignKeysAsync(NpgsqlConnection conn, Dictionary<string, TableModel> tables, CancellationToken ct)
    {
        const string fkQuery = @"
            SELECT
                tc.constraint_name,
                tc.table_name,
                kcu.column_name,
                ccu.table_schema AS ref_schema,
                ccu.table_name AS ref_table,
                ccu.column_name AS ref_column,
                rc.delete_rule,
                rc.update_rule
            FROM information_schema.table_constraints tc
            JOIN information_schema.key_column_usage kcu
                ON tc.constraint_name = kcu.constraint_name AND tc.table_schema = kcu.table_schema
            JOIN information_schema.referential_constraints rc
                ON tc.constraint_name = rc.constraint_name
            JOIN information_schema.constraint_column_usage ccu
                ON rc.unique_constraint_name = ccu.constraint_name
            WHERE tc.constraint_type = 'FOREIGN KEY'
              AND tc.table_schema NOT IN ('pg_catalog','information_schema')
            ORDER BY tc.constraint_name, kcu.ordinal_position";

        await using var cmd = new NpgsqlCommand(fkQuery, conn);
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
                    ReferencedSchema = reader.GetString(3),
                    ReferencedTable = reader.GetString(4),
                    OnDelete = ParseFkAction(reader.GetString(6)),
                    OnUpdate = ParseFkAction(reader.GetString(7))
                };
                fkMap[fkName] = fk;
                fkTableMap[fkName] = tableName;
            }

            fk.Columns.Add(reader.GetString(2));
            fk.ReferencedColumns.Add(reader.GetString(5));
        }

        foreach (var (fkName, fk) in fkMap)
            if (fkTableMap.TryGetValue(fkName, out var tableName) && tables.TryGetValue(tableName, out var table))
                table.ForeignKeys.Add(fk);
    }

    private static async Task ExtractIndexesAsync(NpgsqlConnection conn, Dictionary<string, TableModel> tables, CancellationToken ct)
    {
        const string idxQuery = @"
            SELECT
                t.relname AS table_name,
                i.relname AS index_name,
                ix.indisunique,
                ix.indisclustered,
                a.attname AS column_name,
                pg_get_expr(ix.indpred, ix.indrelid) AS filter_expression
            FROM pg_index ix
            JOIN pg_class t ON t.oid = ix.indrelid
            JOIN pg_class i ON i.oid = ix.indexrelid
            JOIN pg_attribute a ON a.attrelid = t.oid AND a.attnum = ANY(ix.indkey)
            JOIN pg_namespace ns ON ns.oid = t.relnamespace
            WHERE ix.indisprimary = false
              AND ns.nspname NOT IN ('pg_catalog','information_schema')
            ORDER BY t.relname, i.relname";

        await using var cmd = new NpgsqlCommand(idxQuery, conn);
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
                    IsClustered = reader.GetBoolean(3),
                    FilterExpression = reader.IsDBNull(5) ? null : reader.GetString(5)
                };
                idxMap[idxName] = idx;
                idxTableMap[idxName] = tableName;
            }

            idx.Columns.Add(reader.GetString(4));
        }

        foreach (var (idxName, idx) in idxMap)
            if (idxTableMap.TryGetValue(idxName, out var tableName) && tables.TryGetValue(tableName, out var table))
                table.Indexes.Add(idx);
    }

    private static async Task<List<ViewModel>> ExtractViewsAsync(NpgsqlConnection conn, ExtractionOptions options, CancellationToken ct)
    {
        const string query = @"
            SELECT table_schema, table_name, view_definition
            FROM information_schema.views
            WHERE table_schema NOT IN ('pg_catalog','information_schema')
            ORDER BY table_name";

        var results = new List<ViewModel>();
        await using var cmd = new NpgsqlCommand(query, conn);
        await using var reader = await cmd.ExecuteReaderAsync(ct);

        while (await reader.ReadAsync(ct))
        {
            var name = reader.GetString(1);
            if (!options.IsViewIncluded(name)) continue;
            results.Add(new ViewModel
            {
                Schema = reader.GetString(0),
                Name = name,
                Definition = reader.IsDBNull(2) ? string.Empty : reader.GetString(2)
            });
        }

        return results;
    }

    private static async Task<List<ProcedureModel>> ExtractProceduresAsync(NpgsqlConnection conn, ExtractionOptions options, CancellationToken ct)
    {
        const string query = @"
            SELECT routine_schema, routine_name, routine_definition
            FROM information_schema.routines
            WHERE routine_type = 'PROCEDURE'
              AND routine_schema NOT IN ('pg_catalog','information_schema')
            ORDER BY routine_name";

        var results = new List<ProcedureModel>();
        await using var cmd = new NpgsqlCommand(query, conn);
        await using var reader = await cmd.ExecuteReaderAsync(ct);

        while (await reader.ReadAsync(ct))
        {
            var name = reader.GetString(1);
            if (!options.IsProcedureIncluded(name)) continue;
            results.Add(new ProcedureModel
            {
                Schema = reader.GetString(0),
                Name = name,
                Definition = reader.IsDBNull(2) ? string.Empty : reader.GetString(2)
            });
        }

        return results;
    }

    private static async Task<List<FunctionModel>> ExtractFunctionsAsync(NpgsqlConnection conn, ExtractionOptions options, CancellationToken ct)
    {
        const string query = @"
            SELECT routine_schema, routine_name, routine_definition, data_type
            FROM information_schema.routines
            WHERE routine_type = 'FUNCTION'
              AND routine_schema NOT IN ('pg_catalog','information_schema')
            ORDER BY routine_name";

        var results = new List<FunctionModel>();
        await using var cmd = new NpgsqlCommand(query, conn);
        await using var reader = await cmd.ExecuteReaderAsync(ct);

        while (await reader.ReadAsync(ct))
        {
            var name = reader.GetString(1);
            if (!options.IsFunctionIncluded(name)) continue;
            results.Add(new FunctionModel
            {
                Schema = reader.GetString(0),
                Name = name,
                Definition = reader.IsDBNull(2) ? string.Empty : reader.GetString(2),
                ReturnType = reader.IsDBNull(3) ? string.Empty : reader.GetString(3)
            });
        }

        return results;
    }

    private static async Task<List<TriggerModel>> ExtractTriggersAsync(NpgsqlConnection conn, CancellationToken ct)
    {
        const string query = @"
            SELECT
                trigger_schema,
                trigger_name,
                event_object_table,
                action_timing,
                event_manipulation,
                action_statement
            FROM information_schema.triggers
            WHERE trigger_schema NOT IN ('pg_catalog','information_schema')
            ORDER BY trigger_name, event_manipulation";

        var results = new List<TriggerModel>();
        var triggerMap = new Dictionary<string, TriggerModel>(StringComparer.OrdinalIgnoreCase);

        await using var cmd = new NpgsqlCommand(query, conn);
        await using var reader = await cmd.ExecuteReaderAsync(ct);

        while (await reader.ReadAsync(ct))
        {
            var name = reader.GetString(1);
            if (!triggerMap.TryGetValue(name, out var trigger))
            {
                trigger = new TriggerModel
                {
                    Schema = reader.GetString(0),
                    Name = name,
                    TableName = reader.GetString(2),
                    Timing = reader.GetString(3) switch
                    {
                        "BEFORE" => TriggerTiming.Before,
                        "INSTEAD OF" => TriggerTiming.InsteadOf,
                        _ => TriggerTiming.After
                    },
                    Definition = reader.GetString(5)
                };
                triggerMap[name] = trigger;
                results.Add(trigger);
            }

            var eventType = reader.GetString(4) switch
            {
                "INSERT" => TriggerEvent.Insert,
                "UPDATE" => TriggerEvent.Update,
                "DELETE" => TriggerEvent.Delete,
                _ => (TriggerEvent?)null
            };

            if (eventType.HasValue && !trigger.Events.Contains(eventType.Value))
                trigger.Events.Add(eventType.Value);
        }

        return results;
    }

    private static async Task<List<SequenceModel>> ExtractSequencesAsync(NpgsqlConnection conn, CancellationToken ct)
    {
        const string query = @"
            SELECT
                sequence_schema,
                sequence_name,
                data_type,
                start_value,
                increment,
                minimum_value,
                maximum_value,
                cycle_option
            FROM information_schema.sequences
            WHERE sequence_schema NOT IN ('pg_catalog','information_schema')
            ORDER BY sequence_name";

        var results = new List<SequenceModel>();
        await using var cmd = new NpgsqlCommand(query, conn);
        await using var reader = await cmd.ExecuteReaderAsync(ct);

        while (await reader.ReadAsync(ct))
        {
            results.Add(new SequenceModel
            {
                Schema = reader.GetString(0),
                Name = reader.GetString(1),
                DataType = reader.GetString(2),
                StartValue = long.Parse(reader.GetString(3)),
                Increment = long.Parse(reader.GetString(4)),
                MinValue = reader.IsDBNull(5) ? null : (long?)long.Parse(reader.GetString(5)),
                MaxValue = reader.IsDBNull(6) ? null : (long?)long.Parse(reader.GetString(6)),
                Cycle = reader.GetString(7) == "YES"
            });
        }

        return results;
    }

    private static async Task<List<string>> QueryStringsAsync(NpgsqlConnection conn, string query, CancellationToken ct)
    {
        var results = new List<string>();
        await using var cmd = new NpgsqlCommand(query, conn);
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
            results.Add(reader.GetString(0));
        return results;
    }

    private static ForeignKeyAction ParseFkAction(string action) => action switch
    {
        "CASCADE" => ForeignKeyAction.Cascade,
        "SET NULL" => ForeignKeyAction.SetNull,
        "SET DEFAULT" => ForeignKeyAction.SetDefault,
        "RESTRICT" => ForeignKeyAction.Restrict,
        _ => ForeignKeyAction.NoAction
    };
}
