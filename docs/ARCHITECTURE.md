# Architecture Document — Database Schema Tool

## 1. High-Level Overview

The application is structured as a layered ASP.NET Core 8 solution. The Blazor Server UI talks to a set of orchestration services, which in turn delegate to dialect-specific or dialect-agnostic core components.

```
┌────────────────────────────────────────────────────────────────────┐
│                  Blazor Server UI (DatabaseTool.Web)               │
│  Pages: Converter · Comparator · QueryConverter · TypeMapping · Audit│
│  Shared: SourceSelector · ObjectSelector · ConversionResultPanel   │
│          DiffReportPanel · ExecutionReportPanel · MappingTable     │
└──────────────────────────┬─────────────────────────────────────────┘
                           │ injects
         ┌─────────────────▼──────────────────────────────────────┐
         │             SchemaService  (scoped)                     │
         │  Orchestrates: Extract → Convert → Compare → Execute   │
         └───┬───────────────┬──────────────────┬─────────────────┘
             │               │                  │
    ISchemaExtractor   ISqlFileParser     ISchemaConverter
    ISchemaDiffer      IExecutionEngine
             │               │                  │
   ┌─────────┴──┐    ┌───────┴──────┐   ┌──────┴──────────────────┐
   │ DatabaseTool│   │DatabaseTool  │   │    DatabaseTool.Core     │
   │ .SqlServer  │   │.PostgreSQL   │   │  SqlServerToPostgreSql   │
   │             │   │              │   │  PostgreSqlToSqlServer   │
   │ Extractor   │   │ Extractor    │   │  QueryConverter          │
   │ FileParser  │   │ FileParser   │   │  SchemaDiffer            │
   │ ExecEngine  │   │ ExecEngine   │   │  SyncScriptGenerator     │
   └─────────────┘   └──────────────┘   │  FileGenerator           │
                                        └──────────────────────────┘
```

---

## 2. Projects

### 2.1 `DatabaseTool.Core`

The dialect-agnostic heart of the system.

**Models (`Core/Models/`)**

| Class | Purpose |
|---|---|
| `SchemaModel` | Root container: dialect enum, database name, source description, lists of all object types |
| `TableModel` | Table: name, schema, columns, PK, FKs, indexes, triggers, check/unique constraints |
| `ColumnModel` | Column: name, data type, nullability, max length, default value, identity flag |
| `PrimaryKeyModel` | PK name + column list |
| `ForeignKeyModel` | FK name, columns, referenced table/columns, ON DELETE/UPDATE actions |
| `IndexModel` | Index name, columns, uniqueness flag |
| `ViewModel` | View: name, schema, definition (raw SQL) |
| `StoredProcedureModel` | SP: name, schema, parameters, definition (raw SQL) |
| `FunctionModel` | Function: name, schema, parameters, return type, function type, definition |
| `TriggerModel` | Trigger: name, table, timing, events, definition |
| `SequenceModel` | Sequence: name, schema, start, increment, min/max |
| `ExtractionOptions` | Which object types and names to include during extraction |

**Interfaces (`Core/Interfaces/`)**

| Interface | Purpose |
|---|---|
| `ISchemaExtractor` | Extract `SchemaModel` from a live database connection; list individual object types |
| `ISqlFileParser` | Parse SQL text into `SchemaModel` |
| `ISchemaConverter` | Convert `SchemaModel` from one dialect to another; returns `ConversionResult` |
| `ISchemaDiffer` | Compare two `SchemaModel` instances; returns `DiffReport` |
| `IExecutionEngine` | Execute or dry-run a SQL script against a live database; returns `ExecutionReport` |

**Conversion (`Core/Conversion/`)**

| Class | Purpose |
|---|---|
| `TypeMappingConfig` | Dictionary-pair (SS→PG, PG→SS) with default entries and `MapXxx` methods |
| `ConversionOptions` | Target schema, DROP flags, IF NOT EXISTS, type mapping config |
| `SqlServerToPostgreSqlConverter` | Implements `ISchemaConverter`; maps types, generates PostgreSQL DDL |
| `PostgreSqlToSqlServerConverter` | Implements `ISchemaConverter`; maps types, generates T-SQL DDL |
| `QueryConverter` | Regex-based function/syntax replacement for snippet conversion |

**Diff (`Core/Diff/`)**

| Class | Purpose |
|---|---|
| `SchemaDiffer` | Implements `ISchemaDiffer`; compares tables (columns, PK, indexes), views, procedures, functions, triggers |
| `DiffReport` | Root diff result: source + target models, per-section diffs, computed flags/counts |
| `SectionDiff<T>` | Generic: OnlyInSource[], OnlyInTarget[], Different[] |
| `TableDiff` | Detailed table diff: column diffs, index diffs, PK changed |
| `ColumnDiff` | Per-column diff with type, change list, diff type (Added/Removed/Modified) |
| `ObjectDiff` | Simple diff for views/procedures/functions/triggers (definition changed flag) |
| `SyncScriptGenerator` | Generates DDL to apply source changes on the target |

