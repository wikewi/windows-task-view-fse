using System.Diagnostics;
using System.IO;
using System.Media;

namespace WindowsTaskViewFSE.Services;

public class SoundService : ISoundService, IDisposable
{
    public bool IsMuted { get; set; } = false;

    private readonly SoundPlayer? _navigatePlayer;
    private readonly SoundPlayer? _selectPlayer;
    private readonly SoundPlayer? _backPlayer;
    private readonly SoundPlayer? _closePlayer;
    private readonly SoundPlayer? _notificationPlayer;

    private readonly MemoryStream? _navStream;
    private readonly MemoryStream? _selectStream;
    private readonly MemoryStream? _backStream;
    private readonly MemoryStream? _closeStream;
    private readonly MemoryStream? _notifStream;

    public SoundService()
    {
        try
        {
            _navStream = CreateToneWavStream(520, 45, 0.25);
            _selectStream = CreateChordWavStream(523.25, 659.25, 90, 0.35); // C5 - E5
            _backStream = CreateToneWavStream(380, 60, 0.25);
            _closeStream = CreateToneWavStream(260, 80, 0.3);
            _notifStream = CreateArpeggioWavStream(new[] { 440.0, 554.37, 659.25, 880.0 }, 140, 0.35); // A major

            _navigatePlayer = new SoundPlayer(_navStream);
            _navigatePlayer.LoadAsync();

            _selectPlayer = new SoundPlayer(_selectStream);
            _selectPlayer.LoadAsync();

            _backPlayer = new SoundPlayer(_backStream);
            _backPlayer.LoadAsync();

            _closePlayer = new SoundPlayer(_closeStream);
            _closePlayer.LoadAsync();

            _notificationPlayer = new SoundPlayer(_notifStream);
            _notificationPlayer.LoadAsync();
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[SoundService] Init error: {ex.Message}");
        }
    }

    public void PlayNavigate()
    {
        if (IsMuted) return;
        PlaySafe(_navigatePlayer, _navStream);
    }

    public void PlaySelect()
    {
        if (IsMuted) return;
        PlaySafe(_selectPlayer, _selectStream);
    }

    public void PlayBack()
    {
        if (IsMuted) return;
        PlaySafe(_backPlayer, _backStream);
    }

    public void PlayClose()
    {
        if (IsMuted) return;
        PlaySafe(_closePlayer, _closeStream);
    }

    public void PlayNotification()
    {
        if (IsMuted) return;
        PlaySafe(_notificationPlayer, _notifStream);
    }

    private static void PlaySafe(SoundPlayer? player, MemoryStream? stream)
    {
        if (player == null || stream == null) return;
        Task.Run(() =>
        {
            try
            {
                lock (stream)
                {
                    stream.Position = 0;
                    player.Play();
                }
            }
            catch { }
        });
    }

    private static MemoryStream CreateToneWavStream(double frequency, int durationMs, double volume)
    {
        const int sampleRate = 44100;
        int totalSamples = (int)(sampleRate * (durationMs / 1000.0));
        short[] samples = new short[totalSamples];

        for (int i = 0; i < totalSamples; i++)
        {
            double t = (double)i / sampleRate;
            double env = Math.Sin(Math.PI * i / totalSamples); // smooth half-sine envelope
            double val = Math.Sin(2.0 * Math.PI * frequency * t) * env * volume;
            samples[i] = (short)(val * short.MaxValue);
        }

        return BuildWavStream(samples, sampleRate);
    }

    private static MemoryStream CreateChordWavStream(double freq1, double freq2, int durationMs, double volume)
    {
        const int sampleRate = 44100;
        int totalSamples = (int)(sampleRate * (durationMs / 1000.0));
        short[] samples = new short[totalSamples];

        for (int i = 0; i < totalSamples; i++)
        {
            double t = (double)i / sampleRate;
            double env = Math.Pow(1.0 - ((double)i / totalSamples), 1.5); // decay envelope
            double s1 = Math.Sin(2.0 * Math.PI * freq1 * t);
            double s2 = Math.Sin(2.0 * Math.PI * freq2 * t);
            double val = (s1 * 0.6 + s2 * 0.4) * env * volume;
            samples[i] = (short)(val * short.MaxValue);
        }

        return BuildWavStream(samples, sampleRate);
    }

    private static MemoryStream CreateArpeggioWavStream(double[] frequencies, int durationMs, double volume)
    {
        const int sampleRate = 44100;
        int totalSamples = (int)(sampleRate * (durationMs / 1000.0));
        short[] samples = new short[totalSamples];
        int samplesPerTone = totalSamples / frequencies.Length;

        for (int i = 0; i < totalSamples; i++)
        {
            int toneIndex = Math.Min(i / samplesPerTone, frequencies.Length - 1);
            double freq = frequencies[toneIndex];
            double t = (double)i / sampleRate;
            int toneLocalSample = i % samplesPerTone;
            double toneEnv = Math.Sin(Math.PI * toneLocalSample / samplesPerTone);
            double overallEnv = 1.0 - ((double)i / totalSamples) * 0.4;
            double val = Math.Sin(2.0 * Math.PI * freq * t) * toneEnv * overallEnv * volume;
            samples[i] = (short)(val * short.MaxValue);
        }

        return BuildWavStream(samples, sampleRate);
    }

    private static MemoryStream BuildWavStream(short[] samples, int sampleRate)
    {
        var ms = new MemoryStream();
        using var bw = new BinaryWriter(ms, System.Text.Encoding.ASCII, leaveOpen: true);

        int subChunk2Size = samples.Length * 2; // 16-bit mono = 2 bytes per sample
        int chunkSize = 36 + subChunk2Size;

        // RIFF header
        bw.Write(System.Text.Encoding.ASCII.GetBytes("RIFF"));
        bw.Write(chunkSize);
        bw.Write(System.Text.Encoding.ASCII.GetBytes("WAVE"));

        // fmt subchunk
        bw.Write(System.Text.Encoding.ASCII.GetBytes("fmt "));
        bw.Write(16);          // Subchunk1Size (16 for PCM)
        bw.Write((short)1);    // AudioFormat (1 = PCM)
        bw.Write((short)1);    // NumChannels (1 = Mono)
        bw.Write(sampleRate);  // SampleRate
        bw.Write(sampleRate * 2); // ByteRate
        bw.Write((short)2);    // BlockAlign
        bw.Write((short)16);   // BitsPerSample

        // data subchunk
        bw.Write(System.Text.Encoding.ASCII.GetBytes("data"));
        bw.Write(subChunk2Size);

        for (int i = 0; i < samples.Length; i++)
        {
            bw.Write(samples[i]);
        }

        bw.Flush();
        ms.Position = 0;
        return ms;
    }

    public void Dispose()
    {
        _navigatePlayer?.Dispose();
        _selectPlayer?.Dispose();
        _backPlayer?.Dispose();
        _closePlayer?.Dispose();
        _notificationPlayer?.Dispose();
        _navStream?.Dispose();
        _selectStream?.Dispose();
        _backStream?.Dispose();
        _closeStream?.Dispose();
        _notifStream?.Dispose();
    }
}
