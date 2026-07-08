using System;
using Microsoft.Xna.Framework;

namespace MinecraftClone.Rendering;

public class FirstPersonCamera
{
    private const float MouseSensitivity = 0.0025f;
    private const float MaxPitch = MathHelper.PiOver2 - 0.01f;
    private const float FieldOfView = 70f;
    private const float SprintFovBoost = 8f; // extra degrees while sprinting

    private float _aspectRatio = 1f;
    private float _fov = FieldOfView; // eased toward the sprint target each frame

    /// <summary>Set by the game loop each frame; the FOV kicks out while true.</summary>
    public bool SprintFovActive { get; set; }

    public Vector3 Position { get; set; }

    /// <summary>Over-the-shoulder mode: the camera hangs back along -Forward
    /// from the player's eye (V toggles). Look math is unchanged.</summary>
    public bool ThirdPerson { get; set; }

    public float ThirdPersonDistance { get; set; } = 3.5f;

    /// <summary>Rotation around the Y axis in radians. 0 looks toward +Z; increasing yaw turns left.</summary>
    public float Yaw { get; set; }

    /// <summary>Rotation up/down in radians, clamped just short of straight up/down.</summary>
    public float Pitch { get; set; }

    public Matrix Projection { get; private set; }

    public Vector3 Forward => new(
        MathF.Cos(Pitch) * MathF.Sin(Yaw),
        MathF.Sin(Pitch),
        MathF.Cos(Pitch) * MathF.Cos(Yaw));

    /// <summary>Forward projected onto the XZ plane — the direction walking/flying moves on W.</summary>
    public Vector3 HorizontalForward => new(MathF.Sin(Yaw), 0f, MathF.Cos(Yaw));

    /// <summary>Horizontal right direction regardless of pitch.</summary>
    public Vector3 Right => Vector3.Normalize(Vector3.Cross(Forward, Vector3.Up));

    public Matrix View => Matrix.CreateLookAt(Position, Position + Forward, Vector3.Up);

    public void UpdateProjection(float aspectRatio)
    {
        _aspectRatio = aspectRatio;
        RebuildProjection();
    }

    /// <summary>Eases the field of view toward the sprint target and rebuilds
    /// the projection. Called every frame; the exp-based lerp is frame-rate
    /// independent so the kick feels the same at any fps.</summary>
    public void UpdateFov(float dt)
    {
        float target = FieldOfView + (SprintFovActive ? SprintFovBoost : 0f);
        _fov = MathHelper.Lerp(_fov, target, 1f - MathF.Exp(-dt * 9f));
        RebuildProjection();
    }

    private void RebuildProjection()
    {
        Projection = Matrix.CreatePerspectiveFieldOfView(
            MathHelper.ToRadians(_fov), _aspectRatio, 0.1f, 1000f);
    }

    public void Look(float mouseDeltaX, float mouseDeltaY)
    {
        Yaw -= mouseDeltaX * MouseSensitivity;
        Pitch = MathHelper.Clamp(Pitch - mouseDeltaY * MouseSensitivity, -MaxPitch, MaxPitch);
    }
}
