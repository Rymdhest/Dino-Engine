# The sea floor

What lives under the water and on it: meadows of seagrass, kelp and wrack, corals, sponges and sea fans, starfish and urchins, rock standing up off the bed, the weed and barnacles the sea grows on stone, and lilies on still fresh water. How it's chosen, put down, built, moved and shaded.

Status: built 2026-11-14, judged from the flora lab's sheets and stills from the game's camera. **Nobody has seen it move, or swum over a reef in a real session.**

## The one idea

**The sea floor is ground cover.** What grows under the water is put down as the rest of the small plants are (`WorldGroundCover`, by `WorldSeaFloor`): on the client, as chunks stream in, one thing to a 1.6 m cell of the ground cover's grid by one draw, so nothing collides whichever order chunks load in, and a cell always grows the same thing when it comes back. None of it has a say in how a world grows, nothing in the rules reads it, and nothing of it is solid: what could stop a body stands only where nobody can wade.

```
  the coast's biome          SeaLife profile (Cold, Temperate, Warm, Tropical, FreshWater)
        |                          |  which species, on what bottom, how deep, how thickly
  WorldSeaFloor.TryGrow  ->  one draw per cell, by bands: floating, critters, washed, deep, reef, meadow
  WorldSeaFloor.BeginSquare -> rock formations on an 8 m grid, out of anyone's reach, nothing else under them
        |
  species in Flora/Content/Seas.cs, grown from Flora/Forms/SeaForms.cs (headless, a species and a seed like any plant)
        |
  drawn as any instanced plant; swayed by the waves (Wind_Vegetation.frag); stone crusted by the sea (Water/ShoreGrowth.glsl)
```

## What lives where

A coast's life is a `SeaLife` (`World/Content/SeaLife.cs`), chosen by the biome of the water's edge (`SeaLife.Of`): what the biome names (`BiomeDefinition.Sea`), or what its warmth calls for, a biome's `Temperature` being absolute as the weather reads it. Still water, a pond or the lakes of a land of still water, is `FreshWater`.

| Life | Warmth | Soft bottoms | Hard bottoms | In the wash | Deep | Critters |
|---|---|---|---|---|---|---|
| Cold | under 0.38 | eelgrass, sparse | oarweed, sugar kelp | wrack, sea lettuce | giant kelp | green urchins, starfish |
| Temperate | 0.38 to 0.65 | eelgrass | oarweed, sugar kelp | wrack, sea lettuce | giant kelp | purple urchins, starfish |
| Warm | 0.65 to 0.8 | turtle grass | wrack, soft coral, sea fans, sponges | wrack, sea lettuce | | urchins, starfish |
| Tropical | 0.8 and over | turtle grass | staghorn, table, brain and boulder corals, soft corals, fans, sponges, and patch reefs on the sand | | boulder and table corals, fans | black urchins, cushion stars |
| Fresh water | | pondweed | | | | lilies float on it |

What a cell grows goes by:

- **How deep the water is** over it (`WorldData.WaterLevelAt`, a pond's level or the sea's), past `Deepest` (7 m, 5 for fresh water) nothing: nobody can see that far down.
- **The bottom** (`Hardness`): hard where the land's rock runs into the water (`RockNear`), where the bed is steep, in patches on a stony shore (gravel, shingle, ice), and in a hot sea wherever it's built reef on the sand (`Patches`, never off a muddy shore); soft in the sand and mud between.
- **Bands**, each a likelihood with its own window of depth, and one draw falls in at most one: lilies on still shallows in drifts, critters anywhere, weed in the wash on hard bottoms (to a metre), reef on hard bottoms from half a metre, the deep's from 3 m, and meadows on soft bottoms to 4 m, in drifts with an edge, so a meadow is a carpet and never strewn about.

