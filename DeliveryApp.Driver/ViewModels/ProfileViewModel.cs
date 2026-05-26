using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DeliveryApp.Driver.Models;
using DeliveryApp.Driver.Services;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace DeliveryApp.Driver.ViewModels;

public partial class ProfileViewModel : BaseViewModel
{
    readonly ApiService _api;
    readonly AuthService _auth;
    readonly SignalRService _hub;
    readonly LocationService _location;

    [ObservableProperty] DriverProfile? _profile;
    [ObservableProperty] string _userName = string.Empty;
    [ObservableProperty] string _userEmail = string.Empty;

    public ProfileViewModel(ApiService api, AuthService auth, SignalRService hub, LocationService location)
    {
        _api = api;
        _auth = auth;
        _hub = hub;
        _location = location;
    }

    [RelayCommand]
    public async Task LoadAsync()
    {
        IsBusy = true;
        try
        {
            Profile = await _api.GetMyProfileAsync();
            UserName = _auth.GetUserName();
            UserEmail = _auth.GetEmail();
        }
        finally { IsBusy = false; }
    }

    [RelayCommand]
    async Task LogoutAsync()
    {
        var confirm = await ConfirmAsync("Are you sure you want to logout?", "Logout");
        if (!confirm) return;

        _location.StopTracking();
        await _hub.DisconnectAsync();
        _auth.Logout();

        var loginPage = IPlatformApplication.Current!.Services.GetService<Views.LoginPage>()!;
        Application.Current!.MainPage = new NavigationPage(loginPage);
    }
}