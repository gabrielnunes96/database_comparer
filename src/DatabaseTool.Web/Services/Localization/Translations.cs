namespace DatabaseTool.Web.Services.Localization;

/// <summary>
/// Central store of all UI strings keyed by (lang, key).
/// Add new keys here; keep EN-US as the canonical reference.
/// </summary>
public static class Translations
{
    public const string EnUs = "en-US";
    public const string PtBr = "pt-BR";

    public static readonly IReadOnlyList<string> SupportedLanguages = new[]
    {
        EnUs, PtBr
    };

    private static readonly Dictionary<string, Dictionary<string, string>> _store = new()
    {
        [EnUs] = new()
        {
            // Navigation
            ["nav.home"]          = "Home",
            ["nav.converter"]     = "Converter",
            ["nav.comparator"]    = "Comparator",
            ["nav.queryConverter"]= "Query Converter",
            ["nav.typeMapping"]   = "Type Mapping",
            ["nav.auditLog"]      = "Audit Log",

            // Home
            ["home.title"]        = "DB Schema Tool",
            ["home.subtitle"]     = "Bidirectional schema conversion and comparison between SQL Server and PostgreSQL.",
            ["home.card.converter.title"]    = "Schema Converter",
            ["home.card.converter.desc"]     = "Convert database schemas bidirectionally between SQL Server and PostgreSQL. Supports tables, views, procedures, functions, indexes, and triggers.",
            ["home.card.comparator.title"]   = "Schema Comparator",
            ["home.card.comparator.desc"]    = "Compare schemas from any combination of sources: live connection or .sql file. Generates a sync script for detected differences.",
            ["home.card.query.title"]        = "Query Converter",
            ["home.card.query.desc"]         = "Convert individual SQL snippets between T-SQL and PostgreSQL dialects.",
            ["home.card.open"]               = "Open",

            // Converter
            ["converter.title"]          = "Schema Converter",
            ["converter.subtitle"]       = "Convert a database schema between SQL Server and PostgreSQL.",
            ["converter.source"]         = "Source",
            ["converter.direction"]      = "Direction",
            ["converter.options"]        = "Options & Output",
            ["converter.targetSchema"]   = "Target Schema",
            ["converter.includeDrops"]   = "Include DROP statements",
            ["converter.ifNotExists"]    = "Use IF NOT EXISTS",
            ["converter.output"]         = "Output",
            ["converter.output.file"]    = "Generate .sql file",
            ["converter.output.execute"] = "Execute on database (dry-run first)",
            ["converter.targetConn"]     = "Target Connection String",
            ["converter.convert"]        = "Convert",
            ["converter.running"]        = "Running…",

            // Comparator
            ["comparator.title"]     = "Schema Comparator",
            ["comparator.subtitle"]  = "Compare two schemas from any combination of sources.",
            ["comparator.sourceA"]   = "Source A",
            ["comparator.sourceB"]   = "Source B",
            ["comparator.compare"]   = "Compare Schemas",
            ["comparator.syncScript"]= "Sync Script",
            ["comparator.download"]  = "Download",

            // Query Converter
            ["query.title"]      = "Query Converter",
            ["query.subtitle"]   = "Convert SQL snippets between T-SQL and PostgreSQL dialects.",
            ["query.direction"]  = "Direction:",
            ["query.ss2pg"]      = "T-SQL → PostgreSQL",
            ["query.pg2ss"]      = "PostgreSQL → T-SQL",
            ["query.inputTsql"]  = "T-SQL Input",
            ["query.inputPg"]    = "PostgreSQL Input",
            ["query.outputPg"]   = "PostgreSQL Output",
            ["query.outputTsql"] = "T-SQL Output",
            ["query.convert"]    = "Convert",
            ["query.copy"]       = "Copy",
            ["query.warnings"]   = "Warnings:",
            ["query.placeholder"]= "Paste your SQL query here...",

            // Type Mapping
            ["typeMapping.title"]      = "Type Mapping Configuration",
            ["typeMapping.subtitle"]   = "Customize how data types are mapped during conversion.",
            ["typeMapping.reset"]      = "Reset to Defaults",
            ["typeMapping.save"]       = "Save Changes",
            ["typeMapping.saved"]      = "Saved!",
            ["typeMapping.addRow"]     = "+ Add Row",
            ["typeMapping.sourceType"] = "Source Type",
            ["typeMapping.targetType"] = "Target Type",
            ["typeMapping.ss2pg"]      = "SQL Server → PostgreSQL",
            ["typeMapping.pg2ss"]      = "PostgreSQL → SQL Server",
            ["typeMapping.msgSaved"]   = "Type mapping saved successfully.",
            ["typeMapping.msgReset"]   = "Type mapping reset to defaults.",

            // Audit
            ["audit.title"]        = "Audit Log",
            ["audit.subtitle"]     = "All operations performed in this session.",
            ["audit.clear"]        = "Clear Log",
            ["audit.empty"]        = "No audit entries yet.",
            ["audit.timestamp"]    = "Timestamp (UTC)",
            ["audit.action"]       = "Action",
            ["audit.source"]       = "Source",
            ["audit.target"]       = "Target",
            ["audit.status"]       = "Status",
            ["audit.details"]      = "Details",
            ["audit.success"]      = "Success",
            ["audit.failed"]       = "Failed",
            ["audit.entries"]      = "entry/entries in this session.",

            // SourceSelector
            ["source.dialect"]      = "Dialect",
            ["source.inputMode"]    = "Input Mode",
            ["source.connection"]   = "Connection",
            ["source.sqlFile"]      = "SQL File",
            ["source.connStr"]      = "Connection String",
            ["source.testLoad"]     = "Test Connection & Load Objects",
            ["source.connected"]    = "Connected successfully.",
            ["source.connFailed"]   = "Connection failed.",
            ["source.pasteSql"]     = "Paste SQL Content",
            ["source.parse"]        = "Parse SQL",
            ["source.placeholder.ss"]  = "Server=.;Database=mydb;Trusted_Connection=True;",
            ["source.placeholder.pg"]  = "Host=localhost;Database=mydb;Username=user;Password=pass;",
            ["source.placeholder.sql"] = "Paste your CREATE TABLE / CREATE VIEW / CREATE PROCEDURE statements here...",

            // Diff Report
            ["diff.inSync"]    = "In Sync",
            ["diff.differences"]= "difference(s)",
            ["diff.syncScript"]= "Generate Sync Script",
            ["diff.download"]  = "Download Report",
            ["diff.onlySource"]= "Only in Source",
            ["diff.onlyTarget"]= "Only in Target",
            ["diff.modified"]  = "Modified",
            ["diff.tables"]    = "Tables",
            ["diff.views"]     = "Views",
            ["diff.procedures"]= "Procedures",
            ["diff.functions"] = "Functions",
            ["diff.triggers"]  = "Triggers",

            // Execution Report
            ["exec.dryRun"]    = "Dry-Run Report",
            ["exec.report"]    = "Execution Report",
            ["exec.confirmExec"]="Confirm Real Execution",
            ["exec.success"]   = "Success",
            ["exec.failed"]    = "Failed",
            ["exec.statement"] = "Statement",
            ["exec.duration"]  = "Duration",

            // General
            ["general.copy"]     = "Copy",
            ["general.download"] = "Download",
            ["general.running"]  = "Running…",
            ["general.error"]    = "Error",
            ["general.warnings"] = "Warnings",
        },

        [PtBr] = new()
        {
            // Navegação
            ["nav.home"]          = "Início",
            ["nav.converter"]     = "Conversor",
            ["nav.comparator"]    = "Comparador",
            ["nav.queryConverter"]= "Conversor de Query",
            ["nav.typeMapping"]   = "Mapeamento de Tipos",
            ["nav.auditLog"]      = "Log de Auditoria",

            // Home
            ["home.title"]        = "DB Schema Tool",
            ["home.subtitle"]     = "Conversão e comparação bidirecional de schemas entre SQL Server e PostgreSQL.",
            ["home.card.converter.title"]    = "Conversor de Schema",
            ["home.card.converter.desc"]     = "Converta schemas bidirecionalmente entre SQL Server e PostgreSQL. Suporta tabelas, views, procedures, functions, indexes e triggers.",
            ["home.card.comparator.title"]   = "Comparador de Schema",
            ["home.card.comparator.desc"]    = "Compare schemas de qualquer combinação de fontes: conexão direta ou arquivo .sql. Gera script de sincronização para as diferenças detectadas.",
            ["home.card.query.title"]        = "Conversor de Query",
            ["home.card.query.desc"]         = "Converta snippets SQL individuais entre T-SQL e PostgreSQL.",
            ["home.card.open"]               = "Abrir",

            // Converter
            ["converter.title"]          = "Conversor de Schema",
            ["converter.subtitle"]       = "Converta um schema de banco de dados entre SQL Server e PostgreSQL.",
            ["converter.source"]         = "Origem",
            ["converter.direction"]      = "Direção",
            ["converter.options"]        = "Opções e Saída",
            ["converter.targetSchema"]   = "Schema Destino",
            ["converter.includeDrops"]   = "Incluir comandos DROP",
            ["converter.ifNotExists"]    = "Usar IF NOT EXISTS",
            ["converter.output"]         = "Saída",
            ["converter.output.file"]    = "Gerar arquivo .sql",
            ["converter.output.execute"] = "Executar no banco (dry-run obrigatório)",
            ["converter.targetConn"]     = "String de conexão do destino",
            ["converter.convert"]        = "Converter",
            ["converter.running"]        = "Processando…",

            // Comparator
            ["comparator.title"]     = "Comparador de Schema",
            ["comparator.subtitle"]  = "Compare dois schemas de qualquer combinação de fontes.",
            ["comparator.sourceA"]   = "Origem A",
            ["comparator.sourceB"]   = "Origem B",
            ["comparator.compare"]   = "Comparar Schemas",
            ["comparator.syncScript"]= "Script de Sincronização",
            ["comparator.download"]  = "Baixar",

            // Query Converter
            ["query.title"]      = "Conversor de Query",
            ["query.subtitle"]   = "Converta snippets SQL entre T-SQL e PostgreSQL.",
            ["query.direction"]  = "Direção:",
            ["query.ss2pg"]      = "T-SQL → PostgreSQL",
            ["query.pg2ss"]      = "PostgreSQL → T-SQL",
            ["query.inputTsql"]  = "Entrada T-SQL",
            ["query.inputPg"]    = "Entrada PostgreSQL",
            ["query.outputPg"]   = "Saída PostgreSQL",
            ["query.outputTsql"] = "Saída T-SQL",
            ["query.convert"]    = "Converter",
            ["query.copy"]       = "Copiar",
            ["query.warnings"]   = "Avisos:",
            ["query.placeholder"]= "Cole sua query SQL aqui...",

            // Type Mapping
            ["typeMapping.title"]      = "Configuração de Mapeamento de Tipos",
            ["typeMapping.subtitle"]   = "Personalize como os tipos de dados são mapeados durante a conversão.",
            ["typeMapping.reset"]      = "Restaurar Padrões",
            ["typeMapping.save"]       = "Salvar Alterações",
            ["typeMapping.saved"]      = "Salvo!",
            ["typeMapping.addRow"]     = "+ Adicionar Linha",
            ["typeMapping.sourceType"] = "Tipo de Origem",
            ["typeMapping.targetType"] = "Tipo de Destino",
            ["typeMapping.ss2pg"]      = "SQL Server → PostgreSQL",
            ["typeMapping.pg2ss"]      = "PostgreSQL → SQL Server",
            ["typeMapping.msgSaved"]   = "Mapeamento salvo com sucesso.",
            ["typeMapping.msgReset"]   = "Mapeamento restaurado para os padrões.",

            // Audit
            ["audit.title"]        = "Log de Auditoria",
            ["audit.subtitle"]     = "Todas as operações realizadas nesta sessão.",
            ["audit.clear"]        = "Limpar Log",
            ["audit.empty"]        = "Nenhuma entrada de auditoria ainda.",
            ["audit.timestamp"]    = "Data/Hora (UTC)",
            ["audit.action"]       = "Ação",
            ["audit.source"]       = "Origem",
            ["audit.target"]       = "Destino",
            ["audit.status"]       = "Status",
            ["audit.details"]      = "Detalhes",
            ["audit.success"]      = "Sucesso",
            ["audit.failed"]       = "Falhou",
            ["audit.entries"]      = "entrada(s) nesta sessão.",

            // SourceSelector
            ["source.dialect"]      = "Dialeto",
            ["source.inputMode"]    = "Modo de Entrada",
            ["source.connection"]   = "Conexão",
            ["source.sqlFile"]      = "Arquivo SQL",
            ["source.connStr"]      = "String de Conexão",
            ["source.testLoad"]     = "Testar Conexão e Carregar Objetos",
            ["source.connected"]    = "Conexão bem-sucedida.",
            ["source.connFailed"]   = "Falha na conexão.",
            ["source.pasteSql"]     = "Cole o Conteúdo SQL",
            ["source.parse"]        = "Processar SQL",
            ["source.placeholder.ss"]  = "Server=.;Database=meubd;Trusted_Connection=True;",
            ["source.placeholder.pg"]  = "Host=localhost;Database=meubd;Username=usuario;Password=senha;",
            ["source.placeholder.sql"] = "Cole aqui os comandos CREATE TABLE / CREATE VIEW / CREATE PROCEDURE...",

            // Diff Report
            ["diff.inSync"]    = "Sincronizado",
            ["diff.differences"]= "diferença(s)",
            ["diff.syncScript"]= "Gerar Script de Sincronização",
            ["diff.download"]  = "Baixar Relatório",
            ["diff.onlySource"]= "Somente na Origem",
            ["diff.onlyTarget"]= "Somente no Destino",
            ["diff.modified"]  = "Modificado",
            ["diff.tables"]    = "Tabelas",
            ["diff.views"]     = "Views",
            ["diff.procedures"]= "Procedures",
            ["diff.functions"] = "Functions",
            ["diff.triggers"]  = "Triggers",

            // Execution Report
            ["exec.dryRun"]    = "Relatório Dry-Run",
            ["exec.report"]    = "Relatório de Execução",
            ["exec.confirmExec"]="Confirmar Execução Real",
            ["exec.success"]   = "Sucesso",
            ["exec.failed"]    = "Falhou",
            ["exec.statement"] = "Instrução",
            ["exec.duration"]  = "Duração",

            // General
            ["general.copy"]     = "Copiar",
            ["general.download"] = "Baixar",
            ["general.running"]  = "Processando…",
            ["general.error"]    = "Erro",
            ["general.warnings"] = "Avisos",
        }
    };

    public static string Get(string lang, string key)
    {
        if (_store.TryGetValue(lang, out var dict) && dict.TryGetValue(key, out var val))
            return val;
        // Fallback to EN-US
        if (_store.TryGetValue(EnUs, out var en) && en.TryGetValue(key, out var enVal))
            return enVal;
        return key;
    }
}
