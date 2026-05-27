// ═══════════════════════════════════════════════════════════════
// DeliveryApp.Driver / Models / Models.cs
// ═══════════════════════════════════════════════════════════════
namespace DeliveryApp.Driver.Models;

// ─── Auth ────────────────────────────────────────────────────────────────────

public class LoginResponse
{
    public string Token { get; set; } = string.Empty;
    public int Id { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;
}

// ─── Driver Profile ──────────────────────────────────────────────────────────

public class DriverProfile
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
    public double Rating { get; set; }
    public int TotalDeliveries { get; set; }
    public bool IsOnline { get; set; }
    public bool IsVerified { get; set; }
    public string? ProfileImage { get; set; }
    public decimal TotalEarnings { get; set; }

    public string RatingText => $"{Rating:F1} ★";
    public string TotalDeliveriesText => $"{TotalDeliveries} deliveries";
    public string TotalEarningsText => $"{TotalEarnings:F0} EGP";
    public string StatusText => IsOnline ? "Online" : "Offline";
    public Color StatusColor => IsOnline ? Color.FromArgb("#4CAF50") : Color.FromArgb("#9E9E9E");
    public string VerifiedText => IsVerified ? "Verified ✓" : "Pending Verification";
}

// ─── Available Orders ────────────────────────────────────────────────────────

public class AvailableOrder
{
    public int Id { get; set; }
    public string RestaurantName { get; set; } = string.Empty;
    public string RestaurantAddress { get; set; } = string.Empty;
    public string DeliveryAddress { get; set; } = string.Empty;
    public decimal DeliveryFee { get; set; }
    public int ItemCount { get; set; }
    public decimal TotalAmount { get; set; }
    public double? DistanceKm { get; set; }

    public string DeliveryFeeText => $"{DeliveryFee:F0} EGP";
    public string TotalAmountText => $"{TotalAmount:F0} EGP";
    public string DistanceText => DistanceKm.HasValue ? $"{DistanceKm:F1} km" : "--";
    public string ItemCountText => $"{ItemCount} items";
}

// ─── Active Order ────────────────────────────────────────────────────────────

public class ActiveOrder
{
    public int Id { get; set; }
    public string Status { get; set; } = string.Empty;
    public decimal TotalAmount { get; set; }
    public decimal DeliveryFee { get; set; }
    public string DeliveryAddress { get; set; } = string.Empty;
    public double DeliveryLatitude { get; set; }
    public double DeliveryLongitude { get; set; }
    public string? DeliveryNotes { get; set; }
    public string CustomerName { get; set; } = string.Empty;
    public string CustomerPhone { get; set; } = string.Empty;
    public string RestaurantName { get; set; } = string.Empty;
    public double RestaurantLat { get; set; }
    public double RestaurantLng { get; set; }
    public List<ActiveOrderItem> Items { get; set; } = new();

    public string DeliveryFeeText => $"{DeliveryFee:F0} EGP";

    public string StatusText => Status switch
    {
        "ReadyForPickup" => "Go to Restaurant 🏪",
        "OnTheWay" => "Deliver to Customer 📦",
        _ => Status
    };

    public Color StatusColor => Status switch
    {
        "ReadyForPickup" => Color.FromArgb("#FF9800"),
        "OnTheWay" => Color.FromArgb("#2196F3"),
        _ => Color.FromArgb("#FF5722")
    };

    public bool IsReadyForPickup => Status == "ReadyForPickup";
    public bool IsOnTheWay => Status == "OnTheWay";

    // Next action button
    public string NextActionText => Status switch
    {
        "ReadyForPickup" => "I Picked Up the Order",
        "OnTheWay" => "Order Delivered ✓",
        _ => ""
    };

    public string NextStatus => Status switch
    {
        "ReadyForPickup" => "OnTheWay",
        "OnTheWay" => "Delivered",
        _ => ""
    };
}

public class ActiveOrderItem
{
    public string ProductName { get; set; } = string.Empty;
    public int Quantity { get; set; }
    public string? Notes { get; set; }

    public string Display => $"{Quantity}x {ProductName}";
}

// ─── Earnings ────────────────────────────────────────────────────────────────

public class EarningsResult
{
    public string Period { get; set; } = string.Empty;
    public int TotalDeliveries { get; set; }
    public decimal TotalEarnings { get; set; }
    public List<EarningDelivery> Deliveries { get; set; } = new();

    public string TotalEarningsText => $"{TotalEarnings:F0} EGP";
}

public class EarningDelivery
{
    public int Id { get; set; }
    public decimal TotalAmount { get; set; }
    public decimal DeliveryFee { get; set; }
    public DateTime? DeliveredAt { get; set; }
    public string RestaurantName { get; set; } = string.Empty;

    public string EarningText => $"{DeliveryFee:F0} EGP";
    public string TimeText => DeliveredAt?.ToString("hh:mm tt") ?? "";
}

// ─── Notifications ───────────────────────────────────────────────────────────

public class Notification
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Body { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public bool IsRead { get; set; }
    public int? OrderId { get; set; }
    public DateTime CreatedAt { get; set; }

    public string TimeText
    {
        get
        {
            var d = DateTime.UtcNow - CreatedAt;
            if (d.TotalMinutes < 1) return "Just now";
            if (d.TotalHours < 1) return $"{(int)d.TotalMinutes} min ago";
            if (d.TotalDays < 1) return $"{(int)d.TotalHours} hr ago";
            return CreatedAt.ToString("MMM dd");
        }
    }

    public Color BackgroundColor => IsRead ? Colors.White : Color.FromArgb("#FFF3EF");
}

public class PagedResult<T>
{
    public int Total { get; set; }
    public int Page { get; set; }
    public int PageSize { get; set; }
    public int? TotalPages { get; set; }
    public List<T> Data { get; set; } = new();
}

// ─── Chat ────────────────────────────────────────────────────────────────────

public class ChatMessage
{
    // ✅ FIX #2 — السيرفر بيرجع "message" مش "text"
    // بدون JsonPropertyName، الـ deserializer مش بيملّي Text والشات بيظهر فاضي
    [System.Text.Json.Serialization.JsonPropertyName("message")]
    public string Text { get; set; } = string.Empty;
    public bool IsFromMe { get; set; }
    public bool IsFromOther => !IsFromMe;
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    public string TimeText => Timestamp.ToLocalTime().ToString("hh:mm tt");
}

// ─── Driver Order History ─────────────────────────────────────────────────────

public class DriverOrder
{
    public int Id { get; set; }
    public string Status { get; set; } = string.Empty;
    public decimal TotalAmount { get; set; }
    public decimal DeliveryFee { get; set; }
    public string DeliveryAddress { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public DateTime? DeliveredAt { get; set; }
    public string? RestaurantName { get; set; }

    public string TotalText => $"{TotalAmount:F0} EGP";
    public string EarningText => $"+{DeliveryFee:F0} EGP";
    public string DateText => CreatedAt.ToString("MMM dd, hh:mm tt");

    public Color StatusColor => Status switch
    {
        "Delivered" => Color.FromArgb("#4CAF50"),
        "Cancelled" or "Rejected" => Color.FromArgb("#F44336"),
        _ => Color.FromArgb("#FF9800")
    };
}