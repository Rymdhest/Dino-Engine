# Lands

The loop the game is: the Haven, where nothing can reach a hero, and the lands the Saurian Reich holds, which a hero goes to through the great waygate there and either frees or loses. Read this before changing anything about a land's terms, omens, orders, the climb, the Haven or the stash.

Status: built on 2026-11-08. Every rule here has a check in `Dino BuildLab check` and `Dino NetLab lands`, and the Haven has been judged from the game's own camera in stills. **Nobody has played it.** The numbers are where to start, not where to stop.

## The loop

1. **The Haven.** A hero starts there, and comes back there after every land. It's a world like any other, grown from its own seed (`Lands.HavenSeed`) and a kind of world nothing else is (`WorldArchetype.Haven`, `Peaceful`), so it's the same place every time and never saved. Nothing hostile stands in it, and falling there only knocks a hero down for a moment.
2. **The map table** shows three lands (`Lands.Offer`), each a different kind of world with its own story, biomes and omens, all at the level of the hero's climb. They come from what's saved on the hero alone (`HeroState.Offers`), so both ends of a connection see the same three, and they're the same three however often the table is looked at, until a land has been gone to.
3. **Opening the way** to one of them, with orders given for it or none (`GameServer.OpenWay`), lights the great waygate in that land's colour. Stepping through takes everyone in the Haven there (`StepThrough`), and moves every hero's offers on.
4. **The land** asks for two things, in the corner under its name: bring down its tyrant at the keep, and free a number of its places (`LandTerms.Places`, four to six). Both done, it's free (`ServerSession.Free`).
5. **Home**, through any waygate in the land once it's free, or before then at the cost of losing it. If everyone falls, they go home a few seconds later anyway.

## The climb

`HeroState.LandLevel` is the level of the next land the map table shows. A land's level is everything about how hard it is: its enemies are that level, so is what they drop, and omens open with it.

| What happens | What it does to the climb |
|---|---|
| Free a land while standing in it | `Freed`: the climb goes to at least that land's level plus one, so freeing a friend's lower land costs nobody their own climb, and a friend's deeper one takes a hero on from there |
| Fall in a land before it's free | `Lost`: one level down, never below the first, settled at once so that closing the game can't take it back |
| Leave through a waygate before it's free | The same as falling |
| Fall once it's free | Nothing |
| Anything in the Haven | Nothing |

There's **one life to a land**. A hero who falls is out of it (`ServerPlayer.Out`): they stay down, their commands do nothing, and they watch until everyone goes home. Leaving the server and coming back isn't another life: `ServerSession.Fallen` remembers them by name, and they arrive fallen. Once everyone still there is out, `SettleIfAllOut` loses the land and sends them home after `LostLinger`, and that's asked when someone falls, arrives or goes their own way, as someone standing leaving can leave only the fallen.

`HeroState.BestLand` is the deepest land freed, and it's what the talent points for freeing lands go by: `TalentPoints` is one, two a level, and one for each level of `BestLand`, so climbing pays and freeing the same land again doesn't. A save from before lands keeps every point, as `BestLand` starts from `WorldsCleared` (`SaveGame.IntoTheHaven`).

## A land

A land is a world grown for one visit, much as worlds always were: a random archetype, a road network joining its places, and the Reich in it. What's new is in `SettlementStage`, `SiteStage` and `PackStage`:

- **Every held place has a chest**, and a group worth stopping for: the first hostile at a place leads it, Magic in the first two levels and Rare from the third (`EncounterSystem`). Shrines are held by raiders, standing stones by cultists, watchtowers are always guarded, and hamlets, farmsteads, fishing hamlets and lumber camps are overrun. A shrine can't be kindled until its holders are down.
- **Villages are hiding.** Each has a refuge (`SettlementStage.PlaceRefuge`): a waygate, a shrine whose blessing lasts two minutes (`Rulebook.ShrineBlessing`, one of `BuffCatalog.Blessings`), a strongbox for the stash, and a merchant whose stall is always there. Villagers keep their heads down until the land is free.
- **Waygates are scattered about** (`SiteKind.Waygate`, three to six a land, never close together). A hero at one can travel to any other they've found, and go home from any of them (`TravelToSite`, `ReturnHome`, both needing a hero within `GateReach` of a gate). The landing has one, and that's where everyone arrives.
- **The tyrant** is what `Chronicle.BossKindOf` names, and the main objective. It always has orders on it (`Loot.OrdersFrom`).

