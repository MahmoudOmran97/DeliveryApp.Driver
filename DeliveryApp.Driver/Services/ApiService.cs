using System.Net.Http.Json;
using System.Text.Json;
using DeliveryApp.Driver.Models;

namespace DeliveryApp.Driver.Services;

public class ApiService
{
    private readonly HttpClient _http;
    private readonly AuthService _auth;
    private readonly JsonSerializerOptions _json = new() { PropertyNameCaseInsensitive = true };

    private const string Base = "https://deliveryappapi.runasp.net/api";

    public ApiService(AuthService auth)
    {
        _auth = auth;
        _http = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
    }

    private void SetAuth()
    {
        var t = _auth.GetToken();
        if (!string.IsNullOrEmpty(t))
            _http.DefaultRequestHeaders.Authorization =
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", t);
    }

    public class ApiException : Exception
    {
        public ApiException(string message) : base(message) { }
    }

    private async Task<T?> GetAsync<T>(string path)
    {
        SetAuth();
        try
        {
            var r = await _http.GetAsync($"{Base}/{path}");
            if (r.IsSuccessStatusCode)
                return await r.Content.ReadFromJsonAsync<T>(_json);
        }
        catch (Exception ex) { Debug(ex, path); }
        return default;
    }

    private async Task<T?> PostAsync<T>(string path, object payload)
    {
        SetAuth();
        try
        {
            var r = await _http.PostAsJsonAsync($"{Base}/{path}", payload);
            if (r.IsSuccessStatusCode)
                return await r.Content.ReadFromJsonAsync<T>(_json);

            var errorBody = await r.Content.ReadAsStringAsync();
            try
            {
                var doc = JsonDocument.Parse(errorBody);
                if (doc.RootElement.TryGetProperty("message", out var msg))
                    throw new ApiException(msg.GetString()!);
            }
            catch (ApiException) { throw; }
            catch { }

            throw new ApiException($"Request failed ({(int)r.StatusCode})");
        }
        catch (ApiException) { throw; }
        catch (Exception ex) { Debug(ex, path); }
        return default;
    }

    private async Task<bool> PutAsync(string path, object? payload = null)
    {
        SetAuth();
        try
        {
            var r = payload != null
                ? await _http.PutAsJsonAsync($"{Base}/{path}", payload)
                : await _http.PutAsync($"{Base}/{path}", null);
            return r.IsSuccessStatusCode;
        }
        catch (Exception ex) { Debug(ex, path); }
        return false;
    }

    private async Task<T?> PutAsync<T>(string path, object? payload = null)
    {
        SetAuth();
        try
        {
            var r = payload != null
                ? await _http.PutAsJsonAsync($"{Base}/{path}", payload)
                : await _http.PutAsync($"{Base}/{path}", null);
            if (r.IsSuccessStatusCode)
                return await r.Content.ReadFromJsonAsync<T>(_json);
        }
        catch (Exception ex) { Debug(ex, path); }
        return default;
    }

    // ─── Auth ────────────────────────────────────────────────────────────────

    public Task<LoginResponse?> LoginAsync(string email, string password)
        => PostAsync<LoginResponse>("auth/login", new { Email = email, Password = password });

    public Task<LoginResponse?> RegisterAsync(string name, string email, string password, string phone)
        => PostAsync<LoginResponse>("auth/register", new
        {
            FullName = name,
            Email = email,
            Password = password,
            Phone = phone,
            Role = "Driver"
        });

    // ─── Driver ──────────────────────────────────────────────────────────────

    public Task<DriverProfile?> GetMyProfileAsync()
        => GetAsync<DriverProfile>("drivers/me");

    public async Task<bool> RegisterDriverProfileAsync(string vehicleType, string licensePlate, string? nationalId)
    {
        var result = await PostAsync<object>("drivers/register", new
        {
            VehicleType = vehicleType,
            LicensePlate = licensePlate,
            NationalId = nationalId
        });
        return result != null;
    }

    public async Task<(bool isOnline, string message)> ToggleOnlineAsync()
    {
        SetAuth();
        try
        {
            var r = await _http.PutAsync($"{Base}/drivers/toggle-online", null);
            if (r.IsSuccessStatusCode)
            {
                var result = await r.Content.ReadFromJsonAsync<JsonElement>(_json);
                var isOnline = result.GetProperty("isOnline").GetBoolean();
                var message = result.GetProperty("message").GetString() ?? "";
                return (isOnline, message);
            }

            var errorBody = await r.Content.ReadAsStringAsync();
            try
            {
                var doc = JsonDocument.Parse(errorBody);
                if (doc.RootElement.TryGetProperty("message", out var msg))
                    return (false, msg.GetString() ?? "Error");
            }
            catch { }
        }
        catch (Exception ex) { Debug(ex, "toggle-online"); }
        return (false, "Connection error");
    }

    public Task<bool> UpdateLocationAsync(double lat, double lng, int? orderId = null)
        => PutAsync("drivers/location", new
        {
            Latitude = lat,
            Longitude = lng,
            OrderId = orderId
        });

    public Task<EarningsResult?> GetEarningsAsync(string period = "today")
        => GetAsync<EarningsResult>($"drivers/earnings?period={period}");

    public Task<ActiveOrder?> GetActiveOrderAsync()
        => GetAsync<ActiveOrder>("drivers/orders/active");

    // ─── Orders ──────────────────────────────────────────────────────────────

    public Task<List<AvailableOrder>?> GetAvailableOrdersAsync()
        => GetAsync<List<AvailableOrder>>("orders/available");

    public async Task<bool> AssignOrderAsync(int orderId)
        => await PutAsync($"orders/{orderId}/assign-driver");

    public async Task<bool> UpdateOrderStatusAsync(int orderId, string status)
        => await PutAsync($"orders/{orderId}/status", new { Status = status });

    public Task<PagedResult<DriverOrder>?> GetMyOrdersAsync(int page = 1)
        => GetAsync<PagedResult<DriverOrder>>($"orders/my?page={page}");

    // ─── Notifications ───────────────────────────────────────────────────────

    public Task<PagedResult<Notification>?> GetNotificationsAsync(int page = 1)
        => GetAsync<PagedResult<Notification>>($"notifications?page={page}");

    public Task<bool> MarkNotificationReadAsync(int id) => PutAsync($"notifications/{id}/read");
    public Task<bool> MarkAllReadAsync() => PutAsync("notifications/read-all");

    private static void Debug(Exception ex, string path)
        => System.Diagnostics.Debug.WriteLine($"[API] {path}: {ex.Message}");
}