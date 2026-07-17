// ═══════════════════════════════════════════════════════════════
// DeliveryApp.Driver / ViewModels / CallViewModel.cs
// شاشة موحّدة لحالات المكالمة: بترن (Incoming) / بتتصل (Outgoing) / متصلة (Connected)
// ═══════════════════════════════════════════════════════════════
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DeliveryApp.Driver.Services;
using DeliveryApp.Driver.Services.Call;
//using Xamarin.KotlinX.Coroutines.Sync;

namespace DeliveryApp.Driver.ViewModels;

public enum CallState { Ringing, Calling, Connected, Ended }

[QueryProperty(nameof(OrderId), "orderId")]
[QueryProperty(nameof(OtherPartyName), "otherPartyName")]
[QueryProperty(nameof(IsIncomingRaw), "isIncoming")]
public partial class CallViewModel : BaseViewModel, IDisposable
{
    readonly SignalRService _signalR;
    readonly ApiService _api;
    readonly IAgoraCallService _agora;
    System.Timers.Timer? _durationTimer;
    int _secondsElapsed;
    bool _navigatedAway;

    [ObservableProperty] int _orderId;
    [ObservableProperty] string _otherPartyName = "";
    [ObservableProperty] CallState _state;
    [ObservableProperty] string _statusText = "";
    [ObservableProperty] string _durationText = "00:00";
    [ObservableProperty] bool _isMuted;
    [ObservableProperty] bool _isSpeakerOn = true;

    public string IsIncomingRaw
    {
        set => State = value == "true" ? CallState.Ringing : CallState.Calling;
    }

    public bool IsRinging => State == CallState.Ringing;
    public bool IsCalling => State == CallState.Calling;
    public bool IsConnected => State == CallState.Connected;

    public CallViewModel(SignalRService signalR, ApiService api, IAgoraCallService agora)
    {
        _signalR = signalR;
        _api = api;
        _agora = agora;

        _signalR.VoiceCallAccepted += OnAccepted;
        _signalR.VoiceCallRejected += OnRejected;
        _signalR.VoiceCallEnded += OnEnded;

        _agora.RemoteUserJoined += OnRemoteJoined;
        _agora.RemoteUserLeft += OnRemoteLeft;
        _agora.CallError += ex => System.Diagnostics.Debug.WriteLine($"[Call] Agora error: {ex.Message}");
    }

    partial void OnStateChanged(CallState value)
    {
        StatusText = value switch
        {
            CallState.Ringing => "مكالمة واردة...",
            CallState.Calling => "جارِ الاتصال...",
            CallState.Connected => "متصل",
            CallState.Ended => "انتهت المكالمة",
            _ => ""
        };
        OnPropertyChanged(nameof(IsRinging));
        OnPropertyChanged(nameof(IsCalling));
        OnPropertyChanged(nameof(IsConnected));

        if (value == CallState.Calling)
            _ = JoinAgoraChannelAsync();
    }

    [RelayCommand]
    async Task Accept()
    {
        await _signalR.AcceptVoiceCallAsync(OrderId);
        State = CallState.Connected;
        StartTimer();
        await JoinAgoraChannelAsync();
    }

    [RelayCommand]
    async Task Reject()
    {
        await _signalR.RejectVoiceCallAsync(OrderId);
        await CloseAsync();
    }

    [RelayCommand]
    async Task Cancel()
    {
        await _signalR.EndVoiceCallAsync(OrderId);
        await CloseAsync();
    }

    [RelayCommand]
    async Task EndCall()
    {
        await _signalR.EndVoiceCallAsync(OrderId);
        await CloseAsync();
    }

    [RelayCommand]
    void ToggleMute()
    {
        IsMuted = !IsMuted;
        _agora.MuteLocalAudio(IsMuted);
    }

    [RelayCommand]
    void ToggleSpeaker()
    {
        IsSpeakerOn = !IsSpeakerOn;
        _agora.EnableSpeakerphone(IsSpeakerOn);
    }

    async Task JoinAgoraChannelAsync()
    {
        try
        {
            // اسم القناة = رقم الأوردر، ثابت بين الطرفين
            var channelName = $"order-{OrderId}";
            var tokenResult = await _api.GetAgoraTokenAsync(channelName);
            if (tokenResult == null)
            {
                await AlertAsync("تعذّر بدء المكالمة، حاول تاني");
                await CloseAsync();
                return;
            }

            await _agora.JoinChannelAsync(tokenResult.AppId, tokenResult.Token, tokenResult.ChannelName, tokenResult.Uid);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[Call] JoinAgoraChannel failed: {ex.Message}");
        }
    }

    void OnAccepted(int orderId, int byUserId)
    {
        if (orderId != OrderId || State != CallState.Calling) return;
        MainThread.BeginInvokeOnMainThread(() =>
        {
            State = CallState.Connected;
            StartTimer();
        });
    }

    void OnRejected(int orderId, int byUserId)
    {
        if (orderId != OrderId) return;
        MainThread.BeginInvokeOnMainThread(async () =>
        {
            await AlertAsync("الطرف التاني رفض المكالمة");
            await CloseAsync();
        });
    }

    void OnEnded(int orderId, int byUserId)
    {
        if (orderId != OrderId) return;
        MainThread.BeginInvokeOnMainThread(async () => await CloseAsync());
    }

    void OnRemoteJoined() { /* ممكن تستخدمها لو حابب تأكيد إضافي إن الصوت اتوصل فعليًا */ }

    void OnRemoteLeft()
    {
        MainThread.BeginInvokeOnMainThread(async () => await CloseAsync());
    }

    void StartTimer()
    {
        _secondsElapsed = 0;
        _durationTimer = new System.Timers.Timer(1000);
        _durationTimer.Elapsed += (_, _) =>
        {
            _secondsElapsed++;
            var ts = TimeSpan.FromSeconds(_secondsElapsed);
            MainThread.BeginInvokeOnMainThread(() => DurationText = ts.ToString(@"mm\:ss"));
        };
        _durationTimer.Start();
    }

    async Task CloseAsync()
    {
        if (_navigatedAway) return;
        _navigatedAway = true;
        Dispose();
        await Shell.Current.GoToAsync("..");
    }

    public void Dispose()
    {
        _durationTimer?.Stop();
        _durationTimer?.Dispose();
        _agora.LeaveChannel();
        _signalR.VoiceCallAccepted -= OnAccepted;
        _signalR.VoiceCallRejected -= OnRejected;
        _signalR.VoiceCallEnded -= OnEnded;
        _agora.RemoteUserJoined -= OnRemoteJoined;
        _agora.RemoteUserLeft -= OnRemoteLeft;
    }
}
