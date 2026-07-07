using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace MinecraftClone.Rendering;

/// <summary>
/// Draws all chunk meshes with a shared effect: an opaque pass, then a
/// transparent water pass. Chunks outside the view frustum are skipped, and
/// distance fog in the sky color hides chunk pop-in at the load radius.
/// </summary>
public class WorldRenderer
{
    private const float FogStart = 70f;
    private const float FogEnd = 122f; // just inside the mesh radius (8 chunks = 128 blocks)

    private readonly BasicEffect _effect;
    private readonly AlphaTestEffect _cutoutEffect;
    private readonly List<ChunkMesh> _visible = new();

    public WorldRenderer(GraphicsDevice device, TextureAtlas atlas)
    {
        _effect = new BasicEffect(device)
        {
            VertexColorEnabled = true,
            LightingEnabled = false,
            TextureEnabled = true,
            Texture = atlas.Texture,
            FogEnabled = true,
            FogColor = Color.CornflowerBlue.ToVector3(), // must match the clear color
            FogStart = FogStart,
            FogEnd = FogEnd,
        };

        // Cutout (flowers): binary-alpha geometry that writes depth, so it
        // needs alpha *testing* rather than blending.
        _cutoutEffect = new AlphaTestEffect(device)
        {
            VertexColorEnabled = true,
            Texture = atlas.Texture,
            AlphaFunction = CompareFunction.Greater,
            ReferenceAlpha = 128,
            FogEnabled = true,
            FogColor = Color.CornflowerBlue.ToVector3(),
            FogStart = FogStart,
            FogEnd = FogEnd,
        };
    }

    public void Draw(GraphicsDevice device, FirstPersonCamera camera, IEnumerable<ChunkMesh> meshes)
    {
        _effect.View = camera.View;
        _effect.Projection = camera.Projection;

        var frustum = new BoundingFrustum(camera.View * camera.Projection);
        _visible.Clear();
        foreach (var mesh in meshes)
            if (!mesh.IsEmpty && frustum.Intersects(mesh.Bounds))
                _visible.Add(mesh);

        device.DepthStencilState = DepthStencilState.Default;
        device.BlendState = BlendState.Opaque;
        device.RasterizerState = RasterizerState.CullCounterClockwise;
        device.SamplerStates[0] = SamplerState.PointClamp; // crisp pixels, no atlas bleed

        foreach (var mesh in _visible)
        {
            if (!mesh.HasOpaque)
                continue;
            _effect.World = mesh.World;
            foreach (var pass in _effect.CurrentTechnique.Passes)
            {
                pass.Apply();
                mesh.DrawOpaque(device);
            }
        }

        // Cutout pass (flowers): depth-writing like opaque, but double-sided.
        _cutoutEffect.View = camera.View;
        _cutoutEffect.Projection = camera.Projection;
        device.RasterizerState = RasterizerState.CullNone;

        foreach (var mesh in _visible)
        {
            if (!mesh.HasCutout)
                continue;
            _cutoutEffect.World = mesh.World;
            foreach (var pass in _cutoutEffect.CurrentTechnique.Passes)
            {
                pass.Apply();
                mesh.DrawCutout(device);
            }
        }

        device.RasterizerState = RasterizerState.CullCounterClockwise;

        // Water: blended, reads depth but doesn't write it, so overlapping
        // water faces never punch holes in each other.
        device.BlendState = BlendState.NonPremultiplied;
        device.DepthStencilState = DepthStencilState.DepthRead;

        foreach (var mesh in _visible)
        {
            if (!mesh.HasWater)
                continue;
            _effect.World = mesh.World;
            foreach (var pass in _effect.CurrentTechnique.Passes)
            {
                pass.Apply();
                mesh.DrawWater(device);
            }
        }

        device.BlendState = BlendState.Opaque;
        device.DepthStencilState = DepthStencilState.Default;
    }
}
