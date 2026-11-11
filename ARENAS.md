# Arenas

Where a tyrant is fought. Every tenth level's land is nothing but an arena on an isle of its own, and every other land's keep holds a smaller one, with the land's tyrant inside. There are six kinds, each its own place and its own fight. Read this before changing an arena's layout, its pieces, what it does on its own, its gates or a keep's arena, and read `LORE.md` before changing what any of it looks like or is called.

Status: built 2026-11-10 to 11-11. **Nobody has played any of it.** It was judged from stills taken from the game's camera, and every rule below has a check in `Dino WorldLab check` and `Dino NetLab arenas`. The numbers are where to start.

## The one idea

**An arena is sealed.** Nothing that walks on its floor gets off it, and nothing off it gets on, until the tyrant's down. Two things do it together. A ring of solid footprints runs along the inside of its walls (`ArenaLayout.SealLine`, `SealArc`), and the floor is sunk at least four metres below everything round it. Walkable and passable ground is flooded out from where heroes arrive, so whatever's on the other side of the seal isn't ground anyone can stand on at all. Four metres matters: a node of the height grid on the edge of a sharp step takes a height between the two, and a smaller drop splits into two half-steps that can both be walked.

Nothing else may cross a seal either. A page that carries someone lands them on their own side of every keep's arena (`GameServer.Carry`, `ServerSession.SameSide`), calling the roads is refused on a keep's arena's floor while its tyrant stands (`ServerSession.HeldInArena`), and only the arena's own gates take anyone across (`GameServer.Bring`). `WorldValidator.CheckSealed` holds it by flooding the world's own maps from where its fighters stand: everything passable stays within a body of its bounds, and everything walkable within the seal's thickness.

**The camera looks north from the south**, so tall things stand on an arena's north side, where they're seen face on, and its south side stays low. Nothing walkable stands behind the waygate or the way up, where a wall would hide whoever's there. East is the left of the screen, and bearings run clockwise from north.

**What the game needs of an arena is its plan** (`ArenaPlan`, on `WorldSite.Arena`, headless): its kind, the bounds of its floor, the entries the brood comes in by and what moves in them, its set pieces, how far it reaches and where its floor is, and for a keep's arena its way down, where whoever comes down arrives, its way up and the radius of the wall round its rim. It's worked out with the world and never saved.

**Every piece is grown from its id**, as plants are. An `ArenaSpec` id is `arena:<shape>:<seed in hex>:<numbers>`, in whole centimetres and degrees, with no dots or hash, and `ArenaForge` builds it, on worker threads at load (`WorldAssetLibrary.Grow`), and the game grows whatever moves the same way (`CharacterFactory`). Stone colours are multipliers on the Rock material (`ArenaStone`), black iron, oxblood and brass are the Reich's and nothing else's, and its mark is a tyrant's skull (`ReichMark`), which the harness on its dinosaurs carries too.

## The six kinds

| Kind | What it is | The brood comes in by | What it does on its own | Its sky |
|---|---|---|---|---|
| The Pit (`pit`) | The oldest kind: a round pit of sand dug into a hill, a sheer sandstone podium with the Reich's iron along it, four gatehouses on the north, three tiers of stands to a rim of pylons and braziers | Grilles that rise into the gatehouses | Nothing: it's the plain fight | High summer, fair |
| The Colosseum (`colosseum`) | An oval of limestone: a sand floor, four tiers, a great arcade and attic on the north, and the commander's box over the podium under an oxblood canopy | Great gates east and west, and hatches whose leaves drop open in the floor | Its two bolt-throwers loose bolts down lines across the floor from the box, in turn | A golden evening |
| The Jurassic Cage (`cage`) | The Amber Institute's breeding pen: a floor of mud sunk four metres, a granite footing with iron bars leaning in over it, nests, troughs, gibbets and the Institute's gallery of amber jars | Pen doors whose grilles rise | Its clutches hatch into more of the brood unless they're smashed first | Grey and misty |
| The Sun-Temple (`suntemple`) | A court of gold sandstone, a platform with the Reich's sun disc, colonnades, an inlay of rings in the floor and mirrors of brass on pedestals round it | Bronze doors that swing in | Its mirrors gather the sun onto someone and burn whatever's there, the tyrant too | High summer, clear |
| The Fossilized Onslaught (`onslaught`) | The Ossuary's quarry in basalt and ash: two benches cut down to the floor, and a charge of the Reich's war-beasts turned to stone where the ash caught it, with wagons, standards, scaffolds and a crane | The ash heaves where the dead claw up out of it, and throws whoever's on it | Its vents breathe fire along their cracks, and leave them smouldering | Overcast, ash falling |
| The Cathedral of the Iron Apex (`cathedral`) | The Reich's own: a nave of dark granite piers sunk into the ground, arches of riveted iron across it, iron screens between the piers, an apse of oxblood and amber glass round the throne-cage, and the Iron Apex over it, a tyrant's skull in black iron | Pointed gates under the screens, their grilles rising | Its chandeliers give as the tyrant weakens and come down burning on whatever's under them, the tyrant too | Always night |

