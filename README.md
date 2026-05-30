# Database Schema Tool

A web application for bidirectional conversion and comparison of database schemas between **SQL Server** and **PostgreSQL**, built with ASP.NET Core 8 and Blazor Server.

---

## Features

| Feature | Description |
|---|---|
| **Schema Converter** | Convert a full schema SQL Server → PostgreSQL or PostgreSQL → SQL Server (tables, views, procedures, functions, triggers, indexes) |
| **Schema Comparator** | Diff two schemas from any combination of sources (live connection or `.sql` file); generate a sync script |
| **Query Converter** | Convert individual SQL snippets between T-SQL and PostgreSQL dialects |
| **Type Mapping Editor** | View and edit the data type mapping table used during conversion |
| **Audit Log** | In-session log of every convert / compare / execute operation |
| **Language Switcher** | UI supports English (EN-US) and Brazilian Portuguese (PT-BR) |

---

## Tech Stack

| Layer | Technology |
|---|---|
| Web framework | ASP.NET Core 8 |
| UI | Blazor Server (`InteractiveServer`) |
| SQL Server parsing | `Microsoft.SqlServer.TransactSql.ScriptDom` |
| PostgreSQL parsing | `pgsqlparser` (protobuf-backed) |
| SQL Server client | `Microsoft.Data.SqlClient` |
| PostgreSQL client | `Npgsql` |
| Testing | xUnit |
| Deployment | IIS on EC2 Windows |

---

## Project Structure

```
DatabaseTool/
├── DatabaseTool.sln
├── src/
│   ├── DatabaseTool.Core/             Core business logic, interfaces, models
│   │   ├── Models/                    Dialect-agnostic schema representation
│   │   ├── Interfaces/                ISchemaExtractor, ISqlFileParser, ISchemaConverter, ISchemaDiffer, IExecutionEngine
│   │   ├── Conversion/                TypeMappingConfig, SqlServerToPostgreSqlConverter, PostgreSqlToSqlServerConverter, QueryConverter
│   │   ├── Diff/                      SchemaDiffer, DiffReport, SyncScriptGenerator
│   │   └── Execution/                 SqlExecutionEngine (base), FileGenerator, ExecutionReport
│   ├── DatabaseTool.SqlServer/        SQL Server extractor, ScriptDom parser, execution engine
│   ├── DatabaseTool.PostgreSQL/       PostgreSQL extractor, pgsqlparser parser, execution engine
│   └── DatabaseTool.Web/              Blazor Server web application
│       ├── Components/
│       │   ├── Layout/                NavMenu (with language switcher)
│       │   ├── Pages/                 Converter, Comparator, QueryConverter, TypeMapping, Audit, Home
│       │   └── Shared/                SourceSelector, ObjectSelector, ConversionResultPanel,
│       │                              ExecutionReportPanel, DiffReportPanel, DiffSection,
│       │                              MappingTable, LanguageSwitcher
│       └── Services/
│           ├── SchemaService.cs       Orchestrates all schema operations for the UI
│           ├── AuditService.cs        In-memory session audit log
│           ├── TypeMappingService.cs  Singleton; loads/saves typemapping.json
│           └── Localization/          Translations.cs + AppLanguageService.cs
└── tests/
    ├── DatabaseTool.Core.Tests/       Unit tests for converters, differ, type mapping, query converter, sync script
    ├── DatabaseTool.SqlServer.Tests/  Unit tests for T-SQL file parser
    └── DatabaseTool.PostgreSQL.Tests/ Unit tests for PostgreSQL file parser
```

---

## Quick Start (Development)

### Prerequisites

- [.NET 8 SDK](https://dotnet.microsoft.com/en-us/download/dotnet/8.0)

### Run

```powershell
cd src/DatabaseTool.Web
dotnet run
```

The app will be available at `https://localhost:5001` (or the port shown in the console).

### Run Tests

```powershell
dotnet test DatabaseTool.sln
```

Expected: **97 tests, 0 failures** (integration tests against live databases are excluded by default).

---

## Deploy to IIS / EC2

See [DEPLOY_IIS_EC2.md](DEPLOY_IIS_EC2.md) for step-by-step instructions.

Quick summary:
1. Install **ASP.NET Core Hosting Bundle 8** on the Windows Server.
2. `dotnet publish -c Release -r win-x64 --no-self-contained`
3. Create an IIS Application Pool (No Managed Code) and point it at the publish folder.
4. Ensure the App Pool identity has write access to the folder (for `typemapping.json`).

---

## Configuration

### Type Mapping

Custom type mappings are stored in `typemapping.json` in the app's base directory. Edit via the **Type Mapping** page in the UI; click **Save Changes**. Click **Reset to Defaults** to revert.

### Localization

The language switcher (🇺🇸 EN / 🇧🇷 PT) appears in the top navigation bar. Language preference is per-browser-session (Blazor circuit). To add a new language, add entries to `Translations.cs` and `Translations.SupportedLanguages`.

---

## Adding a New Language

1. Open `src/DatabaseTool.Web/Services/Localization/Translations.cs`.
2. Add a new language constant, e.g. `public const string EsEs = "es-ES";`.
3. Add it to `SupportedLanguages`.
4. Copy the `en-US` dictionary block, change the key to `EsEs`, and translate all values.
5. Update `LanguageSwitcher.razor` to show the new flag/label.

---

## Architecture Overview

See [docs/ARCHITECTURE.md](docs/ARCHITECTURE.md) for the full architecture document.

---

## License

Internal tooling — not licensed for public distribution.
