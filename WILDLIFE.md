# Wildlife

Everything alive in a world that the game's rules know nothing about: leaves coming off trees, rabbits, crabs, birds, butterflies, frogs, fish, bats. How it works, and how to add more.

Status: built, and tested hard by machine, in real worlds with a real hero on foot. **Nobody has watched it move.** See "Where it stands" at the end.

## The one idea

**It's only ever looked at.** Nothing here can touch the game. It runs on a client alone and each machine has its own: nothing is sent, nothing is saved, it takes no clicks, and it stops nobody. Two players in one world see the same rabbits in the same meadow, doing different things.

That's why it isn't entities. Like grass, it's a plain simulation that the rest of the engine can't see: `WildlifeSim` keeps its own lists, and `WildlifeSystem` is the only thing that talks to it. There's a lot of it, it comes and goes all the time, and moving things between archetypes for what a rabbit is doing would cost more than the rabbit.

```
  world's maps    wind    weather    whatever walks through plants
        \           |        |          /
         \          |        |         /
          +---- WildlifeView, each frame ----+
                          |
                     WildlifeSim
                     /          \
          Parties of Critters    LeafFall
                     \          /
                 bones and tints, by shape
                          |
                  WildlifeRenderer          one instanced call a shape
```

`Dino Engine/Wildlife/` is headless, and so is `Modelling/Procedural/Wildlife/`, which is what lets `Dino LifeLab` run all of it in real worlds with no window.

## Where the ideas came from

| Source | What's used |
|---|---|
| Sucker Punch on Ghost of Tsushima (PlayStation Blog; Rockenbeck, GDC 2021) | Particles that spawn rigged animals; leaves that land on terrain and drift on water; birds at forest edges, herons in marshes; fauna frightened by "a very low value wind sphere around characters"; crabs that keep their distance; particles that look ahead and rise over slopes |
| Andersen, Pesavento & Wang 2005, *Analysis of transitions between fluttering, tumbling and steady descent of falling cards* | How a leaf falls: equations 5.1 to 5.3 with 4.7 to 4.9, and their constants |
| Reynolds 1987, Boids; Ballerini et al. 2008 | Flocks. Each bird heeds its nearest few, and not everyone in range |
| Hildenbrandt, Hemelrijk et al. 2010, StarDisplay | Birds roll to turn, and a flock needs somewhere to be or it only wanders off |
| Flight initiation studies (Morelli et al. 2019, *Contagious fear*) | A bigger flock goes sooner, for having more eyes |
| Escape waves in sandpipers | One bird going sets off the next, in a wave out from it |
| Bertram & Gutmann 2009, and lagomorph reviews | A rabbit's gait is the half-bound: forelegs land one after the other and the hind legs swing past them |
| Blickhan & Full 1987; autotomy studies of *Ocypode* | How a crab runs: sideways, and able to stop or turn back within a few strides |
| Tobalske on flap-bounding | Finches shut their wings between bursts |
| Work on erratic butterfly flight | The body rises on each downstroke, and the track wanders |

## Who lives where

**A place's residents are a function of where it is.** The world is cut into 28 m cells, and each has one group at most, drawn from a hash of the world's seed and the cell, from what its biome says lives there (`BiomeDefinition.Life`) and what sort of place it is (`Habitat`, from `LifeLand.HabitatAt`). So a meadow's rabbits are always in that meadow, and they're there again when anyone comes back. It's the same idea as plants: nothing is stored, and a whole world is alive for the price of what's on screen.

They're only made while they could be seen. `Survey` settles every cell that comes within `Margin` of the view, so everything's there before it's looked at, and whatever gets `LetGoMargin` beyond it is let go. All that's kept is who's been frightened off, and until when (`BackAfter`).

The view is the camera's corners taken down to the ground, as a wide screen sees far more to the sides than up and down: at 5120x1440 it's some 180 m by 45.

A little weather and hour: `AwakeNow` thins a place out in rain by which cells they are, so it's always the same ones that are left. What can't be out in the rain goes when it starts, by `Party.Shelter`. Night is `NightfallSingleton`, so birds aren't seen in a churchyard and bats are.

## Fright

**Anything with a `PlantContactComponent` is frightening**, so heroes and enemies are the same with no list of who's who. It's Tsushima's wind sphere.

`WildlifeSim.Fright` says how near the worst of them is, as a share of how close this creature will let anything come. Under 1 it goes. Every `Shy` number is someone running at it: standing still is half as frightening, so someone who stops can watch things come back.

