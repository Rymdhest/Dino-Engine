# Lands

The loop the game is: the Haven, where nothing can reach a hero, and the lands the Saurian Reich holds, which a hero goes to through the great waygate there and either frees or doesn't. Every tenth level is a tyrant's arena. Read this before changing anything about a land's terms, omens, orders, the climb, the arenas, the Haven or the stash.

Status: the loop was built on 2026-11-08, and the arenas, free choice of level, orders as prefixes and suffixes, rerolls and smaller lands later the same day. Every rule here has a check in `Dino BuildLab check` and `Dino NetLab lands`. **Nobody has played any of it.** The numbers are where to start, not where to stop.

## The loop

1. **The Haven.** A hero comes back there after every land, and starts there once they've been through the tutorial, the one land every new hero starts in (`TUTORIAL.md`). It's a world like any other, grown from its own seed (`Lands.HavenSeed`) and a kind of world nothing else is (`WorldArchetype.Haven`, `Peaceful`), so it's the same place every time and never saved. Nothing hostile stands in it, and falling there only knocks a hero down for a moment.
2. **The map table** lists every level the hero can open down its side, deepest first: any they've freed, and the one after (`HeroState.LandLevel`). For the level chosen it shows three lands (`Lands.Offer(hero, level)`), each a different kind of world with its own story, biomes, weather and tyrant, or at every tenth level three arenas, each with a different kind of tyrant. They come from what's saved on the hero alone (`HeroState.Offers` and `Rerolls`), so both ends of a connection see the same three, and they stay the same however often the table is looked at and however many tries they take. A land comes with no omens of its own: what it is to a hero is its layout, biomes, weather, tyrant and what it asks. The table is laid out as what it lets a player do, left to right, with no words the layout already says: the levels; the level's three lands, each a card with a map of it (grown on a worker thread as the slotted orders would make it, with where heroes arrive and where the tyrant waits) and the few things that tell lands apart, among them how many enemies it holds; and the orders, put into empty slots from the pack by clicking them and taken out the same way, with everything they come to listed once beneath, rewards then dangers.
3. **Opening the way** to one of them, with as many orders given for it as the table has slots for (`GameServer.OpenWay`), lights the great waygate with a way grown from that land, its colours, its sky and what drifts in its air (`WAYGATES.md`). Anyone can open it, but only to a level they can open themselves, and only while nobody is in a land: the server holds the Haven and one land side by side (`GameServer`, a `ServerRoom` for each), and the way can't be opened again, or shut, until everyone's back (`GameServer.LandOccupied`). Stepping through takes whoever steps through and nobody else (`StepThrough`), so nobody's carried off before they're ready, and changes nobody's lands. Anyone who joins the game meanwhile waits in the Haven.
4. **The land** asks for two things, in the corner under its name: bring down its tyrant, which waits in the open yard of its keep (`ARENAS.md`), and free the places it names (`LandTerms.Places` of them, three to five, drawn from those held on a fork of their own into `Objective.Places`: freeing any other moves nothing on). The map shows those from the start and nothing else but the waygates found, then the keep once they're free, and the mark on the screen points to the nearest. A land's waygates are dark until a hero comes within `Rulebook.GateWake` of one, which lights it for good (`SiteState.Lit`, saved), and only a lit one can be travelled to (`WorldProgress.CanTravelTo`). What's lit in a gate is a ghost of its own (`Roads.Lit`, placed by `EncounterSystem.LightGate` where the world has the gate), as the gate is the world's, and it's a way home, which shows the Haven (`WAYGATES.md`). Calling the waygates (`GameAction.Roads`, B) opens the same map from anywhere: the server watches the hero stand still and unhurt for `Rulebook.CallRoads` (`Roads.Broken`, which the client also reads for its bar) and then lets them take the roads from where they stand (`ServerPlayer.RoadsOpenAt`). An arena asks only for its tyrant. Done, it's free (`ServerSession.Free`).
5. **Home**, through any waygate in the land once it's free, or before then, giving up on it, which takes the one who goes and nobody else. Once nobody's left in a land it's over, and the way shuts (`GameServer.CloseEmpty`). From a waygate a hero can also go straight to a friend in the same land, who's shown on the travel map (`HeroAction.TravelToHero`).