A boss map's sky is held to its kind's (`WorldArchetype.Weather`, read by `WeatherField`), and a keep's arena has its land's, but a cathedral is night round it wherever it stands (`AmbienceSystem`). The names are the game's own, never a real place or event: The Sun-Temple and The Fossilized Onslaught were chosen over names that borrowed from real ones.

## A fight

**The tyrant is made stronger in a boss map's arena than in a keep** (`Rulebook.ArenaTyrantHealth` and `ArenaTyrantDamage`, through `ServerSession.TyrantMods`). Each time the tyrants of an arena lose another share of their health between them (`Rulebook.ArenaWaveEvery`), the handlers open some of its entries and let more of the brood in (`ArenaSystem`): `ArenaWaveGates` of them the first times and all of them the last, each letting through `ArenaWaveSize` and as many more as the land's orders put at a place, Magic or Rare, and already looking for whoever's on the floor. Whose they are goes by the tyrant (`Warbands.ArenaRecipe`). Whatever moves is something everyone sees move, on the arena's own clock, which stands still while the world does. Whoever comes through belongs to no place (`ServerSession.Arrivals`): nothing about them is kept, and they drop what anything else drops. Where the dead come up the ash is marked first and they come up on its tick.

**What an arena does on its own has no rule of its own** (`ArenaSystem.Hazards.cs`). Each is a book on `Shelf`, the arena's and nobody's to carry, in the hands of one of two actors who stand nowhere and can't be struck. One is on the Reich's side, as strong as one of the tyrant's brood. The other falls on the tyrant's side, with Spell Damage of a hundredth of the tyrant's health and nothing else, never a Critical Hit and leaving nothing on anyone, so that a Spell Page of the arena's takes a share of the tyrant's health (`Rulebook.ChandelierTyrantShare`, `SunfireTyrantShare`) before what it resists, give or take what added damage spreads by: anything sized to a hero would be nothing to a tyrant. What's thrown is an Attack and what burns a Spell, which can't be evaded. Every one is marked like any blow (`Combat.Warn`), as grave and from a Unique, so that nothing a player chooses not to see hides it, and lands on the tick its mark says, counted in ticks that pass only while the world does. Within a tick whatever's begun goes on first and whatever's new starts after, so that nothing waits a tick less than its mark. It all begins `Rulebook.HazardGrace` after a hero first sets foot on the floor, or as soon as the tyrant's hurt, and stops when it falls: what was on its way is withdrawn, and a chandelier that was falling lies where it would have.

- **Bolt-throwers** take turns at whoever's on the floor, turning on their posts as they aim, and the bolt goes down a line from where it meets the floor, with a streak of it from the thrower's head (`Shelf.BallistaBolt`).
- **Clutches** are enemies that stand and take it (`Actor.Still`), with the eggs for a body: their crowns glow with what's grown from blood in amber, so a clutch is something to go for at a glance. Each stirs a while before its time and hatches into the brood unless it's been smashed, and either way its shells are left in the nest.
- **Mirrors** come round, the two nearest someone on the floor, and the sun they gather lands where that someone stood, burning whoever's there and the ground (`Shelf.Sunfire`, `CinderTrail`), with a beam off each mirror.
- **Vents** wake near whoever's on the floor, and breathe fire along their cracks, which smoulder after (`Shelf.Eruption`, `Smoulder`). The ash heaving where the dead come up throws whoever's on it (`Shelf.Heave`).
- **Chandeliers** give, the one nearest anyone on the floor each time the entries open, swing as their chains give, fall the last of their time, crash on their tick (`Shelf.Crash`) and burn where they lie (`Shelf.Wreck`). They keep their candles lit as they hang, fall and lie (`PropLightSystem`, `AssetLightEntities`).

## A keep's arena

**Every land's keep holds an arena** with the land's tyrant in it (`ArenaBuilder.Keep`): one of the six kinds, each as likely, drawn from a fork of the keep's own, at a keep's size in the middle of its walls. It's walled round at its rim by a parapet with the seal along it (`ArenaLayout.KeepRim`), so that nobody walks from the keep onto its stands, where its gatehouses would hide them from the camera, and the land's people are made to watch from them as in any arena. The keep's camp and its garrison are round it, between its wall and the keep's (`PackStage.Reinforce` puts nobody inside its wall).

