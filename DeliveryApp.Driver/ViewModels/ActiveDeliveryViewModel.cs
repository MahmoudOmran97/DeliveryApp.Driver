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
    readonly SignalRService _signalR; // ✅ FIX #Call — كانت ناقصة، عشانها المكالمة ما كانتش شغالة
    readonly AuthService _auth; // ✅ FIX #CallGroup — عشان نقدر نعمل ConnectAsync لو مش متصل

    [ObservableProperty] ActiveOrder? _order;
    [ObservableProperty] double _driverLat;
    [ObservableProperty] double _driverLng;

    public event Action? MapUpdated;

    public ActiveDeliveryViewModel(
        ApiService api,
        LocationService location,
        ChatNotificationService chatNotif, // ✅ FIX #4 — inject
        SignalRService signalR, // ✅ FIX #Call — inject
        AuthService auth) // ✅ FIX #CallGroup — inject
    {
        _api = api;
        _location = location;
        _chatNotif = chatNotif;
        _signalR = signalR;
        _auth = auth;

        _location.LocationUpdated += (lat, lng) =>
        {
            DriverLat = lat;
            DriverLng = lng;
            MapUpdated?.Invoke();
        };

        // ✅ FIX #CallGroup — لو الاتصال يتقطع ويرجع (SignalR AutomaticReconnect)،
        // الـ ConnectionId بيتغيّر والسيرفر بينسى إن الدرايفر كان جوه جروب الطلب،
        // فلازم نرجع نضم نفسنا تاني عشان تفضل المكالمات والشات شغالة.
        _signalR.Reconnected += () => _ = JoinOrderGroupAsync();
    }

    partial void OnOrderChanged(ActiveOrder? value)
    {
        if (value != null)
        {
            _location.SetOrderId(value.Id);
            _location.StartTracking(value.Id);
            // ✅ FIX #4 — سجّل الطلب عشان لو العميل بعت رسالة يظهر للدرايفر notification
            _chatNotif.RegisterOrder(value.Id, value.CustomerName);
            _ = LoadInitialDriverLocationAsync();

            // ✅ FIX #CallGroup — دي كانت الـ bug الرئيسية: الدرايفر مكنش بينضم أبداً لجروب
            // "order_{orderId}" على الـ Hub، فكل الأحداث اللي بتتبعت بالـ Group
            // (IncomingVoiceCall / VoiceCallAccepted / VoiceCallRejected / VoiceCallEnded)
            // مكنتش توصله خالص. عشان كده:
            //  - لما العميل يتصل، الدرايفر مكنش بيرن.
            //  - لما الدرايفر يتصل والعميل يقبل، الدرايفر كان فاضل واقف على "جارِ الاتصال"
            //    لأن VoiceCallAccepted مكنش بيوصله، رغم إن العميل دخل فعلاً.
            _ = JoinOrderGroupAsync();
        }
    }

    async Task JoinOrderGroupAsync()
    {
        if (Order == null) return;

        if (!_signalR.IsConnected)
            await _signalR.ConnectAsync(_auth.GetToken());

        await _signalR.JoinOrderAsync(Order.Id);
    }

    async Task LoadInitialDriverLocationAsync()
    {
        var current = await _location.GetCurrentLocationAsync();
        if (current.HasValue)
        {
            DriverLat = current.Value.lat;
            DriverLng = current.Value.lng;
            MapUpdated?.Invoke();
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
                    {
                        if (string.IsNullOrWhiteSpace(updated.CustomerName))
                            updated.CustomerName = Order.CustomerName;
                        Order = updated;
                    }
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

        if (!_signalR.IsConnected)
        {
            await AlertAsync("Not connected to server. Please check your internet connection and try again.", "Call Failed");
            return;
        }

        // ✅ FIX — دي كانت بتنادي _api.StartVoiceCallAsync اللي stub فاضي مبيعملش حاجة.
        // دلوقتي بتنادي SignalRService اللي فعلاً بيبعت الحدث للـ Hub، والـ Hub بيبعت
        // IncomingVoiceCall + FCM push للعميل حتى لو قافل الأبليكيشن.
        await _signalR.StartVoiceCallAsync(Order.Id);
        await Shell.Current.GoToAsync(
            $"CallPage?orderId={Order.Id}&otherPartyName={Uri.EscapeDataString(Order.CustomerName)}&isIncoming=false");
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