## The climb

`HeroState.BestLand` is the deepest land freed, and the only thing saved about the climb: `LandLevel`, the deepest level the table opens, is one more, and `CanOpen` is any level from the first to that. A land's level is everything about how hard it is: its enemies are that level, and so is what they drop, up to the hundredth.

| What happens | What it does |
|---|---|
| Free a land while standing in it | `Freed`: each hero climbs from their own `BestLand` by as many levels as the land climbs, if it's at or past where they've got to (`Lands.ClimbTo`). So a hero who follows a friend into a deeper land goes one level on from their own, as the friend does (a friend at 25 and a hero at 15 come out at 26 and 16), orders that climb further take both further, and a friend's lower land costs nobody anything. Nobody passes an arena of their own by freeing a land, but freeing any arena counts for their next. The table shows three other lands for that level from now on (`HeroState.Reroll`) |
| Fall in a land before it's free | Nothing but the try: the same lands are there to try again |
| Leave through a waygate before it's free | The same as falling |
| Pay gold at the table | Three other lands for that level (`HeroAction.Reroll`, `Lands.RerollCost`: 25 gold a level, growing a twenty-fifth faster each level, so level 5 costs 150 and level 60 costs 5,100) |
| Anything in the Haven | Nothing |

**The table says how many enemies a land holds, and that number is never wrong.** Each card grows its land exactly as the server will if it's opened with what's slotted: the same terms (`Lands.TermsFor`, which `GameServer.OpenWay` uses too), the same request (`Lands.RequestFor`) and the same generator, and counts it by the rules the server spawns by (`Gameplay/LandCensus.cs`): `LandCensus.Stands` is who `EncounterSystem` brings in and what `SiteState.HostilesTotal` counts, `IsTyrant` who comes in as a tyrant, and `ArenaWaves` how many an arena's gates let in, which `ArenaSystem` opens by. Until the land has grown the card says it's counting, never a guess. What depends on how a fight goes is only ever said as the most it can come to: an arena's waves, its clutches' hatchlings, and on a land under the Ossuary's Writ whoever gets up again. Anything new that puts an enemy on a land goes through those rules or into the census, and `Dino NetLab lands` (`TheCount`) opens real lands with orders through a client and holds the server to what the table said.

**Bosses can't be passed by.** `Lands.FreedTo` never climbs past a tenth level that hasn't been freed, whatever orders say, so the only way past level 10 is its arena. Every arena freed gives the map table room for one more orders (`Lands.OrderSlots`: one, then two after level 10, three after 20, up to as many as a pack holds).

**There's no cap on the land's level**, but a hero's level ends at 100 (`HeroState.MaxLevel`), and so does anything worn or put in a book (`ItemGenerator.MostItemLevel`): deeper lands go on getting harder, as `Bestiary.HealthAt` and `DamageAt` keep growing, without paying for it in better gear, so farming an arena over and over is as good a way to spend time as climbing. What a kill is worth in experience stops growing at the hundredth level too (`Bestiary.ExperienceAt`), and nothing is counted at the last level, as it would soon be more than a number holds.

There's **one life to a land**. A hero who falls is out of it (`ServerPlayer.Out`): they stay down and their commands do nothing, but **a friend still standing can raise them**: a mark on them says so (`InteractKind.Raise`), and using it while standing still and unhurt for `Rulebook.RaiseTime` beside them, broken as calling the roads is, stands them up with `Rulebook.RaisedShare` of their Health and Mana, counted among those who'll free the land (`ServerRoom.BeginRaising`, `Raise`). A fallen hero can go home whenever they like, but can't step back into a land they fell in, and leaving the server and coming back isn't another life either: `ServerSession.Fallen` remembers them by name, and they arrive fallen. Once everyone still there is out, `SettleIfAllOut` loses the land and sends them home after `LostLinger`, and that's asked when someone falls, arrives or goes their own way, as someone standing leaving can leave only the fallen. Losing a land costs the orders given for it and nothing else.

