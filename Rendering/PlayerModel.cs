using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using MinecraftClone.Items;
using MinecraftClone.Player;
using MinecraftClone.World;

namespace MinecraftClone.Rendering;

/// <summary>
/// The blocky player character: six textured boxes (head, torso, arms, legs)
/// composed with plain matrix multiplication — each part's vertices are
/// authored around its pivot, so part = rotation * translate(pivot) * body.
/// Third person draws the whole body; first person draws only the right arm,
/// positioned in camera space after a depth-buffer clear so it never clips
/// into walls.
/// </summary>
public class PlayerModel
{
    private const float WalkSwingAmplitude = 0.7f;
    private const float SwingDuration = 0.3f;

    // Same fake-lighting values as the terrain mesher.
    private static readonly float[] FaceShade = { 1f, 0.5f, 0.8f, 0.8f, 0.65f, 0.65f };

    private static readonly Vector3[][] FaceCorners =
    {
        new[] { new Vector3(0, 1, 0), new Vector3(1, 1, 0), new Vector3(1, 1, 1), new Vector3(0, 1, 1) }, // Top
        new[] { new Vector3(1, 0, 0), new Vector3(0, 0, 0), new Vector3(0, 0, 1), new Vector3(1, 0, 1) }, // Bottom
        new[] { new Vector3(0, 0, 0), new Vector3(1, 0, 0), new Vector3(1, 1, 0), new Vector3(0, 1, 0) }, // North (-Z)
        new[] { new Vector3(1, 0, 1), new Vector3(0, 0, 1), new Vector3(0, 1, 1), new Vector3(1, 1, 1) }, // South (+Z, the front)
        new[] { new Vector3(1, 0, 0), new Vector3(1, 0, 1), new Vector3(1, 1, 1), new Vector3(1, 1, 0) }, // East
        new[] { new Vector3(0, 0, 1), new Vector3(0, 0, 0), new Vector3(0, 1, 0), new Vector3(0, 1, 1) }, // West
    };

    private record Part(VertexPositionColorTexture[] Vertices, short[] Indices, Vector3 Pivot);

    private readonly GraphicsDevice _device;
    private readonly BasicEffect _effect;
    private readonly Part _head, _torso, _rightArm, _leftArm, _rightLeg, _leftLeg;

    private float _walkPhase;
    private float _walkBlend; // eases limbs back to rest when stopping
    private float _swingTimer;

    // Reused geometry for the flat held-item sprite (tools/bucket) — mutated in
    // place each frame so first-person drawing stays allocation-free.
    private readonly VertexPositionColorTexture[] _itemQuad = new VertexPositionColorTexture[4];
    private static readonly short[] ItemQuadIndices = { 0, 1, 2, 0, 2, 3 };

    public PlayerModel(GraphicsDevice device, TextureAtlas atlas)
    {
        _device = device;
        _effect = new BasicEffect(device)
        {
            VertexColorEnabled = true,
            LightingEnabled = false,
            TextureEnabled = true,
            Texture = atlas.Texture,
            FogEnabled = true,
            FogColor = Color.CornflowerBlue.ToVector3(),
            FogStart = 70f,
            FogEnd = 122f,
        };

        int skin = BlockInfo.TileSkin, shirt = BlockInfo.TileShirt, pants = BlockInfo.TilePants;
        var headTiles = new[] { skin, skin, skin, BlockInfo.TileFace, skin, skin }; // face on +Z
        _head = BuildBox(new Vector3(-0.25f, 0f, -0.25f), new Vector3(0.25f, 0.5f, 0.25f), headTiles, new Vector3(0f, 1.5f, 0f));
        _torso = BuildBox(new Vector3(-0.25f, 0f, -0.125f), new Vector3(0.25f, 0.75f, 0.125f), Uniform(shirt), new Vector3(0f, 0.75f, 0f));
        _rightArm = BuildBox(new Vector3(-0.125f, -0.75f, -0.125f), new Vector3(0.125f, 0f, 0.125f), Uniform(skin), new Vector3(0.375f, 1.45f, 0f));
        _leftArm = BuildBox(new Vector3(-0.125f, -0.75f, -0.125f), new Vector3(0.125f, 0f, 0.125f), Uniform(skin), new Vector3(-0.375f, 1.45f, 0f));
        _rightLeg = BuildBox(new Vector3(-0.125f, -0.75f, -0.125f), new Vector3(0.125f, 0f, 0.125f), Uniform(pants), new Vector3(0.125f, 0.75f, 0f));
        _leftLeg = BuildBox(new Vector3(-0.125f, -0.75f, -0.125f), new Vector3(0.125f, 0f, 0.125f), Uniform(pants), new Vector3(-0.125f, 0.75f, 0f));
    }

