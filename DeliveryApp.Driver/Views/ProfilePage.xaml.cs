using DeliveryApp.Driver.ViewModels;

namespace DeliveryApp.Driver.Views;

public partial class ProfilePage : ContentPage
{
    readonly ProfileViewModel _vm;

    public ProfilePage(ProfileViewModel vm)
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
