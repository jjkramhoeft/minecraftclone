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
    private readonly BasicEffect _lightEffect;
    private readonly List<ChunkMesh> _visible = new();

    // Torch light must survive night dimming, so it goes in a second pass
    // blended with max(): the frame keeps whichever is brighter, day light or
    // torch light — the classic max(skyTint, blockLight) without a custom shader.
    private static readonly BlendState MaxBlend = new()
    {
        ColorBlendFunction = BlendFunction.Max,
        ColorSourceBlend = Blend.One,
        ColorDestinationBlend = Blend.One,
        AlphaBlendFunction = BlendFunction.Max,
        AlphaSourceBlend = Blend.One,
        AlphaDestinationBlend = Blend.One,
    };

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

        // Fog fades to black here so distant torch light contributes nothing
        // (max with 0 is a no-op); the warm tint comes from DiffuseColor.
        _lightEffect = new BasicEffect(device)
        {
            VertexColorEnabled = true,
            LightingEnabled = false,
            TextureEnabled = true,
            Texture = atlas.Texture,
            DiffuseColor = new Vector3(1f, 0.85f, 0.6f),
            FogEnabled = true,
            FogColor = Vector3.Zero,
            FogStart = FogStart,
            FogEnd = FogEnd,
        };
    }

    /// <summary>Per-frame scene light (day/night dimming) and fog/sky color.</summary>
    public void SetEnvironment(Vector3 light, Color sky)
    {
        _effect.DiffuseColor = light;
        _effect.FogColor = sky.ToVector3();
        _cutoutEffect.DiffuseColor = light;
        _cutoutEffect.FogColor = sky.ToVector3();
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

        // Torch-light pass: re-draws lit faces (and glowing torches) with
        // max() blending on top of the day-lit result. Depth-read with the
        // LessEqual default lets the duplicate geometry pass; CullNone keeps
        // the torch cross-quads double-sided.
        _lightEffect.View = camera.View;
        _lightEffect.Projection = camera.Projection;
        device.BlendState = MaxBlend;
        device.DepthStencilState = DepthStencilState.DepthRead;

        foreach (var mesh in _visible)
        {
            if (!mesh.HasLight)
                continue;
            _lightEffect.World = mesh.World;
            foreach (var pass in _lightEffect.CurrentTechnique.Passes)
            {
                pass.Apply();
                mesh.DrawLight(device);
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