    /// <summary>Per-frame scene light (day/night dimming) and fog/sky color.</summary>
    public void SetEnvironment(Vector3 light, Color sky)
    {
        _effect.DiffuseColor = light;
        _effect.FogColor = sky.ToVector3();
    }

    /// <summary>Starts the mine/place arm swing.</summary>
    public void TriggerSwing() => _swingTimer = SwingDuration;

    public void Update(PlayerController player, float dt, bool miningHeld)
    {
        float speed = new Vector2(player.Velocity.X, player.Velocity.Z).Length();
        _walkPhase += speed * 2.2f * dt;
        _walkBlend = MathHelper.Lerp(_walkBlend, MathHelper.Clamp(speed / 4.5f, 0f, 1f), Math.Min(1f, dt * 10f));

        if (_swingTimer > 0f)
            _swingTimer -= dt;
        else if (miningHeld)
            _swingTimer = SwingDuration; // keep swinging while the mouse is held on a block
    }

    private float WalkSwing => MathF.Sin(_walkPhase) * WalkSwingAmplitude * _walkBlend;

    // A quick punch arc: 0 → -2.1 rad → 0 over the swing duration.
    private float SwingAngle => _swingTimer <= 0f
        ? 0f
        : -2.1f * MathF.Sin((1f - _swingTimer / SwingDuration) * MathF.PI);

    public void DrawBody(FirstPersonCamera camera, Vector3 feetPosition, float yaw, float pitch)
    {
        SetStates();
        var body = Matrix.CreateRotationY(yaw) * Matrix.CreateTranslation(feetPosition);
        float walk = WalkSwing;
        float rightArmAngle = _swingTimer > 0f ? SwingAngle : walk;

        DrawPart(_head, Matrix.CreateRotationX(-MathHelper.Clamp(pitch, -1.4f, 1.4f)), body, camera);
        DrawPart(_torso, Matrix.Identity, body, camera);
        DrawPart(_rightArm, Matrix.CreateRotationX(rightArmAngle), body, camera);
        DrawPart(_leftArm, Matrix.CreateRotationX(-walk), body, camera);
        DrawPart(_rightLeg, Matrix.CreateRotationX(-walk), body, camera);
        DrawPart(_leftLeg, Matrix.CreateRotationX(walk), body, camera);
    }

    public void DrawFirstPersonArm(FirstPersonCamera camera, ItemType heldItem)
    {
        // Fresh depth so the arm draws over the world and never clips into it.
        _device.Clear(ClearOptions.DepthBuffer, Color.CornflowerBlue, 1f, 0);
        SetStates();

        float bob = MathF.Sin(_walkPhase * 2f) * 0.03f * _walkBlend;
        var local = Matrix.CreateRotationX(1.25f - SwingAngle * 0.3f)
            * Matrix.CreateTranslation(0.38f, -0.42f + bob, -0.4f);
        var world = local * Matrix.Invert(camera.View);

        _effect.World = world;
        _effect.View = camera.View;
        _effect.Projection = camera.Projection;
        DrawGeometry(_rightArm);

        if (ItemInfo.IsHeldInHand(heldItem))
            DrawHeldItem(camera, heldItem, bob);
    }

