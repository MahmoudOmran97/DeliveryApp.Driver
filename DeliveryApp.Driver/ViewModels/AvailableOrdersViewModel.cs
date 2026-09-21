using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DeliveryApp.Driver.Models;
using DeliveryApp.Driver.Services;
using System.Collections.ObjectModel;

namespace DeliveryApp.Driver.ViewModels;

// ── شريحة فلتر واحدة (نطاق أو منطقة) — بتتلون لما تبقى محددة ──
public partial class FilterChipItem : ObservableObject
{
    // Value = null بيعني "الكل" (كل النطاقات أو كل المناطق)
    public string? Value { get; init; }
    public string Label { get; init; } = string.Empty;

    [ObservableProperty] bool _isSelected;
}

public partial class AvailableOrdersViewModel : BaseViewModel
{
    readonly ApiService _api;
    readonly LocationService _location;

    [ObservableProperty] bool _isRefreshing;
    [ObservableProperty] string _ordersCount = "0 orders";

    // لو الدريفر Offline منعرضلوش طلبات، ونوريه رسالة توضح السبب بدل "مفيش طلبات"
    [ObservableProperty] bool _isOffline;
    [ObservableProperty] string _emptyMessage = LocalizationService.Get("NoAvailableOrders");

    // القايمة المعروضة فعليًا (بعد الفلترة) — الـ CollectionView مربوط عليها
    public ObservableCollection<AvailableOrder> Orders { get; } = new();

    // النسخة الكاملة الجاية من السيرفر (قبل أي فلترة) — بنفلتر منها محليًا بدون ريكوست جديد
    List<AvailableOrder> _allOrders = new();

    // ── شريط الفلاتر ──
    // النطاق: الكل / جوه النطاق / برّه النطاق (3 خيارات ثابتة)
    public ObservableCollection<FilterChipItem> RangeFilters { get; } = new();
    // المنطقة: الكل + منطقة لكل مجموعة محلات ظاهرة حاليًا في الطلبات (ديناميكي حسب الـ Zones الراجعة من السيرفر)
    public ObservableCollection<FilterChipItem> ZoneFilters { get; } = new();

    string? _selectedRangeValue; // null = الكل, "in" = جوه النطاق, "out" = برّه النطاق
    string? _selectedZoneName;   // null = كل المناطق

    public AvailableOrdersViewModel(ApiService api, LocationService location)
    {
        _api = api;
        _location = location;

        RangeFilters.Add(new FilterChipItem { Value = null, Label = LocalizationService.Get("FilterAll"), IsSelected = true });
        RangeFilters.Add(new FilterChipItem { Value = "in", Label = LocalizationService.Get("FilterInRange") });
        RangeFilters.Add(new FilterChipItem { Value = "out", Label = LocalizationService.Get("FilterOutOfRange") });

        ZoneFilters.Add(new FilterChipItem { Value = null, Label = LocalizationService.Get("FilterAllZones"), IsSelected = true });
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

            _allOrders = (!IsOffline && ordersTask.Result is { } orders) ? orders : new List<AvailableOrder>();

            RebuildZoneFilters();
            ApplyFilters();
        }
        finally { IsRefreshing = false; }
    }

    // بنبني شرائح المناطق من غير المناطق الموجودة فعلاً في الطلبات الحالية بس (مفيش داعي نعرض
    // منطقة مفيهاش طلبات دلوقتي)، ونحافظ على اختيار الدريفر لو المنطقة لسه موجودة بعد الريفريش.
    void RebuildZoneFilters()
    {
        var distinctZones = _allOrders
            .Where(o => !string.IsNullOrWhiteSpace(o.ZoneName))
            .Select(o => o.ZoneName!)
            .Distinct()
            .OrderBy(z => z)
            .ToList();

        ZoneFilters.Clear();
        ZoneFilters.Add(new FilterChipItem { Value = null, Label = LocalizationService.Get("FilterAllZones") });
        foreach (var z in distinctZones)
            ZoneFilters.Add(new FilterChipItem { Value = z, Label = z });

        // لو المنطقة المختارة قبل كده مبقتش موجودة (مفيش طلبات فيها دلوقتي)، نرجع لـ"كل المناطق"
        if (_selectedZoneName != null && !distinctZones.Contains(_selectedZoneName))
            _selectedZoneName = null;

        foreach (var chip in ZoneFilters)
            chip.IsSelected = chip.Value == _selectedZoneName;
    }

    [RelayCommand]
    void SelectRangeFilter(FilterChipItem chip)
    {
        if (chip.Value == _selectedRangeValue) return;
        _selectedRangeValue = chip.Value;
        foreach (var c in RangeFilters) c.IsSelected = c == chip;
        ApplyFilters();
    }

    [RelayCommand]
    void SelectZoneFilter(FilterChipItem chip)
    {
        if (chip.Value == _selectedZoneName) return;
        _selectedZoneName = chip.Value;
        foreach (var c in ZoneFilters) c.IsSelected = c == chip;
        ApplyFilters();
    }

    void ApplyFilters()
    {
        IEnumerable<AvailableOrder> filtered = _allOrders;

        if (_selectedRangeValue == "in")
            filtered = filtered.Where(o => o.CanAccept);
        else if (_selectedRangeValue == "out")
            filtered = filtered.Where(o => !o.CanAccept);

        if (_selectedZoneName != null)
            filtered = filtered.Where(o => o.ZoneName == _selectedZoneName);

        Orders.Clear();
        foreach (var o in filtered) Orders.Add(o);

        OrdersCount = string.Format(LocalizationService.Get("AvailableOrdersCount"), Orders.Count);
    }

    // الضغط على الكارت: يفتح صفحة بكل تفاصيل الطلب قبل ما الدريفر يقبله
    [RelayCommand]
    async Task OpenDetailsAsync(AvailableOrder? order)
    {
        if (order == null || IsBusy) return;

        await Shell.Current.GoToAsync(nameof(Views.AvailableOrderDetailsPage),
            new Dictionary<string, object> { ["OrderId"] = order.Id });
    }

    [RelayCommand]
    async Task AcceptOrderAsync(AvailableOrder order)
    {
        // الطلب برّه نطاق القبول (بعيد عن الدريفر) — منمنعوش من الظهور بس منسمحش بالقبول
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
                await LoadAsync();
                await Shell.Current.GoToAsync("//HomePage");
            }
            else
            {
                // لو السيرفر رافض لأن عندك طلب شغال بالفعل (أو إنت Offline أو الطلب برّه نطاقك)، نوضح ده للدريفر بدل رسالة عامة
                var hasMessage = !string.IsNullOrWhiteSpace(message);
                var text =
                    hasMessage && message!.Contains("active order", StringComparison.OrdinalIgnoreCase)
                        ? "You already have an active order. Deliver it before accepting a new one."
                    : hasMessage && message!.Contains("delivery range", StringComparison.OrdinalIgnoreCase)
                        ? LocalizationService.Get("OutOfRangeMessage")
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