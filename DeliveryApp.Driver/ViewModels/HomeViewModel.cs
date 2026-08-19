using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DeliveryApp.Driver.Models;
using DeliveryApp.Driver.Services;

using System.Collections.ObjectModel;

namespace DeliveryApp.Driver.ViewModels;

public partial class HomeViewModel : BaseViewModel
{
    readonly ApiService _api;
    readonly AuthService _auth;
    readonly SignalRService _hub;
    readonly LocationService _location;

    System.Timers.Timer? _refreshTimer;
    readonly SemaphoreSlim _activeOrdersLoadGate = new(1, 1);

    [ObservableProperty] DriverProfile? _profile;
    [ObservableProperty] ActiveOrder? _activeOrder;
    [ObservableProperty] bool _isOnline;
    [ObservableProperty] string _onlineButtonText = "Go Online";
    [ObservableProperty] Color _onlineButtonColor = Color.FromArgb("#4CAF50");
    [ObservableProperty] string _greetingName = string.Empty;
    [ObservableProperty] EarningsResult? _todayEarnings;
    [ObservableProperty] bool _hasActiveOrder;
    [ObservableProperty] bool _hasActiveOrders;
    [ObservableProperty] bool _isNotVerified;
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasUnreadNotifications))]
    [NotifyPropertyChangedFor(nameof(UnreadNotificationsText))]
    int _unreadNotifications;
    [ObservableProperty] bool _hasPendingDues;

    public bool HasUnreadNotifications => UnreadNotifications > 0;
    public string UnreadNotificationsText => UnreadNotifications > 9 ? "9+" : UnreadNotifications.ToString();
    public ObservableCollection<DriverOrder> ActiveOrders { get; } = new();

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
                await LoadActiveOrdersAsync();
            });
        };

        _hub.NewOrderAvailable += _ =>
        {
            MainThread.BeginInvokeOnMainThread(async () =>
            {
                await LoadActiveOrderAsync();
                await LoadActiveOrdersAsync();
            });
        };

        _hub.NotificationReceived += (_, _, _) =>
        {
            MainThread.BeginInvokeOnMainThread(async () =>
            {
                var result = await _api.GetNotificationsAsync();
                if (result?.Data != null)
                    UnreadNotifications = result.Data.Count(n => !n.IsRead);

                await LoadDuesSummaryAsync();
            });
        };

        _hub.Reconnected += () =>
        {
            MainThread.BeginInvokeOnMainThread(async () =>
            {
                await LoadActiveOrdersAsync();
                await JoinActiveOrderGroupsAsync();
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
            var myOrdersTask = _api.GetMyOrdersAsync();
            var notifTask = _api.GetNotificationsAsync();
            var duesSummaryTask = _api.GetMyDuesSummaryAsync();

            await Task.WhenAll(profileTask, earningsTask, activeTask, myOrdersTask, notifTask, duesSummaryTask);

            Profile = profileTask.Result;
            TodayEarnings = earningsTask.Result;
            HasPendingDues = duesSummaryTask.Result?.HasPending ?? false;

            if (Profile != null)
            {
                IsOnline = Profile.IsOnline;
                UpdateOnlineButton();
                IsNotVerified = !Profile.IsVerified;
            }

            var active = activeTask.Result;
            ActiveOrder = active?.Id > 0 ? active : null;
            HasActiveOrder = ActiveOrder != null;
            UpdateActiveOrders(myOrdersTask.Result?.Data, ActiveOrder);

            var notifs = notifTask.Result;
            if (notifs?.Data != null)
                UnreadNotifications = notifs.Data.Count(n => !n.IsRead);

            // Start GPS if online
            if (IsOnline)
            {
                _location.StartTracking(ActiveOrder?.Id);
                await _hub.ConnectAsync(_auth.GetToken());
                await JoinActiveOrderGroupsAsync();
            }
        }
        finally { IsBusy = false; }
    }

    private async Task LoadDuesSummaryAsync()
    {
        var summary = await _api.GetMyDuesSummaryAsync();
        HasPendingDues = summary?.HasPending ?? false;
    }

    async Task JoinActiveOrderGroupsAsync()
    {
        // خُد snapshot قبل أول await؛ ActiveOrders ممكن تتحدث من event آخر
        // أثناء انتظار SignalR، وenumeration على ObservableCollection وقتها تعمل crash.
        var ordersSnapshot = ActiveOrders.ToList();

        foreach (var order in ordersSnapshot)
        {
            try { await _hub.JoinOrderAsync(order.Id); }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[Call] JoinOrder {order.Id} failed: {ex.Message}");
            }
        }
    }

    private async Task LoadActiveOrderAsync()
    {
        var active = await _api.GetActiveOrderAsync();
        ActiveOrder = active?.Id > 0 ? active : null;
        HasActiveOrder = ActiveOrder != null;

        if (HasActiveOrder)
            _location.SetOrderId(ActiveOrder!.Id);
    }

    private async Task LoadActiveOrdersAsync()
    {
        await _activeOrdersLoadGate.WaitAsync();
        try
        {
            var myOrders = await _api.GetMyOrdersAsync();
            UpdateActiveOrders(myOrders?.Data, ActiveOrder);
        }
        finally
        {
            _activeOrdersLoadGate.Release();
        }
    }

    private void UpdateActiveOrders(List<DriverOrder>? orders, ActiveOrder? activeOrder = null)
    {
        var activeStatuses = new[] { "Assigned", "Accepted", "Preparing", "ReadyForPickup", "OnTheWay" };
        var activeOrders = (orders ?? new List<DriverOrder>())
            .Where(o => activeStatuses.Contains(o.Status, StringComparer.OrdinalIgnoreCase))
            .OrderByDescending(o => o.CreatedAt)
            .ToList();

        if (activeOrder is { Id: > 0 } &&
            activeStatuses.Contains(activeOrder.Status, StringComparer.OrdinalIgnoreCase) &&
            !activeOrders.Any(o => o.Id == activeOrder.Id))
        {
            activeOrders.Insert(0, new DriverOrder
            {
                Id = activeOrder.Id,
                Status = activeOrder.Status,
                TotalAmount = activeOrder.TotalAmount,
                DeliveryFee = activeOrder.DeliveryFee,
                DeliveryAddress = activeOrder.DeliveryAddress,
                CreatedAt = DateTime.UtcNow,
                RestaurantName = activeOrder.RestaurantName
            });
        }

        ActiveOrders.Clear();
        foreach (var order in activeOrders)
            ActiveOrders.Add(order);

        HasActiveOrders = ActiveOrders.Count > 0;
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

    [RelayCommand]
    async Task OpenOrderDetailsAsync(DriverOrder? order)
    {
        if (order == null) return;

        IsBusy = true;
        try
        {
            var details = await _api.GetOrderDetailsForDriverAsync(order.Id);
            if (details == null || details.Id <= 0)
            {
                await AlertAsync(LocalizationService.Get("OrderDetailsNotAvailable"));
                return;
            }

            await Shell.Current.GoToAsync(nameof(Views.ActiveDeliveryPage),
                new Dictionary<string, object> { ["Order"] = details });
        }
        finally { IsBusy = false; }
    }

    private void UpdateOnlineButton()
    {
        OnlineButtonText = IsOnline
            ? LocalizationService.Get("GoOffline")
            : LocalizationService.Get("GoOnline");
        OnlineButtonColor = IsOnline ? Color.FromArgb("#F44336") : Color.FromArgb("#4CAF50");
    }

    public void Cleanup()
    {
        _refreshTimer?.Stop();
        _refreshTimer?.Dispose();
    }
}