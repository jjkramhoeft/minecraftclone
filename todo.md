# TODO — improvement ideas from comparing with other Minecraft clones

Compared against: [fogleman/Craft](https://github.com/fogleman/Craft) (C, ~10k lines),
[TrueCraft](https://github.com/ddevault/TrueCraft) (C#, reimplements Minecraft beta 1.7.3),
[Luanti / Minetest](https://www.luanti.org/en/) (C++ engine, the open-source ceiling),
[Terasology](https://terasology.org/) (Java, survival/rendering showcase), and classic/beta
Minecraft itself as the reference.


## Small polish (grab-bag)

- [x] Per-corner water surface smoothing so flows slope instead of stair-step.
- [x] Sprint field-of-view kick + swim/splash particles.
- [x] Crafting: keep the recipe-list UI (it's honestly friendlier than a grid), but
      gate advanced recipes behind a crafting table block for progression texture.
- [x] Star twinkle + moon phases in `Rendering/SkyRenderer.cs`.
- [x] No fall damage when starting (first fall from the heavens)
- [x] Debug Terrain Generator: Calculate block frequancy (run and show when F3 is pressed)

## Future ideas (not ready yet)

- [ ] More biomes: Mountains, Pine forest, Ocean
- [ ] More Blocks: Cobblestone (placed stone), Clay, Bricks (from furnaced clay), Cravel, 
      Snow, Limestone, Goldore, Gold, Fern, Cactus
- [ ] More Tree types: Birch & Pine
- [ ] Under water visuals. Tint all blue
- [ ] Add 'blob' caves
- [ ] Enemies
- [ ] Player skins
- [ ] Longer view distance