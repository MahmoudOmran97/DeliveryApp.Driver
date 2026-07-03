using DeliveryApp.Driver.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DeliveryApp.Driver.Views
{
    public partial class SplashPage : ContentPage
    {
        readonly AuthService _auth;
        readonly SignalRService _signalR;
        readonly ApiService _api;

        public SplashPage(AuthService auth, SignalRService signalR, ApiService api)
        {
            InitializeComponent();
            _auth = auth;
            _signalR = signalR;
            _api = api;
        }

        protected override async void OnAppearing()
        {
            base.OnAppearing();
            await Task.Delay(2000);

            if (_auth.IsLoggedIn)
            {
                var token = _auth.GetToken();
                await _signalR.ConnectAsync(token);

                var profile = await _api.GetMyProfileAsync();
                if (profile is null)
                {
                    _auth.Logout();
                    await _signalR.DisconnectAsync();
                    var loginPage = IPlatformApplication.Current!.Services.GetService<LoginPage>()!;
                    Application.Current!.MainPage = new NavigationPage(loginPage);
                    return;
                }

                var shell = IPlatformApplication.Current!.Services.GetService<AppShell>()!;
                Application.Current!.MainPage = shell;
            }
            else
            {
                var loginPage = IPlatformApplication.Current!.Services.GetService<LoginPage>()!;
                Application.Current!.MainPage = new NavigationPage(loginPage);
            }
        }
    }
}
