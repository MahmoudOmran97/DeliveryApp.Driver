// ═══════════════════════════════════════════════════════════════
// DeliveryApp.Driver / Services / ChatNotificationService.cs
// ═══════════════════════════════════════════════════════════════
// ✅ FIX #4 — ملف جديد بالكامل
// يستمع لرسائل الشات من SignalR، ولو الدرايفر مش على شاشة الشات
// يوريله in-app alert ويسمحله يفتح المحادثة مباشرة
// ═══════════════════════════════════════════════════════════════
using DeliveryApp.Driver.Models;

namespace DeliveryApp.Driver.Services;

public class ChatNotificationService
{
    private readonly SignalRService _signalR;
    private readonly AuthService _auth;

    // orderId → customerName  (بيتبنى من ActiveDeliveryViewModel)
    private readonly Dictionary<int, string> _activeOrderCustomers = new();

    /// <summary>
    /// لو الدرايفر فاتح CustomerChatPage لطلب معين، نحط orderId هنا
    /// عشان نوقف الـ alert لطلب الدرايفر شايفه أصلاً
    /// </summary>
    public int? ActiveChatOrderId { get; set; }

    public event Action<int, string, string>? NewMessageFromCustomer; // orderId, customerName, preview

    public ChatNotificationService(SignalRService signalR, AuthService auth)
    {
        _signalR = signalR;
        _auth = auth;
        _signalR.ChatMessageReceived += OnChatMessageReceived;
    }

    /// <summary>
    /// سجّل orderId مع اسم العميل.
    /// استدعيه من ActiveDeliveryViewModel في OnOrderChanged.
    /// </summary>
    public void RegisterOrder(int orderId, string customerName)
        => _activeOrderCustomers[orderId] = customerName;

    public void UnregisterOrder(int orderId)
        => _activeOrderCustomers.Remove(orderId);

    private void OnChatMessageReceived(int orderId, int senderId, string message)
    {
        // لو الدرايفر نفسه بعت الرسالة (echo)، تجاهل
        if (senderId == _auth.GetUserId()) return;

        // لو الدرايفر فاتح صفحة الشات لنفس الطلب، تجاهل
        if (ActiveChatOrderId == orderId) return;

        var customerName = _activeOrderCustomers.TryGetValue(orderId, out var n) ? n : "العميل";
        var preview = message.Length > 40 ? message[..40] + "..." : message;

        // إطلاع أي observer (ممكن تربطه بـ badge عداد مثلاً)
        NewMessageFromCustomer?.Invoke(orderId, customerName, preview);

        MainThread.BeginInvokeOnMainThread(async () =>
            await ShowInAppAlertAsync(orderId, customerName, preview));
    }

    private static async Task ShowInAppAlertAsync(int orderId, string customerName, string preview)
    {
        try
        {
            var page = Shell.Current as Page ?? Application.Current?.MainPage;
            if (page == null) return;

            var go = await page.DisplayAlert(
                $"💬 رسالة من {customerName}",
                preview,
                "فتح المحادثة",
                "لاحقاً");

            if (go)
                await Shell.Current.GoToAsync(
                    $"CustomerChatPage?orderId={orderId}&customerName={Uri.EscapeDataString(customerName)}");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[ChatNotif] {ex.Message}");
        }
    }
}