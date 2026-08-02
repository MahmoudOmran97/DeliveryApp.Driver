using Android.App;
using Android.Content;
using Android.Content.PM;
using Android.Media;
using Android.OS;
using Android.Views;
using Android.Widget;
using Color = Android.Graphics.Color;
using Log = Android.Util.Log;
using Orientation = Android.Widget.Orientation;

namespace DeliveryApp.Driver.Platforms.Android;

/// <summary>
/// شاشة مكالمة واردة فوق قفل الشاشة (زي واتساب/مسنجر).
/// </summary>
[Activity(
    Name = "com.companyname.deliveryapp.driver.IncomingCallActivity",
    Theme = "@android:style/Theme.DeviceDefault.NoActionBar.Fullscreen",
    ExcludeFromRecents = true,
    ShowWhenLocked = true,
    TurnScreenOn = true,
    LaunchMode = LaunchMode.SingleInstance,
    Exported = false,
    ScreenOrientation = ScreenOrientation.Portrait)]
public class IncomingCallActivity : Activity
{
    const string ProductionBaseUrl = "https://deliveryappapi.runasp.net/api";
    const string TokenPrefKey = "driver_token";

    int _orderId;
    string _callerName = "";
    Ringtone? _ringtone;
    Vibrator? _vibrator;

    protected override void OnCreate(Bundle? savedInstanceState)
    {
        base.OnCreate(savedInstanceState);
        EnableOverLockScreen();

        _orderId = Intent?.GetIntExtra("orderId", 0) ?? 0;
        _callerName = Intent?.GetStringExtra("callerName") ?? "العميل";
        if (_orderId == 0) { Finish(); return; }

        BuildUi();
        StartRinging();
    }

    void EnableOverLockScreen()
    {
        if (Build.VERSION.SdkInt >= BuildVersionCodes.OMr1)
        {
            SetShowWhenLocked(true);
            SetTurnScreenOn(true);
            var keyguard = (KeyguardManager?)GetSystemService(KeyguardService);
            keyguard?.RequestDismissKeyguard(this, null);
        }
        else
        {
#pragma warning disable CA1422
            Window?.AddFlags(WindowManagerFlags.ShowWhenLocked | WindowManagerFlags.TurnScreenOn | WindowManagerFlags.DismissKeyguard);
#pragma warning restore CA1422
        }
        Window?.AddFlags(WindowManagerFlags.KeepScreenOn);
    }

    void BuildUi()
    {
        var root = new LinearLayout(this)
        {
            Orientation = Orientation.Vertical,
            LayoutParameters = new LinearLayout.LayoutParams(
                ViewGroup.LayoutParams.MatchParent,
                ViewGroup.LayoutParams.MatchParent)
        };
        root.SetBackgroundColor(Color.ParseColor("#1B1B1F"));
        root.SetGravity(GravityFlags.CenterHorizontal);
        root.SetPadding(48, 120, 48, 80);

        var title = new TextView(this) { Text = "مكالمة واردة", TextSize = 18f };
        title.SetTextColor(Color.ParseColor("#B0B0B0"));
        title.Gravity = GravityFlags.Center;
        root.AddView(title, new LinearLayout.LayoutParams(
            ViewGroup.LayoutParams.MatchParent, ViewGroup.LayoutParams.WrapContent)
        { TopMargin = 40, BottomMargin = 24 });

        var avatar = new TextView(this) { Text = "📞", TextSize = 56f, Gravity = GravityFlags.Center };
        avatar.SetBackgroundColor(Color.ParseColor("#FF5722"));
        root.AddView(avatar, new LinearLayout.LayoutParams(240, 240)
        { Gravity = GravityFlags.CenterHorizontal, TopMargin = 40 });

        var name = new TextView(this) { Text = _callerName, TextSize = 28f };
        name.SetTextColor(Color.White);
        name.Gravity = GravityFlags.Center;
        root.AddView(name, new LinearLayout.LayoutParams(
            ViewGroup.LayoutParams.MatchParent, ViewGroup.LayoutParams.WrapContent)
        { TopMargin = 48, BottomMargin = 8 });

        var hint = new TextView(this) { Text = "بيكلمك دلوقتي...", TextSize = 16f };
        hint.SetTextColor(Color.ParseColor("#B0B0B0"));
        hint.Gravity = GravityFlags.Center;
        root.AddView(hint, new LinearLayout.LayoutParams(
            ViewGroup.LayoutParams.MatchParent, ViewGroup.LayoutParams.WrapContent)
        { BottomMargin = 80 });

        root.AddView(new Space(this), new LinearLayout.LayoutParams(
            ViewGroup.LayoutParams.MatchParent, 0, 1f));

        var buttons = new LinearLayout(this)
        {
            Orientation = Orientation.Horizontal,
            LayoutParameters = new LinearLayout.LayoutParams(
                ViewGroup.LayoutParams.MatchParent, ViewGroup.LayoutParams.WrapContent)
        };
        buttons.SetGravity(GravityFlags.Center);

        var reject = MakeActionButton("رفض", "#E53935");
        reject.Click += (_, _) => _ = RejectAsync();
        buttons.AddView(reject, new LinearLayout.LayoutParams(0, 140, 1f) { RightMargin = 24 });

        var accept = MakeActionButton("قبول", "#43A047");
        accept.Click += (_, _) => Accept();
        buttons.AddView(accept, new LinearLayout.LayoutParams(0, 140, 1f) { LeftMargin = 24 });

        root.AddView(buttons);
        SetContentView(root);
    }