    /// <summary>Draws the equipped tool/bucket as a flat, angled sprite gripped
    /// in the fist — a simple viewmodel rather than a voxelised item.</summary>
    private void DrawHeldItem(FirstPersonCamera camera, ItemType item, float bob)
    {
        var uv = TextureAtlas.GetUVBounds(ItemInfo.GetIconTile(item));
        // Upright unit quad in its own XY plane, wound CCW from bottom-left; the
        // matrix scales, tilts and drops it into the hand. v0 is the tile top.
        _itemQuad[0] = new VertexPositionColorTexture(new Vector3(-0.5f, -0.5f, 0f), Color.White, new Vector2(uv.X, uv.W));
        _itemQuad[1] = new VertexPositionColorTexture(new Vector3(0.5f, -0.5f, 0f), Color.White, new Vector2(uv.Z, uv.W));
        _itemQuad[2] = new VertexPositionColorTexture(new Vector3(0.5f, 0.5f, 0f), Color.White, new Vector2(uv.Z, uv.Y));
        _itemQuad[3] = new VertexPositionColorTexture(new Vector3(-0.5f, 0.5f, 0f), Color.White, new Vector2(uv.X, uv.Y));

        var local = Matrix.CreateScale(0.55f)
            * Matrix.CreateRotationZ(-0.6f)                       // handle points to lower-right
            * Matrix.CreateRotationY(0.4f)                        // slight turn for depth
            * Matrix.CreateRotationX(-SwingAngle * 0.25f)         // dips with the punch swing
            * Matrix.CreateTranslation(0.5f, -0.5f + bob, -0.9f); // into the fist
        _effect.World = local * Matrix.Invert(camera.View);

        // Sprite has a transparent background; blend it and don't cull the back.
        _device.BlendState = BlendState.AlphaBlend;
        _device.RasterizerState = RasterizerState.CullNone;
        foreach (var pass in _effect.CurrentTechnique.Passes)
        {
            pass.Apply();
            _device.DrawUserIndexedPrimitives(PrimitiveType.TriangleList, _itemQuad, 0, 4, ItemQuadIndices, 0, 2);
        }
        _device.BlendState = BlendState.Opaque;
        _device.RasterizerState = RasterizerState.CullCounterClockwise;
    }

    private void DrawPart(Part part, Matrix rotation, Matrix body, FirstPersonCamera camera)
    {
        _effect.World = rotation * Matrix.CreateTranslation(part.Pivot) * body;
        _effect.View = camera.View;
        _effect.Projection = camera.Projection;
        DrawGeometry(part);
    }

    private void DrawGeometry(Part part)
    {
        foreach (var pass in _effect.CurrentTechnique.Passes)
        {
            pass.Apply();
            _device.DrawUserIndexedPrimitives(PrimitiveType.TriangleList, part.Vertices, 0, part.Vertices.Length, part.Indices, 0, part.Indices.Length / 3);
        }
    }

    private void SetStates()
    {
        _device.BlendState = BlendState.Opaque;
        _device.DepthStencilState = DepthStencilState.Default;
        _device.RasterizerState = RasterizerState.CullCounterClockwise;
        _device.SamplerStates[0] = SamplerState.PointClamp;
    }

    private static int[] Uniform(int tile) => new[] { tile, tile, tile, tile, tile, tile };

    private static Part BuildBox(Vector3 min, Vector3 max, int[] faceTiles, Vector3 pivot)
    {
        var vertices = new VertexPositionColorTexture[24];
        var indices = new short[36];
        var size = max - min;

        for (int face = 0; face < 6; face++)
        {
            var uv = TextureAtlas.GetUVBounds(faceTiles[face]);
            byte shade = (byte)(255 * FaceShade[face]);
            var color = new Color(shade, shade, shade);
            var uvs = new Vector2[]
            {
                new(uv.X, uv.W), new(uv.Z, uv.W), new(uv.Z, uv.Y), new(uv.X, uv.Y),
            };

            for (int i = 0; i < 4; i++)
                vertices[face * 4 + i] = new VertexPositionColorTexture(min + FaceCorners[face][i] * size, color, uvs[i]);

            int v = face * 4, t = face * 6;
            indices[t + 0] = (short)(v + 0);
            indices[t + 1] = (short)(v + 1);
            indices[t + 2] = (short)(v + 2);
            indices[t + 3] = (short)(v + 0);
            indices[t + 4] = (short)(v + 2);
            indices[t + 5] = (short)(v + 3);
        }

        return new Part(vertices, indices, pivot);
    }
}
