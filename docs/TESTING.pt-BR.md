# Guia de Testes

> Versão em inglês: [TESTING.md](TESTING.md)

## Visão Geral

A solução tem três projetos de testes unitários em `tests/`. Todos os testes são escritos com **xUnit** e rodam sem dependências externas (não exigem banco ao vivo).

```
tests/
├── DatabaseTool.Core.Tests/         74 testes
├── DatabaseTool.SqlServer.Tests/    12 testes
└── DatabaseTool.PostgreSQL.Tests/   11 testes
Total: 97 testes
```

---

## Executando os Testes

```powershell
# Roda todos os testes
dotnet test DatabaseTool.sln

# Roda um único projeto
dotnet test tests/DatabaseTool.Core.Tests/

# Roda com verbosidade (mostra cada nome de teste)
dotnet test DatabaseTool.sln --logger "console;verbosity=normal"

# Roda com cobertura (exige coverlet)
dotnet test DatabaseTool.sln --collect:"XPlat Code Coverage"
```

---

## Projetos de Teste

### `DatabaseTool.Core.Tests`

Testa a lógica de negócio agnóstica de dialeto. Sem dependências externas.

| Classe de teste | O que é testado |
|---|---|
| `TypeMappingConfigTests` | Mapeamentos padrão SQL Server → PostgreSQL e PostgreSQL → SQL Server; case-insensitivity; passthrough de tipos desconhecidos; override de configuração customizada |
| `QueryConverterTests` | SS→PG: GETDATE, ISNULL, LEN, NEWID, remoção de TOP, identificadores entre colchetes, remoção de prefixo N, avisos para DATEDIFF. PG→SS: NOW, COALESCE, LENGTH, gen_random_uuid, remoção de LIMIT, identificadores com aspas duplas, `||` → `+`, remoção de cast `::` |
| `SchemaDifferTests` | Tabelas apenas no source, apenas no target, colunas adicionadas/removidas/modificadas, mudanças de tipo, diffs de view, contagem de triggers, nomes case-insensitive, contagem total de diferenças |
| `SchemaConverterTests` | SS→PG: CREATE TABLE, identity → GENERATED ALWAYS AS IDENTITY, nvarchar → varchar, datetime → timestamp, PK, FK, IF NOT EXISTS, schema alvo customizado. PG→SS: CREATE TABLE, boolean → bit, uuid → uniqueidentifier |
| `SyncScriptGeneratorTests` | Diff vazio → nenhum DDL; tabela só no source → CREATE TABLE; view só no source → CREATE VIEW; tabela só no target → DROP comentado |

### `DatabaseTool.SqlServer.Tests`

Testa o parser de arquivos T-SQL usando ScriptDom. Sem conexão com banco.

| Teste | O que é verificado |
|---|---|
| `Parse_SimpleCreateTable_ExtractsTable` | Nome da tabela e contagem de colunas |
| `Parse_CreateTable_ColumnsHaveCorrectTypes` | Strings de tipo de dado para int, nvarchar, money, bit |
| `Parse_CreateTable_NullabilityExtracted` | Colunas NOT NULL vs NULL |
| `Parse_CreateTable_IdentityColumnDetected` | `IDENTITY(1,1)` → `IsIdentity = true` |
| `Parse_CreateTable_PrimaryKeyConstraint` | Nome da PK e lista de colunas |
| `Parse_CreateTable_ForeignKey` | Colunas da FK e tabela referenciada |
| `Parse_MultipleTables_AllExtracted` | Duas tabelas separadas por GO |
| `Parse_CreateView_ExtractsView` | Nome da view e definição não vazia |
| `Parse_CreateProcedure_ExtractsProcedure` | Nome da SP e definição não vazia |
| `Parse_EmptyInput_ReturnsEmptyModel` | String vazia → modelo vazio |
| `Parse_WhitespaceOnly_ReturnsEmptyModel` | Apenas espaços → modelo vazio |

### `DatabaseTool.PostgreSQL.Tests`

Testa o parser de DDL PostgreSQL usando pgsqlparser. Sem conexão com banco.

| Teste | O que é verificado |
|---|---|
| `Parse_SimpleCreateTable_ExtractsTable` | Nome da tabela e contagem de colunas |
| `Parse_CreateTable_ColumnsHaveCorrectTypes` | Colunas presentes com nomes corretos |
| `Parse_CreateTable_NullabilityExtracted` | NOT NULL vs nullable por padrão |
| `Parse_CreateTable_PrimaryKeyConstraint` | Lista de colunas da PK |
| `Parse_CreateTable_ForeignKeyConstraint` | Nome da coluna FK presente |
| `Parse_MultipleTables_AllExtracted` | Duas tabelas num único bloco SQL |
| `Parse_CreateView_ExtractsView` | Nome da view e definição não vazia |
| `Parse_CreateFunction_ExtractsFunction` | Nome da function |
| `Parse_EmptyInput_ReturnsEmptyModel` | String vazia → modelo vazio |
| `Parse_WhitespaceOnly_ReturnsEmptyModel` | Apenas espaços → modelo vazio |

---

## O Que Não É Testado

Os itens a seguir **não são cobertos por testes unitários** porque exigem conexões com banco ao vivo:

- `SqlServerSchemaExtractor` — exige SQL Server
- `PostgreSqlSchemaExtractor` — exige PostgreSQL
- `SqlServerExecutionEngine` (dry-run / execute) — exige SQL Server
- `PostgreSqlExecutionEngine` (dry-run / execute) — exige PostgreSQL

Para adicionar testes de integração, crie um projeto separado (ex.: `DatabaseTool.Integration.Tests`) e use variáveis de ambiente para as connection strings. Marque esses testes com `[Trait("Category", "Integration")]` e pule-os em CI quando os bancos não estiverem disponíveis.

---

## Adicionando Novos Testes

1. Crie uma classe de teste no projeto apropriado.
2. Nomeie como `<ClasseTestada>Tests.cs`.
3. Use `[Fact]` para testes de caso único e `[Theory] + [InlineData]` para testes parametrizados.
4. Siga o padrão **Arrange / Act / Assert**.
5. Rode `dotnet test` para confirmar 0 falhas antes de commitar.

Exemplo:

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
