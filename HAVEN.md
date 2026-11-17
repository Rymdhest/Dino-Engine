# The Haven's town

The Haven is where a hero spends more time than anywhere else, so it's laid out by hand, to the metre, as nothing else in the game is. A person called the town before this one boring and bland, with houses that looked like AI slop and made no sense, no path to any door, and ground that looked as if a simple brush had painted different textures in different places, cheap and low effort. They asked for the level of Kingdom Come: Deliverance, Crimson Desert and Ghost of Tsushima: a town with level differences and stairs, where everything seen tells something, makes sense, and comes together. Read this before changing anything in the town that isn't its square (`SQUARE.md`) or the inside of a hall (`HALLS.md`), and `LORE.md` before naming or colouring anything in it.

## What was taken from the three

- **One theme and a short palette** (Ghost of Tsushima gives each place one). The Haven's is blossom: timber, lime and thatch, the gate's pale stone, and cloth in six dyes and cream (`TownPieces.Dyes`: blossom, gold, sky, sage, lilac, teal). Everything of cloth, every shutter and door, every sign is one of those. Nothing is black iron, oxblood or brass, which are the Reich's.
- **Plots, as a real village has** (Kingdom Come's are surveyed from Bohemian ones): a house stands at the street behind a fence with a gate, its garden before it and its yard behind, and what the household does is out where it's done. No house stands in a lawn.
- **Layers** (Crimson Desert's Hernand): walls that hold ground up, steps through them, kerbs between every two surfaces, beds, arches. A change of level is always a wall and a flight, never a slope with a texture on it.
- **Props carry the story**, and every one is in the middle of something: a roof half thatched with the ladder still up and the reed stooked beside it, cloth out of the dye still dripping its colour onto the cobbles, a boat on the stocks bigger than any that brought the town here.
- **The key thing is exaggerated**: the gate at the head of everything, each hall on its own terrace, the wall with its buttresses over the harbour.

## What's where

`HavenBuilder` lays the levels, the halls, the streets and the wall, and `HavenTown` everything else, in metres east and north of the town's middle, which is the heart of the square (east is the left of the screen, as the camera looks north). Every hall and every house faces south, at the camera.

- **Three levels.** The harbour is the land as it was. The town stands on a rise two metres over it (`HavenBuilder.Lift`, five steps), put into the land itself before anything is planned (`RaiseTown`), with banks all round but along the south, where the town's wall holds it up. The Forge and the Gaming House stand three steps over that on terraces of their own beside the gate's (`HallTerrace`), and the Archive two steps over the Row on a podium (`Podium`). The gate's terrace is the square's own (`SQUARE.md`).
- **The Forge is by the square**, west of the gate, as it's where a Defender goes most, and the Archive is at the west end of the Row. A person asked for that swap.
- **Streets go somewhere.** Market Street runs west out of the square under the Forge's terrace to the thatcher's gate, Barn Lane east under the Gaming House's to the brewer's, South Lane from the arch to the Row, and the Row along the top of the wall, turning down off the rise at its west end as the cart's way to the harbour. All four are setts between kerbs. A back lane of trodden earth goes on from the end of each side street down to the Row, behind the Archive and past the joiner's yard, so each half of the town is a round and no street ends at nothing; a signpost stands where each parts. The Harbour Steps go down through the middle of the wall, and footpaths along its foot and off the Row's east end meet at `HavenTown.HarbourWay` and go on to the water.
- **The market** is along the foot of the Forge's terrace, on the north side of its street, so that every stall's counter faces south: under an awning that slopes to the camera a stall facing any other way shows only cloth. The merchant and the beta vendor keep the two nearest the square.
- **Each hall's work is out in front of it.** The Forge's forecourt has its anvil, coal, bar stock and a tyre waiting for its wheel, and the dyer's lines; the Gaming House, an old barn, has tables out, straw about its doors, a cart in its yard, a rick on the bank and a hitching rail under its wall; the Archive has the Haven's banners and the Row crossed in flags before its steps.
- **The Row's homes**, west to east: the bakehouse (two storeys under tiles, its upper floor out over its shop, loaves cooling in the garden), the cottage everyone passes (thatch, a wing, roses, the washing), the gardener's (more garden than house, with a kitchen garden behind it of beds each in its own crop, an arbour and hives) and the joiner's (its near end open as a shop, a sawpit and seasoning boards behind). Off the ends of the side streets are the thatcher's and the brewer's.
- **The green**, east of the square: an old oak with a seat round it, a household's linen on the lines, a striped fly with a table under it, and the ways feet have worn across it.
- **The wall and its walk.** Dressed stone with buttresses, every other one carrying an urn, a flagged walk along its top with benches looking out to sea, and at its foot the town's water running from a spout into a trough.
- **Below the wall**: the tiltyard on raked sand, the landing house where whoever comes next is taken in (a fire lit, a table laid, the bundles of whoever came last still at its door), the fisher's cottage, the jetty and its boats, nets and the catch drying, the lanterns hung at the water for those who haven't come yet (`TownKind.Memorial`), and on the shore of the bay the boat the town is building to go back for them.

## Levels

Read Raised ground in `SQUARE.md` first: everything there holds here.

- **Order.** `RaiseTown` changes `World.Elevation` itself, so every road, tree and footprint planned afterwards stands where the ground will be. Soft pads (`TownMason.Level`, `World.Pads`) are laid before the roads are stamped; sharp ones (`TownMason.Raise`, `World.Terraces`) after, as a road's shoulder pulls everything within 8 m to its own height. A hall is given its floor outright (`SiteBuilder.PlaceHall`'s `floor`).
- **A wall hides every step.** Raised ground stops `TownMason.Inset` (1.47 m) inside the face of the wall that holds it, and a flight's ground is a ramp under its treads (`TownMason.Flight`). A wall's returns end short of a street's reserve, or the validator fails them.
- **The land the town's on is land people walk** (`HavenBuilder.OpenTown`): `World.Reachable` is worked out before any site is built, and took the foot of the rise and the back of the beach for ground nobody reaches, which silently refused a hall its plot and fences their runs.
- **The Haven's `check` holds all of it**: a hall missing, a road through a wall, a flight nobody can climb.

## Ground

- **Turf, not meadow** (`GroundPaint.Green`, `WorldData.Green`, `TerrainSurface.Crop`): the town and its harbour stand on a sward kept short by feet and a scythe, thick, fresh, with daisies and clover in drifts, and the meadow only begins where the town ends. Hardly any of the meadow's own ground cover comes up in it, and little of what grows at a water's edge (`WorldGroundCover`). It stops at the beach, which a harbour keeps bare. Its height (`TownHeight`, 0.165 m) was settled by looking: at 0.11 every tuft stood apart as a rosette on bare ground, the field of stubs that cutting the Haven's grass short gave once before, and at 0.24 it was a meadow again.
- **Edges are laid, not frayed** (`WorldData.Trim`): a town's paving and cobbles keep an edge a stride wide where a camp's frays out over metres, and where two surfaces meet there's a band of kerbs flush with the ground (`TownMason.Band`, `Border`). Trodden earth frays only at its edge here too: in the open the fraying reaches right through a yard, which left holes of thick grass in the middle of the town's yards and broke its worn ways into dashes.
- **What each surface says.** Setts between kerbs are streets. Flags (`Paving`) are where the town shows itself: the square, the walk, before the Archive. Cobbles (`Plaza`) are where work is done: the market, the Forge's forecourt, the wall's foot. Trodden earth (`Yard`) is a yard, a worn way across turf (`HavenTown.Worn`) or the round of bare ground under anything that's worked at. Soil is a bed (`Garden`). Sand is the tiltyard and the beach.
- **A way to every door**: stones trodden into the turf from the gate to the step (`TownKind.Stepping`), with the turf worn round them.

## The town's own pieces

`Modelling/Procedural/Architecture/Town/` (headless) grows every piece from a `TownSpec`, whose id is all that's kept: `town:house:c077a6:1300:960:0:0:0:1937:0` is the cottage on the Row. Whole centimetres and choices, no dots and no hash. `TownForge.Make` builds one, on worker threads in `WorldAssetLibrary.Grow`, houses first as they're slowest. Each kind's numbers are listed with it in `TownKind`.

- **A house** (`TownHouse`) is a design (cottage, townhouse, longhouse, fisher's, workshop), a roofing, a walling, features as bits and a colour for its door and shutters. Its roof is most of what's seen of it, so it gets the care: one smooth surface that sags along its ridge and lifts in brows over the loft's windows, moss towards its eaves, streaks where rain runs, soot round its stack, and on a roof marked `Mended` half of it new and pale with the ladder still up. Eaves are short, as from this camera an overhang hides 2.4 times its own depth of the wall under it, which is also why a door has a hood and not a porch. `TownHouse.Solids`, `WingOf` and `DoorAt` say what's solid about one and where its door is, replaying the same draws its builder makes, so the fence's gate and the path are where the door is.
- **The Gaming House's outside is a barn's**: boards on a foot of field stone, a thatched roof from the same `TownHouse.ThatchOver`, a hayloft door with its hoist over the great doors, and two posts and a beam before them carrying its sign.
- **The halls' slates** weather in patches (`HallRoofs.Weathering`), are black round a smithy's stack (`sootAt`), and a square of them here and there was laid later than the rest (`Mended`).
- **Yardwork** (`TownKind.Yardwork`, a `YardTrade`) is what a household's or a hall's work leaves standing: washing, baking, fishing, joinery, gardening, arriving, smithing, tanning, dyeing, drying, brewing, thatching. A new trade goes on the end.
- **Foliage is rounded masses** in dark greens on the moss material (`TownPieces.Leaves`): boxes in a pale leaf card read as mint-green blocks.
- **Cloth moves.** Anything hung is tied to a joint of its own from its top to its foot (`MeshBuilder.Hang`) and swings in the wind as a plant leans in it (`Shaders/Plant.glsl`): banners, washing and dyed cloth on their lines, the hems of awnings, ribbons, the lanterns at the water, and the square's bunting, a joint to each pennant. One joint to a cloth, never one to a strip of it, as two joints read the wind at two places and tear it along the seam; and a joint turns everything it holds about its own top, so nothing far to the side of it may be tied to it.
- **A flat pale cloth is a sheet of paper from above.** The fly on the green was a flat sail first and read as one: it has a ridge, two slopes and stripes now.

## Putting things down

`TownMason` puts everything down at the level it's told, with what's solid about it and nothing more.

- **`Solid` or `Block`.** A footprint in a layer keeps everything else off the ground it's on, and the validator fails a prop or a structure within a street's reserve (its half width and 0.8 m). What stands beside a street, a bench, a stall's counter, a signpost, a flight's cheek, is a `Block`: solid, in no layer.
- **`Grow` or `Flower`.** A tree or a shrub grown where it stands has a footprint, which the validator fails on a road or against a structure. What grows in a border or a pot is a `Flower`, placed at a height with none.
- **`Place`** puts down a piece of the places' kit (`PlaceSpec`, `PLACES.md`) as solid as its own rule says (`Hitboxes.Of`).
- **A house** is a `Structure` footprint, so the chart draws it, with its lean-to, stack and wing as blocks (`HavenTown.House`).
- **Seeds come from one stream** (`TownMason.NextSeed`), in the order things are laid, so adding something in the middle changes how everything after it looks, though not where it is. Add at the end of a method where that matters.

## For this camera

It's 33 m off, 67 degrees up, and only ever looks north.

- A roof is most of a building and a south wall is seen at four tenths of its height: put the detail on roofs, on the ground, and on what stands in front of a south wall.
- Whatever has a front faces south. A stall, a sign, a bench with a back facing any other way is its own back.
- Tall things stand to the north of what they'd hide, and a tree over a street hides the street: the market's trees are small and stand back.
- Nothing pale and flat lies on the ground or hangs level over it.
- At this distance a detail under a few centimetres is nothing, and a thing's silhouette and colour are everything.

## Looking at it

`--haven true --weather clear --hour 12` with `--at x,z` (a place x east and z north of the town's middle is 211.3 + x, 134.1 + z): the market `--at 169,142`, the Forge's forecourt `--at 168,152`, the Gaming House's `--at 255,152`, the Archive from the Row `--at 164,92`, the Row's west end `--at 198,100` and its east `--at 243,100` and `--at 263,100`, the green `--at 258,128`, the thatcher's `--at 139,146`, the brewer's `--at 284,146`, the wall from below `--at 211,78`, the tiltyard `--at 213,62`, the harbour `--at 274,50`, and the boat on the stocks `--at 265,27`. `--look 62` from `--at 211,150` and `--at 211,92` shows the roofs of each half, though the far ground stops where the chunks do. `--wind 0:14:90` blows hard from the west, for seeing what hangs. `Dino WorldLab plan --seed 1610 --archetype haven --site Haven --span 260 --size 1300 --solids true --out file` draws it from above with everything solid.

`Dino WorldLab where --seed 1610 --archetype haven --near x,z` says what the ground at a place is painted as, which is the way to find out why a yard is green.

`Dino WorldLab town` builds every piece of the town from its id and says what each kind costs (68 pieces, 121,000 triangles, 30 lights, 139 things hung, a sixth of a second on one thread). It fails a piece that builds nothing or reaches too far, a joint that doesn't hang down or holds something far to the side of it, a town with too few homes, stalls or lamps, and a home nobody can walk up to the door of: straight out from each door a body has to fit all the way to ground the rest of the town is walked from. It's been seen to fail with every garden's gate shut.

After any change here run `Dino WorldLab check --seed 1610 --count 1 --archetype haven` (`--most 30` lists more than the first six faults), `town`, `square`, `halls` and `hitboxes`, `Dino WorldLab check` on other kinds of land if `TerrainSurface`, `WorldGroundCover`, `HallRoofs` or `MeshBuilder` changed, `Dino NetLab lands` and `game`, `Dino LifeLab giants` on several seeds, the black test, and `--perf` at the square, the market, the Row and the harbour against the build before.

## What it costs

At 3440 x 1369 on the 7900 XTX, standing still at noon, against the town before it, two runs of each, turn about: the square 9.45 ms a frame before and 9.55 after, the market's street (where the Archive's front was) 9.2 and 9.4, the lane south of the arch 9.9 and 9.3, and before the Gaming House 10.2 and 10.3. Places with no before: the market 9.7, the Row 9.6, the harbour 8.8, and the green 10.8, which is the dearest place in the town, a screen of turf under an oak: its grass is 1.1 ms where the square's is 0.4. The walls, flights and piers are 241,000 triangles and 29 lights with the square's own (`Dino WorldLab square`), and the town's pieces 121,000 and 30.

## Where it stands

**Nobody has walked through it but the bots and the stills.** Ask first whether it reads as a town from the game's camera, whether the levels and steps are worth the walking they add, whether the Forge is now where it's wanted, and whether the turf reads as turf: cutting the Haven's grass short was tried once before and read as a field of stubs, and this one is thicker but has only been seen in stills. Nobody has watched the cloth move. Villagers stand where they're put and walk as they did; nothing in the town is used but what was used before.
