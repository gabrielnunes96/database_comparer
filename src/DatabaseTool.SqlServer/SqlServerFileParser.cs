using DatabaseTool.Core.Interfaces;
using DatabaseTool.Core.Models;
using Microsoft.SqlServer.TransactSql.ScriptDom;

namespace DatabaseTool.SqlServer;

public class SqlServerFileParser : ISqlFileParser
{
    public DatabaseDialect SupportedDialect => DatabaseDialect.SqlServer;

    public Task<SchemaModel> ParseAsync(string sqlContent, ExtractionOptions options, CancellationToken cancellationToken = default)
    {
        var parser = new TSql160Parser(initialQuotedIdentifiers: true);
        using var reader = new StringReader(sqlContent);
        var tree = parser.Parse(reader, out _);

        var model = new SchemaModel
        {
            Dialect = DatabaseDialect.SqlServer,
            SourceDescription = "SQL Server File"
        };

        var visitor = new SchemaVisitor(options, model);
        tree.Accept(visitor);

        return Task.FromResult(model);
    }

    private static string FragmentToSql(TSqlFragment fragment)
    {
        var generator = new Sql160ScriptGenerator();
        generator.GenerateScript(fragment, out var sql);
        return sql;
    }

    private sealed class SchemaVisitor : TSqlFragmentVisitor
    {
        private readonly ExtractionOptions _options;
        private readonly SchemaModel _model;

        public SchemaVisitor(ExtractionOptions options, SchemaModel model)
        {
            _options = options;
            _model = model;
        }

        public override void Visit(CreateTableStatement node)
        {
            var schemaName = node.SchemaObjectName.SchemaIdentifier?.Value ?? "dbo";
            var tableName = node.SchemaObjectName.BaseIdentifier.Value;

            if (!_options.IsTableIncluded(tableName)) return;

            var table = new TableModel { Schema = schemaName, Name = tableName };
            var ordinal = 1;

            foreach (var def in node.Definition.ColumnDefinitions)
            {
                var col = new ColumnModel
                {
                    Name = def.ColumnIdentifier.Value,
                    DataType = GetDataType(def.DataType),
                    IsNullable = true,
                    OrdinalPosition = ordinal++
                };

                // Length/precision
                if (def.DataType is SqlDataTypeReference sqlType && sqlType.Parameters.Count > 0)
                {
                    if (sqlType.Parameters[0] is MaxLiteral)
                        col.MaxLength = -1;
                    else if (int.TryParse(sqlType.Parameters[0].Value, out var len))
                        col.MaxLength = len;

                    if (sqlType.Parameters.Count > 1 && int.TryParse(sqlType.Parameters[1].Value, out var scale))
                        col.Scale = scale;
                }

                // Identity is a direct property on ColumnDefinition, not a constraint
                if (def.IdentityOptions is not null)
                {
                    col.IsIdentity = true;
                    col.IdentitySeed = def.IdentityOptions.IdentitySeed is IntegerLiteral seedLit
                        && long.TryParse(seedLit.Value, out var seed) ? seed : 1;
                    col.IdentityIncrement = def.IdentityOptions.IdentityIncrement is IntegerLiteral incLit
                        && long.TryParse(incLit.Value, out var inc) ? inc : 1;
                }

                foreach (var constraint in def.Constraints)
                {
                    switch (constraint)
                    {
                        case NullableConstraintDefinition nullable:
                            col.IsNullable = nullable.Nullable;
                            break;
                        case UniqueConstraintDefinition unique when unique.IsPrimaryKey:
                            table.PrimaryKey = new PrimaryKeyModel
                            {
                                Name = unique.ConstraintIdentifier?.Value ?? $"PK_{tableName}",
                                Columns = new List<string> { col.Name },
                                IsClustered = unique.Clustered != false
                            };
                            break;
                        case DefaultConstraintDefinition defaultCon when defaultCon.Expression is not null:
                            col.DefaultValue = FragmentToSql(defaultCon.Expression);
                            break;
                    }
                }

                table.Columns.Add(col);
            }

            if (_options.IncludeIndexes)
            {
                foreach (var constraint in node.Definition.TableConstraints)
                {
                    switch (constraint)
                    {
                        case UniqueConstraintDefinition pkDef when pkDef.IsPrimaryKey:
                            table.PrimaryKey = new PrimaryKeyModel
                            {
                                Name = pkDef.ConstraintIdentifier?.Value ?? $"PK_{tableName}",
                                Columns = pkDef.Columns
                                    .Select(c => c.Column.MultiPartIdentifier.Identifiers.Last().Value)
                                    .ToList(),
                                IsClustered = pkDef.Clustered != false
                            };
                            break;
                        case UniqueConstraintDefinition uqDef when !uqDef.IsPrimaryKey:
                            table.UniqueConstraints.Add(new UniqueConstraintModel
                            {
                                Name = uqDef.ConstraintIdentifier?.Value ?? string.Empty,
                                Columns = uqDef.Columns
                                    .Select(c => c.Column.MultiPartIdentifier.Identifiers.Last().Value)
                                    .ToList()
                            });
                            break;
                        case ForeignKeyConstraintDefinition fkDef:
                            table.ForeignKeys.Add(new ForeignKeyModel
                            {
                                Name = fkDef.ConstraintIdentifier?.Value ?? string.Empty,
                                Columns = fkDef.Columns.Select(c => c.Value).ToList(),
                                ReferencedTable = fkDef.ReferenceTableName.BaseIdentifier.Value,
                                ReferencedSchema = fkDef.ReferenceTableName.SchemaIdentifier?.Value ?? "dbo",
                                ReferencedColumns = fkDef.ReferencedTableColumns.Select(c => c.Value).ToList(),
                                OnDelete = ParseDeleteUpdateAction(fkDef.DeleteAction),
                                OnUpdate = ParseDeleteUpdateAction(fkDef.UpdateAction)
                            });
                            break;
                        case CheckConstraintDefinition checkDef:
                            table.CheckConstraints.Add(new CheckConstraintModel
                            {
                                Name = checkDef.ConstraintIdentifier?.Value ?? string.Empty,
                                Expression = checkDef.CheckCondition is not null
                                    ? FragmentToSql(checkDef.CheckCondition)
                                    : string.Empty
                            });
                            break;
                    }
                }
            }

            _model.Tables.Add(table);
        }

