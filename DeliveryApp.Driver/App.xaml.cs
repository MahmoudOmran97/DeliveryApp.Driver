using DeliveryApp.Driver.Services;
using DeliveryApp.Driver.Views;

namespace DeliveryApp.Driver;

public partial class App : Application
{
    public App(SplashPage splash)
    {
        InitializeComponent();
        MainPage = splash; // ✅ من DI
    }
}
