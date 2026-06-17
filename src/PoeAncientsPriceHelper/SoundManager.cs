using NAudio.Wave;
using NAudio.Wave.SampleProviders;
using System.Diagnostics;

namespace PoeAncientsPriceHelper;

/// <summary>
/// Plays alert sounds using NAudio. Supports custom MP3/WAV files from a
/// "sounds/" folder next to the executable, or falls back to generated tones.
/// Drop files named "snipe.wav/.mp3" and "expensive.wav/.mp3" to customise.
/// </summary>
internal static class SoundManager
{
    private static readonly string SoundsDir;
    private static readonly string? SnipeSoundPath;
    private static readonly string? ExpensiveSoundPath;

    // Volume (0.0 – 1.0). The generated fallback tones are loud by design.
    private const float DefaultVolume = 0.8f;

    static SoundManager()
    {
        var exeDir = AppDomain.CurrentDomain.BaseDirectory;
        SoundsDir = Path.Combine(exeDir, "sounds");
        SnipeSoundPath = FindSoundFile("snipe");
        ExpensiveSoundPath = FindSoundFile("expensive");
    }

    /// <summary>
    /// Play the normal snipe alert (quick beep when a new snipe appears).
    /// Non-blocking — fires and forgets on a background thread.
    /// </summary>
    public static void PlaySnipeAlert()
    {
        if (SnipeSoundPath is not null)
            PlayFile(SnipeSoundPath);
        else
            PlayGeneratedTone(frequency: 880, durationMs: 120, volume: DefaultVolume);
    }

    /// <summary>
    /// Play the expensive-item alert (three ascending tones for >1 Divine snipes).
    /// Non-blocking — fires and forgets on a background thread.
    /// </summary>
    public static void PlayExpensiveAlert()
    {
        if (ExpensiveSoundPath is not null)
            PlayFile(ExpensiveSoundPath);
        else
            PlayGeneratedTones(new[]
            {
                (1200, 150, DefaultVolume),
                (1600, 200, DefaultVolume),
                (2000, 250, DefaultVolume)
            });
    }

    /// <summary>
    /// Returns true if custom sound files exist (for UI display).
    /// </summary>
    public static bool HasCustomSounds => SnipeSoundPath is not null || ExpensiveSoundPath is not null;

    /// <summary>
    /// Human-readable description of loaded sounds.
    /// </summary>
    public static string SoundStatus
    {
        get
        {
            var parts = new List<string>();
            if (SnipeSoundPath is not null)
                parts.Add($"snipe: {Path.GetFileName(SnipeSoundPath)}");
            if (ExpensiveSoundPath is not null)
                parts.Add($"expensive: {Path.GetFileName(ExpensiveSoundPath)}");
            return parts.Count > 0
                ? $"Custom sounds ({string.Join(", ", parts)})"
                : "Default tones (drop .mp3/.wav in sounds/ to customise)";
        }
    }

    // --- Internal helpers ---

    private static string? FindSoundFile(string baseName)
    {
        if (!Directory.Exists(SoundsDir)) return null;

        // Priority: .wav > .mp3
        foreach (var ext in new[] { ".wav", ".mp3" })
        {
            var path = Path.Combine(SoundsDir, baseName + ext);
            if (File.Exists(path)) return path;
        }
        return null;
    }

    private static void PlayFile(string path)
    {
        ThreadPool.QueueUserWorkItem(_ =>
        {
            try
            {
                using var reader = new AudioFileReader(path);
                using var output = new WaveOutEvent();
                output.Init(reader);
                output.Volume = DefaultVolume;
                output.PlaybackStopped += (_, _) => output.Dispose();
                output.Play();
                // Keep the output alive until playback finishes
                Thread.Sleep(reader.TotalTime + TimeSpan.FromMilliseconds(100));
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[SoundManager] Failed to play {path}: {ex.Message}");
            }
        });
    }

    private static void PlayGeneratedTone(int frequency, int durationMs, float volume)
    {
        ThreadPool.QueueUserWorkItem(_ =>
        {
            try
            {
                // NAudio 2.x: SignalGenerator(sampleRate, channelCount)
                var signal = new SignalGenerator(44100, 1)
                {
                    Gain = volume,
                    Frequency = frequency,
                    Type = SignalGeneratorType.Sin
                };
                var take = signal.Take(TimeSpan.FromMilliseconds(durationMs));
                using var output = new WaveOutEvent();
                output.Init(take);
                output.Play();
                Thread.Sleep(durationMs + 50);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[SoundManager] Failed to generate tone: {ex.Message}");
            }
        });
    }

    private static void PlayGeneratedTones((int freq, int ms, float vol)[] tones)
    {
        ThreadPool.QueueUserWorkItem(_ =>
        {
            try
            {
                // Play tones sequentially — simple and reliable
                using var output = new WaveOutEvent();
                foreach (var (freq, ms, vol) in tones)
                {
                    var signal = new SignalGenerator(44100, 1)
                    {
                        Gain = vol,
                        Frequency = freq,
                        Type = SignalGeneratorType.Sin
                    };
                    var take = signal.Take(TimeSpan.FromMilliseconds(ms));
                    output.Init(take);
                    output.Play();
                    Thread.Sleep(ms + 30);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[SoundManager] Failed to generate tones: {ex.Message}");
            }
        });
    }
}
