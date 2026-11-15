# The asset lexicon

A developer's room, and nothing else: a flat isle where one of everything the game has stands on its own plot with its name on a plaque, every creature and person, every plant and stone, every kind of place a land can have, laid out in labelled rows, so that anything can be found, looked at from the game's own camera and named when something about it needs work. It's reached only from the beta vendor in the Haven ("Go to the asset test level"), and it isn't part of the game: it's built to be taken out whole (see the end).

Status: built on 2026-11-15. `Dino WorldLab lexicon` grows it with no window and holds it to everything below, and `Dino NetLab lexicon` plays it through a client on a line; it's been entered in the game by scripted runs and looked at in their pictures, from the landing, the index and every section, and a dozen of its places. **Nobody has walked it with real input yet**: say what's hard to read or find.

## Getting there and back

- **In**: the beta vendor's panel has a gold button under his controls, "Go to the asset test level". It takes everyone in the world there, as stepping through the way does (`LexiconAsk.Visit`, through `HeroAction.Lexicon`), and only from the Haven. While a way to a land is open it refuses and says why, as leaving the Haven would shut the way and lose the orders given for it.
- **Out**: "Back to the Haven" in the corner box or the index (`LexiconAsk.Leave`), the landing's waygate, or calling the waygates from anywhere (B), as in any land.
- **About**: the index (the corner box's "Index" button) lists everything, section by section and family by family, searched by anything about each (its name, its ids, its look, its family). Choosing a line puts the hero in front of it (`LexiconAsk.GoTo`, the server's `Carry`): due south of a thing, near enough to have it in the upper middle of the screen, a tall tree nearer than a fern, a step south of its plaque rather than on it, and never in what's solid of it; for a place, a little south of the middle of what it's built, as most of it is in view from there, which only the grown world knows (a fishing village is laid out along its shore). The server stands them on the open ground nearest the point, a step clear of anything built or set down (`LexiconWorld.StandFor`). The corner box says the section and row being looked at, and the place being stood in. Its waygates, the landing's, the Waygate exhibit's and the village's, can be travelled between once lit, as in a land, and the map knows the whole room from the first step.

Nothing in it is to be fought, nothing done in it counts, and it's never saved: a hero who quits in it comes back in the Haven. Falling there is as falling in the Haven.

## How it's laid out

The camera looks north, so the room is walked from the landing at its south end northwards. To the west (left on the screen) is the column of small things, to the east the column of places, with an avenue between. Each column is sections one above another, nearest first; each section has a sign at its south-west corner, its rows run west to east (left to right) and go north from there. A family's plots stand side by side in one row with its sign at the west end, and every plaque is along the paved walk at the front of its row, under what it names. A family too long for a row goes on in the next and its sign says "continued". The isle is as small as the two columns let it be: `LexiconLayout.Compose` tries every pair of widths and keeps the one that makes the smallest square, then the one with the shortest walk north.

| Section | What's in it | From |
|---|---|---|
| Dinosaurs | A row for every species in `SaurianCatalog`, by how it's built (theropods, horned, sauropods, arachnids), and in every row the same five plots in the same order: in the Reich's harness, wild, raised as bones, rotting, a ghost. Each holds whatever the game draws that way: the enemies that look so (all their names on one plaque), or what's only caged at a prison, or the Haven's giants, or nothing, and an empty plot says what isn't drawn yet. A species that's `Free` has only its wild plot | `AssetLexicon/CreatureShelf.cs` |
| Creatures | Everything else in `Bestiary.Everyone`, by what it's drawn as, so what shares a look stands together: the Reich's soldiers, turncoats and outlaws, cultists and casters, the dead, the land's animals, and what heroes raise and set down. A look it doesn't know goes in "Unsorted", which wants a line in `CreatureShelf.FamilyOf` | the same |
| People | The six villager looks, which every villager, merchant, elder, keeper, watcher and the beta vendor are drawn from; the hero in each player's colours; the training dummy | the same |
| Trees, Shrubs, Ground cover, Crops, Deadwood | A section for every kind of plant in `FloraCatalog`, and in each a family for every catalogue they're written in (`[FloraCollection]`: Broadleaves, Conifers, Northlands and the rest), sorted by id. Each is the first plant of its kind the room grows (`LexiconAssets.ModelOf`), a little over its middle size, on a tended bed sized to its crown. What lives in the water is left out, as there's no water to stand it in | `Dino Engine/World/Lexicon/Shelves/PlantShelf.cs` |
| Stones | Every form a stone can take (`RockForm`), pebble to bluff, in granite, the room's own rock: a stone is shaded by its world's rock, so one of another kind would be drawn as this | `Dino Engine/World/Lexicon/Shelves/StoneShelf.cs` |
| Places | Every kind in `SiteCatalog.All` and `Stories` but the landing, which is where the room is come into: settlements, what every land has, and places with a story. Each is laid out by its own builder as in a land, at the least radius its kind is ever drawn, its way in to the south, but only on its own plot: while the places are built nothing else can be built on (`LexiconSiteStage`), so what a builder can't fit there it leaves out, as in a crowded land, and a village has fewer fields than it would like. Nobody holds a place: whoever would is taken out, and what it shows of itself stays, villagers, a merchant, caged beasts, a chest, an altar, a gate | `Dino Engine/World/Lexicon/Shelves/PlaceShelf.cs` |

