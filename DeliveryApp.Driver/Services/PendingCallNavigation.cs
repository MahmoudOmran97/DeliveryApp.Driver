namespace DeliveryApp.Driver.Services;

// بيشيل بيانات المكالمة الواردة لحد ما الـ Shell/App يخلصوا يتظبطوا وقت الفتح من نوتيفيكيشن
// (Cold start) عشان نقدر ننتقل لصفحة المكالمة تلقائيًا.
public static class PendingCallNavigation
{
    public static int? OrderId;
    public static string? CallerName;

    public static (int orderId, string callerName)? TakePending()
    {
        if (OrderId is null) return null;
        var result = (OrderId.Value, CallerName ?? "");
        OrderId = null;
        CallerName = null;
        return result;
    }
}