    global::Android.Widget.Button MakeActionButton(string text, string colorHex)
    {
        var btn = new global::Android.Widget.Button(this) { Text = text, TextSize = 18f };
        btn.SetTextColor(Color.White);
        btn.SetBackgroundColor(Color.ParseColor(colorHex));
        return btn;
    }

    void StartRinging()
    {
        try
        {
            var uri = RingtoneManager.GetActualDefaultRingtoneUri(this, RingtoneType.Ringtone)
                      ?? RingtoneManager.GetDefaultUri(RingtoneType.Ringtone);
            if (uri != null)
            {
                _ringtone = RingtoneManager.GetRingtone(this, uri);
                if (_ringtone != null)
                {
                    if (Build.VERSION.SdkInt >= BuildVersionCodes.P)
                    {
                        _ringtone.AudioAttributes = new AudioAttributes.Builder()
                            .SetUsage(AudioUsageKind.NotificationRingtone)
                            .SetContentType(AudioContentType.Sonification)
                            .Build();
                        _ringtone.Looping = true;
                    }
                    _ringtone.Play();
                }
            }

            _vibrator = GetSystemService(VibratorService) as Vibrator;
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
        }
        catch (Exception ex) { Log.Error("IncomingCall", $"Ring failed: {ex.Message}"); }
    }

    void StopRinging()
    {
        try
        {
            if (_ringtone?.IsPlaying == true) _ringtone.Stop();
            _ringtone = null;
            _vibrator?.Cancel();
            _vibrator = null;
        }
        catch { /* ignore */ }
    }

    void Accept()
    {
        StopRinging();
        IncomingCallNotificationHelper.Cancel(this, _orderId);

        var launch = PackageManager?.GetLaunchIntentForPackage(PackageName ?? "");
        if (launch != null)
        {
            launch.SetFlags(ActivityFlags.NewTask | ActivityFlags.ClearTop | ActivityFlags.SingleTop);
            launch.PutExtra("tawseela_call_action", "accept");
            launch.PutExtra("tawseela_order_id", _orderId);
            launch.PutExtra("tawseela_caller_name", _callerName);
            StartActivity(launch);
        }
        Finish();
    }

    async Task RejectAsync()
    {
        StopRinging();
        IncomingCallNotificationHelper.Cancel(this, _orderId);
        try
        {
            var token = Microsoft.Maui.Storage.Preferences.Get(TokenPrefKey, string.Empty);
            if (!string.IsNullOrEmpty(token) && _orderId != 0)
            {
                using var http = new System.Net.Http.HttpClient { Timeout = TimeSpan.FromSeconds(10) };
                http.DefaultRequestHeaders.Authorization =
                    new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
                await http.PostAsync($"{ProductionBaseUrl}/voicecall/reject/{_orderId}", null);
            }
        }
        catch (Exception ex) { Log.Error("IncomingCall", $"Reject failed: {ex.Message}"); }
        Finish();
    }

    protected override void OnDestroy()
    {
        StopRinging();
        base.OnDestroy();
    }
}
