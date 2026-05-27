using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DeliveryApp.Driver.Models;
using DeliveryApp.Driver.Services;
using System.Collections.ObjectModel;

namespace DeliveryApp.Driver.ViewModels;

public partial class AvailableOrdersViewModel : BaseViewModel
{
    readonly ApiService _api;
    readonly LocationService _location;
    System.Timers.Timer? _timer;

    [ObservableProperty] bool _isRefreshing;
    [ObservableProperty] string _ordersCount = "0 orders";

    public ObservableCollection<AvailableOrder> Orders { get; } = new();

    public AvailableOrdersViewModel(ApiService api, LocationService location)
    {
        _api = api;
        _location = location;
    }

    [RelayCommand]
    public async Task LoadAsync()
    {
        IsRefreshing = true;
        try
        {
            var orders = await _api.GetAvailableOrdersAsync();
            Orders.Clear();
            if (orders != null)
                foreach (var o in orders) Orders.Add(o);
            OrdersCount = string.Format(LocalizationService.Get("AvailableOrdersCount"), Orders.Count);
        }
        finally { IsRefreshing = false; }
    }

    [RelayCommand]
    async Task AcceptOrderAsync(AvailableOrder order)
    {
        var confirm = await ConfirmAsync(
            $"Accept delivery from {order.RestaurantName} to {order.DeliveryAddress}?\nEarning: {order.DeliveryFeeText}",
            "Accept Order");
        if (!confirm) return;

        IsBusy = true;
        try
        {
            var ok = await _api.AssignOrderAsync(order.Id);
            if (ok)
            {
                _location.SetOrderId(order.Id);
                await AlertAsync("Order accepted! Head to the restaurant.", "Order Accepted ✓");
                await LoadAsync();
                await Shell.Current.GoToAsync("//HomePage");
            }
            else
            {
                await AlertAsync("This order is no longer available. It may have been taken by another driver.");
                await LoadAsync();
            }
        }
        finally { IsBusy = false; }
    }

    public void StartAutoRefresh()
    {
        _timer = new System.Timers.Timer(15000);
        _timer.Elapsed += (_, _) => MainThread.BeginInvokeOnMainThread(async () => await LoadAsync());
        _timer.Start();
    }

    public void StopAutoRefresh()
    {
        _timer?.Stop();
        _timer?.Dispose();
        _timer = null;
    }
}