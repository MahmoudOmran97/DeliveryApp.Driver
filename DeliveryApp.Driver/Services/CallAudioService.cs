using Concentus.Structs;
using Concentus.Enums;
using DeliveryApp.Driver.Services.Call;
using Microsoft.Maui.ApplicationModel;
using SIPSorcery.Net;
using SIPSorceryMedia.Abstractions;
using System.Text.Json;

namespace DeliveryApp.Driver.Services;

public class CallAudioService
{
    const int SampleRate = 48000;
    const int Channels = 1;
    const int FrameSamples = 960;
    const int OpusPayloadType = 111;

    readonly SignalRService _signalR;
    readonly ApiService _api;
    readonly IPlatformAudioIO _audio;

    RTCPeerConnection? _pc;
    OpusEncoder? _encoder;
    OpusDecoder? _decoder;
    int _orderId;
    bool _closed;

    public event Action<Exception>? OnError;

    public CallAudioService(SignalRService signalR, ApiService api, IPlatformAudioIO audio)
    {
        _signalR = signalR;
        _api = api;
        _audio = audio;
    }

    public async Task<string> CreateOfferAsync(int orderId)
    {
        _orderId = orderId;
        await SetupPeerConnectionAsync();

        var offer = _pc!.createOffer();
        await _pc.setLocalDescription(offer);
        return offer.sdp;
    }

