using DeliveryApp.Driver.Services;
using DeliveryApp.Driver.Views;

namespace DeliveryApp.Driver;

public partial class App : Application
{
    public App(AuthService auth, AppShell shell, LoginPage loginPage)
    {
        InitializeComponent();

        if (auth.IsLoggedIn)
            MainPage = shell;
        else
            MainPage = new NavigationPage(loginPage);
    }
}
