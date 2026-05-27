// ═══════════════════════════════════════════════════════════════
// DeliveryApp.Driver / ViewModels / ActiveDeliveryViewModel.cs
// ═══════════════════════════════════════════════════════════════
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DeliveryApp.Driver.Models;
using DeliveryApp.Driver.Services;

namespace DeliveryApp.Driver.ViewModels;

[QueryProperty(nameof(Order), "Order")]
public partial class ActiveDeliveryViewModel : BaseViewModel
{
    readonly ApiService _api;
    readonly LocationService _location;
    readonly ChatNotificationService _chatNotif; // ✅ FIX #4

    [ObservableProperty] ActiveOrder? _order;
    [ObservableProperty] double _driverLat;
    [ObservableProperty] double _driverLng;

    public event Action? MapUpdated;

    public ActiveDeliveryViewModel(
        ApiService api,
        LocationService location,
        ChatNotificationService chatNotif) // ✅ FIX #4 — inject
    {
        _api = api;
        _location = location;
        _chatNotif = chatNotif;

        _location.LocationUpdated += (lat, lng) =>
        {
            DriverLat = lat;
            DriverLng = lng;
            MapUpdated?.Invoke();
        };
    }

    partial void OnOrderChanged(ActiveOrder? value)
    {
        if (value != null)
        {
            _location.SetOrderId(value.Id);
            // ✅ FIX #4 — سجّل الطلب عشان لو العميل بعت رسالة يظهر للدرايفر notification
            _chatNotif.RegisterOrder(value.Id, value.CustomerName);
        }
    }

    [RelayCommand]
    async Task NextStatusAsync()
    {
        if (Order == null || string.IsNullOrEmpty(Order.NextStatus)) return;

        var nextStatus = Order.NextStatus;
        var actionText = Order.NextActionText;

        var confirm = await ConfirmAsync($"Confirm: {actionText}?");
        if (!confirm) return;

        IsBusy = true;
        try
        {
            var ok = await _api.UpdateOrderStatusAsync(Order.Id, nextStatus);
            if (ok)
            {
                if (nextStatus == "Delivered")
                {
                    _location.SetOrderId(null);
                    _chatNotif.UnregisterOrder(Order.Id); // نظّف بعد التوصيل
                    await AlertAsync("Order delivered successfully! Great job! 🎉", "Delivered ✓");
                    await Shell.Current.GoToAsync("//HomePage");
                }
                else
                {
                    var updated = await _api.GetActiveOrderAsync();
                    if (updated?.Id > 0)
                        Order = updated;
                }
            }
            else
            {
                await AlertAsync("Failed to update order status. Please try again.");
            }
        }
        finally { IsBusy = false; }
    }

    [RelayCommand]
    async Task OpenChatAsync()
    {
        if (Order == null) return;
        await Shell.Current.GoToAsync(
            $"CustomerChatPage?orderId={Order.Id}&customerName={Order.CustomerName}");
    }

    [RelayCommand]
    async Task CallCustomerAsync()
    {
        if (Order == null) return;
        var confirm = await ConfirmAsync("Do you want to start an in-app voice call with the customer?");
        if (!confirm) return;
        await _api.StartVoiceCallAsync(Order.Id);
        await AlertAsync("Calling customer via app... (Voice Call simulation)", "In-App Call");
    }

    [RelayCommand]
    async Task OpenMapAsync()
    {
        if (Order == null) return;
        try
        {
            double lat, lng;
            string label;

            if (Order.IsOnTheWay)
            {
                lat = Order.DeliveryLatitude;
                lng = Order.DeliveryLongitude;
                label = Order.CustomerName;
            }
            else
            {
                lat = Order.RestaurantLat;
                lng = Order.RestaurantLng;
                label = Order.RestaurantName;
            }

            var location = new Location(lat, lng);
            var options = new MapLaunchOptions { Name = label, NavigationMode = NavigationMode.Driving };
            await Map.Default.OpenAsync(location, options);
        }
        catch { await AlertAsync("Cannot open maps app"); }
    }
}