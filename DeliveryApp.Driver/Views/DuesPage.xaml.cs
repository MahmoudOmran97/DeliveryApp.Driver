using DeliveryApp.Driver.ViewModels;

namespace DeliveryApp.Driver.Views;

public partial class DuesPage : ContentPage
{
    readonly DuesViewModel _vm;

    public DuesPage(DuesViewModel vm)
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
