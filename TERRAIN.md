# Terrain and rock

How the land stands up: mountains, cliffs, the stones on them and the rock they're all made of, and the rock that comes up through the land people walk on. For the ground under people's feet, see the world pipeline in `AGENTS.md`.

Status: built, and looked at by machine and in stills. See "Where it stands" at the end for what nobody has judged yet.

## The one idea

**Rock is fractures, and one description of them makes everything.** Noise can't make a mountain and it can't make a stone: what makes rock look like rock is that it breaks along a few planes, over and over, into beds and blocks. So a world has one `Geology`, and the height map, the terrain shader and every stone all read it. That's why a boulder looks as if it came off the cliff behind it.

**And a mountain's shape is its biome's.** The rock says how a mountain breaks; its form (`MountainForm`) says what it breaks into, so one rock stands in towers in one biome and in ledges in the next, as limestone does in the karst of the south and the escarpments of the north.

```
                                 Geology                       a kind of rock, its beds, their colours
                                /   |    \
   MountainStage (coarse)      /    |     \      RockSkin (in tiles, streamed)        Rock.glsl (any shader)
   water finds its way out    /     |      \     every face of bare rock clad in      what's inside a block: its
   -> Drainage, the channels /      |       \    its own joint blocks, as a mesh  ->  bed's colour, laminae,
              |             /    RockForge   \                                         fine fractures, weather
   SiteStage, RoadStage    /   a form + a seed \
   find a wall there      /         |
              |          /          |           MountainForm, per biome  (towers, ledges, domes, canyons, ...)
   RockSculpt (a metre at a time)   |                   |
     wall, the form, ravines,       |                   |
     gullies, beds, joints  <-------+-------------------'
     only where nobody goes         |
              |                     |
          RockStage  ---------------'    formations by form: bluffs, spires, tors, hoodoos, columns, fins, arches
          GroundRock                     the land's own rock: pavements, knolls, outcrops, rockfall at a wall's foot
```

## Where the ideas came from

- **Stream power**, for the shape of a range. Braun and Willett's O(n) solver, and Cordonnier et al., "Large Scale Terrain Generation from Tectonic Uplift and Fluvial Erosion". `MountainStage` doesn't simulate: it finds what the law settles into, by turns: route the water, then rebuild every height up the channels from where it leaves. The water's routes are kept (`Drainage`), as Génevaux et al.'s "Terrain Generation Using Procedural Models Based on Hydrology" builds land from its rivers, and ravines are cut along them.
- **Implicit blocks**, for everything about rock. Paris et al., "Modeling Rocky Scenery using Implicit Blocks": blocks are the smooth intersection of half-spaces, in families set by how the fractures are arranged (tabular, columnar, rhombohedral, equidimensional), with detail warped on along the gradient. `RockKind` is those families. Bare rock in the land is clad in them too (`RockSkin`), as one field measured from the ground.
- **Terrain amplification**, Paris et al., "Terrain Amplification with Implicit 3D Features", for what a height map can't hold: hoodoos under their caps, arches, stratified cliffs. Those are stones (`RockForge`), stood on the height map.
- **Rock piles and overhangs**, Peytavie et al., "Arches".
- **Sculpting Mountains**, Cordonnier et al., for strata that show on every eroded face: beds of hard and soft rock, the hard standing as cliffs and the soft wearing back to benches.
- **Joint sets as geologists describe them**: a systematic set running unbroken through a bed, cross joints ending against it (T junctions), spacing set by how thick the bed is and never even, and the beds of one cliff each jointed on their own. It's what keeps a face of blocks from reading as masonry.
- **Erosion filter**, for gullies. Rune Skovbo Johansen's, which runs a point at a time, so a world bakes in parallel. It's in `World/Generation/ErosionFilter.cs`, and **that one file is under the MPL 2.0**, as what it ports is. It has to stay a file of its own and keep its notice.
- **Quadric error metrics**, Garland and Heckbert, and **surface nets**, Gibson, for turning a field into a mesh worth drawing.
- **Crimson Desert's cliffs**, as Pearl Abyss describe them: a height map for the land, and dedicated meshes for the cliffs over it, presets of them making whole bluffs, placed by slope and height. Our bluffs, spires and the rest are those, grown rather than authored.