What a place needs of the land round it to be what it is comes from what its `SiteDefinition` asks of the ground when a land chooses where it goes (`LexiconDioramas.Of`), and is laid out on its plot: a place with a `MaxInland` or `MostAboveSea` stands on the north shore, on the beach as far in as in a land (the wreck and the fishing village, with its jetties over the water); one with a `LeastRise` has a hillside behind it (the quarry, cut into it); one with a `Climate` stands in the first biome that has it (the snow castle, in the snow); one with `LeastWoods` stands at the edge of a pinewood if it `Fells` (the logging camp) and deep in one if not (the nesting ground). So a new kind of place gets what it needs with nothing added.

Every creature stands three quarters on to the camera, facing its left, as `--parade` stands them, and is only a body: no brain, health or place it belongs to, so nothing can hit it, push it or count it, and it never moves.

The floor is level everywhere a thing stands and every plaque is read (the whole plateau inside the beach is one set of pads, but where a hill is), the lawn is short and has nothing wild on it, the light is a clear morning from behind the camera's left with no weather, and nothing grows wild.

## Where it is

| What | Where |
|---|---|
| What an exhibit, a family and a section are, the shelves, finding them | `Dino Engine/World/Lexicon/Exhibits.cs` (`LexiconCatalog`, `ILexiconShelf`, `[LexiconShelf]`) |
| Where everything stands, the dioramas | `Dino Engine/World/Lexicon/LexiconLayout.cs` (`LexiconLayout.Current`, `LexiconPlan`, `LexiconDioramas`) |
| The room as a kind of world, its lawn, its ground, where whoever the index sends is stood | `Dino Engine/World/Lexicon/LexiconWorld.cs` (`LexiconWorld`, `LexiconGround`, `LexiconWorld.StandFor`) |
| How it's grown | `Dino Engine/World/Lexicon/LexiconStages.cs`: its own land, sites, exhibits and plants stages, and a land's own settlement, terrain bake, water and finalize stages between them. The sites stage marks what can be walked to again once the ground under each place is eased to its height, as the room's beach is so gentle that easing a place by the sea moves the water's edge out by tens of metres, then cuts what can be built on back to the places' plots for the settlement stage, and the exhibits stage puts it back |
| The engine's shelves | `Dino Engine/World/Lexicon/Shelves/` |
| The game's shelf | `Dino Defenders/AssetLexicon/CreatureShelf.cs` |
| Standing the creatures, going in, out and about | `Dino Defenders/AssetLexicon/LexiconServer.cs` |
| The plaques, signs, corner box and index | `Dino Defenders/AssetLexicon/LexiconUi.cs` |
| The check | `Dino WorldLab/LexiconCheck.cs` |

## Adding to it

Most of the time nothing: anything new in a catalogue the room reads turns up by itself, in its place. A new dinosaur species gets its row, a new enemy with a dinosaur's look goes in its species' plot for that look, a new plant goes in its catalogue's family, a new kind of place goes in the places with whatever its definition asks of the ground.

- **A new kind of thing** to show is a shelf: a class marked `[LexiconShelf]` with a parameterless constructor that implements `ILexiconShelf` and says its sections, in the engine or in the game. It's found by reflection with nothing to register. A section's `Order` is where it stands on the walk (dinosaurs 10, creatures 20, people 30, plants 40 and on, stones 50, places 60), `Large` puts it in the column of places, and `Ground` says what its plots are laid on. Each exhibit says its plot's size, so measure what stands on it from what it's grown from, never by growing it, and how tall it is and how near its middle someone can stand (`Height`, `Clear`), which is where the index sends whoever wants to see it.
- **What an exhibit can be** is `LexiconKind`: a creature (stood by the server), an asset (a plant or a stone, planted by the world), a place (laid out by its builder) or missing. Anything else, a structure from `StructureCatalog` say, is a new kind and a line in `LexiconExhibitStage` or `LexiconPlantStage` to put it down.
- **Every exhibit's key** has to be unique across the room and stay the same build to build; a second with the same key is told apart and logged.
- Read what a shelf says from the game as it is. Nothing written into a shelf by hand should be a list of what exists, or the room stops being a picture of the game.

## Taking it out

The room is two folders and the lines that reach it, each marked with `Lexicon (developer tool, LEXICON.md)`:

