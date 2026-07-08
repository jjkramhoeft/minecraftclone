bool smoke = System.Array.Exists(args, a => a == "--smoke");
using var game = new MinecraftClone.MainGame(smoke);
game.Run();
