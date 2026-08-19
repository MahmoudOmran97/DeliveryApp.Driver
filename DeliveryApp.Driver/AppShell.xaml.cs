using DeliveryApp.Driver.Services;
using DeliveryApp.Driver.Views;

namespace DeliveryApp.Driver;

public partial class AppShell : Shell
{
    public AppShell()
    {
        InitializeComponent();
        // ✅ FIX (لغة) — عشان اتجاه الشاشة (RTL/LTR) يتحدث كمان لما نبني Shell جديد
        // بعد تغيير اللغة، زي ما بيحصل بالظبط في تطبيق الكاستمر.
        FlowDirection = LocalizationService.Flow;
        Shell.SetTabBarIsVisible(this, false);
        Navigated += OnShellNavigated;

        // ── Pushed pages (need back button) ──────────────────────
        Routing.RegisterRoute(nameof(ActiveDeliveryPage), typeof(ActiveDeliveryPage));
        Routing.RegisterRoute(nameof(CustomerChatPage), typeof(CustomerChatPage));
        Routing.RegisterRoute(nameof(SupportChatPage), typeof(SupportChatPage));
        Routing.RegisterRoute(nameof(ComplaintsPage), typeof(ComplaintsPage));
        Routing.RegisterRoute(nameof(AboutPage), typeof(AboutPage));
        Routing.RegisterRoute(nameof(CallPage), typeof(CallPage));

        // EarningsPage is now a pushed page (not a tab anymore)
        Routing.RegisterRoute(nameof(EarningsPage), typeof(EarningsPage));
        Routing.RegisterRoute(nameof(DuesPage), typeof(DuesPage));

        // ── Removed from routes (now TabBar items) ───────────────
        // NotificationsPage → Tab 3
        // SettingsPage       → Tab 5
    }

    void OnShellNavigated(object? sender, ShellNavigatedEventArgs e)
    {
        Shell.SetTabBarIsVisible(this, false);
        if (CurrentPage is Page page)
            Shell.SetTabBarIsVisible(page, false);
    }
}