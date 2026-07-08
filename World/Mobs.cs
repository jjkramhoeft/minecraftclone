using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using MinecraftClone.Player;

namespace MinecraftClone.World;

public enum MobKind : byte
{
    Pig,
    Chicken,
}

/// <summary>
/// Passive wandering animals. Each mob alternates idle and walk states on
/// random timers, hops one-block steps by jumping when it bumps a wall, and
/// uses the same AABB collision as the player (smaller box). The population
/// is maintained around the player: spawns on nearby grass, despawns far
/// away. Mobs are ambient — never saved, like falling blocks mid-air.
/// </summary>
public class Mobs
{
    public class Mob
    {
        public MobKind Kind;
        public Vector3 Position; // feet center
        public Vector3 Velocity;
        public float Yaw;
        public float WalkTimer;  // remaining walk time; idle when <= 0
        public float IdleTimer;  // remaining idle time before the next stroll
        public float WalkPhase;  // drives leg swing in the renderer
        public bool IsOnGround;
    }

    private const int MaxPopulation = 10;
    private const float SpawnInterval = 2f;
    private const float MinSpawnDistance = 20f;
    private const float MaxSpawnDistance = 40f;
    private const float DespawnDistance = 96f;
    private const float Gravity = -25f;
    private const float HopVelocity = 7.5f;

    private readonly List<Mob> _mobs = new();
    private readonly Random _rng = new();
    private float _spawnTimer;

    public IReadOnlyList<Mob> All => _mobs;

    public void Clear() => _mobs.Clear();

    public void Update(ChunkManager world, Vector3 playerPosition, float dt)
    {
        _spawnTimer += dt;
        if (_spawnTimer >= SpawnInterval)
        {
            _spawnTimer = 0f;
            if (_mobs.Count < MaxPopulation)
                TrySpawn(world, playerPosition);
        }

        for (int i = _mobs.Count - 1; i >= 0; i--)
        {
            var mob = _mobs[i];
            if (Vector3.Distance(mob.Position, playerPosition) > DespawnDistance
                || !world.IsChunkLoaded(ChunkManager.ToChunkCoord(mob.Position)))
            {
                _mobs.RemoveAt(i);
                continue;
            }
            UpdateMob(mob, world, dt);
        }
    }

    private void UpdateMob(Mob mob, ChunkManager world, float dt)
    {
        float speed = mob.Kind == MobKind.Pig ? 1.3f : 1.0f;

        if (mob.WalkTimer > 0f)
        {
            mob.WalkTimer -= dt;
            mob.Velocity.X = MathF.Sin(mob.Yaw) * speed;
            mob.Velocity.Z = MathF.Cos(mob.Yaw) * speed;
            mob.WalkPhase += speed * 4f * dt;
        }
        else
        {
            mob.Velocity.X = 0f;
            mob.Velocity.Z = 0f;
            mob.IdleTimer -= dt;
            if (mob.IdleTimer <= 0f)
            {
                mob.Yaw = (float)(_rng.NextDouble() * MathF.Tau);
                mob.WalkTimer = 1f + (float)_rng.NextDouble() * 2.5f;
                mob.IdleTimer = 2f + (float)_rng.NextDouble() * 4f;
            }
        }

        bool inWater = BlockInfo.IsWater(world.GetBlock(
            (int)MathF.Floor(mob.Position.X), (int)MathF.Floor(mob.Position.Y + 0.3f), (int)MathF.Floor(mob.Position.Z)));
        mob.Velocity.Y = inWater
            ? 2f // buoyant: bob up and paddle out
            : Math.Max(mob.Velocity.Y + Gravity * dt, -50f);

        var (halfWidth, height) = SizeOf(mob.Kind);
        mob.IsOnGround = PlayerPhysics.MoveWithCollision(
            ref mob.Position, ref mob.Velocity, world, dt, out bool hitWall, halfWidth, height);

        // A wall while walking is usually a one-block step — hop it. If the
        // hop doesn't clear it, the next idle roll turns the mob anyway.
        if (hitWall && mob.IsOnGround && mob.WalkTimer > 0f)
            mob.Velocity.Y = HopVelocity;
    }

    public static (float HalfWidth, float Height) SizeOf(MobKind kind) =>
        kind == MobKind.Pig ? (0.35f, 0.9f) : (0.2f, 0.6f);

    private void TrySpawn(ChunkManager world, Vector3 playerPosition)
    {
        float angle = (float)(_rng.NextDouble() * MathF.Tau);
        float distance = MinSpawnDistance + (float)_rng.NextDouble() * (MaxSpawnDistance - MinSpawnDistance);
        int x = (int)MathF.Floor(playerPosition.X + MathF.Sin(angle) * distance);
        int z = (int)MathF.Floor(playerPosition.Z + MathF.Cos(angle) * distance);
        if (!world.IsChunkLoaded(new ChunkCoord(x >> 4, z >> 4)))
            return;

        // Walk down from above the terrain to the first solid block.
        for (int y = Chunk.SizeY - 2; y > 1; y--)
        {
            var ground = world.GetBlock(x, y, z);
            if (ground == BlockType.Air || BlockInfo.IsPlant(ground))
                continue;
            if (ground == BlockType.Grass) // animals only live on grass
                _mobs.Add(new Mob
                {
                    Kind = _rng.Next(2) == 0 ? MobKind.Pig : MobKind.Chicken,
                    Position = new Vector3(x + 0.5f, y + 1, z + 0.5f),
                    Yaw = (float)(_rng.NextDouble() * MathF.Tau),
                    IdleTimer = (float)_rng.NextDouble() * 2f,
                });
            return; // water/sand/stone surface: no spawn, try again later
        }
    }
}
