using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using MinecraftClone.Rendering;
using MinecraftClone.World;

namespace MinecraftClone.Player;

/// <summary>
/// Turns input into player movement: walking with gravity and jumping, or
/// free-fly (F toggles). Both modes collide with the terrain.
/// </summary>
public class PlayerController
{
    private const float WalkSpeed = 4.5f;
    private const float SprintMultiplier = 1.6f;
    private const float FlySpeed = 12f;
    private const float FlyVerticalSpeed = 10f;
    private const float JumpVelocity = 8.5f;
    private const float Gravity = -25f;
    private const float TerminalVelocity = -50f;
    private const float SwimUpSpeed = 4.5f;
    private const float WaterSinkSpeed = -4f;
    // Swimming against a wall, Space boosts hard enough to lift the feet a full
    // block above the surface — SwimUpSpeed alone tops out just below the shore.
    private const float WaterExitBoost = 7f;
    public const float EyeHeight = 1.62f;

    private KeyboardState _previousKeyboard;
    private bool _againstWall;

    /// <summary>Feet position (center of the AABB footprint).</summary>
    public Vector3 Position;
    public Vector3 Velocity;
    public bool IsFlying { get; private set; }
    public bool IsOnGround { get; private set; }

    public Vector3 EyePosition => Position + new Vector3(0f, EyeHeight, 0f);

    public PlayerController(Vector3 spawnPosition, bool isFlying = false)
    {
        Position = spawnPosition;
        IsFlying = isFlying;
    }

    public void Update(KeyboardState keyboard, FirstPersonCamera camera, ChunkManager world, float dt)
    {
        if (keyboard.IsKeyDown(Keys.F) && _previousKeyboard.IsKeyUp(Keys.F))
        {
            IsFlying = !IsFlying;
            Velocity = Vector3.Zero;
        }
        _previousKeyboard = keyboard;

        // Freeze until the terrain under the player exists, so we never fall
        // through chunks that haven't loaded yet.
        if (!world.IsChunkLoaded(ChunkManager.ToChunkCoord(Position)))
            return;

        var wish = Vector3.Zero;
        if (keyboard.IsKeyDown(Keys.W)) wish += camera.HorizontalForward;
        if (keyboard.IsKeyDown(Keys.S)) wish -= camera.HorizontalForward;
        if (keyboard.IsKeyDown(Keys.D)) wish += camera.Right;
        if (keyboard.IsKeyDown(Keys.A)) wish -= camera.Right;
        if (wish != Vector3.Zero) wish.Normalize();

        if (IsFlying)
        {
            Velocity = wish * FlySpeed;
            if (keyboard.IsKeyDown(Keys.Space)) Velocity.Y = FlyVerticalSpeed;
            else if (keyboard.IsKeyDown(Keys.LeftShift)) Velocity.Y = -FlyVerticalSpeed;
        }
        else
        {
            float speed = WalkSpeed * (keyboard.IsKeyDown(Keys.LeftControl) ? SprintMultiplier : 1f);
            bool inWater = BlockInfo.IsWater(world.GetBlock(
                (int)MathF.Floor(Position.X),
                (int)MathF.Floor(Position.Y + 0.6f),
                (int)MathF.Floor(Position.Z)));

            if (inWater)
            {
                // Buoyancy: slow sinking, and Space swims up regardless of ground.
                Velocity.X = wish.X * speed * 0.6f;
                Velocity.Z = wish.Z * speed * 0.6f;
                Velocity.Y = MathF.Max(Velocity.Y + Gravity * 0.35f * dt, WaterSinkSpeed);
                if (keyboard.IsKeyDown(Keys.Space))
                    Velocity.Y = _againstWall ? WaterExitBoost : SwimUpSpeed;
            }
            else
            {
                Velocity.X = wish.X * speed;
                Velocity.Z = wish.Z * speed;
                Velocity.Y = MathF.Max(Velocity.Y + Gravity * dt, TerminalVelocity);
                if (IsOnGround && keyboard.IsKeyDown(Keys.Space))
                    Velocity.Y = JumpVelocity;
            }
        }

        IsOnGround = PlayerPhysics.MoveWithCollision(ref Position, ref Velocity, world, dt, out _againstWall);
    }
}
