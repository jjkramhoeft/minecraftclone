# Minecraft Clone

A small Minecraft-style voxel sandbox written in C# with [MonoGame](https://monogame.net/) (DesktopGL) for Windows. No mobs, no NPCs — just you and an infinite, procedurally generated landscape you can explore, dig into, and build on.

You spawn in a world of rolling grass hills, forests, sandy beaches, and still lakes, all generated from a single seed. Walk, jump, sprint, and swim across it — or toggle fly mode and soar over it. Aim at any block to break it, or place one of six block types from the hotbar. Everything you change is saved and waiting for you the next time you play.

The whole game is plain C# on top of MonoGame's rendering primitives: chunk meshing, physics, raycasting, texture generation, and world persistence are all implemented in this repository. There are no art assets — every texture is generated in code at startup. The only third-party code besides MonoGame itself is a single vendored noise library file.

## Features

- **Infinite terrain** that streams in around you on background threads: hills, forests, beaches, and lakes, deterministic from one world seed
- **First-person movement** with gravity, jumping, collision, sprinting, swimming, and a free-fly mode
- **Building and digging**: break any block, place grass, dirt, stone, sand, wood, or leaves
- **Pixel-style textures** from a procedurally generated texture atlas
- **Lighting look** from per-face directional shading plus per-vertex ambient occlusion
- **Distance fog** blending the horizon into the sky
- **Persistent worlds**: your edits, position, and settings survive restarts

## Controls

Mouse and keyboard only.

| Input | Action |
|---|---|
| `W A S D` | Move |
| Mouse | Look around |
| `Space` | Jump / swim up (fly up in fly mode) |
| `Left Shift` | Fly down (fly mode) |
| `Left Ctrl` | Sprint |
| Left click | Break block |
| Right click | Place block |
| `1`–`6` / scroll wheel | Select hotbar slot |
| `F` | Toggle fly mode |
| `F5` | Save world |
| `Esc` | Quit |

## Build & run

