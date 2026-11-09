# Dino Engine

**Language:** C# | **Rendering:** OpenGL (via OpenTK) | **Status:** In active development  

## Showcase


<img width="1907" height="1079" alt="engine grass" src="https://github.com/user-attachments/assets/4e0510b4-1bb6-4804-9bf7-9107c65fa2a1" />

---

<img width="1907" height="1074" alt="engine 2" src="https://github.com/user-attachments/assets/4ccadb53-51c7-4a29-9544-47a7f78602a3" />

This is a fully **procedural game engine** built from scratch in C# using OpenGL and OpenTK. The engine does **not use any external libraries** beyond OpenTK (the game adds LiteNetLib and Mono.Nat for playing over the internet, behind an interface the engine defines). Every asset, from textures and models to terrain, effects, sound and music, is generated **procedurally at runtime**.

The code is under the MIT licence, but for [`ErosionFilter.cs`](Dino%20Engine/World/Generation/ErosionFilter.cs), a port of Rune Skovbo Johansen's erosion filter, which stays under the Mozilla Public License 2.0.

---

## Dino Defenders

The game built on the engine: an action RPG about a land invaded by dinosaurs. The Saurian Reich, an empire from over the sea, grows them from blood found in amber, raises their bones out of the ground, and sends them ahead of its soldiers in harness, and every land you'll see is one it has taken. You're a Defender, and you take it back a place at a time: burn out the pens, put what the Ossuary raised back in the ground, rekindle the shrines, and bring down the tyrant that holds the land. Think Jurassic Park meets Ghost of Tsushima. See [LORE.md](LORE.md).

No two worlds are alike and none are made by hand. Each seed grows its own land, biomes, weather, roads, settlements and building style, and writes its own chronicle: which commander holds the land, what their tyrant is called, and what it will take to free it. Every dinosaur is grown from a species and a seed as well, raptors, spitters, ravagers, hornbacks and tyrants, living, in the Reich's harness, rotting or bare bones, so no two of them are alike either. See [DINOSAURS.md](DINOSAURS.md).

Between lands you rest in the Haven, the one place the Reich has never found: an isle of blossom with a merchant, a stash every hero on the machine shares, training dummies to try a build on, the great waygate, and three gentle giants, the only great beasts the Reich never found, browsing the tops of its trees. Its map table lists every level you've freed and the one after, three lands to each, every one with its own story, biomes, weather and tyrant, and you can pay gold for three others. Every tenth level is a tyrant's arena, fought before the people of the land while its handlers open the gates on you. The orders you take from the Reich are what make a land worse and richer: each brings rewards and dangers, and every arena you free lets the table take one more at once. There's one life to a land, and losing one costs only the try. The hero and what they carry go with you, and progress is saved automatically. See [LANDS.md](LANDS.md).

There are no classes. There is one great talent tree, of over 850 nodes, and what a hero is depends on where they've walked on it. Stats stack in the way anyone who has played a big ARPG will know: flat, then all increases together, then each "more" on its own. What's less usual is that the land itself is part of a build, that elements react when they meet, and that enemies are built from exactly the same parts as you: bosses fight with what they wear, and drop it. See [BUILDS.md](BUILDS.md).

| | |
|---|---|
| WASD | Move |
| Mouse | Aim |
| Left button, right button, Q, 1 to 4 | Whatever's on your skill bar |
| Space | Dodge |
| R | Drink a flask |
| E | Use, talk, trade |
| F | Guiding wind, once you've set a land free: the wind turns toward your next goal |
| M | Map |
| C or I | Hero and inventory |
| T | The talent tree |
| K | Your skills, and what's on the bar |
| J | Chronicle |
| Mouse wheel | Zoom |
| Esc | Menu |

Start it with `dotnet run --project "Dino Defenders" -c Release`.

