# Lands

The loop the game is: the Haven, where nothing can reach a hero, and the lands the Saurian Reich holds, which a hero goes to through the great waygate there and either frees or doesn't. Every tenth level is a tyrant's arena. Read this before changing anything about a land's terms, omens, orders, the climb, the arenas, the Haven or the stash.

Status: the loop was built on 2026-11-08, and the arenas, free choice of level, orders as prefixes and suffixes, rerolls and smaller lands later the same day. Every rule here has a check in `Dino BuildLab check` and `Dino NetLab lands`. **Nobody has played any of it.** The numbers are where to start, not where to stop.

## The loop

1. **The Haven.** A hero starts there, and comes back there after every land. It's a world like any other, grown from its own seed (`Lands.HavenSeed`) and a kind of world nothing else is (`WorldArchetype.Haven`, `Peaceful`), so it's the same place every time and never saved. Nothing hostile stands in it, and falling there only knocks a hero down for a moment.
2. **The map table** lists every level the hero can open down its side, deepest first: any they've freed, and the one after (`HeroState.LandLevel`). For the level chosen it shows three lands (`Lands.Offer(hero, level)`), each a different kind of world with its own story, biomes, weather and tyrant, or at every tenth level three arenas, each with a different kind of tyrant. They come from what's saved on the hero alone (`HeroState.Offers` and `Rerolls`), so both ends of a connection see the same three, and they stay the same however often the table is looked at and however many tries they take. A land comes with no omens of its own: what it is to a hero is its layout, biomes, weather, tyrant and what it asks.
3. **Opening the way** to one of them, with as many orders given for it as the table has slots for (`GameServer.OpenWay`), lights the great waygate in that land's colour. Stepping through takes everyone in the Haven there (`StepThrough`), and changes nobody's lands.
4. **The land** asks for two things, in the corner under its name: bring down its tyrant at the keep, and free a number of its places (`LandTerms.Places`, three to five). An arena asks only for its tyrant. Done, it's free (`ServerSession.Free`).
5. **Home**, through any waygate in the land once it's free, or before then, giving up on it. If everyone falls, they go home a few seconds later anyway.

## The climb

`HeroState.BestLand` is the deepest land freed, and the only thing saved about the climb: `LandLevel`, the deepest level the table opens, is one more, and `CanOpen` is any level from the first to that. A land's level is everything about how hard it is: its enemies are that level, and so is what they drop, up to the hundredth.

| What happens | What it does |
|---|---|
| Free a land while standing in it | `Freed`: `BestLand` goes to at least `Lands.FreedTo` of it, so freeing a friend's lower land costs nobody anything and a friend's deeper one takes a hero on from there. The table shows three other lands for that level from now on (`HeroState.Reroll`) |
| Fall in a land before it's free | Nothing but the try: the same lands are there to try again |
| Leave through a waygate before it's free | The same as falling |
| Pay gold at the table | Three other lands for that level (`HeroAction.Reroll`, `Lands.RerollCost`: 25 gold a level, growing a twenty-fifth faster each level, so level 5 costs 150 and level 60 costs 5,100) |
| Anything in the Haven | Nothing |

**Bosses can't be passed by.** `Lands.FreedTo` never climbs past a tenth level that hasn't been freed, whatever orders say, so the only way past level 10 is its arena. Every arena freed gives the map table room for one more orders (`Lands.OrderSlots`: one, then two after level 10, three after 20, up to as many as a pack holds).

**There's no cap on the land's level**, but a hero's level ends at 100 (`HeroState.MaxLevel`), and so does anything worn or put in a book (`ItemGenerator.MostItemLevel`): deeper lands go on getting harder, as `Bestiary.HealthAt` and `DamageAt` keep growing, without paying for it in better gear, so farming an arena over and over is as good a way to spend time as climbing. What a kill is worth in experience stops growing at the hundredth level too (`Bestiary.ExperienceAt`), and nothing is counted at the last level, as it would soon be more than a number holds.

There's **one life to a land**. A hero who falls is out of it (`ServerPlayer.Out`): they stay down, their commands do nothing, and they watch until everyone goes home. Leaving the server and coming back isn't another life: `ServerSession.Fallen` remembers them by name, and they arrive fallen. Once everyone still there is out, `SettleIfAllOut` loses the land and sends them home after `LostLinger`, and that's asked when someone falls, arrives or goes their own way, as someone standing leaving can leave only the fallen. Losing a land costs the orders given for it and nothing else.

