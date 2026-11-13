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

There are sixteen, and a new one is an entry in `SaurianCatalog` and a kind in the game's `Bestiary`:

| Species | After | Length | Skin budget | Form |
|---|---|---|---|---|
| Raptor | Deinonychus | 7.6 to 8.6 m | 6,500 | On two legs |
| Skitter | Compsognathus | 2.2 to 2.7 m | 2,600 | On two legs |
| Spitter | Dilophosaurus | 8.4 to 9.4 m | 6,500 | On two legs |
| Ravager | Allosaurus and Carnotaurus | 11.5 to 13 m | 9,000 | On two legs |
| Tyrant | Tyrannosaurus | 16.4 to 17.6 m | 30,000 | On two legs |
| Hornback | Triceratops and its kin | 9.4 to 10.6 m | 10,000 | On four |
| Giant | Brachiosaurus and Giraffatitan | 37 to 41 m | 50,000 | On four pillars |
| Ripper | Utahraptor and Achillobator | 10.8 to 12 m | 8,000 | On two legs |
| Wader | Suchomimus and Baryonyx | 12 to 13.5 m | 8,500 | On two legs |
| Spikeback | Kentrosaurus and Stegosaurus | 9 to 10.2 m | 9,000 | On four |
| Weaver | Nothing that lived: the Institute's own | 6.2 to 7.2 m | 9,000 | On eight |
| Shagback | Yutyrannus | 10.5 to 11.8 m | 12,000 | On two legs, in a shaggy coat |
| Reaper | Therizinosaurus | 10 to 11.2 m | 10,000 | On two legs, feathered |
| Strider | Gallimimus | 7.8 to 8.8 m | 7,000 | On two legs, feathered |
| Clubtail | Ankylosaurus | 9 to 10.2 m | 10,000 | On four, armoured |
| Domehead | Pachycephalosaurus | 7.2 to 8.2 m | 6,500 | On two legs |

**The tyrant was grown again on 2026-11-13** to be what a land's boss should be, after a person asked for a T. rex as majestic as the giants: its hips at 28% of its length, close to what Sue's are (they stood under a quarter of it, and its legs read as stubs), a sixth longer than it was, with hornlets before its eyes as Daspletosaurus had, deeper jowls, a row of scutes down its back, darker hides, bronze eyes, and a skin twice as fine, laid exactly on its surface (`SaurianSpecies.Close`, as a giant's is). In the game a land's boss is a Unique, drawn 1.7 times its kind's size (`Bestiary.PowerOf`), so a tyrant holding a land is some 27 m from its snout to the end of its tail. In the Reich's hands it wears regalia, not a harness (`SaurianRegalia`, below).

**The six added on 2026-11-13** are from the forms there were, but for the weaver, which is a form of its own. The **weaver** is the Institute's worst idea, a hunter grown from blood that wasn't all one animal's: a raptor's head with eight eyes (`EyePairs`) and a pair of fangs under its snout (`Fangs`), a body like a low shield with eight jointed legs round its edge whose knees stand over its back, a narrow waist and an abdomen swollen into an egg, with ringed legs and a row of arrowheads down its back (`HidePattern.Ringed`, `Chevrons`). The **shagback** is a tyrant's kin in a coat of long filaments, for the snow; the **reaper** a small beaked head on an upright neck with arms as long as it is tall and a scythe for each claw; the **strider** an ostrich of a dinosaur; the **clubtail** low and broad under rows of keeled bone plates (`Armour`), with spikes down its flanks, horns at the corners of its skull and a club on the end of a stiff tail (`Club`); and the **domehead** a grazer on two legs with a dome of bone for a skull, ringed with knobs (`Dome`). A beak is horn-coloured and a dome is what's shown off (`SaurianPaint`).

Three were added on 2026-11-11 from the forms there were, with nothing new in how a body is built: the ripper is a great raptor, deep in the chest with a sickle claw half as big again and a mane of quills; the wader has a long, low, narrow skull with many small teeth, a great thumb claw and a ridge of spines; and the spikeback is the horned form with no horns or frill, its hips high over its shoulders, its small head carried low, and spikes as long as Kentrosaurus's close together down its back and tail (`ScuteSpacing`, how far apart a species' spines stand in their own lengths, 2.6 unless a species says otherwise, as a species constant and not a draw, so it moved no other animal). Spines that small and far apart read as nothing on a spikeback, and a body as deep as a hornback's with no frill to read it by was a lump.

