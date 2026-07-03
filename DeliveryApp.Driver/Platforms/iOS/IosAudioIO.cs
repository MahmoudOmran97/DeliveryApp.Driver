using AVFoundation;
using AudioToolbox;
using DeliveryApp.Driver.Services.Call;

namespace DeliveryApp.Driver.Platforms.iOS;

/// <summary>
/// التقاط الصوت من المايك وتشغيله على السماعة على iOS، باستخدام AVAudioEngine.
/// ⚠️⚠️ ده الجزء اللي أقل ثقة فيه في الكود كله — مقدرتش أختبره على جهاز/Simulator حقيقي
/// خالص (محتاج Mac + Xcode مش متاحين هنا). فيه احتمال حقيقي إن الـ sample rate conversion
/// بين هاردوير المايك (غالباً 44.1kHz أو 48kHz حسب الجهاز) والـ 48kHz اللي محتاجينه لـ Opus
/// يحتاج ضبط إضافي بالتجربة على جهاز فعلي. لو الصوت طلع متقطع أو فيه تشويش، ده أول مكان تدور فيه.
/// </summary>
public class IosAudioIO : IPlatformAudioIO
{
    public int SampleRate => 48000;
    public int FrameSizeSamples => 960;

    public event Action<short[]>? PcmCaptured;

    AVAudioEngine? _engine;
    AVAudioPlayerNode? _playerNode;
    AVAudioFormat? _pcmFormat;
    bool _capturing;

    public void StartCapture()
    {
        if (_capturing) return;

        var session = AVAudioSession.SharedInstance();
        session.SetCategory(AVAudioSessionCategory.PlayAndRecord,
            AVAudioSessionCategoryOptions.DefaultToSpeaker | AVAudioSessionCategoryOptions.AllowBluetooth);
        session.SetActive(true, out _);

        _engine = new AVAudioEngine();
        var input = _engine.InputNode;
        var inputFormat = input.GetBusInputFormat(0);

        // فورمات الهدف: 16-bit PCM mono @ 48kHz (اللي محتاجينها لـ Opus)
        _pcmFormat = new AVAudioFormat(AVAudioCommonFormat.PCMInt16, SampleRate, 1, false);

        input.InstallTapOnBus(0, (uint)FrameSizeSamples, inputFormat, (buffer, when) =>
        {
            try
            {
                // ⚠️ لو inputFormat.SampleRate != 48000 (شائع جداً على iOS)، لازم Resample هنا
                // قبل ما نودّي البيانات لـ Concentus. AVAudioConverter هو الأداة المناسبة —
                // سبتها كـ TODO لحد ما تتظبط على جهاز حقيقي.
                var frameCount = (int)buffer.FrameLength;
                if (frameCount == 0) return;

                var pcm = new short[frameCount];
                unsafe
                {
                    var channelData = (short**)(void*)buffer.Int16ChannelData;
                    for (int i = 0; i < frameCount; i++)
                        pcm[i] = channelData[0][i];
                }
                PcmCaptured?.Invoke(pcm);
            }
            catch { /* لو حصل استثناء هنا مش هيوقف الالتقاط بالكامل */ }
        });

        _engine.Prepare();
        _engine.StartAndReturnError(out var err);
        _capturing = err == null;
    }

    public void StopCapture()
    {
        if (!_capturing) return;
        _engine?.InputNode.RemoveTapOnBus(0);
        _engine?.Stop();
        _engine = null;
        _capturing = false;
    }

    public void StartPlayback()
    {
        // بنستخدم نفس الـ engine بتاع الالتقاط لو موجود، أو ننشئ واحد جديد للتشغيل بس
        _engine ??= new AVAudioEngine();
        _playerNode = new AVAudioPlayerNode();
        _engine.AttachNode(_playerNode);
        var outputFormat = new AVAudioFormat(AVAudioCommonFormat.PCMInt16, SampleRate, 1, false);
        _engine.Connect(_playerNode, _engine.MainMixerNode, outputFormat);

        if (!_engine.Running)
        {
            _engine.Prepare();
            _engine.StartAndReturnError(out _);
        }
        _playerNode.Play();
    }

    public void StopPlayback()
    {
        _playerNode?.Stop();
        _playerNode = null;
    }

    public void PlayPcm(short[] pcm)
    {
        if (_playerNode == null || _pcmFormat == null) return;

        var buffer = new AVAudioPcmBuffer(_pcmFormat, (uint)pcm.Length);
        buffer.FrameLength = (uint)pcm.Length;
        unsafe
        {
            var channelData = (short**)(void*)buffer.Int16ChannelData;
            for (int i = 0; i < pcm.Length; i++)
                channelData[0][i] = pcm[i];
        }
        _playerNode.ScheduleBuffer(buffer, null);
    }
}
