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

        public SplashPage(AuthService auth)
        {
            InitializeComponent();
            _auth = auth;
        }

        protected override async void OnAppearing()
        {
            base.OnAppearing();
            await Task.Delay(2000);

            if (_auth.IsLoggedIn)
            {
                // ✅ بنيها هنا بعد ما Resources اتحملت
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
