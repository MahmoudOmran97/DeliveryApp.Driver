using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DeliveryApp.Driver.Services;

namespace DeliveryApp.Driver.ViewModels;

public partial class LoginViewModel : BaseViewModel
{
    readonly ApiService _api;
    readonly AuthService _auth;
    readonly SignalRService _signalR;

    [ObservableProperty] string _email = string.Empty;
    [ObservableProperty] string _password = string.Empty;

    public LoginViewModel(ApiService api, AuthService auth, SignalRService signalR)
    {
        _api = api;
        _auth = auth;
        _signalR = signalR;
    }

    [RelayCommand]
    async Task LoginAsync()
    {
        if (string.IsNullOrWhiteSpace(Email) || string.IsNullOrWhiteSpace(Password))
        { await AlertAsync("Please fill in all fields"); return; }

        IsBusy = true;
        try
        {
            var r = await _api.LoginAsync(Email, Password);
            if (r != null)
            {
                if (r.Role != "Driver")
                {
                    await AlertAsync("This app is for delivery drivers only.");
                    return;
                }
                _auth.SaveUser(r.Token, r.Id, r.FullName, r.Email);
                await _signalR.ConnectAsync(r.Token);

                var profile = await _api.GetMyProfileAsync();
                if (profile is null)
                {
                    await AlertAsync("Your driver profile is not set up yet. Please contact the administrator.");
                    _auth.Logout();
                    await _signalR.DisconnectAsync();
                    return;
                }

                var shell = IPlatformApplication.Current!.Services.GetService<AppShell>()!;
                Application.Current!.MainPage = shell;
            }
            else await AlertAsync("Invalid email or password");
        }
        finally { IsBusy = false; }
    }
}
