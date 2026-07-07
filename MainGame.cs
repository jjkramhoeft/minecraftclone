using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using MinecraftClone.Items;
using MinecraftClone.Persistence;
using MinecraftClone.Player;
using MinecraftClone.Rendering;
using MinecraftClone.UI;
using MinecraftClone.World;

namespace MinecraftClone;

public class MainGame : Game
{
    private const int DefaultSeed = 12345;
    private const float Reach = 5f;

    private GraphicsDeviceManager _graphics;
    private SpriteBatch _spriteBatch;
    private FirstPersonCamera _camera;
    private WorldRenderer _worldRenderer;
    private ChunkManager _chunkManager;
    private PlayerController _player;
    private BlockHighlight _blockHighlight;
    private Hud _hud;
    private TextureAtlas _atlas;
    private Hotbar _hotbar;
    private Inventory _inventory;
    private InventoryScreen _inventoryScreen;
    private PixelFont _font;

    private WorldSave _worldSave;
    private int _seed;

    private MouseState _previousMouse;
    private KeyboardState _previousKeyboard;
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
        _worldSave = new WorldSave();
        var meta = _worldSave.TryLoadMetadata();
        _seed = meta?.Seed ?? DefaultSeed;

        _inventory = new Inventory { SelectedIndex = meta?.HotbarIndex ?? 0 };
        if (meta?.Inventory != null)
        {
            foreach (var slot in meta.Inventory)
                if (slot.Slot >= 0 && slot.Slot < Inventory.Size)
                    _inventory[slot.Slot] = new ItemStack((ItemType)slot.Item, slot.Count);
        }

        _camera = new FirstPersonCamera { Yaw = meta?.Yaw ?? 0f, Pitch = meta?.Pitch ?? 0f };
        _camera.UpdateProjection(GraphicsDevice.Viewport.AspectRatio);

        // Fresh world: spawn above the highest possible terrain; the player
        // falls to the ground once the spawn chunk has loaded.
        _player = meta != null
            ? new PlayerController(new Vector3(meta.PlayerX, meta.PlayerY, meta.PlayerZ), meta.IsFlying)
            : new PlayerController(new Vector3(8.5f, 70f, 8.5f));

