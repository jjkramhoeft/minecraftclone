using System.Collections.Generic;
using Microsoft.Xna.Framework.Graphics;

namespace MinecraftClone.Rendering;

/// <summary>Draws all chunk meshes with a shared effect.</summary>
public class WorldRenderer
{
    private readonly BasicEffect _effect;

    public WorldRenderer(GraphicsDevice device, TextureAtlas atlas)
    {
        _effect = new BasicEffect(device)
        {
            VertexColorEnabled = true,
            LightingEnabled = false,
            TextureEnabled = true,
            Texture = atlas.Texture,
        };
    }

    public void Draw(GraphicsDevice device, FirstPersonCamera camera, IEnumerable<ChunkMesh> meshes)
    {
        device.DepthStencilState = DepthStencilState.Default;
        device.BlendState = BlendState.Opaque;
        device.RasterizerState = RasterizerState.CullCounterClockwise;
        device.SamplerStates[0] = SamplerState.PointClamp; // crisp pixels, no atlas bleed

        _effect.View = camera.View;
        _effect.Projection = camera.Projection;

        foreach (var mesh in meshes)
        {
            if (mesh.IsEmpty)
                continue;

            _effect.World = mesh.World;
            foreach (var pass in _effect.CurrentTechnique.Passes)
            {
                pass.Apply();
                mesh.Draw(device);
            }
        }
    }
}
