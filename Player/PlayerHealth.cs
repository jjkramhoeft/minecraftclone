using System;
using Microsoft.Xna.Framework;
using MinecraftClone.World;

namespace MinecraftClone.Player;

/// <summary>
/// Survival vitals: health in half-hearts, fall damage from landing impact,
/// and an air meter that drains while the eye is underwater. Death fires an
/// event; the composition root decides what respawning means.
/// </summary>
public class PlayerHealth
{
    public const int MaxHealth = 20;   // half-hearts, like the reference game
    public const float MaxAir = 10f;   // seconds of breath

    private const float SafeFallBlocks = 3f;
    private const float GravityMagnitude = 25f; // matches PlayerController.Gravity
    private const int DrownDamagePerSecond = 2;

    public int Health { get; private set; } = MaxHealth;
    public float Air { get; private set; } = MaxAir;
    public bool IsDead => Health <= 0;
    public bool IsUnderwater { get; private set; }

    private float _drownTimer;

    public event Action Died;
    public event Action<int> Damaged;

    public void Update(PlayerController player, ChunkManager world, float dt)
    {
        if (player.LandingImpact > 0f && !player.IsFlying)
        {
            // Convert impact speed back to fall height (v² = 2gh); everything
            // beyond the safe height hurts, one half-heart per block.
            float fallBlocks = player.LandingImpact * player.LandingImpact / (2f * GravityMagnitude);
            int damage = (int)MathF.Floor(fallBlocks - SafeFallBlocks);
            if (damage > 0 && !FeetInWater(player, world))
                Damage(damage);
        }

        var eye = player.EyePosition;
        IsUnderwater = BlockInfo.IsWater(world.GetBlock(
            (int)MathF.Floor(eye.X), (int)MathF.Floor(eye.Y), (int)MathF.Floor(eye.Z)));
        if (IsUnderwater)
        {
            Air = MathF.Max(0f, Air - dt);
            if (Air <= 0f)
            {
                _drownTimer += dt;
                if (_drownTimer >= 1f)
                {
                    _drownTimer = 0f;
                    Damage(DrownDamagePerSecond);
                }
            }
        }
        else
        {
            Air = MaxAir;
            _drownTimer = 0f;
        }
    }

    public void Damage(int amount)
    {
        if (IsDead || amount <= 0)
            return;
        Health = Math.Max(0, Health - amount);
        Damaged?.Invoke(amount);
        if (Health == 0)
            Died?.Invoke();
    }

    public void Reset()
    {
        Health = MaxHealth;
        Air = MaxAir;
        _drownTimer = 0f;
    }

    private static bool FeetInWater(PlayerController player, ChunkManager world) =>
        BlockInfo.IsWater(world.GetBlock(
            (int)MathF.Floor(player.Position.X),
            (int)MathF.Floor(player.Position.Y + 0.1f),
            (int)MathF.Floor(player.Position.Z)));
}
