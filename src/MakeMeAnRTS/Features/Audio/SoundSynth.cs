using MakeMeAnRTS.Core;
using MakeMeAnRTS.Engine;

namespace MakeMeAnRTS.Features.Audio;

/// <summary>
/// Builds the game's sound effects out of arithmetic: tones, noise and envelopes.
/// </summary>
/// <remarks>
/// No audio files, for the same reason there are no image files - the whole game is meant to build and
/// run from source with nothing else to fetch. These are deliberately short and quiet: an RTS plays a
/// lot of them, and anything with a tail turns into mush the moment two things happen at once.
/// </remarks>
public static class SoundSynth
{
    private static readonly Rng Noise = new(20260910);

    /// <summary>A tone that slides from one pitch to another, with a soft attack and decay.</summary>
    public static float[] Tone(float startHz, float endHz, float seconds, float amplitude = 0.25f)
    {
        var samples = new float[(int)(AudioDevice.SampleRate * seconds)];
        var phase = 0f;

        for (var i = 0; i < samples.Length; i++)
        {
            var t = i / (float)samples.Length;
            var hz = MathHelpers.Lerp(startHz, endHz, t);

            phase += MathF.Tau * hz / AudioDevice.SampleRate;
            samples[i] = MathF.Sin(phase) * Envelope(t) * amplitude;
        }

        return samples;
    }

    /// <summary>A filtered noise burst, for impacts: chopping, hammering, a blow landing.</summary>
    public static float[] NoiseBurst(float seconds, float amplitude = 0.22f, float smoothing = 0.35f)
    {
        var samples = new float[(int)(AudioDevice.SampleRate * seconds)];
        var previous = 0f;

        for (var i = 0; i < samples.Length; i++)
        {
            var t = i / (float)samples.Length;
            var white = Noise.NextFloat(-1f, 1f);

            // A one-pole low pass: white noise alone is a hiss, this makes it a thud.
            previous = MathHelpers.Lerp(white, previous, smoothing);
            samples[i] = previous * Envelope(t) * amplitude;
        }

        return samples;
    }

    /// <summary>Plays several parts one after another, for little melodies.</summary>
    public static float[] Sequence(params float[][] parts)
    {
        ArgumentNullException.ThrowIfNull(parts);

        var total = parts.Sum(part => part.Length);
        var combined = new float[total];
        var offset = 0;

        foreach (var part in parts)
        {
            part.CopyTo(combined, offset);
            offset += part.Length;
        }

        return combined;
    }

    /// <summary>Adds parts together so they sound at once, making a chord out of tones.</summary>
    public static float[] Layer(params float[][] parts)
    {
        ArgumentNullException.ThrowIfNull(parts);

        var combined = new float[parts.Max(part => part.Length)];

        foreach (var part in parts)
            for (var i = 0; i < part.Length; i++)
                combined[i] = Math.Clamp(combined[i] + part[i], -1f, 1f);

        return combined;
    }

    /// <summary>
    /// Fades in over the first few percent and out over the rest.
    /// </summary>
    /// <remarks>
    /// The fade-in matters as much as the fade-out: starting a waveform at full amplitude puts a step in
    /// the signal, which is audible as a click at the front of every single sound.
    /// </remarks>
    private static float Envelope(float t)
    {
        const float attack = 0.04f;

        return t < attack ? t / attack : MathF.Pow(1f - (t - attack) / (1f - attack), 1.6f);
    }
}
