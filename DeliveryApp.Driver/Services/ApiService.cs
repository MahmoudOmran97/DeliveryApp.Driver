using System.Net.Http.Json;
using System.Text.Json;
using DeliveryApp.Driver.Models;

namespace DeliveryApp.Driver.Services;

public class ApiService
{
    public const string ApiBaseUrlPreferenceKey = "api_base_url";
    private const string ProductionBaseUrl = "https://deliveryappapi.runasp.net/api";
    private readonly HttpClient _http;
    private readonly AuthService _auth;
    private readonly JsonSerializerOptions _json = new() { PropertyNameCaseInsensitive = true };
    private string _baseUrl;

    public ApiService(AuthService auth)
    {
        _auth = auth;
        _http = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
        _baseUrl = ResolveBaseUrl();
    }

    public string GetBaseUrl() => _baseUrl;

    public string GetHubUrl()
    {
        var root = _baseUrl.EndsWith("/api", StringComparison.OrdinalIgnoreCase)
            ? _baseUrl[..^4]
            : _baseUrl.TrimEnd('/');
        return $"{root}/hubs/tracking";
    }

    public void SetBaseUrl(string baseUrl)
    {
        var normalized = NormalizeBaseUrl(baseUrl);
        if (string.IsNullOrWhiteSpace(normalized))
            return;

        Preferences.Set(ApiBaseUrlPreferenceKey, normalized);
        _baseUrl = normalized;
    }

    private static string ResolveBaseUrl()
    {
        var preferred = Preferences.Get(ApiBaseUrlPreferenceKey, string.Empty);
        if (!string.IsNullOrWhiteSpace(preferred))
            return NormalizeBaseUrl(preferred);

        var env = Environment.GetEnvironmentVariable("DELIVERY_API_BASE_URL");
        if (!string.IsNullOrWhiteSpace(env))
            return NormalizeBaseUrl(env);

        return ProductionBaseUrl;
    }

    private static string NormalizeBaseUrl(string? value)
    {
        var raw = (value ?? string.Empty).Trim().TrimEnd('/');
        if (string.IsNullOrWhiteSpace(raw))
            return string.Empty;

        if (!raw.EndsWith("/api", StringComparison.OrdinalIgnoreCase))
            raw += "/api";

        return raw;
    }