Prerequisites: [.NET SDK](https://dotnet.microsoft.com/download) 9.0 or later.

```powershell
dotnet tool restore   # restores the MonoGame content tools (needed once per clone)
dotnet build
dotnet run
```

### VS Code

Open the folder in VS Code with the **C# Dev Kit** extension installed:

- `Ctrl+Shift+B` builds (default build task)
- `F5` builds and launches the game with the debugger attached

## Components & structure

Single executable project; namespaces mirror folders. [MainGame.cs](MainGame.cs) is the composition root: it owns one instance of each system, wires input to them in `Update`, and orders the passes in `Draw`.

### `World\` — the voxel world

| File | Responsibility |
|---|---|
| `BlockType.cs` | The block palette: a `byte`-backed enum (Air, Grass, Dirt, Stone, Sand, Wood, Leaves, Water) |
| `BlockInfo.cs` | Per-block properties: solidity and which texture-atlas tile each face uses |
| `Chunk.cs` | Storage for one 16×128×16 column of blocks — a flat 32 KB `byte[]` with a single index-layout helper, plus dirty/modified flags |
| `ChunkCoord.cs` | A chunk's position in the 2D chunk grid |
| `ChunkManager.cs` | The heart of the world: streams chunks in and out around the player, schedules background generation/meshing, and exposes `GetBlock`/`SetBlock` in world coordinates — the single block API used by rendering, physics, and raycasting |
| `TerrainGenerator.cs` | Fills chunks deterministically from the seed: fBm-noise heightmap, stone/dirt/grass strata, sandy lowlands, water-filled valleys, and hash-placed trees |
| `FastNoiseLite.cs` | Vendored single-file noise library (MIT) |

### `Rendering\` — from blocks to pixels

| File | Responsibility |
|---|---|
| `FirstPersonCamera.cs` | Yaw/pitch mouse-look camera; provides view/projection matrices and the direction vectors movement and raycasting use |
| `ChunkMesher.cs` | Turns a chunk's blocks into vertex/index arrays: one quad per face bordering a non-solid block, with per-face shading and per-vertex ambient occlusion; water goes into a separate list for the transparent pass. Pure CPU code, safe on worker threads |
| `ChunkMesh.cs` | GPU buffers for one chunk (opaque + optional water set); created and disposed on the main thread only |
| `TextureAtlas.cs` | Generates the 256×256 block texture atlas in code at startup and maps tile indices to UV rectangles |
| `WorldRenderer.cs` | Draws all visible chunk meshes: frustum culling, distance fog, an opaque pass, then a blended water pass |
| `BlockHighlight.cs` | Wireframe outline around the block under the crosshair |

### `Player\` — input, physics, interaction

| File | Responsibility |
|---|---|
| `PlayerController.cs` | Turns keyboard input into movement intent: walking, sprinting, jumping, swimming, and the fly-mode toggle |
| `PlayerPhysics.cs` | Collision of the player's 0.6×1.8×0.6 box against the terrain, resolved one axis at a time; gravity and ground detection |
| `VoxelRaycaster.cs` | Steps the view ray block-by-block (Amanatides & Woo DDA) to find the targeted block and hit face for breaking/placing |

### `UI\` — 2D overlay

| File | Responsibility |
|---|---|
| `Hud.cs` | Crosshair |
| `Hotbar.cs` | Block selection (keys/scroll) and the slot bar rendered from atlas tiles |

### `Persistence\` — worlds on disk

| File | Responsibility |
|---|---|
| `WorldSave.cs` | Reads/writes `world.json` (seed, player state, hotbar) and one gzip file per player-modified chunk |

## Design decisions

The choices that shape the code, written down so future-me doesn't relitigate them:

- **Column chunks, 16×128×16**, keyed by `(X, Z)` in a 2D grid. Block data is a flat `byte[]` (32 KB per chunk) — no palette compression, no bit-packing; it's pointless at this scale.
- **Naive culled meshing**, not greedy: emit a quad only for faces bordering a non-solid block. Removes ~99% of faces and keeps texturing and per-vertex shading simple.
- **Threading rule:** worker threads generate block data and build vertex arrays; **only the main thread touches the GraphicsDevice**, uploading a few finished meshes per frame from a queue. A chunk is meshed only once its 3×3 neighborhood has block data (ambient occlusion samples diagonal neighbors), so borders are always seamless.
- **Block edits remesh synchronously** on the main thread (a chunk meshes in well under 2 ms) so breaking/placing feels instant.
- **Textures are generated in code** at startup — a 256×256 atlas of 16×16 tiles with per-tile color + noise speckle. No art assets, and no MonoGame content pipeline (MGCB) involvement.
- **Only player-modified chunks are saved.** Everything else regenerates deterministically from the world seed.
- **Water is still**: it renders semi-transparent in a second pass and you swim in it, but there is no flow simulation — breaking a block under water leaves air.

## Save format

Saves live in `saves\default\` relative to the working directory (next to the executable when launching the exe directly, or in the project root under `dotnet run`/F5):

- `world.json` — seed, format version, player position/rotation, selected hotbar slot, fly mode
- `chunks\c_{x}_{z}.bin` — one file per *modified* chunk: small header (magic, version, chunk coordinate) followed by the raw 32 KB block array behind a `GZipStream` (~1-3 KB on disk)

Saving happens automatically when chunks unload and when the game exits; `F5` forces a full save. Delete the `saves` folder for a fresh world.

## Non-goals

Written down so scope creep has to argue with a document:

- No mobs, NPCs, or other creatures
- No multiplayer
- No crafting, inventory management, or survival mechanics (this is creative-mode building)
- No gamepad — mouse and keyboard only

## Credits

- [MonoGame](https://monogame.net/) — Microsoft Public License / MIT
- [FastNoiseLite](https://github.com/Auburn/FastNoiseLite) — MIT (vendored single-file C# noise library)
