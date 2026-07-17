namespace DeliveryApp.Driver.Services.Call;

/// <summary>
/// الـ interface المشترك بين Android و iOS.
/// الـ CallViewModel بيتكلم مع ده بس، ومش عارف حاجة عن Agora أو أي SDK نيتف.
/// كل منصة هتعمل implementation باسم AgoraCallService جوه Platforms/Android و Platforms/iOS،
/// وهما اللي فعليًا بيكلموا الـ Agora native engine.
/// </summary>
public interface ICallService
{
    /// <summary>لما تنضم للقناة فعليًا وتقدر تتكلم</summary>
    event Action? OnJoined;

    /// <summary>لما الطرف التاني (السائق/العميل) ينضم للمكالمة</summary>
    event Action? OnRemoteUserJoined;

    /// <summary>لما الطرف التاني يقفل أو يخرج</summary>
    event Action? OnRemoteUserLeft;

    /// <summary>أي خطأ (فشل اتصال، توكن منتهي، مشكلة شبكة...)</summary>
    event Action<string>? OnError;

    Task JoinAsync(string appId, string token, string channelName, uint uid);

    Task LeaveAsync();

    void SetMicMuted(bool muted);

    void SetSpeakerEnabled(bool enabled);

    bool IsJoined { get; }
}
