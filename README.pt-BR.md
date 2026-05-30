# Database Schema Tool

> Versão em inglês: [README.md](README.md)

Aplicação web para conversão e comparação bidirecional de schemas de banco entre **SQL Server** e **PostgreSQL**, construída com ASP.NET Core 8 e Blazor Server.

---

## Funcionalidades

| Funcionalidade | Descrição |
|---|---|
| **Conversor de Schema** | Converte um schema completo SQL Server → PostgreSQL ou PostgreSQL → SQL Server (tabelas, views, procedures, functions, triggers, índices) |
| **Comparador de Schema** | Faz diff de dois schemas em qualquer combinação de fontes (conexão direta ou arquivo `.sql`); gera script de sincronização |
| **Conversor de Query** | Converte snippets SQL individuais entre os dialetos T-SQL e PostgreSQL |
| **Editor de Type Mapping** | Visualiza e edita a tabela de mapeamento de tipos de dados usada na conversão |
| **Log de Auditoria** | Log em sessão de cada operação de conversão / comparação / execução |
| **Seletor de Idioma** | UI suporta Inglês (EN-US) e Português Brasileiro (PT-BR) |

---

## Stack Tecnológica

| Camada | Tecnologia |
|---|---|
| Framework web | ASP.NET Core 8 |
| UI | Blazor Server (`InteractiveServer`) |
| Parsing SQL Server | `Microsoft.SqlServer.TransactSql.ScriptDom` |
| Parsing PostgreSQL | `pgsqlparser` (baseado em protobuf) |
| Cliente SQL Server | `Microsoft.Data.SqlClient` |
| Cliente PostgreSQL | `Npgsql` |
| Testes | xUnit |
| Deploy | IIS no EC2 Windows |

---

## Estrutura do Projeto

```
DatabaseTool/
├── DatabaseTool.sln
├── src/
│   ├── DatabaseTool.Core/             Lógica de negócio, interfaces, models (core)
│   │   ├── Models/                    Representação de schema agnóstica de dialeto
│   │   ├── Interfaces/                ISchemaExtractor, ISqlFileParser, ISchemaConverter, ISchemaDiffer, IExecutionEngine
│   │   ├── Conversion/                TypeMappingConfig, SqlServerToPostgreSqlConverter, PostgreSqlToSqlServerConverter, QueryConverter
│   │   ├── Diff/                      SchemaDiffer, DiffReport, SyncScriptGenerator
│   │   └── Execution/                 SqlExecutionEngine (base), FileGenerator, ExecutionReport
│   ├── DatabaseTool.SqlServer/        Extractor SQL Server, parser ScriptDom, execution engine
│   ├── DatabaseTool.PostgreSQL/       Extractor PostgreSQL, parser pgsqlparser, execution engine
│   └── DatabaseTool.Web/              Aplicação web Blazor Server
│       ├── Components/
│       │   ├── Layout/                NavMenu (com seletor de idioma)
│       │   ├── Pages/                 Converter, Comparator, QueryConverter, TypeMapping, Audit, Home
│       │   └── Shared/                SourceSelector, ObjectSelector, ConversionResultPanel,
│       │                              ExecutionReportPanel, DiffReportPanel, DiffSection,
│       │                              MappingTable, LanguageSwitcher
│       └── Services/
│           ├── SchemaService.cs       Orquestra todas as operações de schema para a UI
│           ├── AuditService.cs        Log de auditoria em memória, por sessão
│           ├── TypeMappingService.cs  Singleton; carrega/salva typemapping.json
│           └── Localization/          Translations.cs + AppLanguageService.cs
└── tests/
    ├── DatabaseTool.Core.Tests/       Testes unitários para converters, differ, type mapping, query converter, sync script
    ├── DatabaseTool.SqlServer.Tests/  Testes unitários para o parser de arquivos T-SQL
    └── DatabaseTool.PostgreSQL.Tests/ Testes unitários para o parser de arquivos PostgreSQL
```

---

## Quick Start (Desenvolvimento)

### Pré-requisitos

- [.NET 8 SDK](https://dotnet.microsoft.com/en-us/download/dotnet/8.0)

### Rodar

```powershell
cd src/DatabaseTool.Web
dotnet run
```

A aplicação ficará disponível em `https://localhost:5001` (ou na porta exibida no console).

### Rodar os Testes

```powershell
dotnet test DatabaseTool.sln
```

Esperado: **97 testes, 0 falhas** (testes de integração contra banco ao vivo são excluídos por padrão).

---

## Deploy no IIS / EC2

Veja [DEPLOY_IIS_EC2.pt-BR.md](DEPLOY_IIS_EC2.pt-BR.md) para instruções passo a passo.

Resumo rápido:
1. Instale o **ASP.NET Core Hosting Bundle 8** no Windows Server.
2. `dotnet publish -c Release -r win-x64 --no-self-contained`
3. Crie um Application Pool no IIS (No Managed Code) apontando para a pasta do publish.
4. Garanta que a identidade do App Pool tenha acesso de escrita à pasta (para o `typemapping.json`).

---

## Configuração

### Type Mapping

Mapeamentos customizados são armazenados em `typemapping.json` no diretório base da aplicação. Edite pela página **Type Mapping** na UI; clique em **Save Changes**. Clique em **Reset to Defaults** para reverter.

### Localização

O seletor de idioma (🇺🇸 EN / 🇧🇷 PT) aparece na barra de navegação superior. A preferência de idioma é por sessão de navegador (circuito Blazor). Para adicionar um novo idioma, adicione entradas em `Translations.cs` e em `Translations.SupportedLanguages`.

---

## Adicionando Um Novo Idioma

1. Abra `src/DatabaseTool.Web/Services/Localization/Translations.cs`.
2. Adicione uma nova constante de idioma, ex.: `public const string EsEs = "es-ES";`.
3. Adicione em `SupportedLanguages`.
4. Copie o bloco do dicionário `en-US`, troque a chave para `EsEs` e traduza todos os valores.
5. Atualize `LanguageSwitcher.razor` para mostrar a nova bandeira/label.

---

## Visão Geral da Arquitetura

Veja [docs/ARCHITECTURE.pt-BR.md](docs/ARCHITECTURE.pt-BR.md) para o documento de arquitetura completo.

Documentação de testes: [docs/TESTING.pt-BR.md](docs/TESTING.pt-BR.md).

---

## Licença

Ferramenta interna — não licenciada para distribuição pública.
