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
    public event Action<int, int>? VoiceCallAccepted; // orderId, byUserId

    public event Action<int, int>? VoiceCallRejected; // orderId, byUserId
    public event Action<int, int>? VoiceCallEnded;    // orderId, byUserId

    // ✅ FIX #CallGroup — بتتنادي بعد أي Reconnect تلقائي، عشان اللي مستخدم الـ Hub
    // (زي ActiveDeliveryViewModel) يقدر يرجع يعمل JoinOrderTracking تاني، لأن
    // الجروبات بتتفقد لما الـ ConnectionId يتغيّر بعد انقطاع/رجوع الاتصال.
    public event Action? Reconnected;

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
            Reconnected?.Invoke(); // ✅ FIX #CallGroup — نبّه المشتركين يرجعوا ينضموا لجروبات الطلبات
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



        _hub.On<JsonElement>("VoiceCallAccepted", el =>
        {
            var orderId = el.GetProperty("orderId").GetInt32();
            var byUserId = el.GetProperty("byUserId").GetInt32();
            MainThread.BeginInvokeOnMainThread(() => VoiceCallAccepted?.Invoke(orderId, byUserId));
        });

        _hub.On<JsonElement>("VoiceCallRejected", el =>
        {
            var orderId = el.GetProperty("orderId").GetInt32();
            var byUserId = el.GetProperty("byUserId").GetInt32();
            MainThread.BeginInvokeOnMainThread(() => VoiceCallRejected?.Invoke(orderId, byUserId));
        });

        _hub.On<JsonElement>("VoiceCallEnded", el =>
        {
            var orderId = el.GetProperty("orderId").GetInt32();
            var byUserId = el.GetProperty("byUserId").GetInt32();
            MainThread.BeginInvokeOnMainThread(() => VoiceCallEnded?.Invoke(orderId, byUserId));
        });

        _hub.On<JsonElement>("DriverAssigned", el =>
        {
            var orderId = el.GetProperty("orderId").GetInt32();
            MainThread.BeginInvokeOnMainThread(() => OrderStatusChanged?.Invoke(orderId, "AssignedToDriver"));
        });
        _hub.On<JsonElement>("NotificationReceived", el =>
        {
            var id = el.GetProperty("notificationId").GetInt32();
            var title = el.GetProperty("title").GetString() ?? "";
            var message = el.GetProperty("message").GetString() ?? "";
            MainThread.BeginInvokeOnMainThread(() => NotificationReceived?.Invoke(id, title, message));
        });
        try { await _hub.StartAsync(); }
        catch (Exception ex)
        { System.Diagnostics.Debug.WriteLine($"[SignalR] Connect failed: {ex.Message}"); }
    }
    public event Action<int, string, string>? NotificationReceived;
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

    public async Task AcceptVoiceCallAsync(int orderId)
    {
        if (IsConnected) await _hub!.InvokeAsync("AcceptVoiceCall", orderId);
    }



    public async Task RejectVoiceCallAsync(int orderId)
    {
        if (IsConnected) await _hub!.InvokeAsync("RejectVoiceCall", orderId);
    }

    public async Task EndVoiceCallAsync(int orderId)
    {
        if (IsConnected) await _hub!.InvokeAsync("EndVoiceCall", orderId);
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