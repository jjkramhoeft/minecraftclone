using System;
using Microsoft.Xna.Framework;

namespace MinecraftClone.World;

/// <summary>
/// A tiny fixed-pool particle simulator for cosmetic effects — currently water
/// splashes and swim bubbles. Allocation-free: particles live in a flat array
/// with an active count, and dead ones are swap-removed each update. Each
/// particle carries its own vertical acceleration so droplets arc down under
/// gravity while bubbles drift up.
/// </summary>
public class Particles
{
    public struct Particle
    {
        public Vector3 Position;
        public Vector3 Velocity;
        public float AccelY; // per-particle vertical acceleration (gravity or buoyancy)
        public float Age;
        public float Life;
        public float Size; // half-extent of the billboard, in blocks
        public Color Color;
    }

    public const int Capacity = 256;

    private readonly Particle[] _pool = new Particle[Capacity];
    private int _count;
    // A fixed seed keeps spawns cheap (no per-frame allocation) and repeatable.
    private readonly Random _rng = new(1234);

    public int Count => _count;

    /// <summary>Live particles occupy the first <see cref="Count"/> entries.</summary>
    public Particle[] Pool => _pool;

    public void Clear() => _count = 0;

    public void Update(float dt)
    {
        for (int i = 0; i < _count;)
        {
            ref var p = ref _pool[i];
            p.Age += dt;
            if (p.Age >= p.Life)
            {
                _pool[i] = _pool[--_count]; // swap-remove; don't advance i
                continue;
            }
            p.Velocity.Y += p.AccelY * dt;
            p.Position += p.Velocity * dt;
            i++;
        }
    }

    /// <summary>Bursts droplets upward and outward from a splash point.</summary>
    public void SpawnSplash(Vector3 center, int count)
    {
        for (int i = 0; i < count; i++)
        {
            float ang = (float)(_rng.NextDouble() * Math.PI * 2);
            float spread = 1.2f + (float)_rng.NextDouble() * 1.8f;
            var velocity = new Vector3(
                MathF.Cos(ang) * spread,
                2.5f + (float)_rng.NextDouble() * 2.5f,
                MathF.Sin(ang) * spread);
            Spawn(center, velocity, -14f, 0.5f + (float)_rng.NextDouble() * 0.35f);
        }
    }

    /// <summary>A single lazy bubble drifting up while swimming.</summary>
    public void SpawnBubble(Vector3 center)
    {
        var jitter = new Vector3(
            (float)(_rng.NextDouble() - 0.5) * 0.6f,
            (float)(_rng.NextDouble() - 0.5) * 0.4f,
            (float)(_rng.NextDouble() - 0.5) * 0.6f);
        var velocity = new Vector3(0f, 0.9f + (float)_rng.NextDouble() * 0.7f, 0f);
        Spawn(center + jitter, velocity, 1.5f, 0.6f + (float)_rng.NextDouble() * 0.5f);
    }

    private void Spawn(Vector3 position, Vector3 velocity, float accelY, float life)
    {
        if (_count >= Capacity)
            return;
        ref var p = ref _pool[_count++];
        p.Position = position;
        p.Velocity = velocity;
        p.AccelY = accelY;
        p.Age = 0f;
        p.Life = life;
        p.Size = 0.05f + (float)_rng.NextDouble() * 0.05f;
        p.Color = new Color(210, 232, 255); // watery white-blue
    }
}
