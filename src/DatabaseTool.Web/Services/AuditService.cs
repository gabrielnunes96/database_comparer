using DatabaseTool.Core.Models;

namespace DatabaseTool.Web.Services;

public class AuditEntry
{
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    public string Action { get; set; } = string.Empty;
    public string SourceDescription { get; set; } = string.Empty;
    public string TargetDescription { get; set; } = string.Empty;
    public string? Details { get; set; }
    public bool Success { get; set; }
}

public class AuditService
{
    private readonly List<AuditEntry> _entries = new();

    public IReadOnlyList<AuditEntry> Entries => _entries.AsReadOnly();

    public IReadOnlyList<AuditEntry> GetEntries() => _entries.AsReadOnly();

    public void Clear() => _entries.Clear();

    public void Log(string action, string source, string target, bool success, string? details = null)
    {
        _entries.Insert(0, new AuditEntry
        {
            Timestamp = DateTime.UtcNow,
            Action = action,
            SourceDescription = source,
            TargetDescription = target,
            Success = success,
            Details = details
        });

        if (_entries.Count > 500)
            _entries.RemoveRange(500, _entries.Count - 500);
    }
}
