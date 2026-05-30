namespace DatabaseTool.Web.Services.Localization;

/// <summary>
/// Per-circuit (scoped) service that holds the user's chosen language and exposes
/// a strongly-typed indexer for translation lookup.
/// </summary>
public class AppLanguageService
{
    private string _lang = Translations.EnUs;

    public string CurrentLanguage => _lang;

    public string FlagEmoji => _lang == Translations.PtBr ? "🇧🇷" : "🇺🇸";

    public string DisplayName => _lang == Translations.PtBr ? "PT-BR" : "EN-US";

    public string this[string key] => Translations.Get(_lang, key);

    /// <summary>Raised when the language changes so components can call StateHasChanged.</summary>
    public event Action? OnLanguageChanged;

    public void SetLanguage(string lang)
    {
        if (!Translations.SupportedLanguages.Contains(lang)) return;
        if (_lang == lang) return;
        _lang = lang;
        OnLanguageChanged?.Invoke();
    }

    public void Toggle() =>
        SetLanguage(_lang == Translations.EnUs ? Translations.PtBr : Translations.EnUs);
}
