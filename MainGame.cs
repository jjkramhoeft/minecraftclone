using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using MinecraftClone.Rendering;
using MinecraftClone.World;

namespace MinecraftClone;

public class MainGame : Game
{
    private const float FlySpeed = 12f;
    private const int WorldSeed = 12345;

    private GraphicsDeviceManager _graphics;
    private SpriteBatch _spriteBatch;
    private FirstPersonCamera _camera;
    private WorldRenderer _worldRenderer;
    private ChunkMesh _chunkMesh;

    // False whenever the window lost focus, so the first recentering of the
    // mouse doesn't register as a huge look delta.
    private bool _mouseCaptured;

    public MainGame()
    {
        _graphics = new GraphicsDeviceManager(this);
        _graphics.PreferredBackBufferWidth = 1280;
        _graphics.PreferredBackBufferHeight = 720;
        Content.RootDirectory = "Content";
        IsMouseVisible = false;
        Window.Title = "Minecraft Clone";
    }

    protected override void Initialize()
    {
        _camera = new FirstPersonCamera
        {
            Position = new Vector3(8f, 68f, -14f), // above and south of the chunk, looking in
            Yaw = 0f,
            Pitch = -0.5f,
        };
        _camera.UpdateProjection(GraphicsDevice.Viewport.AspectRatio);

        base.Initialize();
    }

    protected override void LoadContent()
    {
        _spriteBatch = new SpriteBatch(GraphicsDevice);
        _worldRenderer = new WorldRenderer(GraphicsDevice);

        // Phase 2: a single chunk at the origin. Phase 3 moves this into ChunkManager.
        var generator = new TerrainGenerator(WorldSeed);
        var chunk = new Chunk(new ChunkCoord(0, 0));
        generator.Generate(chunk);
        var meshData = ChunkMesher.Build(chunk, (x, y, z) => BlockType.Air);
        _chunkMesh = new ChunkMesh(GraphicsDevice, chunk.Coord, meshData);
    }

    protected override void Update(GameTime gameTime)
    {
        var keyboard = Keyboard.GetState();
        if (GamePad.GetState(PlayerIndex.One).Buttons.Back == ButtonState.Pressed || keyboard.IsKeyDown(Keys.Escape))
            Exit();

        float dt = (float)gameTime.ElapsedGameTime.TotalSeconds;
        UpdateMouseLook();
        UpdateFlyMovement(keyboard, dt);

        base.Update(gameTime);
    }

    private void UpdateMouseLook()
    {
        if (!IsActive)
        {
            _mouseCaptured = false;
            return;
        }

        int centerX = Window.ClientBounds.Width / 2;
        int centerY = Window.ClientBounds.Height / 2;
        var mouse = Mouse.GetState();

        if (_mouseCaptured)
            _camera.Look(mouse.X - centerX, mouse.Y - centerY);

        Mouse.SetPosition(centerX, centerY);
        _mouseCaptured = true;
    }

    private void UpdateFlyMovement(KeyboardState keyboard, float dt)
    {
        var move = Vector3.Zero;
        if (keyboard.IsKeyDown(Keys.W)) move += _camera.HorizontalForward;
        if (keyboard.IsKeyDown(Keys.S)) move -= _camera.HorizontalForward;
        if (keyboard.IsKeyDown(Keys.D)) move += _camera.Right;
        if (keyboard.IsKeyDown(Keys.A)) move -= _camera.Right;
        if (move != Vector3.Zero) move.Normalize();
        if (keyboard.IsKeyDown(Keys.Space)) move += Vector3.Up;
        if (keyboard.IsKeyDown(Keys.LeftShift)) move -= Vector3.Up;

        _camera.Position += move * FlySpeed * dt;
    }

    protected override void Draw(GameTime gameTime)
    {
        GraphicsDevice.Clear(Color.CornflowerBlue);

        _worldRenderer.Draw(GraphicsDevice, _camera, new[] { _chunkMesh });

        base.Draw(gameTime);
    }
}
