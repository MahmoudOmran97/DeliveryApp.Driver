using global::Android.Media;
using DeliveryApp.Driver.Services.Call;
namespace DeliveryApp.Driver.Platforms.Android;

public class AndroidAudioIO : IPlatformAudioIO
{
    public int SampleRate => 48000;
    public int FrameSizeSamples => 960;
    public event Action<short[]>? PcmCaptured;
    AudioRecord? _recorder;
    AudioTrack? _player;
    CancellationTokenSource? _captureCts;
    Task? _captureTask;

    public void StartCapture()
    {
        if (_recorder != null) return;
        int minBufBytes = AudioRecord.GetMinBufferSize(
            SampleRate, ChannelIn.Mono, Encoding.Pcm16bit);   // ← بدون "Android.Media."
        int bufBytes = Math.Max(minBufBytes, FrameSizeSamples * 2 * 4);
        _recorder = new AudioRecord(
            AudioSource.VoiceCommunication,
            SampleRate,
            ChannelIn.Mono,
            Encoding.Pcm16bit,                                // ← بدون "Android.Media."
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
    }

    public void StartPlayback()
    {
        if (_player != null) return;
        int minBufBytes = AudioTrack.GetMinBufferSize(
            SampleRate, ChannelOut.Mono, Encoding.Pcm16bit);   // ← بدون "Android.Media."
        int bufBytes = Math.Max(minBufBytes, FrameSizeSamples * 2 * 4);
        _player = new AudioTrack(
            new AudioAttributes.Builder()!
                .SetUsage(AudioUsageKind.VoiceCommunication)!
                .SetContentType(AudioContentType.Speech)!
                .Build()!,
            new AudioFormat.Builder()!
                .SetSampleRate(SampleRate)!
                .SetChannelMask(ChannelOut.Mono)!
                .SetEncoding(Encoding.Pcm16bit)!               // ← بدون "Android.Media."
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
    }

    public void PlayPcm(short[] pcm)
    {
        _player?.Write(pcm, 0, pcm.Length);
    }
}