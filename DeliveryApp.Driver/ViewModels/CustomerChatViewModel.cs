// ═══════════════════════════════════════════════════════════════
// DeliveryApp.Driver / ViewModels / CustomerChatViewModel.cs
// ═══════════════════════════════════════════════════════════════
using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DeliveryApp.Driver.Models;
using DeliveryApp.Driver.Services;

namespace DeliveryApp.Driver.ViewModels;

[QueryProperty(nameof(OrderId), "orderId")]
[QueryProperty(nameof(CustomerName), "customerName")]
public partial class CustomerChatViewModel : BaseViewModel
{
    private readonly SignalRService _signalR;
    private readonly AuthService _auth;
    private readonly ApiService _api;
    private readonly ChatNotificationService _chatNotif; // ✅ FIX #4

    [ObservableProperty] private int _orderId;
    [ObservableProperty] private string _customerName = string.Empty;
    [ObservableProperty] private string _inputText = string.Empty;
    [ObservableProperty] private bool _isConnected;

    public ObservableCollection<ChatMessage> Messages { get; } = new();

    public CustomerChatViewModel(
        SignalRService signalR,
        AuthService auth,
        ApiService api,
        ChatNotificationService chatNotif) // ✅ FIX #4 — inject
    {
        _signalR = signalR;
        _auth = auth;
        _api = api;
        _chatNotif = chatNotif;
        _signalR.ChatMessageReceived += OnChatMessageReceived;
    }

    partial void OnOrderIdChanged(int value)
    {
        if (value <= 0) return;

        // ✅ FIX #4 — لما الدرايفر يفتح الشات، وقّف الـ in-app notification
        _chatNotif.ActiveChatOrderId = value;
        _ = EnsureConnectedAsync();
    }

    private async Task EnsureConnectedAsync()
    {
        if (!_signalR.IsConnected)
            await _signalR.ConnectAsync(_auth.GetToken());

        // الانضمام لغرفة الطلب عشان يستقبل رسائل العميل
        await _signalR.JoinOrderAsync(OrderId);

        IsConnected = _signalR.IsConnected;

        // تحميل الرسائل القديمة من API
        await LoadHistoryAsync();
    }

    private async Task LoadHistoryAsync()
    {
        try
        {
            var history = await _api.GetChatMessagesAsync(OrderId);
            if (history == null) return;

            Messages.Clear();
            foreach (var msg in history)
                Messages.Add(msg);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[ChatHistory] {ex.Message}");
        }
    }

    private void OnChatMessageReceived(int orderId, int senderId, string message)
    {
        if (orderId != OrderId) return;

        // تجاهل الـ echo — السيرفر بيبعت الرسالة لكل الـ group بما فيه المرسل
        if (senderId == _auth.GetUserId()) return;

        Messages.Add(new ChatMessage
        {
            Text = message,
            IsFromMe = false,
            Timestamp = DateTime.UtcNow
        });
    }

    [RelayCommand]
    private async Task Send()
    {
        if (string.IsNullOrWhiteSpace(InputText)) return;

        var text = InputText;
        InputText = string.Empty;

        Messages.Add(new ChatMessage
        {
            Text = text,
            IsFromMe = true,
            Timestamp = DateTime.UtcNow
        });

        await _signalR.SendChatMessageAsync(OrderId, text);
    }

    public void Cleanup()
    {
        // ✅ FIX #4 — لما الدرايفر يخرج من الشات، اسمح للـ notification تشتغل تاني
        _chatNotif.ActiveChatOrderId = null;
        _signalR.ChatMessageReceived -= OnChatMessageReceived;
    }
}