using Android.Graphics;
using AndroidX.Core.App;

namespace DeliveryApp.Driver;

internal static class NotificationHelper
{
    internal static void ApplyBranding(NotificationCompat.Builder builder, Android.Content.Context context)
    {
        builder.SetSmallIcon(Resource.Drawable.ic_notification);

        try
        {
            var logoId = context.Resources?.GetIdentifier("notification_logo", "drawable", context.PackageName) ?? 0;
            if (logoId != 0)
            {
                var bitmap = BitmapFactory.DecodeResource(context.Resources, logoId);
                if (bitmap != null)
                    builder.SetLargeIcon(bitmap);
            }
        }
        catch
        {
            // Large icon is optional
        }
    }
}
