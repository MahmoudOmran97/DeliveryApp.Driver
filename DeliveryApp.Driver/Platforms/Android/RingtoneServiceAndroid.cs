using Android.Content;
using Android.Media;
using Android.OS;
using DeliveryApp.Driver.Services.Call;
using Application = Android.App.Application;
using Android.Media;

namespace DeliveryApp.Driver.Platforms.Android;

// بيشغّل رنة المكالمة الافتراضية بتاعة الجهاز + اهتزاز متكرر لحد ما المستخدم يرد أو يرفض
// أو المكالمة تتلغي. بيوقف نفسه لو اتنسي مفتوح (Timeout حماية).
public class RingtoneServiceAndroid : IRingtoneService
{
    Ringtone? _ringtone;
    Vibrator? _vibrator;
    System.Threading.Timer? _safetyTimeout;

    public void Start()
    {
        try
        {
            Stop(); // تأكيد إنه مفيش رنة شغالة قبلها

            var context = Application.Context;

            var uri = RingtoneManager.GetActualDefaultRingtoneUri(context, RingtoneType.Ringtone)
             ?? RingtoneManager.GetDefaultUri(RingtoneType.Ringtone);

            if (uri != null)
            {
                _ringtone = RingtoneManager.GetRingtone(context, uri);
                if (_ringtone != null)
                {
                    if (Build.VERSION.SdkInt >= BuildVersionCodes.P)
                        _ringtone.AudioAttributes = new AudioAttributes.Builder()
                            .SetUsage(AudioUsageKind.NotificationRingtone)
                            .SetContentType(AudioContentType.Sonification)
                            .Build();
                    _ringtone.Looping = true; // متاحة من API 28+، لو أقدم هتتكرر يدويًا تحت
                    _ringtone.Play();
                }
            }

            _vibrator = context.GetSystemService(Context.VibratorService) as Vibrator;
            if (_vibrator?.HasVibrator == true)
            {
                var pattern = new long[] { 0, 800, 500 };
                if (Build.VERSION.SdkInt >= BuildVersionCodes.O)
                    _vibrator.Vibrate(VibrationEffect.CreateWaveform(pattern, 0));
                else
#pragma warning disable CA1422
                    _vibrator.Vibrate(pattern, 0);
#pragma warning restore CA1422
            }

            // حماية: لو حد نسي يقفلها، توقف لوحدها بعد دقيقة
            _safetyTimeout = new System.Threading.Timer(_ => Stop(), null, TimeSpan.FromSeconds(60), Timeout.InfiniteTimeSpan);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[Ringtone] Start failed: {ex.Message}");
        }
    }

    public void Stop()
    {
        try
        {
            _safetyTimeout?.Dispose();
            _safetyTimeout = null;

            if (_ringtone?.IsPlaying == true)
                _ringtone.Stop();
            _ringtone = null;

            _vibrator?.Cancel();
            _vibrator = null;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[Ringtone] Stop failed: {ex.Message}");
        }
    }
}
