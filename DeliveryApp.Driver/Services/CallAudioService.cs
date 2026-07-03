using Concentus.Structs;
using Concentus.Enums;
using DeliveryApp.Driver.Services.Call;
using Microsoft.Maui.ApplicationModel;
using SIPSorcery.Net;
using SIPSorceryMedia.Abstractions;

namespace DeliveryApp.Driver.Services;

/// <summary>
/// محرّك المكالمة نفسه: WebRTC (SIPSorcery) + ترميز الصوت (Opus عن طريق Concentus)،
/// مربوط بمايك/سماعة المنصة عن طريق IPlatformAudioIO.
///
/// ⚠️ مهم: الكود ده اتكتب على أساس توثيق SIPSorcery الرسمي (createOffer/setLocalDescription/
/// setRemoteDescription/addIceCandidate/SendAudio/OnRtpPacketReceived)، لكن *مقدرتش أعمل build
/// أو تجربة حقيقية* في البيئة دي (مفيش dotnet SDK ولا جهاز أندرويد/iOS متاح). لو حصل خطأ Compile
/// بسيط في اسم method/overload، قارنه بمثال SIPSorcery الرسمي هنا:
/// https://github.com/sipsorcery-org/sipsorcery/blob/master/examples/WebRTCExamples/WebRTCReceiveAudio/Program.cs
/// </summary>
public class CallAudioService
{
    const int SampleRate = 48000;
    const int Channels = 1;
    const int FrameSamples = 960; // 20ms @ 48kHz
    const int OpusPayloadType = 111; // dynamic payload type متعارف عليه لـ Opus في WebRTC

    readonly SignalRService _signalR;
    readonly IPlatformAudioIO _audio;

    RTCPeerConnection? _pc;
    OpusEncoder? _encoder;
    OpusDecoder? _decoder;
    int _orderId;
    bool _closed;

    public event Action<Exception>? OnError;

    public CallAudioService(SignalRService signalR, IPlatformAudioIO audio)
    {
        _signalR = signalR;
        _audio = audio;
    }

    public async Task<string> CreateOfferAsync(int orderId)
    {
        _orderId = orderId;
        SetupPeerConnection();

        var offer = _pc!.createOffer();
        await _pc.setLocalDescription(offer);
        return offer.sdp;
    }

    public async Task HandleRemoteOfferAsync(int orderId, string sdp)
    {
        _orderId = orderId;
        SetupPeerConnection();

        var result = _pc!.setRemoteDescription(new RTCSessionDescriptionInit { type = RTCSdpType.offer, sdp = sdp });
        if (result != SetDescriptionResultEnum.OK)
        {
            OnError?.Invoke(new Exception($"setRemoteDescription(offer) failed: {result}"));
            return;
        }

        var answer = _pc.createAnswer();
        await _pc.setLocalDescription(answer);
        await _signalR.SendCallAnswerAsync(orderId, answer.sdp);

        await StartMediaAsync();
    }

    public Task HandleRemoteAnswerAsync(string sdp)
    {
        var result = _pc!.setRemoteDescription(new RTCSessionDescriptionInit { type = RTCSdpType.answer, sdp = sdp });
        if (result != SetDescriptionResultEnum.OK)
        {
            OnError?.Invoke(new Exception($"setRemoteDescription(answer) failed: {result}"));
            return Task.CompletedTask;
        }
        return StartMediaAsync();
    }

    public Task AddRemoteIceCandidateAsync(string candidateJson)
    {
        try
        {
            var init = System.Text.Json.JsonSerializer.Deserialize<RTCIceCandidateInit>(candidateJson);
            if (init != null) _pc?.addIceCandidate(init);
        }
        catch (Exception ex) { OnError?.Invoke(ex); }
        return Task.CompletedTask;
    }

    void SetupPeerConnection()
    {
        var config = new RTCConfiguration
        {
            iceServers = new List<RTCIceServer>
            {
                new RTCIceServer { urls = "stun:stun.l.google.com:19302" },
                new RTCIceServer { urls = "stun:stun1.l.google.com:19302" }
                // ⚠️ ملحوظة: STUN بس من غير TURN ممكن يفشل على شبكات معينة (NAT صارم/شركات).
                // لو المكالمات فشلت بس الاتنين على النت، محتاجين TURN server (زي coturn ببلاش لو
                // استضفته بنفسك، أو خدمة زي Metered.ca عندها free tier صغير).
            }
        };

        _pc = new RTCPeerConnection(config);

        // ⚠️ لازم AudioFormat مخصص لـ Opus (dynamic payload) — راجعه مع توثيق SIPSorcery وقت
        // الـ build لو الـ constructor overload مختلف شوية.
        var audioTrack = new MediaStreamTrack(
            new List<AudioFormat> { new AudioFormat(AudioCodecsEnum.OPUS, OpusPayloadType, SampleRate, Channels, "useinbandfec=1") },
            MediaStreamStatusEnum.SendRecv);
        _pc.addTrack(audioTrack);

        _pc.onicecandidate += candidate =>
        {
            if (candidate == null) return;
            var json = System.Text.Json.JsonSerializer.Serialize(candidate);
            _ = _signalR.SendIceCandidateAsync(_orderId, json);
        };

        _pc.OnRtpPacketReceived += (rep, mediaType, rtpPacket) =>
        {
            if (mediaType != SDPMediaTypesEnum.audio || _decoder == null) return;
            try
            {
                var opusPayload = rtpPacket.Payload;
                var pcmShort = new short[FrameSamples * 4]; // مساحة كافية، Concentus بيرجع العدد الفعلي
                int decoded = _decoder.Decode(opusPayload, 0, opusPayload.Length, pcmShort, 0, FrameSamples, false);
                if (decoded > 0)
                {
                    var frame = new short[decoded];
                    Array.Copy(pcmShort, frame, decoded);
                    _audio.PlayPcm(frame);
                }
            }
            catch (Exception ex) { OnError?.Invoke(ex); }
        };

        _encoder = new OpusEncoder(SampleRate, Channels, OpusApplication.OPUS_APPLICATION_VOIP);
        _decoder = new OpusDecoder(SampleRate, Channels);
    }

    async Task StartMediaAsync()
    {
        var status = await Permissions.RequestAsync<Permissions.Microphone>();
        if (status != PermissionStatus.Granted)
        {
            OnError?.Invoke(new Exception("Microphone permission was not granted."));
            return;
        }

        _audio.PcmCaptured += OnPcmCaptured;
        _audio.StartCapture();
        _audio.StartPlayback();
    }

    void OnPcmCaptured(short[] pcm)
    {
        if (_encoder == null || _pc == null || _closed) return;
        try
        {
            var opusBuf = new byte[4000];
            int len = _encoder.Encode(pcm, 0, pcm.Length, opusBuf, 0, opusBuf.Length);
            var payload = new byte[len];
            Array.Copy(opusBuf, payload, len);

            // SendAudio موروثة من RTPSession — بتاخد مدة الفريم بوحدات الـ RTP clock (48000) + الـ payload
            _pc.SendAudio((uint)pcm.Length, payload);
        }
        catch (Exception ex) { OnError?.Invoke(ex); }
    }

    public void Hangup()
    {
        if (_closed) return;
        _closed = true;
        _audio.PcmCaptured -= OnPcmCaptured;
        _audio.StopCapture();
        _audio.StopPlayback();
        try { _pc?.close(); } catch { /* ignore */ }
        _pc = null;
        _encoder?.Dispose();
        _decoder = null;
    }
}
