using System;
using Microsoft.Xna.Framework;
using MinecraftClone.World;

namespace MinecraftClone.Player;

/// <summary>
/// AABB-vs-blocks collision. The player is a 0.6 x 1.8 x 0.6 box; Position is
/// the feet center. Movement is resolved one axis at a time (Y, then X, then Z),
/// which handles corners and 1-block steps the classic Minecraft way. Per-frame
/// deltas are well under one block at our speeds, so penetration resolution
/// after moving is sufficient — no swept test needed.
/// </summary>
public static class PlayerPhysics
{
    public const float HalfWidth = 0.3f;
    public const float Height = 1.8f;

    // Gap kept between the box and block faces so floating-point drift never
    // re-embeds the player in the geometry it was just pushed out of.
    private const float Skin = 0.001f;

    /// <param name="hitWall">True when the move was blocked horizontally.</param>
    /// <returns>True when the player ended the move standing on solid ground.</returns>
    public static bool MoveWithCollision(ref Vector3 position, ref Vector3 velocity, ChunkManager world, float dt, out bool hitWall)
    {
        bool onGround = false;
        hitWall = false;
        MoveAxis(ref position.Y, ref velocity.Y, velocity.Y * dt, Axis.Y, ref position, world, ref onGround, ref hitWall);
        MoveAxis(ref position.X, ref velocity.X, velocity.X * dt, Axis.X, ref position, world, ref onGround, ref hitWall);
        MoveAxis(ref position.Z, ref velocity.Z, velocity.Z * dt, Axis.Z, ref position, world, ref onGround, ref hitWall);
        return onGround;
    }

    private enum Axis { X, Y, Z }

    private static void MoveAxis(ref float positionAxis, ref float velocityAxis, float delta, Axis axis, ref Vector3 position, ChunkManager world, ref bool onGround, ref bool hitWall)
    {
        if (delta == 0f)
            return;

        positionAxis += delta;

        var min = new Vector3(position.X - HalfWidth, position.Y, position.Z - HalfWidth);
        var max = new Vector3(position.X + HalfWidth, position.Y + Height, position.Z + HalfWidth);

        // Blocks overlapped by the box. The tiny epsilon keeps a box whose face
        // sits exactly on a block boundary from counting the cell beyond it.
        int x0 = (int)MathF.Floor(min.X), x1 = (int)MathF.Floor(max.X - 1e-5f);
        int y0 = (int)MathF.Floor(min.Y), y1 = (int)MathF.Floor(max.Y - 1e-5f);
        int z0 = (int)MathF.Floor(min.Z), z1 = (int)MathF.Floor(max.Z - 1e-5f);

        bool hit = false;
        // The face of the nearest colliding block, measured along the move axis.
        float bound = delta > 0 ? float.MaxValue : float.MinValue;

        for (int y = y0; y <= y1; y++)
        {
            for (int z = z0; z <= z1; z++)
            {
                for (int x = x0; x <= x1; x++)
                {
                    if (!BlockInfo.IsSolid(world.GetBlock(x, y, z)))
                        continue;

                    hit = true;
                    float cell = axis switch { Axis.X => x, Axis.Y => y, _ => z };
                    bound = delta > 0 ? MathF.Min(bound, cell) : MathF.Max(bound, cell + 1f);
                }
            }
        }

        if (!hit)
            return;

        // Snap the box flush against the blocking face and stop on this axis.
        float minOffset = axis == Axis.Y ? 0f : HalfWidth;  // box min relative to position
        float maxOffset = axis == Axis.Y ? Height : HalfWidth;
        positionAxis = delta > 0
            ? bound - maxOffset - Skin
            : bound + minOffset + Skin;

        if (axis == Axis.Y && delta < 0)
            onGround = true;
        if (axis != Axis.Y)
            hitWall = true;
        velocityAxis = 0f;
    }
}
