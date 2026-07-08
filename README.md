# Minecraft Clone

A small Minecraft-style voxel sandbox written in C# with [MonoGame](https://monogame.net/) (DesktopGL) for Windows. An infinite, procedurally generated landscape you can explore, dig into, build on, and survive in — with a handful of passive animals wandering the grass.

You spawn in a world of rolling grass plains, tall forests, sandy deserts, and still lakes, all generated from a single seed. Walk, jump, sprint, sneak, and swim across it — or toggle fly mode and soar over it. Aim at any block to break it, or place blocks from your hotbar. Mine ores, chop trees, craft tools at a crafting table, smelt in a furnace, store loot in chests, and light the dark with torches. Falls hurt and staying underwater too long drowns you. Everything you change is saved and waiting the next time you play.

The whole game is plain C# on top of MonoGame's rendering primitives: chunk meshing, physics, raycasting, water simulation, texture generation, sound synthesis, and world persistence are all implemented in this repository. There are no art assets and no audio files — every texture and every sound is generated in code at startup. The only third-party code besides MonoGame itself is a single vendored noise library file.

## Features

- **Infinite terrain** that streams in around you on background threads, deterministic from one world seed: four biomes (grassy **plains**, wooded **forests**, flat sandy **deserts**, and towering **mountains** — pine on the lower slopes, bare rock above the treeline, and snow-capped peaks) blended from a noise field, with a lake biome of broad, deep water basins, cave systems, and a fake-bedrock floor
- **Trees & plants**: oak, birch, and pine trees (pine forests carpeted with ferns), flowers in three colors, and reed beds along shorelines — all drawn as cross-quads in an alpha-test pass
- **Ores**: coal and iron seams buried in the stone, coal shallow and common, iron deeper and rarer
- **First-person movement** with gravity, jumping, collision, sprinting, sneaking (with a ledge-stop so you don't walk off edges), swimming, and a free-fly mode
- **Survival vitals**: 20 half-hearts and a breath meter — fall damage above three blocks, drowning when your air runs out, and a gentle respawn back at spawn on death
- **Building & digging**: break any block and place from a 6-slot hotbar; placed stone turns to cobblestone; broken blocks pop out as collectible item drops
- **Timed mining**: blocks have hardness, tools have speed and tiers; stone needs at least a wooden pickaxe to drop, iron ore needs a stone pickaxe, and a crack overlay shows breaking progress
- **Tools that wear out**: wooden, stone, and iron pickaxes/axes/shovels, each with its own durability
- **Crafting**: hand-craft basics (planks, sticks, bricks, torches, a crafting table) from the inventory screen; unlock tools, furnaces, chests, and buckets at a crafting table
- **Smelting**: furnaces turn iron ore into ingots and sand into glass, burning coal as fuel and glowing while lit
- **Storage**: place chests for 12 extra slots of storage that persist with the world
- **Buckets & flowing water**: scoop a water source into a bucket and pour it back out; water spreads, falls, and drains as a self-correcting cellular automaton, and two adjacent sources refill between them
- **Torches & block lighting**: caves and enclosed spaces stay dark day or night, so torches (and lit furnaces) are your main underground light — they cast a smooth per-vertex glow that keeps their surroundings bright
- **Passive mobs**: pigs and chickens wander, hop over ledges, and paddle in water — up to ten around you at a time
- **A visible player character**: an animated blocky figure — you see its arm swing in first person (holding the equipped tool or bucket) and the whole walking (and sneaking) body in third person
- **Dual camera**: first-person or over-the-shoulder third person (`V`), with the camera boom pulling in so terrain never occludes the view
- **Pixel-style textures** from a procedurally generated 256×256 atlas — and you can override any tile by dropping a 16×16 PNG into a `textures/` folder
- **Synthesized sound**: footsteps, digging, placing, splashes, and pickups, all generated from noise at startup — no audio files
- **Lighting look** from per-face directional shading, per-vertex ambient occlusion, a vertical sky-light channel (so caves go dark), and torch block-light
- **Day/night cycle** (10-minute days): the sky shifts through sunrise, noon, sunset, and night; a sun and phased moon cross the sky, 150 stars come out after dark (hidden where clouds cover them), and the world dims to a moonlit floor
- **Sky & weather dressing**: drifting clouds, distance fog blending the horizon into the sky, and water splash/bubble particles
- **Persistent worlds**: three save slots with a main menu and pause menu; your edits, position, inventory (including tool wear), furnaces, chests, and time of day all survive restarts

## Controls

Mouse and keyboard only.

| Input | Action |
|---|---|
| `W A S D` | Move |
| Mouse | Look around |
| `Space` | Jump / swim up / fly up (in fly mode) |
| `Left Ctrl` | Sprint |
| `Left Shift` | Sneak (slow, ledge-safe) / fly down (in fly mode) |
| Left click (hold) | Mine block — speed depends on block hardness and the held tool |
| Right click | Place block, or use a chest / furnace / crafting table, or use a bucket on water |
| `1`–`6` / scroll wheel | Select hotbar slot |
| `E` | Open/close inventory (hand crafting) |
| `V` | Toggle first/third-person camera |
| `F` | Toggle fly mode |
| `F3` | Toggle debug overlay |
| `F5` | Save world |
| `Esc` | Pause menu (or back out of an open inventory/chest) |

Right-clicking a **crafting table** opens the inventory with the advanced (table-only) recipes unlocked; a **furnace** collects finished output or starts a smelt; a **chest** opens its 12 storage slots.

## Build & run

Prerequisites: [.NET SDK](https://dotnet.microsoft.com/download) 9.0 or later.

```powershell
dotnet tool restore   # restores the MonoGame content tools (needed once per clone)
dotnet build
dotnet run
```

The game opens on a main menu with three world slots. There are also two headless CLI modes:

```powershell
dotnet run -- --smoke           # run a throwaway world for ~3 s, print a summary, and exit (used for verification)
dotnet run -- --dump-textures   # write every generated atlas tile out as a 16×16 PNG, then exit
```

### VS Code

Open the folder in VS Code with the **C# Dev Kit** extension installed:

- `Ctrl+Shift+B` builds (default build task)
- `F5` builds and launches the game with the debugger attached

## Components & structure

Single executable project; namespaces mirror folders. [MainGame.cs](MainGame.cs) is the composition root: it owns one instance of each system, wires input and events to them in `Update`, and orders the passes in `Draw`. [Program.cs](Program.cs) parses the CLI flags and starts the game.

### `World\` — the voxel world

| File | Responsibility |
|---|---|
| `BlockType.cs` | The block palette: a `byte`-backed enum — terrain, plants, ores, water source/flow/fall variants, torches, furnaces, glass, chests, crafting tables, and tree-species logs/leaves |
| `BlockInfo.cs` | Per-block properties: solidity, opacity, hardness, effective tool, required tool tier, light emission, gravity, and which atlas tile each face uses |
| `Chunk.cs` | Storage for one 16×128×16 column of blocks — a flat `byte[]` plus per-cell block-light and sky-light arrays, with dirty/modified flags |
| `ChunkCoord.cs` | A chunk's position in the 2D chunk grid |
| `ChunkManager.cs` | The heart of the world: streams chunks in and out around the player, schedules background generation/meshing, and exposes `GetBlock`/`SetBlock` in world coordinates — the single block API used by rendering, physics, and raycasting |
| `TerrainGenerator.cs` | Fills chunks deterministically from the seed: fBm heightmap, four biomes, stone/dirt/grass strata, sandy lowlands, snow-capped mountains, deep lake basins, spaghetti caves, coal/iron ore blobs, oak/birch/pine trees, ferns, flowers, and shoreline reeds |
| `BlockUpdater.cs` | Tick-based block updates: detaches unsupported gravity blocks, pops unsupported plants, and feeds the water automaton its disturbed cells |
| `FallingBlocks.cs` | Airborne gravity blocks (sand): smooth accelerated fall, landing back into the grid (settled on exit so none are lost) |
| `WaterFlow.cs` | Self-correcting water cellular automaton: sources, decrementing sideways flow levels, falling water, draining orphaned flows, and two-source infill for bucket refills |
| `Mobs.cs` | Passive pigs and chickens: idle/wander state machine, shared AABB physics, ledge-hopping, buoyancy, ring spawning on grass, and distance despawn (never saved) |
| `DayNightCycle.cs` | Game time (0..1 over a 10-minute day) and the keyframed sky color, scene light, and star visibility derived from it |
| `Particles.cs` | Allocation-free fixed-pool particle simulator (splash droplets, swim bubbles) |
| `FastNoiseLite.cs` | Vendored single-file noise library (MIT) |

### `Rendering\` — from blocks to pixels

| File | Responsibility |
|---|---|
| `FirstPersonCamera.cs` | Yaw/pitch mouse-look camera with an optional third-person boom and a sprint FOV kick; provides view/projection matrices and the direction vectors movement and raycasting use |
| `PlayerModel.cs` | The blocky player character: textured boxes, walk/sneak/swing animation, first-person arm and third-person body |
| `ChunkMesher.cs` | Turns a chunk's blocks into vertex arrays across four sets — opaque (greedy-merged), transparent water, alpha-test cutout (plants/glass), and an emissive block-light overlay — with per-face shading, per-vertex ambient occlusion, and torch light. Pure CPU code, safe on worker threads |
| `ChunkMesh.cs` | GPU buffers for one chunk (opaque, water, cutout, light sets); created and disposed on the main thread only |
| `TerrainVertex.cs` | The custom vertex format for the shaded/AO/torch-lit opaque and light passes |
| `TextureAtlas.cs` | Generates the 256×256 block/item/sky/mob texture atlas in code at startup, maps tile indices to UV rects, loads PNG tile overrides from `textures/`, and can dump tiles to PNG |
| `WorldRenderer.cs` | Draws all visible chunk meshes: frustum culling, distance fog, an opaque pass, a max-blended torch-light pass, an alpha-test cutout pass, and a blended water pass |
| `BlockHighlight.cs` | Wireframe outline around the block under the crosshair |
| `BreakingOverlay.cs` | Crack texture drawn over the block being mined, staged by progress |
| `FallingBlockRenderer.cs` | Textured cubes for airborne falling blocks at their continuous positions |
| `MobRenderer.cs` | Quadruped box models for pigs and chickens, with a leg-swing walk animation |
| `ItemDropRenderer.cs` | Quarter-size spinning, bobbing cubes for dropped items on the ground |
| `SkyRenderer.cs` | Sun, phased moon, and stars — camera-relative quads drawn behind the world |
| `CloudRenderer.cs` | Flat drifting cloud quads on a noise grid at a fixed height, dimming at night |
| `ParticleRenderer.cs` | Camera-facing billboard quads for the particle pool |

### `Player\` — input, physics, interaction

| File | Responsibility |
|---|---|
| `PlayerController.cs` | Turns keyboard input into movement intent: walking, sprinting, sneaking, jumping, swimming, and the fly-mode toggle |
| `BlockInteraction.cs` | Crosshair targeting, timed mining (hardness × tool speed/tier, with durability wear), item drops, block placement, bucket use, and the right-click "use" hook for interactable blocks |
| `PlayerPhysics.cs` | Collision of an actor's box against the terrain, resolved one axis at a time; gravity and ground detection (shared by the player and mobs) |
| `PlayerHealth.cs` | 20 half-hearts and a breath meter: fall damage, drowning, and death/respawn events |
| `VoxelRaycaster.cs` | Steps the view ray block-by-block (Amanatides & Woo DDA) to find the targeted block and hit face, optionally including water/plants |

### `Items\` — what the player holds

| File | Responsibility |
|---|---|
| `ItemType.cs` | Every holdable item; ids 0–31 mirror `BlockType` byte values, tools/materials from 32 (sticks, coal, iron ingot, tools, buckets) |
| `ItemStack.cs` | An item type, count, and tool damage in a slot |
| `ItemInfo.cs` | Per-item tables: block↔item mapping, stack sizes, tool class/tier/durability, block drops, UI icons |
| `Inventory.cs` | The player's 24 slots (6×4); slots 0–5 are the hotbar |
| `Recipe.cs` | Shapeless recipes with all-or-nothing crafting and inventory rollback; some flagged as crafting-table-only |
| `Furnaces.cs` | Per-position furnace state: coal-fueled smelting (iron ore → ingot, sand → glass), lit-block toggling, output collection, and save/restore |
| `Chests.cs` | Per-position 12-slot chest storage, shared with the chest UI, with save/restore and break-into-inventory |
| `ItemDrops.cs` | Airborne item entities from broken blocks: gravity, pickup radius/delay, and despawn (never saved) |

### `UI\` — 2D overlay

| File | Responsibility |
|---|---|
| `Hud.cs` | Crosshair and the heart/air vitals bar |
| `Hotbar.cs` | Selected-slot input (keys/scroll) and the hotbar strip, a view over inventory slots 0–5 |
| `InventoryScreen.cs` | Full inventory overlay (`E`): click to lift/drop/merge/swap stacks, plus the click-to-craft recipe panel (basic by hand, advanced at a crafting table) |
| `ChestScreen.cs` | Chest overlay: the chest's 12 slots above the player inventory, same click model |
| `SlotRenderer.cs` | Shared slot drawing (frame, item icon, stack count) |
| `MenuScreen.cs` | Main menu (three world slots with save/delete) and the in-game pause menu (resume/save/quit to menu) |
| `DebugOverlay.cs` | `F3` panel: FPS, position/mode, chunk counts, camera angles, time of day, vertex count, and block- and biome-frequency snapshots |
| `PixelFont.cs` | Tiny procedural bitmap font — the project has no SpriteFont/content pipeline |

### `Audio\` — synthesized sound

| File | Responsibility |
|---|---|
| `GameSounds.cs` | Generates all sound effects at startup (noise through a lowpass with decay): per-material dig/break/place, footsteps, splash, and pickup. Self-disables on headless machines |

### `Persistence\` — worlds on disk

| File | Responsibility |
|---|---|
| `WorldSave.cs` | Reads/writes `world.json` (metadata format v5) and one gzip file per player-modified chunk; only touched chunks are stored |

## Design decisions

The choices that shape the code, written down so future-me doesn't relitigate them:

- **Column chunks, 16×128×16**, keyed by `(X, Z)` in a 2D grid. Block data is a flat `byte[]` per chunk (plus block-light and sky-light arrays) — no palette compression, no bit-packing; it's pointless at this scale.
- **Greedy meshing.** Coplanar faces with uniform tile, ambient occlusion, and torch light merge into large quads; faces with corner gradients stay 1×1 so the result is pixel-identical to naive culled meshing where it matters. Meshing splits into an opaque pass, a transparent water pass, an alpha-test cutout pass (plants and glass), and an emissive light overlay.
- **Threading rule:** worker threads generate block data and build vertex arrays; **only the main thread touches the GraphicsDevice**, uploading a few finished meshes per frame from a queue. A chunk is meshed only once its 3×3 neighborhood has block data (AO samples diagonal neighbors), so borders are always seamless.
- **Block edits remesh synchronously** on the main thread so breaking/placing feels instant.
- **Lighting is a few cheap channels, not a full engine.** A fixed per-face directional shade plus per-vertex ambient occlusion gives the base look. A vertical-only sky-light channel (sun straight down each column, stopped by the first opaque block) is baked into vertex brightness and scaled by the day tint, so caves stay dark at noon. Torch/furnace block-light propagates through its own array and is max-blended over the day-lit pass, so lit surfaces — and underground torches — stay bright. Sky-light does not spread sideways, so enclosed rooms need torches even with a window.
- **Water is a self-verifying automaton.** Each disturbed cell recomputes its own state from its neighbors, so flows spread, fall, and drain toward a stable configuration without a global solver, and freeze cleanly at unloaded-chunk borders.
- **Everything is generated in code** at startup — the texture atlas (with optional PNG overrides) and every sound effect. No art assets, no audio files, and no MonoGame content pipeline (MGCB) involvement.
- **Only player-modified chunks are saved.** Everything else — terrain, ores, trees, mobs, item drops — regenerates or respawns; mobs and drops are deliberately ambient and never persisted.

## Save format

Saves live in `saves\{default,world2,world3}\` relative to the working directory (next to the executable when launching the exe directly, or in the project root under `dotnet run`/F5). Slot 1 keeps the historical `default\` directory name so old saves survive.

- `world.json` — metadata format version 5: seed, player position/rotation, selected hotbar slot, fly mode, inventory (with per-tool damage), time of day, furnace states, and chest contents
- `chunks\c_{x}_{z}.bin` — one file per *modified* chunk: a small header (magic, version, chunk coordinate) followed by the raw block array behind a `GZipStream`. Written write-then-rename so a crash can't corrupt an existing save

Saving happens automatically when chunks unload, when quitting to the menu, and when the game exits; `F5` and the pause menu's SAVE button force a full save. All save paths settle airborne falling blocks first so none are lost between sessions.

## Non-goals

Written down so scope creep has to argue with a document:

- No multiplayer
- No hostile mobs or combat — the pigs and chickens are passive scenery
- No gamepad — mouse and keyboard only

## Credits

- [MonoGame](https://monogame.net/) — Microsoft Public License / MIT
- [FastNoiseLite](https://github.com/Auburn/FastNoiseLite) — MIT (vendored single-file C# noise library)
