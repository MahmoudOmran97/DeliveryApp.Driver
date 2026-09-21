// ═══════════════════════════════════════════════════════════════
// DeliveryApp.Driver / Models / AvailableOrderDetails.cs
// تفاصيل طلب متاح كاملة، بتظهر للدريفر قبل ما يقبله
// (GET orders/available/{id} — من غير بيانات العميل الشخصية)
// ═══════════════════════════════════════════════════════════════
using DeliveryApp.Driver.Services;

namespace DeliveryApp.Driver.Models;

public class AvailableOrderDetails
{
    public int Id { get; set; }
    public string Status { get; set; } = string.Empty;
    public decimal SubTotal { get; set; }
    public decimal DeliveryFee { get; set; }
    public decimal Discount { get; set; }
    public decimal TotalAmount { get; set; }
    public string PaymentMethod { get; set; } = string.Empty;
    public string DeliveryAddress { get; set; } = string.Empty;
    public string? DeliveryNotes { get; set; }
    public int? EstimatedDeliveryMin { get; set; }
    public int? EstimatedDeliveryMax { get; set; }
    public DateTime CreatedAt { get; set; }
    public string RestaurantName { get; set; } = string.Empty;
    public string? RestaurantAddress { get; set; }
    public double? DistanceKm { get; set; }

    // ✅ من السيرفر: المسافة بين المحل وعنوان العميل (مش بين الدريفر والمحل)
    public double? RestaurantToCustomerDistanceKm { get; set; }

    // ✅ من السيرفر: الطلب جوه نطاق القبول المسموح للدريفر ولا لأ
    public bool CanAccept { get; set; } = true;

    public List<AvailableOrderItem> Items { get; set; } = new();

    // ── للعرض ──
    public string OrderNumberText => $"#{Id}";
    public string SubTotalText => $"{SubTotal:F0} EGP";
    public string DiscountText => $"-{Discount:F0} EGP";
    public string DeliveryFeeText => $"{DeliveryFee:F0} EGP";
    public string TotalAmountText => $"{TotalAmount:F0} EGP";
    public bool HasDiscount => Discount > 0;
    public bool HasNotes => !string.IsNullOrWhiteSpace(DeliveryNotes);
    public bool HasRestaurantAddress => !string.IsNullOrWhiteSpace(RestaurantAddress);

    public string DistanceText => DistanceKm.HasValue ? $"{DistanceKm:F1} km" : "--";
    public string RestaurantToCustomerDistanceText => RestaurantToCustomerDistanceKm.HasValue
        ? $"{RestaurantToCustomerDistanceKm:F1} km"
        : "--";
    public bool IsOutOfRange => !CanAccept;
    public string AcceptButtonText => CanAccept
        ? LocalizationService.Get("AcceptOrder")
        : LocalizationService.Get("OutOfRange");
    public string PreparationTimeText => EstimatedDeliveryMin.HasValue && EstimatedDeliveryMax.HasValue
        ? $"{EstimatedDeliveryMin}-{EstimatedDeliveryMax} min"
        : "--";

    // CreatedAt بيوصل متحوّل لتوقيت الجهاز أصلاً (UtcDateTimeConverter)
    public string CreatedAtText => CreatedAt.ToString("MMM dd, hh:mm tt");

    public string StatusText => Status switch
    {
        "Preparing" => LocalizationService.Get("OrderPreparing"),
        "ReadyForPickup" => LocalizationService.Get("OrderReadyForPickup"),
        _ => Status
    };

    public Color StatusColor => Status switch
    {
        "ReadyForPickup" => Color.FromArgb("#FF9800"),
        _ => Color.FromArgb("#FF5722")
    };

    public string PaymentMethodText => string.Equals(PaymentMethod, "Cash", StringComparison.OrdinalIgnoreCase)
        ? LocalizationService.Get("AOD_PayCash")
        : PaymentMethod;
}

public class AvailableOrderItem
{
    public string ProductName { get; set; } = string.Empty;
    public int Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal TotalPrice { get; set; }
    public string? Notes { get; set; }

    public string Display => $"{Quantity}x {ProductName}";
    public string TotalPriceText => $"{TotalPrice:F0} EGP";
    public bool HasNotes => !string.IsNullOrWhiteSpace(Notes);
}