## The rules

**Nothing happens on ground anyone uses, but the land's own rock.** It's the whole point of the land being smooth. `RockSculpt` measures everything out from `Reachable`, and returns a point untouched if it isn't in rock. Sites, roads and packs all come after `MountainStage`, so they find a wall where a mountain is and go round it with nothing to do. `Rock` over half stops a character (`FinalizeStage.BuildWalkable`), so a ramp of noise can't lead anyone up a mountain. `check` fails on rock in the land. The one exception is `GroundRock`: bedrock showing through and low knolls, which never come within 7 m of anything built, laid out or wet, never make ground steeper than a third, never touch the `Rock` map, and are walked on like any other ground.

**Nothing may hide the hero.** The camera only ever looks along +z, at 2.32 rise to run. So a face that falls towards +z steeper than that, with someone beyond it, could stand between them. `RockSculpt.KeepHeroInSight` caps those at `SightSlope` in one pass down each column, and any ground short of bare rock sets the limit, not just `Reachable`: `Walkable` goes further than the land sites were placed on. Nothing is dithered away, as that would be a hole in the ground. Faces the camera can see are left alone. Stones don't open for the focus cutout (only trees do), so a formation on the rock is held to the same line by `RockStage.HidesNoOne`, from its top (`RockForge.TopOf`) to the middle of anyone on any node of the walkable grid north of it, and `check` fails any formation that breaks it. What lies on the land, a boulder or an outcrop, is walked round like anything else if it stands over a knee and stepped over if it doesn't (`Hitboxes.StepOver`), and kept low.

**Mountains are in the coarse map, and rock is in the fine one.** What sites have to know about goes in `Elevation`, before `SiteStage`. What only shows goes in `Height`, after. The 4 m grid can't hold a cliff, and water leaving by eight directions on it leaves a comb a cell wide, which `Settle` takes out.

**A form only reshapes rock.** Towers, caps, crests, domes, blades and ravines are worked in `RockSculpt.Apply`, scaled by how bare and how far inside the rock a point is, so they come in from the foot of a wall and never reach the land. A mountain takes the form of the biomes on it, blended across their borders (`MountainForms.At`), and a kind of world known for one has it outright (`WorldArchetype.Mountains`: the Badlands are canyons, the Ashen Isle columns). Columns are basalt's alone and blades need beds stood on end (`MountainForms.Resolve`).

**A ravine always falls to its mouth.** `Drainage` keeps every cell that gathers enough water, joined to the one it drains into, and the lowest a floor may be cut there: where the water leaves the rock, and a little for every metre of the way (`LeastFall`). Only the big channels cut deep (`Drainage.Size`): a mountain cut as deep by every runnel is all spikes.

**At this camera a metre is thirty pixels.** So the shape has to read first: a busy normal map makes a surface look flat from up here, and lines drawn on a smooth face read as a smooth face with lines on it, which is what the first two versions of this were called. Anything between a metre and the height map's grid is geometry (`RockSkin`), and only what's finer than a block is the shader's. The cliff material is a quiet grain, never blocks of its own. `Proud`, how far each point stands out of what's around it, baked from the finished ground, still shades the ground's own rock where it shows: it goes to the shader in the road channel of the chunk data, as no road crosses rock.

**That channel is signed, and level is 0.** Anything else draws a sawtooth along the rock's edge, as texels filter into their neighbours. It's why `grassFertility` clamps the road it reads.

**Bare rock is clad in its own blocks, and they're a mesh.** `RockSkin` is a signed distance like a stone's, measured from the drawn ground: the world's beds (`Geology.BedAt`) are split into courses (`Breaks`, by the kind of rock), each course by two sets of joints into blocks, unevenly spaced and staggered, some of them shut so two blocks are one, and basalt into hexagonal columns. Every block stands out of the ground by its own amount: a hard course further than a soft one, so a ledge overhangs the band worn back under it, a course and a block each their own share, and whole runs of blocks together over several metres (`Mass`), which makes a buttress of one stretch of face and a bay of the next. Each face is tipped its own way, never far enough for a corner to go back into the ground, and each edge is broken off by its own amount. How a form wears its blocks is a line in `RockSkin.StyleOf`, blended across biomes as the forms are. The joints are open down to a core just inside the ground, so what's seen in a crack is the ground itself, in shadow.