`TalentPoints` is one, two a level, and one for each level of `BestLand` up to the hundredth (`Lands.MostCountedLand`), so climbing pays and freeing the same land again doesn't.

## A land

A land is a world grown for one visit: a kind of world, a road network joining its places, and the Reich in it.

- **A land is its kind's usual size** (`WorldArchetype.UsualSize`), about 900 m a side for an isle up to about 1,150 m for an archipelago or a lakeland, drawn on a fork of its own so nothing else about a world moves with it. Each kind's was measured to leave about 0.3 km² that anyone can walk, a fifth of what lands had at first, as water, mountains and coasts don't shrink with a land: a kind with more of them is larger. Sprawling orders make one larger, up to the most its kind can be (`WorldArchetype.Size`, `WorldRequest.Larger`), with room for more packs, more places and their chests. The Haven and an arena are always the size they are. Places of a kind stand closer together on a smaller land (`WorldPlan.Closeness`), and `SiteStage.MakeEnough` looks again, more loosely, for whatever a land is still short of: five held places (`SiteStage.LeastHeld`) and two waygates.
- **Every held place has a chest**, and a group worth stopping for: the first hostile at a place leads it, Magic in the first two levels and Rare from the third (`EncounterSystem`). Shrines are held by raiders, standing stones by cultists, watchtowers are always guarded, and hamlets, farmsteads, fishing hamlets and lumber camps are overrun. A shrine can't be kindled until its holders are down.
- **Villages are hiding.** Each has a refuge (`SettlementStage.PlaceRefuge`): a waygate, a shrine whose blessing lasts two minutes (`Rulebook.ShrineBlessing`), a strongbox for the stash, and a merchant whose stall is always there. Villagers keep their heads down until the land is free.
- **Waygates are scattered about** (`SiteKind.Waygate`, two to six a land counting the landing's and the villages', at least `Spacing` apart, closer on a smaller land). A hero at one can travel to any other they've found, and go home from any of them (`TravelToSite`, `ReturnHome`). The landing has one, and that's where everyone arrives.
- **The tyrant** is what `Chronicle.BossKindOf` names, and the main objective. It always has orders on it (`Loot.OrdersFrom`).

A smaller land has less room for what it must have: when there isn't room on the usual terms, `SiteStage` looks again on easier ones (`SiteDefinition.Relaxed`), from a fork of its own. `WorldValidator` holds all of this: at least five places to free (`CheckLandPlaces`), at least two waygates, a keep, a chest at every held place, a stash in every village, and nothing standing on a waygate's spawn.

## Arenas

Every tenth level (`Lands.IsBossLevel`) the map table shows three arenas instead of lands, each with a different kind of tyrant: bred, raised from bones by the Ossuary, or rotting where it stands (`Chronicle.TyrantKinds`, a seed chosen for each by `Lands.SeedFor`). An arena is a kind of world that's nothing but one place (`WorldArchetype.Arena`), never drawn from a seed: they're in `ArenaCatalog`, as biomes are, so more kinds can be added, each with its own builder. There's one so far.

**The colosseum** (`ArenaCatalog.Colosseum`, `ArenaBuilder`) is a bowl dug into a low hill on a small isle: a sand floor sunk six and a half metres into level ground, three tiers of stone stands stepping up all the way round it to a colonnade of braziers at the rim, four gates at the foot of the stands round its north half, where the camera sees them face on, and a ramp cut through the south stands from the waygate down to the floor. The stands are the ground itself, in terraces, so that the people of the land, made to watch (`SpawnRole.Spectator`), stand on them as anyone stands anywhere, look towards the fight and call down to it now and then (`VillagerComponent.Crowd`). The stands rise more gently than the camera looks down, so nothing on the floor is ever hidden. Nothing grows in it or fills it with water (`PlacementLayer.Keepout`).

