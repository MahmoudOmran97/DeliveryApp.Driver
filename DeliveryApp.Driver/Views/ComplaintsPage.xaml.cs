using DeliveryApp.Driver.ViewModels;
namespace DeliveryApp.Driver.Views;
public partial class ComplaintsPage : ContentPage
{
    readonly ComplaintsViewModel _vm;
    public ComplaintsPage(ComplaintsViewModel vm){InitializeComponent();_vm=vm;BindingContext=vm;}
    protected override void OnAppearing(){base.OnAppearing();_vm.LoadCommand.Execute(null);}
}
