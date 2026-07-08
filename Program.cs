bool smoke = System.Array.Exists(args, a => a == "--smoke");
bool dumpTextures = System.Array.Exists(args, a => a == "--dump-textures");
using var game = new MinecraftClone.MainGame(smoke, dumpTextures);
game.Run();