    public async Task HandleRemoteOfferAsync(int orderId, string sdp)
    {
        _orderId = orderId;
        await SetupPeerConnectionAsync();

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
            var init = JsonSerializer.Deserialize<RTCIceCandidateInit>(candidateJson);
            if (init != null) _pc?.addIceCandidate(init);
        }
        catch (Exception ex) { OnError?.Invoke(ex); }
        return Task.CompletedTask;
    }

    async Task SetupPeerConnectionAsync()
    {
        var iceServers = new List<RTCIceServer>();

        try
        {
            var servers = await _api.GetIceServersAsync();
            if (servers != null)
            {
                foreach (var s in servers)
                {
                    try
                    {
                        var json = JsonSerializer.Serialize(s);
                        var server = JsonSerializer.Deserialize<RTCIceServer>(json);
                        if (server != null && !string.IsNullOrWhiteSpace(server.urls))
                            iceServers.Add(server);
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"[Call] Skipped bad ice server entry: {ex.Message}");
                    }
                }
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[Call] Failed to fetch ICE servers: {ex.Message}");
        }

        if (iceServers.Count == 0)
        {
            iceServers.Add(new RTCIceServer { urls = "stun:stun.l.google.com:19302" });
            iceServers.Add(new RTCIceServer { urls = "stun:stun1.l.google.com:19302" });
        }

        try
        {
            var config = new RTCConfiguration { iceServers = iceServers };
            _pc = new RTCPeerConnection(config);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[Call] RTCPeerConnection creation failed, falling back to STUN only: {ex.Message}");
            var fallbackConfig = new RTCConfiguration
            {
                iceServers = new List<RTCIceServer>
            {
                new RTCIceServer { urls = "stun:stun.l.google.com:19302" }
            }
            };
            _pc = new RTCPeerConnection(fallbackConfig);
        }
        var audioTrack = new MediaStreamTrack(
            new List<AudioFormat> { new AudioFormat(AudioCodecsEnum.OPUS, OpusPayloadType, SampleRate, Channels, "useinbandfec=1") },
            MediaStreamStatusEnum.SendRecv);
        _pc.addTrack(audioTrack);

        _pc.onicecandidate += candidate =>
        {
            if (candidate == null) return;
            var json = JsonSerializer.Serialize(candidate);
            _ = _signalR.SendIceCandidateAsync(_orderId, json);
        };

        // ─── DIAGNOSTIC: تتبع حالة الـ ICE/Connection عشان نعرف هل فعلاً بيوصل connected ───
        _pc.oniceconnectionstatechange += state =>
            System.Diagnostics.Debug.WriteLine($"[Call][ICE] iceConnectionState -> {state}");
        _pc.onconnectionstatechange += state =>
            System.Diagnostics.Debug.WriteLine($"[Call][PC] connectionState -> {state}");

        int rtpAudioPacketsSeen = 0;
        int rtpDecodeOk = 0;
        int rtpDecodeFail = 0;
        var lastLog = DateTime.UtcNow;

        _pc.OnRtpPacketReceived += (rep, mediaType, rtpPacket) =>
        {
            if (mediaType != SDPMediaTypesEnum.audio)
            {
                System.Diagnostics.Debug.WriteLine($"[Call][RTP] Non-audio packet received, mediaType={mediaType}");
                return;
            }

            rtpAudioPacketsSeen++;

            if (_decoder == null)
            {
                System.Diagnostics.Debug.WriteLine("[Call][RTP] Audio packet arrived but decoder is null!");
                return;
            }

            try
            {
                var opusPayload = rtpPacket.Payload;
                var pcmShort = new short[FrameSamples * 4];
                int decoded = _decoder.Decode(opusPayload, 0, opusPayload.Length, pcmShort, 0, FrameSamples, false);
                if (decoded > 0)
                {
                    rtpDecodeOk++;
                    var frame = new short[decoded];
                    Array.Copy(pcmShort, frame, decoded);
                    _audio.PlayPcm(frame);
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine($"[Call][RTP] Decode returned {decoded} samples (payloadLen={opusPayload.Length})");
                }
            }
            catch (Exception ex)
            {
                rtpDecodeFail++;
                System.Diagnostics.Debug.WriteLine($"[Call][RTP] Decode EXCEPTION: {ex.GetType().Name}: {ex.Message}");
                OnError?.Invoke(ex);
            }

            // كل ثانية تقريبًا نطبع ملخص عشان مانغرقش اللوج بباكيت باكيت
            if ((DateTime.UtcNow - lastLog).TotalSeconds >= 1)
            {
                System.Diagnostics.Debug.WriteLine(
                    $"[Call][RTP] Last 1s summary: audioPacketsSeen={rtpAudioPacketsSeen}, decodeOk={rtpDecodeOk}, decodeFail={rtpDecodeFail}");
                rtpAudioPacketsSeen = 0;
                rtpDecodeOk = 0;
                rtpDecodeFail = 0;
                lastLog = DateTime.UtcNow;
            }
        };

        _encoder = new OpusEncoder(SampleRate, Channels, OpusApplication.OPUS_APPLICATION_VOIP);
        _decoder = new OpusDecoder(SampleRate, Channels);
    }

    int _sentFrames;
    DateTime _lastSendLog = DateTime.UtcNow;

    async Task StartMediaAsync()
    {
        try
        {
            var status = await Permissions.RequestAsync<Permissions.Microphone>();
            if (status != PermissionStatus.Granted)
            {
                System.Diagnostics.Debug.WriteLine("[Call][Media] Microphone permission NOT granted.");
                OnError?.Invoke(new Exception("Microphone permission was not granted."));
                return;
            }

            _audio.PcmCaptured += OnPcmCaptured;
            _audio.StartCapture();
            _audio.StartPlayback();
            System.Diagnostics.Debug.WriteLine("[Call][Media] StartCapture/StartPlayback called successfully.");
        }
        catch (Exception ex)
        {
            // ⚠️ ده أهم سطر log مضاف — لو الـ AudioRecord/AudioTrack فشلوا يفتحوا (مثلاً
            // تعارض مع تطبيق تاني بيستخدم المايك)، كان بيحصل throw هنا وميحصلش أي تسجيل
            // لأي حاجة، فتحس إن "مفيش صوت" من غير أي سبب ظاهر.
            System.Diagnostics.Debug.WriteLine($"[Call][Media] StartMediaAsync FAILED: {ex.GetType().Name}: {ex.Message}\n{ex.StackTrace}");
            OnError?.Invoke(ex);
        }
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
            _pc.SendAudio((uint)pcm.Length, payload);
            _sentFrames++;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[Call][Send] Encode/Send EXCEPTION: {ex.GetType().Name}: {ex.Message}");
            OnError?.Invoke(ex);
        }

        if ((DateTime.UtcNow - _lastSendLog).TotalSeconds >= 1)
        {
            System.Diagnostics.Debug.WriteLine($"[Call][Send] Last 1s summary: framesSent={_sentFrames}");
            _sentFrames = 0;
            _lastSendLog = DateTime.UtcNow;
        }
    }

    public void Hangup()
    {
        if (_closed) return;
        _closed = true;
        _audio.PcmCaptured -= OnPcmCaptured;
        _audio.StopCapture();
        _audio.StopPlayback();
        try { _pc?.close(); } catch { }
        _pc = null;
        _encoder?.Dispose();
        _decoder = null;
    }
}