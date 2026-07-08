using System;
using Microsoft.Xna.Framework;
using MinecraftClone.World;

namespace MinecraftClone.Player;

/// <summary>The block a ray hit, the face it entered through (unit normal),
/// and how far along the ray the hit is.</summary>
public readonly record struct RaycastHit(int X, int Y, int Z, int NormalX, int NormalY, int NormalZ, float Distance);

/// <summary>
/// Voxel ray marching using the Amanatides &amp; Woo DDA algorithm: steps the ray
/// from grid cell to grid cell, always crossing the nearest cell boundary next,
/// so no block along the ray is ever skipped.
/// </summary>
public static class VoxelRaycaster
{
    /// <param name="includeFlowers">False restricts hits to solid blocks —
    /// used by the third-person camera boom, which shouldn't stop at flowers.</param>
    /// <param name="includeWater">True also stops at water cells — used by
    /// buckets, which target the liquid itself.</param>
    public static bool Cast(ChunkManager world, Vector3 origin, Vector3 direction, float maxDistance, out RaycastHit hit, bool includeFlowers = true, bool includeWater = false)
    {
        int x = (int)MathF.Floor(origin.X);
        int y = (int)MathF.Floor(origin.Y);
        int z = (int)MathF.Floor(origin.Z);

        bool Hits(BlockType type) => (includeWater && BlockInfo.IsWater(type))
            || (includeFlowers ? BlockInfo.IsTargetable(type) : BlockInfo.IsSolid(type));

        // Degenerate but possible: the eye is inside a targetable block.
        if (Hits(world.GetBlock(x, y, z)))
        {
            hit = new RaycastHit(x, y, z, 0, 0, 0, 0f);
            return true;
        }

        int stepX = MathF.Sign(direction.X) >= 0 ? 1 : -1;
        int stepY = MathF.Sign(direction.Y) >= 0 ? 1 : -1;
        int stepZ = MathF.Sign(direction.Z) >= 0 ? 1 : -1;

        // t distance to the first boundary crossing per axis, and per-cell increment.
        float tMaxX = DistanceToBoundary(origin.X, direction.X, x);
        float tMaxY = DistanceToBoundary(origin.Y, direction.Y, y);
        float tMaxZ = DistanceToBoundary(origin.Z, direction.Z, z);
        float tDeltaX = MathF.Abs(direction.X) < 1e-8f ? float.PositiveInfinity : MathF.Abs(1f / direction.X);
        float tDeltaY = MathF.Abs(direction.Y) < 1e-8f ? float.PositiveInfinity : MathF.Abs(1f / direction.Y);
        float tDeltaZ = MathF.Abs(direction.Z) < 1e-8f ? float.PositiveInfinity : MathF.Abs(1f / direction.Z);

        while (true)
        {
            float t;
            int nx = 0, ny = 0, nz = 0;
            if (tMaxX < tMaxY && tMaxX < tMaxZ)
            {
                t = tMaxX;
                tMaxX += tDeltaX;
                x += stepX;
                nx = -stepX;
            }
            else if (tMaxY < tMaxZ)
            {
                t = tMaxY;
                tMaxY += tDeltaY;
                y += stepY;
                ny = -stepY;
            }
            else
            {
                t = tMaxZ;
                tMaxZ += tDeltaZ;
                z += stepZ;
                nz = -stepZ;
            }

            if (t > maxDistance)
            {
                hit = default;
                return false;
            }

            if (Hits(world.GetBlock(x, y, z)))
            {
                hit = new RaycastHit(x, y, z, nx, ny, nz, t);
                return true;
            }
        }
    }

    private static float DistanceToBoundary(float origin, float direction, int cell)
    {
        if (MathF.Abs(direction) < 1e-8f)
            return float.PositiveInfinity;
        float boundary = direction > 0 ? cell + 1 : cell;
        return (boundary - origin) / direction;
    }
}
