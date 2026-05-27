using Android.App;
using Android.Content;
using Android.Content.PM;
using Android.OS;

namespace DeliveryApp.Driver
{
    [Activity(
        Theme = "@style/Maui.SplashTheme",
        MainLauncher = true,
        // ✅ FIX 3 — SingleTask بدل SingleTop
        // SingleTop بيعمل مشكلة لما التطبيق يرجع من الـ background
        // SingleTask بيضمن instance واحدة وبيشتغل صح مع MAUI lifecycle
        LaunchMode = LaunchMode.SingleTask,
        ConfigurationChanges =
            ConfigChanges.ScreenSize |
            ConfigChanges.Orientation |
            ConfigChanges.UiMode |
            ConfigChanges.ScreenLayout |
            ConfigChanges.SmallestScreenSize |
            ConfigChanges.Density)]
    public class MainActivity : MauiAppCompatActivity
    {
        // ✅ FIX 3 — لما التطبيق يرجع من الـ background بـ Intent جديد
        protected override void OnNewIntent(Intent? intent)
        {
            base.OnNewIntent(intent);
            Intent = intent;
        }
    }
}