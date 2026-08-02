namespace DeliveryApp.Driver.Services;

// بيشيل بيانات المكالمة الواردة لحد ما الـ Shell/App يخلصوا يتظبطوا وقت الفتح من نوتيفيكيشن
// (cold start أو warm start من الخلفية) عشان نقدر ننتقل لصفحة المكالمة تلقائيًا.
public static class PendingCallNavigation
{
    public static int? OrderId;
    public static string? CallerName;
    public static bool AutoAccept;

    public static (int orderId, string callerName, bool autoAccept)? TakePending()
    {
        if (OrderId is null) return null;
        var result = (OrderId.Value, CallerName ?? "", AutoAccept);
        OrderId = null;
        CallerName = null;
        AutoAccept = false;
        return result;
    }
}
