# Terrain and rock

How the land stands up: mountains, cliffs, the stones on them and the rock they're all made of. For the ground under people's feet, see the world pipeline in `AGENTS.md`.

Status: built, and looked at by machine and in stills. See "Where it stands" at the end for what nobody has judged yet.

## The one idea

**Rock is fractures, and one description of them makes everything.** Noise can't make a mountain and it can't make a stone: what makes rock look like rock is that it breaks along a few planes, over and over, into layers and blocks. So a world has one `Geology`, and the height map, the terrain shader and every stone all read it. That's why a boulder looks as if it came off the cliff behind it.

```
                                Geology                    a kind of rock, its beds, its joints
                               /   |    \
                              /    |     \
   MountainStage (coarse)    /     |      \     Rock.glsl (any shader)
   water finds its way out  /      |       \    the same layers, in world space
              |            /       |        \
     SiteStage, RoadStage /   RockForge      \  a stone is the same stone as the cliff
     find a wall there   /    a form + a seed \
              |         /          |
   RockSculpt (a metre at a time)  |
     wall, gullies, beds, joints   |
     only where nobody goes        |
              |                    |
          RockStage  --------------'    crags on the ridges, rockfall at the foot
```

## Where the ideas came from

- **Stream power**, for the shape of a range. Braun and Willett's O(n) solver, and Cordonnier et al., "Large Scale Terrain Generation from Tectonic Uplift and Fluvial Erosion". `MountainStage` doesn't simulate: it finds what the law settles into, by turns: route the water, then rebuild every height up the channels from where it leaves.
- **Implicit blocks**, for everything about rock. Paris et al., "Modeling Rocky Scenery using Implicit Blocks": blocks are the smooth intersection of half-spaces, in families set by how the fractures are arranged (tabular, columnar, rhombohedral, equidimensional), with detail warped on along the gradient. `RockKind` is those families.
- **Rock piles and overhangs**, Peytavie et al., "Arches".
- **Erosion filter**, for gullies. Rune Skovbo Johansen's, which runs a point at a time, so a world bakes in parallel. It's in `World/Generation/ErosionFilter.cs`, and **that one file is under the MPL 2.0**, as what it ports is. It has to stay a file of its own and keep its notice.
- **Quadric error metrics**, Garland and Heckbert, and **surface nets**, Gibson, for turning a field into a mesh worth drawing.

## The rules

**Nothing happens on ground anyone uses.** It's the whole point of the land being smooth. `RockSculpt` measures everything out from `Reachable`, and returns a point untouched if it isn't in rock. Sites, roads and packs all come after `MountainStage`, so they find a wall where a mountain is and go round it with nothing to do. `Rock` over half stops a character (`FinalizeStage.BuildWalkable`), so a ramp of noise can't lead anyone up a mountain. `check` fails on rock in the land.

**Nothing may hide the hero.** The camera only ever looks along +z, at 2.32 rise to run. So a face that falls towards +z steeper than that, with someone beyond it, could stand between them. `RockSculpt.KeepHeroInSight` caps those at `SightSlope` in one pass down each column, and any ground short of bare rock sets the limit, not just `Reachable`: `Walkable` goes further than the land sites were placed on. Nothing is dithered away, as that would be a hole in the ground. Faces the camera can see are left alone. Stones are models, so `FocusCutout` thins them like anything else. `check` fails on any walkable spot that can't be seen.

**Mountains are in the coarse map, and rock is in the fine one.** What sites have to know about goes in `Elevation`, before `SiteStage`. What only shows goes in `Height`, after. The 4 m grid can't hold a cliff, and water leaving by eight directions on it leaves a comb a cell wide, which `Settle` takes out.

**At this camera a metre is thirty pixels.** So everything finer than the height map is the material's job, and the shape has to read first: a busy normal map makes a surface look flat from up here. Keep `rockTap`'s normals under the rock's own form. What gives rock its shape at the size in between is `Proud`, which is how far each point stands out of what's around it, baked from the finished ground. It goes to the shader in the road channel of the chunk data, as no road crosses rock.

**That channel is signed, and level is 0.** Anything else draws a sawtooth along the rock's edge, as texels filter into their neighbours. It's why `grassFertility` clamps the road it reads.

