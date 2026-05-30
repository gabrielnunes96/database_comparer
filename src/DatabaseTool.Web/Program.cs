using DatabaseTool.Core.Conversion;
using DatabaseTool.Core.Diff;
using DatabaseTool.Core.Execution;
using DatabaseTool.Core.Interfaces;
using DatabaseTool.PostgreSQL;
using DatabaseTool.SqlServer;
using DatabaseTool.Web.Components;
using DatabaseTool.Web.Services;
using DatabaseTool.Web.Services.Localization;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

// Schema Extractors
builder.Services.AddKeyedScoped<ISchemaExtractor, SqlServerSchemaExtractor>("sqlserver");
builder.Services.AddKeyedScoped<ISchemaExtractor, PostgreSqlSchemaExtractor>("postgresql");

// File Parsers
builder.Services.AddKeyedScoped<ISqlFileParser, SqlServerFileParser>("sqlserver");
builder.Services.AddKeyedScoped<ISqlFileParser, PostgreSqlFileParser>("postgresql");

// Execution Engines
builder.Services.AddKeyedScoped<IExecutionEngine, SqlServerExecutionEngine>("sqlserver");
builder.Services.AddKeyedScoped<IExecutionEngine, PostgreSqlExecutionEngine>("postgresql");

// Converters
builder.Services.AddScoped<SqlServerToPostgreSqlConverter>();
builder.Services.AddScoped<PostgreSqlToSqlServerConverter>();
builder.Services.AddScoped<QueryConverter>();

// Diff
builder.Services.AddScoped<ISchemaDiffer, SchemaDiffer>();
builder.Services.AddScoped<SyncScriptGenerator>();

// File generation
builder.Services.AddScoped<FileGenerator>();

// Application services
builder.Services.AddScoped<SchemaService>();
builder.Services.AddScoped<AuditService>();
builder.Services.AddSingleton<TypeMappingService>();
builder.Services.AddScoped<AppLanguageService>();

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseAntiforgery();

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();
