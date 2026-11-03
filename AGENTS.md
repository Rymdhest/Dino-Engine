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
- Terrain uses **fixed-size chunks** streamed in a ring around the camera (`TerrainChunkSystem` + `TerrainQuadTreeSingleton.Chunks`), NOT an LOD quadtree. Tunables are registered in `Dino Defenders/DemoGame.cs`: `new TerrainQuadTreeSingleton(chunkSize: 8f, radius: 5)`.
- Grass is fully GPU-driven (after Ghost of Tsushima), with no CPU grass system: settings in `GrassSingleton` (F3 Render window), submitted with the visible chunks by `TerrainChunkRenderSystem`. Per view (camera, and each shadow) `GrassRenderer` runs `Shaders/Grass/Grass_Generate.comp`, which scatters blades on a world-seeded grid, clumps them into Voronoi tufts, thins them with distance, culls them and appends finished Bézier blades to a near (15 vertex) and far (7 vertex) list drawn with `glDrawArraysIndirect` (GL 4.3). The `GrassBlade` layout in `Grass/GrassBlade.glsl` must match `GrassRenderer.BladeStride`; the `GrassData` UBO (binding 7) layout in `Grass/GrassField.glsl` must match `GrassRenderer.UploadSettings`. `Terrain.frag` includes `Grass/GrassField.glsl` to tint the ground under the grass, so grass colour and ground tint change together. Grass draws before terrain so blades early-z-reject the terrain's parallax shading.
- Wind is one engine-wide system: settings/state in `WindSingleton` (advanced by `WindSystem`), GPU side in `WindRenderer` (wind map + vegetation spring sim, `WindData` UBO at binding 6), shader API in `Shaders/Wind.glsl`, CPU API in `Physics/Wind/` (`Wind.Sample`, `Wind.AddBlast`). `WindField.cs` mirrors `Wind.glsl` exactly: change both together. Grass, instanced vegetation and volumetric fog all read it; new consumers should too.
- Water: settings/state in `WaterSingleton` (advanced by `WaterSystem`, after `WindSystem`); still water is entities with `WaterBodyComponent` + `PositionComponent` (+ optional `RotationComponent` yaw); the ocean fills all loaded terrain below `SeaLevel`. GPU side in `Rendering/Renderers/PosGeometry/Water*.cs`: `WaterRenderer.SimulationPass` runs at frame start (shore field + ripple sim), the surface pass runs after `LightPass` (forward, over a copy of the lit scene) and blits its depth back into the G-buffer. Shaders in `Shaders/Water/`; `WaterShape.glsl` mirrors `Physics/Water/WaterShape.cs` and the swell set / constants in `Water_Ocean.frag` mirror `WaterWaves.cs`: change both together. Water decides where it stands from the terrain mesh (`WaterShoreField.HeightTexture`), never the parallax-mapped depth buffer. CPU API: `Physics/Water/` (`Water.Sample`, `Water.AddSplash`, `WaterQuery`, `WaterBasins.Find` for placing ponds in terrain hollows). Light systems submit lights to `_waterRenderer` as they do to the fog.
- GUI lives in `Dino Engine/GUI/` (retained-mode element tree, not ECS). `Engine.Gui` is the `GuiContext`; it updates before game/world update and renders to the back buffer at the end of `RenderEngine.Render`. Gameplay input must check `Engine.Gui.WantsMouse` / `WantsKeyboard`. Text uses a built-in TrueType parser/rasterizer reading OS fonts (`GUI/Text/`), no font assets. Shaders: `Shaders/GUI.vert/.frag` (uniform lines must be `uniform type name;` at column 0 for `ShaderProgram`'s parser). Debug overlay (F3): `GUI/Debug/`.
