using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DeliveryApp.Driver.Models;
using DeliveryApp.Driver.Services;

namespace DeliveryApp.Driver.ViewModels;

/// <summary>
/// صفحة تفاصيل طلب متاح: الدريفر بيشوف كل تفاصيل الطلب قبل ما يقبله.
/// بتتفتح من كارت الطلب في صفحة الطلبات المتاحة (بتاخد OrderId بس وبتجيب الباقي من السيرفر).
/// </summary>
[QueryProperty(nameof(OrderId), "OrderId")]
public partial class AvailableOrderDetailsViewModel : BaseViewModel
{
    readonly ApiService _api;
    readonly LocationService _location;

    [ObservableProperty] int _orderId;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasOrder))]
    AvailableOrderDetails? _order;

    public bool HasOrder => Order != null;

    // برّه نطاق القبول أو الصفحة مشغولة (Busy) → الزرار يتقفل
    public bool CanAccept => IsNotBusy && (Order?.CanAccept ?? false);

    public AvailableOrderDetailsViewModel(ApiService api, LocationService location)
    {
        _api = api;
        _location = location;

        // IsBusy متعرّفة في BaseViewModel، فبنسمع للتغيير عشان نحدّث CanAccept تبعها
        PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(IsBusy))
                OnPropertyChanged(nameof(CanAccept));
        };
    }

    partial void OnOrderChanged(AvailableOrderDetails? value)
        => OnPropertyChanged(nameof(CanAccept));

    [RelayCommand]
    public async Task LoadAsync()
    {
        if (OrderId <= 0) return;

        IsBusy = true;
        try
        {
            var details = await _api.GetAvailableOrderDetailsAsync(OrderId);
            if (details == null || details.Id <= 0)
            {
                // الطلب اتاخد من دريفر تاني (أو اتلغى) أو الدريفر بقى Offline
                await AlertAsync(LocalizationService.Get("AOD_NoLongerAvailable"));
                await Shell.Current.GoToAsync("..");
                return;
            }

            Order = details;
        }
        finally { IsBusy = false; }
    }

    [RelayCommand]
    async Task AcceptAsync()
    {
        var order = Order;
        if (order == null) return;

        // الطلب برّه نطاق القبول — منسمحش بالقبول حتى لو الزرار اتنادى عليه برمجياً
        if (!order.CanAccept)
        {
            await AlertAsync(LocalizationService.Get("OutOfRangeMessage"));
            return;
        }

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
                await Shell.Current.GoToAsync("//HomePage");
                return;
            }

            var hasMessage = !string.IsNullOrWhiteSpace(message);

            // عنده طلب شغال بالفعل: الطلب ده لسه متاح، فنسيبه على الصفحة يقرا التفاصيل
            if (hasMessage && message!.Contains("active order", StringComparison.OrdinalIgnoreCase))
            {
                await AlertAsync("You already have an active order. Deliver it before accepting a new one.");
                return;
            }

            var text = hasMessage && message!.Contains("online", StringComparison.OrdinalIgnoreCase)
                ? LocalizationService.Get("OfflineNoAvailableOrders")
                : LocalizationService.Get("AOD_NoLongerAvailable");

            await AlertAsync(text);
            await Shell.Current.GoToAsync("..");
        }
        finally { IsBusy = false; }
    }
}
