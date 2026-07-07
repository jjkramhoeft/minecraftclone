using System;
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

    private GraphicsDeviceManager _graphics;
    private SpriteBatch _spriteBatch;
    private FirstPersonCamera _camera;
    private WorldRenderer _worldRenderer;
    private ChunkManager _chunkManager;
    private BlockUpdater _blockUpdater;
    private FallingBlocks _fallingBlocks;
    private FallingBlockRenderer _fallingBlockRenderer;
    private DayNightCycle _dayNight;
    private SkyRenderer _skyRenderer;
    private PlayerController _player;
    private BlockInteraction _blockInteraction;
    private BlockHighlight _blockHighlight;
    private BreakingOverlay _breakingOverlay;
    private PlayerModel _playerModel;
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
        _dayNight = new DayNightCycle { TimeOfDay = meta?.TimeOfDay ?? 0.1f };

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
        _breakingOverlay = new BreakingOverlay(GraphicsDevice, _atlas);
        _blockUpdater = new BlockUpdater();
        _fallingBlocks = new FallingBlocks();
        _fallingBlockRenderer = new FallingBlockRenderer(GraphicsDevice, _atlas);
        _skyRenderer = new SkyRenderer(GraphicsDevice, _atlas);
        _blockInteraction = new BlockInteraction(_chunkManager, _inventory, _player, _blockUpdater);
        _playerModel = new PlayerModel(GraphicsDevice, _atlas);
        _blockInteraction.ActionPerformed += _playerModel.TriggerSwing;
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

        if (keyboard.IsKeyDown(Keys.V) && _previousKeyboard.IsKeyUp(Keys.V))
            _camera.ThirdPerson = !_camera.ThirdPerson;

        if (!_inventoryScreen.IsOpen && keyboard.IsKeyDown(Keys.N) && _previousKeyboard.IsKeyUp(Keys.N))
            RestartWorld();

        float dt = (float)gameTime.ElapsedGameTime.TotalSeconds;
        if (_inventoryScreen.IsOpen)
        {
            _inventoryScreen.Update(mouse, _previousMouse, GraphicsDevice.Viewport.Width, GraphicsDevice.Viewport.Height);
        }
        else
        {
            UpdateMouseLook();
            _player.Update(keyboard, _camera, _chunkManager, dt);
            _camera.Position = ComputeCameraPosition();
            if (IsActive)
                _hotbar.Update(keyboard, mouse);
            _blockInteraction.Update(_camera, mouse, _previousMouse, IsActive && _mouseCaptured, dt);
        }
        _chunkManager.Update(_player.Position);
        _blockUpdater.Update(_chunkManager, _fallingBlocks, dt);
        _fallingBlocks.Update(_chunkManager, _blockUpdater, dt);
        _dayNight.Update(dt);
        _playerModel.Update(_player, dt, !_inventoryScreen.IsOpen && _blockInteraction.IsMining);

        if (keyboard.IsKeyDown(Keys.F5) && _previousKeyboard.IsKeyUp(Keys.F5))
            SaveWorld();
        _previousKeyboard = keyboard;
        _previousMouse = mouse;

        base.Update(gameTime);
    }

    /// <summary>Abandons the current world (deleting its save) and starts a
    /// fresh one with a random seed: new terrain, empty inventory, respawn.</summary>
    private void RestartWorld()
    {
        _seed = Random.Shared.Next();
        _worldSave.DeleteAll();

        // Everything that referenced the old world is rebuilt; the old chunk
        // manager's in-flight workers finish into discarded queues.
        _chunkManager.Dispose();
        _chunkManager = new ChunkManager(GraphicsDevice, new TerrainGenerator(_seed), _worldSave);
        _blockUpdater = new BlockUpdater();
        _fallingBlocks.Clear();
        _player = new PlayerController(new Vector3(8.5f, 70f, 8.5f));
        _blockInteraction = new BlockInteraction(_chunkManager, _inventory, _player, _blockUpdater);
        _blockInteraction.ActionPerformed += _playerModel.TriggerSwing;

        // Hotbar and InventoryScreen hold the inventory reference — clear in place.
        _inventory.Clear();
        _camera.Yaw = 0f;
        _camera.Pitch = 0f;
        _dayNight.TimeOfDay = 0.1f;

        SaveWorld(); // pin the new seed to disk immediately
    }

    private Vector3 ComputeCameraPosition()
    {
        var eye = _player.EyePosition;
        if (!_camera.ThirdPerson)
            return eye;

        // Boom straight back from the eye, pulled in when terrain would
        // occlude it so the camera never ends up inside a block.
        float boom = _camera.ThirdPersonDistance;
        if (VoxelRaycaster.Cast(_chunkManager, eye, -_camera.Forward, boom, out var occluder, includeFlowers: false))
            boom = System.MathF.Max(0.3f, occluder.Distance - 0.25f);
        return eye - _camera.Forward * boom;
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
            TimeOfDay = _dayNight.TimeOfDay,
        });
    }

    protected override void OnExiting(object sender, ExitingEventArgs args)
    {
        // Land anything mid-air so no block is lost between sessions.
        _fallingBlocks.SettleAll(_chunkManager, _blockUpdater);
        SaveWorld();
        base.OnExiting(sender, args);
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
        GraphicsDevice.Clear(_dayNight.SkyColor);

        int width = GraphicsDevice.Viewport.Width, height = GraphicsDevice.Viewport.Height;

        _skyRenderer.Draw(GraphicsDevice, _camera, _dayNight);
        _worldRenderer.SetEnvironment(_dayNight.LightColor, _dayNight.SkyColor);
        _playerModel.SetEnvironment(_dayNight.LightColor, _dayNight.SkyColor);
        _fallingBlockRenderer.SetEnvironment(_dayNight.LightColor, _dayNight.SkyColor);

        _worldRenderer.Draw(GraphicsDevice, _camera, _chunkManager.Meshes);
        _fallingBlockRenderer.Draw(GraphicsDevice, _camera, _fallingBlocks.Entries);
        if (!_inventoryScreen.IsOpen)
        {
            if (_blockInteraction.IsMining)
            {
                var pos = _blockInteraction.MiningPos;
                _breakingOverlay.Draw(GraphicsDevice, _camera, pos, _blockInteraction.BreakProgress);
            }
            if (_blockInteraction.Target is { } target)
                _blockHighlight.Draw(GraphicsDevice, _camera, target.X, target.Y, target.Z);
        }

        if (_camera.ThirdPerson)
            _playerModel.DrawBody(_camera, _player.Position, _camera.Yaw, _camera.Pitch);
        else
            _playerModel.DrawFirstPersonArm(_camera);

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
