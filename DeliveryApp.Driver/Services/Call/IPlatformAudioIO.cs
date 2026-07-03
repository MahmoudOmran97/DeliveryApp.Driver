namespace DeliveryApp.Driver.Services.Call;

/// <summary>
/// كل منصة (Android/iOS) بتنفّذ الـ interface ده بمايكها وسماعتها الحقيقية.
/// الجزء ده هو الوحيد اللي محتاج كود Native — كل حاجة تانية (WebRTC/Opus) مشتركة C#.
/// PCM = صوت خام 16-bit signed mono @ SampleRate (48000 مقترح لتوافق Opus).
/// </summary>
public interface IPlatformAudioIO
{
    int SampleRate { get; }              // مثلاً 48000
    int FrameSizeSamples { get; }        // مثلاً 960 (يساوي 20ms عند 48kHz)

    /// بيتنادي كل ما فيه Frame جديد من المايك (على أي Thread — الكولر مسؤول عن الـ Marshalling)
    event Action<short[]>? PcmCaptured;

    void StartCapture();
    void StopCapture();

    /// شغّل الصوت المستقبل من الطرف التاني على السماعة
    void PlayPcm(short[] pcm);

    void StartPlayback();
    void StopPlayback();
}
