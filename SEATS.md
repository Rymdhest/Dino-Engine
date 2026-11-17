# Seats

A land's keep is its commander's seat: where its tyrant waits, and the one place in a land that has to look like the end of it. There are eight kinds, a land's seed says which, and each tells how that land's tyrant came to be there. Read this before changing a seat's layout, its pieces, how it's sited or which a land gets, `ARENAS.md` (A keep's tyrant) for what the yard has to stay, and `LORE.md` before changing what any of it looks like or is called.

A person called the keep before this "the same really cheap looking castle" in every land, "something out of runescape", and asked that it be a different place each time, grand, plainly the boss's, and telling a story that makes sense in the lore.

## The eight

| Seat (`SeatKind`) | Whose | Its tyrant | What it's built round | What it says |
|---|---|---|---|---|
| Citadel ("Fort ...") | The land's, taken by the Reich | Bred | The land's old castle: a hall and keep on a terrace at the back, curtain walls and round towers in the land's own stone, a gatehouse at each side and an open gate at the front | The land had a lord. His statue lies pulled down in the mud, a stretch of his wall is closed in the Reich's plate where it was breached, the tithe is weighed in his yard, and the Reich built a stall of iron for its tyrant under his terrace |
| Hatchery ("...wiege", "...horst", "...brut") | The Amber Institute's | Bred | A brood house: a dome of black iron and amber glass over a drum of brick, burst open towards the yard | The tyrant was grown here, and came out through the wall. The tank it grew in stands in shards inside, its broth run out through the breach; rows of tanks either side hold the next ones; the shell it hatched from is kept behind a chain |
| Foundry ("...hammer", "...schmiede", "...esse", "...werk") | The Reich's | Bred | A blast furnace: a stepped stack of brick with its throat burning, iron running out down a bed of sand | What the tyrant wears is made here out of what the land had: its bells, ploughs and gates are heaped to be melted, and a dummy of a tyrant in canvas stands in a cradle with the new regalia on it |
| Triumph ("...mal", "...thron", "...wacht") | The Reich's | Bred | A colossus: the tyrant in black iron, half as long again as it lives, roaring, on a plinth of the Reich's granite | The Reich raised it over what the land held holy, which lies dragged aside in a corner with the land's lord in stone. Pylons hung with the shields of the land's dead stand round the yard, and the commander's stand has a chair nobody's in |
| Warcamp ("...lager", "...schanze", "...wall") | A brood's, in the field | Bred | A pavilion striped in oxblood, inside a wall of wagons chained end to end | The brood took no house and built none. The cage the tyrant came in is drawn up by the yard with its door let down |
| Boneyard ("...grube", "...gruft", "...bruch") | The Ossuary's | Bones | The slab a bone tyrant was raised out of, tilted up to the camera, the hollow it left still in it | It was dug out of the rock here. The tackle it came up on is still over the slab, and the next beast is half out of the ground beside it |
| Bileworks ("...sud", "...kessel", "...galle") | The Ossuary's | Rotting | Four vats of bile on their firebox, and the frame the tyrant is hooked into | One the Institute failed with is kept on its feet here. The failures that are next wait in a pen |
| Barrow ("... Barrow") | The land's, opened by the Ossuary | Ghost | One of the land's great barrows, cut open to the camera, inside the land's ring of stones | The bones in it were too far gone to stand, so the Ossuary called up what was left in light. It's always night there (`AmbienceSystem`) |

**Which a land gets** is `SeatCatalog.KindOf(seed)`, from the land's seed alone, so that the site, its name, its builder, the chronicle and whoever holds it agree with nothing passed between them. `SeatCatalog.TyrantOf` is the roll `Chronicle.BossKindOf` always made (seven in ten bred, a tenth each bones, rotting and a ghost): what the Ossuary made has the Ossuary's seat for it, and a bred tyrant has one of the Reich's five, by a fork of its own. A new kind of seat for a bred tyrant goes on the end of `SeatCatalog.OfTheReich`, which moves which every land with a bred tyrant gets.

## The frame

A seat is a stage set for the game's camera, which looks north from above, so every seat has the same frame (`SeatBuilder`, `SeatPlan` on `WorldSite.Seat`) and never turns to face its road:

- **Whatever's tallest stands along the back**, to the north, the sides step down, and the front stays low, so that the yard is seen whole. A gatehouse at the front would stand between the camera and whoever walks in, so the front has an open gate or none.
- **The yard** is half the seat's radius, in the middle and a little forward, kept clear of everything solid, with the tyrant in the middle of it (`OpenYard`, and `ARENAS.md`). Great bowls of fire stand round it (`Fires`), in the Reich's flame or one of the Ossuary's cold lights.
- **Three ways in**: the middle of the front, and each side level with the tyrant. `SeatCheck` fails one that can't be walked through.
- **The road is brought round** (`SeatBuilder.Frame`, before roads are reserved): every road that came to the seat is cut at 1.3 of its radius and carried round a ring 12 m outside the walls to the nearest gate there's ground to, the shorter way and then the longer. The first road's gate is the great one (`SeatPlan.GateSide`).
- **It's sited where there's room** (`SeatBuilder.RoomAt`, read by `SiteStage`): as the frame is known before the site is picked, a spot whose ways in would open onto a cliff or the sea, or that would take a hill for its floor, loses to one that wouldn't. What's still in the way of the yard, the back and the ground outside each way in is opened and laid level (`OpenGround`, `LevelGround`); the corners are left, so a land with little rock keeps it and a wall that comes to a cliff stops there.
- **Once it's laid out its whole ground is kept** (`Finish`), so no boulder or wild tree comes up inside it.

In the frame +x is the right of the screen and +y the front. A piece is raised facing `Vector2.UnitY` to face the camera.

## How one is built

Each kind has a builder (`World/Generation/Seats/`: `CitadelBuilder`, `OssuaryBuilders.cs`, `ReichBuilders.cs`), a `SeatBuilder`, which is a `PlaceBuilder` (`PLACES.md`), drawing every decision from a fork of its own. Its pieces are `PlaceSpec` ids grown by `PlaceForge` from `Modelling/Procedural/Architecture/Seats/` (headless, on worker threads): `Fortress` and `SeatKit` (walls, towers, gates, the hall, masonry), `SeatDressing` (fires, banners, what a tyrant leaves), `OssuaryWorks`, `ReichWorks` (the dome, tanks, furnace, dummy, heaps) and `ReichField` (the colossus, pylons, stand, pavilion, wagons, cage).

- **`Raise`** stands a piece the seat is built of. What's solid about it is its hitbox rule's parts (`Hitboxes.Of`), put down as `Keepout`, not as building, as a wall's end stands inside the tower it meets and two buildings may not touch; what the chart draws of it is said apart with `Chart`, a little inside it. Everything else goes down with `PutBox` and `PutRound` as in any place, and is refused on what's kept, which is how the yard and the ways in stay clear.
- **A tyrant that isn't alive is the game's own animal** (`SeatBeasts`): grown by `SaurianForge`, posed by `CharacterAnimator`, and taken as it stands, triangle by triangle. The colossus is cast from it roaring and the foundry's dummy sewn from it at rest, so both are the animal the hero is about to fight. Each takes about two seconds to grow.
- **A new piece** needs its line in `PlacePiece` (on the end), its case in `PlaceForge.Make`, a rule in `Hitboxes.Places.cs`, and if it's what a kind is built round, a line in `SeatCheck.Signatures` and `Slowness` 3, which is what allows it more triangles.

## What the stills taught

All of this was judged in stills from the game's camera, and these are what they changed:

- **An animal is side on.** Facing the camera a colossus 18 m long was a dark lump on a block. Along x its outline says what it is, and nothing is foreshortened, so it can be as long as the back of the seat allows: 32 to 36 m.
- **What a seat is built round has to be big.** The living tyrant is some 20 m long, and a furnace 7 m across beside it read as a chimney.
- **The yard's floor is quiet.** Sand, or mud. Ash is mottled with orange and chippings with dark patches, and a tyrant's marks have to be read off the floor. The paving paint over a whole seat is the cobbled look a person called cheap: it's for aprons and ways.
- **Glass and glows are deep and weak**, and no two panes alike, or a dome is flat orange. Oxblood in the sun goes scarlet, so cloth that faces up is darkened.
- **Puddles on sand or pale ground are black holes.** None in a yard.
- **Walling runs in courses only on faces laid out with their axes** (`SeatKit`), and dressed stone is in blocks no longer than a stone is.

## Costs

A citadel is some 150,000 triangles in its models and 180,000 as drawn, a foundry or a triumph 110,000 to 135,000 (47,000 of it the animal), a hatchery 110,000, a war camp 150,000 in its models and up to 500,000 as drawn with forty wagons, and the Ossuary's 50,000 to 80,000; 20 to 40 lights each. `SeatCheck` holds a seat to 1,100,000 drawn, 120 models and 60 lights.

Measured once, at a citadel against the keep it replaced (seed 9, `--perf`, 3440 x 1369, three runs each, on a machine doing other things): 16.0 to 17.5 ms of GPU against 15.2 to 15.6, so half a millisecond to two more. One millisecond of that is the point light pass (0.25 ms to 1.3), which thirty lights with a long reach make of a yard: shortening the great braziers' reach from 25 m to 19 took off a tenth, so it's the number in view and not those eight. The other seven kinds haven't been measured, and a war camp's forty wagons are the one to look at first.

## The game's side

`Chronicle` names the seat and says two lines of it by kind (`SeatLines`), `Warbands` says who holds it (the Reich's own, or the dead, the rotting or ghosts), `Territory` makes an Ossuary seat's ground the dead's, and `AmbienceSystem` makes it night at a barrow. `PackStage` keeps the garrison out of the yard by the seat's own heart (`SettlementStage.KeepHeart`, `KeepYard`).

## Checking

```
dotnet "Dino WorldLab/bin/Release/net8.0/Dino WorldLab.dll" seats --count 100            # every seat: what it's built round, its budget, its tyrant, its three ways in, its road
dotnet "Dino WorldLab/bin/Release/net8.0/Dino WorldLab.dll" seats --kind foundry --count 6 --pieces true
dotnet "Dino WorldLab/bin/Release/net8.0/Dino WorldLab.dll" seats --list true            # seeds by kind
dotnet "Dino WorldLab/bin/Release/net8.0/Dino WorldLab.dll" check --count 100            # and --layout linear, and --people 150 --packs 100
dotnet "Dino WorldLab/bin/Release/net8.0/Dino WorldLab.dll" hitboxes --seed 8 --count 8  # lands with a hatchery, a citadel, a triumph and a foundry
```

`check`, `seats` and `hitboxes` take a land's kind by where it comes in the run, so a seed that fails in a run of a hundred is looked at again with its `--archetype`. Then `Dino NetLab arenas`, `lands`, `foes` and `game`, `Dino BuildLab check` and `Dino AnimLab check`. In the game: `seats --seed N --count 1` says where a seat is, its tyrant stands 0.06 of its radius south of that, and `--seed N --at x,z --calm true --look 95` shows the whole of it. Seeds to look at: citadel 9, hatchery 8, foundry 15, triumph 11, war camp 5, boneyard 2, bileworks 3, barrow 1.

## What nobody has done

**Nobody has walked into one, or fought in one.** Everything was judged from stills. Ask first whether each reads as the boss's place at a glance, whether any of the eight feels thin beside the others (the boneyard and the bileworks have least to them), whether the colossus and the dome are grand enough from the game's own zoom, where only part of a seat is on screen, and whether the road round the walls reads as a road or a detour.

Left out, and worth doing: a seat's pieces in the asset lexicon; a fire in the colossus's mouth; the boneyard's and the bileworks' middles built up as the Reich's were; anything the seat does in the fight, which is the arenas' alone for now.
