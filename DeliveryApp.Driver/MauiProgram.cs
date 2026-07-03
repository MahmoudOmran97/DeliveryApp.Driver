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
            });

        // ── Services ─────────────────────────────────────────────────────────
        builder.Services.AddSingleton<AuthService>();
        builder.Services.AddSingleton<ApiService>();
        builder.Services.AddSingleton<SignalRService>();
        builder.Services.AddSingleton<LocationService>();
        builder.Services.AddSingleton<LocaleStrings>();
        builder.Services.AddSingleton<ChatNotificationService>(); // ✅ FIX #4

        // ── ViewModels ────────────────────────────────────────────────────────
        builder.Services.AddTransient<LoginViewModel>();
        builder.Services.AddSingleton<HomeViewModel>();
        builder.Services.AddTransient<AvailableOrdersViewModel>();
        builder.Services.AddTransient<ActiveDeliveryViewModel>();
        builder.Services.AddTransient<EarningsViewModel>();
        builder.Services.AddTransient<NotificationsViewModel>();
        builder.Services.AddTransient<CustomerChatViewModel>();
        builder.Services.AddTransient<SupportChatViewModel>();
        builder.Services.AddTransient<CallViewModel>();
        builder.Services.AddTransient<CallAudioService>();
#if ANDROID
        builder.Services.AddSingleton<DeliveryApp.Driver.Services.Call.IPlatformAudioIO, DeliveryApp.Driver.Platforms.Android.AndroidAudioIO>();
#elif IOS
        builder.Services.AddSingleton<DeliveryApp.Driver.Services.Call.IPlatformAudioIO, DeliveryApp.Driver.Platforms.iOS.IosAudioIO>();
#endif
        // ملحوظة: مفيش تسجيل لـ MacCatalyst/Windows — لو الأبليكيشن اتبني لأي منهم، شاشة
        // المكالمة هترمي خطأ DI. مش هدف أساسي حسب كلامك (Android + iOS)، فسبتها من غير حل دلوقتي.
        builder.Services.AddTransient<SettingsViewModel>();
        builder.Services.AddTransient<ProfileViewModel>();

        // ── Pages ─────────────────────────────────────────────────────────────
        builder.Services.AddSingleton<AppShell>();
        builder.Services.AddSingleton<SplashPage>();
        builder.Services.AddTransient<LoginPage>();
        builder.Services.AddSingleton<HomePage>();
        builder.Services.AddTransient<AvailableOrdersPage>();
        builder.Services.AddTransient<ActiveDeliveryPage>();
        builder.Services.AddTransient<EarningsPage>();
        builder.Services.AddTransient<NotificationsPage>();
        builder.Services.AddTransient<CustomerChatPage>();
        builder.Services.AddTransient<SupportChatPage>();
        builder.Services.AddTransient<CallPage>();
        builder.Services.AddTransient<SettingsPage>();
        builder.Services.AddTransient<ProfilePage>();

#if DEBUG
        builder.Logging.AddDebug();
#endif

        return builder.Build();
    }
}