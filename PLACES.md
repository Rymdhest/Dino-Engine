# Places with a story of their own

Status: built on 2026-11-14 to the churchyard's standard (`CHURCHYARD.md`). Every place below has been grown in 40 to 100 lands by `Dino WorldLab places` and `check`, freed on a running server with a client by `Dino NetLab places`, and seen in one or two stills from the game's own camera. **Nobody has walked through any of them, fought in one or watched a beast leave a cage.** Ask how they read first. See "Where it stands" at the end.

## The standard

The churchyard set it: a place that tells something, laid out as such places really are, and never the same twice. Each place here holds to the same rules.

- **It says whose it is and what happened there.** The Reich's places are black iron, oxblood and brass and nothing else of a land's is (`LORE.md`); the land's are its own way of building (`WorldData.StyleOf`) and its own colours; the Ossuary's are bones and its cold green light; what got out of the pens is wild-coloured and unharnessed.
- **It stands where it would.** A wreck lies on a beach with its stern in the water, a fishing village faces the sea and runs its jetty out to where a boat floats, a logging camp stands at the edge of woods worth felling (never bamboo or palms, `PlaceStory.IsTimber`), a quarry is cut into a hillside that climbs north, the way the camera looks, a nest is deep in the woods, a snow castle only stands in snow. `SiteDefinition` holds what a kind needs of the ground (`Climate`, `LeastWoods`, `WoodsNearby`, `Fells`, `LeastRise`, `MostAboveSea`), and `SiteStage.PlaceStories` puts down only kinds the land has ground for.
- **It's grown, never stored.** Every piece is a `PlaceSpec` id, `place:<piece>:<seed hex>:<numbers>` with no dots or hash, which `PlaceForge.Make` grows on worker threads (`Modelling/Procedural/Architecture/Places/`, headless). What a piece needs to know of its land comes in its id: the bark of the wood round a logging camp, the colour of the land's stone in a quarry (`PlaceMaterials.StoneOf`, from the `Geology` the cliffs are drawn in), the waygates' stone's shade in ruins.
- **It's laid out, not scattered.** A builder per kind (`World/Generation/Places/`, on `PlaceBuilder`) works out the shape first and then what goes in it, with room to fight, and everything tried and refused if the ground or what's there won't take it (`Tries`, `Put`, `Ground`, `Clear`). What it can't do without has a fallback that still tells the story: a dig the ground won't take a pit for is a shallow scrape with its beast coming out of it, a wreck with no room for her whole is her bow alone in the shallows, and a trader always has somewhere to stand (`StandNear`).
- **It's seen from 33 m up at 67°, looking north.** Tall things stand on a place's north side, a hatching shed is open towards the camera, a quarry's face faces it, ruins' back wall stands highest and their front lowest, and a paddock's fence is low enough that what's in it shows.
- **It's freed, and something comes of it.** Every place a land asks to be freed has a chest of what its holders took. Freeing a prison or a farm swings every door and gate open and its beasts walk out into the wild (`CaptiveSystem`); the people of a logging camp, a quarry, a fishing village, a farm or a trading post come back to it (`SpawnRole.Returning`); and a trading post's trader trades again (`SpawnRole.Trader`, a real merchant).
- **It's on the map**, in the chart's own hand: its buildings on their footprints, the Reich's in its dark stone, and its own marks (`PlacePlan.Marks`, drawn by `PlaceGlyph`): a wreck's hull, a pit or a quarry's floor hatched in from its edge, a nest a ring of twigs with its clutch, a web a few threads.

## The places

