using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DeliveryApp.Driver.Models;
using DeliveryApp.Driver.Services;
using Microsoft.Maui.ApplicationModel;

namespace DeliveryApp.Driver.ViewModels;

public partial class AboutViewModel : BaseViewModel
{
    private readonly ApiService _api;

    [ObservableProperty] private string _websiteUrl = "https://Taly-app.com";
    [ObservableProperty] private string _facebookUrl = "https://facebook.com/Taly";
    [ObservableProperty] private string _instagramUrl = "https://instagram.com/Taly";
    [ObservableProperty] private string _xUrl = "https://x.com/Taly";
    [ObservableProperty] private string _tikTokUrl = "https://www.tiktok.com/@Taly";

    public AboutViewModel(ApiService api)
    {
        _api = api;
        _ = LoadSiteLinksAsync();
    }

    private async Task LoadSiteLinksAsync()
    {
        try
        {
            var links = await _api.GetSiteLinksAsync();
            if (links == null) return;
            foreach (var link in links)
            {
                if (string.IsNullOrWhiteSpace(link.Url)) continue;
                switch (link.Key.Trim().ToLowerInvariant())
                {
                    case "website":
                    case "site": _websiteUrl= link.Url; break;
                    case "facebook": _facebookUrl = link.Url; break;
                    case "instagram": _instagramUrl = link.Url; break;
                    case "x":
                    case "twitter": _xUrl = link.Url; break;
                    case "tiktok": _tikTokUrl = link.Url; break;
                }
            }
        }
        catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"Failed to load site links: {ex.Message}"); }
    }

    [RelayCommand]
    private async Task OpenSocialLink(string url)
    {
        if (!string.IsNullOrWhiteSpace(url))
            try { await Launcher.Default.OpenAsync(url); }
            catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"Failed to open link: {ex.Message}"); }
    }

    [RelayCommand]
    private async Task OpenLegal(string type)
    {
        var website = string.IsNullOrWhiteSpace(_websiteUrl) ? "https://Taly-app.com" : _websiteUrl.TrimEnd('/');
        await OpenSocialLink(type switch
        {
            "privacy" => $"{website}/Home/Privacy",
            "terms" => $"{website}/Home/Terms",
            _ => website
        });
    }
}
