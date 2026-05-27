namespace DeliveryApp.Driver.Services;

public class LocationService
{
    private System.Timers.Timer? _timer;
    private readonly ApiService _api;
    private int? _currentOrderId;
    private bool _isTracking;

    public event Action<double, double>? LocationUpdated;

    public LocationService(ApiService api)
    {
        _api = api;
    }

    public void StartTracking(int? orderId = null)
    {
        // ✅ FIX 2 — لو الـ timer اتعملها dispose (لما يرجع من background)
        // مش بس نتحقق من الـ flag، لازم نتحقق من الـ timer كمان
        if (_isTracking && _timer != null) return;

        _currentOrderId = orderId;
        _isTracking = true;

        // ✅ FIX 2 — نتأكد إن القديم اتنظّف قبل ما نعمل جديد
        _timer?.Stop();
        _timer?.Dispose();

        _timer = new System.Timers.Timer(8000);
        _timer.Elapsed += async (_, _) => await UpdateLocationAsync();
        _timer.Start();

        // أول update فوري
        _ = UpdateLocationAsync();
    }

    public void SetOrderId(int? orderId)
    {
        _currentOrderId = orderId;
    }

    public void StopTracking()
    {
        _isTracking = false;
        _timer?.Stop();
        _timer?.Dispose();
        _timer = null;
    }

    /// <summary>
    /// استدعيه من App.xaml.cs في OnSleep/OnResume
    /// عشان لما التطبيق يرجع من الـ background يعمل restart للـ timer
    /// </summary>
    public void OnAppResumed()
    {
        if (_isTracking)
        {
            // ✅ FIX 2 — إعادة تشغيل الـ tracking لما التطبيق يرجع
            _isTracking = false; // نفضي الـ flag عشان StartTracking ميرجعش
            StartTracking(_currentOrderId);
        }
    }

    public void OnAppSleeping()
    {
        // وقّف الـ timer بس خلّي الـ flag زي ما هي عشان OnAppResumed يعرف يشغله تاني
        _timer?.Stop();
        _timer?.Dispose();
        _timer = null;
        // _isTracking يفضل true عشان OnAppResumed يعرف يرجع يشتغل
    }

    private async Task UpdateLocationAsync()
    {
        try
        {
            var location =
                await Geolocation.GetLastKnownLocationAsync()
                ?? await Geolocation.GetLocationAsync(
                       new GeolocationRequest(GeolocationAccuracy.High, TimeSpan.FromSeconds(5)));

            if (location != null)
            {
                await _api.UpdateLocationAsync(location.Latitude, location.Longitude, _currentOrderId);
                MainThread.BeginInvokeOnMainThread(() =>
                    LocationUpdated?.Invoke(location.Latitude, location.Longitude));
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[Location] {ex.Message}");
        }
    }

    public async Task<(double lat, double lng)?> GetCurrentLocationAsync()
    {
        try
        {
            var loc = await Geolocation.GetLocationAsync(
                new GeolocationRequest(GeolocationAccuracy.Medium, TimeSpan.FromSeconds(10)));
            if (loc != null) return (loc.Latitude, loc.Longitude);
        }
        catch { }
        return null;
    }
}