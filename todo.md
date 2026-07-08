# TODO — improvement ideas from comparing with other Minecraft clones

Compared against: [fogleman/Craft](https://github.com/fogleman/Craft) (C, ~10k lines),
[TrueCraft](https://github.com/ddevault/TrueCraft) (C#, reimplements Minecraft beta 1.7.3),
[Luanti / Minetest](https://www.luanti.org/en/) (C++ engine, the open-source ceiling),
[Terasology](https://terasology.org/) (Java, survival/rendering showcase), and classic/beta
Minecraft itself as the reference.


## High impact — the world itself

- [x] **Caves.** Carve tunnels with 3D noise (FastNoiseLite supports 3D; threshold a
      ridged/simplex field, or swept "worm" carvers) in `World/TerrainGenerator.cs`.
      Right now underground is solid stone, so mining has no reward loop. Skip carving
      below y≈4 to fake bedrock. Watch interaction with `WaterLevel` (don't breach the
      ocean floor, or embrace it and let water pour in — the flow sim already handles it).
- [x] **Ores.** Coal + iron as new `BlockType`s with depth-banded spawn blobs. Coal
      enables torches; iron enables a tier-3 tool set on the existing
      `GetRequiredTier`/`GetToolTier` scaffolding in `World/BlockInfo.cs` /
      `Items/ItemInfo.cs`. Without ores the stone tier is a dead end.
- [x] **Biomes.** A second, low-frequency noise (temperature/moisture) selecting
      surface palette + decoration density: desert (sand, no trees), forest (dense
      trees), plains. Even 3 biomes breaks up the current single-texture landscape.
      Amplitude modulation per biome gives mountains cheaply.
- [x] **Block light propagation (torches).** The flagship visual upgrade, and Craft
      has placeable lights already. Classic approach: per-block light nibble, BFS flood
      from emitters on place/break, vertex color = `max(skyTint, blockLight)` in
      `Rendering/ChunkMesher.cs` (AO already multiplies per-vertex color, so the
      plumbing exists). Pairs with caves + coal→torch crafting to complete the loop:
      *mine → craft torch → explore cave*.

## High impact — game feel

- [ ] **Audio.** The game is completely silent — no `SoundEffect` usage anywhere.
      In keeping with the zero-asset ethos (textures are procedural in
      `Rendering/TextureAtlas.cs`), generate sounds procedurally into
      `SoundEffect.FromStream`/`new SoundEffect(buffer,...)`: noise-burst dig/place per
      material, splash, footsteps. Even 5 crude sounds transform the feel.
- [ ] **Health, fall damage, drowning, respawn.** The survival foundation every
      comparison target has. Hearts on the HUD (`UI/Hud.cs`), fall damage from impact
      velocity in `Player/PlayerPhysics.cs` (velocity is already known at landing),
      air meter while the eye cell is water, death → respawn at spawn point, keep it
      simple: drop nothing on death at first.
- [ ] **Simple mobs.** Even 1–2 passive animals (chicken/pig analogue) wandering on
      land makes the world feel inhabited. The `Rendering/PlayerModel.cs` box-model +
      walk-swing animation code generalizes to quadrupeds; `FallingBlocks` shows the
      entity-update pattern. Hostiles can wait — pathfinding is the hard part.

## Medium — systems and UX

- [ ] **Menus + world slots.** No main menu, pause menu, or world list; `N` silently
      deletes the world. `Persistence/WorldSave.cs` already takes a `worldName` — a
      world-select screen and an Esc pause menu (resume/save/quit) close the roughest
      UX edge. Requires letters in `UI/PixelFont.cs` (currently digits + 'x' only) —
      extend the 3×5 glyph table to A–Z first.
- [ ] **Furnace + smelting.** Iron ore → ingots, sand → glass. First block with state
      and a timer; follows the chest/container pattern below.
- [ ] **Chests.** First container block; needs per-block inventory storage keyed by
      position, saved in world metadata (the chunk byte array can't hold it).
- [ ] **Item drops as world entities.** Broken blocks currently teleport into the
      inventory (and are *lost* when it's full — see `BlockInteraction.BreakBlock`).
      Spawn a small bobbing cube entity with pickup radius instead, reusing the
      `FallingBlocks` entry pattern.
- [ ] **Sneak.** LeftShift while grounded: slower speed + edge-stop (don't walk off
      the block you're standing on). Small change in `Player/PlayerController.cs` +
      an extra ledge probe in `PlayerPhysics`; makes building over voids viable.
- [ ] **Clouds.** Craft has them and they're cheap: a flat translucent quad layer at
      y≈100 scrolled by time, or noise-sampled cell quads. Big sky-feel win for an
      afternoon of work.
- [ ] **On-screen debug overlay (F3).** Stats currently go to the window title.
      After PixelFont gets letters, draw position/fps/chunk counts as an overlay.
- [ ] **Tool durability.** `ItemStack` needs a damage value; tools currently last
      forever, which cheapens the tier ladder.

## Performance headroom (fine today, will bite as the world grows)

- [ ] **Greedy meshing** in `Rendering/ChunkMesher.cs` — merges coplanar faces,
      typically 5–10× fewer vertices; the AO-aware variant is well documented.
- [ ] **Raise render distance** (`LoadRadius 8` = 128 blocks is modest; Craft ships
      with much larger visible ranges). Likely needs greedy meshing first, plus fog
      pushed out to match.
- [ ] **Sub-column meshing or empty-section skip.** Chunks are full 16×128×16 meshes;
      splitting into 16³ sections culls better vertically once caves exist.

## Small polish (grab-bag)

- [ ] Buckets: pick up / place water sources (the flow sim makes this fun now).
- [ ] Infinite-water rule: two adjacent sources over solid ground create a third —
      removes the "dip" left when a surface source is removed.
- [ ] Per-corner water surface smoothing so flows slope instead of stair-step.
- [ ] Sprint field-of-view kick + swim/splash particles.
- [ ] Crafting: keep the recipe-list UI (it's honestly friendlier than a grid), but
      gate advanced recipes behind a crafting table block for progression texture.
- [ ] Star twinkle + moon phases in `Rendering/SkyRenderer.cs`.
