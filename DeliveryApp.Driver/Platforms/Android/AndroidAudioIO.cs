using global::Android.Content;
using global::Android.Media;
using DeliveryApp.Driver.Services.Call;

namespace DeliveryApp.Driver.Platforms.Android;

/// <summary>
/// التقاط الصوت من المايك وتشغيله على السماعة على أندرويد، باستخدام Android.Media.AudioRecord/
/// AudioTrack مباشرة (Java APIs متاحة native في .NET Android، مفيش حاجة إضافية لازم تتحمّل).
///
/// ⚠️ السبب اللي كان بيخلي "مفيش صوت": AudioAttributes بـ Usage=VoiceCommunication كان بيخلي
/// أندرويد يوجّه الصوت تلقائيًا للسمّاعة الصغيرة (earpiece) زي مكالمة تليفون عادية، مش السبيكر.
/// المستخدم هنا مش حاطط الموبايل على ودنه، فكان حاسس إن مفيش صوت خالص. ضفنا كمان طلب
/// Audio Focus وتفعيل AudioManager.Mode = ModeInCommunication، لأن من غيرهم أجهزة كتير بتشغّل
/// الصوت بمستوى واطي جدًا أو متقطع حتى لو الـ AudioTrack شغّال فعليًا.
/// </summary>
public class AndroidAudioIO : IPlatformAudioIO
{
    public int SampleRate => 48000;
    public int FrameSizeSamples => 960;
    public event Action<short[]>? PcmCaptured;
    AudioRecord? _recorder;
    AudioTrack? _player;
    CancellationTokenSource? _captureCts;
    Task? _captureTask;
    AudioManager? _audioManager;
    AudioFocusRequestClass? _focusRequest;
    int _priorMode = (int)Mode.Normal;

    AudioManager? GetAudioManager()
    {
        if (_audioManager != null) return _audioManager;
        var ctx = global::Android.App.Application.Context;
        _audioManager = ctx.GetSystemService(Context.AudioService) as AudioManager;
        return _audioManager;
    }

    void ConfigureAudioForCall()
    {
        var am = GetAudioManager();
        if (am == null) return;

        _priorMode = (int)am.Mode;

        if (OperatingSystem.IsAndroidVersionAtLeast(26))
        {
            var attrs = new AudioAttributes.Builder()!
                .SetUsage(AudioUsageKind.VoiceCommunication)!
                .SetContentType(AudioContentType.Speech)!
                .Build()!;
            _focusRequest = new AudioFocusRequestClass.Builder(AudioFocus.Gain)!
                .SetAudioAttributes(attrs)!
                .Build()!;
            am.RequestAudioFocus(_focusRequest);
        }
        else
        {
#pragma warning disable CA1422
            am.RequestAudioFocus(null, global::Android.Media.Stream.VoiceCall, AudioFocus.Gain);
#pragma warning restore CA1422
        }

        am.Mode = Mode.InCommunication;
        am.SpeakerphoneOn = true;
    }

    void RestoreAudio()
    {
        var am = GetAudioManager();
        if (am == null) return;
        try
        {
            am.SpeakerphoneOn = false;
            am.Mode = (Mode)_priorMode;
            if (OperatingSystem.IsAndroidVersionAtLeast(26) && _focusRequest != null)
                am.AbandonAudioFocusRequest(_focusRequest);
            else
#pragma warning disable CA1422
                am.AbandonAudioFocus(null);
#pragma warning restore CA1422
        }
        catch { }
        _focusRequest = null;
    }

    public void StartCapture()
    {
        if (_recorder != null) return;
        ConfigureAudioForCall();
        int minBufBytes = AudioRecord.GetMinBufferSize(
            SampleRate, ChannelIn.Mono, Encoding.Pcm16bit);
        int bufBytes = Math.Max(minBufBytes, FrameSizeSamples * 2 * 4);
        _recorder = new AudioRecord(
            AudioSource.VoiceCommunication,
            SampleRate,
            ChannelIn.Mono,
            Encoding.Pcm16bit,
            bufBytes);
        _recorder.StartRecording();
        _captureCts = new CancellationTokenSource();
        var token = _captureCts.Token;
        _captureTask = Task.Run(() =>
        {
            var buffer = new short[FrameSizeSamples];
            while (!token.IsCancellationRequested && _recorder != null)
            {
                int read = _recorder.Read(buffer, 0, buffer.Length);
                if (read > 0)
                {
                    var frame = read == buffer.Length ? buffer : buffer[..read];
                    PcmCaptured?.Invoke(frame);
                }
            }
        }, token);
    }

    public void StopCapture()
    {
        _captureCts?.Cancel();
        try { _recorder?.Stop(); } catch { }
        _recorder?.Release();
        _recorder = null;
        _captureCts = null;
        _captureTask = null;
        if (_player == null) RestoreAudio();
    }

    public void StartPlayback()
    {
        if (_player != null) return;
        if (_recorder == null) ConfigureAudioForCall();
        int minBufBytes = AudioTrack.GetMinBufferSize(
            SampleRate, ChannelOut.Mono, Encoding.Pcm16bit);
        int bufBytes = Math.Max(minBufBytes, FrameSizeSamples * 2 * 4);
        _player = new AudioTrack(
            new AudioAttributes.Builder()!
                .SetUsage(AudioUsageKind.VoiceCommunication)!
                .SetContentType(AudioContentType.Speech)!
                .Build()!,
            new AudioFormat.Builder()!
                .SetSampleRate(SampleRate)!
                .SetChannelMask(ChannelOut.Mono)!
                .SetEncoding(Encoding.Pcm16bit)!
                .Build()!,
            bufBytes,
            AudioTrackMode.Stream,
            AudioManager.AudioSessionIdGenerate);
        _player.Play();
    }

    public void StopPlayback()
    {
        try { _player?.Stop(); } catch { }
        _player?.Release();
        _player = null;
        if (_recorder == null) RestoreAudio();
    }

    public void PlayPcm(short[] pcm)
    {
        _player?.Write(pcm, 0, pcm.Length);
    }
}