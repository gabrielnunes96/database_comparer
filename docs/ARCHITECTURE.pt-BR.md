# Documento de Arquitetura — Database Schema Tool

> Versão em inglês: [ARCHITECTURE.md](ARCHITECTURE.md)

## 1. Visão Geral

A aplicação é estruturada como uma solução ASP.NET Core 8 em camadas. A UI Blazor Server conversa com um conjunto de serviços orquestradores, que por sua vez delegam para componentes específicos de cada dialeto ou para componentes agnósticos de dialeto.

```
┌────────────────────────────────────────────────────────────────────┐
│                  UI Blazor Server (DatabaseTool.Web)               │
│  Páginas: Converter · Comparator · QueryConverter · TypeMapping · Audit│
│  Shared: SourceSelector · ObjectSelector · ConversionResultPanel   │
│          DiffReportPanel · ExecutionReportPanel · MappingTable     │
└──────────────────────────┬─────────────────────────────────────────┘
                           │ injeta
         ┌─────────────────▼──────────────────────────────────────┐
         │             SchemaService  (scoped)                     │
         │  Orquestra: Extrair → Converter → Comparar → Executar  │
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

## 2. Projetos

### 2.1 `DatabaseTool.Core`

O coração agnóstico de dialeto do sistema.

**Models (`Core/Models/`)**

| Classe | Propósito |
|---|---|
| `SchemaModel` | Container raiz: enum de dialeto, nome do banco, descrição da origem, listas de todos os tipos de objeto |
| `TableModel` | Tabela: nome, schema, colunas, PK, FKs, índices, triggers, restrições check/unique |
| `ColumnModel` | Coluna: nome, tipo de dado, nullability, tamanho máximo, valor padrão, flag identity |
| `PrimaryKeyModel` | Nome da PK + lista de colunas |
| `ForeignKeyModel` | Nome da FK, colunas, tabela/colunas referenciadas, ações ON DELETE/UPDATE |
| `IndexModel` | Nome do índice, colunas, flag de unicidade |
| `ViewModel` | View: nome, schema, definição (SQL bruto) |
| `StoredProcedureModel` | SP: nome, schema, parâmetros, definição (SQL bruto) |
| `FunctionModel` | Function: nome, schema, parâmetros, tipo de retorno, tipo de função, definição |
| `TriggerModel` | Trigger: nome, tabela, timing, eventos, definição |
| `SequenceModel` | Sequence: nome, schema, start, increment, min/max |
| `ExtractionOptions` | Quais tipos de objeto e nomes incluir durante a extração |

**Interfaces (`Core/Interfaces/`)**

| Interface | Propósito |
|---|---|
| `ISchemaExtractor` | Extrai `SchemaModel` de uma conexão de banco ao vivo; lista tipos de objeto individualmente |
| `ISqlFileParser` | Faz parse de texto SQL para `SchemaModel` |
| `ISchemaConverter` | Converte `SchemaModel` de um dialeto para outro; retorna `ConversionResult` |
| `ISchemaDiffer` | Compara duas instâncias de `SchemaModel`; retorna `DiffReport` |
| `IExecutionEngine` | Executa ou faz dry-run de um script SQL contra um banco ao vivo; retorna `ExecutionReport` |

**Conversion (`Core/Conversion/`)**

| Classe | Propósito |
|---|---|
| `TypeMappingConfig` | Par de dicionários (SS→PG, PG→SS) com entradas padrão e métodos `MapXxx` |
| `ConversionOptions` | Schema alvo, flags de DROP, IF NOT EXISTS, configuração de mapeamento de tipos |
| `SqlServerToPostgreSqlConverter` | Implementa `ISchemaConverter`; mapeia tipos, gera DDL PostgreSQL |
| `PostgreSqlToSqlServerConverter` | Implementa `ISchemaConverter`; mapeia tipos, gera DDL T-SQL |
| `QueryConverter` | Substituição baseada em regex de funções/sintaxe para conversão de snippets |

**Diff (`Core/Diff/`)**

| Classe | Propósito |
|---|---|
| `SchemaDiffer` | Implementa `ISchemaDiffer`; compara tabelas (colunas, PK, índices), views, procedures, functions, triggers |
| `DiffReport` | Resultado raiz do diff: modelos source + target, diffs por seção, flags/contagens computadas |
| `SectionDiff<T>` | Genérico: OnlyInSource[], OnlyInTarget[], Different[] |
| `TableDiff` | Diff detalhado de tabela: diffs de coluna, diffs de índice, PK alterada |
| `ColumnDiff` | Diff por coluna com tipo, lista de mudanças, tipo do diff (Added/Removed/Modified) |
| `ObjectDiff` | Diff simples para views/procedures/functions/triggers (flag de definição alterada) |
| `SyncScriptGenerator` | Gera DDL para aplicar mudanças do source no target |

**Execution (`Core/Execution/`)**

| Classe | Propósito |
|---|---|
| `SqlExecutionEngine` | Base abstrata; divide SQL por GO ou `;`; rastreia resultados de statements |
| `ExecutionReport` | Flag de sucesso, resultados por statement, lista de erros, duração |
| `StatementResult` | Por statement: índice, preview, sucesso, erro |
| `FileGenerator` | Escreve um arquivo `.sql` com cabeçalho de metadados |

---

### 2.2 `DatabaseTool.SqlServer`

| Classe | Propósito |
|---|---|
| `SqlServerSchemaExtractor` | Consulta `INFORMATION_SCHEMA` e views `sys.*` via `Microsoft.Data.SqlClient` |
| `SqlServerFileParser` | Faz parse de T-SQL usando `Microsoft.SqlServer.TransactSql.ScriptDom` (Sql160ScriptGenerator) |
| `SqlServerExecutionEngine` | Estende `SqlExecutionEngine`; usa `SqlConnection`, `BEGIN TRAN`/`ROLLBACK` para dry-run |

---

### 2.3 `DatabaseTool.PostgreSQL`

| Classe | Propósito |
|---|---|
| `PostgreSqlSchemaExtractor` | Consulta `information_schema` e `pg_catalog` via `Npgsql` |
| `PostgreSqlFileParser` | Faz parse de DDL PostgreSQL usando `pgsqlparser` (AST protobuf, dispatch via `Node.NodeCase`) |
| `PostgreSqlExecutionEngine` | Estende `SqlExecutionEngine`; usa `NpgsqlConnection`, `BEGIN`/`ROLLBACK` para dry-run |

---

### 2.4 `DatabaseTool.Web`

Aplicação Blazor Server. Todos os componentes usam `@rendermode InteractiveServer`.

**Services (`Web/Services/`)**

| Serviço | Lifetime | Propósito |
|---|---|---|
| `SchemaService` | Scoped | Orquestrador — resolve serviços DI keyed e delega para o Core |
| `AuditService` | Scoped | Lista em memória de `AuditEntry` (até 500); `Log()`, `GetEntries()`, `Clear()` |
| `TypeMappingService` | Singleton | Mantém o `TypeMappingConfig` ativo; carrega/salva `typemapping.json` ao lado da DLL |
| `AppLanguageService` | Scoped | Estado de idioma por circuito Blazor; lookup `this["key"]`; dispara evento `OnLanguageChanged` |

**Componentes-chave (`Web/Components/`)**

| Componente | Propósito |
|---|---|
| `SourceSelector` | Seleciona dialeto + modo de entrada (conexão / texto SQL), testa conexão, carrega lista de objetos |
| `ObjectSelector` | Lista de multi-seleção de objetos do banco com Select All / None |
| `ObjectSelector` | Mostra SQL gerado, avisos, botões Copy/Download/Run Dry-Run |
| `ExecutionReportPanel` | Mostra resultados de dry-run ou execução por statement, botão de confirmação |
| `DiffReportPanel` | Renderiza todas as seções `SectionDiff` com codificação por cor |
| `DiffSection` | Renderiza uma seção (tabelas/views/etc.) dividida em três colunas |
| `MappingTable` | Grid editável de linhas de mapeamento de tipos |
| `LanguageSwitcher` | Botões de bandeira que chamam `AppLanguageService.SetLanguage()` |
| `LocalizedComponentBase` | Classe base — se inscreve em `OnLanguageChanged` e chama `StateHasChanged` |

**Injeção de Dependência (Program.cs)**

```csharp
// Keyed pela string de dialeto "sqlserver" / "postgresql"
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

