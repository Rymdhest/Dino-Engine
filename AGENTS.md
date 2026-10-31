# Agent Instructions — Dino Engine

## CRITICAL: File editing rules for this environment (Windows / PowerShell)

The `edit_existing_file` tool is **broken in this environment**: it truncates files at the edit point and silently strips UTF-8 BOMs. Do NOT use it for partial edits. Instead:

1. **Small targeted change** → on-disk string replacement via PowerShell:
   ```powershell
   $c = [IO.File]::ReadAllText($path)
   if (-not $c.Contains("old text")) { throw "pattern not found" }
   $c = $c.Replace("old text", "new text")
   [IO.File]::WriteAllText($path, $c)
   ```
2. **Large change / whole-file rewrite** → delete the file and recreate it with `create_new_file`.
3. **Never issue parallel edits to the same file** — concurrent writes corrupt files (observed: two simultaneous edits truncated a file to 10 lines).
4. **Verify after every write**: check line count (`(Get-Content $path).Count`) and/or build.

## Safety net (always available)

This repo is a git repository. If any file gets mangled:

```powershell
git checkout -- .            # restore all modified files to last commit
git diff --stat              # spot-check: huge deletion counts on lightly-touched files = truncation
dotnet build "Dino Engine.sln" -c Debug --nologo   # truncation always breaks the C# build
```

Commit a checkpoint (`git add -A; git commit -m "checkpoint"`) before any multi-file refactor.

## Project notes

- Solution: `Dino Engine.sln` (projects: `Dino Engine/`, `Dino Defenders/`). Build with `dotnet build`.
- Terrain uses **fixed-size chunks** streamed in a ring around the camera (`TerrainChunkSystem` + `TerrainQuadTreeSingleton.Chunks`), NOT an LOD quadtree. Tunables are registered in `Dino Defenders/DemoGame.cs`: `new TerrainQuadTreeSingleton(chunkSize: 64f, radius: 2)`.
- Grass rendering is fully GPU-driven via `GrassRenderer`; there is no CPU grass system (the old `GrassChunkRenderSystem` stub was removed).