To play without building it, [download the launcher](https://github.com/Rymdhest/Dino-Engine/releases/latest/download/DinoDefendersLauncher.exe) (Windows 10 or 11). It installs the game and keeps it up to date. Windows will warn about it once, as it isn't code signed: choose **More info**, then **Run anyway**. See [LAUNCHER.md](LAUNCHER.md), which also covers sending out a new build with `release.cmd`.

Up to five heroes can share a world, with no server to run: in the pause menu, **Travel with friends** opens your world and gives you an invite to send, and takes an invite you've been sent. See [NETWORKING.md](NETWORKING.md).

`Dino WorldLab` generates and checks worlds with no window, for example `check --seed 1 --count 100`, or `map` to draw one. `Dino NetLab` tests the networking the same way: `core` for replication alone, `game` for the whole game on every kind of bad line, and `build` for every skill and keystone on a running server. `Dino BuildLab` does it for the build system: `check` for the sums and the content, `bench` for what it costs, `balance` for how a fight goes at each level. `Dino LaunchLab` does it for the launcher and release tool: `core`, `release` and `live`. `Dino SoundLab` does it for sound and music: `check` for everything that can be known without ears, `render` to listen to it, `sheet` to see it, `musiccheck` and `music` for the score. See [SOUND.md](SOUND.md). `Dino LifeLab` does it for what's alive: `check` walks someone through real worlds, and `sheet` draws any creature doing anything. See [WILDLIFE.md](WILDLIFE.md). `Dino AnimLab` does it for bodies: `check` throws everything at them, `sheet` draws anyone doing anything, and `zoo` stands dinosaurs side by side, living, harnessed, rotting or bones. See [ANIMATION.md](ANIMATION.md) and [DINOSAURS.md](DINOSAURS.md).

---

## Key Features

### Core Systems
- **Entity-Component-System (ECS)** architecture for flexible and efficient scene management

### Rendering
- **Deferred shading pipeline**
- **Physically Based Rendering (PBR)**
- **Normal mapping**
- **parallax mapping**
- **Screen Space Ambient Occlusion**
- **Screen Space Reflections**
- **animated, height-based fog**

### Lighting
- **Directional lights** with **cascaded shadow mapping**
- **Point and spot lights with shadow mapping**
- **Light rays** for volumetric effects
- **Geometric subsurface scattering**
- **Emissive materials**

### Procedural Content
- **Procedural terrain** and landscape generation
- **Procedural modeling** for all objects
- **Procedural dinosaurs**: each grown from a species and a seed as a field of signed distances, skinned by surface nets and quadric decimation, rigged from the same body, and moved with nothing recorded
- **Procedural textures** for all textures
- **GPU-driven grass** in the style of Ghost of Tsushima: a compute pass grows, clumps, thins and culls the blades every frame, drawn as curved Bézier blades with indirect draws; nothing per blade is stored
- **GPU particles**: spawned, simulated in the wind, lit once each in six directions and sorted in compute, with nothing read back. Flames are grown at load by a fluid simulation and coloured as a black body, smoke is six-way lit puffs, and what lies on the ground is painted as a decal. One recipe sets a candle, a body or a whole field on fire

### Anti-aliasing
- **Deferred MSAA** (2x/4x/8x): multisampled G-buffer, lights and composites shaded per sample only where a pixel's samples are on different things and once from their average elsewhere, sky/water/particles multisampled in hardware, tone-mapped-weighted resolve; no temporal accumulation, so no ghosting
- **Coverage anti-aliasing for sub-pixel grass**: blades thinner than a pixel draw a pixel wide over a matching share of the samples, so swaying grass doesn't shimmer
- **Analytic alpha-to-coverage** for cutout foliage: the cutout edge is placed between samples like a polygon edge
- **Geometric specular anti-aliasing** on grass
- Optional **FXAA 3.11** over the tone-mapped image

### Other Post-processing Effects
- **Depth of Field**
-  **bloom** (up/down-sampling)
- **HDR tone-mapping**
---

