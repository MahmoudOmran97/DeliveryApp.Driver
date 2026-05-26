using DeliveryApp.Driver.ViewModels;

namespace DeliveryApp.Driver.Views;

public partial class HomePage : ContentPage
{
    readonly HomeViewModel _vm;

    public HomePage(HomeViewModel vm)
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

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        _vm.Cleanup();
    }

    private async void OnNotificationsTapped(object sender, TappedEventArgs e)
        => await Shell.Current.GoToAsync(nameof(NotificationsPage));

    private async void OnAvailableOrdersTapped(object sender, TappedEventArgs e)
        => await Shell.Current.GoToAsync("//AvailableOrdersPage");

    private async void OnEarningsTapped(object sender, TappedEventArgs e)
        => await Shell.Current.GoToAsync("//EarningsPage");
}