    private void SetAuth()
    {
        _http.DefaultRequestHeaders.Authorization = null;
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
            var r = await _http.GetAsync($"{_baseUrl}/{path}");
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
            var r = await _http.PostAsJsonAsync($"{_baseUrl}/{path}", payload);
            if (r.StatusCode == System.Net.HttpStatusCode.NotFound &&
                path.StartsWith("auth/", StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(_baseUrl, ProductionBaseUrl, StringComparison.OrdinalIgnoreCase))
            {
                // Safety fallback in case an old dev URL was persisted.
                _baseUrl = ProductionBaseUrl;
                Preferences.Set(ApiBaseUrlPreferenceKey, _baseUrl);
                r = await _http.PostAsJsonAsync($"{_baseUrl}/{path}", payload);
            }

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
                ? await _http.PutAsJsonAsync($"{_baseUrl}/{path}", payload)
                : await _http.PutAsync($"{_baseUrl}/{path}", null);
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
                ? await _http.PutAsJsonAsync($"{_baseUrl}/{path}", payload)
                : await _http.PutAsync($"{_baseUrl}/{path}", null);
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
            var r = await _http.PutAsync($"{_baseUrl}/drivers/toggle-online", null);
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

    public async Task<ActiveOrder?> GetOrderDetailsForDriverAsync(int orderId)
    {
        SetAuth();
        try
        {
            var response = await _http.GetAsync($"{_baseUrl}/orders/{orderId}");
            if (!response.IsSuccessStatusCode)
                return null;

            using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            var root = json.RootElement;

            var result = new ActiveOrder
            {
                Id = root.TryGetProperty("id", out var id) ? id.GetInt32() : orderId,
                Status = root.TryGetProperty("status", out var status) ? status.GetString() ?? string.Empty : string.Empty,
                TotalAmount = root.TryGetProperty("totalAmount", out var total) ? total.GetDecimal() : 0,
                DeliveryFee = root.TryGetProperty("deliveryFee", out var fee) ? fee.GetDecimal() : 0,
                DeliveryAddress = root.TryGetProperty("deliveryAddress", out var address) ? address.GetString() ?? string.Empty : string.Empty,
                DeliveryLatitude = root.TryGetProperty("deliveryLatitude", out var lat) ? lat.GetDouble() : 0,
                DeliveryLongitude = root.TryGetProperty("deliveryLongitude", out var lng) ? lng.GetDouble() : 0,
                DeliveryNotes = root.TryGetProperty("deliveryNotes", out var notes) ? notes.GetString() : null,
                CustomerName = root.TryGetProperty("customerName", out var customerName) ? customerName.GetString() ?? string.Empty : string.Empty
            };

            if (root.TryGetProperty("restaurant", out var restaurant))
            {
                result.RestaurantName = restaurant.TryGetProperty("name", out var rName) ? rName.GetString() ?? string.Empty : string.Empty;
                result.RestaurantLat = restaurant.TryGetProperty("latitude", out var rLat) ? rLat.GetDouble() : 0;
                result.RestaurantLng = restaurant.TryGetProperty("longitude", out var rLng) ? rLng.GetDouble() : 0;
            }

            if (root.TryGetProperty("items", out var items) && items.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in items.EnumerateArray())
                {
                    result.Items.Add(new ActiveOrderItem
                    {
                        ProductName = item.TryGetProperty("productName", out var productName) ? productName.GetString() ?? string.Empty : string.Empty,
                        Quantity = item.TryGetProperty("quantity", out var quantity) ? quantity.GetInt32() : 0,
                        Notes = item.TryGetProperty("notes", out var itemNotes) ? itemNotes.GetString() : null
                    });
                }
            }

            return result;
        }
        catch (Exception ex) { Debug(ex, $"orders/{orderId}"); }
        return null;
    }

    public async Task<bool> StartVoiceCallAsync(int orderId)
    {
        // This is a placeholder for the actual voice call initiation via SignalR or a dedicated endpoint
        // For now, we'll assume it's handled via the SignalR service we updated.
        return true;
    }

    // ─── Orders ──────────────────────────────────────────────────────────────

    public Task<List<AvailableOrder>?> GetAvailableOrdersAsync()
        => GetAsync<List<AvailableOrder>>("orders/available");

    public async Task<bool> AssignOrderAsync(int orderId)
        => await PutAsync($"orders/{orderId}/assign-driver");

    public async Task<bool> UpdateOrderStatusAsync(int orderId, string status)
        => await PutAsync($"orders/{orderId}/status", new { Status = status });

    public async Task<PagedResult<DriverOrder>?> GetMyOrdersAsync(int page = 1)
    {
        var driverOrders = await GetAsync<PagedResult<DriverOrder>>($"orders/driver/my?page={page}&pageSize=50");
        if (driverOrders?.Data?.Count > 0)
            return driverOrders;

        // Backward-compatible fallback for older APIs.
        return await GetAsync<PagedResult<DriverOrder>>($"orders/my?page={page}&pageSize=50");
    }

    // ─── Notifications ───────────────────────────────────────────────────────

    public Task<PagedResult<Notification>?> GetNotificationsAsync(int page = 1)
        => GetAsync<PagedResult<Notification>>($"notifications?page={page}");

    public Task<bool> MarkNotificationReadAsync(int id) => PutAsync($"notifications/{id}/read");
    public Task<bool> MarkAllReadAsync() => PutAsync("notifications/read-all");

    // ─── Chat ────────────────────────────────────────────────────────────────

    public Task<List<ChatMessage>?> GetChatMessagesAsync(int orderId)
        => GetAsync<List<ChatMessage>>($"chatmessages/{orderId}");

    private static void Debug(Exception ex, string path)
        => System.Diagnostics.Debug.WriteLine($"[API] {path}: {ex.Message}");


    public Task<List<object>?> GetIceServersAsync()
        => GetAsync<List<object>>("webrtc/ice-servers");
    public Task<AgoraTokenResult?> GetAgoraTokenAsync(string channelName, uint uid = 0)
    => GetAsync<AgoraTokenResult>($"agora/token?channelName={channelName}&uid={uid}");

    // ─── FCM Token ────────────────────────────────────────────────────────────
    public async Task UpdateFcmTokenAsync(string token)
    {
        try
        {
            SetAuth();
            if (string.IsNullOrEmpty(_auth.GetToken()))
            {
                System.Diagnostics.Debug.WriteLine("[API] user/fcm-token skipped — not logged in");
                return;
            }

            var body = System.Net.Http.Json.JsonContent.Create(new { token });
            var response = await _http.PutAsync($"{_baseUrl}/user/fcm-token", body);

            if (response.IsSuccessStatusCode)
                System.Diagnostics.Debug.WriteLine("[API] FCM token saved to backend");
            else
                System.Diagnostics.Debug.WriteLine($"[API] user/fcm-token failed: {(int)response.StatusCode} {response.ReasonPhrase}");
        }
        catch (Exception ex) { Debug(ex, "user/fcm-token"); }
    }

    // ✅ بيستخدمها زرار "رفض" الأحمر في نوتيفيكيشن المكالمة الواردة لما التطبيق يكون
    // مقفول تمامًا (مفيش SignalR متصل)، بيبعت الرفض مباشرة عن طريق REST بدل الـ Hub.
    public async Task<bool> RejectVoiceCallRestAsync(int orderId)
    {
        try
        {
            SetAuth();
            var response = await _http.PostAsync($"{_baseUrl}/voicecall/reject/{orderId}", null);
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex) { Debug(ex, "voicecall/reject"); return false; }
    }

    // وضيف الكلاس ده فى أي مكان مناسب (مثلاً جوه Models/AgoraTokenResult.cs ملف جديد)

   
}
public class AgoraTokenResult
{
    public string AppId { get; set; } = string.Empty;
    public string ChannelName { get; set; } = string.Empty;
    public uint Uid { get; set; }
    public string Token { get; set; } = string.Empty;
    public int ExpiresInSeconds { get; set; }
}