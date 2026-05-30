using DatabaseTool.Core.Conversion;
using System.Text.Json;

namespace DatabaseTool.Web.Services;

public class TypeMappingService
{
    private static readonly string ConfigPath =
        Path.Combine(AppContext.BaseDirectory, "typemapping.json");

    private TypeMappingConfig _active;

    public TypeMappingService()
    {
        _active = Load();
    }

    public TypeMappingConfig Active => _active;

    public void Update(TypeMappingConfig config)
    {
        _active = config;
        Save(config);
    }

    public void Reset()
    {
        _active = TypeMappingConfig.Default;
        Save(_active);
    }

    private static TypeMappingConfig Load()
    {
        if (!File.Exists(ConfigPath)) return TypeMappingConfig.Default;
        try
        {
            var json = File.ReadAllText(ConfigPath);
            var dto = JsonSerializer.Deserialize<TypeMappingDto>(json);
            if (dto is null) return TypeMappingConfig.Default;

            var config = new TypeMappingConfig();
            foreach (var kv in dto.SqlServerToPostgreSQL)
                config.SqlServerToPostgreSQL[kv.Key] = kv.Value;
            foreach (var kv in dto.PostgreSQLToSqlServer)
                config.PostgreSQLToSqlServer[kv.Key] = kv.Value;
            return config;
        }
        catch
        {
            return TypeMappingConfig.Default;
        }
    }

    private static void Save(TypeMappingConfig config)
    {
        try
        {
            var dto = new TypeMappingDto
            {
                SqlServerToPostgreSQL = new Dictionary<string, string>(config.SqlServerToPostgreSQL),
                PostgreSQLToSqlServer = new Dictionary<string, string>(config.PostgreSQLToSqlServer)
            };
            var json = JsonSerializer.Serialize(dto, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(ConfigPath, json);
        }
        catch { /* non-critical */ }
    }

    private class TypeMappingDto
    {
        public Dictionary<string, string> SqlServerToPostgreSQL { get; set; } = new();
        public Dictionary<string, string> PostgreSQLToSqlServer { get; set; } = new();
    }
}
