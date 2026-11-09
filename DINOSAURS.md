# Dinosaurs

How a dinosaur is grown: its body, flesh, skin, colours and hard parts, what the Saurian Reich puts on it, and what the Ossuary does to it. Read `LORE.md` for who they are and `ANIMATION.md` for how they move. This is what they're made of.

Status: built, and checked by machine in the anim lab and on a running server. **Nobody has seen one move but in the lab's pictures.** See "Where it stands" at the end.

## The one idea

**A dinosaur is a species and a seed.** Nothing is stored. A model's name, such as `dino:raptor.reich.3`, is everything there is to know about it, and any thread on any machine grows the same animal from it (`SaurianSpec.Parse`). A species is ranges (`SaurianCatalog`): it's the only hand-written thing about a dinosaur, and everything else is drawn from it by name through `Genes`, so a new number moves nothing else.

```
  SaurianSpec: species, seed, what's been done to it, harnessed or not
        |
   SaurianBody: every measurement, drawn from the species        ---> BuildRig: the bones, from the same numbers
        |
   SaurianSculpt: the flesh as a field of signed distances
        |                               \
   SaurianSkin: surface nets,         SaurianRot: wounds carved into it
   decimation, normals from the field
        |
   SaurianDress and SaurianPaint: which bones each vertex hangs from, and its colour
        |
   SaurianParts: teeth, eyes, claws, horns, frill, crests, quills, spines
        |
   SaurianHarness / SaurianRot / SaurianBones: what the Reich and the Ossuary add
        |
       Mesh
```

The rig is made from the same body as the skin, so the bones are always where the flesh is, however the seed came out. `Dino Engine/Modelling/Procedural/Saurian/` is headless and touches no GPU, which is what lets `Dino AnimLab` grow and draw them with no window, and the game grow them on every core at once.

## Species

There are seven, and a new one is an entry in `SaurianCatalog` and a kind in the game's `Bestiary`:

| Species | After | Length | Skin budget | Form |
|---|---|---|---|---|
| Raptor | Deinonychus | 7.6 to 8.6 m | 6,500 | On two legs |
| Skitter | Compsognathus | 2.2 to 2.7 m | 2,600 | On two legs |
| Spitter | Dilophosaurus | 8.4 to 9.4 m | 6,500 | On two legs |
| Ravager | Allosaurus and Carnotaurus | 11.5 to 13 m | 9,000 | On two legs |
| Tyrant | Tyrannosaurus | 14.2 to 15.4 m | 15,000 | On two legs |
| Hornback | Triceratops and its kin | 9.4 to 10.6 m | 10,000 | On four |
| Giant | Brachiosaurus and Giraffatitan | 37 to 41 m | 50,000 | On four pillars |

**The giant is the Haven's alone** (`LORE.md`). It's `Free`, so it can't be harnessed, rot or be bones, and `SaurianSpec.Parse` refuses any name that says it is. It isn't in the `Bestiary` either, as nothing fights it. Its skin is finer than any other's by far, as it's the one dinosaur a player can stand under and look up at, and the one in the picture behind the menu.

Lengths are the game's metres, where a person stands 4.7 m tall, which is about 2.7 times life: a raptor's head is about a person's height. Proportions are shares of the whole length, taken from skeletons (Hartman's for the tyrant) and then bent towards what reads from 33 m up, which is mostly the head, the tail and how it stands. Everything else on a species is a range: how long the skull, neck and trunk are, how deep and wide the chest, how folded the legs stand, the head's depth, width, snout and profile, teeth, horns, crests, frill, quills and spines, and three or four colour schemes, each of which an animal drifts from.

So two raptors differ in length, build, posture, the shape of the head, how many teeth, whether they have a mane, which scheme they wear and where its stripes fall, and `MotionStyle.Individual` makes them move differently too.

## The body

`SaurianBody` is every measurement of one animal. It's built around a line (`SaurianAxis`), from the tip of the tail to the back of the skull, down the middle of the body: Bézier curves from the tail tip to the hips, sagging a little as a tail does, then to the shoulders, up the neck in the S every theropod has, and along the skull. It's sampled finely, and everything along a body is said by how far along that line it is: how thick the flesh is, which bone a stretch of skin hangs from, and which way a hide's stripes run.

