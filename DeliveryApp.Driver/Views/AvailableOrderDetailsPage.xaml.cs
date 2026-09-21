using DeliveryApp.Driver.ViewModels;

namespace DeliveryApp.Driver.Views;

public partial class AvailableOrderDetailsPage : ContentPage
{
    readonly AvailableOrderDetailsViewModel _vm;

    public AvailableOrderDetailsPage(AvailableOrderDetailsViewModel vm)
    {
        InitializeComponent();
        _vm = vm;
        BindingContext = vm;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        // OrderId بيتحدد من الـ QueryProperty قبل ما الصفحة تظهر
        _ = _vm.LoadCommand.ExecuteAsync(null);
    }
}
