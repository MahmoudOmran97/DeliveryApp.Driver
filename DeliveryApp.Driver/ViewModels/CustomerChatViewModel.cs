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

    [ObservableProperty] private int _orderId;
    [ObservableProperty] private string _customerName = string.Empty;
    [ObservableProperty] private string _inputText = string.Empty;

    public ObservableCollection<ChatMessage> Messages { get; } = new();

    public CustomerChatViewModel(SignalRService signalR)
    {
        _signalR = signalR;
        _signalR.ChatMessageReceived += OnChatMessageReceived;
    }

    private void OnChatMessageReceived(int orderId, int senderId, string message)
    {
        if (orderId == OrderId)
        {
            Messages.Add(new ChatMessage
            {
                Text = message,
                IsFromMe = false,
                Timestamp = DateTime.UtcNow
            });
        }
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
}