**The giant is the Haven's alone** (`LORE.md`). It's `Free`, so it can't be harnessed, rot or be bones, and `SaurianSpec.Parse` refuses any name that says it is. It isn't in the `Bestiary` either, as nothing fights it. Its skin is finer than any other's by far, as it's the one dinosaur a player can stand under and look up at, and the one in the picture behind the menu.

Lengths are the game's metres, where a person stands 4.7 m tall, which is about 2.7 times life: a raptor's head is about a person's height. Proportions are shares of the whole length, taken from skeletons (Hartman's for the tyrant) and then bent towards what reads from 33 m up, which is mostly the head, the tail and how it stands. Everything else on a species is a range: how long the skull, neck and trunk are, how deep and wide the chest, how folded the legs stand, the head's depth, width, snout and profile, teeth, horns, crests, frill, quills and spines, and three or four colour schemes, each of which an animal drifts from.

So two raptors differ in length, build, posture, the shape of the head, how many teeth, whether they have a mane, which scheme they wear and where its stripes fall, and `MotionStyle.Individual` makes them move differently too.

## The body

`SaurianBody` is every measurement of one animal. It's built around a line (`SaurianAxis`), from the tip of the tail to the back of the skull, down the middle of the body: Bézier curves from the tail tip to the hips, sagging a little as a tail does, then to the shoulders, up the neck in the S every theropod has, and along the skull. It's sampled finely, and everything along a body is said by how far along that line it is: how thick the flesh is, which bone a stretch of skin hangs from, and which way a hide's stripes run.

**A giant is a sauropod** (`SaurianForm.Sauropod`, `BuildSauropodAxis`): its shoulder stands higher than its hip (`Withers`), its neck is longer than the body in front of its tail and goes up from the shoulders as a giraffe's does, as the necks of living animals at their ease say it did, and its tail is carried clear of the ground, drooping to the tip. Its legs are columns on round feet (`BuildPillars`), with a thumb claw on each forefoot and three claws on each hind one, as a sauropod's feet had, and its head is small and long with a dome over the nostrils (`Arch`, after Giraffatitan's), a blunt mouth, and no teeth showing, as a browser's lips cover them. **Its forelegs stand under the front of its chest**, where the neck rises from it (`ShoulderAt`), as a sauropod's shoulder blade lay along its front ribs: under the middle of the chest, as they first were, a third of the trunk went on past them and they were shorter than its withers said, which a person saw at once. Its trunk is a little shorter than it was, the hip-to-shoulder share Giraffatitan's has, which kept its feet as far apart as a herd can turn round in the Haven.

**What walks on eight is a form of its own** (`SaurianForm.Arachnid`, `SaurianBody.Arachnid.cs`): its line runs as any other's does, from the tip of the abdomen, which is the tail's bones, through the waist, which is the hips, to the back of the skull, so the head, the jaw, the eyes and the hide work as on anything else. Its eight legs are built as they stand, round the edge of the body, each a thigh up and out to a knee over the back, a shin down and out, and a foot down to its claw far out round it (`BuildArachnidLegs`), and each is a part of its own (`SaurianPart.SecondL` to `ThirdR`), so that no two grow together.

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

The same field and skin grow the land's own animals, wild boars, wolves and bears (see The land's animals below).

