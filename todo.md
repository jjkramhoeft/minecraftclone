# TODO — improvement ideas from comparing with other Minecraft clones

Compared against: [fogleman/Craft](https://github.com/fogleman/Craft) (C, ~10k lines),
[TrueCraft](https://github.com/ddevault/TrueCraft) (C#, reimplements Minecraft beta 1.7.3),
[Luanti / Minetest](https://www.luanti.org/en/) (C++ engine, the open-source ceiling),
[Terasology](https://terasology.org/) (Java, survival/rendering showcase), and classic/beta
Minecraft itself as the reference.

## Medium — systems and UX

- [x] **Item drops as world entities.** Broken blocks currently teleport into the
      inventory (and are *lost* when it's full — see `BlockInteraction.BreakBlock`).
      Spawn a small bobbing cube entity with pickup radius instead, reusing the
      `FallingBlocks` entry pattern.
- [x] **Sneak.** LeftShift while grounded: slower speed + edge-stop (don't walk off
      the block you're standing on). Small change in `Player/PlayerController.cs` +
      an extra ledge probe in `PlayerPhysics`; makes building over voids viable.
- [x] **Clouds.** Craft has them and they're cheap: a flat translucent quad layer at
      y≈100 scrolled by time, or noise-sampled cell quads. Big sky-feel win for an
      afternoon of work.
- [x] **On-screen debug overlay (F3).** Stats currently go to the window title.
      After PixelFont gets letters, draw position/fps/chunk counts as an overlay.
- [x] **Tool durability.** `ItemStack` needs a damage value; tools currently last
      forever, which cheapens the tier ladder.

## Performance headroom (fine today, will bite as the world grows)

- [x] **Greedy meshing** in `Rendering/ChunkMesher.cs` — merges coplanar faces,
      typically 5–10× fewer vertices; the AO-aware variant is well documented.
      *(Done lighting-exact: only merges that reproduce per-block AO/torch
      gradients bit-for-bit. ~22% fewer vertices on this terrain — the rest is
      cave walls whose AO tuples are all unique; measured the unrestricted
      merge too and it gains nothing more.)*
- [x] **Raise render distance** (`LoadRadius 8` = 128 blocks is modest; Craft ships
      with much larger visible ranges). Likely needs greedy meshing first, plus fog
      pushed out to match. *(Now 12 chunks / 192 blocks, fog 130–186.)*
- [x] **Sub-column meshing or empty-section skip.** Chunks are full 16×128×16 meshes;
      splitting into 16³ sections culls better vertically once caves exist.
      *(Done as tight per-mesh Y bounds for frustum culling — same culling win
      as sections without splitting draw calls; world-bottom faces skipped too.)*

## Small polish (grab-bag)

- [x] Buckets: pick up / place water sources (the flow sim makes this fun now).
- [x] Infinite-water rule: two adjacent sources over solid ground create a third —
      removes the "dip" left when a surface source is removed.
- [ ] Per-corner water surface smoothing so flows slope instead of stair-step.
- [ ] Sprint field-of-view kick + swim/splash particles.
- [ ] Crafting: keep the recipe-list UI (it's honestly friendlier than a grid), but
      gate advanced recipes behind a crafting table block for progression texture.
- [ ] Star twinkle + moon phases in `Rendering/SkyRenderer.cs`.

## Future ideas (not ready yet)

- [ ] More biomes: Mountains, Pine forest, Ocean
- [ ] More Blocks: Cobblestone (placed stone), Clay, Bricks (from furnaced clay), Cravel, 
      Snow, Limestone, Goldore, Gold, Fern, Cactus
- [ ] More Tree types: Birch & Pine
- [ ] Under water visuals. Tint all blue
- [ ] Add 'blob' caves
- [ ] No fall damage when starting (first fall from the heavens)
- [ ] Enemies
- [ ] Player skins
- [ ] Debug Terrain Generator: Calculate block frequancy
- [ ] Longer view distance