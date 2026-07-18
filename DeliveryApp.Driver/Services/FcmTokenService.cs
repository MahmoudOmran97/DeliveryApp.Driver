#if ANDROID
using DeliveryApp.Driver;
#endif

namespace DeliveryApp.Driver.Services;

public class FcmTokenService
{
    private readonly ApiService _api;
    private readonly AuthService _auth;

    public FcmTokenService(ApiService api, AuthService auth)
    {
        _api = api;
        _auth = auth;
    }

    public async Task RegisterAsync()
    {
#if ANDROID || IOS || MACCATALYST
        try
        {
            System.Diagnostics.Debug.WriteLine("[FCM] RegisterAsync started...");

            await Plugin.Firebase.CloudMessaging.CrossFirebaseCloudMessaging.Current.CheckIfValidAsync();

            var token = await Plugin.Firebase.CloudMessaging.CrossFirebaseCloudMessaging.Current.GetTokenAsync();
            if (string.IsNullOrEmpty(token))
            {
                System.Diagnostics.Debug.WriteLine("[FCM] Token is empty!");
                return;
            }

            await _api.UpdateFcmTokenAsync(token);
            System.Diagnostics.Debug.WriteLine("[FCM] Token sent to backend");

            await Plugin.Firebase.CloudMessaging.CrossFirebaseCloudMessaging.Current.SubscribeToTopicAsync("all");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[FCM] RegisterAsync failed: {ex.Message}");
        }
#endif
    }

    public void ListenForTokenRefresh()
    {
#if ANDROID || IOS || MACCATALYST
        Plugin.Firebase.CloudMessaging.CrossFirebaseCloudMessaging.Current.TokenChanged += async (_, args) =>
        {
            if (!string.IsNullOrEmpty(args.Token))
                await _api.UpdateFcmTokenAsync(args.Token);
        };
#endif
    }

    public void ListenForMessages()
    {
#if ANDROID || IOS || MACCATALYST
        Plugin.Firebase.CloudMessaging.CrossFirebaseCloudMessaging.Current.NotificationReceived += (_, args) =>
        {
            var title = args.Notification?.Title ?? "New Notification";
            var body  = args.Notification?.Body  ?? "";
            var data  = args.Notification?.Data;

            // ✅ لو الـ push ده تنبيه مكالمة واردة، افتح شاشة المكالمة مباشرة (تغطية حالة
            // الأبليكيشن في الخلفية؛ حالة القفل التام بيتكفّل بيها MainActivity full-screen
            // notification تحت).
            if (data != null && data.TryGetValue("type", out var type) && type == "IncomingCall"
                && data.TryGetValue("orderId", out var orderIdStr)
                && int.TryParse(orderIdStr, out var orderId))
            {
                MainThread.BeginInvokeOnMainThread(async () =>
                {
                    try
                    {
                        await Shell.Current.GoToAsync(
                            $"CallPage?orderId={orderId}&otherPartyName={Uri.EscapeDataString("العميل")}&isIncoming=true");
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"[FCM] Navigate to CallPage failed: {ex.Message}");
                    }
                });
                return;
            }

            MainThread.BeginInvokeOnMainThread(() => ShowLocalNotification(title, body));
        };
#endif
    }

#if ANDROID
    private static int _notifId = 2000;

    private static void ShowLocalNotification(string title, string body)
    {
        try
        {
            var context = Android.App.Application.Context;

            var builder = new AndroidX.Core.App.NotificationCompat.Builder(context, "default")
                .SetContentTitle(title)
                .SetContentText(body)
                .SetPriority(AndroidX.Core.App.NotificationCompat.PriorityHigh)
                .SetDefaults(AndroidX.Core.App.NotificationCompat.DefaultAll)
                .SetAutoCancel(true)
                .SetStyle(new AndroidX.Core.App.NotificationCompat.BigTextStyle().BigText(body));

            NotificationHelper.ApplyBranding(builder, context);

            var intent = context.PackageManager?.GetLaunchIntentForPackage(context.PackageName ?? "");
            if (intent != null)
            {
                intent.SetFlags(Android.Content.ActivityFlags.SingleTop | Android.Content.ActivityFlags.ClearTop);
                var pending = Android.App.PendingIntent.GetActivity(
                    context, 0, intent,
                    Android.App.PendingIntentFlags.UpdateCurrent | Android.App.PendingIntentFlags.Immutable);
                builder.SetContentIntent(pending);
            }

            AndroidX.Core.App.NotificationManagerCompat.From(context).Notify(_notifId++, builder.Build());
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[FCM] ShowLocalNotification error: {ex.Message}");
        }
    }
#else
    private static void ShowLocalNotification(string title, string body)
    {
        System.Diagnostics.Debug.WriteLine($"[FCM] ShowLocalNotification (non-Android): {title}");
    }
#endif
}
