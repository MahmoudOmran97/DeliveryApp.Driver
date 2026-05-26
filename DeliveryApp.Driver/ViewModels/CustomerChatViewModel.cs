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

    [ObservableProperty] private int _orderId;
    [ObservableProperty] private string _customerName = string.Empty;
    [ObservableProperty] private string _inputText = string.Empty;
    [ObservableProperty] private bool _isConnected;

    public ObservableCollection<ChatMessage> Messages { get; } = new();

    public CustomerChatViewModel(SignalRService signalR, AuthService auth)
    {
        _signalR = signalR;
        _auth = auth;
        _signalR.ChatMessageReceived += OnChatMessageReceived;
    }

    // BUG FIX #2: لما الـ OrderId يتغير نعمل connect ونضم group الطلب
    // بدون ده، الدرايفر مش في الـ group ومش بيستقبل رسائل العميل
    partial void OnOrderIdChanged(int value)
    {
        if (value > 0)
            _ = EnsureConnectedAsync();
    }

    private async Task EnsureConnectedAsync()
    {
        if (!_signalR.IsConnected)
            await _signalR.ConnectAsync(_auth.GetToken());

        // الانضمام لغرفة الطلب عشان يستقبل رسائل العميل
        await _signalR.JoinOrderAsync(OrderId);

        IsConnected = _signalR.IsConnected;
    }

    private void OnChatMessageReceived(int orderId, int senderId, string message)
    {
        if (orderId != OrderId) return;

        // BUG FIX #3: تجاهل الـ echo — السيرفر بيبعت الرسالة لكل الـ group بما فيه المرسل
        var myId = _auth.GetUserId();
        if (senderId == myId) return;

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

        var msg = new ChatMessage
        {
            Text = InputText,
            IsFromMe = true,
            Timestamp = DateTime.UtcNow
        };

        Messages.Add(msg);
        var text = InputText;
        InputText = string.Empty;

        await _signalR.SendChatMessageAsync(OrderId, text);
    }

    public void Cleanup()
    {
        _signalR.ChatMessageReceived -= OnChatMessageReceived;
    }
}