`WorldValidator` holds all of this: at least six places to free (`CheckLandPlaces`), at least three waygates, a chest at every held place, a stash in every village, and nothing standing on a waygate's spawn.

## Omens

An omen is something a land holds that makes it worse and richer (`Progression/Omens.cs`). Each is one number, rolled from tiers that open with the land's level, and everything about it grows with that number, what it pays included. There are three kinds:

- **What enemies are**: everything hostile wears it, bosses too (`LandTerms.FoeMods`, into the session's `Bestiary`). Ironhide, Bloodthirsty, Quickened, Ashen, Rimed, Stormborn, Venomous, Leeching, Thorned, Keen-edged, Spellproof, Plated, Hexing, Bloodletting and Unyielding.
- **What the land is**: Teeming (larger packs), Overrun (more of them), Warhost, Gilded (better chests), Endless Night, Volatile, Rallying, Bloodlines (more Magic and Rare enemies) and Tyrant's Wrath.
- **What the land does to heroes**: Bleak, Exposed, Frail, Parched, Sapped and Lagging. These are part of every step a hero takes, so both ends put them on, last, through `HeroBuild.Compile(hero, buffs, land)`, and take them off again at home.

**Rolling** (`Lands.Roll`): a land of level L holds `MostOmensAt(L)` omens or one fewer, from one at the start to six from level 28, each drawn once by weight from those its level allows, and each rolled from the best tier its level reaches, often one or two below (`Omens.TierDrop`). Tiers open at levels 1, 6, 14, 24 and 38, and some omens start later.

**What they pay** is a `LootBoon` (`LandTerms.Boon`): each point of an omen adds its `Quantity` and `Rarity` shares, a unique's chance gets half of the rarity, and each tier above the first an omen reached adds a little to how well items roll. Teeming and Overrun pay nothing more a kill, as more enemies is the reward, and Gilded pays through chests. An empty land's boon is `LootBoon.None`, which leaves every roll as it was. What a land's omens add to an enemy's health is divided back out wherever a find's worth is read from health (`Combat.DropLoot`), or fodder would drop like champions.

**More of them is part of the world.** Teeming and Overrun change who stands on the land, so `LandTerms.Shape` makes the `WorldRequest` (`MorePeople`, `MorePacks`) on both ends, and only `PackStage` reads them. The same omen held twice is the two added up, as far as `Most` (one and a half times its best tier).

An omen's id is saved, and its place in `Omens.All` is what's sent, so a new one goes on the end. Names, numbers and tiers can change freely.

## Orders

Sealed orders taken from the Reich (`Progression/Orders.cs`). They're items like any other, of the same four rarities, carried in the pack and given to the map table when the way is opened, which uses them up: the land it opens on holds what they say (`LandTerms.Give`).

| Rarity | Holds |
|---|---|
| Normal | What its kind always brings |
| Magic | That, one omen of what enemies are, and one of what the land is |
| Rare | That, and two of each |
| Unique | What it is, and nothing else |

Each kind brings one omen of what the land is, at the middle of the best tier its level reaches: Muster Orders bring Teeming, Patrol Orders Overrun, Supply Orders Gilded, Breeding Orders Bloodlines, and Hunting Orders Tyrant's Wrath. Orders are named for their first omen of each side around their kind, as "Ironhide Patrol Orders of Embers". A line never repeats the kind's own omen, and `OrdersMaker.Fix` brings whatever a save or a stranger sends up to these rules from the orders' own seed, and always names them from their lines.

Unique orders are each a trade, what they give and what they cost said apart:

| Orders | Gives | Costs |
|---|---|---|
| The Kommandant's Wager | Freeing the land climbs two levels, and 30% more items | Losing it drops two |
| The Brutmeister's Ledger | Every pack has a Rare leader if it can, and Rare enemies drop twice as much | Rare enemies have one more trait |
| Bred for War | 60% more item rarity | Nothing is Normal |
| Orders for the Twin Pens | A second tyrant at one of the farthest places, which drops all a tyrant does | Both must fall |
| The Last Garrison | Nothing on the roads, in the wild or at a camp, and every place's chest is a hoard | Every place holds three times as many |
| The Ossuary's Writ | 50% more items | Everything that falls rises once more as bones, a moment later |

What each does is checked for by name where it happens (`LandTerms.Unique`), never as a flag. Unique orders' ids are saved.

Orders come from tyrants, always, one in twenty of everything else that drops (`Loot.OrdersShare`), and the last two cells of the Haven merchant's table.

## The Haven

`HavenBuilder` lays it out for the game's camera, which looks north from high in the south. The great waygate stands at the head of everything on a terrace of its own, among blossom; the map table is at the top of the square in front of it; the merchant and the strongbox face each other across the square; a quiet garden is off it; homes stand round it, kept out of the gate's way (`GateClear`); and the training ground is south, past the well. The isle is little more than the village and a ring of woods (`WorldArchetype.Haven.Size`), as a player who finds open ground goes looking for something to fight on it, and there's nothing. There's no shrine, as there's nothing here to be blessed for, and `WorldValidator` fails one.

- **Its trees were planted** (`WorldData.Plantings`, which `VegetationStage.PlantOnPurpose` plants before the woods, on the same rules as anything else, smaller or not at all where there isn't room), Nobody else's are. Its grass is the land's own: cutting it short was tried, and from this camera short grass is a field of stubs.
- **The training ground** is a gravel yard with three pens along it, divided by low fences: a dummy alone, a group of five and a crowd of fifteen (`SpawnRole.Dummy`, `DummyComponent.Group`), at least ten metres from one another so that what's aimed at one meets no other, which `Dino NetLab lands` holds. Gravel runs a little way past its fence, as grass grows in tufts that reach in as far again as they stand. A dummy can't die, is as good as new once it's been left alone for `Rulebook.DummyRest`, and its group shows what's been dealt to it a second, from the first blow after it was last left alone to the last (`DummyMeter`, which only reads what's shown and goes by nothing).
- **The merchant** stocks for the hero's climb rather than for the Haven, turns over every time they've been to a land, and sells a couple of orders (`GameServer.TableFor`).
- **The stash** is in the strongbox (`Progression/Stash.cs`): four tabs of 48 cells, shared by every hero on the machine, where nothing moves unless it's moved. It runs on both ends by the same rules, as the pack does: the client changes it at once and the server has the last word (`Stow`, `Unstow` and `Shift` go through `AskGear`, and `ServerPlayer.Heard` answers). It's the player's own, so a friend's server only holds a copy while they're there, and it's their machine that keeps it (`StashStore`), in one file that only one copy of the game can hold at once. One that can't be read is kept aside rather than written over.
- **The world cache keeps the Haven** apart from the last land grown (`WorldCache`), as everyone goes back to it between every two lands.

## Where it is

| What | Where |
|---|---|
| A land's terms, the climb's offers, the Haven's request | `Progression/Lands.cs` (`LandTerms`, `Lands`) |
| Omens | `Progression/Omens.cs` |
| Orders, their kinds and the unique ones | `Progression/Orders.cs` |
| The climb | `HeroState.LandLevel`, `BestLand`, `Offers`, `Freed`, `Lost` |
| The stash | `Progression/Stash.cs`, `HeroPanel` (the strongbox is a shelf beside the pack) |
| Opening the way, stepping through, going home, travelling | `GameServer.OpenWay`, `StepThrough`, `GoHome`, the `TravelToSite` and `ReturnHome` actions |
| One life, judging a land, freeing it | `ServerSession.HeroFell`, `Judge`, `Leave`, `Free`, `SettleIfAllOut` |
| What's in a place, who leads it, the dummies, a shrine's blessing | `EncounterSystem` |
| The map table and the leave prompt | `UI/LandPanels.cs` |
| The corner | `Hud.DrawChronicle` |
| The travel map | `MapPanel`, in its travel mode |
| The Haven | `World/Generation/HavenBuilder.cs`, `WorldArchetype.Haven` |
| Refuges, held places and their chests | `SettlementStage` |
| A land held only at its places | `PackStage`, through `WorldRequest.Emptied` |
| On the wire | `Protocol.WriteTerms`, `WriteStash`, the hero summary's climb |

## Changing it

- **Saved and sent things stay**: omen ids and their order, unique orders' ids, and the names of the orders' kinds (an item's `Base`). Everything else can change freely.
- **`LandTerms.Shape` makes the request**, on both ends, and nothing else does. A save keeps the request as asked and the terms beside it.
- **The Haven is never saved**, and a land that's over isn't either: `ServerSession.Capture` writes nothing for them, and a host who's fallen in a land doesn't keep it to go back to.
- **A new omen** goes on the end of `Omens`, says what it does at every tier, pays for itself somehow, and is put on what it says it's put on: `check` fails otherwise.
- **A new unique orders** needs a cost a player would notice, a line in the table above, its effect checked for by name where it happens, and a test in `Dino NetLab lands`.
- **After any change here** run `Dino BuildLab check` and `lands` (the table of what each level holds and what each omen pays), `Dino NetLab lands`, `game`, `loot`, `pack` and `build`, and `Dino WorldLab check`, with `--people 150 --packs 100` for crowded lands, `--packs -100` for a land held only at its places, and `--seed 1610 --count 1 --archetype haven` for the Haven. A change to how orders look needs `Dino GearLab check` and `variety --kind Orders`.

## Seeing it

`--haven true` starts in the Haven, `--landlevel N` sets the climb, `--openway N` opens the way to the Nth land after a second, `--travel N` goes home or out after N frames, `--omens a,b` and `--orders id` set up the first land, `--stash file` keeps the stash somewhere, and `--panel table`, `stash` or `travel` opens those. The Haven's square is about `--at 160,174`, the walk up to its gate `--at 160,195`, and its training ground `--at 154,112`.

## Decided, and open to change

Choices made without asking. Say if any are wrong.

- **"Land" for a map, and "Land Level" for a map's level**, as the game was already about lands. "The Haven" for the hub, "the map table" for the selection screen and "the way" for the portal.
- **"Orders" for map items**: sealed orders taken from the Reich, which say where a brood is bound and what it brings. Their kinds and the six unique ones are named in the Reich's tongue or its plain army words.
- **The omens' names and numbers**, and how many a land holds: one at first, six from level 28.
- **Four to six places to free**, four in the first two levels, as that's where someone learns what a land is.
- **Leaving a friend's game halfway costs nothing**, as their machine only ever kept their own world, and a dropped connection shouldn't cost a level.
- **One player going home through a waygate takes everyone**, as a server only holds one world, and everyone who hasn't freed the land loses it.
- **What falling costs in the Haven**: nothing but a moment.
- **The stash**: four tabs of 48, shared by every hero on one machine, and not shared with friends.
- **Twelve things on a table, two of them orders in the Haven**, and one in twenty drops orders.
- **The training ground**: one, five and fifteen dummies, four seconds to reset, and the number shown being what a build does over a whole fight rather than its best moment.

## Where it stands

**Nobody has played the loop, chosen a land, given orders or stood in the Haven but the bots and the stills.** Ask how it feels first: whether choosing between three lands is a real choice, whether the climb is too fast or too slow, whether one life is too harsh with friends, and whether the Haven is somewhere they'd like to be.

Not built:

- **Caves, mines and wrecks.** The places a land asks to be freed are the kinds there already were.
- **Anything at the top of the climb.** It goes on for as long as anyone can.
- **Orders being crafted or changed.** They're found and given as they are.
- **A count of time in a land**, for anyone who wants to race.
