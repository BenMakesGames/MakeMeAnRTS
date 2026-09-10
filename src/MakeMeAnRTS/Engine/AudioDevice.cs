using System.Runtime.InteropServices;
using SDL3;

namespace MakeMeAnRTS.Engine;

/// <summary>
/// Opens the audio device and plays short sounds built in memory.
/// </summary>
/// <remarks>
/// The game ships no audio files, so effects are synthesised as raw PCM the same way the font and the
/// art are made in code.
///
/// Every failure here is survivable and none of them stop the game: a machine with no sound device, a
/// container with no audio at all, a driver that will not open. <see cref="IsAvailable"/> goes false and
/// every play becomes a no-op, because a silent game is fine and a game that refuses to start because
/// it could not find a speaker is not.
/// </remarks>
public sealed class AudioDevice : IDisposable
{
    /// <summary>Sample rate for everything synthesised here. Plenty for short blips and cheap to build.</summary>
    public const int SampleRate = 22050;

    private readonly IntPtr _mixer;
    private readonly List<IntPtr> _sounds = [];
    private bool _disposed;

    public bool IsAvailable => _mixer != IntPtr.Zero;

    private AudioDevice(IntPtr mixer) => _mixer = mixer;

    /// <summary>Opens the default playback device, or returns a silent device if that is not possible.</summary>
    public static AudioDevice Open()
    {
        if (!SDL.InitSubSystem(SDL.InitFlags.Audio))
            return new AudioDevice(IntPtr.Zero);

        if (!Mixer.Init())
        {
            SDL.QuitSubSystem(SDL.InitFlags.Audio);
            return new AudioDevice(IntPtr.Zero);
        }

        var mixer = Mixer.CreateMixerDevice(SDL.AudioDeviceDefaultPlayback, IntPtr.Zero);

        if (mixer == IntPtr.Zero)
        {
            Mixer.Quit();
            SDL.QuitSubSystem(SDL.InitFlags.Audio);
        }

        return new AudioDevice(mixer);
    }

    /// <summary>
    /// Registers a mono sample buffer as a playable sound and returns its handle.
    /// </summary>
    /// <returns><see cref="IntPtr.Zero"/> on a silent device, which <see cref="Play"/> ignores.</returns>
    public IntPtr CreateSound(float[] samples)
    {
        ArgumentNullException.ThrowIfNull(samples);

        if (!IsAvailable || samples.Length == 0)
            return IntPtr.Zero;

        // Both the samples and the format description have to cross to native memory; SDL copies each.
        var bytes = samples.Length * sizeof(float);
        var buffer = Marshal.AllocHGlobal(bytes);
        var specHandle = Marshal.AllocHGlobal(Marshal.SizeOf<SDL.AudioSpec>());

        try
        {
            Marshal.Copy(samples, 0, buffer, samples.Length);
            Marshal.StructureToPtr(
                new SDL.AudioSpec { Format = SDL.AudioFormat.AudioF32LE, Channels = 1, Freq = SampleRate },
                specHandle,
                fDeleteOld: false);

            var sound = Mixer.LoadRawAudio(_mixer, buffer, (UIntPtr)bytes, in specHandle);
            if (sound != IntPtr.Zero)
                _sounds.Add(sound);

            return sound;
        }
        finally
        {
            Marshal.FreeHGlobal(buffer);
            Marshal.FreeHGlobal(specHandle);
        }
    }

    public void Play(IntPtr sound)
    {
        if (IsAvailable && sound != IntPtr.Zero)
            Mixer.PlayAudio(_mixer, sound);
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;

        if (!IsAvailable)
            return;

        Mixer.StopAllTracks(_mixer, 0);

        foreach (var sound in _sounds)
            Mixer.DestroyAudio(sound);

        Mixer.DestroyMixer(_mixer);
        Mixer.Quit();
        SDL.QuitSubSystem(SDL.InitFlags.Audio);
    }
}