        public override void Visit(CreateViewStatement node)
        {
            var viewName = node.SchemaObjectName.BaseIdentifier.Value;
            if (!_options.IsViewIncluded(viewName)) return;

            _model.Views.Add(new ViewModel
            {
                Schema = node.SchemaObjectName.SchemaIdentifier?.Value ?? "dbo",
                Name = viewName,
                Definition = FragmentToSql(node)
            });
        }

        public override void Visit(CreateProcedureStatement node)
        {
            var name = node.ProcedureReference.Name.BaseIdentifier.Value;
            if (!_options.IsProcedureIncluded(name)) return;

            _model.Procedures.Add(new ProcedureModel
            {
                Schema = node.ProcedureReference.Name.SchemaIdentifier?.Value ?? "dbo",
                Name = name,
                Definition = FragmentToSql(node),
                Parameters = node.Parameters.Select(MapParameter).ToList()
            });
        }

        public override void Visit(CreateFunctionStatement node)
        {
            var name = node.Name.BaseIdentifier.Value;
            if (!_options.IsFunctionIncluded(name)) return;

            var returnTypeSql = node.ReturnType is not null ? FragmentToSql(node.ReturnType) : string.Empty;

            _model.Functions.Add(new FunctionModel
            {
                Schema = node.Name.SchemaIdentifier?.Value ?? "dbo",
                Name = name,
                Definition = FragmentToSql(node),
                Parameters = node.Parameters.Select(MapParameter).ToList(),
                ReturnType = returnTypeSql,
                FunctionType = node.ReturnType is SelectFunctionReturnType or TableValuedFunctionReturnType
                    ? FunctionType.TableValued
                    : FunctionType.Scalar
            });
        }

        public override void Visit(CreateTriggerStatement node)
        {
            if (!_options.IncludeTriggers) return;

            var trigger = new TriggerModel
            {
                Schema = node.Name.SchemaIdentifier?.Value ?? "dbo",
                Name = node.Name.BaseIdentifier.Value,
                TableName = node.TriggerObject?.Name?.BaseIdentifier.Value ?? string.Empty,
                Definition = FragmentToSql(node),
                Timing = node.TriggerType == TriggerType.InsteadOf ? TriggerTiming.InsteadOf : TriggerTiming.After
            };

            foreach (var action in node.TriggerActions)
            {
                trigger.Events.Add(action.TriggerActionType switch
                {
                    TriggerActionType.Insert => TriggerEvent.Insert,
                    TriggerActionType.Update => TriggerEvent.Update,
                    TriggerActionType.Delete => TriggerEvent.Delete,
                    _ => TriggerEvent.Insert
                });
            }

            _model.Triggers.Add(trigger);
        }

        public override void Visit(CreateIndexStatement node)
        {
            if (!_options.IncludeIndexes) return;

            var tableName = node.OnName?.BaseIdentifier.Value;
            if (tableName is null) return;

            var table = _model.Tables.FirstOrDefault(t =>
                string.Equals(t.Name, tableName, StringComparison.OrdinalIgnoreCase));
            if (table is null) return;

            table.Indexes.Add(new IndexModel
            {
                Name = node.Name?.Value ?? string.Empty,
                IsUnique = node.Unique,
                IsClustered = node.Clustered == true,
                Columns = node.Columns
                    .Where(c => c.Column is not null)
                    .Select(c => c.Column!.MultiPartIdentifier.Identifiers.Last().Value)
                    .ToList(),
                IncludedColumns = node.IncludeColumns?
                    .Select(c => c.MultiPartIdentifier.Identifiers.Last().Value)
                    .ToList() ?? new List<string>(),
                FilterExpression = node.FilterPredicate is not null
                    ? FragmentToSql(node.FilterPredicate)
                    : null
            });
        }

        public override void Visit(CreateSequenceStatement node)
        {
            if (!_options.IncludeSequences) return;

            _model.Sequences.Add(new SequenceModel
            {
                Schema = node.Name.SchemaIdentifier?.Value ?? "dbo",
                Name = node.Name.BaseIdentifier.Value
            });
        }

        private static RoutineParameterModel MapParameter(ProcedureParameter p) => new()
        {
            Name = p.VariableName.Value,
            DataType = GetDataType(p.DataType),
            Direction = p.Modifier == ParameterModifier.Output ? ParameterDirection.Output : ParameterDirection.Input
        };

        private static string GetDataType(DataTypeReference? dataType)
        {
            if (dataType is SqlDataTypeReference sql)
                return sql.SqlDataTypeOption.ToString().ToLowerInvariant();
            if (dataType is XmlDataTypeReference)
                return "xml";
            if (dataType is not null)
                return FragmentToSql(dataType);
            return "unknown";
        }

        private static ForeignKeyAction ParseDeleteUpdateAction(DeleteUpdateAction action) => action switch
        {
            DeleteUpdateAction.Cascade => ForeignKeyAction.Cascade,
            DeleteUpdateAction.SetNull => ForeignKeyAction.SetNull,
            DeleteUpdateAction.SetDefault => ForeignKeyAction.SetDefault,
            _ => ForeignKeyAction.NoAction
        };
    }
}
