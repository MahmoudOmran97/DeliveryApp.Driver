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

    [ObservableProperty] bool _isRefreshing;
    [ObservableProperty] string _ordersCount = "0 orders";

    // لو الدريفر Offline منعرضلوش طلبات، ونوريه رسالة توضح السبب بدل "مفيش طلبات"
    [ObservableProperty] bool _isOffline;
    [ObservableProperty] string _emptyMessage = LocalizationService.Get("NoAvailableOrders");

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
            // حالة الأونلاين من السيرفر (مصدر الحقيقة) — بنجيبها مع الطلبات في نفس الوقت
            var profileTask = _api.GetMyProfileAsync();
            var ordersTask = _api.GetAvailableOrdersAsync();
            await Task.WhenAll(profileTask, ordersTask);

            IsOffline = profileTask.Result is { IsOnline: false };
            EmptyMessage = LocalizationService.Get(IsOffline ? "OfflineNoAvailableOrders" : "NoAvailableOrders");

            Orders.Clear();
            if (!IsOffline && ordersTask.Result is { } orders)
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
            var (ok, message) = await _api.AssignOrderWithMessageAsync(order.Id);
            if (ok)
            {
                _location.SetOrderId(order.Id);
                await AlertAsync("Order accepted! Head to the restaurant.", "Order Accepted ✓");
                await LoadAsync();
                await Shell.Current.GoToAsync("//HomePage");
            }
            else
            {
                // لو السيرفر رافض لأن عندك طلب شغال بالفعل (أو إنت Offline)، نوضح ده للدريفر بدل رسالة عامة
                var hasMessage = !string.IsNullOrWhiteSpace(message);
                var text =
                    hasMessage && message!.Contains("active order", StringComparison.OrdinalIgnoreCase)
                        ? "You already have an active order. Deliver it before accepting a new one."
                    : hasMessage && message!.Contains("online", StringComparison.OrdinalIgnoreCase)
                        ? LocalizationService.Get("OfflineNoAvailableOrders")
                        : "This order is no longer available. It may have been taken by another driver.";
                await AlertAsync(text);
                await LoadAsync();
            }
        }
        finally { IsBusy = false; }
    }

}