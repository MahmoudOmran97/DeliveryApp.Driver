using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DeliveryApp.Driver.Models;
using DeliveryApp.Driver.Services;
using System.Collections.ObjectModel;

namespace DeliveryApp.Driver.ViewModels;

public partial class SupportChatViewModel : BaseViewModel
{
    readonly ApiService _api;
    readonly SignalRService _signalR;
    public ObservableCollection<SupportChatMessage> Messages { get; } = new();
    [ObservableProperty] string _inputText = string.Empty;
    [ObservableProperty] bool _isTyping;
    [ObservableProperty] bool _isEscalated;
    private bool _initialized;
    private int _sessionId;

    public SupportChatViewModel(ApiService api, SignalRService signalR)
    {
        _api = api; _signalR = signalR;
        _signalR.SupportMessageReceived += OnAdminMessageReceived;
    }

    public void InitIfNeeded() { if (_initialized) return; _initialized = true; _ = InitAsync(); }

    async Task InitAsync()
    {
        IsBusy = true;
        try
        {
            var session = await _api.GetOrCreateSupportSessionAsync();
            if (session == null) { Messages.Add(new SupportChatMessage { Text = "تعذر الاتصال بالدعم حالياً.", IsFromAi = true }); return; }
            _sessionId = session.Id; IsEscalated = session.Status == "Escalated";
            if (session.Messages.Count == 0) Messages.Add(new SupportChatMessage { Text = "أهلاً بك. أنا مساعد الدعم الذكي. اكتب مشكلتك وسأحاول مساعدتك، ولو احتجت موظف دعم هحوّلك له مباشرة.", IsFromAi = true });
            else foreach (var m in session.Messages) Messages.Add(new SupportChatMessage { Text = m.Message, IsFromAi = !m.IsMine, Time = m.CreatedAt });
        }
        finally { IsBusy = false; }
    }

    [RelayCommand]
    async Task Send()
    {
        var text = InputText.Trim();
        if (string.IsNullOrEmpty(text) || IsTyping || _sessionId == 0) return;
        InputText = string.Empty; Messages.Add(new SupportChatMessage { Text = text, IsFromAi = false }); IsTyping = true;
        try
        {
            var result = await _api.SendSupportMessageAsync(_sessionId, text);
            if (result == null) { Messages.Add(new SupportChatMessage { Text = "حصلت مشكلة في إرسال الرسالة. حاول تاني.", IsFromAi = true }); return; }
            if (result.Escalated) IsEscalated = true;
            if (result.AiReply != null) Messages.Add(new SupportChatMessage { Text = result.AiReply.Message, IsFromAi = true, Time = result.AiReply.CreatedAt });
        }
        catch { Messages.Add(new SupportChatMessage { Text = "تعذر الاتصال بالدعم. حاول بعد لحظات.", IsFromAi = true }); }
        finally { IsTyping = false; }
    }

    void OnAdminMessageReceived(int sessionId, string message)
    {
        if (sessionId != _sessionId) return;
        MainThread.BeginInvokeOnMainThread(() => { IsEscalated = true; Messages.Add(new SupportChatMessage { Text = message, IsFromAi = true }); });
    }

}