**Execution (`Core/Execution/`)**

| Class | Purpose |
|---|---|
| `SqlExecutionEngine` | Abstract base; splits SQL by GO or `;`; tracks statement results |
| `ExecutionReport` | Success flag, per-statement results, error list, duration |
| `StatementResult` | Per-statement: index, preview, success, error |
| `FileGenerator` | Writes a `.sql` file with metadata header |

---

### 2.2 `DatabaseTool.SqlServer`

| Class | Purpose |
|---|---|
| `SqlServerSchemaExtractor` | Queries `INFORMATION_SCHEMA` and `sys.*` views via `Microsoft.Data.SqlClient` |
| `SqlServerFileParser` | Parses T-SQL using `Microsoft.SqlServer.TransactSql.ScriptDom` (Sql160ScriptGenerator) |
| `SqlServerExecutionEngine` | Extends `SqlExecutionEngine`; uses `SqlConnection`, `BEGIN TRAN`/`ROLLBACK` for dry-run |

---

### 2.3 `DatabaseTool.PostgreSQL`

| Class | Purpose |
|---|---|
| `PostgreSqlSchemaExtractor` | Queries `information_schema` and `pg_catalog` via `Npgsql` |
| `PostgreSqlFileParser` | Parses PostgreSQL DDL using `pgsqlparser` (protobuf AST, `Node.NodeCase` dispatch) |
| `PostgreSqlExecutionEngine` | Extends `SqlExecutionEngine`; uses `NpgsqlConnection`, `BEGIN`/`ROLLBACK` for dry-run |

---

### 2.4 `DatabaseTool.Web`

Blazor Server application. All components use `@rendermode InteractiveServer`.

**Services (`Web/Services/`)**

| Service | Lifetime | Purpose |
|---|---|---|
| `SchemaService` | Scoped | Orchestrator — resolves keyed DI services and delegates to Core |
| `AuditService` | Scoped | In-memory list of `AuditEntry` (up to 500); `Log()`, `GetEntries()`, `Clear()` |
| `TypeMappingService` | Singleton | Holds active `TypeMappingConfig`; loads/saves `typemapping.json` next to the DLL |
| `AppLanguageService` | Scoped | Per-circuit language state; `this["key"]` lookup; fires `OnLanguageChanged` event |

**Key Components (`Web/Components/`)**

| Component | Purpose |
|---|---|
| `SourceSelector` | Selects dialect + input mode (connection / SQL text), tests connection, loads object list |
| `ObjectSelector` | Multi-select list of database objects with Select All / None |
| `ConversionResultPanel` | Shows generated SQL, warnings, Copy/Download/Run Dry-Run buttons |
| `ExecutionReportPanel` | Shows dry-run or execution results per statement, confirm button |
| `DiffReportPanel` | Renders all `SectionDiff` sections with colour coding |
| `DiffSection` | Renders one section (tables/views/etc.) split into three columns |
| `MappingTable` | Editable grid of type mapping rows |
| `LanguageSwitcher` | Flag buttons that call `AppLanguageService.SetLanguage()` |
| `LocalizedComponentBase` | Base class — subscribes to `OnLanguageChanged` and calls `StateHasChanged` |

**Dependency Injection (Program.cs)**

```csharp
// Keyed by dialect string "sqlserver" / "postgresql"
AddKeyedScoped<ISchemaExtractor,   SqlServerSchemaExtractor>("sqlserver")
AddKeyedScoped<ISchemaExtractor,   PostgreSqlSchemaExtractor>("postgresql")
AddKeyedScoped<ISqlFileParser,     SqlServerFileParser>("sqlserver")
AddKeyedScoped<ISqlFileParser,     PostgreSqlFileParser>("postgresql")
AddKeyedScoped<IExecutionEngine,   SqlServerExecutionEngine>("sqlserver")
AddKeyedScoped<IExecutionEngine,   PostgreSqlExecutionEngine>("postgresql")

AddScoped<SqlServerToPostgreSqlConverter>()
AddScoped<PostgreSqlToSqlServerConverter>()
AddScoped<QueryConverter>()
AddScoped<ISchemaDiffer, SchemaDiffer>()
AddScoped<SyncScriptGenerator>()
AddScoped<FileGenerator>()
AddScoped<SchemaService>()
AddScoped<AuditService>()
AddSingleton<TypeMappingService>()
AddScoped<AppLanguageService>()
```

---

## 3. Key Workflows

### 3.1 Schema Conversion

