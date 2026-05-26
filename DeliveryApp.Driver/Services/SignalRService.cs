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

    public bool IsConnected => _hub?.State == HubConnectionState.Connected;

    public async Task ConnectAsync(string token)
    {
        if (IsConnected) return;

        _hub = new HubConnectionBuilder()
            .WithUrl(HubUrl, o => o.AccessTokenProvider = () => Task.FromResult<string?>(token))
            .WithAutomaticReconnect()
            .Build();

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

        // When driver is assigned to an order
        _hub.On<JsonElement>("DriverAssigned", el =>
        {
            var orderId = el.GetProperty("orderId").GetInt32();
            MainThread.BeginInvokeOnMainThread(() => OrderStatusChanged?.Invoke(orderId, "AssignedToDriver"));
        });

        try { await _hub.StartAsync(); }
        catch (Exception ex)
        { System.Diagnostics.Debug.WriteLine($"[SignalR] {ex.Message}"); }
    }

    public async Task SendChatMessageAsync(int orderId, string message)
    {
        if (IsConnected) await _hub!.InvokeAsync("SendChatMessage", orderId, message);
    }

    public async Task StartVoiceCallAsync(int orderId)
    {
        if (IsConnected) await _hub!.InvokeAsync("StartVoiceCall", orderId);
    }

    public async Task DisconnectAsync()
    {
        if (_hub != null)
        {
            await _hub.StopAsync();
            await _hub.DisposeAsync();
            _hub = null;
        }
    }
}