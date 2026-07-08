using System;
using Microsoft.Xna.Framework;

namespace MinecraftClone.World;

/// <summary>
/// Game time and everything derived from it. TimeOfDay runs 0..1 over a
/// 10-minute real-time day: 0 = sunrise, 0.25 = noon, 0.5 = sunset,
/// 0.75 = midnight. Sky color, scene light, and star visibility come from a
/// keyframe table interpolated with smoothstep; renderers consume them every
/// frame (the sky as the clear/fog color, the light as the effects'
/// DiffuseColor). The night light never drops below a moonlit floor, so the
/// world stays playable in the dark.
/// </summary>
public class DayNightCycle
{
    public const float DayLengthSeconds = 600f;

    private readonly record struct Keyframe(float Time, Color Sky, Vector3 Light, float Stars);

    private static readonly Color DaySky = new(100, 149, 237); // cornflower — matches the original fixed sky
    private static readonly Color NightSky = new(10, 14, 40);
    private static readonly Vector3 DayLight = Vector3.One;
    private static readonly Vector3 NightLight = new(0.30f, 0.32f, 0.45f); // moonlit floor

    private static readonly Keyframe[] Keyframes =
    {
        new(0.00f, new Color(235, 145, 105), new Vector3(0.82f, 0.75f, 0.68f), 0f),   // sunrise
        new(0.06f, DaySky, DayLight, 0f),                                             // morning
        new(0.44f, DaySky, DayLight, 0f),                                             // late afternoon
        new(0.52f, new Color(250, 140, 70), new Vector3(0.85f, 0.72f, 0.60f), 0f),    // sunset
        new(0.60f, NightSky, NightLight, 1f),                                         // nightfall
        new(0.92f, NightSky, NightLight, 1f),                                         // late night
        new(1.00f, new Color(235, 145, 105), new Vector3(0.82f, 0.75f, 0.68f), 0f),   // wraps to sunrise
    };

    private float _timeOfDay;

    public float TimeOfDay
    {
        get => _timeOfDay;
        set
        {
            _timeOfDay = value - MathF.Floor(value);
            Recalculate();
        }
    }

    public Color SkyColor { get; private set; }
    public Vector3 LightColor { get; private set; }

    /// <summary>0 in daylight, 1 in deep night — drives star visibility.</summary>
    public float StarAlpha { get; private set; }

    /// <summary>Ever-increasing seconds since this session began — drives the
    /// star-twinkle shimmer. Not wrapped and not persisted.</summary>
    public float AnimationTime { get; private set; }

    // Eight discrete lunar phases, like the reference game; advances one step
    // each midnight. Session-scoped (not saved), and offset so the first night
    // opens on a full moon rather than an invisible new moon.
    private const int LunarPhases = 8;
    private int _dayCount;

    /// <summary>Lunar phase in [0,1): 0 and 1 are new moon, 0.5 is full.</summary>
    public float MoonPhase => ((_dayCount + LunarPhases / 2) % LunarPhases) / (float)LunarPhases;

    /// <summary>Unit direction toward the sun: rises at +X, arcs overhead, sets at -X.</summary>
    public Vector3 SunDirection
    {
        get
        {
            float angle = _timeOfDay * MathHelper.TwoPi;
            return Vector3.Normalize(new Vector3(MathF.Cos(angle), MathF.Sin(angle), 0.25f));
        }
    }

    public Vector3 MoonDirection => -SunDirection;

    public DayNightCycle() => Recalculate();

    public void Update(float dt)
    {
        AnimationTime += dt;
        float next = _timeOfDay + dt / DayLengthSeconds;
        if (next >= 1f)
            _dayCount++; // crossed midnight → next lunar phase
        TimeOfDay = next; // setter wraps back into 0..1
    }

    private void Recalculate()
    {
        int segment = 0;
        while (segment < Keyframes.Length - 2 && Keyframes[segment + 1].Time < _timeOfDay)
            segment++;

        var from = Keyframes[segment];
        var to = Keyframes[segment + 1];
        float t = MathHelper.Clamp((_timeOfDay - from.Time) / (to.Time - from.Time), 0f, 1f);
        t = t * t * (3f - 2f * t); // smoothstep

        SkyColor = Color.Lerp(from.Sky, to.Sky, t);
        LightColor = Vector3.Lerp(from.Light, to.Light, t);
        // Squared so stars only appear once the sky is properly dark.
        float stars = MathHelper.Lerp(from.Stars, to.Stars, t);
        StarAlpha = stars * stars;
    }
}
