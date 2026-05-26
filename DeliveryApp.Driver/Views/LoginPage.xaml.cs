using DeliveryApp.Driver.ViewModels;

namespace DeliveryApp.Driver.Views;

public partial class LoginPage : ContentPage
{
    public LoginPage(LoginViewModel vm)
    {
        InitializeComponent();
        BindingContext = vm;
    }
}