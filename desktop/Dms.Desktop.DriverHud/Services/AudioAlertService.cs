using System.IO;
using System.Media;
using Dms.Shared.Contracts.Telemetry;
using Microsoft.Extensions.Logging;

namespace Dms.Desktop.DriverHud.Services;

/// <summary>
/// High-reliability Audio Alert Service for Driver HUD:
/// Uses native Win32 SoundPlayer loops and synthesized PCM WAV streams for zero-latency,
/// repeatable multi-tiered warning sound sirens.
/// </summary>
public class AudioAlertService : IDisposable
{
    private readonly ILogger<AudioAlertService> _logger;
    private readonly SoundPlayer _sirenPlayer;
    private readonly SoundPlayer _chimePlayer;
    private readonly SoundPlayer _beepPlayer;
    private bool _isPlaying;
    private readonly object _lock = new();

    public AudioAlertService(ILogger<AudioAlertService> logger)
    {
        _logger = logger;

        // Pre-synthesize WAV streams for zero-latency playback
        _sirenPlayer = new SoundPlayer(GenerateToneWavStream(frequencyHz: 1000, durationMs: 400));
        _chimePlayer = new SoundPlayer(GenerateToneWavStream(frequencyHz: 600, durationMs: 300));
        _beepPlayer = new SoundPlayer(GenerateToneWavStream(frequencyHz: 850, durationMs: 150));

        try
        {
            _sirenPlayer.Load();
            _chimePlayer.Load();
            _beepPlayer.Load();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to pre-load audio alert sound players");
        }
    }

    /// <summary>
    /// Plays an audio alert appropriate for the given incident severity.
    /// MicroSleep & Perclos trigger a repeating siren loop until Stop() is called.
    /// </summary>
    public void PlayAlert(IncidentType type)
    {
        lock (_lock)
        {
            StopInternal();

            try
            {
                switch (type)
                {
                    case IncidentType.MicroSleep:
                    case IncidentType.Perclos:
                        _sirenPlayer.PlayLooping();
                        _isPlaying = true;
                        break;
                    case IncidentType.Yawning:
                        _chimePlayer.Play();
                        _isPlaying = true;
                        break;
                    case IncidentType.OffRoadDistraction:
                        _beepPlayer.Play();
                        _isPlaying = true;
                        break;
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Primary audio alert playback failed; using system sound fallback");
                PlayFallbackSound();
            }
        }
    }

    /// <summary>
    /// Plays Stage 1 fast advisory warning chime when eyes are closed > 600ms.
    /// </summary>
    public void PlayCautionChime()
    {
        lock (_lock)
        {
            StopInternal();
            try
            {
                _chimePlayer.Play();
                _isPlaying = true;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Caution chime playback failed");
                PlayFallbackSound();
            }
        }
    }

    /// <summary>
    /// Instantly stops any active audio alert or repeating siren loop.
    /// </summary>
    public void Stop()
    {
        lock (_lock)
        {
            StopInternal();
        }
    }

    private void StopInternal()
    {
        try
        {
            if (_isPlaying)
            {
                _sirenPlayer.Stop();
                _chimePlayer.Stop();
                _beepPlayer.Stop();
                _isPlaying = false;
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error stopping sound player");
        }
    }

    private static void PlayFallbackSound()
    {
        try
        {
            SystemSounds.Exclamation.Play();
        }
        catch
        {
            // Ignore if audio device unavailable
        }
    }

    /// <summary>
    /// Synthesizes a PCM 16-bit 44.1kHz mono WAV MemoryStream for a sine wave tone.
    /// </summary>
    private static MemoryStream GenerateToneWavStream(int frequencyHz, int durationMs)
    {
        int sampleRate = 44100;
        int numSamples = (sampleRate * durationMs) / 1000;
        int dataSize = numSamples * 2;

        var stream = new MemoryStream();
        using (var writer = new BinaryWriter(stream, System.Text.Encoding.UTF8, leaveOpen: true))
        {
            // RIFF header
            writer.Write("RIFF".ToCharArray());
            writer.Write(36 + dataSize);
            writer.Write("WAVE".ToCharArray());

            // fmt subchunk
            writer.Write("fmt ".ToCharArray());
            writer.Write(16); // Subchunk1Size
            writer.Write((short)1); // PCM
            writer.Write((short)1); // Mono
            writer.Write(sampleRate);
            writer.Write(sampleRate * 2); // ByteRate
            writer.Write((short)2); // BlockAlign
            writer.Write((short)16); // BitsPerSample

            // data subchunk
            writer.Write("data".ToCharArray());
            writer.Write(dataSize);

            // Sine wave samples with smooth envelope tapering
            int fadeSamples = (sampleRate * 15) / 1000;
            for (int i = 0; i < numSamples; i++)
            {
                double t = (double)i / sampleRate;
                double sine = Math.Sin(2 * Math.PI * frequencyHz * t);

                double envelope = 1.0;
                if (i < fadeSamples)
                {
                    envelope = (double)i / fadeSamples;
                }
                else if (i > numSamples - fadeSamples)
                {
                    envelope = (double)(numSamples - i) / fadeSamples;
                }

                short sample = (short)(sine * envelope * 28000);
                writer.Write(sample);
            }
        }

        stream.Position = 0;
        return stream;
    }

    public void Dispose()
    {
        Stop();
        _sirenPlayer.Dispose();
        _chimePlayer.Dispose();
        _beepPlayer.Dispose();
        GC.SuppressFinalize(this);
    }
}
