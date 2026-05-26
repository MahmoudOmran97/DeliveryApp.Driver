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
        if (_isTracking) return;
        _currentOrderId = orderId;
        _isTracking = true;

        _timer = new System.Timers.Timer(8000); // every 8 seconds
        _timer.Elapsed += async (_, _) => await UpdateLocationAsync();
        _timer.Start();

        // immediate first update
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

    private async Task UpdateLocationAsync()
    {
        try
        {
            var location = await Geolocation.GetLastKnownLocationAsync()
                ?? await Geolocation.GetLocationAsync(new GeolocationRequest(GeolocationAccuracy.High, TimeSpan.FromSeconds(5)));

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