using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace MinecraftClone.Rendering;

/// <summary>
/// Draws all chunk meshes: an opaque terrain pass and a torch-light pass with
/// the custom TerrainEffect (greedy quads need per-block texture wrapping),
/// then cutout and transparent water passes with the stock effects. Chunks
/// outside the view frustum are skipped, and distance fog in the sky color
/// hides chunk pop-in at the load radius.
/// </summary>
public class WorldRenderer
{
    private const float FogStart = 70f;
    private const float FogEnd = 122f; // just inside the mesh radius (8 chunks = 128 blocks)

    private static readonly Vector3 TorchTint = new(1f, 0.85f, 0.6f);

    private readonly Effect _terrainEffect;
    private readonly EffectParameter _terrainWorld;
    private readonly EffectParameter _terrainViewProjection;
    private readonly EffectParameter _terrainCameraPosition;
    private readonly EffectParameter _terrainDiffuse;
    private readonly EffectParameter _terrainFogColor;
    private readonly BasicEffect _effect;          // water
    private readonly AlphaTestEffect _cutoutEffect;
    private readonly List<ChunkMesh> _visible = new();

    private Vector3 _light = Vector3.One;
    private Vector3 _sky = Color.CornflowerBlue.ToVector3();

    // Torch light must survive night dimming, so it goes in a second pass
    // blended with max(): the frame keeps whichever is brighter, day light or
    // torch light — the classic max(skyTint, blockLight) without extra passes.
    private static readonly BlendState MaxBlend = new()
    {
        ColorBlendFunction = BlendFunction.Max,
        ColorSourceBlend = Blend.One,
        ColorDestinationBlend = Blend.One,
        AlphaBlendFunction = BlendFunction.Max,
        AlphaSourceBlend = Blend.One,
        AlphaDestinationBlend = Blend.One,
    };

    public WorldRenderer(GraphicsDevice device, TextureAtlas atlas, Effect terrainEffect)
    {
        _terrainEffect = terrainEffect;
        _terrainEffect.Parameters["AtlasTexture"].SetValue(atlas.Texture);
        _terrainEffect.Parameters["FogStart"].SetValue(FogStart);
        _terrainEffect.Parameters["FogEnd"].SetValue(FogEnd);
        // All tiles sample the same inset span (see TextureAtlas.GetUVBounds).
        _terrainEffect.Parameters["TileSpan"].SetValue(
            new Vector2((TextureAtlas.TileSize - 1f) / TextureAtlas.AtlasSize));
        _terrainWorld = _terrainEffect.Parameters["World"];
        _terrainViewProjection = _terrainEffect.Parameters["ViewProjection"];
        _terrainCameraPosition = _terrainEffect.Parameters["CameraPosition"];
        _terrainDiffuse = _terrainEffect.Parameters["DiffuseColor"];
        _terrainFogColor = _terrainEffect.Parameters["FogColor"];

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

    /// <summary>Per-frame scene light (day/night dimming) and fog/sky color.</summary>
    public void SetEnvironment(Vector3 light, Color sky)
    {
        _light = light;
        _sky = sky.ToVector3();
        _effect.DiffuseColor = light;
        _effect.FogColor = _sky;
        _cutoutEffect.DiffuseColor = light;
        _cutoutEffect.FogColor = _sky;
    }

    public void Draw(GraphicsDevice device, FirstPersonCamera camera, IEnumerable<ChunkMesh> meshes)
    {
        var frustum = new BoundingFrustum(camera.View * camera.Projection);
        _visible.Clear();
        foreach (var mesh in meshes)
            if (!mesh.IsEmpty && frustum.Intersects(mesh.Bounds))
                _visible.Add(mesh);

        device.DepthStencilState = DepthStencilState.Default;
        device.BlendState = BlendState.Opaque;
        device.RasterizerState = RasterizerState.CullCounterClockwise;
        device.SamplerStates[0] = SamplerState.PointClamp; // crisp pixels, no atlas bleed

        _terrainViewProjection.SetValue(camera.View * camera.Projection);
        _terrainCameraPosition.SetValue(camera.Position);
        _terrainDiffuse.SetValue(_light);
        _terrainFogColor.SetValue(_sky);

        var terrainPass = _terrainEffect.CurrentTechnique.Passes[0];
        foreach (var mesh in _visible)
        {
            if (!mesh.HasOpaque)
                continue;
            _terrainWorld.SetValue(mesh.World);
            terrainPass.Apply();
            mesh.DrawOpaque(device);
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
        // the torch cross-quads double-sided. Fog fades to black so distant
        // torch light contributes nothing (max with 0 is a no-op); the warm
        // tint comes from the diffuse color.
        device.BlendState = MaxBlend;
        device.DepthStencilState = DepthStencilState.DepthRead;
        _terrainDiffuse.SetValue(TorchTint);
        _terrainFogColor.SetValue(Vector3.Zero);

        foreach (var mesh in _visible)
        {
            if (!mesh.HasLight)
                continue;
            _terrainWorld.SetValue(mesh.World);
            terrainPass.Apply();
            mesh.DrawLight(device);
        }

        device.RasterizerState = RasterizerState.CullCounterClockwise;

        // Water: blended, reads depth but doesn't write it, so overlapping
        // water faces never punch holes in each other.
        device.BlendState = BlendState.NonPremultiplied;
        device.DepthStencilState = DepthStencilState.DepthRead;
        _effect.View = camera.View;
        _effect.Projection = camera.Projection;

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