`Nature.Startle` is whatever can be heard: `GameSounds.Hit` calls it for every blow. A `Wind.AddBlast` frightens by itself, out to half as far again as its wind.

## Leaves

`LeafFall` is one array of structs. A leaf comes off a real crown, with that tree's outline and colours, falls as a card falls, rides the wind the grass bends in, and then lies where it landed or floats.

**A falling card is a table.** Andersen, Pesavento and Wang's equations don't depend on where a card is, and wind only moves the frame they're solved in, so `FallingCard` solves them once and every leaf is only somewhere in the result. It costs a lookup, can't blow up, and doesn't care about the frame rate. One number, the dimensionless moment of inertia, decides how it falls, and the four used are from their figure 3: `Flutter`, `Tumble`, `Mixed`, `Sink`.

**Check any change here against the paper.** A first version had the gravity term as `π sin θ`, from memory, and looked like a perfectly good falling leaf: it was only comparing the regimes with figure 3 that showed I*=3 was chaotic when it should be a small broadside swing. The term is `sin θ`. `check` holds the regimes now.

A leaf's speed and size are chosen, not derived: real ones flutter at several hertz, and a speck that flickers quicker than a couple of swings a second reads as a fault in the picture. So `Rate` is set to keep it there, and only the shape of the motion comes from the solution.

- **It's the tree's own leaf.** `KindOf` reads what a tree's cards are painted from: the `LeafForm` is the outline and `Foliage.Colours` the colours. Needles and fronds have nothing worth following, so those trees drop nothing.
- **It's geometry, not a card.** A leaf's a few pixels, its edge is what's seen, and that gets antialiased for nothing. Lobes need vertices, or they alias into a zigzag (`LeafBody`).
- **It comes off the outside of a crown.** This camera is nearly overhead, so anything falling inside one is behind it the whole way down.
- **Anything green has started to go.** `Wither` mixes it towards brown.
- **A gust can be seen going through a wood**, by what it shakes loose: `Shed` thins from the strongest it could be, by the gust at each tree.
- **The wind is `Air`**, a coarse grid of `WindField.Sample`, as a full sample costs as much as hundreds of leaves. It's what plants read too, levelled off by `PatternSpeed`.
- **`AmbientMotes.FallingLeaves` and `Petals` mean something else now:** they're how much a biome sheds. Nothing draws them as motes. The guiding wind goes through `LeafFall.Guide`, and has a leaf of its own so it works in a wood of pines.

## Being seen at all

**Grass is the problem, and it's most of what isn't obvious here.** Blades reach 2.6 m and a rabbit stands 0.6 m. In a first run the rabbits were ear tips, and sparrows simply weren't there.

- **What hops sits on top of the grass**, on the seed heads, as finches do. `LifeLand.GrassTop` is how high it comes up, which is well short of how long a blade is.
- **What walks stays out of long grass** (`Flock.Fits`, `WildlifeSim.Suits`). A crow in grass up to its neck isn't anything.
- **A rabbit presses a form.** Hares really do, and it's what lets one be seen. Grass goes down quickly and rises slowly, so `Rabbit.PressForm` is a short press every `FormEvery` seconds, not a contact held all the time, as each contact is paid for by every texel of the field. It's a length of time and not a frame, or it'd press harder at a lower frame rate.
- **Tall grass is cleared, not pushed through.** A bound's height goes with the grass, so a rabbit in a meadow is seen in leaps.

Everything's a little bigger than life. People are 2.7 times real here, so that's where sizes start, and what's small gets a little more. Things in the air stay low, as the camera's only about 30 m up and a gull at 15 m is the size of a person (`BirdKind.Height`).

**Small things stand on the drawn ground**, `LifeLand.Ground`, which is `TerrainRenderer.SurfaceOffset` above where people stand. That's nothing to someone 4.7 m tall and the whole of a crab.

## Bodies

A body is a `Skeleton`, which is pivots and parents, and a pose struct of a few numbers around 0 and 1, eased by `Spring`, so one way of standing goes into another with nothing to get stuck in. `Solve` writes each bone all the way into the world, so a vertex needs nothing but its bones. Bones turn and scale evenly, so a normal only has to be turned. Model space is +Z forward, +Y up and +X to its left, as it is for people.

`BodyMesh` lofts smooth skin from rings and colours it in the vertices: at a few dozen pixels colour is most of what's seen, and there's no texture to pay for.

