using System.Globalization;
using System.Resources;

namespace DeliveryApp.Driver.Services;

public static class LocalizationService
{
    public const string LangKey = "app_language";
    public const string Arabic = "ar";
    public const string English = "en";

    private static readonly ResourceManager _rm =
        new("DeliveryApp.Driver.Resources.Strings.AppResources",
            typeof(LocalizationService).Assembly);

    public static CultureInfo Current { get; private set; } = GetSavedCulture();

    public static bool IsRtl => Current.TextInfo.IsRightToLeft;

    public static FlowDirection Flow =>
        IsRtl ? FlowDirection.RightToLeft : FlowDirection.LeftToRight;

    public static string Get(string key)
    {
        try { return _rm.GetString(key, Current) ?? key; }
        catch { return key; }
    }

    public static void SetLanguage(string langCode)
    {
        Preferences.Set(LangKey, langCode);
        Apply(langCode);
    }

    public static void Apply(string langCode)
    {
        Current = new CultureInfo(langCode);
        CultureInfo.DefaultThreadCurrentCulture = Current;
        CultureInfo.DefaultThreadCurrentUICulture = Current;
        Thread.CurrentThread.CurrentCulture = Current;
        Thread.CurrentThread.CurrentUICulture = Current;
    }

    public static string ToggleLanguage()
    {
        var next = Current.TwoLetterISOLanguageName == Arabic ? English : Arabic;
        SetLanguage(next);
        return next;
    }

    private static CultureInfo GetSavedCulture()
    {
        var saved = Preferences.Get(LangKey, English);
        return new CultureInfo(saved);
    }
}
