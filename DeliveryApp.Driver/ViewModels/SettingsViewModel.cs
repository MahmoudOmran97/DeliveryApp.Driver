using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DeliveryApp.Driver.Services;

namespace DeliveryApp.Driver.ViewModels;

public partial class SettingsViewModel : BaseViewModel
{
    // ── Language button colors ────────────────────────────────────
    private string _currentLang => LocalizationService.Current.TwoLetterISOLanguageName;

    public Color ArabicBtnBg => _currentLang == "ar" ? Color.FromArgb("#FF5722") : Color.FromArgb("#F5F5F5");
    public Color ArabicBtnBorder => _currentLang == "ar" ? Color.FromArgb("#FF5722") : Color.FromArgb("#E0E0E0");
    public Color ArabicBtnText => _currentLang == "ar" ? Colors.White : Color.FromArgb("#212121");
    public Color ArabicSubText => _currentLang == "ar" ? Color.FromArgb("#FFCCBC") : Color.FromArgb("#757575");

    public Color EnglishBtnBg => _currentLang == "en" ? Color.FromArgb("#FF5722") : Color.FromArgb("#F5F5F5");
    public Color EnglishBtnBorder => _currentLang == "en" ? Color.FromArgb("#FF5722") : Color.FromArgb("#E0E0E0");
    public Color EnglishBtnText => _currentLang == "en" ? Colors.White : Color.FromArgb("#212121");
    public Color EnglishSubText => _currentLang == "en" ? Color.FromArgb("#FFCCBC") : Color.FromArgb("#757575");

    [RelayCommand]
    async Task SetLanguage(string lang)
    {
        if (lang == _currentLang) return;

        LocalizationService.SetLanguage(lang);
        RefreshButtons();

        await Task.Delay(150);
        RestartApp();
    }

    private void RefreshButtons()
    {
        OnPropertyChanged(nameof(ArabicBtnBg));
        OnPropertyChanged(nameof(ArabicBtnBorder));
        OnPropertyChanged(nameof(ArabicBtnText));
        OnPropertyChanged(nameof(ArabicSubText));
        OnPropertyChanged(nameof(EnglishBtnBg));
        OnPropertyChanged(nameof(EnglishBtnBorder));
        OnPropertyChanged(nameof(EnglishBtnText));
        OnPropertyChanged(nameof(EnglishSubText));
    }

    private void RestartApp()
    {
        LocalizationService.Apply(_currentLang);
        Application.Current!.MainPage = new AppShell();
    }

    [RelayCommand]
    async Task OpenChat()
    {
        await Shell.Current.GoToAsync("SupportChatPage");
    }
}
