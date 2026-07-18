using Android.App;
using Android.Content;
using Android.Media;
using Android.OS;
using AndroidX.Core.App;

namespace DeliveryApp.Driver.Platforms.Android;

internal static class IncomingCallNotificationHelper
{
    public const string CallChannelId = "incoming_calls";
    public const string ActionAccept = "com.companyname.deliveryapp.driver.ACTION_ACCEPT_CALL";
    public const string ActionReject = "com.companyname.deliveryapp.driver.ACTION_REJECT_CALL";

    static void EnsureChannel(Context context)
    {
        if (Build.VERSION.SdkInt < BuildVersionCodes.O) return;

        var manager = (NotificationManager?)context.GetSystemService(Context.NotificationService);
        if (manager?.GetNotificationChannel(CallChannelId) != null) return;

        var channel = new NotificationChannel(CallChannelId, "مكالمات واردة", NotificationImportance.High)
        {
            Description = "تنبيهات المكالمات الصوتية داخل التطبيق",
            LockscreenVisibility = NotificationVisibility.Public
        };
        channel.EnableVibration(true);
        channel.EnableLights(true);

        var ringtoneUri = RingtoneManager.GetActualDefaultRingtoneUri(context, RingtoneType.Ringtone);
        if (ringtoneUri != null)
        {
            var attrs = new AudioAttributes.Builder()
                .SetUsage(AudioUsageKind.NotificationRingtone)
                .SetContentType(AudioContentType.Sonification)
                .Build();
            channel.SetSound(ringtoneUri, attrs);
        }

        manager?.CreateNotificationChannel(channel);
    }

    public static int NotificationIdFor(int orderId) => 90000 + (orderId % 10000);

    public static void Show(Context context, int orderId, string callerName)
    {
        try
        {
            EnsureChannel(context);
            var notifId = NotificationIdFor(orderId);

            var acceptIntent = new Intent(ActionAccept).SetPackage(context.PackageName);
            acceptIntent.PutExtra("orderId", orderId);
            acceptIntent.PutExtra("callerName", callerName);
            acceptIntent.PutExtra("notificationId", notifId);
            var acceptPending = PendingIntent.GetBroadcast(context, notifId * 10 + 1, acceptIntent,
                PendingIntentFlags.UpdateCurrent | PendingIntentFlags.Immutable);

            var rejectIntent = new Intent(ActionReject).SetPackage(context.PackageName);
            rejectIntent.PutExtra("orderId", orderId);
            rejectIntent.PutExtra("notificationId", notifId);
            var rejectPending = PendingIntent.GetBroadcast(context, notifId * 10 + 2, rejectIntent,
                PendingIntentFlags.UpdateCurrent | PendingIntentFlags.Immutable);

            PendingIntent? fullScreenPending = null;
            var launch = context.PackageManager?.GetLaunchIntentForPackage(context.PackageName ?? "");
            if (launch != null)
            {
                launch.SetFlags(ActivityFlags.NewTask | ActivityFlags.ClearTop);
                launch.PutExtra("tawseela_call_action", "accept");
                launch.PutExtra("tawseela_order_id", orderId);
                launch.PutExtra("tawseela_caller_name", callerName);
                fullScreenPending = PendingIntent.GetActivity(context, notifId, launch,
                    PendingIntentFlags.UpdateCurrent | PendingIntentFlags.Immutable);
            }

            var builder = new NotificationCompat.Builder(context, CallChannelId)
                .SetSmallIcon(Resource.Drawable.ic_notification)
                .SetContentTitle("مكالمة واردة")
                .SetContentText($"{callerName} بيكلمك دلوقتي")
                .SetPriority(NotificationCompat.PriorityMax)
                .SetCategory(NotificationCompat.CategoryCall)
                .SetAutoCancel(true)
                .SetOngoing(true)
                .AddAction(new NotificationCompat.Action(Resource.Drawable.ic_call_accept, "قبول", acceptPending))
                .AddAction(new NotificationCompat.Action(Resource.Drawable.ic_call_reject, "رفض", rejectPending));

            if (fullScreenPending != null)
            {
                builder.SetFullScreenIntent(fullScreenPending, true);
                builder.SetContentIntent(fullScreenPending);
            }

            NotificationHelper.ApplyBranding(builder, context);
            NotificationManagerCompat.From(context).Notify(notifId, builder.Build());
        }
        catch (Exception ex)
        {
            global::Android.Util.Log.Error("IncomingCall", $"Show failed: {ex.Message}");
        }
    }

    public static void Cancel(Context context, int orderId)
    {
        try { NotificationManagerCompat.From(context).Cancel(NotificationIdFor(orderId)); }
        catch { /* ignore */ }
    }
}
