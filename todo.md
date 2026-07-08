# TODO — improvement ideas from comparing with other Minecraft clones

Compared against: [fogleman/Craft](https://github.com/fogleman/Craft) (C, ~10k lines),
[TrueCraft](https://github.com/ddevault/TrueCraft) (C#, reimplements Minecraft beta 1.7.3),
[Luanti / Minetest](https://www.luanti.org/en/) (C++ engine, the open-source ceiling),
[Terasology](https://terasology.org/) (Java, survival/rendering showcase), and classic/beta
Minecraft itself as the reference.

## Future ideas (not ready yet)

- [ ] More biomes: Mountains, Pine forest, Ocean
- [ ] More Blocks: Cobblestone (placed stone), Clay, Bricks (from furnaced clay), Cravel, 
      Snow, Limestone, Goldore, Gold, Fern, Cactus
- [ ] Add 'blob' caves in mountain biomes, with goldore
- [ ] Enemies
- [ ] Better break up decal. Do not use the current ramdom 'splatter' - there should be progression 
      and some build up in the dark pattern.
- [ ] Better cobblestone texture      
- [ ] Fix The held-tool position/angle and scale. If it sits wrong in the hand, the numbers to tweak are
      the local matrix in DrawHeldItem (Rendering/PlayerModel.cs) 
      — scale 0.55, 
      — the Z/Y rotations, 
      — and the translation (0.5, -0.5, -0.9).
- [ ] Fix the arm swing when walking in 1st person view