`TalentPoints` is one, two a level, and one for each level of `BestLand` up to the hundredth (`Lands.MostCountedLand`), so climbing pays and freeing the same land again doesn't.

## A land

A land is a world grown for one visit: a kind of world, a road network joining its places, and the Reich in it.

- **A land is its kind's usual size** (`WorldArchetype.UsualSize`), about 900 m a side for an isle up to about 1,150 m for an archipelago or a lakeland, drawn on a fork of its own so nothing else about a world moves with it. Each kind's was measured to leave about 0.3 km² that anyone can walk, a fifth of what lands had at first, as water, mountains and coasts don't shrink with a land: a kind with more of them is larger. Sprawling orders make one larger, up to the most this one could be, drawn from what its kind can be (`WorldArchetype.Size`, `WorldRequest.Larger`), with room for more packs, more places and their chests, and Swarming makes one larger by half as much as the packs it adds (`Omens.PackRoom`), as a small land has no room for them otherwise. A request can ask to be larger itself, as scripted runs and the labs do (`GameLab.OpenIsle`, the old full-size isle that some suites were laid out against), and the larger of that and the orders' is what it is; it goes over the wire with the rest of the request, and `WorldCache` tells sizes apart. The Haven and an arena are always the size they are. Places of a kind stand closer together on a smaller land (`WorldPlan.Closeness`), and `SiteStage.MakeEnough` looks again, more loosely, for whatever a land is still short of: five held places (`SiteStage.LeastHeld`) and two waygates.
- **Every held place has a chest**, and a group worth stopping for: the first hostile at a place leads it, Magic in the first two levels and Rare from the third (`EncounterSystem`). Shrines are held by raiders, standing stones by cultists, watchtowers are always guarded, and hamlets, farmsteads, fishing hamlets and lumber camps are overrun. A shrine can be kindled at any time, held or not.
- **Villages are hiding.** Each has a refuge (`SettlementStage.PlaceRefuge`): a waygate, a shrine whose blessing lasts two minutes (`Rulebook.ShrineBlessing`), a strongbox for the stash, and a merchant whose stall is always there. Villagers keep their heads down until the land is free.
- **Waygates are scattered about** (`SiteKind.Waygate`, two to six a land counting the landing's and the villages', at least `Spacing` apart, closer on a smaller land). A hero at one can travel to any other they've found, and go home from any of them (`TravelToSite`, `ReturnHome`). The landing has one, and that's where everyone arrives.
- **The tyrant** is what `Chronicle.BossKindOf` names, and the main objective. It always has orders on it (`Loot.OrdersFrom`).

**A land is open country or one way through.** Two in five, drawn from the seed on a fork of their own (`WorldPlan.Path`, `LandPath`), are a chain of clearings joined by narrower passes from where heroes land in the first clearing to the keep in the last. Most wander across the land, turning a little at every clearing and more where the edge or an earlier stretch is in the way, so that they bend, hook and double back as a valley does; about a third are a lopsided U of two legs. A person found a perfect S of three legs too long and too drawn, so there's none, and a way runs 850 to 1,700 m, with nothing to turn off for and nothing to come back for. A gorge between walls of mountain is what a land with walls round it becomes (a coast, a vale, a lakeland, the highlands, a mire or the badlands), and an isle or an archipelago is a strand of land winding through the sea, rocky along its edges, the sea falling away fast off it so that the channels between its legs are out of anyone's depth. Its walls stand everywhere the way doesn't run, so no mountains or terraces of its own cut it, a mire's floor stands a little higher so that no lake does, and no road loops (`RoadStage.AddLoops`). Places stand along it as anywhere, by walking distance from the landing, and the keep takes the last clearing before anything else can (`SiteStage.TryPickNear`). The map table says which a land is (`Lands.LayoutOf`). `WorldValidator.CheckOneWay` fails a linear land whose keep isn't in its last clearing, or whose keep is less of a walk from the landing than 62% of the way, as a gap through a wall or a channel waded across would make it open country again. The walk is always somewhat shorter than the way's own length, as a road cuts the corners of its clearings.

