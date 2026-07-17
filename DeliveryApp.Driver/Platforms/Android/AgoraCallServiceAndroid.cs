using Android.App;
using IO.Agora.Rtc2;
using DeliveryApp.Driver.Services.Call;
using Application = Android.App.Application;

namespace DeliveryApp.Driver.Platforms.Android
{
    public class AgoraCallServiceAndroid : IAgoraCallService
    {
        RtcEngine? _engine;

        public event Action<Exception>? CallError;
        public event Action? RemoteUserJoined;
        public event Action? RemoteUserLeft;

        public Task JoinChannelAsync(string appId, string token, string channelName, uint uid)
        {
            try
            {
                _engine = RtcEngine.Create(
                    Application.Context,
                    appId,
                    new RtcEventHandler(this));

                _engine.EnableAudio();
                _engine.DisableVideo();
                _engine.SetEnableSpeakerphone(true);

                _engine.JoinChannel(
                    string.IsNullOrEmpty(token) ? null : token,
                    channelName,
                    null,
                    (int)uid);
            }
            catch (Exception ex)
            {
                CallError?.Invoke(ex);
            }
            return Task.CompletedTask;
        }

        public void LeaveChannel()
        {
            try
            {
                _engine?.LeaveChannel();
                RtcEngine.Destroy();
                _engine = null;
            }
            catch (Exception ex) { CallError?.Invoke(ex); }
        }

        public void MuteLocalAudio(bool mute) => _engine?.MuteLocalAudioStream(mute);

        public void EnableSpeakerphone(bool enable) => _engine?.SetEnableSpeakerphone(enable);

        class RtcEventHandler : IRtcEngineEventHandler
        {
            readonly AgoraCallServiceAndroid _owner;
            public RtcEventHandler(AgoraCallServiceAndroid owner) => _owner = owner;

            public override void OnUserJoined(int uid, int elapsed) => _owner.RemoteUserJoined?.Invoke();
            public override void OnUserOffline(int uid, int reason) => _owner.RemoteUserLeft?.Invoke();
            public override void OnError(int err) => _owner.CallError?.Invoke(new Exception($"Agora error code: {err}"));
        }
    }
}
