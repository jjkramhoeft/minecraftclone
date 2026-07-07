using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using MinecraftClone.Player;
using MinecraftClone.Rendering;
using MinecraftClone.UI;
using MinecraftClone.World;

namespace MinecraftClone;

public class MainGame : Game
{
    private const int WorldSeed = 12345;
    private const float Reach = 5f;

    private GraphicsDeviceManager _graphics;
    private SpriteBatch _spriteBatch;
    private FirstPersonCamera _camera;
    private WorldRenderer _worldRenderer;
    private ChunkManager _chunkManager;
    private PlayerController _player;
    private BlockHighlight _blockHighlight;
    private Hud _hud;

    private MouseState _previousMouse;
    private RaycastHit? _targetedBlock;

    private double _titleTimer;
    private int _frames;

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
        _camera = new FirstPersonCamera { Yaw = 0f, Pitch = 0f };
        _camera.UpdateProjection(GraphicsDevice.Viewport.AspectRatio);

        // Spawn above the highest possible terrain; the player falls to the
        // ground once the spawn chunk has loaded.
        _player = new PlayerController(new Vector3(8.5f, 70f, 8.5f));

        base.Initialize();
    }

    protected override void LoadContent()
    {
        _spriteBatch = new SpriteBatch(GraphicsDevice);
        _worldRenderer = new WorldRenderer(GraphicsDevice);
        _chunkManager = new ChunkManager(GraphicsDevice, new TerrainGenerator(WorldSeed));
        _blockHighlight = new BlockHighlight(GraphicsDevice);
        _hud = new Hud(GraphicsDevice);
    }

    protected override void Update(GameTime gameTime)
    {
        var keyboard = Keyboard.GetState();
        if (GamePad.GetState(PlayerIndex.One).Buttons.Back == ButtonState.Pressed || keyboard.IsKeyDown(Keys.Escape))
            Exit();

        float dt = (float)gameTime.ElapsedGameTime.TotalSeconds;
        UpdateMouseLook();
        _player.Update(keyboard, _camera, _chunkManager, dt);
        _camera.Position = _player.EyePosition;
        UpdateBlockInteraction();
        _chunkManager.Update(_player.Position);

        base.Update(gameTime);
    }

    private void UpdateBlockInteraction()
    {
        var mouse = Mouse.GetState();
        _targetedBlock = VoxelRaycaster.Cast(_chunkManager, _camera.Position, _camera.Forward, Reach, out var hit)
            ? hit
            : null;

        if (IsActive && _mouseCaptured && _targetedBlock is { } target)
        {
            bool leftClick = mouse.LeftButton == ButtonState.Pressed && _previousMouse.LeftButton == ButtonState.Released;
            bool rightClick = mouse.RightButton == ButtonState.Pressed && _previousMouse.RightButton == ButtonState.Released;

            if (leftClick)
            {
                _chunkManager.SetBlock(target.X, target.Y, target.Z, BlockType.Air);
            }
            else if (rightClick && (target.NormalX != 0 || target.NormalY != 0 || target.NormalZ != 0))
            {
                int x = target.X + target.NormalX;
                int y = target.Y + target.NormalY;
                int z = target.Z + target.NormalZ;
                if (_chunkManager.GetBlock(x, y, z) == BlockType.Air && !IntersectsPlayer(x, y, z))
                    _chunkManager.SetBlock(x, y, z, BlockType.Dirt); // Phase 6 replaces with the hotbar selection
            }
        }

        _previousMouse = mouse;
    }

    private bool IntersectsPlayer(int x, int y, int z)
    {
        var p = _player.Position;
        return x + 1 > p.X - PlayerPhysics.HalfWidth && x < p.X + PlayerPhysics.HalfWidth
            && y + 1 > p.Y && y < p.Y + PlayerPhysics.Height
            && z + 1 > p.Z - PlayerPhysics.HalfWidth && z < p.Z + PlayerPhysics.HalfWidth;
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

    protected override void Draw(GameTime gameTime)
    {
        GraphicsDevice.Clear(Color.CornflowerBlue);

        _worldRenderer.Draw(GraphicsDevice, _camera, _chunkManager.Meshes);
        if (_targetedBlock is { } target)
            _blockHighlight.Draw(GraphicsDevice, _camera, target.X, target.Y, target.Z);
        _hud.Draw(_spriteBatch, GraphicsDevice.Viewport.Width, GraphicsDevice.Viewport.Height);

        UpdateDebugTitle(gameTime);
        base.Draw(gameTime);
    }

    private void UpdateDebugTitle(GameTime gameTime)
    {
        _frames++;
        _titleTimer += gameTime.ElapsedGameTime.TotalSeconds;
        if (_titleTimer < 1.0)
            return;

        var pos = _player.Position;
        string mode = _player.IsFlying ? "fly" : _player.IsOnGround ? "walk" : "air";
        Window.Title = $"Minecraft Clone — {_frames} fps | {_chunkManager.LoadedChunkCount} chunks ({_chunkManager.PendingCount} pending) | {pos.X:0.#}, {pos.Y:0.#}, {pos.Z:0.#} [{mode}]";
        _frames = 0;
        _titleTimer = 0;
    }

    protected override void UnloadContent()
    {
        _chunkManager.Dispose();
        base.UnloadContent();
    }
}
