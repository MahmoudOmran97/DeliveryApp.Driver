using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace DeliveryApp.Driver.ViewModels;

public partial class BaseViewModel : ObservableObject
{
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsNotBusy))]
    private bool _isBusy;

    public bool IsNotBusy => !IsBusy;

    [RelayCommand]
    protected async Task GoBack() => await Shell.Current.GoToAsync("..");

    protected static Task AlertAsync(string msg, string title = "Notice")
        => Application.Current!.MainPage!.DisplayAlert(title, msg, "OK");

    protected static Task<bool> ConfirmAsync(string msg, string title = "Confirm")
        => Application.Current!.MainPage!.DisplayAlert(title, msg, "Yes", "No");
}