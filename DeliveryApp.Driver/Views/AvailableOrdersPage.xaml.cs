using DeliveryApp.Driver.Models;
using DeliveryApp.Driver.ViewModels;

namespace DeliveryApp.Driver.Views;

public partial class AvailableOrdersPage : ContentPage
{
    readonly AvailableOrdersViewModel _vm;

    public AvailableOrdersPage(AvailableOrdersViewModel vm)
    {
        InitializeComponent();
        BindingContext = vm;
        _vm = vm;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        _ = _vm.LoadCommand.ExecuteAsync(null);
        _vm.StartAutoRefresh();
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        _vm.StopAutoRefresh();
    }

    private void OnAcceptClicked(object sender, EventArgs e)
    {
        if (sender is Button btn && btn.CommandParameter is AvailableOrder order)
            _ = _vm.AcceptOrderCommand.ExecuteAsync(order);
    }
}