- **Rabbit** (`RabbitPose`, `RabbitGait`). Twelve bones. A bound is a function of how far through it is: the hind legs drive, it flies stretched out, the forelegs land first and the hind legs swing past, which bunches the whole animal up between bounds. It turns only at landings, as direction changes with feet on the ground. It freezes first and runs when something's too close, zigzags unless it's nearly caught, and goes to ground if it's cornered.
- **Bird** (`BirdPose`, `Flock`). Ten bones, one body for a finch, a heron and a bat. A wing is an arm and a hand: the downstroke is straight and spread, the upstroke bends at the wrist with the hand trailing, and that difference is what makes it a wingbeat and not a hinge. Folded it's a Z, with the hand back along the arm, or a finch has three tails.
- **Crab** (`CrabLegs`). The same rule as people's feet: **a foot on the ground stays exactly there until it lifts.** Legs step in two sets of four, each when the other is down, and the legs are solved exactly with two bones. It always goes sideways, so it turns its side to where it's going and its face to what it's afraid of.
- **The rest** (`SmallLife`). Butterflies, dragonflies, frogs, leaping fish and bats, each a few lines of how it moves.

Wing beats are capped well under what's real, as a sparrow's 20 Hz at 60 frames a second is a strobe.

## How it's drawn

`WildlifeRenderer` takes one instanced call per shape, reading bones from one buffer filled once a frame (`Wildlife.vert`, bindings 3 and 4, which grass doesn't use), so a flock costs what a bird does. It's the geometry pass like anything solid, through `Model.frag` with `WILDLIFE` defined, so it's lit, shadowed, wet and reflected with no more said.

- **It writes nothing to the characters' stencil.** A mark on the ground is a promise, and it goes over whatever's hopping across it.
- **It isn't `CHARACTER`**, so the focus cutout dithers away what passes between the camera and the hero.
- **What comes and goes thins out** (`fragFade`), as something that's only for looking at should never be seen to pop.
- **A bird's shadow is most of how height is read from up here**, so everything casts one.

## Adding things

**A new animal of a shape there already is**: an entry in `Fauna` and a word in a biome's list. No code.

**A new way of living**: a `Creature`, a `Party` and a `Critter`. Say where it lives (`Lives`), how it moves, and what frightens it.

**A new sound**: a line in `LifeNoiseKind` and in `WildlifeSystem.Sound`, which spaces each kind out, so a hero going through a village isn't a storm of pigeons. The recipes are in `Sound/Content/Life.cs`.

## What will break it

- **Anything in the game reading wildlife.** It's different on every machine and it isn't there when nobody's near. It's always safe to delete all of it.
- **Drawing from a world's random streams.** It has its own fork, and places are hashes: a creature can't have a say in how the land grew.
- **Holding a contact on the grass.** See above.
- **Anything per frame that isn't per second.** Everything here reads the delta, and bounds, presses and wing beats are lengths of time.
- **Sampling the wind per leaf.** Read `Air`.
- **Allocating in a step.** `check` fails over 1 KB a frame. What's alive is made when it settles, and only then.

## Checking it

`Dino LifeLab` runs it all with no window:

- `check --count N --seconds S`: walks someone through real worlds and fails on anything underground, in the water, solid, or up by the camera, and over its budget, and then runs someone at everything, as nothing should get trodden on. It also holds the falling cards to the paper's regimes. It has found real faults: birds flying into cliffs at a leap, rabbits boxed in by trees.
- `leaf`: what each falling card does.
- `sheet --do rabbit.bound --view side`: any body doing anything, on a floor that stays put. `three`, `front`, `top` and `game` are the other views.

In the game: `--bring rabbit,sparrow` puts some near the hero, `--startle N` frightens everything after N frames, for a `--film`, `--lifelog file` writes down what's alive and where on the screen, and `--nolife true` turns it all off to see what it costs. `--biome` finds a wood, and `Dino FloraLab find` says which seed has one.

Judge any of this moving, and from the game's own camera, where a metre is about 30 pixels. A rabbit is only a rabbit in a still if you know it's there.

## Where it stands

Measured on the 7900 XTX at 3440x1369, in a wood with leaves coming down: 124 FPS with it and 124 without, as near as that can be told. It's about 0.07 ms of CPU and 0.03 ms of GPU.

Nobody has played with this. In particular, none of these have been seen moving by a person:

- How many there should be. Those numbers are a first guess: about 30 creatures and a few dozen leaves in the air around the view on average, and many more in a wood that's dropping them.
- Whether gulls are still too big, and whether anything distracts in a fight.
- Whether the wing sounds and calls are right. They pass `Dino SoundLab check`, which says nothing about whether they sound like wings.
- A rabbit that's chased straight at through dense cover can still be brushed past: `check` got within 0.6 m of one. It can't matter, as it isn't there as far as anything's concerned.