**A giant is a sauropod** (`SaurianForm.Sauropod`, `BuildSauropodAxis`): its shoulder stands higher than its hip (`Withers`), its neck is longer than the body in front of its tail and goes up from the shoulders as a giraffe's does, as the necks of living animals at their ease say it did, and its tail is carried clear of the ground, drooping to the tip. Its legs are columns on round feet (`BuildPillars`), with a thumb claw on each forefoot and three claws on each hind one, as a sauropod's feet had, and its head is small and long with a dome over the nostrils (`Arch`, after Giraffatitan's), a blunt mouth, and no teeth showing, as a browser's lips cover them.

Legs are hip, knee, ankle and the ball of the foot, **built folded, as they stand**, as that's where a skin bends best from. Small fast animals stand more folded on long shins and giants on near columns, which is `Crouch` and `LegBones`. Arms are carried folded too, and on four legs they're forelegs. The head has a frame of its own, from the back of the skull, which is where it turns on the neck.

## Flesh

`SaurianSculpt` makes the flesh a field of signed distances (`SaurianField`): squashed rounded cones down the line of the body, a belly hanging below it, muscle on the thighs and calves, and a head from a few ellipsoids along the skull, deep and wide at the back, narrowing to the snout and sitting more above the line of the mouth than below it. Pieces join with a smooth minimum, so a body is one surface. **A limb is blended into the body only near where it joins**, or a thigh would melt into the tail all the way down.

The jaw is a field of its own, the bottom of the same head cut along the lip line, so the mouth can open with real flesh on both sides of it.

## Skin

`SaurianSkin` turns a field into a mesh:

1. **Sample it far finer than can be drawn.** `CellFor` works out a cell from the body's surface area, so the surface comes out at about three times what it's allowed to keep: a thick animal has far more surface than a slender one of the same length.
2. **Surface nets** make even triangles of it, and loose bits where two pieces nearly touched are swept up.
3. **Quadric decimation** (`Rock/Decimator`, after Garland and Heckbert) spends the budget where the shape needs it, **with no edge longer than a forty-second of the body**: anything skinned needs vertices along it to bend at, however flat it is there, and a decimator left alone makes a flank one long triangle that folds wrongly when the leg moves.
4. **Normals come from the field**, not the triangles, so the light is smooth however they fell.
5. **One way of laying the texture to a triangle**, round the body, round each limb or from the side of the head, and vertices are split wherever two triangles disagree, so a texture never smears across a seam.

A skin hangs from **two bones a vertex at most**, which is what the shaders read (`SaurianDress`): along the body a stretch of skin belongs to whichever bone that stretch of the line is and blends into the next across each joint, and a limb's skin is its own bones', blending at the knee and ankle and softly into the body where it joins.

It's coloured in its vertices, on the gear materials (`GearMaterials`), with leather's grain for a hide, so there's no texture to pay for and nothing to load. **Every colour here is the albedo the game draws.** The shader multiplies a vertex's colour by its material's own, and leather is brown, cloth off-white and bone grey, so `SaurianMesh.Build` divides each colour by `GearMaterials.AlbedoOf` its material. Before that, every hide in the game came out a third as bright as it was written, and nearly black: the lab draws no textures, so only the game showed it. The anim lab multiplies the same albedo back in, so its pictures show what the game will.

## Colour

`SaurianPaint` works out a colour at each point from **where it is on the body, not in space**: how far up the side it is, from the belly to the spine, and how far along. So stripes run across the back and rings go round the tail whatever the mesh is like, and every pattern is something living animals wear (`HidePattern`): stripes, spots, rings, a mask through the eye, saddles and mottling. Everything is dark above and lighter below, as nearly every animal is and as *Sinosauropteryx* is known to have been. Feet and what's near the ground are darker, and a throat or crest that's shown off is the scheme's display colour. A giant's patterns are faint (`Contrast`), as something with nothing to hide from or ambush needs no bold ones, and what it shows off is the dome over its nose and a ring round each eye. Its eyes are dark and soft, as a grazer's are.

Colours are albedos under a sun of about 13, so all of them are deep, as anything pale goes white. Judge them in the game, as the lab's pictures are brighter.

## Hard parts

`SaurianParts` puts on what's solid, each hanging from one bone: teeth along both jaws, biggest a little way back from the front as Sue's are, inside the lips or outside them as the species has it (`Lips`, after Cullen and others); eyes in their sockets; toes and claws, with a raptor's killing claw held up off the ground; fingers, two on a tyrant and three on a raptor; horns over the eyes and on the nose; a horned grazer's frill, solid on some and edged with spikes on others; a spitter's thin crests; a mane of quills down the neck and back with short feathers on the arms, as dromaeosaurs had; and a row of low spines.

**Anything smaller than a pixel and a half from the game's camera isn't made at all** (`Smallest`, 4.5 cm). A body is paid for thousands of times over.

## What's done to them

The same seed is the same animal whatever's been done to it, so a raptor's bones are that raptor's bones (`SaurianCondition`).

- **In harness** (`.reich`, `SaurianHarness`): an iron collar and its bolts, oxblood straps round the chest and belly joined down the back, and on the big ones an iron mask over the snout, spikes, and a plate on the back with a pole and the Reich's pennant, oxblood with its mark in brass on both sides (`SaurianHarness.Mark`). `SaurianPaint` adds the war paint: an oxblood band over the snout and a ring round the tail. Black iron, oxblood and brass are the Reich's alone.
- **Rotting** (`.rot`, `SaurianRot`): the living colours drained, greyed and gone green with bruising (`Sicken`), three to five wounds carved into the field where there's most to rot, ribs showing in the worst of them, a sick light in the eyes, and what keeps it going: an iron collar, a glass of glowing bile on its back, brass tubes from that into its neck and bolts in its skull.
- **Bones** (`.bones`, `SaurianBones`): the skeleton the flesh was measured from. The skull is the head's field with the openings a theropod skull has cut through it, the backbone is counted as Sue's is, ten in the neck, thirteen in the back, five over the hips and forty down the tail, and the ribs follow the shape the flesh had. A cold light burns in the eyes and where the heart was.

## The rig

`SaurianBody.BuildRig` makes a `Rig` with a `SaurianFrame`, which is what tells the animator it has a dinosaur. It adds to a person's bones a jaw, a neck in two (`Neck`, `NeckTop`), a tail of five (`TailBase`, `Tail1` to `Tail3`, `TailTip`) and toes, all of which move, and says how thick the body is at each bone, so it falls as what it's made of. A giant's neck is in five (`SaurianFrame.LongNeck`: `Neck`, `Neck1` to `Neck3`, `NeckTop`), so that it curves rather than bends, and on anything else those three sit on the neck's root and move nothing. That's 31 bones, and the shaders take 32 (`u_Bones[32]`). How it moves is in `ANIMATION.md`.

## In the game

- A kind's `Look` names its species and what's been done to it, as `dino:raptor.reich.`, and `Variants` how many seeds there are. `CharacterFactory.Prepare` grows every one a world could send at the loading screen, on every core at once, and only the upload is left for the main thread.
- A dinosaur is far thicker than a person, so its body is solid that far out: `EnemyKind.Bulk` is its radius at a scale of one (`RadiusAt`), for the server's body, the motor and the parade alike.
- `CharacterFactory.BearingOf` gives it its own rig and `SaurianForge.StyleOf`, and `SkillMotions.Of` picks its gestures from a page by what it's built like: a theropod bites, stomps, pounces, spits, sweeps its tail and roars, and a horned one gores, tramples and bellows.
- Its voice is in `Sound/Content/Saurians.cs`, picked by `GameSounds.DinosaurVoice` from its species and condition, and anything with legs over 1.6 times a person's shakes the ground as it walks (`Saurians.Tread`).
- **The giants aren't sent by any server.** They're the Haven's, only ever looked at, and this client's alone: a `Herd` decides where each stands and what its head is doing, and `GiantShow` is their bodies (`WILDLIFE.md`). Each takes three seconds to grow, so they're begun the moment the game starts, before anything else, as they're in the picture behind the menu (`CharacterFactory.GrowGiants`), and nothing ever waits for them.
- Its books are on `Shelf`, and what comes first in its list rests after use, or it would do nothing else: an enemy does the first thing that's ready and reaches. A blow is a cone, as a rectangle is a beam and a circle a nova, which go off like spells.

## Adding things

**A new kind of dinosaur of a form there already is**: an entry in `SaurianCatalog`, a line in `SaurianForge.StyleOf` if it moves like none of them, a kind in the `Bestiary` with its `Look`, `Variants` and `Bulk`, and its voice in `GameSounds.DinosaurVoice`. Look at it with `zoo --species` and `sheet --body dino:name.0`.

**A new form** (an armoured one, as the giant was a long-necked one): a `SaurianForm`, its axis and legs in `SaurianBody`, and whatever its head needs in `SaurianSculpt`. Its rig follows from the body.

**A new thing done to them**: a `SaurianCondition`, a word for it in `SaurianSpec.Parse`, and a way of moving in `StyleOf`.

## What will break it

- **Drawing a number from another one's stream.** Everything comes from `Genes` by name, so a new parameter moves nothing else: draw it by a name of its own.
- **A long edge across a joint.** The decimator's `longestEdge` is what keeps a flank bending with the leg.
- **More than two bones on a vertex.** The shaders read two.
- **Patterns in world space.** They'd slide over an animal as it moves. Paint goes by the body's own coordinates.
- **Pale colours.** They go white under this sun.
- **Forgetting a material has a colour of its own.** Leather, cloth and bone multiply what's drawn on them, which `GearMaterials.AlbedoOf` says and the textures are built from, so they can't drift apart.
- **A detail under 4.5 cm.** It costs triangles on every animal and nobody can see it.
- **Touching anything shared or a GPU.** It runs on worker threads during loading.

## Testing

```
dotnet "Dino AnimLab/bin/Release/net8.0/Dino AnimLab.dll" zoo --variants 4 --as living,reich,rot,bones --out zoo.png
dotnet "Dino AnimLab/bin/Release/net8.0/Dino AnimLab.dll" sheet --body tyrant --do roar --view side
dotnet "Dino AnimLab/bin/Release/net8.0/Dino AnimLab.dll" grow --dino dino:tyrant.0,dino:raptor.bones.0
dotnet "Dino AnimLab/bin/Release/net8.0/Dino AnimLab.dll" check
dotnet "Dino NetLab/bin/Release/net8.0/Dino NetLab.dll" foes
```

`zoo` stands one of each, or as many variants as asked, living, harnessed, rotting or bones, with no GPU. `sheet` does anything to any body (`raptor`, `skitter`, `spitter`, `ravager`, `tyrant`, `hornback`, or any model name such as `dino:raptor.rot.2`), and a dinosaur's own scripts are `chomp`, `roar`, `tailsweep`, `stomp`, `pounce`, `spit`, `gore` and `trample`. `grow` says where the time goes. `check` includes every dinosaur in every situation, and thrown at the buildings and trees of a real world. In the game, `--parade tyrant,war-raptor,hornback` stands them in front of the hero, and `--brawl` the same awake.

A lab can't say an animal looks alive. Judge them from the game's own camera, moving.

## Where it stands

Growing one takes 0.16 to 0.9 s on one thread: a raptor about half a second and 10,000 triangles, a tyrant 0.8 s and 22,000, its bones 0.3 s. All 54 that the game can send are grown at once at the first loading screen and kept, which on a machine with few cores is a few seconds. A body's pose costs what a person's does, about 2.5 µs a frame, and allocates nothing. A giant takes 3 s and has 53,000 triangles, and its pose 5 µs, as its neck is worked out afresh each frame.

**Nobody has seen one but in the lab's stills.** Ask how they look in the game, and especially:

- Whether a tyrant reads as a tyrant from the game's camera, and as big enough as a boss.
- Whether the harness and war paint tell the Reich's from the wild at a glance.
- Whether the rotting and the bones read as different things.
- Whether the giants read as gentle and as big as they are, close up in the Haven and far off behind the menu, and whether their faces are kind.

Not built:

- An armoured one, and anything that flies or swims.
- Blood and wounds that come from a fight. A dinosaur looks the same at the end of one.
- Feathers beyond the quills. The evidence says raptors were feathered all over, and the quills are a nod to it that reads from up here.

## Where this came from

- Michael Garland and Paul Heckbert, *Surface Simplification Using Quadric Error Metrics* (1997): the decimator.
- Sarah Gibson, *Constrained Elastic Surface Nets* (1998): surface nets.
- Inigo Quilez on signed distance functions and the smooth minimum.
- Scott Hartman's skeletal reconstructions: proportions, above all the tyrant's.
- Sue (FMNH PR 2081): the tyrant's teeth and backbone.
- Thomas Cullen and others, "Theropod dinosaur facial reconstruction and the importance of soft tissues in paleobiology", *Science* (2023): lips over the teeth.
- Fiann Smithwick and others, "Countershading and stripes in the theropod dinosaur *Sinosauropteryx*", *Current Biology* (2017): dark above, light below, and a banded tail.
- Alan Turner and others, "Feather quill knobs in the dinosaur *Velociraptor*", *Science* (2007): quills and arm feathers on raptors.
- John Ostrom on *Deinonychus*: the killing claw and the tail stiffened into a rod.
- Adam Marsh and Timothy Rowe on *Dilophosaurus* (2020): its thin paired crests.
- Michael Taylor, "A re-evaluation of *Brachiosaurus altithorax*" (2009), and Gregory Paul's skeletals: the giant's proportions, and Giraffatitan's domed nose.
- Michael Taylor, Mathew Wedel and Darren Naish, "Head and neck posture in sauropod dinosaurs inferred from extant animals" (2009): a neck held up at its ease.
