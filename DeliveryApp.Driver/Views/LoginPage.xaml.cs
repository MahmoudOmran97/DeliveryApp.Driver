using DeliveryApp.Driver.ViewModels;
using System.Globalization;

namespace DeliveryApp.Driver.Views;

public partial class LoginPage : ContentPage
{
    public LoginPage(LoginViewModel vm)
    {
        InitializeComponent();
        BindingContext = vm;
        FlowDirection = DeliveryApp.Driver.Services.LocalizationService.Flow;

        string lang = CultureInfo.CurrentUICulture.TwoLetterISOLanguageName;

        imgLogo.Source = lang == "ar"
            ? "logo_ar.png"
            : "logo_en.png";
        BindingContext = vm;
    }
}