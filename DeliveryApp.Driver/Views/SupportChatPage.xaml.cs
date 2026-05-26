using DeliveryApp.Driver.ViewModels;

namespace DeliveryApp.Driver.Views;

public partial class SupportChatPage : ContentPage
{
    public SupportChatPage(SupportChatViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }
}