| Place | Whose | What it is | What freeing it does |
|---|---|---|---|
| Wreck (`SiteKind.Shipwreck`) | The Reich's, or the land's | One of the Reich's iron ships (two in three) or a land ship, broken in two on a beach, driven ashore or lying broadside, her stern in the water, her cargo strewn up the sand: a Reich ship's burst cages and what got out of them hunting the beach, a land ship's wreckers camped by her | Its chest |
| Prison (`SiteKind.Prison`) | The Reich's | A palisade round rows of iron cages, a grazer caught in the wild in each, a breaking yard with one chained to a post, the collar forge, harness racks, the wranglers' tents and tower | Every cage door swings open and every beast walks out of the gate and away |
| Laboratory (`SiteKind.Laboratory`) | The Reich's (the Amber Institute) | The Institute's brick hall with its amber windows, its vats of amber, its hatchery, a workshop of specimens in jars, and a pen of its failures the Ossuary will take | Its chest |
| Excavation (`SiteKind.Excavation`) | The Ossuary's | A stepped pit, shored with boards, a great beast's bones half freed on its floor under the grid, the hoist over it, the sieve, the jackets and crates, the drawing table under its oxblood awning; the dead it raised walk there | Its chest |
| Nesting ground (`SiteKind.Nest`) | Escaped broods' | Raptors' ring of mounds round a trampled clearing, a ravager's great scrape ringed with its kills and the trees it snapped coming in, a wader's reed nests by the water, or a weaver's lair of webs and silk-wound trunks with egg sacs and what it caught hung up (`PlaceStory.NestOf`) | Its chest |
| Logging camp (`SiteKind.LumberCamp`) | The Reich's, with the land's hands | A clear-cut at the edge of the woods: stumps and felled trunks in the wood's own bark, the bunkhouse, the sawpit, boards stacked to season, the tools and grindstone, logs decked by the road, the skidder's high wheels and a charcoal clamp smoking | The woodcutters come back |
| Quarry (`SiteKind.Quarry`) | The Reich's, with the land's hands | A face cut into the hill in benches of the land's own stone, blocks standing proud of it and rubble down its ends, the topsoil stripped back from its lip; on its floor of chippings the treadwheel crane, blocks daubed in oxblood for the Reich, one on its sledge, the split line, the bankers, stacks waiting to be carted | The quarrymen come back |
| Fishing village (`SiteKind.FishingHamlet`) | The land's, held | Its jetty run out to where a boat floats, boats moored and drawn up with the capstan above them, a boat on the stocks, nets and fish hung to dry, the smokehouse smoking, pots, and the houses in the land's way of building back from the water; its catch goes to the Reich's broods | The fishers come back |
| Rearing farm (`SiteKind.Farm`) | The Reich's, on a land's farm | Paddocks in the farm's own fencing with the Reich's chain on their gates, a few of one kind of grazer wandering each, hay racks, the cistern, the barn, and the hatching shed where the Institute's eggs are kept warm over braziers | Every gate swings open and the grazers go; the farmers come back |
| Trading post (`SiteKind.Tradepost`) | The turncoats' | A stockade on the highway round the post's house and store, the beam balance, bales waiting for the Reich's wagons, the turncoats' tally desk with its sealed papers, the hitching rail, and the notice board outside with the Reich's notices nailed over the land's | Its trader comes back and trades |
| Ruins (`SiteKind.Ruins`) | Nobody's now | The waygates' builders' hall in their own stone, its back wall standing high and its front broken low, a colonnade standing and fallen, its gateway's arch, steps up to nothing, rune stones nobody can read and what's left of its figures, the land growing back over it; the dead walk there or the Reich's diggers | Its chest |
| Snow castle (`SiteKind.SnowCastle`) | The frost's cult | What the north's children build every winter, older now than this winter: packed-snow walls with merlons, bucket-tiered towers with pennants in the land's colours, an arch, snowmen, snowball lanterns lit round the cult's altar of ice and its cold violet flame, and the children's sledges left lying | Its chest |

A place's name and its holders read its story number the same way (`WorldSite.Story`, `PlaceStory`), so they can't disagree: a wreck named for a Reich ship is one, and a weaver's lair is held by weavers. Names are in `SiteStage.NameFor` and `PlaceNames` (the Reich's ships, prisons and farms in its German), what the chronicle says of each in `Chronicle.Describe`, and who holds each in `Warbands.PlaceRecipe`.

## How a place is made

