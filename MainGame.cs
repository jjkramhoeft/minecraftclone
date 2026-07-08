using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using MinecraftClone.Audio;
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

    // Slot 0 keeps the historical "default" directory so old saves survive.
    private static readonly string[] WorldSlotNames = { "default", "world2", "world3" };

    private GraphicsDeviceManager _graphics;
    private SpriteBatch _spriteBatch;
    private FirstPersonCamera _camera;
    private WorldRenderer _worldRenderer;
    private ChunkManager _chunkManager;
    private BlockUpdater _blockUpdater;
    private FallingBlocks _fallingBlocks;
    private FallingBlockRenderer _fallingBlockRenderer;
    private Mobs _mobs;
    private MobRenderer _mobRenderer;
    private ItemDrops _itemDrops;
    private ItemDropRenderer _itemDropRenderer;
    private DayNightCycle _dayNight;
    private SkyRenderer _skyRenderer;
    private CloudRenderer _cloudRenderer;
    private Particles _particles;
    private ParticleRenderer _particleRenderer;
    private float _bubbleTimer;
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
    private MenuScreen _menu;
    private PixelFont _font;
    private DebugOverlay _debugOverlay;
    private readonly long[] _blockCounts = new long[256]; // scratch for F3 block-frequency
    private GameSounds _sounds;
    private PlayerHealth _health;
    private Furnaces _furnaces;
    private Chests _chests;
    private ChestScreen _chestScreen;

    // Footstep/splash state, driven from player movement each frame.
    private Vector3 _lastStepPosition;
    private float _stepDistance;
    private bool _wasInWater;

    private WorldSave _worldSave;
    private int _seed;

    /// <summary>False while sitting in the main menu with no world loaded —
    /// world objects (_chunkManager, _player, ...) must not be touched then.</summary>
    private bool _worldActive;

    private MouseState _previousMouse;
    private KeyboardState _previousKeyboard;

    private double _titleTimer;
    private int _frames;

    // False whenever the window lost focus, so the first recentering of the
    // mouse doesn't register as a huge look delta.
    private bool _mouseCaptured;

    // Smoke mode (--smoke): runs a fixed number of frames against a throwaway
    // world, prints a machine-readable summary to stdout, and exits without
    // touching the real save. Lets tooling verify a change without screenshots.
    private readonly bool _smoke;
    private const int SmokeUpdateFrames = 180; // ~3 s at the fixed 60 Hz step
    private int _smokeUpdates;
    private int _smokeDraws;
    private double _smokeElapsed;

    public MainGame(bool smoke = false)
    {
        _smoke = smoke;
        _graphics = new GraphicsDeviceManager(this);
        _graphics.PreferredBackBufferWidth = 1280;
        _graphics.PreferredBackBufferHeight = 720;
        Content.RootDirectory = "Content";
        IsMouseVisible = false;
        Window.Title = smoke ? "Minecraft Clone (smoke test)" : "Minecraft Clone";
    }

    protected override void Initialize()
    {
        _inventory = new Inventory();
        _camera = new FirstPersonCamera();
        _camera.UpdateProjection(GraphicsDevice.Viewport.AspectRatio);
        _dayNight = new DayNightCycle();

        base.Initialize(); // runs LoadContent

        if (_smoke)
            StartWorld("smoke");
        else
            OpenMainMenu();
    }

    /// <summary>Loads (or creates) a world into the existing object graph:
    /// long-lived UI/renderers stay, world-scoped state is rebuilt in place.</summary>
    private void StartWorld(string name)
    {
        _worldSave = new WorldSave(name);
        var meta = _smoke ? null : _worldSave.TryLoadMetadata();
        _seed = meta?.Seed ?? (_smoke ? DefaultSeed : Random.Shared.Next());

        _inventory.Clear();
        _inventory.SelectedIndex = meta?.HotbarIndex ?? 0;
        if (meta?.Inventory != null)
        {
            foreach (var slot in meta.Inventory)
                if (slot.Slot >= 0 && slot.Slot < Inventory.Size)
                    _inventory[slot.Slot] = new ItemStack((ItemType)slot.Item, slot.Count) { Damage = slot.Damage };
        }

        _camera.Yaw = meta?.Yaw ?? 0f;
        _camera.Pitch = meta?.Pitch ?? 0f;
        _dayNight.TimeOfDay = meta?.TimeOfDay ?? 0.1f;

        // Fresh world: spawn above the highest possible terrain; the player
        // falls to the ground once the spawn chunk has loaded.
        _player = meta != null
            ? new PlayerController(new Vector3(meta.PlayerX, meta.PlayerY, meta.PlayerZ), meta.IsFlying)
            : new PlayerController(new Vector3(8.5f, 70f, 8.5f));

        _chunkManager?.Dispose();
        _chunkManager = new ChunkManager(GraphicsDevice, new TerrainGenerator(_seed), _worldSave);
        _blockUpdater = new BlockUpdater();
        _fallingBlocks.Clear();
        _mobs.Clear();
        _furnaces.Clear();
        if (meta?.Furnaces != null)
        {
            foreach (var f in meta.Furnaces)
                _furnaces.Restore(f.X, f.Y, f.Z, (ItemType)f.OutputItem, f.OutputCount, f.SecondsRemaining);
        }
        _chests.Clear();
        if (meta?.Chests != null)
        {
            foreach (var c in meta.Chests)
            {
                var slots = new ItemStack[Chests.ChestSize];
                if (c.Slots != null)
                    foreach (var s in c.Slots)
                        if (s.Slot >= 0 && s.Slot < Chests.ChestSize)
                            slots[s.Slot] = new ItemStack((ItemType)s.Item, s.Count) { Damage = s.Damage };
                _chests.Restore(c.X, c.Y, c.Z, slots);
            }
        }
        if (_chestScreen.IsOpen)
            _chestScreen.Close();
        _itemDrops.Clear();
        _blockInteraction = new BlockInteraction(_chunkManager, _inventory, _player, _blockUpdater, _itemDrops);
        WireInteractionEvents();
        _health.Reset();
        _particles.Clear();
        _bubbleTimer = 0f;
        _wasInWater = false;
        _stepDistance = 0f;
        _lastStepPosition = _player.Position;

        _worldActive = true;
        _menu.Current = MenuScreen.Mode.Hidden;
        IsMouseVisible = false;
        _mouseCaptured = false;

        if (meta == null && !_smoke)
            SaveWorld(); // pin the new seed to disk immediately
    }

    private void OpenMainMenu()
    {
        for (int i = 0; i < MenuScreen.WorldSlots; i++)
            _menu.SlotSaved[i] = WorldSave.Exists(WorldSlotNames[i]);
        _menu.Current = MenuScreen.Mode.Main;
        IsMouseVisible = true;
        _mouseCaptured = false;
    }

    private void OpenPauseMenu()
    {
        _menu.Current = MenuScreen.Mode.Pause;
        IsMouseVisible = true;
        _mouseCaptured = false;
    }

    private void ResumeGame()
    {
        _menu.Current = MenuScreen.Mode.Hidden;
        IsMouseVisible = false;
        _mouseCaptured = false;
    }

    private void QuitToMenu()
    {
        _fallingBlocks.SettleAll(_chunkManager, _blockUpdater);
        SaveWorld();
        _worldActive = false;
        _chunkManager.Dispose();
        OpenMainMenu();
    }

    protected override void LoadContent()
    {
        _spriteBatch = new SpriteBatch(GraphicsDevice);
        _atlas = new TextureAtlas(GraphicsDevice);
        _worldRenderer = new WorldRenderer(GraphicsDevice, _atlas, Content.Load<Effect>("TerrainEffect"));
        _blockHighlight = new BlockHighlight(GraphicsDevice);
        _breakingOverlay = new BreakingOverlay(GraphicsDevice, _atlas);
        _fallingBlocks = new FallingBlocks();
        _fallingBlockRenderer = new FallingBlockRenderer(GraphicsDevice, _atlas);
        _mobs = new Mobs();
        _mobRenderer = new MobRenderer(GraphicsDevice, _atlas);
        _itemDrops = new ItemDrops();
        _itemDropRenderer = new ItemDropRenderer(GraphicsDevice, _atlas);
        _skyRenderer = new SkyRenderer(GraphicsDevice, _atlas);
        _cloudRenderer = new CloudRenderer(GraphicsDevice);
        _particles = new Particles();
        _particleRenderer = new ParticleRenderer(GraphicsDevice);
        _playerModel = new PlayerModel(GraphicsDevice, _atlas);
        _sounds = new GameSounds();
        _itemDrops.PickedUp += _sounds.PlayPickup;
        _health = new PlayerHealth();
        _health.Died += RespawnPlayer;
        _furnaces = new Furnaces();
        _chests = new Chests();
        _hud = new Hud(GraphicsDevice);
        _font = new PixelFont(GraphicsDevice);
        _debugOverlay = new DebugOverlay(GraphicsDevice);
        _hotbar = new Hotbar(GraphicsDevice, _inventory);
        _inventoryScreen = new InventoryScreen(GraphicsDevice, _inventory);
        _chestScreen = new ChestScreen(GraphicsDevice, _inventory);

        _menu = new MenuScreen(GraphicsDevice);
        _menu.WorldChosen += slot => StartWorld(WorldSlotNames[slot]);
        _menu.WorldDeleted += slot =>
        {
            new WorldSave(WorldSlotNames[slot]).DeleteAll();
            _menu.SlotSaved[slot] = false;
        };
        _menu.ResumeRequested += ResumeGame;
        _menu.SaveRequested += SaveWorld;
        _menu.QuitToMenuRequested += QuitToMenu;
        _menu.ExitRequested += Exit;
    }

    protected override void Update(GameTime gameTime)
    {
        var keyboard = Keyboard.GetState();
        var mouse = Mouse.GetState();

        // Menu (main or pause) suspends the world entirely.
        if (_menu.Current != MenuScreen.Mode.Hidden)
        {
            if (keyboard.IsKeyDown(Keys.Escape) && _previousKeyboard.IsKeyUp(Keys.Escape))
            {
                if (_menu.Current == MenuScreen.Mode.Pause)
                    ResumeGame();
                else
                    Exit();
            }
            if (IsActive)
                _menu.Update(mouse, _previousMouse, GraphicsDevice.Viewport.Width, GraphicsDevice.Viewport.Height);
            _previousKeyboard = keyboard;
            _previousMouse = mouse;
            base.Update(gameTime);
            return;
        }

        if (keyboard.IsKeyDown(Keys.Escape) && _previousKeyboard.IsKeyUp(Keys.Escape))
        {
            if (_chestScreen.IsOpen)
                CloseChestScreen();
            else if (_inventoryScreen.IsOpen)
                ToggleInventoryScreen();
            else
                OpenPauseMenu();
        }

        if (keyboard.IsKeyDown(Keys.E) && _previousKeyboard.IsKeyUp(Keys.E))
        {
            if (_chestScreen.IsOpen)
                CloseChestScreen();
            else
                ToggleInventoryScreen();
        }

        if (keyboard.IsKeyDown(Keys.V) && _previousKeyboard.IsKeyUp(Keys.V))
            _camera.ThirdPerson = !_camera.ThirdPerson;

        if (keyboard.IsKeyDown(Keys.F3) && _previousKeyboard.IsKeyUp(Keys.F3))
        {
            _debugOverlay.Visible = !_debugOverlay.Visible;
            if (_debugOverlay.Visible)
                BuildBlockFrequency();
            else
                _debugOverlay.FreqLines.Clear();
        }

        float dt = (float)gameTime.ElapsedGameTime.TotalSeconds;
        if (_chestScreen.IsOpen)
        {
            _chestScreen.Update(mouse, _previousMouse, GraphicsDevice.Viewport.Width, GraphicsDevice.Viewport.Height);
        }
        else if (_inventoryScreen.IsOpen)
        {
            _inventoryScreen.Update(mouse, _previousMouse, GraphicsDevice.Viewport.Width, GraphicsDevice.Viewport.Height);
        }
        else
        {
            if (!_smoke) // don't recenter the real cursor during an unattended run
                UpdateMouseLook();
            _player.Update(keyboard, _camera, _chunkManager, dt);
            _camera.Position = ComputeCameraPosition();
            if (IsActive)
                _hotbar.Update(keyboard, mouse);
            _blockInteraction.Update(_camera, mouse, _previousMouse, IsActive && _mouseCaptured, dt);
            UpdateMovementSounds(dt);
            _health.Update(_player, _chunkManager, dt);
        }
        _camera.SprintFovActive = _worldActive && _player.IsSprinting;
        _camera.UpdateFov(dt);
        _particles.Update(dt);
        _chunkManager.Update(_player.Position);
        _blockUpdater.Update(_chunkManager, _fallingBlocks, dt);
        _fallingBlocks.Update(_chunkManager, _blockUpdater, dt);
        _mobs.Update(_chunkManager, _player.Position, dt);
        _itemDrops.Update(_chunkManager, _inventory, _player.Position, dt);
        _furnaces.Update(_chunkManager, dt);
        _dayNight.Update(dt);
        _cloudRenderer.Update(dt);
        _playerModel.Update(_player, dt, !_inventoryScreen.IsOpen && _blockInteraction.IsMining);

        if (keyboard.IsKeyDown(Keys.F5) && _previousKeyboard.IsKeyUp(Keys.F5))
            SaveWorld();
        _previousKeyboard = keyboard;
        _previousMouse = mouse;

        if (_smoke)
        {
            _smokeUpdates++;
            _smokeElapsed += gameTime.ElapsedGameTime.TotalSeconds;
            if (_smokeUpdates >= SmokeUpdateFrames)
                Exit();
        }

        base.Update(gameTime);
    }

    /// <summary>Death is gentle for now: back to the world spawn with full
    /// vitals, inventory intact.</summary>
    private void RespawnPlayer()
    {
        _player.Teleport(new Vector3(8.5f, 70f, 8.5f));
        _health.Reset();
    }

    private void WireInteractionEvents()
    {
        _blockInteraction.ActionPerformed += _playerModel.TriggerSwing;
        _blockInteraction.BlockBroken += _sounds.PlayBreak;
        _blockInteraction.BlockPlaced += _sounds.PlayPlace;
        _blockInteraction.UseBlock = (x, y, z, type) =>
        {
            if (type == BlockType.Chest)
            {
                _chestScreen.Open(_chests.GetOrCreate(x, y, z));
                IsMouseVisible = true;
                _mouseCaptured = false;
                return true;
            }
            if (type == BlockType.CraftingTable)
            {
                _inventoryScreen.Open(advanced: true);
                IsMouseVisible = true;
                _mouseCaptured = false;
                return true;
            }
            return _furnaces.Use(_chunkManager, _inventory, x, y, z);
        };
        _blockInteraction.BlockBrokenAt += (x, y, z, type) =>
        {
            if (type is BlockType.Furnace or BlockType.FurnaceLit)
                _furnaces.OnBroken(_inventory, x, y, z);
            else if (type == BlockType.Chest)
                _chests.OnBroken(_inventory, x, y, z);
        };
    }

    /// <summary>Footsteps every couple of blocks walked on solid ground, and a
    /// splash when the player's feet enter water.</summary>
    private void UpdateMovementSounds(float dt)
    {
        const float StepStride = 2.2f;

        var feet = _player.Position;
        bool inWater = BlockInfo.IsWater(_chunkManager.GetBlock(
            (int)MathF.Floor(feet.X), (int)MathF.Floor(feet.Y + 0.6f), (int)MathF.Floor(feet.Z)));
        if (inWater && !_wasInWater)
        {
            _sounds.PlaySplash();
            // A bigger splash the faster the feet hit the surface.
            int droplets = 8 + (int)MathHelper.Clamp(-_player.Velocity.Y * 1.5f, 0f, 20f);
            _particles.SpawnSplash(new Vector3(feet.X, feet.Y + 0.1f, feet.Z), droplets);
        }
        _wasInWater = inWater;

        // Lazy bubbles trailing off a swimmer while they move.
        if (inWater)
        {
            var swim = _player.Velocity;
            swim.Y = 0f;
            _bubbleTimer -= dt;
            if (_bubbleTimer <= 0f && swim.LengthSquared() > 0.5f)
            {
                _bubbleTimer = 0.18f;
                _particles.SpawnBubble(_player.EyePosition - new Vector3(0f, 0.3f, 0f));
            }
        }

        if (_player.IsOnGround && !_player.IsFlying && !inWater)
        {
            var delta = feet - _lastStepPosition;
            delta.Y = 0f;
            _stepDistance += delta.Length();
            if (_stepDistance >= StepStride)
            {
                _stepDistance = 0f;
                var ground = _chunkManager.GetBlock(
                    (int)MathF.Floor(feet.X), (int)MathF.Floor(feet.Y - 0.5f), (int)MathF.Floor(feet.Z));
                _sounds.PlayFootstep(ground);
            }
        }
        else
        {
            _stepDistance = 0f;
        }
        _lastStepPosition = feet;
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

    private void CloseChestScreen()
    {
        _chestScreen.Close();
        IsMouseVisible = false;
        _mouseCaptured = false;
    }

    private void SaveWorld()
    {
        var inventorySlots = new List<InventorySlotData>();
        for (int i = 0; i < Inventory.Size; i++)
        {
            if (!_inventory[i].IsEmpty)
                inventorySlots.Add(new InventorySlotData { Slot = i, Item = (int)_inventory[i].Item, Count = _inventory[i].Count, Damage = _inventory[i].Damage });
        }

        var chestData = new List<ChestData>();
        foreach (var (pos, slots) in _chests.All)
        {
            var slotData = new List<InventorySlotData>();
            for (int i = 0; i < slots.Length; i++)
                if (!slots[i].IsEmpty)
                    slotData.Add(new InventorySlotData { Slot = i, Item = (int)slots[i].Item, Count = slots[i].Count, Damage = slots[i].Damage });
            chestData.Add(new ChestData { X = pos.X, Y = pos.Y, Z = pos.Z, Slots = slotData });
        }

        var furnaceData = new List<FurnaceData>();
        foreach (var (pos, state) in _furnaces.States)
            furnaceData.Add(new FurnaceData
            {
                X = pos.X, Y = pos.Y, Z = pos.Z,
                OutputItem = (int)state.Output, OutputCount = state.OutputCount,
                SecondsRemaining = state.SecondsRemaining,
            });

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
            Furnaces = furnaceData,
            Chests = chestData,
        });
    }

    protected override void OnExiting(object sender, ExitingEventArgs args)
    {
        if (_smoke)
        {
            PrintSmokeSummary();
            base.OnExiting(sender, args);
            return;
        }

        // Land anything mid-air so no block is lost between sessions.
        if (_worldActive)
        {
            _fallingBlocks.SettleAll(_chunkManager, _blockUpdater);
            SaveWorld();
        }
        base.OnExiting(sender, args);
    }

    private void PrintSmokeSummary()
    {
        int opaque = 0, water = 0, cutout = 0, lightV = 0;
        foreach (var m in _chunkManager.Meshes)
        {
            opaque += m.OpaqueVertexCount; water += m.WaterVertexCount;
            cutout += m.CutoutVertexCount; lightV += m.LightVertexCount;
        }
        Console.WriteLine($"SMOKE VERTS — opaque {opaque / 1000}k, water {water / 1000}k, cutout {cutout / 1000}k, light {lightV / 1000}k");
        var pos = _player.Position;
        double fps = _smokeElapsed > 0 ? _smokeDraws / _smokeElapsed : 0;
        Console.WriteLine(
            $"SMOKE OK — {_smokeUpdates} updates / {_smokeDraws} draws in {_smokeElapsed:0.0}s ({fps:0} fps) | " +
            $"{_chunkManager.LoadedChunkCount} chunks loaded ({_chunkManager.PendingCount} pending) | " +
            $"{_chunkManager.TotalVertexCount / 1000}k mesh vertices | " +
            $"player at {pos.X:0.#}, {pos.Y:0.#}, {pos.Z:0.#}");
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

        if (_worldActive)
        {
            _skyRenderer.Draw(GraphicsDevice, _camera, _dayNight);
            _worldRenderer.SetEnvironment(_dayNight.LightColor, _dayNight.SkyColor);
            _playerModel.SetEnvironment(_dayNight.LightColor, _dayNight.SkyColor);
            _fallingBlockRenderer.SetEnvironment(_dayNight.LightColor, _dayNight.SkyColor);
            _mobRenderer.SetEnvironment(_dayNight.LightColor, _dayNight.SkyColor);
            _itemDropRenderer.SetEnvironment(_dayNight.LightColor, _dayNight.SkyColor);

            _worldRenderer.Draw(GraphicsDevice, _camera, _chunkManager.Meshes);
            _cloudRenderer.Draw(GraphicsDevice, _camera, _dayNight.LightColor);
            _fallingBlockRenderer.Draw(GraphicsDevice, _camera, _fallingBlocks.Entries);
            _mobRenderer.Draw(_camera, _mobs.All);
            _itemDropRenderer.Draw(GraphicsDevice, _camera, _itemDrops.All);
            _particleRenderer.Draw(GraphicsDevice, _camera, _particles);
            bool showGameplayUi = !_inventoryScreen.IsOpen && !_chestScreen.IsOpen
                && _menu.Current == MenuScreen.Mode.Hidden;
            if (showGameplayUi)
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

            if (showGameplayUi)
                _hud.Draw(_spriteBatch, _health, width, height);
            _debugOverlay.Draw(_spriteBatch, _font);
            _hotbar.Draw(_spriteBatch, _atlas, _font, width, height);
            if (_inventoryScreen.IsOpen)
                _inventoryScreen.Draw(_spriteBatch, _atlas, _font, Mouse.GetState(), width, height);
            _chestScreen.Draw(_spriteBatch, _atlas, _font, Mouse.GetState(), width, height);

            UpdateDebugTitle(gameTime);
        }

        _menu.Draw(_spriteBatch, _font, Mouse.GetState(), width, height);

        if (_smoke)
            _smokeDraws++;

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

        if (_debugOverlay.Visible)
        {
            _debugOverlay.Lines[0] = $"{_frames} FPS";
            _debugOverlay.Lines[1] = $"XYZ: {pos.X:0.#} {pos.Y:0.#} {pos.Z:0.#} [{mode}]";
            _debugOverlay.Lines[2] = $"CHUNKS: {_chunkManager.LoadedChunkCount} ({_chunkManager.PendingCount} PENDING)";
            _debugOverlay.Lines[3] = $"YAW: {MathHelper.ToDegrees(_camera.Yaw):0} PITCH: {MathHelper.ToDegrees(_camera.Pitch):0}";
            _debugOverlay.Lines[4] = $"TIME: {_dayNight.TimeOfDay:0.00}";
            _debugOverlay.Lines[5] = $"VERTS: {_chunkManager.TotalVertexCount / 1000}K";
        }

        _frames = 0;
        _titleTimer = 0;
    }

    /// <summary>Snapshots the block-type composition of every loaded chunk when
    /// F3 opens the overlay — a quick read on what the terrain generator is
    /// actually producing. Percentages are of non-air blocks, sorted descending.</summary>
    private void BuildBlockFrequency()
    {
        var freq = _debugOverlay.FreqLines;
        freq.Clear();
        if (_chunkManager == null)
            return;

        int chunks = _chunkManager.CountBlocks(_blockCounts);
        long solid = 0;
        for (int i = 1; i < _blockCounts.Length; i++) // skip air (id 0)
            solid += _blockCounts[i];

        freq.Add($"BLOCK FREQ - {chunks} CHUNKS");
        if (solid == 0)
            return;

        var present = new List<(int Id, long Count)>();
        for (int i = 1; i < _blockCounts.Length; i++)
            if (_blockCounts[i] > 0)
                present.Add((i, _blockCounts[i]));
        present.Sort((a, b) => b.Count.CompareTo(a.Count));

        int shown = Math.Min(present.Count, 12);
        for (int i = 0; i < shown; i++)
        {
            var (id, count) = present[i];
            float pct = 100f * count / solid;
            freq.Add($"{(BlockType)id} {pct:0.0}");
        }
    }

    protected override void UnloadContent()
    {
        _chunkManager?.Dispose();
        base.UnloadContent();
    }
}