It's coloured in its vertices, on the gear materials (`GearMaterials`), with leather's grain for a hide, so there's no texture to pay for and nothing to load. **Every colour here is the albedo the game draws.** The shader multiplies a vertex's colour by its material's own, and leather is brown, cloth off-white and bone grey, so `SaurianMesh.Build` divides each colour by `GearMaterials.AlbedoOf` its material. Before that, every hide in the game came out a third as bright as it was written, and nearly black: the lab draws no textures, so only the game showed it. The anim lab multiplies the same albedo back in, so its pictures show what the game will.

## The land's animals

A wild boar was the wolf's body squashed long and low, legs out at its sides, and was called a big sausage dog, so it's grown as the dinosaurs are (`Modelling/Procedural/Character/Boar/`, headless, safe on any thread). A model's name is `boar0` to `boar5` (`BoarSpec`, the bestiary's six `Variants`), and that's all it takes to grow that boar. `BoarBody` draws it from its seed by `Genes`, from its age, 0 a young sow and 1 an old tusker (tusks, mane, shoulders and head all grow with it), and measures everything from the withers: a trunk a little longer than it's tall, legs as far apart as three quarters of that, under the body and not at its sides, hips as high as shoulders with the withers standing over them, a head nearly half as long as the trunk carried low, and a short thick neck. It's a wedge: deep and narrow, broad at the shoulders and slim at the hips, as a wild boar is and a farm pig isn't. `BoarSculpt` makes its flesh a `SaurianField`, `SaurianSkin` grows its skin, and `BoarDress` hangs it and paints it: the body and a leg share skin only near the shoulder or hip and on their own side, as a belly that went with whichever leg was nearest was torn into flaps by walking. Its eyes, ears, mane, tusks and tail are laid on after, the mane as two rows of dark tufts along the crest it's built with, as from where the camera is that's what a wild boar is known by. Its coat is the `Bristles` shell, long and coarse with pale tips over a warm, dark hide, and each of the six has the coat `BoarBody.Coats` gives it, as six drawn by chance once came out half black and none brown. It moves as `MotionStyle.Boar` on the same bones as everything else, and `Dino AnimLab check` walks it with the rest. Look with `Dino AnimLab sheet --body boar3 --do run` (any of `boar0` to `boar5`), `grow --dino boar0,boar1`, and in the game with `--parade boar` (it stands straight in front of the hero as the fourth of a parade, as in `--parade restless,shambler,bone-archer,boar`). Nobody has seen one move in the game.

Wolves were the same tubes, so they're grown the same way (`Modelling/Procedural/Character/Wolf/`): `wolf0` to `wolf5` and `direwolf0` to `direwolf3` (`WolfSpec`, the bestiary's `Variants`), and the Pack Hound a hero raises is `wolf`, the first of them. `WolfBody` draws one from its seed by its build, 0 a lean yearling and 1 a big male: long in the leg and short in the body, a trunk about as long as it's tall, a deep narrow chest with the waist drawn up sharply in front of the hips, a level back, a long neck carried up and forward with the head above the withers (carried level with the back, it read as a hyena), a broad skull and a long muzzle, big upright ears, compact paws, and a bushy tail with a black tip hanging to its hocks. A dire wolf is a third bigger, heavier in the shoulder and head, darker, with longer fur (`DireFur`) and its hackles up as two rows of tufts along its nape and shoulders, which is how it's told from the pack it leads. Its coats are a grizzled grey, a tawny, a black and a pale one, and a dire wolf's iron-grey, black-brown or black, each wolf's given by `WolfBody` as a boar's are. From the camera a wolf is mostly its back, so a dark saddle made the whole animal black. The skin is hung on the bones by `QuadrupedDress`, which a boar's and a wolf's dressers share and only add their colours and coats to (a refactor that left all six boars exactly as they were, which `grow`'s mesh fingerprint holds). It moves as `MotionStyle.Hound`. Look with `Dino AnimLab sheet --body wolf --do run`, `--body direwolf` or any `wolf3` or `direwolf2`, and in the game with `--parade restless,shambler,wolf,dire-wolf`, which stands the dire wolf straight in front of the hero and the wolf beside it. Nobody has seen one move in the game.

