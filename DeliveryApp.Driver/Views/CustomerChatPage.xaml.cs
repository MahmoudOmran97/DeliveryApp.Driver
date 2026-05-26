namespace DeliveryApp.Driver.Views;

public partial class CustomerChatPage : ContentPage
{
    public CustomerChatPage(ViewModels.CustomerChatViewModel vm)
    {
        InitializeComponent();
        BindingContext = vm;
    }
}
