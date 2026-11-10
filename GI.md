# Global illumination

How light bounces: the sky into a doorway, the sun off a sandy road onto whoever walks it, a fire off the walls around it. It takes the place of the flat sky and ground the ambient pass had, and of the fill every lamp used to add for its own bounce. It's one toggle in the settings, Global illumination, on by default, kept apart from Graphics quality until it's been played enough to go into the presets.

## The one idea

**The world is kept as voxels, and light is gathered from them by radiance cascades, on probes that stand in the world near what the camera sees.** The rest follows from the camera, which looks down on a patch of land some 60 by 25 m from 33 m away:

- **Nothing on screen is enough to light the screen.** Most of what lights a place is outside the picture or hidden from this camera: the sky over a roof, the far side of a wall, whatever stands behind the camera. Screen-space GI can't see any of it, and fades or flickers as it comes and goes. The voxels are built from the world's own data (`WorldData.Height`, the world's objects and their materials), never from what happens to be drawn, so what's under a roof is as solid as what's in view.
- **The probes stand in the world, not on the screen**, so moving the camera never makes them swim, and a probe found again next frame keeps what it worked out.
- **Nothing in the voxels moves**, so a lot is worked out once and kept: what each surface sees of the sun and sky, where each probe stands and which of its parents it can see, and what the far cascades see, a few frames apart. Lighting is the only thing redone every frame, so a lamp that flickers or a spell going off bounces at once.
- **The world is mostly outdoors, and its first rooms are the Haven's halls (`HALLS.md`)**, and both come out of the same rules: a probe merges only parents it can see, so a wall ends the sun's light at the wall, and a probe never moves to the far side of the surfaces that asked for it, so the probes lighting a roof never end up in the attic.

Radiance cascades (Sannikov, 2023) suit this better than the alternatives. Probe grids with irradiance and depth (DDGI) leak through thin walls and blur small things; Lumen's screen probes and Godot's cascaded voxels are made for a camera among things, and cost more than this camera needs; screen-space cascades, as Path of Exile 2 does them, see only the screen. Cascades get exact near light from many dense probes with few directions, and far light from few sparse probes with many, so every cascade costs about the same, and probes on the world's surfaces only (as in Freeman, Sannikov and Pitt-Francis, "Split radiance cascades", 2026) keep the count to what's in view.

```
 WorldData, objects ──> voxels: four nested windows ──> lit each frame (sun, sky, lamps, the bounce before)
                                       │
 G-buffer ──> which probes are wanted ──> placed (remembered, or found room) ──> traced, five cascades ──> merged ──> L1 irradiance
                                                                                                                     │
 G-buffer ──> gathered at half resolution (the probes in front of each surface that it can see) ──> ambient pass: diffuse and rough specular
```

Everything is on the GPU, and the CPU never waits on it, except for the debug views' report. `GlobalIlluminationRenderer` (in three files: the frame, the voxels, the probes) runs from `RenderEngine.LightPass`, after the G-buffer and before SSAO; `GlobalIlluminationSystem` tells it the world, its objects and which chunks have come; `Ambient_Light.frag` reads what it gathered.

## The voxels

Four levels of 192 by 64 by 128 voxels, 0.25, 0.5, 1 and 2 m across, so the finest window is 48 by 16 by 32 m and the coarsest 384 by 128 by 256 m. Each centres on a point 4 m ahead of the hero, the way the camera looks (`LevelAhead`), with at least 30% of its height below the hero (`WindowBelow`), as most of what stands stands up. A window is toroidal and moves in slabs of 16 voxels (`ScrollStep`), with three quarters of a slab of slack, so walking rebuilds a 4 m slab of the finest level every 4 m along an axis and nothing else; one level is rebuilt a frame at most (`UpdateVoxels`).

A rebuild (`BuildRegion`):

1. **Terrain** from the height map: solid up to the ground, coloured as `Terrain.frag` colours it (`GI_Ground.glsl`, which reads the chunks' surface weights and the material palette, rock as `Geology.glsl` says), and coloured again when a chunk's textures arrive (`RefreshGround`).
2. **Objects** drawn into a framebuffer with no attachments, each triangle along its dominant axis (`GI_Voxelize.geom`), with its own material, so a crown of leaves has the colour of its leaves. How much of a voxel a triangle covers is added up, as the triangles of one surface share a voxel and together fill it, and the voxel is made solid with that chance, the same way every time (`GI_Voxel_Finalize.comp`): a crown of leaves is a scatter of solid voxels that lets through about what the leaves would. Loose clutter and anything smaller than 0.4 of a voxel are left out. Characters, projectiles and particles are never voxels: they receive GI and don't cast it.
3. **Bricks** of 4 by 4 by 4 with a 64-bit mask each, which is what every ray reads, skipping empty bricks whole (`GI_Voxels.glsl`, `giTrace`).
4. **Surfaces**: which faces of each solid voxel are open (`GI_Voxel_Surface.comp`). Each surface voxel gets a place in its level's pool (`GI_Voxel_Index.comp`), with a radiance for each of its six faces, kept as three small floats in a uint.

**What a surface sees of the sun and sky is worked out once** (`GI_Voxel_Cache.comp`): four rays to the sun, a little apart for a soft edge, and eight to the sky from each open face. The sun stands where it is all day and the moon takes its place (`NightfallSingleton`), so this holds until the voxel is rebuilt. New voxels are listed when their level is laid out, and the list is gone through 2048 a level a frame once GI is showing (`CacheWork`), faster before, as nothing is shown until it's done. Until then a voxel guesses: in the sun, and the sky a face looking that way usually sees.

**Lighting** (`GI_Voxel_Light.comp`), every frame for the two finest levels and every second and fourth for the others: each open face, lit as the surface it belongs to (the steps of a slope alike, the two sides of a thin wall apart, `giSurfaceNormal`), takes the sun through the clouds (`weatherSunVisibility`), the sky it can see as an ambient cube of the sky map (`GI_Sky.comp`), every lamp and fire GI takes that it can see, by a voxel shadow ray, and the light of the surfaces around it, last frame, in the part of its sky they hide, which is how light goes on bouncing (`GI_Voxel_BrickLight.comp`). Then its own glow.

**Lamps**: whatever submits a light to the point light and spotlight renderers submits it here too (`SubmitPointLight`, `SubmitSpotLight`: `PointLightSystem`, `SpotLightSystem`, `ParticleEffectSystem`, `WeatherSystem`). GI takes the 64 that reach nearest the hero (`MaxLights`); a light renderer leaves out the fill that stood in for a light's bounce only for those (`FillKept`), so a lamp past them keeps its old fill rather than losing both.

## The probes

Five cascades, each with probes twice as far apart as the one before and four times the directions, each looking four times as far:

| Cascade | Apart | Directions | Looks |
|---|---|---|---|
| 0 | 0.5 m | 12 | 0.1 to 0.5 m |
| 1 | 1 m | 48 | 0.5 to 2 m |
| 2 | 2 m | 192 | 2 to 8 m |
| 3 | 4 m | 768 | 8 to 32 m |
| 4 | 8 m | 3072 | 32 to 128 m, then the sky |

Directions are HEALPix cells (`HealPix.cs`), nested, so each cell of a cascade is four of the next. The finest two cascades trace the finest voxels and each after them starts a level coarser (`GI_TRACE_LEVEL`), and every ray moves to a coarser level where it leaves a window.

**Which probes there are is worked out every frame** (`GI_Probe_Mark.comp` and on): the world is laid out in columns of each cascade's lattice, 256 by 128 m around a point 12 m ahead of the hero, each with a bit for each of 32 heights above a floor a metre under its lowest ground. Every fourth pixel of the G-buffer each way wants the eight probes at the corners of the cell its surface lies in, and says which way it faces; each probe wants its eight parents in the next cascade. Probes are numbered by a scan, so finding one is a bit count.

**Each is found among last frame's** (`GI_Probe_Recall.comp`), and keeps where it stood and which parents it could see for as long as it stands among the same voxels: each probe is stamped with the finest level its own rays and its parents' walk through where it stands (`giProbeStamp`), and when a window slides over it the stamp changes and it's set up again and looks along every direction that frame. **Where a probe stands and what it sees come from its place in the lattice and the voxels alone**, never from what's on screen or what happened before, so keeping it is only a saving and never a difference anyone can see. That's what makes GI stand still: a probe that kept a place worked out among coarser voxels stood inside the finer ground, saw nothing, and as the hero walked the ground ahead of them was crossed by dark bars that jumped from frame to frame. The others are listed (`GI_Probe_Init.comp`) and set up by many threads each, every cascade at once, as each takes many lookups that wait on one another:

- **Where it stands** (`GI_Probe_Place.comp`, 32 threads to a probe): a lattice point inside something, or up against it, moves to whichever of 78 spots around it has the most room. A finest probe inside something open on one side moves out that side and never the other, so one in a roof comes out above it and not into the attic, by the voxels around it; any other never moves down unless it's buried; a probe with nowhere to go is buried and sees nothing.
- **Which parents it can see** (`GI_Probe_See.comp`, a thread to each parent): a parent through a wall doesn't count in the merge, and if none can be seen, any that isn't buried is better than nothing.

**Tracing** (`GI_Probe_Trace.comp`): each probe looks along each of its directions over its own stretch, and keeps the light of whatever it strikes, or that it struck nothing. The coarse cascades look again every 2, 4 and 8 frames. **Merging** (`GI_Probe_Merge.comp`), from the coarsest down: what a probe sees in a direction is what its own stretch struck, or else what the parents it can see see beyond, blended by where it stands among them; past the coarsest is the sky. Each probe's result is averaged four directions at a time into those of the cascade below, which is all that cascade needs. **The finest two** become first-order spherical harmonics, irradiance E(n) = A + B·n for each colour (`GI_Probe_SH.comp`), with how open each finest probe stands: how many of its twelve rays struck nothing.

## The gather and the ambient pass

`GI_Gather.comp` runs at half the resolution each way. Each texel blends the eight finest probes around its surface, by where it stands among them, how much each is in front of it and how open it stands, but only those:

- **in front of the surface**, by the surface's own normal worked out from depth, as a shingle's normal map leans far enough that a probe under the roof could seem to be in front of it;
- **it can see**, by points through the finest voxels (`giClearNear`), past the solid voxels it starts in, as a point on a surface is often inside the voxels that stand for it.

Where none of them counts, the next cascade's do, and where none of those do, the texel says it has no answer.

Whether a texel sees a probe is a yes or a no from one point, and as the camera slides that point slides over the world, so along a fence's rails the answers would flicker. **What's gathered settles**: each texel takes in what was gathered last frame where the same spot of the world was then, by last frame's camera, over `Settle` (0.12 s, by the time between frames, so it means the same at any rate), and only from texels that lay at the same distance from that camera, so nothing is carried onto what's just come out from behind something. A flickering yes or no becomes how much of the spot sees the probe. `Ambient_Light.frag` fills in the pixels between by depth (`giIrradiance`), so light never runs across the edge of what's in front, and uses what it finds for diffuse light and for the reflections of surfaces too rough for the reflection pass, and the flat sky and ground for whatever has no answer. GI fades in over 0.6 s when it's ready (`Presence`). While it's in, SSAO only reaches 0.35 m (`ContactReach`), as GI has everything further.

## Rules

- **With the sun and sky off and lights and emission out, the frame is black**, GI included: `--render sun=0,sky=0,lights=0,emission=0`. A new source of light goes to GI as it goes to the light passes.
- **The picture stands still when the world does, whoever walks through it.** Anything a probe keeps from frame to frame comes from its place in the lattice and the voxels alone, and is worked out again when those change (`giProbeStamp`): nothing kept may depend on what the screen showed, or on when it was worked out. A person found the first version flickered as they walked, which no still had shown. Judge any change here walking, from `--render gidebug=3`, which shows what GI gives with nothing else to hide it, and shots taken mid-walk (`--bot true --walk x,z --shot`), as a `--film` can't walk: its server outruns frames that save at a few a second.
- **Nothing that moves is in the voxels**, and nothing in them may move: what's kept from frame to frame relies on it. A door that opens or a wall that falls would have to rebuild its region (`BuildRegion`) and forget the probes around it.
- **The sun's direction is fixed in a world.** The cached sun visibility relies on it; a sun that moved would have to be worked out again.
- **Nothing reads back to the CPU** but the debug report. Sizes of passes are worked out on the GPU and dispatched indirectly.
- **One probe that searches holds up the 63 beside it** for as long as all of them searching, and a chain of passes behind barriers takes as long as all of them. Anything that waits on many lookups in turn is spread over threads, or the pass costs ten times what its work does (probe setup did, until it was split: 0.46 ms to 0.04).
- **The layout is shared**: `GI/GI.glsl` and `GI/GI_Probe.glsl` against the constants at the top of `GlobalIlluminationRenderer.cs`, and the `GiData` uniform block (binding 11) against `UploadFrame`. Change them together. GI's own passes use texture units 20 to 29; the ambient pass reads what's gathered on unit 11.
- **A uniform `ShaderProgram` can see is declared at column 0 and outside any `#ifdef`**, which is why the height map is in `GI_Height.glsl` and the material arrays in `GI_Materials.glsl`. Compute shaders can't use `textureUtil.glsl`, as it takes derivatives, which is why `Geology.glsl` is apart from `Rock.glsl`.

## Seeing what it does

`--render gidebug=N`, or Show in the Global illumination part of the F3 Render window:

| | |
|---|---|
| 1 | The voxels' colours |
| 2 | The light leaving the voxels |
| 3 | The light GI gives every surface, on white, with nothing else |
| 4 | Where the probes give an answer (green) and where they don't (red) |
| 5 | As 3, with each pixel taking only the probe that counts most, so each probe shows as a patch |

While one is up, GI writes what it holds to the console every 120 frames: surfaces in each level, probes in each cascade and how many were placed afresh, the sky it sees against the flat sky, and how long each level's list of voxels waiting for a sun and sky is. `giprobe=x:y` (a pixel of the picture from its top left) adds that pixel's sixteen probes: where each stands, why it counted or didn't (buried, behind, blocked by the voxels, or used), which parents it sees, its light, and the twelve directions of each finest one it used. That's how a dark roof was found to be no fault of GI's but the old fill: the stone lanterns' fill had lit their roofs from the flame underneath.

## What it costs

GPU time at 3440x1369 on an RX 7900 XTX, from `--perf`:

| | The Haven, standing | The isle, walking |
|---|---|---|
| Gather | 0.31 ms | 0.32 ms |
| Probe trace | 0.29 ms | 0.33 ms |
| Voxel light | 0.25 ms (64 lamps) | 0.07 ms |
| Probe find | 0.18 ms | 0.17 ms |
| Voxel cache | 0.01 ms | 0.18 ms |
| Probe merge | 0.10 ms | 0.11 ms |
| Probe setup | 0.04 ms | 0.06 ms |
| Whole frame | 8.13 ms, 6.95 off | 7.35 ms, 6.23 off |

The gather and finding probes go with the resolution (half the gather is its visibility test); the rest goes with the world. Walking costs the cache: every 4 m along an axis brings a slab of new surfaces, about 9,000 at the finest level, mostly crowns of trees, and each wants some twenty long rays. It's spread over frames, and on the walk the worst frame was no worse with GI on than off. It holds about 290 MB of video memory, most of it voxels (86 MB) and what the probes see (90 MB), and gives it all back when it's turned off in the settings (turning it on again compiles its shaders, a moment's pause).

Letting what's gathered settle costs 0.05 ms in the gather.

## What nobody has seen yet

A person played it once and found it flickered badly walking past fences and houses: that was probes keeping places worked out among coarser voxels, fixed. Since then it's been judged from shots taken mid-walk and from stills, not by a person moving. The finest window (48 by 32 m) is smaller than what the camera sees, so its edges are on screen and move 4 m at a time: GI's own light shows faint lines there while walking, which the finished picture didn't show in any shot. If a seam is ever seen to travel with the hero, that's where it is, and the fix is a finest window wider than the view. How it feels moving, on a slower GPU, in a storm, and in a room, as nothing can be entered yet, are all still to be seen.
