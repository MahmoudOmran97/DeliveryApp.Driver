using Android.App;
using IO.Agora.Rtc2;
using DeliveryApp.Driver.Services.Call;
using Application = Android.App.Application;

namespace DeliveryApp.Driver.Platforms.Android
{
    public class AgoraCallServiceAndroid : IAgoraCallService
    {
        RtcEngine? _engine;
        bool _speakerEnabled = true;

        public event Action<Exception>? CallError;
        public event Action? RemoteUserJoined;
        public event Action? RemoteUserLeft;
        public event Action? LocalUserJoined;

        public Task JoinChannelAsync(string appId, string token, string channelName, uint uid)
        {
            try
            {
                // لو في engine قديم (مكالمة سابقة)، نمسحه قبل ما نعمل واحد جديد
                CleanupEngine();

                var engine = RtcEngine.Create(
                    Application.Context,
                    appId,
                    new RtcEventHandler(this));
                _engine = engine;

                // Agora 4.x الافتراضي = LIVE_BROADCASTING + Audience
                // Audience مش بينشر صوت → الطرفين ينضموا ومفيش صوت خالص.
                // COMMUNICATION + Broadcaster = مكالمة 1-1 طبيعية (نشر + استقبال).
                engine.SetChannelProfile(Constants.ChannelProfileCommunication);
                engine.SetClientRole(Constants.ClientRoleBroadcaster);
                engine.EnableAudio();
                engine.DisableVideo();

                // توجيه الصوت للسماعة الخارجية قبل الـ join (API الثابت في 4.x)
                engine.SetDefaultAudioRoutetoSpeakerphone(_speakerEnabled);

                // ChannelProfile / ClientRoleType على Options read-only في الـ binding،
                // فبنظبطهم من على الـ engine فوق، وهنا بنفعّل نشر/استقبال المايك صراحة.
                var options = new ChannelMediaOptions
                {
                    PublishMicrophoneTrack = Java.Lang.Boolean.True,
                    AutoSubscribeAudio = Java.Lang.Boolean.True,
                    PublishCameraTrack = Java.Lang.Boolean.False,
                    AutoSubscribeVideo = Java.Lang.Boolean.False
                };

                var result = engine.JoinChannel(
                    string.IsNullOrEmpty(token) ? null : token,
                    channelName,
                    (int)uid,
                    options);

                if (result != 0)
                    CallError?.Invoke(new Exception($"Agora JoinChannel failed with code: {result}"));
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
            }
            catch (Exception ex)
            {
                CallError?.Invoke(ex);
            }
            finally
            {
                CleanupEngine();
            }
        }

        public void MuteLocalAudio(bool mute) => _engine?.MuteLocalAudioStream(mute);

        public void EnableSpeakerphone(bool enable)
        {
            _speakerEnabled = enable;
            try
            {
                _engine?.SetDefaultAudioRoutetoSpeakerphone(enable);
                _engine?.SetEnableSpeakerphone(enable);
            }
            catch (Exception ex)
            {
                CallError?.Invoke(ex);
            }
        }

        void CleanupEngine()
        {
            try
            {
                if (_engine != null)
                {
                    RtcEngine.Destroy();
                    _engine = null;
                }
            }
            catch (Exception ex)
            {
                CallError?.Invoke(ex);
                _engine = null;
            }
        }

        void ApplySpeakerAfterJoin()
        {
            try
            {
                _engine?.SetEnableSpeakerphone(_speakerEnabled);
            }
            catch (Exception ex)
            {
                CallError?.Invoke(ex);
            }
        }

        class RtcEventHandler : IRtcEngineEventHandler
        {
            readonly AgoraCallServiceAndroid _owner;
            public RtcEventHandler(AgoraCallServiceAndroid owner) => _owner = owner;

            public override void OnJoinChannelSuccess(string? channel, int uid, int elapsed)
            {
                // بعد نجاح الانضمام نعيد تطبيق السماعة (قبل الـ join ممكن تتجاهل)
                _owner.ApplySpeakerAfterJoin();
                _owner.LocalUserJoined?.Invoke();
            }

            public override void OnUserJoined(int uid, int elapsed) => _owner.RemoteUserJoined?.Invoke();
            public override void OnUserOffline(int uid, int reason) => _owner.RemoteUserLeft?.Invoke();
            public override void OnError(int err) => _owner.CallError?.Invoke(new Exception($"Agora error code: {err}"));
        }
    }
}
