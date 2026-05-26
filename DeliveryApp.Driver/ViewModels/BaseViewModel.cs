using CommunityToolkit.Mvvm.ComponentModel;

namespace DeliveryApp.Driver.ViewModels;

public partial class BaseViewModel : ObservableObject
{
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsNotBusy))]
    private bool _isBusy;

    public bool IsNotBusy => !IsBusy;

    protected static Task AlertAsync(string msg, string title = "Notice")
        => Application.Current!.MainPage!.DisplayAlert(title, msg, "OK");

    protected static Task<bool> ConfirmAsync(string msg, string title = "Confirm")
        => Application.Current!.MainPage!.DisplayAlert(title, msg, "Yes", "No");
}