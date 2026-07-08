---
name: verify-game
description: Verify MinecraftClone changes after code edits — build plus smoke run, no screenshots. Use after any change to game code to confirm it compiles and the core loop still runs.
---

# Verify MinecraftClone changes

## Steps

1. Build:
   ```
   dotnet build MinecraftClone.csproj
   ```
2. Smoke run:
   ```
   dotnet run -- --smoke
   ```
   This starts the game against a throwaway world (`saves/smoke/`, fixed seed, nothing loaded from or written to `saves/default/`), runs 180 update frames (~3 s), prints a summary to stdout, and exits.

## Interpreting the result

- Success looks like:
  `SMOKE OK — 180 updates / 175 draws in 3.0s (58 fps) | 41 chunks loaded (0 pending) | player at 8.5, 34.2, 8.5`
- Check that fps is roughly 55–60, chunks loaded is well above zero, and the player Y has settled onto terrain (below the 70 spawn height).
- A crash, hang, or non-zero exit code means the change broke startup or the core Update/Draw loop — read the exception text, don't re-run hoping it passes.
- Output is only visible when stdout is captured/piped (the game is a WinExe); running the command through the shell tool captures it correctly.

## Rules

- **Never take screenshots or screen captures to verify a change** unless the user explicitly asks for a visual check. The user inspects visuals manually in VS Code (launch config "C#: MinecraftClone Debug").
- For rendering-only changes the smoke test can't judge (colors, lighting, mesh appearance), verify the build + smoke run, then describe the expected visual result and ask the user to confirm in-game.
- Never touch `saves/default/` — that's the user's real world.