- **Where:** `SiteStage.PlaceStories` runs once the ruins are placed, drawing from `SiteCatalog.Stories` by `StoryWeight` among the kinds the land doesn't have and has ground for, on a fork of its own. Quarries and digs are weighted down, as they need least of the ground. Roads stop short of what they'd cut through: at a walled place's gate or a quarry's mouth (0.92 of its radius), and short of a dig, ruins, a snow castle and a nesting ground (0.85); passing roads go round a quarry (`RoadStage.KeepCore`).
- **The ground:** a place that reshapes the land does it with `GroundPad`s, applied in order with each over the one before (`TerrainBakeStage.ApplyPads`). A quarry lays the ground behind its face to its lip, the floor to its foot, and under its benches a ramp half a metre below the foot of every riser, so that the drawn ground, 0.15 m over where people stand, never comes up through a tread, and narrows its face until neither it nor its rubble is on a road. A dig steps down to a bench and a floor with a ramp up. A quarry's floor is a ground paint of its own, `GroundPaint.Chippings`, the land's stone broken small with the bedrock bare through it, which nothing grows through.
- **What stands:** `PlaceBuilder.Put` puts a piece down only if the ground and what's there let it (including trees), solid to its height, so that a blow is stopped by anything over 1 m. A wall's stretches stop short of their ends so they never touch, with no gap anyone fits through.
- **What's held:** a caged or penned beast is a `PlaceCage` (`PlacePlan.Cages`), its door model, hinge and swing, where it stands, the way out and where it goes; a paddock is a `PlacePaddock`. `EncounterSystem` stands them and hangs the doors while the place is held, and `CaptiveSystem` swings the doors and walks the beasts out once it's freed, a penned one wandering its paddock until then. They're not anyone's to fight and nothing in the rules reads them.
- **New materials:** riveted plate, cut stone, netting and packed snow (`MaterialCatalog`, `MaterialRecipes.Places.cs`).

## Checks

- `dotnet "Dino WorldLab/bin/Release/net8.0/Dino WorldLab.dll" places --seed 1 --count 40 [--kind Quarry] [--archetype highlands] [--pieces true]` grows every piece of every place in a run of lands and fails a piece that builds nothing, has a point that isn't a number, reaches over 48 m or doesn't write back as its id, a place over its budget (a big piece 60,000 triangles, any other 14,000, 70 models, 40 lights), a held place with no chest, and a place missing what makes it one (`PlaceCheck.Signatures`: a wreck's hull, a prison's three cages, a nest's mounds, webs or kills, and so on). `--pieces true` lists what each holds. Snow castles need snowy lands (`--archetype highlands`).
- `Dino WorldLab check`, whose validator holds that nothing solid overlaps and everyone but what's caged or penned can be reached on foot.
- `dotnet "Dino NetLab/bin/Release/net8.0/Dino NetLab.dll" places` frees a prison, a rearing farm and a trading post on a running server with a client on a bad line: the beasts stand there and the client sees them, the doors hang shut, penned ones wander and never leave their paddocks, and once freed every door opens, every beast walks out and is gone, the people come back and the trader trades. It caught the captive system never moving anything, which nothing else had.
- `Dino WorldLab chart` for how a land's places look on the map.

## Where it stands

**Nobody has walked through, fought in or freed any of these but the bots, the labs and a still or two each from the game's camera.** Ask how they read first: whether each says at a glance what it is and whose, whether the quarry's face reads as a quarry and not a wall, whether a weaver's lair reads in its gloom, whether the beasts leaving a prison or a farm is a moment worth having, whether places turn up too often or too seldom, and whether the farm's grazers and the prison's captives are too small to see.

Not built:

- **Caves and mines.** A quarry is the only place cut into the ground.
- **A trading post's own goods.** Its trader trades as a village's merchant does.
- **Freed beasts staying anywhere.** They walk off and are gone; nothing of them is met again.
- **A wreck's sea.** Her stern stands in the water, and nothing moves her.
- **Night in any of them but by the land's own hours.** Only a churchyard is always at night.

## Decided, and open to change

Choices made without asking. Say if any are wrong.

- **The name**, places with a story of their own, or places, which is what the game already called them.
- **A prison's captives and a farm's grazers are herbivores caught in the wild**, hornbacks, clubtails, spikebacks, domeheads and striders in the wild's colours, so that freeing them frees animals and not the Reich's weapons.
- **A farm's paddocks are the farm's own fences**, low, with the Reich's black chain on their gates, as the Reich's tall pen palisade hid what was in them from the camera.
- **A snow castle is the north's children's, kept by the frost's cult**, its flame a pale violet that's nobody else's, as green is the Ossuary's and blue a ghost's.
- **Ruins are the waygates' builders'**, in a land gate's stone, so the two read as one people's work.
- **A trading post is the turncoats'**, the outlaws who sell to the Reich, and its trader comes back once they're gone.
- **A logging camp and a quarry are worked by the land's people for the Reich**, who come back to them freed.
