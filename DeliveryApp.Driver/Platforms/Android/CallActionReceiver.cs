using Android.App;
using Android.Content;
using AndroidX.Core.App;
using System.Net.Http;

namespace DeliveryApp.Driver.Platforms.Android;

[BroadcastReceiver(Enabled = true, Exported = false, Label = "Tawseela Driver Call Actions")]
[IntentFilter(new[] { IncomingCallNotificationHelper.ActionAccept, IncomingCallNotificationHelper.ActionReject })]
public class CallActionReceiver : BroadcastReceiver
{
    const string ProductionBaseUrl = "https://deliveryappapi.runasp.net/api";
    const string TokenPrefKey = "driver_token"; // لازم يتطابق مع AuthService.K_Token بتاع الدرايفر

    public override void OnReceive(Context? context, Intent? intent)
    {
        if (context == null || intent == null) return;

        var orderId = intent.GetIntExtra("orderId", 0);
        var notifId = intent.GetIntExtra("notificationId", 0);

        if (notifId != 0)
            NotificationManagerCompat.From(context).Cancel(notifId);

        if (intent.Action == IncomingCallNotificationHelper.ActionAccept)
        {
            var callerName = intent.GetStringExtra("callerName") ?? "";
            var launch = context.PackageManager?.GetLaunchIntentForPackage(context.PackageName ?? "");
            if (launch != null)
            {
                launch.SetFlags(ActivityFlags.NewTask | ActivityFlags.ClearTop);
                launch.PutExtra("tawseela_call_action", "accept");
                launch.PutExtra("tawseela_order_id", orderId);
                launch.PutExtra("tawseela_caller_name", callerName);
                context.StartActivity(launch);
            }
        }
        else if (intent.Action == IncomingCallNotificationHelper.ActionReject)
        {
            var pending = GoAsync();
            _ = Task.Run(async () =>
            {
                try { await RejectCallRestAsync(orderId); }
                catch (Exception ex) { global::Android.Util.Log.Error("CallActionReceiver", $"Reject failed: {ex.Message}"); }
                finally { pending.Finish(); }
            });
        }
    }

    static async Task RejectCallRestAsync(int orderId)
    {
        if (orderId == 0) return;

        var token = Microsoft.Maui.Storage.Preferences.Get(TokenPrefKey, string.Empty);
        if (string.IsNullOrEmpty(token)) return;

        using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };
        http.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
        await http.PostAsync($"{ProductionBaseUrl}/voicecall/reject/{orderId}", null);
    }
}