- **The way down** is a small gatehouse in the rim wall on the south (`ArenaPlan.Door`, `InteractKind.ArenaDown`), which takes whoever uses it down to the floor.
- **The way up** is a gatehouse where a boss map's waygate would stand, of the arena's own stone, its grille shut behind whoever came down (`ArenaPlan.Way`, `ArenaLayout.KeepGate`). Once the tyrant's down the grille rises and the way up is there in front of it (`InteractKind.ArenaUp`), which takes whoever uses it back up by the way down. It isn't there before.
- **The tyrant and the keep's hoard are on the floor**, and the tyrant is only made once a hero's come down (`EncounterSystem.WakeFloor`), as nobody on the keep's ground could reach it.
- **The mark points to the way down** from off the floor (`GameSession.GoalAt`), and to the way up once the land's free.
- **A keep stands on ground** at least `SiteStage.KeepAboveSea` above the sea, as its arena is dug down from it and its floor has to stay clear of the sea: a keep on low ground stands on a mound the land is eased up to. Roads that end at a keep stop inside its gate (`SettlementStage.KeepRoadEnd`), and roads going elsewhere go round its middle (`RoadStage.KeepCore`).

A world's walkable ground is flooded out from the landing and from each keep's arena's arrival (`FinalizeStage.FloodsFrom`), and nobody is moved across a seal when a spawn is repaired (`RepairSpawns`). `WorldValidator.CheckKeepArena` holds that the land walks to the way down and never onto the floor, that the floor holds the tyrant and nobody else hostile, and that the seal holds from where whoever comes down arrives. Its walking check carries whoever reaches the door down to the floor, as the game does.

## Adding to it

- **A new kind**: a value on the end of `ArenaKind`, a layout of its own under `World/Generation/Arenas/` built on `ArenaLayout`, an archetype in `ArenaCatalog` and its `All`, its entries of an `ArenaEntryKind` with whatever moves in them, and its keep's version: a `KeepGate` for the way up, `Tyrants`, and a `KeepRim` at its reach. Lines of its own in `Chronicle`'s arena lines. `Dino NetLab arenas` walks every kind and every kind of keep's arena by itself.
- **A new piece**: a value on the end of `ArenaShape`, its numbers listed beside it, a builder and a case in `ArenaForge`. Nothing in it smaller than a pixel from the game's camera, and nothing that can be seen coplanar with anything else.
- **Something an arena does**: a value of `ArenaPieceKind`, a book on the end of `Shelf` (not Triggered: it's the arena's own), its numbers in the `Rulebook`, what it does in `ArenaSystem.Hazards.cs`, and a check in `Dino NetLab arenas` that it's marked, lands on its tick on whoever's in it and not on whoever's stepped out.

## Testing

```
dotnet "Dino WorldLab/bin/Release/net8.0/Dino WorldLab.dll" check --archetype cathedral --count 30 --level 10   # one kind of boss map, sealed
dotnet "Dino WorldLab/bin/Release/net8.0/Dino WorldLab.dll" check --count 100                                  # lands, every keep's arena among them
dotnet "Dino WorldLab/bin/Release/net8.0/Dino WorldLab.dll" arenas [--archetype id] [--seed N]                 # a boss map's plan, and what its pieces cost
dotnet "Dino WorldLab/bin/Release/net8.0/Dino WorldLab.dll" arenas --land any --seed N                         # a land's keep's arena, its ways, and a map of where anyone can walk
dotnet "Dino NetLab/bin/Release/net8.0/Dino NetLab.dll" arenas [--kind id | --kind keeps]                     # waves, what each does on its own, the seal, and keeps
```

`lands` runs the arena checks with the rest. In the game, a boss map is `--archetype cathedral --level 10`, and `--calm true` keeps everything hostile standing still, for looking at it. A keep's arena is any land's: `arenas --land any --seed N` says where its way down is, for `--seed N --at x,z`.

## Where it stands

**Nobody has played any of it, or watched it move.** Ask how a fight in each feels first: whether the set pieces read in a fight, whether bolts, sunfire and chandeliers are fair, whether clutches hatch too soon, and whether a keep's arena reads as somewhere to go down into.

Decided, and open to change:

- **The Pit does nothing of its own.** It's the plain fight, so the others have something to be different from.
- **What burns falls on the tyrant too**, for a share of its health, so luring it under a chandelier or into the sun is worth doing. Bolts and vents don't, as they're aimed at the hero.
- **A keep's arena's way up only appears once the tyrant's down**, rather than standing there shut, which asked why it wouldn't open.
- **Its set pieces have no sounds of their own** but the gate's: a chandelier giving and a hero going down or up use it.
- **A chandelier's light casts no shadow.** Three of them nearest the hero took the cube maps lent to lights, each rendering the whole nave and its crowd, and the Cathedral stuttered: 95% of its frames went from 22 ms to 9 without them.

Measured on the 7900 XTX at 3440×1369, standing on the floor: the Cathedral at 114 frames a second, the Colosseum at 123, and a keep's arena from its gate at 131. Nothing has been measured on a slower card.

Not built:

- A hero on a keep's ground can still put an area down by the cursor onto its floor while a friend is down there fighting. Nothing walks or flies in.
- The way down and up is a cut, not something seen happening.
