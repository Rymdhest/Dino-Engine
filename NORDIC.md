# The north's villages

How the pinewoods and the snowfields are settled (`BuildingStyle.Nordic`): red houses with white corners, the poles-on-a-slant fence round every field and along the lanes, birches by the farms and along the roads, a midsummer pole on the green, a well drawn with a sweep, hay on racks and firewood stacked round. Read `LORE.md` before giving any of it a new look or a name, `FENCES.md` before changing the fence, and `FLORA.md` before changing a birch.

Status: built on 2026-11-13 at the request of a person who grew up in Sweden and wanted the old Swedish village, red with white corners, and above all its fence, the gärdesgård, done with care. Judged from the lab's pictures and from stills of the game. See "Where it stands" at the end.

## The one idea

**A village of the north is red walls drawn in white under red-brown roofs, in a land fenced with slanted poles and lit by white birches.** Everything here is after the old farms of Sweden, and everything is judged from the game's camera, 33 m up at 67 degrees, where a roof and a fence's slant are what's seen.

- **Falu red.** Walls are upright boards with a batten over every joint (lockpanel), painted the red of the copper mines' earth: matt, chalking paler where the sun gets at it, each farm's a little different (`NordicKit`). It's a warm red, with the yellow of iron earth in it, and it's always with white. It's never the Reich's oxblood, which is deeper and leans to blue, and which nothing white goes with (`LORE.md`).
- **White corners (vita knutar).** A white board up every corner, the long wall's lapping the gable's so the corner is one white line; white casings round six-pane windows, with a crown over each and a sill under it; white verge boards up every gable, standing a little over the roofing so every gable is drawn in white from above; white fascias along the eaves; a white board across each gable at the eaves.
- **Roofs** of red clay pantiles, deeper and browner than the south's, on most houses; turf, lower pitched and held at its foot and ridge by logs, on the rest and on many outbuildings; tarred shingles, steep and near black, on the chapel, its bell tower and some barns. Snow settles on all of them as on anything that faces up (`Weather/Wetness.glsl`).

## The buildings

`Modelling/Procedural/Architecture/NordicStyle.cs` builds every building of the catalogue, front towards +z, from a kit of its own (`Nordic`), not the older styles' `Kit`: `BuildingGenerator.Generate` hands the north to it before anything else.

| Catalogue | In the north | What makes it the north's |
|---|---|---|
| Hut | A crofter's cottage (torp) | Its door to one side under a little roof on white brackets, one window beside it |
| Cottage | A one-room house (enkelstuga) | A farstukvist, or a door hood where there's no room for one |
| House | A farmhouse of a storey and a half, or two (parstuga, mangårdsbyggnad) | Door in the middle of the front, windows either side, small windows under the eaves, a second row in the gables |
| Longhouse | A long farmhouse | Two chimneys, and a plain kitchen door at one end under a hood |
| Inn | A coaching inn (gästgivargård) | Two storeys, its porch a balcony with a door onto it from upstairs |
| Hall | The parish meeting house (sockenstuga) | Two storeys, and a bell in an open turret on its ridge (a vällingklocka) |
| Barn | A red barn (ladugård) | Wagon doors in white frames with white crosses on them, the braces of a pair making a chevron; stable windows; a hay hatch high in each gable under the hoist's beam; now and then a bell on its ridge |
| Temple | A red chapel and its bell tower (kapell, klockstapel) | A steep roof of tarred shingles, tall windows, a pediment over the door in its gable; the bells in a tower of their own beside it, tarred, with a red belfry, a flared skirt and a spire |
| Shed | A privy beside a woodshed (dass, vedbod), or a storehouse up on stones (härbre) | The privy's door has a heart cut in it; the storehouse is red logs crossing at the corners, on four stones each with a flat stone on top that no mouse gets past, and its verge boards cross past the ridge |
| Watchtower | Tarred posts, a red lookout, a tile roof | (`PropGenerator.BuildWatchTower`) |

A **farstukvist** is the porch over the front door: a deck up two steps, white posts with rails between them and the wall, a little gable of the house's roofing with its own white verges, and now and then white boards fanned in its gable like a rising sun. Its ridge never comes higher than the lowest edge of the house's eaves, or the two roofs would cut into each other; where there isn't room for that a door gets a hood on brackets instead (`NordicPorch`, `DoorHood`).

