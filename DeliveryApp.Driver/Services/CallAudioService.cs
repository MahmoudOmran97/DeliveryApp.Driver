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

        _pc.OnRtpPacketReceived += (rep, mediaType, rtpPacket) =>
        {
            if (mediaType != SDPMediaTypesEnum.audio || _decoder == null) return;
            try
            {
                var opusPayload = rtpPacket.Payload;
                var pcmShort = new short[FrameSamples * 4];
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
        try { _pc?.close(); } catch { }
        _pc = null;
        _encoder?.Dispose();
        _decoder = null;
    }
}
