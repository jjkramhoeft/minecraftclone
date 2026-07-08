# TODO — improvement ideas from comparing with other Minecraft clones

Compared against: [fogleman/Craft](https://github.com/fogleman/Craft) (C, ~10k lines),
[TrueCraft](https://github.com/ddevault/TrueCraft) (C#, reimplements Minecraft beta 1.7.3),
[Luanti / Minetest](https://www.luanti.org/en/) (C++ engine, the open-source ceiling),
[Terasology](https://terasology.org/) (Java, survival/rendering showcase), and classic/beta
Minecraft itself as the reference.

## Main outstanding 

- [ ] Fix lighting in caves/under ground. It's supposed to be dark underground, no matter what 
      time of day it is. Currently it is light in caves under ground at daytime, and only dark under ground when it also is dark obove ground at night time. Torches should be the main light source under ground.
- [ ] Only show available recipies (if not at CraftingTable do not show recipies from crafting table)
- [ ] Hold active tool in hand. Visual improvement. Currently the players hands are alway empty, 
      even when a tool is equipped (make pickaxe, axe, shovel and bucket)
- [ ] Stars should not be visible through the clouds

## Future ideas (not ready yet)

- [ ] More biomes: Mountains, Pine forest, Ocean
- [ ] More Blocks: Cobblestone (placed stone), Clay, Bricks (from furnaced clay), Cravel, 
      Snow, Limestone, Goldore, Gold, Fern, Cactus
- [ ] Add 'blob' caves in mountain biomes, with goldore
- [ ] Enemies
- [ ] Better break up decal. Do not use the current ramdom 'splatter' - there should be progression 
      and some build up in the dark pattern.
- [ ] Better cobblestone texture      