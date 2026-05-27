using DeliveryApp.Driver.Services;
using DeliveryApp.Driver.Views;

namespace DeliveryApp.Driver;

public partial class App : Application
{
    private readonly LocationService _location;
    private readonly SignalRService _signalR;
    private readonly AuthService _auth;

    public App(SplashPage splash, LocationService location, SignalRService signalR, AuthService auth)
    {
        InitializeComponent();
        _location = location;
        _signalR = signalR;
        _auth = auth;
        MainPage = splash;
    }

    // ✅ FIX 2 & 3 — لما التطبيق يرجع من الـ background
    protected override void OnResume()
    {
        base.OnResume();
        System.Diagnostics.Debug.WriteLine("[App] OnResume");

        // أعد تشغيل GPS tracking لو كان شغال
        _location.OnAppResumed();

        // أعد الاتصال بـ SignalR لو كان متصل
        if (_auth.IsLoggedIn)
        {
            _ = Task.Run(async () =>
            {
                await Task.Delay(500); // استنى الـ UI يرجع
                await _signalR.ConnectAsync(_auth.GetToken());
            });
        }
    }

    // ✅ FIX 2 — لما التطبيق يروح الـ background
    protected override void OnSleep()
    {
        base.OnSleep();
        System.Diagnostics.Debug.WriteLine("[App] OnSleep");
        _location.OnAppSleeping();
    }
}