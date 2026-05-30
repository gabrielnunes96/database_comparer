using DatabaseTool.Web.Services.Localization;
using Microsoft.AspNetCore.Components;

namespace DatabaseTool.Web.Components.Shared;

/// <summary>
/// Base class for pages/components that need to re-render when the language changes.
/// Inject L (AppLanguageService) and use L["key"] for translated strings.
/// </summary>
public abstract class LocalizedComponentBase : ComponentBase, IDisposable
{
    [Inject] protected AppLanguageService L { get; set; } = null!;

    protected override void OnInitialized()
    {
        L.OnLanguageChanged += HandleLanguageChanged;
    }

    private void HandleLanguageChanged()
    {
        InvokeAsync(StateHasChanged);
    }

    public void Dispose()
    {
        L.OnLanguageChanged -= HandleLanguageChanged;
    }
}