**Habilitando interatividade Blazor**

Em `Components/App.razor`, tanto `HeadOutlet` quanto `Routes` declaram `@rendermode="InteractiveServer"`. Isso é necessário para que o `NavMenu` e o `LanguageSwitcher` no layout participem do circuito interativo (sem isso, `@onclick` não dispara).

---

## 3. Fluxos Principais

### 3.1 Conversão de Schema

```
Usuário preenche o SourceSelector (dialeto + conexão/SQL)
  → SchemaService.ExtractFromConnectionAsync / ExtractFromFileAsync
    → ISchemaExtractor (BD ao vivo) ou ISqlFileParser (texto SQL)
      → SchemaModel (agnóstico de dialeto)
        → ISchemaConverter.ConvertAsync(SchemaModel, ConversionOptions)
          → ConversionResult { Script, Warnings }
            → ConversionResultPanel mostra o script
              → [se "execute"] SchemaService.DryRunAsync
                → IExecutionEngine.DryRunAsync → ExecutionReport
                  → ExecutionReportPanel mostra resultados por statement
                    → [se sem erros críticos] usuário confirma
                      → IExecutionEngine.ExecuteAsync → ExecutionReport
```

### 3.2 Comparação de Schema

```
Usuário preenche SourceSelector A + SourceSelector B
  → Extrai SchemaModel A e SchemaModel B (qualquer combinação)
    → ISchemaDiffer.Compare(A, B) → DiffReport
      → DiffReportPanel renderiza todas as seções
        → [opcional] SyncScriptGenerator.Generate(DiffReport) → script SQL
          → usuário baixa o script ou prossegue com dry-run/execute
```

