using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DeliveryApp.Driver.Models;

namespace DeliveryApp.Driver.ViewModels;

public partial class SupportChatViewModel : BaseViewModel
{
    [ObservableProperty] private string _inputText = string.Empty;

    public ObservableCollection<ChatMessage> Messages { get; } = new();

    public SupportChatViewModel()
    {
        // Add a welcome message
        Messages.Add(new ChatMessage
        {
            Text = "Hello! How can we help you today?",
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
        InputText = string.Empty;

        // Simulate support response
        await Task.Delay(1000);
        Messages.Add(new ChatMessage
        {
            Text = "Thank you for your message. A support representative will be with you shortly.",
            IsFromMe = false,
            Timestamp = DateTime.UtcNow
        });
    }
}
