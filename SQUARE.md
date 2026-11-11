# The Haven's square

The heart of the Haven is built as a town's square is: in layers of walls, steps and beds, quiet in the middle and rich at its edges, so that it reads from the game's camera, 33 m off, high in the south and looking north. It was asked for after the first square, a paved disc in a meadow, was called "ground texture slop": the standard is a AAA town, Crimson Desert's Hernand in particular, whose squares are low rubble walls holding up beds of grass and shrubs, kerbs and gutters in setts, arches, pillars and things set out for people to use. The rules here are what makes that hold together with this engine's ground, camera and checks.

## What's where

`World/Generation/HavenSquare.cs` lays it out, in metres east and north of the Haven's middle (east is the left of the screen, as the camera looks north).

- **The terrace.** The great waygate stands two metres up (`TerraceUp`, five steps of a real 15 cm at a person's 4.6 m) on a terrace of dressed stone, its walls' faces at `TerraceFront`, `TerraceSide` and `TerraceBack` with a parapet `Parapet` above it and piers at its corners, the front two carrying urns of lavender. A paved court surrounds the gate, with lawns and sakuras either side of it and one behind it, framed by its arch.
- **The flight.** Twelve metres wide, between cheek walls whose piers stand out in front with a lantern each, and a smaller lantern where each cheek ends at the top. Whoever comes through the gate steps out at the head of it (`GameServer.HavenArriveBefore`), and the map table stands at its foot (`TableAhead`).
- **The avenue's beds.** Raised beds of field stone either side of the avenue, against the terrace, with rounded corners, a pot on each wall where it meets the avenue, a maple in each, and box, lavender, hosta and azaleas.
- **The heart.** A round bed in the middle whose wall is a seat all round (`WallForm.Seat`), with a sakura and azaleas in it, a ring of kerbs round it (`Collar`), and eight spokes of the same stone out to the band round the square's edge, between the ways in: every waygate's road comes here first, and the square is its compass.
- **The southern beds.** An arc of ground either side of the lane in from the south, walled in field stone on both sides with half circles at their ends, a white cherry in one and an olive in the other.
- **The arch.** Over the lane where it comes into the square: piers, an arch of wedge stones round a keystone, coursed spandrels, a cornice and a little roof of clay tiles, with a lantern hung under its crown. Coming in from the south, it frames the square, the heart, the flight and the gate.
- **The loggia.** On the east side, by the way to the Gaming House: stone columns under oak beams and joists, a striped sail over half of it, lanterns hung in its bays, and two trestle tables laid for a meal (plates, cups, bread, fruit, jugs). The stash stands beside it, facing across the square.
- **The market** keeps the west side: the merchant and the beta vendor (`HavenBuilder.PlaceTrade`).
- **The well** stands off the heart to the south-east, a round wall round dark water under a windlass and a little tiled roof.
- **Masts and bunting.** Four masts round the square, each with a lantern out over it, which is all that lights the square's middle at night, and bunting in the Haven's colours strung between them and on to the loggia. A hung lantern reaches 17 m rather than a street lantern's 24 (`PlazaStone.HungLantern`), as at 24 the masts' four cost 0.3 ms, each lighting most of the square, and at 13 the heart went dark.
- **Paving.** The square, the avenue, the court round the gate and the loggia's floor are the gates' stone (`GroundPaint.Paving`), with cobbles round the beds; the halls' streets and the lane in from the south are setts between kerbs, as a village's streets are (`TerrainBakeStage.NearVillage` counts the Haven).

## Pieces are grown from their ids

Every piece is a `PlazaSpec` (`Modelling/Procedural/Architecture/Plaza/`, headless): a kind, a seed and whole centimetres or degrees, so `plaza:arc:5a0a2e:0:1720:124:34:160:120:120:-1` is a field-stone wall 17.2 m round an arc, facing in, and that's all that's kept. Ids have no dots and no hash, which the validator would read as a building's or a plant's. `PlazaForge.Make` builds one from its spec, `WorldAssetLibrary.Grow` builds them on worker threads with the plants and stones, and `WorldAssetLibrary.Get` builds any it missed. Each kind's numbers are listed with it in `PlazaKind`, and a new kind is a line there, a case in the forge, and a builder.

- **Stone is the gate's own.** Dressed stone is `WaygateDesign.GreatStone`, darker towards the ground (`WeatherShade`), on `MaterialCatalog.Rock`, as the user loved the gate's stone; field stone is the same rock with more to each stone (`PlazaStone.Rubble`), and what faces the sky goes green with moss. A stone is a block of its own with its arrises taken off (`Masonry.Block`), touching its neighbours only along their chamfers, and nothing seen shares a plane with anything else: an arch's spandrels stand 5 cm back from its ring, a pier is a little wider than the cheek it ends. A big face in the rock material reads as crazy paving, so nothing is one block longer than about 1.6 m: a cornice is laid in blocks.
- **A wall** is laid along a `WallLine`, straight along +x or round an arc by bearing (clockwise from +z, from above, as `HavenSquare.Toward` is), its stones cut to the curve so their joints run to its middle. `PlazaWalls.Lay` turns a block's corners round to the way `Masonry` wants them, whichever way a wall faces.
- **Nothing here is the Reich's**: no black iron, oxblood or brass (`LORE.md`). Cloth is blossom, cream, gold, sky, sage and lilac, and metal is left out.
- **Every lantern is a piece of its own** (`PlazaKind.Lamp`), set on the flight's piers, under the arch and in the loggia by `HavenSquare` at the places their pieces say (`PlazaPieces.FlightLamps`, `ArchCrown`, `PlazaDressing.PergolaLamps`). A light inside a model over 6 m across is shadowed by that model (`LocalShadowCasters`), so built into the flight they were shut in by their own glass whenever they held one of the few shadow maps, which go to the lights nearest the hero, and they went dark and lit again as anyone climbed it. A mast's lantern stays in the mast, which is small enough to be left out of its shadow.
- `Dino WorldLab square` builds every piece of the Haven's square from its id and says what it costs. It's about 140,000 triangles and 11 lights, and builds in a fifth of a second on one thread.

## Raised ground

A bed, the terrace and the flight are ground raised exactly, and walls hide where it's raised.

- **Terraces are laid after the roads** (`WorldData.Terraces`, applied by `TerrainBakeStage` after `StampRoads`), as a road's shoulder reaches 8 m and pulls whatever's near it towards its own height. The square's own level (`HavenSquare.Level`, the average of the ground there) is an ordinary pad laid before the roads, and the streets into it start at its edge at that height (`RoadStage.BuildRoad`'s `startHeight`), so they meet it.
- **Their edges are sharp** (`Sharp`) and **stop inside the walls** (`Inset`): heights are a metre apart and the ground is drawn through them by Catmull-Rom, so a step is laid as a slope up to 1.42 m wide, which has to fall inside a wall and never in front of it. That's also why a bed's corners are no rounder than the inset.
- **What's left inside a wall is covered**: a wall that holds ground up lays its edge against its back (`PlazaWalls.Berm`), green and mossy and as high as the drawn ground, and the bed's grass grows up through it. It was bare dark soil first, and every bed read as an empty frame.
- **A flight's ground is a ramp** (`GroundPad.Rise`), laid along the inner corners of its steps, so the drawn ground, 0.15 m above where people stand, never comes up through a tread. Feet sink about 0.3 m into a tread rather than the usual 0.15, which only shows on the stairs.
- **The Haven's middle is a bed**, so the floods of what's walkable and passable start from its footing instead (`WorldSite.Footing`, `Landing`), on the paving between the heart and the map table. Before that, every spawn in the Haven ended up in the heart's bed. Anything else that means "where the landing is" reads `Landing`.

