using DeliveryApp.Driver.Services;
using DeliveryApp.Driver.Views;
using Microsoft.Extensions.DependencyInjection;

namespace DeliveryApp.Driver;

public partial class App : Application
{
    private readonly LocationService _location;
    private readonly SignalRService _signalR;
    private readonly AuthService _auth;
    private int? _navigatingIncomingCallOrderId;
    private bool _loggedOutDueToDeactivation;

    /// <summary>true لما التطبيق ظاهر قدام المستخدم — عشان نقرر نفتح CallPage ولا شاشة الرنين الخارجية.</summary>
    public static bool IsInForeground { get; private set; }

    public App(SplashPage splash, LocationService location, SignalRService signalR, AuthService auth,
        FcmTokenService fcmToken, ApiService api, IServiceProvider services)
    {
        InitializeComponent();
        _location = location;
        _signalR = signalR;
        _auth = auth;
        MainPage = splash;

        // ✅ لو الأدمن أوقف حساب الطيار — نعمل logout فوري ونرجعه لشاشة الدخول،
        // بنفس المنطق المستخدم في تطبيق الكاستمر (401/ACCOUNT_DEACTIVATED، SignalR،
        // أو FCM push، أيهما وصل الأول).
        async Task HandleAccountDeactivatedAsync()
        {
            if (_loggedOutDueToDeactivation) return;
            _loggedOutDueToDeactivation = true;

            try { await signalR.DisconnectAsync(); } catch { }
            _location.OnAppSleeping();
            auth.Logout();

            MainThread.BeginInvokeOnMainThread(async () =>
            {
                var loginPage = services.GetRequiredService<LoginPage>();
                MainPage = new NavigationPage(loginPage);
                await MainPage.DisplayAlert(
                    "الحساب موقوف",
                    "تم إيقاف حسابك من قبل الإدارة. تواصل مع الدعم لمزيد من التفاصيل.",
                    "حسنًا");
            });
        }

        signalR.AccountDeactivated += () => _ = HandleAccountDeactivatedAsync();
        api.AccountDeactivated += () => _ = HandleAccountDeactivatedAsync();
        fcmToken.AccountDeactivated += () => _ = HandleAccountDeactivatedAsync();

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

            TryNavigatePendingCall();
        });

        // مكالمة واردة:
        // - التطبيق ظاهر → CallPage جوه الأبليكيشن
        // - التطبيق في الخلفية/مقفول → IncomingCallActivity فوق الشاشة (زي واتساب)
        _signalR.IncomingVoiceCall += (orderId, callerId) =>
        {
            MainThread.BeginInvokeOnMainThread(async () =>
            {
                if (_navigatingIncomingCallOrderId == orderId) return;
                _navigatingIncomingCallOrderId = orderId;

#if ANDROID
                if (!IsInForeground)
                {
                    try
                    {
                        Platforms.Android.IncomingCallNotificationHelper.Show(
                            Android.App.Application.Context, orderId, "العميل");
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"[Call] Show incoming UI failed: {ex.Message}");
                        _navigatingIncomingCallOrderId = null;
                    }
                    return;
                }
#endif
                try
                {
                    await Shell.Current.GoToAsync(
                        $"CallPage?orderId={orderId}&otherPartyName={Uri.EscapeDataString("العميل")}&isIncoming=true");
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[Call] Navigate failed: {ex.Message}");
                    _navigatingIncomingCallOrderId = null;
                }
            });
        };
    }

    protected override void OnResume()
    {
        base.OnResume();
        IsInForeground = true;
        System.Diagnostics.Debug.WriteLine("[App] OnResume");

        _location.OnAppResumed();
        TryNavigatePendingCall();

        if (_auth.IsLoggedIn)
        {
            _ = Task.Run(async () =>
            {
                await Task.Delay(500);
                await _signalR.ConnectAsync(_auth.GetToken());
            });
        }
    }

    protected override void OnSleep()
    {
        base.OnSleep();
        IsInForeground = false;
        System.Diagnostics.Debug.WriteLine("[App] OnSleep");
        _location.OnAppSleeping();
    }

    void TryNavigatePendingCall()
    {
        var pendingCall = PendingCallNavigation.TakePending();
        if (pendingCall == null) return;

        var (orderId, callerName, autoAccept) = pendingCall.Value;
        var autoAcceptFlag = autoAccept ? "true" : "false";

        MainThread.BeginInvokeOnMainThread(async () =>
        {
            try
            {
                await Shell.Current.GoToAsync(
                    $"CallPage?orderId={orderId}&otherPartyName={Uri.EscapeDataString(callerName)}&isIncoming=true&autoAccept={autoAcceptFlag}");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[Call] Pending navigate failed: {ex.Message}");
            }
        });
    }
}