**How far from the ground goes sideways too.** A face of rock is nearly upright, and the height under a point in front of one is its foot, so the ground's distance is worked out a slice at a time: how far down to the ground and how far across to where it crosses that height, exactly, by the lower envelope of parabolas, combined as the distance to a plane through both. It's capped well past anything a block can stand out (`FarthestGround`), as a cap any lower filled everything over a tower's top with rock.

**Tiles meet without a crack.** A world is cut into tiles of 16 m, each meshed by surface nets at a quarter of a metre and simplified (`Decimator`, held to an error and then to a budget), on threads of their own. Two tiles share a layer of cells, and work out every point in it from the same numbers, whatever their own windows are: their heights reach as high and as low over what they share, their forms are read on the world's grid and never the tile's, and the ground's distance is exact rather than propagated. The points on the edges where they meet (a face on one side only, or on more than two, which is a saddle) are pinned and keep every edge they have. `skin` checks that every open edge one tile has along another, out of the ground, the other has too.

**The skin is never where anyone is.** It fades out over the last 2 m before anywhere anyone stands, by the distance up and across from it, as a cliff's face is a step from the path at its foot all the way up and nobody's feet are near its top; it keeps 1 to 3 m off anything built, laid out or a road (`ClearOfBuilt`); and it's cut off flat under the line from the camera to the middle of anyone standing north of it, the same line `RockStage.HidesNoOne` holds a formation under. Rock is whatever's drawn as rock: the `Rock` map, and anything as steep as `TerrainSurface` paints rock. `skin` fails on any of it standing a body's height over walkable ground.

**It's streamed and grown, never stored.** `RockSkinStream` grows tiles nearest the camera first, 40 m further out than they're shown, on a quarter of the cores at below the frame's priority, keeps what's grown for as long as the world is and only what's near on the GPU, and a land's loading grows those round the landing (`RockSkinStream.Prepare`), so a land is clad when it's first seen. A tile comes to megabytes of grid and lists, so none of it is made for the collector: the decimator's arrays are rented and its faces round each point are runs of one array, and a tile's grids are rented too, as a hundred thousand short lists a tile once paused the game forty milliseconds at a time. `--skin false` turns it off, for measuring it.

**A block has its joints in its shape.** So the skin and every stone take only the finer things from `Rock.glsl`: their beds' colours and laminae, the fractures inside a block, and weathering. The ground under the skin is drawn the same way, as the floor of a crack.

**The beds come round again.** Beds that dip steeply, as schist's do, reach hundreds of metres past the ends of the table of layers across a land, and every point out there was once the bottom bed: no colour, and no blocks at all in the skin. `Geology.PlaceOf` and `geologyPlace` wrap a layer round the table, and change together.

**A face is a plane.** `RockSculpt.Break` is what stops it looking melted: slopes are cut into cells along the strike, each becomes the plane through it, pulled towards the nearest of `Geology.Joints`. Planes run away from a curving slope, so each is clamped to stay near the surface. Without that the mountains turn into needles.

**Anything open in a stone is wider than a cell's diagonal.** That's `Shape.Narrowest`. Narrower, and the two sides of a crack share the grid's vertices and fold together, and that surface can't be simplified: the decimator stops thousands of triangles over budget. So cracks have a floor rather than tapering to nothing, joints have a smallest gap and a smallest rounding, and a lattice's thinnest bed, at four tenths of its spacing, is still wider than that. `rockcheck` fails on anything over budget, which is how this shows up.

**Joints in a stone are never evenly spaced.** `JointLattice` puts each joint a third of a spacing either side of where it would evenly be, as the shader does: evenly spaced beds and blocks read as a wall someone built.