**Trim boards** are painted boards too, through `Trim`, which lays each face's texture with the grain along the board and across one of the material's boards only, between its battens, so that a white corner board shows no batten. Keep a trim board 0.3 m wide or less, or it reaches a batten.

Only a ground-floor window throws its pool of light on the ground (`NordicWindow`'s `shines`), as one above it would only light the same ground again: lighting every window doubled the cost of the spotlights.

## The material

`MaterialCatalog.PaintedBoards` (`boards-painted`) and `MaterialRecipes.PaintedBoards`: five boards to a 2.4 m tile, battens standing two fingers proud, each board and batten a shade of its own, the paint chalkier along the grain and dirtier at a batten's foot. It's pale (about 0.7), as the colour is in the vertices, so one material is the red walls, the white trim and every painted door. `Painted(albedo)` divides an albedo by what the material comes to.

## The fence

The gärdesgård is `FenceCatalog.Roundpole` (`RoundpoleFence.cs`, read `FENCES.md`): stakes driven in pairs and bound with withies at three heights, poles laid on a slant between them, split or whole. It's laid **steeper, sparser and thicker than most parishes lay it**, about fifty degrees with poles 20 to 25 cm through: the game's camera squashes what stands to four tenths of its height, and laid as they really are, at thirty degrees and close together, the poles read from up here as a flat band. Each pole is its own grey, which is what draws the slants.

The north fences every field with it, most of its gardens (the rest with field stone, or a paling), its yards, and one side of each lane out of a village, from the last houses out past the fields (`SettlementStage.LineLanes`): where the beasts were driven out to the grazing between fences (a fägata). A lane's fence stops wherever anything else is in the way: a road, a field, a garden, another fence, or anyone standing within 2.6 m of it. A village behind a stockade has no lane fences, as its gates are its own.

## Birches

Birches are what the north is: `Broadleaves.Birch` grows among the pines, and `Broadleaves.WinterBirch` in the snow, its white trunk and every twig of its weeping crown furred with frost (the `twigs.frost` card, pale, with its frost in its palette, and its young wood frosted too). They're a biome's `Mingled` kinds: each takes one of the canopy's places, a fifth of the pinewood's and more than a quarter of the snowfield's, nearly twice that at a wood's edge and out in the open, chosen by where the place is, with the room and the scale the pine or spruce there drew (`VegetationStage.Mingle`). Putting them on the canopy's own lists was tried first, and moved trees in every wood of a world, as the world's trees are one stream and whether a tree is placed changes what's drawn for every one after it: it put a maple where two shrubs had stood by seed 303's landing, and a test of minions settling by their owner failed there.

A village plants them on purpose (`world.Plantings`, planted before the woods by `VegetationStage.PlantOnPurpose`):

- **A vårdträd by each farm**: the tree by a house that nobody may fell, a birch or now and then a linden (`SettlementStage.PlantGuardian`). It's tried beside the house and behind it, each place with the reach of the very tree that would grow there (`PlantSpecies.TryCrown`, as `NatureCatalog.ModelAt` says which tree stands where), so that no bough reaches into a roof. Its crown's ground is kept from anything built later as a site keeps a bed (`PlacementLayer.Garden`, which `PlantOnPurpose` ignores and everything else keeps off).
- **Birches along the lanes**, on the side the fence isn't, every dozen metres or so. A wayside tree keeps only part of its room from the lane (`Planting.Room`, `WaysideRoom`), as its crown leans out over it, and its whole crown from anything with a roof.

## The village

- **The midsummer pole** (midsommarstång, `StructureCatalog.Maypole`): a tall pole wound with two garlands of birch leaves crossing as they climb, an arm with a ring of leaves hanging from each end, flowers over all of it, white, yellow, blue and red. It stands on the green, never in front of the waygate, the shrine, the stash or a merchant from where the camera looks, and the ground under its arms is kept from anything built (`SettlementStage.PlaceMaypole`). It's dressed in fresh leaves, so nothing under snow has one.
- **A well with a sweep** (brunnsvipp, `StructureCatalog.WellSweep`): a curb of boards, a forked post, the long pole pivoting in the fork with a stone lashed to its butt and the rod and bucket hanging over the well. In the square if there's room for it, a stone well if not, and behind about one farm in five.
- **Hay on racks** (hässja, `StructureCatalog.HayRack`): posts down a line with the hay hung over the rails between them, swelling over each rail. In meadows (two fields in five are mown for hay rather than sown, `SiteBuilder.Field`'s `meadow`), by barns, and two to four in the open round the village along the lie of the road (`HangHayAbout`). None under snow.
- **Firewood stacked round** (vedstack, `StructureCatalog.Woodstack`) behind about a third of the houses, under a cap of slabs.
- Gardens: three in five houses fence a garden behind them, against one in three elsewhere.

Everything a village of the north adds draws on a fork of its own, so nothing it draws changes what any other place draws, though what it puts down takes room its own houses might otherwise have had.

## Where it is

| What | Where |
|---|---|
| The style, and which biomes build it | `BuildingStyle.Nordic` (`World/Content/StructureCatalog.cs`), `BiomeDefinition.Builds` of the pinewood and the snowfield |
| The buildings | `Modelling/Procedural/Architecture/NordicStyle.cs` |
| The pole, the sweep, the rack, the stack | `Modelling/Procedural/Architecture/PropGenerator.Nordic.cs` |
| The boards' material | `MaterialCatalog.PaintedBoards`, `MaterialRecipes.PaintedBoards` |
| The fence | `Modelling/Procedural/Fence/RoundpoleFence.cs`, `FenceCatalog.Roundpole` and `Choose` |
| The birches | `Flora/Content/Broadleaves.cs` (`Birch`, `WinterBirch`, `FrostTwigs`), a biome's `Mingled` and `MingledShare`, `VegetationStage.Mingle` |
| The village | `SettlementStage`: `PlaceMaypole`, `PlaceSquare`, `FurnishPlot`, `PlantGuardian`, `YardClutter`, `PlaceFields`, `LineLanes`, `HangHayAbout`, `BuildFarmstead` |

## Testing

- `Dino FloraLab houses --style nordic [--kinds a,b] [--variants N]` draws every building of a style, or any structure by its id (`--kinds maypole,well.sweep,hayrack,woodstack,tower.watch`), from the game's height and low down, front and back, flat-shaded with each face lit by its winding, so a face wound inside out shows dark. It's for shape: colour is the game's.
- `Dino FloraLab fences --kinds roundpole` and `fencecheck`; `Dino FloraLab check`, `variety` and `bend` for the birches.
- `Dino WorldLab check`, and `where --seed N --what maypole` (or `well.sweep`, `hayrack`, `woodstack`, `fence:roundpole`, `birch`) to find them. Seed 1 has a village in the pinewood (Treby, at 212,245), seed 3 another, seed 13 a hamlet in the snow (Halillin, 627,294), seeds 12 and 23 farmsteads.
- In the game: `--seed 1 --site Village`, `--seed 1 --at 335,206` for a lane's fence, `--seed 13 --site Hamlet` for the snow.

## Where it stands

**Nobody has walked through one.** Everything was judged from the lab's pictures and from stills of hidden runs, by machine.

- The gärdesgård is laid for the camera, steeper and sparser than it really is: from low down it looks too steep. Whether it reads as a gärdesgård from the game's own height is for the person who asked for it to say.
- Colours were set against the game's sun and tone mapping in a few stills: Falu red came out right in sun and overcast; the tiles were brought down once from salmon. Nobody has seen them at every hour, or at night, when the windows' light comes up.
- The winter birch reads white from above in the lab, but in the snow's bright, overcast light its crown came out darker than a hoarfrosted birch's; it was lightened once and hasn't been looked at again.
- Worlds whose pinewoods and snowfields used to build in logs now build like this, so a saved land's villages there look different. Sites and roads are where they were, but inside a village of the north its houses, its people and its clutter may stand elsewhere, as its gardens, trees and pole take room the log village didn't, and as a village's ground is in the world's one stream of trees, a world with such a village grows its woods a little differently all over. A world with no village, hamlet or farmstead of the north has every tree where it was, a share of its pinewood's and snowfield's turned birch.
- Nothing about the Reich is in any of it, by design: no black iron, oxblood or brass, and no flag of any country, Sweden's included.
