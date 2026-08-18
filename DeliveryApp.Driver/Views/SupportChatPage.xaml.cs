using DeliveryApp.Driver.ViewModels;
namespace DeliveryApp.Driver.Views;
public partial class SupportChatPage : ContentPage
{
    readonly SupportChatViewModel _vm;
    public SupportChatPage(SupportChatViewModel vm){InitializeComponent();_vm=vm;BindingContext=vm;_vm.Messages.CollectionChanged += (_,__) => MainThread.BeginInvokeOnMainThread(()=>ChatList.ScrollTo(_vm.Messages.LastOrDefault(), position:ScrollToPosition.End, animate:true));}
    protected override void OnAppearing(){base.OnAppearing();_vm.InitIfNeeded();}
}