The bear was the last of the tubes, and with it went `BeastGenerator`. It's grown the same way (`Modelling/Procedural/Character/Bear/`): `bear0` to `bear3` (`BearSpec`, the bestiary's `Variants`), a cave bear drawn from its seed by its build. Massive and long in the body, a hump of muscle over the shoulders standing above a back that falls to a broad rump, short thick legs as long behind as in front but set lower, a broad domed head carried low with a steep brow, a short thick muzzle and small round ears, five pale claws on each paw, and hardly a tail. It walks on its soles, heel and all, so its legs bend at the wrist and the ankle, both just above the paw, its foot's bone is its toes (`BearDress.FootHang`), and its step rolls over no more than their length: given the whole paw, its ankles rolled a third of a metre up at every step and its hips jolted at two g. Its legs are columns, so `MotionStyle.Bear` limits its stride (`StrideDip`) as a giant's does, and it steps quicker rather than longer. Its coats are brown, nearly black, grizzled and tawny, each bear's given by `BearBody` as the others' are, and its fur is `BearFur`, long and shaggy. Every one of them, boar, wolf and bear, is drawn in `GearMaterials.Hide`, which lets light in under the skin (SUBSURFACE.md), but for a boar's hooves and a bear's claws, which are horn. Look with `Dino AnimLab sheet --body bear --do run` and in the game with `--parade restless,shambler,bone-archer,bear`, which stands it straight in front of the hero. Nobody has seen one move in the game.

## Colour

`SaurianPaint` works out a colour at each point from **where it is on the body, not in space**: how far up the side it is, from the belly to the spine, and how far along. So stripes run across the back and rings go round the tail whatever the mesh is like, and every pattern is something living animals wear (`HidePattern`): stripes, spots, rings, a mask through the eye, saddles and mottling. Everything is dark above and lighter below, as nearly every animal is and as *Sinosauropteryx* is known to have been. Feet and what's near the ground are darker, and a throat or crest that's shown off is the scheme's display colour. A giant's patterns are faint (`Contrast`), as something with nothing to hide from or ambush needs no bold ones, and what it shows off is the dome over its nose and a ring round each eye. Its eyes are dark and soft, as a grazer's are.

Colours are albedos under a sun of about 13, so all of them are deep, as anything pale goes white. Judge them in the game, as the lab's pictures are brighter.

## Hard parts

`SaurianParts` puts on what's solid, each hanging from one bone: teeth along both jaws, biggest a little way back from the front as Sue's are, inside the lips or outside them as the species has it (`Lips`, after Cullen and others); eyes in their sockets; toes and claws, with a raptor's killing claw held up off the ground; fingers, two on a tyrant and three on a raptor; horns over the eyes and on the nose; a horned grazer's frill, solid on some and edged with spikes on others; a spitter's thin crests; a mane of quills down the neck and back with short feathers on the arms, as dromaeosaurs had; and a row of low spines.

**Anything smaller than a pixel and a half from the game's camera isn't made at all** (`Smallest`, 4.5 cm). A body is paid for thousands of times over.

## What grows on them

Feathers, a shaggy coat and hair are shells (`SHELLS.md`): a species says which (`Coat`, a `ShellStyles` number: `Plumage`, `Shag`, `SpiderHair` or `Down`), and how far it goes, down the legs from the hip (`CoatLegs`), forward over the head (`CoatHead`) and underneath (`CoatBelly`), or along the top alone, as a mane (`CoatMane`). `SaurianDress.Shell` says how much grows at each vertex, so the snout, the jaws, the shins and the feet stay scaled, and `SaurianForge.Make` gives the mesh its style. What's rotting keeps what's left of its coat, and bones and ghosts have none. The lab draws no shells: judge coats in the game.

## What's done to them

The same seed is the same animal whatever's been done to it, so a raptor's bones are that raptor's bones (`SaurianCondition`).

