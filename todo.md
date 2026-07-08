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