using System.Collections.Generic;
using Microsoft.Xna.Framework.Graphics;

namespace MinecraftClone.Rendering;

/// <summary>Draws all chunk meshes with a shared effect.</summary>
public class WorldRenderer
{
    private readonly BasicEffect _effect;

    public WorldRenderer(GraphicsDevice device)
    {
        _effect = new BasicEffect(device)
        {
            VertexColorEnabled = true,
            LightingEnabled = false,
        };
    }

    public void Draw(GraphicsDevice device, FirstPersonCamera camera, IEnumerable<ChunkMesh> meshes)
    {
        device.DepthStencilState = DepthStencilState.Default;
        device.BlendState = BlendState.Opaque;
        device.RasterizerState = RasterizerState.CullCounterClockwise;

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