        base.Initialize();
    }

    protected override void LoadContent()
    {
        _spriteBatch = new SpriteBatch(GraphicsDevice);
        _atlas = new TextureAtlas(GraphicsDevice);
        _worldRenderer = new WorldRenderer(GraphicsDevice, _atlas);
        _chunkManager = new ChunkManager(GraphicsDevice, new TerrainGenerator(_seed), _worldSave);
        _blockHighlight = new BlockHighlight(GraphicsDevice);
        _hud = new Hud(GraphicsDevice);
        _font = new PixelFont(GraphicsDevice);
        _hotbar = new Hotbar(GraphicsDevice, _inventory);
        _inventoryScreen = new InventoryScreen(GraphicsDevice, _inventory);
    }

    protected override void Update(GameTime gameTime)
    {
        var keyboard = Keyboard.GetState();
        var mouse = Mouse.GetState();

        if (keyboard.IsKeyDown(Keys.Escape) && _previousKeyboard.IsKeyUp(Keys.Escape))
        {
            if (_inventoryScreen.IsOpen)
                ToggleInventoryScreen();
            else
                Exit();
        }

        if (keyboard.IsKeyDown(Keys.E) && _previousKeyboard.IsKeyUp(Keys.E))
            ToggleInventoryScreen();

        float dt = (float)gameTime.ElapsedGameTime.TotalSeconds;
        if (_inventoryScreen.IsOpen)
        {
            _inventoryScreen.Update(mouse, _previousMouse, GraphicsDevice.Viewport.Width, GraphicsDevice.Viewport.Height);
        }
        else
        {
            UpdateMouseLook();
            _player.Update(keyboard, _camera, _chunkManager, dt);
            _camera.Position = _player.EyePosition;
            UpdateBlockInteraction(keyboard, mouse);
        }
        _chunkManager.Update(_player.Position);

        if (keyboard.IsKeyDown(Keys.F5) && _previousKeyboard.IsKeyUp(Keys.F5))
            SaveWorld();
        _previousKeyboard = keyboard;
        _previousMouse = mouse;

        base.Update(gameTime);
    }

    private void ToggleInventoryScreen()
    {
        _inventoryScreen.Toggle();
        IsMouseVisible = _inventoryScreen.IsOpen;
        // Prevents the recentering after closing from reading as a huge look delta.
        _mouseCaptured = false;
    }

    private void SaveWorld()
    {
        var inventorySlots = new List<InventorySlotData>();
        for (int i = 0; i < Inventory.Size; i++)
        {
            if (!_inventory[i].IsEmpty)
                inventorySlots.Add(new InventorySlotData { Slot = i, Item = (int)_inventory[i].Item, Count = _inventory[i].Count });
        }

        _chunkManager.SaveAllModified();
        _worldSave.SaveMetadata(new WorldMetadata
        {
            Seed = _seed,
            PlayerX = _player.Position.X,
            PlayerY = _player.Position.Y,
            PlayerZ = _player.Position.Z,
            Yaw = _camera.Yaw,
            Pitch = _camera.Pitch,
            HotbarIndex = _inventory.SelectedIndex,
            IsFlying = _player.IsFlying,
            Inventory = inventorySlots,
        });
    }

    protected override void OnExiting(object sender, ExitingEventArgs args)
    {
        SaveWorld();
        base.OnExiting(sender, args);
    }

    private void UpdateBlockInteraction(KeyboardState keyboard, MouseState mouse)
    {
        if (IsActive)
            _hotbar.Update(keyboard, mouse);

        _targetedBlock = VoxelRaycaster.Cast(_chunkManager, _camera.Position, _camera.Forward, Reach, out var hit)
            ? hit
            : null;

        if (IsActive && _mouseCaptured && _targetedBlock is { } target)
        {
            bool leftClick = mouse.LeftButton == ButtonState.Pressed && _previousMouse.LeftButton == ButtonState.Released;
            bool rightClick = mouse.RightButton == ButtonState.Pressed && _previousMouse.RightButton == ButtonState.Released;

            if (leftClick)
            {
                var broken = _chunkManager.GetBlock(target.X, target.Y, target.Z);
                _chunkManager.SetBlock(target.X, target.Y, target.Z, BlockType.Air);
                var drop = ItemInfo.GetDrop(broken);
                if (drop != ItemType.None)
                    _inventory.TryAdd(drop); // full inventory = the drop is lost
            }
            else if (rightClick && (target.NormalX != 0 || target.NormalY != 0 || target.NormalZ != 0))
            {
                TryPlaceBlock(target);
            }
        }
    }

    private void TryPlaceBlock(RaycastHit target)
    {
        var stack = _inventory.SelectedStack;
        if (stack.IsEmpty || !ItemInfo.TryGetBlock(stack.Item, out var blockToPlace))
            return;

        int x = target.X + target.NormalX;
        int y = target.Y + target.NormalY;
        int z = target.Z + target.NormalZ;

        // Placing into water replaces it (there's no flow simulation). SetBlock
        // silently no-ops on unloaded chunks, so verify before consuming the item.
        if (BlockInfo.IsSolid(_chunkManager.GetBlock(x, y, z))
            || IntersectsPlayer(x, y, z)
            || !_chunkManager.IsChunkLoaded(ChunkManager.ToChunkCoord(new Vector3(x, 0, z))))
            return;

        _chunkManager.SetBlock(x, y, z, blockToPlace);
        _inventory.ConsumeFromSlot(_inventory.SelectedIndex);
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

        int width = GraphicsDevice.Viewport.Width, height = GraphicsDevice.Viewport.Height;

        _worldRenderer.Draw(GraphicsDevice, _camera, _chunkManager.Meshes);
        if (_targetedBlock is { } target && !_inventoryScreen.IsOpen)
            _blockHighlight.Draw(GraphicsDevice, _camera, target.X, target.Y, target.Z);

        if (!_inventoryScreen.IsOpen)
            _hud.Draw(_spriteBatch, width, height);
        _hotbar.Draw(_spriteBatch, _atlas, _font, width, height);
        if (_inventoryScreen.IsOpen)
            _inventoryScreen.Draw(_spriteBatch, _atlas, _font, Mouse.GetState(), width, height);

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