1. Delete `Dino Engine/World/Lexicon/`, `Dino Defenders/AssetLexicon/`, `Dino WorldLab/LexiconCheck.cs` and `Dino NetLab/LexiconTests.cs`, and this file.
2. Search the solution for `Lexicon (developer tool` and take out each marked line or block: `WorldGenerator.Generate` (its stages), `WorldArchetype` (the `Lexicon` flag and `Enclosed`), `ArchetypeCatalog.Get`, `HeroAction.Lexicon` in `Protocol.cs`, `GameClient.AskLexicon`, `GameServer` (the action, `Travel`, standing the creatures, falling), `ServerSession` (`Judge`, `HeroFell`, `Capture`), `WorldProgress.IsLexicon` and its chronicle, `GameSession` (the map, `IsGoal`, guidance), `Hud.DrawChronicle`, `GameUi` (the layer and the index), `ChroniclePanel.Open`, the beta vendor's button and `GoToLexicon`, and `ArpgGame` (loading, arrival, the giants, `--panel lexicon`). Then remove `IsLexicon` wherever the compiler says it's still used, and `case "lexicon"` (and its line in the summary) in `Dino WorldLab/Program.cs` and `Dino NetLab/Program.cs`, and the line about it in `AGENTS.md` and in the beta vendor's part of `LANDS.md`.
3. Four things it opened up are harmless to leave: `VegetationStage.PlantOnPurpose` is internal rather than private, `CharacterFactory.VillagerLooks` and `HeroLooks` are internal, `Chronicle.Prologue` has an internal setter, and `PlaceCheck.Signatures` is internal.
4. Build the solution and run `Dino WorldLab check` and `Dino NetLab vendor`.

Nothing saved ever names the room, so no save needs anything done to it.

## Seeing it

`Dino WorldLab lexicon [--out file.png] [--size pixels]` grows the room, prints every section and every place with how many pieces it has and what it stands in, fails a plot on another, a place not built or without what makes it what it is (`PlaceCheck.Signatures`, the same as any land's), anything a place builds outside its plot, anything standing on a walk, a plant not planted, the floor not level where something stands or a plaque is read, any pond, any haunt, anything hostile, and a viewpoint nobody can walk to, that's in something solid or that's on another exhibit's plot, and draws its plan as the game's camera sees it. It says where whoever's sent to each place is stood, which `--find` can't, as it grows nothing. It loads the game's own assembly from beside it when the game's been built (or from `--game dll`), so the creatures and people are laid out and checked as in the game; it says so when it couldn't. `--find words` lists every exhibit with those words about it and where to stand to see it.

`Dino NetLab lexicon` plays it through a real client on a line: in by the beta vendor's ask from the Haven and from nowhere else, and not while a way is open; both ends growing the same room; every creature stood on its plot, and sent to the client that comes near; nothing hostile, held or saved; the index's carrying, only in the room and never off it; and home again with nothing counted.

In the game: `"Dino Defenders.exe" --archetype lexicon --at x,z` starts in the room at a point (`--find` says where, and the log where each section starts), `--site Kind` at one of its places, and `--panel lexicon` opens the index. From the Haven, `--haven true --panel vendor` opens the beta vendor with the button.

## Decided, and open to change

Choices made without asking. Say if any are wrong.

- **"The Asset Lexicon"** for the room, and "Go to the asset test level" on the button, as it was asked for.
- **Dinosaurs as a grid of five ways**, an empty plot for each way a species isn't drawn, so what's missing shows. Free species show only their wild plot.
- **One of each**: the first variant of a creature's looks (`raptor0`), the first plant of a kind the room grows, one place of each kind at its smallest. Variants, building styles and a place's stories (a Reich wreck or the land's, which cult holds a ring of stones) aren't each shown.
- **Families by look** for everything that isn't a dinosaur, as it's what something looks like that the room is for.
- **Places at their least radius**, which is a real size for each, to keep the room small, and built only on their plots, so a village or a hamlet has fewer fields round it than in a land.
- **Where the index sends the hero**: in front of a thing, as near as fits it in the middle of the screen with the game's camera (30 m up, 67°, 60° of view, which `LexiconLayout` works from), and into a place rather than to its plaque, which is at the front of a big plot and too far from what's in it. The Haven's giant is taller than the screen from anywhere it can be stood in front of.
- **The places show what they show of themselves**: villagers, a merchant and an elder in the village, caged beasts in the prison and the farm, chests that open and an altar that blesses, as in a land with its holders gone.
- **Leaves and moss lie on top of the ground**: laid 2 cm into it, as a land's ground cover lays them (`WorldGroundCover`), they're under the ground as it's drawn and don't show at all, so the room lays them 20 cm up (`LexiconPlantStage.LyingLift`). It may be that litter and moss never show in a land's woods either: worth a look.
- **No water life, no birds or rabbits, no grass of each biome**: the room has no water, wildlife is each client's own and never stands still, and a patch of every biome would overrun the terrain's palette of materials.

## Where it stands

**Nobody has walked through it with real input.** Ask whether the plaques read well from the game's camera, whether anything's hard to find, and what else should be in it. Not built: the sea's life on the shore, wildlife, every biome's ground and grass, and a house of each building style, any of which a shelf would add.