- **Masses, not strewn.** What grows on hard bottoms grows in masses, as reef heads, kelp beds and the barrens between them do (`ReefPatch` metres across, `ReefCover` of the hard ground in one, `ReefScatter` of the reef's likelihood left between them for a few young colonies): crowded and overgrowing each other in each, two to a cell in its heart (the second a draw of its own half a cell off), larger towards its heart and smaller and fewer towards its edge, with sand between. One to a cell, evenly, a reef read as polka dots, and even a heart showed sand between every colony. Urchins and starfish keep to the bare hard ground between the masses and round their edges, which they graze.
- **Stands of one kind.** A mass is mostly one kind (`StandShare` of it), a thicket of staghorn, a field of tables, a stand of oarweed, chosen by a cell of a grid warped so its edges wander (`StandOf`), with others among it: a random mix of everything read as litter.
- **Halos.** A patch reef in a meadow is ringed with bare sand, grazed by what shelters on the reef and goes out from it by night, as reefs show from the air.

Nothing stands out of the water. Every form that lives under water says the most it can stand at a scale of one (`IUnderWater.Tallest`, worked out from its own numbers as its spines are laid, and held by `Dino FloraLab check`, which grows every one): what's rigid, a coral, a sponge, a fan, keeps under the deepest trough of an ordinary day's waves (`WorldSeaFloor.Trough`: a wave breaks at 0.78 of the depth, its trough is 0.41 of it under the mean, and no ordinary breaker here is much over a metre), as what's out of the water at low tide doesn't live; what sways, a meadow's blades, weed and kelp, keeps under the still water, as they lie out along the surface rather than stand up out of it, and a trough may pass over them. Either is grown smaller to fit, as in the shallows it is, or not at all. A giant kelp is made for 3.7 m of water (`Seas.GiantKelpReach`) and scaled to the depth it stands in, so its crown lies flat on the surface, and its fronds lean out further the higher they leave the rope, never rising past its end. A sea fan is turned broadside to the waves, which come in up the slope of the bed (`Downhill`). A lily floats at the water's level.

## Rock formations

`WorldSeaFloor.BeginSquare` puts rock up off the bed, on a grid of 8 m squares (`FormationCell`, a terrain chunk), a formation and a smaller boulder beside it at most to a square: the world's own rock (`NatureCatalog.RockOf`), boulders, blocks and slabs anywhere, outcrops where it's deep, and tors in granite and columns in basalt deeper still.

- **Out of anyone's reach.** The water is deeper than anyone wades (`WorldData.WadingDepth`) under all of a formation, as far as it really reaches, and the world says nobody stands within a body of it (`Walkable`), as nothing here is solid to anyone.
- **Always under water.** Its top keeps under the deepest trough of an ordinary day (`Trough`), and it's sunk into the bed a little, as rock that's lain there a long time is.
- **Measured, not designed.** `RockForge.TopOf` and a rock's footprint are what its forms are designed to, and the meshes go past both: a schist slab stands 2.55 times its `TopOf`, boulders and blocks 1.4 times, and an outcrop reaches 1.85 times its footprint. So a formation is sized by the most measured over 200 of each with a tenth more (`TallestOf`, `ReachOf`), and `Dino WorldLab sea` holds every one placed to its real mesh. (The land's own rock is still held by `TopOf`, which its meshes exceed in the same way: TERRAIN.md's business, not changed here.)
- **Decided by its neighbours' draws alone.** Where two would overlap, the one with the higher rank stands (`Placed`), from each place's own hash, so the answer is the same whichever squares load first. Nothing else grows where one stands.
- **Only on rocky bottoms**, as likely as the coast's `Formations` says.

## The forms

`Modelling/Procedural/Flora/Forms/SeaForms.cs`, headless like every plant, a species and a seed:

- **`RibbonForm`**: seagrass, wrack, sea lettuce, pondweed. A broad sheet on a ribbon's give stretches past what holds (`Dino FloraLab bend`), so sea lettuce is one stiff link. Shoots strewn along a runner or two under the bed, a few blades to a shoot, the outer ones the oldest, longest, leaning most and carrying what's grown on their ends (epiphytes, a pale crust, browning). Buoyant, standing up in still water. A meadow's clumps are wider than their cells, so they reach into each other.
- **`KelpForm`**: a holdfast, a stalk and fronds. Oarweed's straps fan out from its top; sugar kelp's one strap is crinkled down its middle (`Ruffle`); a giant kelp is a rope to the surface with fronds up its top and its crown lying out on the water.
- **`CoralForm`**: a colony is one colour of the few its kind comes in, and its youngest parts are paler. `Branching` (staghorn: antlers that fork as they climb), `Table` (a lobed, scalloped plate on a stalk, one rim for both faces so its underside never shows past its top, its rim's noise read round a circle so the lathe's seam closes, its top closed over the stalk, and the stalk rising straight into it with its flared end inside the plate, as a slanted end came up through the top), `Massive` (a broad dome, ridged as a brain or lumpy as a boulder: as tall as it was wide, a boulder coral was a cone), `Fingers` (a soft coral: fat fingers domed over at their tips, from a mound), `Tubes` (a barrel sponge, or a stand of tube sponges, open onto a dark hollow), `Fan` (a sea fan: a lattice card on a stalk).
- **`StarForm`** and **`UrchinForm`**: starfish, and urchins whose spines are a few thin blades, as anything finer is less than a pixel from up here.
- **`LilyForm`**: pads (`Cards/SeaCards.cs`'s `LilyPad`: round, cleft to the middle, veined, reddish at the rim), a young one bronze, a water lily open among them or a bud standing out of the water.

Everything is a few hundred triangles at most (staghorn is the heaviest, 720), and colours are albedos under a sun of about 13, muted as live reefs and weed beds really are: the water's own tint and caustics do the rest. Living coral gives back a tenth of the light or so, brown, olive and purple, and sand two fifths, so a reef reads from the air as dark heads on pale sand: as bright as the sand, colonies read as pale discs lying on it, like lily pads, which is what they first did under a high sun. Wet kelp is dark olive-brown, no lighter than the seagrass beside it. Fans and sponges are brighter, as pigment colours them. Shapes are judged in `Dino FloraLab sheet`, colours only in the game, through the water.

## Motion

Under the water it's the waves and not the wind that move what grows. `Wind_Vegetation.frag` drives the vegetation spring that every plant's joints read: where the water covers a patch (`Water/TerrainHeight.glsl`, the water's own capture of the ground), it heads for the bend the water passing over it would give it, as the water runs shorewards under a crest and back out under a trough at the surface's height times `sqrt(g / depth)` (`shoreSurgeBend`), from the shore's own waves (`Water/ShoreData.glsl`, `shoreSurgeAt`), so a meadow leans with each wave that comes in and back as it goes, and plants and sea keep the same time. The shore's waves are only worked out within `ShoreWaveReach` (30 m) of the water's edge, so out past that the surge is the swell coming in on the same phase, fading out over four reaches, or every meadow beyond the surf stood still. `--render shoredebug=7` shows it on the bed: red where the water runs shorewards, blue where it runs back. Water drags as air 29 times as fast does (`WATER_PULL`), through the wind's own curve. It's the frame before's shore, as the wind runs before the water: nothing a frame late in a wave seconds long shows. Nothing under water flutters (`SeaJoints`), a lily's pads give to nothing but a body wading through them, and a pond, which has no waves the spring knows of, barely moves its pondweed.

## What the sea grows on stone

`Water/ShoreGrowth.glsl`, on stone alone (`Model.frag`), so every stone set down in the water, every formation and the cliffs the rock skin clads have it, and nothing that lives there does: below the troughs a coat of weed, thickest on what faces the sky (olive-brown in cold seas, green turf in warm), and in a warm sea a pink crust of coralline on what the weed leaves bare; from there up as high as the waves wash, barnacles in a pale crust, most on the faces that take the waves; above them, as high as the spray goes, the black band of the lichen that lives on salt. A land of lakes has no tide and no salt: a dark ring at its level and a film of green below. It's laid on the colour before the weather too, so the water that soaks it soaks the growth. The sea's warmth and whether it's salt are `WaterSingleton.SeaWarmth` and `Salt` (from the world, `WorldScene.CreateWater`), in `ShoreData`'s `shoreLife`.

## What it costs

Measured at 3440 x 1369 on a 7900 XTX, `--calm true --nolife true --weather clear`, against the same build with the sea floor switched off: a bay of eelgrass meadows and kelp (`--seed 3 --archetype archipelago --at 552,966`) 7.55 ms a frame against 6.64, and the busiest reef in its world (`--seed 55 --archetype archipelago --at 1144,556`, some 900 colonies in view) 7.95 against 7.15. Of that, the instanced geometry is about a third; the rest is what any crowd of small shapes costs downstream: edge pixels lit sample by sample, the soft sun shadow working harder near what casts, and the G-buffer's resolve. What was taken off on the way:

- **Nothing thin casts a shadow** (`PlantSpecies.CastsShadow`, `glModel.CastsNoShadow`): seagrass, weed, pondweed, urchins, starfish and the bed's kelp are left out of the sun's maps, as a blade's shadow under a metre of water is less than a pixel and every map, and every pixel near it, paid for it. Corals, sponges, fans, the giant kelp's canopy, lily pads and rock cast.
- **No facet under a pixel**: a staghorn's branches are four sides at the foot and three after, a soft coral's fingers four, and a seagrass blade three segments.
- **A meadow keeps its shoots.** Two thirds as many, even half as wide again, read as sprigs on sand rather than a carpet: the carpet stays and costs what it costs.

## Rules

- **Never solid, never in the way.** What could stop a body stands only where nobody can wade; everything else is walked through, as any small plant is.
- **Never on the main menu.** The vista is left as it was composed (MENU.md).
- **New randomness is its own fork** (`"sea floor"`), and a new species or life changes no world.
- **Grown while a world loads** (`WorldSeaFloor.AssetsOf`, in `WorldAssetLibrary.AssetsOf`), with the beach's pebbles, or the first beach to stream in grows them on the frame that draws it.

## Looking and checking

- `Dino FloraLab sheet --plants coral.staghorn,coral.table,...` for shapes, `check` for soundness and budgets (`CheckBiomes` counts every life's species as grown, and every form under water is held to the height it says it can stand), `list` for what each costs.
- `Dino WorldLab sea --seed N --count N [--archetype id]` grows the sea floor over whole worlds as the game streams it and holds it to the rules on the models themselves: no formation anyone can wade to or stand within a body of, nothing rigid out of a trough, nothing that sways out of the still water, lilies only on still fresh water and on it, a giant kelp's crown no more than a quarter metre over the surface. It prints where each kind grows thickest, for `--at`.
- In the game, temperate: `--seed 12 --archetype isle --at 832,600` (meadows) and `--seed 3 --archetype archipelago --at 552,966` (meadows and kelp); tropical under a high sun: `--seed 55 --archetype archipelago --at 1144,556` (a patch reef), and in a golden evening `--seed 6 --archetype archipelago --at 999,334`; lilies: `--seed 2 --archetype lakeland --at 285,425`. Add `--calm true --nolife true --weather clear`. A land keeps its own light (`--hour` is the Haven's alone), so judge a reef's colours under a high sun.
- After any change here run `Dino FloraLab check` and `bend`, `Dino WorldLab sea` (on archipelagos, isles and a lakeland) and `check`, look at a coast of each warmth and a lake, and `--perf` at a coast against the build before.