```
User fills SourceSelector (dialect + connection/SQL)
  → SchemaService.ExtractFromConnectionAsync / ExtractFromFileAsync
    → ISchemaExtractor (live DB) or ISqlFileParser (SQL text)
      → SchemaModel (dialect-agnostic)
        → ISchemaConverter.ConvertAsync(SchemaModel, ConversionOptions)
          → ConversionResult { Script, Warnings }
            → ConversionResultPanel shows script
              → [if "execute"] SchemaService.DryRunAsync
                → IExecutionEngine.DryRunAsync → ExecutionReport
                  → ExecutionReportPanel shows per-statement results
                    → [if no critical errors] user confirms
                      → IExecutionEngine.ExecuteAsync → ExecutionReport
```

### 3.2 Schema Comparison

```
User fills SourceSelector A + SourceSelector B
  → Extract SchemaModel A and SchemaModel B (any combination)
    → ISchemaDiffer.Compare(A, B) → DiffReport
      → DiffReportPanel renders all sections
        → [optional] SyncScriptGenerator.Generate(DiffReport) → SQL script
          → user downloads script or proceeds with dry-run/execute
```

### 3.3 Type Mapping Persistence

```
App starts → TypeMappingService reads typemapping.json (if present)
  fallback → TypeMappingConfig.Default
User edits in TypeMappingPage → Save → TypeMappingService.Update(config)
  → writes typemapping.json next to the DLL
ConverterPage reads TypeMappingService.Active at conversion time
```

---

## 4. SQL Parsing Approach

### T-SQL (ScriptDom)

`SqlServerFileParser` uses `TSql160Parser` → `TSqlScript` AST. Visitor pattern dispatches on statement type (`CreateTableStatement`, `CreateViewStatement`, `CreateProcedureStatement`, etc.). SQL text is regenerated from AST nodes via `Sql160ScriptGenerator.GenerateScript`.

### PostgreSQL (pgsqlparser)

`PostgreSqlFileParser` uses `Parser.Parse(sql, ParserOptions.Default)` → `ParseResult`. Each `RawStmt.Stmt` is a `Node`; type is determined via `Node.NodeCase` (e.g., `Node.NodeOneofCase.CreateStmt`). All list fields are `RepeatedField<Node>` (protobuf).

---

## 5. Testing

Tests live in `tests/`. Each project has its own test assembly.

| Assembly | Coverage |
|---|---|
| `DatabaseTool.Core.Tests` | `TypeMappingConfig` (default mappings, case-insensitivity, custom overrides), `QueryConverter` (all replacements in both directions, warnings), `SchemaDiffer` (add/remove/modify tables, columns, views; case-insensitive names), `SqlServerToPostgreSqlConverter` / `PostgreSqlToSqlServerConverter` (DDL output, type mapping, PK, FK, IF NOT EXISTS, target schema), `SyncScriptGenerator` (CREATE from diff, DROP comments) |
| `DatabaseTool.SqlServer.Tests` | `SqlServerFileParser` (tables, column types, nullability, identity, PK, FK, multiple tables, views, procedures, empty input) |
| `DatabaseTool.PostgreSQL.Tests` | `PostgreSqlFileParser` (tables, column types, nullability, PK, FK, multiple tables, views, functions, empty input) |

Run with:

```powershell
dotnet test DatabaseTool.sln
```

Note: Tests that require live database connections are **not included**. Those should be run against a real SQL Server / PostgreSQL instance in a separate integration test project.

---

## 6. Localization

All UI strings live in `Services/Localization/Translations.cs` in a nested `Dictionary<lang, Dictionary<key, value>>`.

`AppLanguageService` (scoped) holds the current language per Blazor circuit and exposes `this["key"]` for lookup and `OnLanguageChanged` for reactive re-render.

Pages/components inherit from `LocalizedComponentBase`, which subscribes to `OnLanguageChanged` and calls `StateHasChanged` automatically.

Supported languages: `en-US` (default), `pt-BR`.

---

## 7. Deployment Notes

- The app targets **framework-dependent** win-x64 (requires .NET 8 runtime on the server).
- IIS hosting uses **AspNetCoreModuleV2** in-process mode (`web.config` included).
- `typemapping.json` is written to `AppContext.BaseDirectory`; ensure the App Pool identity has write access.
- The publish profile `IIS_EC2.pubxml` targets Release + win-x64.

---

## 8. Extension Points

| What to extend | Where |
|---|---|
| Add a new database dialect | Implement `ISchemaExtractor`, `ISqlFileParser`, `IExecutionEngine` in a new project; register with a new key in `Program.cs` |
| Add a new type mapping | Edit `TypeMappingConfig.Default` or use the Type Mapping UI |
| Add a new UI language | Add entries to `Translations.cs` and `Translations.SupportedLanguages`; update `LanguageSwitcher.razor` |
| Add new conversion rules | Edit `SqlServerToPostgreSqlConverter.cs` or `PostgreSqlToSqlServerConverter.cs`; add unit tests in `Core.Tests` |
| Persist audit log | Replace the in-memory `List<AuditEntry>` in `AuditService` with a database or file writer |