- **In harness** (`.reich`, `SaurianHarness`): an iron collar and its bolts, oxblood straps round the chest and belly joined down the back, and on the big ones an iron mask over the snout, spikes, and a plate on the back with a pole and the Reich's pennant, oxblood with its mark in brass on both sides (`SaurianHarness.Mark`). `SaurianPaint` adds the war paint: an oxblood band over the snout and a ring round the tail. Black iron, oxblood and brass are the Reich's alone.
- **In regalia** (`.reich` on a species with `Regalia`, the tyrant, `SaurianRegalia`): what a commander puts on its own beast. An iron helm over its skull with a crest of blades down the middle and guards over its cheeks, riveted in brass; an iron gorget at the root of its neck; a ridge of iron plates down its back over an oxblood saddle-cloth edged in brass, each plate with a blade standing off it; oxblood tabards hanging down its flanks, pointed at their hems, the middle one on each side carrying the mark; two standards on its shoulders, a banner hanging from each with the mark on both faces; plates on its thighs; iron rings round its tail; and chains from its helm to its gorget. Cloth and plates are laid on the skin itself, found by searching the body's field outward from its line (`SaurianField.Exit`), as the belly and chest stand well outside the line and anything laid by it was inside them. A clubtail's plates are laid the same way, as a body that broad buried all but their tips.
- **Rotting** (`.rot`, `SaurianRot`): the living colours drained, greyed and gone green with bruising (`Sicken`), three to five wounds carved into the field where there's most to rot, ribs showing in the worst of them, a sick light in the eyes, and what keeps it going: an iron collar, a glass of glowing bile on its back, brass tubes from that into its neck and bolts in its skull.
- **Bones** (`.bones`, `SaurianBones`): the skeleton the flesh was measured from. The skull is the head's field with the openings a theropod skull has cut through it, the backbone is counted as Sue's is, ten in the neck, thirteen in the back, five over the hips and forty down the tail, and the ribs follow the shape the flesh had. A cold light burns in the eyes and where the heart was.
- **A ghost** (`.ghost`, `SaurianSpectral`): what the Ossuary calls up where the bones it digs are too far gone to stand, the cold light alone wearing the shape the animal had, round what's left of its skeleton. Its bones are solid, gone pale and cold, with the ghost's blue in the eyes and the heart; its flesh is light. The flesh's triangles come after everything solid in the mesh (`SaurianMesh.BeginSpectral`, `Mesh.SpectralFrom`, `glModel.SpectralIndexCount`), so the geometry pass and the shadows draw only the bones, and `CharacterRenderer.SpectralPass` adds the rest over the lit scene, before the reflections and under the fog (`Spectral.vert`, `Spectral.frag`): faint seen face on and bright edge on, light running up and back through it in the body's own frame, a slow breath, thinning to a wisp at its feet (baked into its vertices), tested against the depth of what's solid but writing none, so a wall hides it, its own bones show through it, and wet ground reflects it. It's emission, so it goes with emission and the black test holds. In the game it trails wisps of its light and shines on the ground round it (`Haunts.Ghost`). Its colour is a cold blue nothing else is, to be told from the bones' cyan-green and the rot's yellow-green at a glance.

## The rig

`SaurianBody.BuildRig` makes a `Rig` with a `SaurianFrame`, which is what tells the animator it has a dinosaur. It adds to a person's bones a jaw, a neck in two (`Neck`, `NeckTop`), a tail of five (`TailBase`, `Tail1` to `Tail3`, `TailTip`) and toes, all of which move, and says how thick the body is at each bone, so it falls as what it's made of. A giant's neck is in five (`SaurianFrame.LongNeck`: `Neck`, `Neck1` to `Neck3`, `NeckTop`), so that it curves rather than bends, and on anything else those three sit on the neck's root and move nothing. What walks on eight has two more pairs of legs, `SecondUpperL` to `ThirdFootR`, and a rig of its own plan (`BodyPlan.Arachnid`): its forelegs are the arms' bones and its hind legs the legs', as on four, and on anything with fewer the new twelve sit inside the chest and move nothing. Each leg's bones, in the order `Legs` has them, are `SaurianBody.LegUppers`, `LegLowers` and `LegEnds`. That's 43 bones, and the shaders take 48 (`u_Bones[48]`). How it moves is in `ANIMATION.md`.

