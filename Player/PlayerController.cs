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
    private const float FlySpeed = 12f;
    private const float FlyVerticalSpeed = 10f;
    private const float JumpVelocity = 8.5f;
    private const float Gravity = -25f;
    private const float TerminalVelocity = -50f;
    public const float EyeHeight = 1.62f;

    private KeyboardState _previousKeyboard;

    /// <summary>Feet position (center of the AABB footprint).</summary>
    public Vector3 Position;
    public Vector3 Velocity;
    public bool IsFlying { get; private set; }
    public bool IsOnGround { get; private set; }

    public Vector3 EyePosition => Position + new Vector3(0f, EyeHeight, 0f);

    public PlayerController(Vector3 spawnPosition) => Position = spawnPosition;

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
            Velocity.X = wish.X * WalkSpeed;
            Velocity.Z = wish.Z * WalkSpeed;
            Velocity.Y = MathF.Max(Velocity.Y + Gravity * dt, TerminalVelocity);
            if (IsOnGround && keyboard.IsKeyDown(Keys.Space))
                Velocity.Y = JumpVelocity;
        }

        IsOnGround = PlayerPhysics.MoveWithCollision(ref Position, ref Velocity, world, dt);
    }
}
