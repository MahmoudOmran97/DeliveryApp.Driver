using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DeliveryApp.Driver.Models;
using DeliveryApp.Driver.Services;
using Microsoft.UI.Xaml.Documents;
using System.Collections.ObjectModel;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace DeliveryApp.Driver.ViewModels;

public partial class HomeViewModel : BaseViewModel
{
    readonly ApiService _api;
    readonly AuthService _auth;
    readonly SignalRService _hub;
    readonly LocationService _location;

    System.Timers.Timer? _refreshTimer;

    [ObservableProperty] DriverProfile? _profile;
    [ObservableProperty] ActiveOrder? _activeOrder;
    [ObservableProperty] bool _isOnline;
    [ObservableProperty] string _onlineButtonText = "Go Online";
    [ObservableProperty] Color _onlineButtonColor = Color.FromArgb("#4CAF50");
    [ObservableProperty] string _greetingName = string.Empty;
    [ObservableProperty] EarningsResult? _todayEarnings;
    [ObservableProperty] bool _hasActiveOrder;
    [ObservableProperty] bool _isNotVerified;

    public HomeViewModel(ApiService api, AuthService auth, SignalRService hub, LocationService location)
    {
        _api = api;
        _auth = auth;
        _hub = hub;
        _location = location;

        GreetingName = auth.GetUserName();

        _hub.OrderStatusChanged += (id, status) =>
        {
            MainThread.BeginInvokeOnMainThread(async () =>
            {
                await LoadActiveOrderAsync();
            });
        };

        _hub.NewOrderAvailable += _ =>
        {
            MainThread.BeginInvokeOnMainThread(async () =>
            {
                await LoadActiveOrderAsync();
            });
        };
    }

    [RelayCommand]
    public async Task LoadAsync()
    {
        IsBusy = true;
        try
        {
            var profileTask = _api.GetMyProfileAsync();
            var earningsTask = _api.GetEarningsAsync("today");
            var activeTask = _api.GetActiveOrderAsync();

            await Task.WhenAll(profileTask, earningsTask, activeTask);

            Profile = profileTask.Result;
            TodayEarnings = earningsTask.Result;

            if (Profile != null)
            {
                IsOnline = Profile.IsOnline;
                UpdateOnlineButton();
                IsNotVerified = !Profile.IsVerified;
            }

            var active = activeTask.Result;
            ActiveOrder = active?.Id > 0 ? active : null;
            HasActiveOrder = ActiveOrder != null;

            // Start GPS if online
            if (IsOnline)
            {
                _location.StartTracking(ActiveOrder?.Id);
                await _hub.ConnectAsync(_auth.GetToken());
            }
        }
        finally { IsBusy = false; }
    }

    private async Task LoadActiveOrderAsync()
    {
        var active = await _api.GetActiveOrderAsync();
        ActiveOrder = active?.Id > 0 ? active : null;
        HasActiveOrder = ActiveOrder != null;

        if (HasActiveOrder)
            _location.SetOrderId(ActiveOrder!.Id);
    }

    [RelayCommand]
    async Task ToggleOnlineAsync()
    {
        if (Profile?.IsVerified == false)
        {
            await AlertAsync("Your account is pending verification by admin. Please wait.");
            return;
        }

        IsBusy = true;
        try
        {
            var (isOnline, message) = await _api.ToggleOnlineAsync();
            IsOnline = isOnline;
            UpdateOnlineButton();

            if (isOnline)
            {
                _location.StartTracking();
                await _hub.ConnectAsync(_auth.GetToken());
            }
            else
            {
                _location.StopTracking();
                await _hub.DisconnectAsync();
            }
        }
        finally { IsBusy = false; }
    }

    [RelayCommand]
    async Task GoToDeliveryAsync()
    {
        if (ActiveOrder == null) return;
        await Shell.Current.GoToAsync(nameof(Views.ActiveDeliveryPage),
            new Dictionary<string, object> { ["Order"] = ActiveOrder });
    }

    private void UpdateOnlineButton()
    {
        OnlineButtonText = IsOnline ? "Go Offline" : "Go Online";
        OnlineButtonColor = IsOnline ? Color.FromArgb("#F44336") : Color.FromArgb("#4CAF50");
    }

    public void Cleanup()
    {
        _refreshTimer?.Stop();
        _refreshTimer?.Dispose();
    }
}