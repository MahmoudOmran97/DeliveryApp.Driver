using DeliveryApp.Driver.ViewModels;

namespace DeliveryApp.Driver.Views;

public partial class AboutPage : ContentPage
{
    public AboutPage(AboutViewModel vm)
    {
        InitializeComponent();
        BindingContext = vm;
    }
}
