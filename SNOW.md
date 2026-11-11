# Snow

What walking through snow leaves behind: a print for every foot, a furrow through deep snow, powder kicked up, and glitter on what nobody has trodden. Snow has no grass, so this is what moves on it instead of the grass parting.

Status: built 2026-11-11, judged from stills from hidden runs. **Nobody has watched it move.**

## The one idea

**Snow remembers.** A map of the ground around the camera (`SnowTrailRenderer`, 2048 texels over 96 m, about 5 cm each) holds everything pressed into it, and the terrain's parallax march goes through it as part of the ground's depth (`Terrain.frag`, `Shaders/Snow/SnowTrail.glsl`). A print is a real hollow: its far wall hides what's behind it, the sun shadows it (`ReliefShadow` reads the same depth) and the sky sees less of its floor. It isn't a decal or a picture of a hollow.

```
  SnowTrailSystem: each planted foot, each body ploughing        (CPU, client only)
          |  SnowStamp: where, which way, what shape, how deep
  SnowTrailRenderer: MAX blend into a wrapping RGBA8 map, falling snow subtracted, mips
          |
  Terrain.frag: SetUpSnow -> the march (BlendedDepth -> SnowTrailed) -> TreadSnow -> SnowGlitter
```

Texel channels: **r** how deep a floor goes (of `Deepest`, 0.6 m), **g** how much of the snow's own surface that floor replaces (1 in a print, 0.6 in a furrow), **b** snow heaped round it (of `Highest`, 0.25 m), **a** clumps of what was kicked up.

## The map

- It wraps round: a texel is a place in the world for as long as it's in the region, and moving the region only empties the strips it moves onto (scissored clears), so nothing is ever copied.
- Prints go in with a MAX blend, as snow pressed down stays down however many feet come after, so a foot can press again every frame it stands at no harm, and turning on the spot follows it.
- Falling snow fills it all in again by a subtracting blend, in whole steps of eight bits: the heaviest snowfall fills a print in `FillTime` (80 s), and kicked-up clumps settle in `SettleTime` (40 s) snowing or not.
- Its last mip, at 0.75 m a texel, lets a pixel say at two looks, under it and at the far end of its ray, that nothing is near (`snowTrailCovers`), which is most of a snowfield. A coarser one lost lone clumps to eight bits' rounding and cut them out in squares.
- Everything a stamp makes fades to nothing well inside the square it's drawn on, and a furrow throws nothing beside it, as it's laid again every frame: a dusting left at the square's edge drew straight lines in the snow, and a body walking on the slant stacked them into bands. A world that has never had anything pressed into it reads nothing at all (`Region.w`).
- Prints fade out before the region's edge, where the texture comes round again, and anything that leaves the region is gone.

## What presses

`SnowTrailSystem` reads everything with a `PlantContactComponent`, as the grass does:

- **Feet**, each its own shape where it really stands, by what the rig is (`PrintOf`): a **boot** (heel and ball, the arch shallower, a running foot digging its toes in), **three toes** for a theropod (a pad, three toes with the claws deepest, and a dew claw behind), a **pad** with nubs of toes for a sauropod or anything else saurian on four, and a **paw** for a beast. Sizes come from the rig's feet times its scale, so a tyrant's print is a tyrant's. A foot presses in over `PressIn` (0.12 s) from landing.
- **The body**, in deep snow only (`DeepSnow`, which only the Snowfield's own ground is): a furrow one frame's travel long, so there are no gaps at any speed, U-shaped, banked either side and churned into chunks. The giants' bodies are carried high (`feetOnly`) and plough nothing.
- Snow underfoot is the Snowfield's ground, some on its paths and edges, or whatever the weather has settled (`SnowAt`). In a dusting the shader takes prints at a third of the depth (`gSnowDeep`).

Every stamp says which way it was going (`Push`, up to 1 at a run), which heaps its rim ahead and throws its clumps that way.

## How it looks

All in `Terrain.frag`, after the weather, as settled snow is what's trodden:

- A print's floor is level, measured down from where the snow lies on average (`gSnowLevel`), never shallower than the snow already was. It's pressed smooth and glazed, and **blue the deeper it goes**, as packed snow lets light further in before it comes back, and its floor sees less of the sky.
- A furrow's floor is churned into chunks (`snowChunks`), blue-grey and rough.
- What's heaped can only lessen the depth, so **nothing ever stands out of the mesh** (TEXTURES.md's rule): banks rise as far as the snow's own relief lets them, broken into lumps, bright and rough.
- Slopes come from the trail itself, read at full depth whatever the quality, so on Low, which marches no relief, prints still show by their shading.
- **Glitter** (`SnowGlitter`): one cell in seven of 6 cm on untrodden snow holds a tiny, very smooth facet tipped at random. The light pass only gives one a highlight while it's nearly a mirror between the sun and the eye, so they twinkle as the camera moves, go out under cloud by themselves, and trodden snow has none. Geometric specular anti-aliasing (`weatherFilterRoughness`) dims them where they'd be smaller than a pixel.

## Thrown up

A foot coming down in snow (`Gait.Landed`) plays `Powder.Kick` at a walk, `Powder.Spray` at a run and `Powder.Stomp` under a foot over 1.4 m: a thin lit puff that sinks back, clumps thrown the way it was going that vanish where they land, and a few crystals. All `Ambient`, low and gone in a second, so a crowd in a snowfield can't hide the ground (read Clarity in `WEATHER.md`).

Settled snow is also what a foot is heard landing on (`CharacterSoundSystem.RainedOn`, past `SnowedOver`), out of doors.

## What it costs

At 3440x1369 on a Radeon RX 7900 XTX, walking through a snowfield with everything in view snow: terrain geometry 0.66 ms without the trail, 0.75 ms with it, and the trail's own pass 0.04 ms (stamps, fill and mips), so about 0.13 ms. A land with no snow pays nothing: the pass is skipped and the terrain reads nothing.

## Rules

- Nothing in the rules reads it: it's this machine's alone, like the grass a body parts, and it's always safe to clear.
- `SNOW_TRAIL_DEEPEST` and `SNOW_TRAIL_HIGHEST` in `SnowTrail.glsl` are `SnowTrailRenderer.Deepest` and `Highest`, and `SNOW_TRAIL_NEAR_LOD` is `Levels`: change them together.
- A pixel only marches as deep as a print it might meet (`gTrailReach`, from four looks along its ray), so the cost stays where the prints are.
- Judge it from the game's camera, by day and moving.

## Tools

`"Dino Defenders.exe" --seed 2 --archetype highlands --biome snow --weather clear --calm true --bot true --walk 428,742` runs the hero through a snowfield, and `--seed 303 --archetype isle --weather snow` puts prints in snow the weather settled. Leave out `--calm` and the raptors that follow leave theropod tracks.

## Where it stands

Seen in stills only: prints, furrows, banks, raptor tracks and glitter all show from the game's camera. Nobody has watched the powder fly, the glitter twinkle as the camera moves, or prints fill in under falling snow, and the sizes and depths were tuned from those stills.