**Biome shares are of the land people walk on.** Otherwise a biome that likes the heights is spent on bare mountain. Which biome is on a mountain still decides its form, so a biome that likes the heights decides most mountains: give it a form a whole world could stand.

**Rock isn't only mountains.** Anywhere too steep to hold anything is rock too, as a sea cliff is, but it falls away from the land, so it gets gullies, beds and joints and no wall, and no form. Nothing above the shore's first metres is touched, so the coast stays put.

**A mark stops at the foot of a face.** `Mark_Composite.frag` finds the ground by going back and forth along each pixel's ray, which only settles on what's gentler than the view. On a cliff it tears, and nobody can stand there anyway, so it fades out.

**The air doesn't feel a cliff.** `WeatherField` reads a blurred copy of the land, and `LiftHeight` goes by the height of real mountains. Change the size of mountains and run `weather`: a ridge that wrings the air dry shows up there as a sky that jumps.

**A long shadow fills with light.** A mountain lays its shadow over everything in a low sun, and a hero can't be lost in it, so `SunShadow.glsl` lifts a shadow by how far off whatever casts it is. That's four more taps, moved up the ray, on what's already in shadow, each a faint step of its own: a single one draws a line inside the shadow. Close up it's as dark as ever. Terrain past the streamed chunks still casts: `TerrainRenderer.SetFarRock` keeps a coarse whole-world height map for it, each height the lowest for some way round, so that it always lies under the real ground and can't shade it.

## The forms

Each is a line in `MountainForms.Of` (how strongly it beds, gullies, facets, steps its blocks, cuts ravines, draws up crests and caps its tops) and, for the ones with a shape of their own, a pass in `RockSculpt.Apply`. What stands on it is `RockStage.Choose`.

| Form | Biomes | Shape | Formations |
|---|---|---|---|
| Ledged | meadow, steppe, moor, savanna, downs, wildwood, ... | strong benches, tops capped at a bed, gorges | bluffs on the cliff bands, crags on crests, outcrops on ledges |
| Jagged | snowfield, larch, cedars, high pastures | crests drawn up into teeth, deep couloirs, hard facets | spires and fins on crests, crags on faces |
| Towers | jungle, bamboo, blossom, mistwood, tidewood | towers out of the mountain's foot, aprons of scree, clefts | fluted spires |
| Domes | pine, birch, elder pines, bog, windward coast | swollen convex slopes, sheets peeling off in rings, shallow runnels | tors |
| Canyons | badlands, red scrub, crimson wood, dunes, redwoods | a plateau cut flat at a bed, slot canyons with sheer stepped walls | hoodoos in companies, bluffs on walls, an arch now and then |
| Columns | ashlands, fernland, and basalt laid level | flows in steps, strong benches | stands of columns, bluffs |
| Blades | the Blackwood, and schist laid level | hogback ribs across the dip, fins along the strike | fins in rows |

## The land's own rock

`GroundRock` is one description of it, built when the land is baked: how rocky a point is (the biomes' `BoulderDensity`, none where water gathers, none within `Room` of anything built), where bedrock shows through (`Pavement`), and the low knolls it stands up in (`Relief`, never on ground that's already a slope). The height map takes the knolls, `TerrainSurface` paints the pavement as rock (drawn as blocks from above, its joints as grikes with earth and moss in them), `WorldData.SurfaceAt` reports it for footsteps, and `WorldGroundCover` strews small stones on it. `VegetationStage.PlaceOutcrops` brings the land's rock up through it in companies by form (slabs in rows along the strike, a stone forest of little spires, small hoodoos, column stumps, slabs stood on edge), solid where they stand over `SolidFrom`, and `PlaceRunout` rolls boulders out from the foot of every wall (`WorldData.RockNear`), the big ones furthest.

## How to

**Add a kind of rock.** A line in `RockKind`, its case in `Geology` and in `RockForge.Joint`, a `JointStyle`, and a `Bedrock` on the biomes it belongs to. `WorldArchetype.Bedrock` is for a kind of world that's known for one. Colours are albedos under a sun of about 13, and a face in shade has nothing but its albedo to be seen by, so nothing very dark: basalt at 0.09 was a black hole.

