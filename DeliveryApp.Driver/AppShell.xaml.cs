using DeliveryApp.Driver.Views;

namespace DeliveryApp.Driver;

public partial class AppShell : Shell
{
    public AppShell()
    {
        InitializeComponent();

        Routing.RegisterRoute(nameof(ActiveDeliveryPage), typeof(ActiveDeliveryPage));
        Routing.RegisterRoute(nameof(NotificationsPage), typeof(NotificationsPage));
       //Routing.RegisterRoute(nameof(RegisterPage), typeof(RegisterPage));
    }
}
