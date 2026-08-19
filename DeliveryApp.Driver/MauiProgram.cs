// ═══════════════════════════════════════════════════════════════
// DeliveryApp.Driver / MauiProgram.cs
// ═══════════════════════════════════════════════════════════════
using CommunityToolkit.Maui;
using DeliveryApp.Driver.Services;
using DeliveryApp.Driver.ViewModels;
using DeliveryApp.Driver.Views;
using DeliveryApp.Driver.Converters;
using Mapsui.UI.Maui;
using Microsoft.Extensions.Logging;
using Microsoft.Maui.Controls.Hosting;
using SkiaSharp.Views.Maui.Controls.Hosting;
using Microsoft.Maui.LifecycleEvents;

#if ANDROID
using Plugin.Firebase.Core.Platforms.Android;
#elif IOS
using Plugin.Firebase.Core.Platforms.iOS;
#endif

namespace DeliveryApp.Driver;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        // تطبيق اللغة قبل ما الـ UI يبدأ
        LocalizationService.Apply(
            Preferences.Get(LocalizationService.LangKey, LocalizationService.Arabic));

        var builder = MauiApp.CreateBuilder();

        builder
            .UseMauiApp<App>()
            .UseMauiCommunityToolkit()
            .UseSkiaSharp()
            .ConfigureFonts(fonts =>
            {
                fonts.AddFont("Cairo-Regular.ttf", "CairoRegular");
                fonts.AddFont("Cairo-Bold.ttf", "CairoBold");
                fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
            })
            // ✅ لازم يتعمل Initialize لـ Firebase قبل أي استخدام لـ CrossFirebaseCloudMessaging
            // وإلا GetTokenAsync/CheckIfValidAsync بيفشلوا بصمت (اللي كان بيحصل قبل كده).
            .ConfigureLifecycleEvents(events =>
            {
#if ANDROID
                events.AddAndroid(android => android.OnCreate((activity, _) =>
                  CrossFirebase.Initialize(activity, () => Platform.CurrentActivity!)));
#elif IOS
                events.AddiOS(ios => ios.WillFinishLaunching((_, __) =>
                {
                    CrossFirebase.Initialize();
                    return false;
                }));
#endif
            });

        // ── Services ─────────────────────────────────────────────────────────
        builder.Services.AddSingleton<AuthService>();
        builder.Services.AddSingleton<ApiService>();
        builder.Services.AddSingleton<SignalRService>();
        builder.Services.AddSingleton<LocationService>();
        builder.Services.AddSingleton<LocaleStrings>();
        builder.Services.AddSingleton<ChatNotificationService>(); // ✅ FIX #4
        builder.Services.AddSingleton<FcmTokenService>();

        // ── ViewModels ────────────────────────────────────────────────────────
        builder.Services.AddTransient<LoginViewModel>();
        builder.Services.AddSingleton<HomeViewModel>();
        builder.Services.AddTransient<AvailableOrdersViewModel>();
        builder.Services.AddTransient<ActiveDeliveryViewModel>();
        builder.Services.AddTransient<EarningsViewModel>();
        builder.Services.AddTransient<DuesViewModel>();
        builder.Services.AddTransient<NotificationsViewModel>();
        builder.Services.AddTransient<CustomerChatViewModel>();
        builder.Services.AddTransient<SupportChatViewModel>();
        builder.Services.AddTransient<ComplaintsViewModel>();
        builder.Services.AddTransient<AboutViewModel>();
        builder.Services.AddTransient<CallViewModel>();
        // builder.Services.AddTransient<CallAudioService>();
#if ANDROID
        builder.Services.AddSingleton<DeliveryApp.Driver.Services.Call.IAgoraCallService, DeliveryApp.Driver.Platforms.Android.AgoraCallServiceAndroid>();
        builder.Services.AddSingleton<DeliveryApp.Driver.Services.Call.IRingtoneService, DeliveryApp.Driver.Platforms.Android.RingtoneServiceAndroid>();

#elif IOS
     //   builder.Services.AddSingleton<DeliveryApp.Driver.Services.Call.IPlatformAudioIO, DeliveryApp.Driver.Platforms.iOS.IosAudioIO>();
#endif
        // ملحوظة: مفيش تسجيل لـ MacCatalyst/Windows — لو الأبليكيشن اتبني لأي منهم، شاشة
        // المكالمة هترمي خطأ DI. مش هدف أساسي حسب كلامك (Android + iOS)، فسبتها من غير حل دلوقتي.
        builder.Services.AddTransient<SettingsViewModel>();
        builder.Services.AddTransient<ProfileViewModel>();

        // ── Pages ─────────────────────────────────────────────────────────────
        // ✅ FIX (لغة) — كانوا Singleton فكانت نفس نسخة الصفحة القديمة (باللغة القديمة)
        // بترجع تظهر تاني بعد RestartApp بدل ما تتبني من جديد باللغة الجديدة، زي ما
        // بيحصل في تطبيق الكاستمر (Transient) بالظبط.
        builder.Services.AddTransient<AppShell>();
        builder.Services.AddSingleton<SplashPage>();
        builder.Services.AddTransient<LoginPage>();
        builder.Services.AddTransient<HomePage>();
        builder.Services.AddTransient<AvailableOrdersPage>();
        builder.Services.AddTransient<ActiveDeliveryPage>();
        builder.Services.AddTransient<EarningsPage>();
        builder.Services.AddTransient<DuesPage>();
        builder.Services.AddTransient<NotificationsPage>();
        builder.Services.AddTransient<CustomerChatPage>();
        builder.Services.AddTransient<SupportChatPage>();
        builder.Services.AddTransient<ComplaintsPage>();
        builder.Services.AddTransient<AboutPage>();
        builder.Services.AddTransient<CallPage>();
        builder.Services.AddTransient<SettingsPage>();
        builder.Services.AddTransient<ProfilePage>();

#if DEBUG
        builder.Logging.AddDebug();
#endif

        return builder.Build();
    }
}