**A face is a plane.** `RockSculpt.Break` is what stops it looking melted: slopes are cut into cells along the strike, each becomes the plane through it, pulled towards the nearest of `Geology.Joints`. Planes run away from a curving slope, so each is clamped to stay near the surface. Without that the mountains turn into needles.

**Anything open in a stone is wider than a cell's diagonal.** That's `Shape.Narrowest`. Narrower, and the two sides of a crack share the grid's vertices and fold together, and that surface can't be simplified: the decimator stops thousands of triangles over budget. So cracks have a floor rather than tapering to nothing, and joints have a smallest gap and a smallest rounding. `rockcheck` fails on anything over budget, which is how this shows up.

**Biome shares are of the land people walk on.** Otherwise a biome that likes the heights is spent on bare mountain.

**Rock isn't only mountains.** Anywhere too steep to hold anything is rock too, as a sea cliff is, but it falls away from the land, so it gets gullies, beds and joints and no wall. Nothing above the shore's first metres is touched, so the coast stays put.

**A mark stops at the foot of a face.** `Mark_Composite.frag` finds the ground by going back and forth along each pixel's ray, which only settles on what's gentler than the view. On a cliff it tears, and nobody can stand there anyway, so it fades out.

**The air doesn't feel a cliff.** `WeatherField` reads a blurred copy of the land, and `LiftHeight` goes by the height of real mountains. Change the size of mountains and run `weather`: a ridge that wrings the air dry shows up there as a sky that jumps.

**A long shadow fills with light.** A mountain lays its shadow over everything in a low sun, and a hero can't be lost in it, so `SunShadow.glsl` lifts a shadow by how far off whatever casts it is. That's four more taps, moved up the ray, on what's already in shadow, each a faint step of its own: a single one draws a line inside the shadow. Close up it's as dark as ever. Terrain past the streamed chunks still casts: `TerrainRenderer.SetFarRock` keeps a coarse whole-world height map for it, each height the lowest for some way round, so that it always lies under the real ground and can't shade it.

## How to

**Add a kind of rock.** A line in `RockKind`, its case in `Geology` and in `RockForge.Joint`, and a `Bedrock` on the biomes it belongs to. `WorldArchetype.Bedrock` is for a kind of world that's known for one. Colours are albedos under a sun of about 13, and a face in shade has nothing but its albedo to be seen by, so nothing very dark: basalt at 0.09 was a black hole.

**Add a form of stone.** A line in `RockForm`, in `RockForge.ShapeOf` and `Design`, and in `NatureCatalog.BuildRocks`. Stones go through `NatureCatalog.Resolve`: a biome says it has boulders, and what they're made of is the world's. Ids have one dot, as `rock.granite-crag#1f3a`: three parts is a building to the validator.

**Change how rock looks.** `Rock.glsl` for all of it. `Geology.LayerAt` and `geologyLayerAt` are the same function and change together. Layers go up as bytes in the `GeologyData` block, 8 KB, so any shader that includes `Rock.glsl` has them, with no sampler to bind: terrain has used all sixteen a stage is promised. The material is `TextureGenerator.cliff`, grey, as all its colour is the world's.

## Checks

After any change here:

```
dotnet "Dino WorldLab/bin/Release/net8.0/Dino WorldLab.dll" check --count 110
dotnet "Dino WorldLab/bin/Release/net8.0/Dino WorldLab.dll" check --people 150 --packs 100
dotnet "Dino FloraLab/bin/Release/net8.0/Dino FloraLab.dll" rockcheck
```

`check` holds the rules above, and fails about one world in a hundred on road grade, as it always has. `weather` passes as it stands, but over a dozen worlds it fails on distance with or without any of this: that limit was already close. For looking: `relief` says what a world's land is made of and where to go and see it, `view --at x,z` draws a place with no game, in a second, `--plain true` for its shape alone. `rocks` is a sheet of every stone. The camera looks along +z, so a mountain only faces it from the south: the spots `relief` lists are those. In the game, `--at x,z --weather clear`.

## Where it stands

Judged from stills, by a machine, in a handful of places and mostly in clear weather. Nobody has walked along a wall, watched a shadow come in, or played a fight against one. Everything is tuned to the game's camera and only that. Saved lands move: sites go where the mountains aren't, and a hero saved inside what's now rock wakes at the landing.