**The fight** is the tyrant (`SpawnRole.Boss`), made much stronger than in a keep (`Rulebook.ArenaTyrantHealth` and `ArenaTyrantDamage`, through `ServerSession.TyrantMods`, which `TyrantHealth` reads back out for what it's worth). Each time it loses another quarter of its health (`Rulebook.ArenaWaveEvery`) its handlers throw up some of the gates and let more of its brood in (`ArenaSystem`): two gates the first two times and all four the last, each letting through two (`ArenaWaveSize`), as many more as the land's orders put at a place, every one Magic or Rare (`ArenaWaveRareChance`), and already looking for whoever's on the floor. Whose they are goes by the tyrant (`Warbands.ArenaRecipe`): the Reich's war-band for a bred tyrant, the Ossuary's dead or its rotting failures for one of its own. A gate's portcullis is a thing everyone sees go up and come down (`ArenaSystem.PortcullisModel`, drawn by `CharacterFactory.Portcullis`), and a gate still up when the next lot are let in stays up for them. Whoever comes through belongs to no place (`ServerSession.Arrivals`): nothing about them is kept, and they drop what anything else does.

Freeing an arena pays what a land does for its tyrant and its places together, and opens the next level and another orders slot. Orders work there too: Teeming and the Last Garrison let more through each gate, Orders for the Twin Pens let a second tyrant in beside the first (`SpawnRole.Rival`), and the rest do what they always do.

`WorldValidator.CheckArena` holds that an arena has a tyrant, a waygate, four gates, at least 24 people watching, its floor above the sea and nothing standing on it.

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

`HavenBuilder` lays it out for the game's camera, which looks north from high in the south. The great waygate stands at the head of everything on a terrace of its own, among blossom; the map table is at the top of the square in front of it; the merchant and the strongbox face each other across the square; a quiet garden is off it; homes stand round it, kept out of the gate's way (`GateClear`); and the training ground is south, past the well. The isle is little more than the village and a ring of woods (`WorldArchetype.Haven.Size`), as a player who finds open ground goes looking for something to fight on it, and there's nothing. There's no shrine, as there's nothing here to be blessed for, and `WorldValidator` fails one.

- **Its trees were planted** (`WorldData.Plantings`, which `VegetationStage.PlantOnPurpose` plants before the woods, on the same rules as anything else, smaller or not at all where there isn't room), Nobody else's are. Its grass is the land's own: cutting it short was tried, and from this camera short grass is a field of stubs.
- **The training ground** is a gravel yard with three pens along it, divided by low fences: a dummy alone, a group of five and a crowd of fifteen (`SpawnRole.Dummy`, `DummyComponent.Group`), at least ten metres from one another so that what's aimed at one meets no other, which `Dino NetLab lands` holds. A dummy can't die, is as good as new once it's been left alone for `Rulebook.DummyRest`, and its group shows what's been dealt to it a second (`DummyMeter`).
- **The merchant** stocks for the deepest land the hero can open, turns over every time they free one, and sells a couple of orders (`GameServer.TableFor`).
- **The stash** is in the strongbox (`Progression/Stash.cs`): four tabs of 48 cells, shared by every hero on the machine, where nothing moves unless it's moved. It runs on both ends by the same rules, as the pack does: the client changes it at once and the server has the last word (`Stow`, `Unstow` and `Shift` go through `AskGear`, and `ServerPlayer.Heard` answers). It's the player's own, so a friend's server only holds a copy while they're there, and it's their machine that keeps it (`StashStore`), in one file that only one copy of the game can hold at once. One that can't be read is kept aside rather than written over.
- **The world cache keeps the Haven** apart from the last land grown (`WorldCache`), as everyone goes back to it between every two lands.
- **The beta vendor** stands below the merchant, there for testing the game and for nothing else, and gives away whatever he has (`Progression/BetaVendor.cs`). Each of his tabs is a line in `BetaVendor.Tabs`. What's out is a pure function of which tab, which roll and what level, so a client shows it with nothing sent, and asks for one by those (`HeroAction.TakeOffer`, through `AskGear`); the server makes the very same thing again, and only in the Haven (`BetaVendor.GivesIn`). His uniques are a lookup rather than a table (`Tooltips/UniqueLookup.cs`). `BetaVendor.Standing` sends him away when the game isn't being tested any more.

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
| An arena's gates and who comes through them | `Server/ArenaSystem.cs` |
| An arena's shape | `World/Generation/ArenaBuilder.cs`, `ArenaCatalog` in `WorldArchetype.cs` |
| The map table and the leave prompt | `UI/LandPanels.cs` |
| The corner | `Hud.DrawChronicle` |
| The travel map | `MapPanel`, in its travel mode |
| The Haven | `World/Generation/HavenBuilder.cs`, `WorldArchetype.Haven` |
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
- **A new kind of arena** goes on the end of `ArenaCatalog.All` with a builder of its own, a `SiteKind` if it needs one, a line in `WorldValidator.CheckArena` and gates for `ArenaSystem` as `SpawnRole.ArenaGate`, or none.
- **After any change here** run `Dino BuildLab check` and `lands` (the table of what orders put on a land and what each omen pays), `Dino NetLab lands`, `game`, `loot`, `pack` and `build`, and `Dino WorldLab check`, with `--people 150 --packs 100` for crowded lands, `--packs -100` for a land held only at its places, `--archetype colosseum --count 30` for arenas and `--seed 1610 --count 1 --archetype haven` for the Haven. A change to how orders look needs `Dino GearLab check` and `variety --kind Orders`.
- **The beta vendor**: a new tab is a line in `BetaVendor.Tabs`. After any change to him run `Dino BuildLab check` and `Dino NetLab vendor` and `pack`.

## Seeing it

`--haven true` starts in the Haven, `--landlevel N` opens every level up to N, `--openway N` opens the way to the Nth land of the deepest level after a second, `--travel N` goes home or out after N frames, `--omens a,b` and `--orders a,b` set up the first land, `--stash file` keeps the stash somewhere, and `--panel table`, `stash`, `travel`, `vendor` or `uniques` opens those. An arena is `--archetype colosseum --level 10`. The Haven's square is about `--at 160,174`, the walk up to its gate `--at 160,195`, and its training ground `--at 154,112`.

## Decided, and open to change

Choices made without asking. Say if any are wrong.

- **"Land" for a map, and "Land Level" for a map's level**, "the Haven" for the hub, "the map table" for the selection screen, "the way" for the portal, and "arena" for a boss level, with the colosseum the Reich's.
- **"Orders" for map items**, and **"omens" for their lines**, a prefix a reward and a suffix a danger.
- **The arena's numbers**: the tyrant with 200% more health and 35% more damage than in a keep, gates every quarter of its health, two then two then all four, two through each, a third of them Rare, and seven seconds up.
- **Who watches**: the land's people, made to by the Reich, who want the Defender to win and say so quietly.
- **Lands of about 0.3 km² to walk**, a fifth of the first ones, after two rounds of "still too big".
- **Rerolling** at 25 gold a level, growing as the level does, and freeing a level showing other lands for it next time.
- **Orders slots**: one, and one more for each arena freed, up to a pack's worth.
- **Losing a land costs only the try and the orders**, and the same three lands wait for another go.
- **Talent points from lands stop at the hundredth**, as a hero's level does.
- **Three to five places to free**, three in the first two levels, as a land is small and that's where someone learns what a land is. Two waygates are enough on a land this size.
- **Leaving a friend's game halfway costs nothing.**
- **One player going home through a waygate takes everyone**, as a server only holds one world.
- **The stash**: four tabs of 48, shared by every hero on one machine, and not shared with friends.
- **Twelve things on a table, two of them orders in the Haven**, and one in twenty drops orders.
- **The training ground**: one, five and fifteen dummies, four seconds to reset.

## Where it stands

**Nobody has played the loop, an arena, the level list, rerolling or orders as they are now, or stood in the Haven but the bots and the stills.** Ask how it feels first: whether the arena reads as a colosseum from the game's camera, whether its tyrant is hard enough, whether the waves come too soon or too late, whether choosing between three lands is a real choice, whether lands are now the right size, and whether rerolling costs what it should.

Not built:

- **Other kinds of arena.** One so far; `ArenaCatalog` is where more go.
- **Caves, mines and wrecks.** The places a land asks to be freed are the kinds there already were.
- **Anything unlocked by an arena but an orders slot.**
- **Orders being crafted or changed.** They're found and given as they are.
- **A count of time in a land**, for anyone who wants to race.
