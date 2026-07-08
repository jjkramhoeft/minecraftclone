using System;
using Microsoft.Xna.Framework.Audio;
using MinecraftClone.World;

namespace MinecraftClone.Audio;

/// <summary>
/// All game audio, synthesized at startup in keeping with the zero-asset
/// ethos: filtered noise bursts with exponential decay per material family
/// (wood adds a low sine knock). If no audio hardware is available the class
/// silently disables itself, so headless/smoke runs never crash.
/// </summary>
public class GameSounds
{
    private const int SampleRate = 22050;

    private enum Material { Stone = 0, Dirt = 1, Sand = 2, Wood = 3, Plant = 4 }
    private const int MaterialCount = 5;

    private readonly Random _rng = new();
    private readonly bool _enabled;
    private readonly SoundEffect[] _dig = new SoundEffect[MaterialCount];
    private readonly SoundEffect _splash;

    public GameSounds()
    {
        try
        {
            //                      duration  cutoff  decay  tone    toneAmp
            _dig[(int)Material.Stone] = Synthesize(0.14f, 0.55f, 28f);
            _dig[(int)Material.Dirt] = Synthesize(0.14f, 0.16f, 32f);
            _dig[(int)Material.Sand] = Synthesize(0.20f, 0.30f, 18f);
            _dig[(int)Material.Wood] = Synthesize(0.14f, 0.22f, 26f, toneHz: 170f, toneAmp: 0.55f);
            _dig[(int)Material.Plant] = Synthesize(0.09f, 0.40f, 48f);
            _splash = Synthesize(0.55f, 0.12f, 7f);
            _enabled = true;
        }
        catch (Exception e) when (e is NoAudioHardwareException or DllNotFoundException)
        {
            _enabled = false;
        }
    }

    public void PlayBreak(BlockType type) => Play(DigFor(type), 0.7f, Jitter(0.15f));

    /// <summary>Same burst as breaking, pitched up — reads as a distinct "tap".</summary>
    public void PlayPlace(BlockType type) => Play(DigFor(type), 0.55f, 0.35f + Jitter(0.1f));

    public void PlayFootstep(BlockType ground) => Play(DigFor(ground), 0.18f, 0.2f + Jitter(0.15f));

    public void PlaySplash() => Play(_splash, 0.8f, Jitter(0.1f));

    /// <summary>Short high pop when a drop lands in the inventory.</summary>
    public void PlayPickup() => Play(_dig[(int)Material.Plant], 0.4f, 0.8f);

    private void Play(SoundEffect sound, float volume, float pitch)
    {
        if (_enabled)
            sound.Play(volume, pitch, 0f);
    }

    private float Jitter(float amount) => (float)(_rng.NextDouble() * 2 - 1) * amount;

    private SoundEffect DigFor(BlockType type) => _dig[(int)MaterialOf(type)];

    private static Material MaterialOf(BlockType type) => type switch
    {
        BlockType.Stone or BlockType.Bricks or BlockType.CoalOre or BlockType.IronOre => Material.Stone,
        BlockType.Sand => Material.Sand,
        BlockType.Wood or BlockType.Planks or BlockType.BirchLog or BlockType.PineLog => Material.Wood,
        BlockType.Leaves or BlockType.BirchLeaves or BlockType.PineLeaves => Material.Plant,
        _ when BlockInfo.IsPlant(type) => Material.Plant,
        _ => Material.Dirt,
    };

    /// <summary>White noise through a one-pole lowpass (cutoff01 = filter
    /// coefficient, lower = duller) with an exponential envelope; an optional
    /// decaying sine gives wood its knock.</summary>
    private static SoundEffect Synthesize(float duration, float cutoff01, float decayRate,
        float toneHz = 0f, float toneAmp = 0f)
    {
        int samples = (int)(SampleRate * duration);
        var data = new byte[samples * 2];
        var rng = new Random(unchecked((int)(cutoff01 * 10000) * 31 + (int)toneHz)); // deterministic per sound
        float lowpass = 0f;

        for (int i = 0; i < samples; i++)
        {
            float t = i / (float)SampleRate;
            float envelope = MathF.Exp(-decayRate * t);
            float white = (float)(rng.NextDouble() * 2 - 1);
            lowpass += cutoff01 * (white - lowpass);

            float sample = lowpass * envelope;
            if (toneHz > 0f)
                sample += toneAmp * envelope * MathF.Sin(MathF.Tau * toneHz * t);

            short value = (short)(Math.Clamp(sample, -1f, 1f) * (short.MaxValue * 0.8f));
            data[i * 2] = (byte)value;
            data[i * 2 + 1] = (byte)(value >> 8);
        }

        return new SoundEffect(data, SampleRate, AudioChannels.Mono);
    }
}
