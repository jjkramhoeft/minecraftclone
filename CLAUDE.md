# MinecraftClone

Single-project MonoGame (DesktopGL) voxel game on .NET 9. No solution file, no test projects. Entry point: `Program.cs` → `MainGame.cs` (composition root).

## Layout

Folders mirror namespaces:

- `World/` — chunks, terrain generation, block updates, water flow, falling blocks
- `Rendering/` — world/sky/player renderers, texture atlas, meshing
- `Player/` — controller, camera, raycasting, interaction
- `Items/` — inventory, item stacks, crafting
- `UI/` — HUD, hotbar, inventory screen, pixel font
- `Persistence/` — `WorldSave.cs` (save format: `saves/{world}/world.json` + per-chunk gzip files)

## Commands

```
dotnet tool restore                      # once per clone (MGCB tools)
dotnet build MinecraftClone.csproj       # compile
dotnet run                               # play (main menu with 3 world slots; slot 1 = saves/default/)
dotnet run -- --smoke                    # smoke test: ~3 s throwaway world, prints summary, exits
```

Textures are generated in code — the MGCB content pipeline (`Content/Content.mgcb`) is effectively unused. Don't add assets there.

## Verification policy — IMPORTANT

- Verify changes with `dotnet build`, then `dotnet run -- --smoke` and read its stdout: a `SMOKE OK` line with frame/fps/chunk stats. Non-zero exit or a crash means the change broke startup or the core loop.
- **Do NOT launch the game and take screenshots or screen captures to verify changes.** The user does all visual/gameplay inspection manually in VS Code (launch config "C#: MinecraftClone Debug"). Only capture the screen if the user explicitly asks for it.
- For rendering changes the smoke test can't judge, describe the expected visual result and ask the user to check it in-game.
- See the project skill `.claude/skills/verify-game/SKILL.md` for details.

## Gotchas

- World saves live in `saves/{default,world2,world3}/` relative to the working directory, so they exist both under the repo root and under `bin/Debug/net9.0/`. Never modify or delete them; smoke mode deliberately uses a separate `saves/smoke/` world and skips saving.
- `README.md` was refreshed on 2026-07-08 to match the current feature set; keep it in sync when adding player-facing features.
- Keep `Update`/`Draw` paths allocation-free — per-frame garbage causes GC hitches in MonoGame.
- Escape opens the pause menu (resume/save/quit to menu); the game exits via the menu or window close and autosaves in `OnExiting` when a world is loaded. Smoke mode skips that autosave.
