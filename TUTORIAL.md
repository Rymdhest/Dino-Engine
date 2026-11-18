# The tutorial

Every new hero starts here, before they've seen the Haven: one land, always the same, so that it can be tuned until it's the start a new player should have. It plays as a land in every rule that isn't listed below.

Status: built on 2026-11-18. Proven by `Dino NetLab tutorial` (30 checks), `Dino WorldLab check --archetype tutorial`, a bot clearing it and a real click on "Enter the world" from the menu. **Nobody has played it.** The layout, the beast and the words are first guesses, there to be tuned.

## What it is

- **One way through** a vale walled by its gorge: four clearings joined by passes, laid out by hand (`TutorialLand.Rooms`, `Passes`) and not drawn, from the landing in the south-west to the lair in the north-east, about 480 m.
- **The landing** is a camp with its fire lit, a tent and a signpost, and **no waygate**: nobody has shown a new Defender the roads yet (`SettlementStage.BuildTutorialLanding`).
- **Two places to free**, each in the clearing it's given (`TutorialLand.Places`: ruins, then one of the Reich's pens), held as any are, each with a chest.
- **The lair** (`SiteKind.Lair`, `SettlementStage.BuildLair`) at the end of the way: one **Rare** dinosaur in the middle (`Tutorial.Beast`, a ripper), a chest to its left, and behind it, to the north, a land waygate facing the camera, dark.
- **No keep, tyrant, village, shrine, ring of stones or other waygate**, and packs stand further apart than a land's (`TutorialLand.PackSpacing`).
- **Smaller than a land**: 640 m a side (`TutorialLand.Size`), against 830 to 1,260 m.
- Always **the same light and weather**: a clear morning, fair (`TutorialLand.Archetype`).

What it asks, in the corner: free its two places, and bring down its beast. Both pay as a first land does.

## Its rules

| What | Where |
|---|---|
| Nothing is Magic or Rare but its beast, which is Rare and nothing more: not a tyrant's loadout, no boss health, no orders when it falls | `EncounterSystem.Field`, `SpawnEnemy`, `Combat.DropLoot` |
| Its beast is the land's main objective, as a tyrant is (`Actor.Tyrant`), and is marked as sought | `Chronicle.WriteTutorial`, `GameSession.Sought` |
| The waygates can't be called: the button is greyed and the server refuses | `GameSession.CanCall`, `HeroAction.CallRoads` |
| Falling is as falling in the Haven: no gold lost, Rise on the death screen, and the hero stands up at its landing | `ServerSession.HeroFell`, `ServerRoom.UpdateDown`, `StandUp`, `DeathScreen.Primary` |
| Its waygate wakes only when its beast falls, and then shows the Haven and is used to go there, at once | `ServerSession.UpdateDiscovery`, `EncounterSystem.OpenTutorialGate`, `InteractionSystem.Use` |
| Freeing it climbs nothing: whoever comes out of it has the whole climb before them | `ServerSession.Judge` |
| Who starts in it: a hero who's never been anywhere (level 1, no experience, nothing freed). Nobody comes out of it without experience, so nobody is sent back. A hero who quits halfway is in a land and comes back to it, as anyone does | `Tutorial.Starts`, `ArpgGame.FirstWorld` |

## Tuning it

Everything about its ground is in `Dino Engine/World/Content/TutorialLand.cs`: its seed, size, clearings, passes, which place stands where, how sparse its packs are, its biomes and its light. Everything about how it plays is in `Dino Defenders/Progression/Tutorial.cs`: its level, its beast, and every line it says. Changing either changes the land every new hero starts in, which is the point; a hero saved halfway through it comes back to the new one.

- A clearing is a centre in metres from the world's corner and a radius: +x is to the left on screen and +z up it, away from the camera. Keep every clearing 70 m from the edge, and clearings that aren't neighbours on the way 110 m apart past their radii (`LandPath.LeastBetween`), or a wall won't stand between them.
- A place is any `SiteDefinition` that's held. It stands as near its clearing's middle as there's ground for it (`SiteStage.PlaceTutorial`), and `WorldValidator.CheckTutorial` fails one outside its clearing.
- The beast can be any `EnemyKind` in the `Bestiary`. It's grown at the loading screen with the rest (`EncounterSystem.Foresee`).

## Seeing it

`--tutorial true` starts there, as a new hero does; `--site Lair --calm true` at its end. `Dino WorldLab where --seed 4127 --archetype tutorial` lists its places, `map` draws it and `check` holds it to the rules above. `Dino NetLab tutorial` plays it through a client on a line: who starts there, that it's the same land twice, that only its beast is Rare, that calling is refused, falling and rising, and that its gate is dark until its beast is down and then goes home without climbing.

After any change here run `Dino WorldLab check --seed 4127 --count 1 --archetype tutorial`, `Dino NetLab tutorial` and `lands`, `Dino BuildLab check`, and look in the game with `--tutorial true`.

## Decided, and open to change

- **A ripper** for its beast, as it pounces and sweeps and so shows what a mark on the ground means; a war raptor would be easier and a hornback harder.
- **Ruins, then one of the Reich's pens**, for its two places. A bot that teleports into the pen falls there at level 1, so it may be too crowded for someone new.
- **"Two objectives"** read as its two places to free, with its beast after them as the land's main objective.
- **Rising at its landing** when a hero falls, rather than losing the land.
- **Its name** is drawn from its seed: "The Vale of Halilath".