### 3.3 Persistência do Type Mapping

```
App inicia → TypeMappingService lê typemapping.json (se existir)
  fallback → TypeMappingConfig.Default
Usuário edita na TypeMappingPage → Save → TypeMappingService.Update(config)
  → escreve typemapping.json ao lado da DLL
ConverterPage lê TypeMappingService.Active na hora da conversão
```

---

## 4. Abordagem de Parsing SQL

### T-SQL (ScriptDom)

`SqlServerFileParser` usa `TSql160Parser` → AST `TSqlScript`. O padrão Visitor faz dispatch pelo tipo de statement (`CreateTableStatement`, `CreateViewStatement`, `CreateProcedureStatement`, etc.). O texto SQL é regenerado a partir dos nós da AST via `Sql160ScriptGenerator.GenerateScript`.

### PostgreSQL (pgsqlparser)

`PostgreSqlFileParser` usa `Parser.Parse(sql, ParserOptions.Default)` → `ParseResult`. Cada `RawStmt.Stmt` é um `Node`; o tipo é determinado por `Node.NodeCase` (ex.: `Node.NodeOneofCase.CreateStmt`). Todos os campos de lista são `RepeatedField<Node>` (protobuf).

---

## 5. Testes

Os testes ficam em `tests/`. Cada projeto tem seu próprio assembly de teste.

| Assembly | Cobertura |
|---|---|
| `DatabaseTool.Core.Tests` | `TypeMappingConfig` (mapeamentos padrão, case-insensitive, overrides customizados), `QueryConverter` (todas as substituições nas duas direções, warnings), `SchemaDiffer` (add/remove/modify tabelas, colunas, views; nomes case-insensitive), `SqlServerToPostgreSqlConverter` / `PostgreSqlToSqlServerConverter` (saída DDL, mapeamento de tipos, PK, FK, IF NOT EXISTS, schema alvo), `SyncScriptGenerator` (CREATE do diff, DROP comentado) |
| `DatabaseTool.SqlServer.Tests` | `SqlServerFileParser` (tabelas, tipos de coluna, nullability, identity, PK, FK, múltiplas tabelas, views, procedures, entrada vazia) |
| `DatabaseTool.PostgreSQL.Tests` | `PostgreSqlFileParser` (tabelas, tipos de coluna, nullability, PK, FK, múltiplas tabelas, views, functions, entrada vazia) |

Execute com:

```powershell
dotnet test DatabaseTool.sln
```

Nota: Testes que exigem conexão com banco ao vivo **não estão incluídos**. Devem ser executados contra uma instância real de SQL Server / PostgreSQL num projeto separado de testes de integração.

---

## 6. Localização

Todas as strings da UI vivem em `Services/Localization/Translations.cs`, num `Dictionary<lang, Dictionary<key, value>>` aninhado.

`AppLanguageService` (scoped) mantém o idioma atual por circuito Blazor e expõe `this["key"]` para lookup e `OnLanguageChanged` para re-render reativo.

Páginas/componentes herdam de `LocalizedComponentBase`, que se inscreve em `OnLanguageChanged` e chama `StateHasChanged` automaticamente.

Idiomas suportados: `en-US` (padrão), `pt-BR`.

---

## 7. Notas de Deploy

- A app tem alvo **framework-dependent** win-x64 (requer runtime .NET 8 no servidor).
- Hosting IIS usa **AspNetCoreModuleV2** em modo in-process (`web.config` incluído).
- `typemapping.json` é escrito em `AppContext.BaseDirectory`; garanta que a identidade do App Pool tenha acesso de escrita.
- O publish profile `IIS_EC2.pubxml` tem alvo Release + win-x64.

---

## 8. Pontos de Extensão

| O que estender | Onde |
|---|---|
| Adicionar um novo dialeto de banco | Implemente `ISchemaExtractor`, `ISqlFileParser`, `IExecutionEngine` num novo projeto; registre com uma nova chave em `Program.cs` |
| Adicionar um novo mapeamento de tipo | Edite `TypeMappingConfig.Default` ou use a UI de Type Mapping |
| Adicionar um novo idioma da UI | Adicione entradas em `Translations.cs` e em `Translations.SupportedLanguages`; atualize `LanguageSwitcher.razor` |
| Adicionar novas regras de conversão | Edite `SqlServerToPostgreSqlConverter.cs` ou `PostgreSqlToSqlServerConverter.cs`; adicione testes unitários em `Core.Tests` |
| Persistir log de auditoria | Substitua a `List<AuditEntry>` em memória em `AuditService` por uma escrita em banco ou arquivo |
