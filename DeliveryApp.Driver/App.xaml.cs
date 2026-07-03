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

        // ✅ CALL FIX — لما مكالمة واردة توصل والأبليكيشن فاتح (foreground/background بس مش مقفول
        // خالص)، افتح شاشة المكالمة تلقائي زي أي تطبيق اتصال. لو الأبليكيشن مقفول تماماً، ده
        // بيتوصل عن طريق الـ FCM data push بدل SignalR (شوف Platforms/Android للـ full-screen notification).
        _signalR.IncomingVoiceCall += (orderId, callerId) =>
        {
            MainThread.BeginInvokeOnMainThread(async () =>
            {
                await Shell.Current.GoToAsync(
                    $"CallPage?orderId={orderId}&otherPartyName={Uri.EscapeDataString("العميل")}&isIncoming=true");
            });
        };
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