## Beds and planting

- **A bed is a garden** (`GroundPaint.Garden`, `WorldData.Garden`): `TerrainSurface.Tend` keeps its grass thick, short, green and in flower in small drifts of pink, cream, violet and marigold, whatever the land round it grows, and it never holds water (`HollowAt` agrees).
- **Laid-out ground is `PlacementLayer.Garden`**, in `Built`: nothing is put down on it later and nothing wild grows there, but what a site plants does. The open paving of the square and its avenue is kept the same way.
- **The square's plants are grown where they stand** (`HavenSquare.Grow`), on the soil they stand in, rather than planted: a site's planting keeps a tree's whole crown clear of anything built, and a tree in a bed always reaches over its wall, so none of them was planted. Sizes are given outright, as a kind's own range only spans 15%, and a town keeps its trees smaller than the woods do. What stands in a pot or an urn is placed at its soil's height (`PlazaDressing.PotSoil`, `PlazaPieces.UrnSoil`).

## Looking at it

`--haven true` with `--at x,z`: the square is `--at 211,142` (its middle, 211,134, is in the heart's bed), the flight and the terrace `--at 211,151` and `--at 211,158`, the loggia `--at 232,126`, the arch from the south `--at 211,99`, and `--look 20` comes in close. `--render night=1` shows its lanterns. The intro banner covers the top of the screen for a while, so take a shot after 1,300 frames. Judge it from the game's camera and the closest zoom. Of all of it only the trees thin away for a hero behind them (`glModel.OpensForFocus`): the arch's roof hides someone just north of it for a step or two, which is what was asked for.

After any change here run `Dino WorldLab check --seed 1610 --count 1 --archetype haven` and `square`, `Dino NetLab lands`, and `Dino LifeLab giants` on several seeds, and look at it in the game, by day and at night.

Nobody has walked through it but the bots and the stills.