## In the game

- A kind's `Look` names its species and what's been done to it, as `dino:raptor.reich.`, and `Variants` how many seeds there are. `CharacterFactory.Prepare` grows every one a world could send at the loading screen, on every core at once, and only the upload is left for the main thread.
- A dinosaur is far thicker than a person, so its body is solid that far out: `EnemyKind.Bulk` is its radius at a scale of one (`RadiusAt`), for the server's body, the motor and the parade alike.
- `CharacterFactory.BearingOf` gives it its own rig and `SaurianForge.StyleOf`, and `SkillMotions.Of` picks its gestures from a page by what it's built like: a theropod bites, stomps, pounces, spits, sweeps its tail and roars, and a horned one gores, tramples and bellows.
- Its voice is in `Sound/Content/Saurians.cs`, picked by `GameSounds.DinosaurVoice` from its species and condition, and anything with legs over 1.6 times a person's shakes the ground as it walks (`Saurians.Tread`).
- **Anything as big as a tyrant is felt where each foot lands, as a giant is** (`Treads`, `TreadSystem`): with its hips over `Treads.FeltFrom` at the size it's drawn, it carries its body over the grass, so only its feet press it, each where it lands (`PlantContactComponent.feetOnly`), and each sends a ring of wind out through the grass round it, too weak to break anything, and throws up a little of the ground, or splashes. `GiantShow` and the server's beasts go through the same `Treads.Felt`.
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

Growing one takes 0.16 to 0.9 s on one thread: a raptor about half a second and 10,000 triangles, a tyrant 0.8 s and 22,000, its bones 0.3 s. All 68 that the game can send are grown at once at the first loading screen and kept, which on a machine with few cores is a few seconds. A body's pose costs what a person's does, about 2.5 µs a frame, and allocates nothing. A giant takes 3 s and has 53,000 triangles, and its pose 5 µs, as its neck is worked out afresh each frame.

**On 2026-11-13** the tyrant was grown again as a T. rex in the Reich's regalia, the giant's forelegs were moved to the front of its chest, and six species (the weaver on eight legs, the shagback, the reaper, the strider, the clubtail and the domehead), ghosts drawn in light, coats as shells, and the bestiary for all of them were added. A tyrant in its regalia has about 47,000 triangles, and 38,500 bare. They were judged from the lab's stills, which draw no shells and no light: nobody has seen a ghost, a coat or a weaver in the game.

**Nobody has seen one but in the lab's stills.** Ask how they look in the game, and especially:

- Whether the tyrant reads as majestic and dangerous, and whether its regalia says the Reich at a glance without hiding the animal.
- Whether a ghost is "freaking awesome", as asked: bright enough in daylight, not too bright at night, and whether its bones should show more or less.
- Whether the weaver reads as a dinosaur as well as a spider, and how its eight legs look moving.
- Whether the coats read from the game's camera, and what they cost on a weaker card.

- Whether a tyrant reads as a tyrant from the game's camera, and as big enough as a boss.
- Whether the harness and war paint tell the Reich's from the wild at a glance.
- Whether the rotting and the bones read as different things.
- Whether the giants read as gentle and as big as they are, close up in the Haven and far off behind the menu, and whether their faces are kind.

Not built:

- An armoured one, plated and clubbed as an ankylosaur was (the spikeback is spiked, not armoured), a sail, and anything that flies or swims.
- The new three seen anywhere but the zoo: the ripper, the wader and the spikeback are checked by `check` and drawn by `zoo`, and nobody has watched them move in the game.
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