A smaller land has less room for what it must have: when there isn't room on the usual terms, `SiteStage` looks again on easier ones (`SiteDefinition.Relaxed`), from a fork of its own. `WorldValidator` holds all of this: at least five places to free (`CheckLandPlaces`), at least two waygates, a keep, a chest at every held place, a stash in every village, and nothing standing on a waygate's spawn.

## Arenas

Every tenth level (`Lands.IsBossLevel`) the map table shows three arenas instead of lands, three different kinds drawn for the level, each with a different kind of tyrant: bred, raised from bones by the Ossuary, or rotting where it stands (`Chronicle.TyrantKinds`, a seed chosen for each by `Lands.SeedFor`). An arena is a kind of world that's nothing but one place (`WorldArchetype.Arena`), never drawn from a seed. There are six (`ArenaCatalog`), each laid out a little differently every time, and a land's keep has none: its tyrant waits in the open yard in its middle, where it can be led anywhere. What each is, how each is sealed so that nobody leaves it until the tyrant's down, and what each does on its own is in `ARENAS.md`.

**The fight** is the tyrant (`SpawnRole.Boss`), made much stronger in a boss map than in a keep (`Rulebook.ArenaTyrantHealth` and `ArenaTyrantDamage`, through `ServerSession.TyrantMods`, which `TyrantHealth` reads back out for what it's worth). Each time it loses another quarter of its health (`Rulebook.ArenaWaveEvery`) its handlers open some of its entries and let more of its brood in (`ArenaSystem`), as many more as the land's orders put at a place, every one Magic or Rare (`ArenaWaveRareChance`). Whose they are goes by the tyrant (`Warbands.ArenaRecipe`), and whoever comes through belongs to no place (`ServerSession.Arrivals`).

Freeing an arena pays what a land does for its tyrant and its places together, and opens the next level and another orders slot. Orders work there too: Teeming and the Last Garrison let more through each entry, Orders for the Twin Pens let a second tyrant in beside the first (`SpawnRole.Rival`), and the rest do what they always do.

`WorldValidator.CheckArena` holds that a boss map's arena has a tyrant, a waygate, at least two ways for the brood to come in, at least 24 people watching, its floor above the sea, and its seal, and `CheckKeepArena` holds a keep's (`ARENAS.md`).

## Omens

An omen is one line that orders put on a land (`Progression/Omens.cs`). A land holds none of its own. Like an item's affixes, each is a prefix or a suffix (`OmenSide`): **a prefix is what the land gives**, and nothing else, and **a suffix is what it asks**, and pays nothing, so every reward comes with a danger. Each is one number, rolled from tiers that open with the orders' level (at 1, 6, 14, 24 and 38, and some later), and everything about it grows with that number.

- **Prefixes**: Teeming (larger packs), Swarming (more packs), Bountiful (more items), Fortunate (more item rarity), Rich (more gold), Instructive (more experience), Sprawling (a larger land), Gilded (better chests), Pedigreed (more Magic and Rare enemies) and Trophied (the tyrant drops more).
- **Suffixes on enemies**: everything hostile wears them, bosses too (`LandTerms.FoeMods`, into the session's `Bestiary`): Ironhide (health), Bloodthirsty (damage), Quickened (speed), Keen-edged (critical chance), Plated (armour), Piercing (penetration), Spellproof (resistances), Ashen, Rimed, Stormborn and Venomous (elemental damage), Leeching, Thorned, Hexing, Bloodletting, Unyielding, Warhost, Endless Night, Volatile, Rallying and Tyrant's Wrath.
- **Suffixes on heroes**: Bleak, Exposed, Frail, Parched, Sapped and Lagging. These are part of every step a hero takes, so both ends put them on, last, through `HeroBuild.Compile(hero, buffs, land)`, and take them off again at home.

**What they pay** is a `LootBoon` (`LandTerms.Boon`), from prefixes and unique orders alone: each point adds its `Quantity`, `Rarity`, `Gold` and `Experience` shares, a unique's chance gets half of the rarity, and each tier above the first a prefix reached adds a little to how well items roll. Teeming, Swarming and Sprawling pay nothing more a kill, as more is the reward, Gilded pays through chests and Trophied through the tyrant. An empty land's boon is `LootBoon.None`, which leaves every roll as it was. What a land's omens add to an enemy's health is divided back out wherever a find's worth is read from health (`Combat.DropLoot`), or fodder would drop like champions.

**More of them is part of the world.** Teeming, Swarming and Sprawling change who stands on the land and how large it is, so `LandTerms.Shape` makes the `WorldRequest` (`MorePeople`, `MorePacks`, `Larger`) on both ends. The same omen held twice is the two added up, as far as `Most` (one and a half times its best tier).

An omen's id is saved, and its place in `Omens.All` is what's sent, so a new one goes on the end. Names, numbers and tiers can change freely.

## Orders

Sealed orders taken from the Reich (`Progression/Orders.cs`). They're items like any other, of the same four rarities, carried in the pack and given to the map table when the way is opened, as many as it has slots for, which uses them up: the land it opens on holds what they all say (`LandTerms.Give`).

| Rarity | Holds |
|---|---|
| Normal | What its kind always brings |
| Magic | That, a prefix and a suffix |
| Rare | That, two prefixes and two suffixes |
| Unique | What it is, and nothing else |

Each kind brings one prefix, at the middle of the best tier its level reaches: Muster Orders bring Teeming, Patrol Orders Swarming, Supply Orders Rich, Breeding Orders Pedigreed, Hunting Orders Trophied and Survey Orders Sprawling. Orders are named as any item of their rarity is, their first prefix before what they are and their first suffix after, as "Fortunate Patrol Orders of Iron Hides". A line never repeats the kind's own omen, prefixes come before suffixes, and `OrdersMaker.Fix` brings whatever a save or a stranger sends up to these rules from the orders' own seed, and always names them from their lines.

Unique orders are each a trade, what they give and what they cost said apart, and none of them costs a level: there's no way to lose one.

| Orders | Gives | Costs |
|---|---|---|
| The Kommandant's Wager | Freeing the land opens the next two levels at once, never past an arena not yet fought, and 30% more items | The tyrant has 60% more health and damage |
| The Brutmeister's Ledger | Every pack has a Rare leader if it can, and Rare enemies drop twice as much | Rare enemies have one more trait |
| Bred for War | 60% more item rarity | Nothing is Normal |
| Orders for the Twin Pens | A second tyrant at one of the farthest places, or in the arena beside the first, which drops all a tyrant does | Both must fall |
| The Last Garrison | Nothing on the roads, in the wild or at a camp, and every place's chest is a hoard | Every place holds three times as many, and an arena's gates let as many more through |
| The Ossuary's Writ | 50% more items | Everything that falls rises once more as bones, a moment later |

What each does is checked for by name where it happens (`LandTerms.Has`), never as a flag, and a land can hold several, each once. Unique orders' ids are saved.

Orders come from tyrants, always, one in twenty of everything else that drops (`Loot.OrdersShare`), and the last two cells of the Haven merchant's table.

## The Haven

`HavenBuilder` and `HavenTown` lay it out to the metre as a little town on a rise over its harbour (`HAVEN.md`), for the game's camera, which looks north from high in the south, so that everything worth seeing faces south. The great waygate stands at the head of everything on a terrace of dressed stone two metres up, among blossom, at the top of a broad flight with the map table at its foot, and the square below it is built in layers, as a town's is: raised beds walled in field stone, a sakura in a bed in its middle whose wall is a seat, spokes of the gate's stone across its paving, an arch over the lane in from the south, a loggia with tables laid on its east side, and bunting from four masts (`SQUARE.md`). Two of the halls that can be walked into flank the gate, each on a terrace of its own up three steps: the Forge to the west, as it's where a Defender goes most, with the market along the foot of its wall, where the merchant and the beta vendor keep stalls, and the Gaming House to the east (`HALLS.md`). The strongbox is on the square's east side, beside the loggia, and the green is behind that. A lane runs south from the square's arch to the Row, the street along the top of the town's wall, with the Archive at its west end on a podium and homes along its north side, each behind a garden of its own. The Harbour Steps go down through the wall to the training ground, the landing house, a fisher's cottage, a jetty and a boat on the stocks. Two plots are kept empty for halls to come (`HavenBuilder.Plots`), with timber and stone stacked on them. The village reaches 92 m from its middle (`SiteCatalog.Haven`) and the isle little more than it and a ring of woods (`WorldArchetype.Haven.Size`), as a player who finds open ground goes looking for something to fight on it, and there's nothing. There's no shrine, as there's nothing here to be blessed for, and `WorldValidator` fails one.

- **Its trees are grown where they stand** (`TownMason.Grow`), as the square's are in its beds (`SQUARE.md`), at sizes given outright. Its grass is turf (`GroundPaint.Green`, `HAVEN.md`): cutting the land's own grass short was tried once, and from this camera short grass was a field of stubs, so the turf is grown thick as well as short, and only as short as still closes over.
- **The training ground** is a yard of raked sand below the town's wall, at the foot of the Harbour Steps, with three pens along it, divided by low fences: a dummy alone, a group of five and a crowd of fifteen (`SpawnRole.Dummy`, `DummyComponent.Group`), at least ten metres from one another so that what's aimed at one meets no other, which `Dino NetLab lands` holds. A dummy can't die, is as good as new once it's been left alone for `Rulebook.DummyRest`, and its group shows what's been dealt to it a second (`DummyMeter`).
- **The merchant** stocks for the deepest land the hero can open, turns over every time they free one, and sells a couple of orders (`GameServer.TableFor`).
- **The stash** is in the strongbox (`Progression/Stash.cs`): four tabs of 48 cells, shared by every hero on the machine, where nothing moves unless it's moved. It runs on both ends by the same rules, as the pack does: the client changes it at once and the server has the last word (`Stow`, `Unstow` and `Shift` go through `AskGear`, and `ServerPlayer.Heard` answers). It's the player's own, so a friend's server only holds a copy while they're there, and it's their machine that keeps it (`StashStore`), in one file that only one copy of the game can hold at once. One that can't be read is kept aside rather than written over.
- **Its halls** are the Forge, with a crafting bench, the Gaming House, with a gaming table, and the Archive, where everything this player's heroes have done stands on displays that speak when someone walks up to them. The bench and the table are placeholders for now, and every hall is open to everyone until how they're earned is decided (`HALLS.md`).
- **The world cache keeps the Haven** apart from the last land grown (`WorldCache`), as everyone goes back to it between every two lands.
- **Its giants** live outside the village, three of them, browsing the tops of its trees, and are the Haven's own (`LORE.md`). They're only looked at, so they're each client's own and nothing about them is sent, saved or read by the server: `GiantShow` makes them when the Haven is entered, somewhere different each time, and they never set foot in the village or on anyone (`WILDLIFE.md`).
- **The beta vendor** stands below the merchant, there for testing the game and for nothing else, and gives away whatever he has (`Progression/BetaVendor.cs`). Each of his tabs is a line in `BetaVendor.Tabs`. What's out is a pure function of which tab, which roll and what level, so a client shows it with nothing sent, and asks for one by those (`HeroAction.TakeOffer`, through `AskGear`); the server makes the very same thing again, and only in the Haven (`BetaVendor.GivesIn`). His uniques are a lookup rather than a table (`Tooltips/UniqueLookup.cs`). The way to the asset test level, a developer's room with one of everything on show (`LEXICON.md`), was his and is now in the F3 overlay's Developer window, with the developers' cheats (`UI/DeveloperWindow.cs`, `HeroAction.Cheat`, `DeveloperCheat`); it still only goes from the Haven, and not while a way to a land is open, as leaving would shut it for nothing. `BetaVendor.Standing` sends him away when the game isn't being tested any more, and the server stops giving cheats and the window the button with him.

## Where it is

| What | Where |
|---|---|
| A land's terms, the offers, rerolls, slots, the Haven's request | `Progression/Lands.cs` (`LandTerms`, `Lands`) |
| Omens | `Progression/Omens.cs` |
| Orders, their kinds and the unique ones | `Progression/Orders.cs` |
| The climb | `HeroState.BestLand`, `LandLevel`, `CanOpen`, `Offers`, `Rerolls`, `Freed`, `Reroll` |
| The stash | `Progression/Stash.cs`, `HeroPanel` (the strongbox is a shelf beside the pack) |
| Opening the way, rerolling, stepping through, going home, travelling | `GameServer.OpenWay`, `Reroll`, `StepThrough`, `GoHome`, the `TravelToSite` and `ReturnHome` actions |
| One life, judging a land, freeing it | `ServerSession.HeroFell`, `Judge`, `Leave`, `Free`, `SettleIfAllOut` |
| What's in a place, who leads it, the dummies, the people watching an arena | `EncounterSystem`, `VillagerSystem` |
| An arena's gates, who comes through them, what it does on its own | `Server/ArenaSystem.cs`, `ArenaSystem.Hazards.cs` |
| An arena's shape | `World/Generation/ArenaBuilder.cs` and `Arenas/`, `ArenaCatalog` in `WorldArchetype.cs`, and `ARENAS.md` |
| The map table and the leave prompt | `UI/LandPanels.cs` |
| The corner | `Hud.DrawChronicle` |
| The travel map | `MapPanel`, in its travel mode |
| The Haven | `World/Generation/HavenBuilder.cs`, `HavenTown.cs`, `HavenSquare.cs`, `TownMason.cs`, `WorldArchetype.Haven` (`HAVEN.md`) |
| Refuges, held places and their chests | `SettlementStage` |
| A land's size | `WorldArchetype.UsualSize`, `WorldPlan.SizeOf`, `Closeness`, `SiteStage.MakeEnough` |
| A land held only at its places | `PackStage`, through `WorldRequest.Emptied` |
| On the wire | `Protocol.WriteTerms`, `WriteStash`, the hero summary's climb and rerolls, `HeroAction.OpenWay` and `Reroll` |

## Changing it

- **Saved and sent things stay**: omen ids and their order, unique orders' ids, the names of the orders' kinds (an item's `Base`), and `HeroState.Rerolls` by level. Everything else can change freely.
- **`LandTerms.Shape` makes the request**, on both ends, and nothing else does. A save keeps the request as asked and the terms beside it.
- **`Lands.Offer` is the hero alone**: seed, level and how often that level has been rerolled. Anything else in it would show two players different lands.
- **The Haven is never saved**, and a land that's over isn't either: `ServerSession.Capture` writes nothing for them, and a host who's fallen in a land doesn't keep it to go back to.
- **A new omen** goes on the end of `Omens`, is a prefix that gives or a suffix that asks, says what it does at every tier, and is put on what it says it's put on: `check` fails otherwise.
- **A new unique orders** needs a cost a player would notice and never a level lost, a line in the table above, its effect checked for by name where it happens, and a test in `Dino NetLab lands`.
- **A new kind of arena**: read Adding to it in `ARENAS.md`.
- **After any change here** run `Dino BuildLab check` and `lands` (the table of what orders put on a land and what each omen pays), `Dino NetLab lands`, `game`, `loot`, `pack` and `build`, and `Dino WorldLab check`, with `--people 150 --packs 100` for crowded lands, `--packs -100` for a land held only at its places, `--layout linear` for linear lands alone (`--layout open` for the rest), `--archetype pit --count 30` and each other kind for arenas (`ARENAS.md`), and `--seed 1610 --count 1 --archetype haven` for the Haven. A change to how orders look needs `Dino GearLab check` and `variety --kind Orders`.
- **The beta vendor**: a new tab is a line in `BetaVendor.Tabs`. After any change to him run `Dino BuildLab check` and `Dino NetLab vendor` and `pack`.

## Seeing it

`--haven true` starts in the Haven, `--landlevel N` opens every level up to N, `--openway N` opens the way to the Nth land of the deepest level after a second, `--travel N` goes home or out after N frames, `--omens a,b` and `--orders a,b` set up the first land, `--stash file` keeps the stash somewhere, and `--panel table`, `stash`, `travel`, `vendor` or `uniques` opens those. An arena is `--archetype pit --level 10`, or any of the six (`ARENAS.md`). A linear land is any seed whose plan draws one: `Dino WorldLab where --layout linear --seed N --archetype id` finds the first from N on and lists its clearings, for `--seed` and `--archetype` in the game. The Haven's square is about `--at 211,142`, the walk up to its gate `--at 211,155`, its training ground `--at 213,62`, its halls are in `HALLS.md` and the rest of the town in `HAVEN.md`.

## Decided, and open to change

Choices made without asking. Say if any are wrong.

- **"Land" for a map, and "Land Level" for a map's level**, "the Haven" for the hub, "the map table" for the selection screen, "the way" for the portal, and "arena" for where a tyrant is fought every tenth level.
- **Two lands in five are one way through** (`WorldPlan.LinearShare`), and the rest open country.
- **"Orders" for map items**, and **"omens" for their lines**, a prefix a reward and a suffix a danger.
- **The arena's numbers**: the tyrant with 200% more health and 35% more damage in a boss map than in a keep, entries every quarter of its health, two then two then all of them, two through each, a third of them Rare, and seven seconds open.
- **Who watches**: the land's people, made to by the Reich, who want the Defender to win and say so quietly.
- **Lands of about 0.3 km² to walk**, a fifth of the first ones, after two rounds of "still too big".
- **Rerolling** at 25 gold a level, growing as the level does, and freeing a level showing other lands for it next time.
- **Orders slots**: one, and one more for each arena freed, up to a pack's worth.
- **Losing a land costs only the try and the orders**, and the same three lands wait for another go.
- **Talent points from lands stop at the hundredth**, as a hero's level does.
- **Three to five places to free**, three in the first two levels, as a land is small and that's where someone learns what a land is. Two waygates are enough on a land this size.
- **Leaving a friend's game halfway costs nothing.**
- **Each hero steps through and goes home for themselves**, and the way only opens while nobody's in a land, as the server holds the Haven and one land at a time.
- **Raising a fallen friend** takes four seconds standing still beside them, and they get up with half their Health and Mana. What falling cost them in gold stays lost.
- **Enemies have 100% more Health for each hero in the game past the first** (`Lands.PartyHealth`, `Rulebook.PartyHealth`), listed with the land's omens on the map table and in the land's corner, and changing at once as friends join or leave: every enemy is rebuilt at the same share of its Health (`ServerSession.PartyHealth`, `Rebuild`), and what it's worth divides it back out (`Combat.DropLoot`).
- **Gold is shared evenly among everyone in the game**, and the host chooses whether finds are free for all or allocated in turn (`BUILDS.md`).
- **The stash**: four tabs of 48, shared by every hero on one machine, and not shared with friends.
- **Twelve things on a table, two of them orders in the Haven**, and one in twenty drops orders.
- **The training ground**: one, five and fifteen dummies, four seconds to reset.

## Where it stands

**Nobody has played the loop, an arena, the level list, rerolling or orders as they are now, or stood in the Haven but the bots and the stills.** Ask how it feels first: whether each kind of arena reads as what it is from the game's camera, whether its tyrant is hard enough, whether the waves come too soon or too late, whether choosing between three lands is a real choice, whether lands are now the right size, whether a linear land feels like a road to follow or a corridor, whether two in five is the right share of them, and whether rerolling costs what it should.

Not built:

- **Caves and mines.** Since 2026-11-14 a land asks to be freed of places with a story of their own as well, wrecks among them (`PLACES.md`), but nothing goes underground.
- **Anything unlocked by an arena but an orders slot.**
- **Orders being crafted or changed.** They're found and given as they are.
- **A count of time in a land**, for anyone who wants to race.