**Add a mountain form.** A line in `MountainForm`, `MountainForms.Of` and `Resolve`, its pass in `RockSculpt.Apply` if it has a shape of its own, its formations in `RockStage.Choose` and its land's pieces in `VegetationStage.PieceOf`, and a `Mountains` on the biomes it belongs to. New forms go on the end. Look at it with `relief`, which lists each form's share of a world's rock and somewhere to look at it.

**Add a form of stone.** A line in `RockForm` (on the end), in `RockForge.ShapeOf`, `Design` and `TopOf`, and in `NatureCatalog.BuildRocks`. Stones go through `NatureCatalog.Resolve`: a biome says it has boulders, and what they're made of is the world's. Ids have one dot, as `rock.granite-crag#1f3a`: three parts is a building to the validator. Anything made of many pieces that don't touch has to be kept from `SweepUp`, as the columns are.

**Change how rock looks.** `RockSkin` for its shape, `StyleOf` per form and `BreaksOf` per kind of rock, and `Rock.glsl` for everything finer than a block. `Geology.LayerAt` and `geologyLayerAt`, `Geology.BedAt` and `geologyBedAt`, and `Geology.PlaceOf` and `geologyPlace` are the same functions and change together. The layers and beds go up in the `GeologyData` block, about 12 KB, so any shader that includes `Rock.glsl` has them, with no sampler to bind: terrain has used all sixteen a stage is promised. `TerrainRenderer.SetGeology` writes it. The material is `TextureGenerator.cliff`, grey, as all its colour is the world's.

## Checks

After any change here:

```
dotnet "Dino WorldLab/bin/Release/net8.0/Dino WorldLab.dll" check --count 110
dotnet "Dino WorldLab/bin/Release/net8.0/Dino WorldLab.dll" check --people 150 --packs 100
dotnet "Dino FloraLab/bin/Release/net8.0/Dino FloraLab.dll" rockcheck
dotnet "Dino WorldLab/bin/Release/net8.0/Dino WorldLab.dll" skin --seed 4 --archetype isle --at 760,520
```

`skin` grows the skin round a point and says what a tile costs and comes to, whether tiles meet, and whether any of it stands where someone would; run it on a few kinds of rock (`--seed 5 --archetype archipelago --at 384,472`, `--seed 9 --archetype badlands --at 240,816`, `--seed 10 --archetype ashen --at 480,376`). `skin --from x,y,z --to x,y,z` prints what the field makes of the points along a line, for looking into it.

`check` holds the rules above, and fails a world now and then on road grade, as it always has, where a highway passes close to itself. `weather` passes as it stands, but over a dozen worlds it fails on distance with or without any of this: that limit was already close. For looking: `relief` says what a world's land is made of, which forms its mountains take, and where to go and see each (`look at`), and the rockiest open ground. `view --at x,z` draws a place with no game, in a second, `--plain true` for its shape alone. `probe --at x,z [--larger n]` says what's at a point, packs included. `rocks` is a sheet of every stone. The camera looks along +z, so a mountain only faces it from the south: the spots `relief` lists are those. In the game, `--at x,z --weather clear --calm true`, and `--look 55` to see more of a mountain at once.

## Where it stands

The skin was built on 2026-11-12 after the shader's blocks were called horrible, "mostly just smooth patches and boring mountains". It costs about nothing on a 7900 XTX at 3440 x 1369 (the canyon: 160.6 FPS with it, 162 without), as the ground's lattice it replaced cost about as much, and a tile is 5,000 to 9,000 triangles, more on schist and basalt (up to 16,000 on average), where the budget isn't always reached. Domes read as blocky granite rather than domes. Judged from stills, by a machine, in a handful of places per form and mostly in clear weather at noon. Nobody has walked along a wall, watched a shadow come in, or played a fight against one. Nobody has watched the joints move, which is where a fault in their filtering would show. Everything is tuned to the game's camera and only that. Saved lands move: sites go where the mountains aren't, and a hero saved inside what's now rock wakes at the landing.
