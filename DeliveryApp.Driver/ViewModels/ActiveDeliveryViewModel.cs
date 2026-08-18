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

    // ── العداد اللحظي (زي تطبيق الكاستمر بالظبط) ──
    System.Timers.Timer? _countdownTimer;
    DateTime? _prepStartUtc;
    DateTime? _prepTargetUtc;
    DateTime? _deliveryStartUtc;
    DateTime? _deliveryTargetUtc;

    [ObservableProperty] bool _isPrepTimerVisible;
    [ObservableProperty] string _prepCountdownText = "00:00";
    [ObservableProperty] double _prepTimerProgress;

    [ObservableProperty] bool _isDeliveryTimerVisible;
    [ObservableProperty] string _deliveryCountdownText = "00:00";
    [ObservableProperty] double _deliveryTimerProgress;

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

            ConfigurePrepCountdown(value);
            ConfigureDeliveryCountdown(value);
            if (_countdownTimer == null)
            {
                _countdownTimer = new System.Timers.Timer(1_000);
                _countdownTimer.Elapsed += (_, _) => MainThread.BeginInvokeOnMainThread(() =>
                {
                    UpdatePrepCountdown();
                    UpdateDeliveryCountdown();
                });
                _countdownTimer.Start();
            }

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

    // ── يظهر عداد التحضير طول ما الأوردر لسة "Preparing" (نفس منطق تطبيق الكاستمر) ──
    void ConfigurePrepCountdown(ActiveOrder order)
    {
        IsPrepTimerVisible = false;

        if (order.Status != "Preparing") return;

        _prepStartUtc = order.AcceptedAt ?? order.CreatedAt;
        var prepMinutes = Math.Clamp(order.EstimatedDeliveryMax ?? 25, 10, 90);
        _prepTargetUtc = _prepStartUtc.Value.AddMinutes(prepMinutes);
        IsPrepTimerVisible = true;
        UpdatePrepCountdown();
    }

    void UpdatePrepCountdown()
    {
        if (!IsPrepTimerVisible || !_prepStartUtc.HasValue || !_prepTargetUtc.HasValue) return;

        var now = DateTime.UtcNow;
        var total = Math.Max(1, (_prepTargetUtc.Value - _prepStartUtc.Value).TotalSeconds);
        var remaining = Math.Max(0, (_prepTargetUtc.Value - now).TotalSeconds);
        PrepCountdownText = FormatCountdown(remaining);
        PrepTimerProgress = Math.Clamp(1 - remaining / total, 0, 1);
    }

    // ── يظهر عداد التوصيل من لحظة ما الدريفر يستلم الطلب فعلياً (OnTheWay) ──
    void ConfigureDeliveryCountdown(ActiveOrder order)
    {
        if (order.Status != "OnTheWay")
        {
            IsDeliveryTimerVisible = false;
            _deliveryStartUtc = null;
            _deliveryTargetUtc = null;
            return;
        }

        // ??= عشان لو الأوردر اتحدّث تاني وهو لسه OnTheWay، ميعملش reset للعداد من الأول
        _deliveryStartUtc ??= order.PickedUpAt ?? DateTime.UtcNow;
        var deliveryMinutes = Math.Max(10, order.EstimatedDeliveryMax ?? 25);
        _deliveryTargetUtc ??= _deliveryStartUtc.Value.AddMinutes(deliveryMinutes);
        IsDeliveryTimerVisible = true;
        UpdateDeliveryCountdown();
    }

    void UpdateDeliveryCountdown()
    {
        if (!IsDeliveryTimerVisible || !_deliveryStartUtc.HasValue || !_deliveryTargetUtc.HasValue) return;

        var now = DateTime.UtcNow;
        var total = Math.Max(1, (_deliveryTargetUtc.Value - _deliveryStartUtc.Value).TotalSeconds);
        var remaining = Math.Max(0, (_deliveryTargetUtc.Value - now).TotalSeconds);
        DeliveryCountdownText = FormatCountdown(remaining);
        DeliveryTimerProgress = Math.Clamp(1 - remaining / total, 0, 1);
    }

    // ← بيتنادى من صفحة الماب لما مسار OSRM الحقيقي يرجع مدة أدق من الـ estimate الثابت
    public void UpdateDeliveryEta(double durationSeconds)
    {
        if (durationSeconds <= 0 || Order?.Status != "OnTheWay") return;

        _deliveryStartUtc ??= Order.PickedUpAt ?? DateTime.UtcNow;
        _deliveryTargetUtc = _deliveryStartUtc.Value.AddSeconds(Math.Max(60, durationSeconds));
        MainThread.BeginInvokeOnMainThread(UpdateDeliveryCountdown);
    }

    static string FormatCountdown(double seconds)
    {
        var total = Math.Max(0, (int)Math.Ceiling(seconds));
        var hours = total / 3600;
        var minutes = (total % 3600) / 60;
        var secs = total % 60;
        return hours > 0 ? $"{hours:00}:{minutes:00}:{secs:00}" : $"{minutes:00}:{secs:00}";
    }

    public void Cleanup()
    {
        _countdownTimer?.Stop();
        _countdownTimer?.Dispose();
        _countdownTimer = null;
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