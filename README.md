# Minecraft Clone

A small Minecraft-style voxel sandbox written in C# with [MonoGame](https://monogame.net/) (DesktopGL). No mobs, no NPCs — just you and an infinite, procedurally generated landscape you can explore, dig into, and build on.

## Features / Roadmap

Each phase produces something runnable. Checked boxes are done.

- [x] **Phase 0 — Scaffold + window**: MonoGame project builds and opens a 1280×720 window, Escape quits
- [x] **Phase 1 — Fly camera**: first-person mouse-look + WASD free-fly, one test cube rendered with `BasicEffect`
- [x] **Phase 2 — One chunk meshed**: 16×128×16 terrain chunk from a noise heightmap, culled meshing, per-face shading
- [x] **Phase 3 — Infinite world**: chunks generate/mesh on background threads as you move, unload behind you, seamless borders
- [ ] **Phase 4 — Walking player**: gravity, jumping, AABB collision against the terrain (with a fly-mode debug toggle)
- [ ] **Phase 5 — Break & place blocks**: DDA raycast from the crosshair, left-click break, right-click place, block highlight
- [ ] **Phase 6 — Textures, block variety, hotbar**: procedural texture atlas; grass, dirt, stone, sand, wood, leaves; hotbar selection
- [ ] **Phase 7 — Save & load**: modified chunks persist to disk, world seed + player position saved
- [ ] **Phase 8 — Polish (optional)**: distance fog, frustum culling, water, ambient occlusion, sounds

## Controls (planned)

| Input | Action |
|---|---|
| `W A S D` | Move |
| Mouse | Look around |
| `Space` | Jump (fly up in fly mode) |
| `Left Shift` | Fly down (fly mode) |
| Left click | Break block |
| Right click | Place block |
| `1`–`9` / scroll wheel | Select hotbar slot |
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

## Architecture

Single executable project; namespaces mirror folders.

```
MainGame.cs      Game subclass and composition root — owns one instance of each system
World\           Block types, chunk storage, chunk lifecycle (ChunkManager), terrain generation (FastNoiseLite)
Rendering\       First-person camera, chunk meshing, texture atlas, world renderer
Player\          Input, movement, AABB physics, voxel raycasting
UI\              Hotbar, crosshair, debug HUD
Persistence\     Saving and loading modified chunks + world metadata
```

Load-bearing design decisions (so future-me doesn't relitigate them):

- **Column chunks, 16×128×16**, keyed by `(X, Z)` in a 2D grid. Block data is a flat `byte[]` (32 KB per chunk) — no palette compression, no bit-packing; it's pointless at this scale.
- **Naive culled meshing**, not greedy: emit a quad only for faces bordering a non-solid block. Removes ~99% of faces and keeps texturing and per-vertex shading simple. `ChunkManager.GetBlock/SetBlock` (world coordinates) is the single API used by meshing, physics, and raycasting.
- **Threading rule:** worker threads generate block data and build vertex arrays; **only the main thread touches the GraphicsDevice**, uploading a few finished meshes per frame from a queue. Block data is generated one chunk beyond the mesh radius so border faces cull correctly.
- **Block edits remesh synchronously** on the main thread (a chunk meshes in well under 2 ms) so breaking/placing feels instant.
- **Textures are generated in code** at startup — a 256×256 atlas of 16×16 tiles with per-tile color + noise speckle. No art assets, and no MonoGame content pipeline (MGCB) involvement.
- **Only player-modified chunks are saved.** Everything else regenerates deterministically from the world seed.

## Save format

Saves live in `saves\default\` next to the executable:

- `world.json` — seed, format version, player position/rotation, selected hotbar slot
- `chunks\c_{x}_{z}.bin` — one file per *modified* chunk: small header (magic, version, chunk coordinate) followed by the raw 32 KB block array behind a `GZipStream`

## Non-goals

Written down so scope creep has to argue with a document:

- No mobs, NPCs, or other creatures
- No multiplayer
- No crafting, inventory management, or survival mechanics (v1 is creative-mode building)

## Credits

- [MonoGame](https://monogame.net/) — Microsoft Public License / MIT
- [FastNoiseLite](https://github.com/Auburn/FastNoiseLite) — MIT (vendored single-file C# noise library, added in Phase 2)
