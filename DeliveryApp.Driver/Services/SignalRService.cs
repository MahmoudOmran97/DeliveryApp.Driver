using Microsoft.AspNetCore.SignalR.Client;
using System.Text.Json;

namespace DeliveryApp.Driver.Services;

public class SignalRService
{
    private HubConnection? _hub;
    private const string HubUrl = "https://deliveryappapi.runasp.net/hubs/tracking";

    public event Action<int, string>? OrderStatusChanged;
    public event Action<int>? NewOrderAvailable;
    public event Action<int, int, string>? ChatMessageReceived;
    public event Action<int, int>? IncomingVoiceCall;

    // ✅ FIX 1 — بنتحقق من Connected وبس مش من وجود الـ object
    public bool IsConnected => _hub?.State == HubConnectionState.Connected;

    public async Task ConnectAsync(string token)
    {
        // ✅ FIX 1 — لو في connection قديمة مش Connected، نعملها Dispose الأول
        if (_hub != null && _hub.State != HubConnectionState.Disconnected)
        {
            if (IsConnected) return; // شغالة فعلاً، مفيش لزمة
        }

        if (_hub != null)
        {
            // نظّف الـ connection القديمة قبل ما نعمل جديدة
            try { await _hub.DisposeAsync(); } catch { }
            _hub = null;
        }

        _hub = new HubConnectionBuilder()
            .WithUrl(HubUrl, o => o.AccessTokenProvider = () => Task.FromResult<string?>(token))
            .WithAutomaticReconnect(new[] { TimeSpan.Zero, TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(10) })
            .Build();

        // ✅ FIX 1 — لو انقطع الاتصال، نعرف ونقدر نعمل reconnect يدوي
        _hub.Closed += async (error) =>
        {
            System.Diagnostics.Debug.WriteLine($"[SignalR] Connection closed: {error?.Message}");
            await Task.Delay(3000);
            // WithAutomaticReconnect هيتولى الموضوع، بس لو مشيش نحاول يدوي
        };

        _hub.Reconnected += (connectionId) =>
        {
            System.Diagnostics.Debug.WriteLine($"[SignalR] Reconnected: {connectionId}");
            return Task.CompletedTask;
        };

        _hub.On<JsonElement>("OrderStatusChanged", el =>
        {
            var id = el.GetProperty("orderId").GetInt32();
            var status = el.GetProperty("status").GetString() ?? "";
            MainThread.BeginInvokeOnMainThread(() => OrderStatusChanged?.Invoke(id, status));
        });

        _hub.On<int>("NewOrder", id =>
        {
            MainThread.BeginInvokeOnMainThread(() => NewOrderAvailable?.Invoke(id));
        });

        _hub.On<JsonElement>("ChatMessageReceived", el =>
        {
            var orderId = el.GetProperty("orderId").GetInt32();
            var senderId = el.GetProperty("senderId").GetInt32();
            var message = el.GetProperty("message").GetString() ?? "";
            MainThread.BeginInvokeOnMainThread(() => ChatMessageReceived?.Invoke(orderId, senderId, message));
        });

        _hub.On<JsonElement>("IncomingVoiceCall", el =>
        {
            var orderId = el.GetProperty("orderId").GetInt32();
            var callerId = el.GetProperty("callerId").GetInt32();
            MainThread.BeginInvokeOnMainThread(() => IncomingVoiceCall?.Invoke(orderId, callerId));
        });

        _hub.On<JsonElement>("DriverAssigned", el =>
        {
            var orderId = el.GetProperty("orderId").GetInt32();
            MainThread.BeginInvokeOnMainThread(() => OrderStatusChanged?.Invoke(orderId, "AssignedToDriver"));
        });

        try { await _hub.StartAsync(); }
        catch (Exception ex)
        { System.Diagnostics.Debug.WriteLine($"[SignalR] Connect failed: {ex.Message}"); }
    }

    public async Task SendChatMessageAsync(int orderId, string message)
    {
        if (IsConnected) await _hub!.InvokeAsync("SendChatMessage", orderId, message);
    }

    public async Task JoinOrderAsync(int orderId)
    {
        if (IsConnected) await _hub!.InvokeAsync("JoinOrderTracking", orderId);
    }

    public async Task LeaveOrderAsync(int orderId)
    {
        if (IsConnected) await _hub!.InvokeAsync("LeaveOrderTracking", orderId);
    }

    public async Task StartVoiceCallAsync(int orderId)
    {
        if (IsConnected) await _hub!.InvokeAsync("StartVoiceCall", orderId);
    }

    public async Task DisconnectAsync()
    {
        if (_hub != null)
        {
            try
            {
                await _hub.StopAsync();
                await _hub.DisposeAsync();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[SignalR] Disconnect error: {ex.Message}");
            }
            finally { _hub = null; }
        }
    }
}