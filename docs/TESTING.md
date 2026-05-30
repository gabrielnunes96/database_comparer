# Testing Guide

> Versão em português: [TESTING.pt-BR.md](TESTING.pt-BR.md)

## Overview

The solution has three unit test projects under `tests/`. All tests are written with **xUnit** and run without external dependencies (no live database required).

```
tests/
├── DatabaseTool.Core.Tests/         74 tests
├── DatabaseTool.SqlServer.Tests/    12 tests
└── DatabaseTool.PostgreSQL.Tests/   11 tests
Total: 97 tests
```

---

## Running Tests

```powershell
# Run all tests
dotnet test DatabaseTool.sln

# Run a single project
dotnet test tests/DatabaseTool.Core.Tests/

# Run with verbosity to see each test name
dotnet test DatabaseTool.sln --logger "console;verbosity=normal"

# Run with coverage (requires coverlet)
dotnet test DatabaseTool.sln --collect:"XPlat Code Coverage"
```

---

## Test Projects

### `DatabaseTool.Core.Tests`

Tests the dialect-agnostic business logic. No external dependencies.

| Test class | What is tested |
|---|---|
| `TypeMappingConfigTests` | Default SQL Server → PostgreSQL and PostgreSQL → SQL Server mappings; case-insensitivity; unknown type passthrough; custom config override |
| `QueryConverterTests` | SS→PG: GETDATE, ISNULL, LEN, NEWID, TOP removal, bracket identifiers, N-prefix removal, warnings for DATEDIFF. PG→SS: NOW, COALESCE, LENGTH, gen_random_uuid, LIMIT removal, double-quote identifiers, `||` → `+`, `::` cast removal |
| `SchemaDifferTests` | Tables only-in-source, only-in-target, added/removed/modified columns, type changes, view diffs, trigger count, case-insensitive names, total difference count |
| `SchemaConverterTests` | SS→PG: CREATE TABLE, identity → GENERATED ALWAYS AS IDENTITY, nvarchar → varchar, datetime → timestamp, PK, FK, IF NOT EXISTS, custom target schema. PG→SS: CREATE TABLE, boolean → bit, uuid → uniqueidentifier |
| `SyncScriptGeneratorTests` | Empty diff → no DDL; table only-in-source → CREATE TABLE; view only-in-source → CREATE VIEW; table only-in-target → commented DROP |

### `DatabaseTool.SqlServer.Tests`

Tests the T-SQL file parser using ScriptDom. No database connection required.

| Test | What is verified |
|---|---|
| `Parse_SimpleCreateTable_ExtractsTable` | Table name and column count |
| `Parse_CreateTable_ColumnsHaveCorrectTypes` | Data type strings for int, nvarchar, money, bit |
| `Parse_CreateTable_NullabilityExtracted` | NOT NULL vs NULL columns |
| `Parse_CreateTable_IdentityColumnDetected` | `IDENTITY(1,1)` → `IsIdentity = true` |
| `Parse_CreateTable_PrimaryKeyConstraint` | PK name and column list |
| `Parse_CreateTable_ForeignKey` | FK columns and referenced table |
| `Parse_MultipleTables_AllExtracted` | Two tables separated by GO |
| `Parse_CreateView_ExtractsView` | View name and non-empty definition |
| `Parse_CreateProcedure_ExtractsProcedure` | SP name and non-empty definition |
| `Parse_EmptyInput_ReturnsEmptyModel` | Empty string → empty model |
| `Parse_WhitespaceOnly_ReturnsEmptyModel` | Whitespace-only → empty model |

### `DatabaseTool.PostgreSQL.Tests`

Tests the PostgreSQL DDL parser using pgsqlparser. No database connection required.

| Test | What is verified |
|---|---|
| `Parse_SimpleCreateTable_ExtractsTable` | Table name and column count |
| `Parse_CreateTable_ColumnsHaveCorrectTypes` | Columns present with correct names |
| `Parse_CreateTable_NullabilityExtracted` | NOT NULL vs default-nullable |
| `Parse_CreateTable_PrimaryKeyConstraint` | PK column list |
| `Parse_CreateTable_ForeignKeyConstraint` | FK column name present |
| `Parse_MultipleTables_AllExtracted` | Two tables in one SQL block |
| `Parse_CreateView_ExtractsView` | View name and non-empty definition |
| `Parse_CreateFunction_ExtractsFunction` | Function name |
| `Parse_EmptyInput_ReturnsEmptyModel` | Empty string → empty model |
| `Parse_WhitespaceOnly_ReturnsEmptyModel` | Whitespace-only → empty model |

---

## What Is Not Tested

The following are **not covered by unit tests** because they require live database connections:

- `SqlServerSchemaExtractor` — requires SQL Server
- `PostgreSqlSchemaExtractor` — requires PostgreSQL
- `SqlServerExecutionEngine` (dry-run / execute) — requires SQL Server
- `PostgreSqlExecutionEngine` (dry-run / execute) — requires PostgreSQL

To add integration tests, create a separate project (e.g., `DatabaseTool.Integration.Tests`) and use environment variables for connection strings. Mark those tests with `[Trait("Category", "Integration")]` and skip them in CI if the databases are unavailable.

---

## Adding New Tests

1. Create a test class in the appropriate project.
2. Name it `<ClassUnderTest>Tests.cs`.
3. Use `[Fact]` for single-case tests and `[Theory] + [InlineData]` for parameterized tests.
4. Follow the **Arrange / Act / Assert** pattern.
5. Run `dotnet test` to confirm 0 failures before committing.

Example:

```csharp
[Fact]
public async Task MyParser_SomeCase_ExpectedResult()
{
    // Arrange
    const string sql = "CREATE TABLE foo (id INTEGER);";

    // Act
    var model = await _parser.ParseAsync(sql, ExtractionOptions.All);

    // Assert
    Assert.Single(model.Tables);
    Assert.Equal("foo", model.Tables[0].Name);
}
```
