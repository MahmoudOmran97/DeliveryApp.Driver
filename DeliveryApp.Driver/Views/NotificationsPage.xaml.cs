using DeliveryApp.Driver.ViewModels;

namespace DeliveryApp.Driver.Views;

public partial class NotificationsPage : ContentPage
{
    readonly NotificationsViewModel _vm;

    public NotificationsPage(NotificationsViewModel vm)
    {
        InitializeComponent();
        BindingContext = vm;
        _vm = vm;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        _ = _vm.LoadCommand.ExecuteAsync(null);
    }
}
