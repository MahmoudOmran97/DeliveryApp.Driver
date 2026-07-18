using DeliveryApp.Driver.Services;
using DeliveryApp.Driver.Views;

namespace DeliveryApp.Driver;

public partial class App : Application
{
    private readonly LocationService _location;
    private readonly SignalRService _signalR;
    private readonly AuthService _auth;

    public App(SplashPage splash, LocationService location, SignalRService signalR, AuthService auth,
        FcmTokenService fcmToken)
    {
        InitializeComponent();
        _location = location;
        _signalR = signalR;
        _auth = auth;
        MainPage = splash;

        fcmToken.ListenForTokenRefresh();
        fcmToken.ListenForMessages();

        // Register FCM token only when user is already logged in (needs JWT for API)
        _ = Task.Run(async () =>
        {
            await Task.Delay(1500);
            if (_auth.IsLoggedIn)
            {
                await fcmToken.RegisterAsync();
                await _signalR.ConnectAsync(_auth.GetToken());
            }

            // ✅ لو التطبيق اتفتح لسه (cold start) بسبب دوس على زرار "قبول" في نوتيفيكيشن
            // مكالمة واردة، انقل الدرايفر مباشرة لصفحة المكالمة مع قبول تلقائي.
            var pendingCall = Services.PendingCallNavigation.TakePending();
            if (pendingCall != null)
            {
                MainThread.BeginInvokeOnMainThread(async () =>
                {
                    await Shell.Current.GoToAsync(
                        $"CallPage?orderId={pendingCall.Value.orderId}&otherPartyName={Uri.EscapeDataString(pendingCall.Value.callerName)}&isIncoming=true&autoAccept=true");
                });
            }
        });

        // ✅ CALL FIX — لما مكالمة واردة توصل والأبليكيشن فاتح (foreground/background بس مش مقفول
        // خالص)، افتح شاشة المكالمة تلقائي زي أي تطبيق اتصال. لو الأبليكيشن مقفول تماماً، ده
        // بيتوصل عن طريق الـ FCM data push بدل SignalR (Platforms/Android/IncomingCallNotificationHelper).
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