namespace DeliveryApp.Driver.Views;

public partial class CallPage : ContentPage
{
    readonly ViewModels.CallViewModel _vm;

    public CallPage(ViewModels.CallViewModel vm)
    {
        InitializeComponent();
        _vm = vm;
        BindingContext = vm;
    }

    protected override bool OnBackButtonPressed() => true; // مانعين رجوع بالـ back الهاردوير أثناء مكالمة، لازم يستخدم الأزرار
}
