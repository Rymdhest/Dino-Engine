# Animation and combat

How bodies move, and how a fight tells a player what's happening. Read `BUILDS.md` for what a blow is worth and `NETWORKING.md` for how anything reaches a client. This is what it looks like and when.

Status: built, and tested hard by machine, with real keyboard and mouse messages through the real game. **Nobody has played it.** See "Where it stands" at the end.

## The one idea

**The rules own time, and bodies follow.** A blow lands on the tick the server says, whatever any arm is doing. Nothing in the rules waits for an animation, reads one, or is moved by one: there's no root motion, and a swing can be cut off, slowed or stopped at any moment without anything else having to know.

```
   Server, or a hero's own rules             Client, every frame
  +---------------------------------+
  |  when it starts, when it lands, |      AnimationInput: where the body is,
  |  when it's over, which way      | ---> what it's been told, by when
  +---------------------------------+               |
                                                    v
                                     Gait -> feet    Gesture -> hands    Ragdoll -> all of it
                                                    |
                                                    v
                                              bones, this frame
```

A pose is worked out from those numbers each frame. Nothing is recorded and nothing is played back, so there's no animation to be in the middle of, and no state that can get stuck. Whatever it's told next, it goes from where it is.

Three tools keep that from ever showing as a jump, and everything in `Dino Engine/Animation/` is built from them:

- **Springs** (`Spring.cs`) that are solved exactly, after Holden's "Spring-It-On". They're given a half-life instead of a stiffness, don't depend on the frame rate, and can't blow up.
- **Continuity** (`Continuity`, `TurnContinuity`), after Bollo's inertialization. When something jumps, the jump is kept as an offset that dies away, with a speed limit, so a pose can change completely in a tick and the eye never sees it.
- **Effectors.** A hand is told where to be and the arm works out how, as in Spore and Unreal's IK Rig. Hands are placed by angle, height and reach around the body, in arm lengths, so one gesture fits anyone. Limbs are solved by `Reach.Solve`, analytic two-bone IK with soft reach, which has no iterations to fail and gives the same answer however it got there.

## Bodies

A body is a `Rig` (`Rig.cs`), which is where its joints are, and everything else is read off that. `BodyPlan` says if it's on two legs or four. All bones have no rest rotation, so what's stored is only where they are.

Conventions, as getting them wrong is the commonest mistake here: model space is +Z forward, +Y up and +X to the body's left. `Frames.Pitch` with a positive angle bows forward. People are 4.7 m tall in this world, and the game's camera is some 33 m away at 67°.

`MotionStyle` is how someone moves as opposed to what they're built like: a few numbers around 1, and a new way of moving is one more of them. `MotionStyle.Individual` makes everyone a little different. `Carry` is what's in the hand and how it's held at rest, on the run and on guard.

`CharacterFactory.BearingOf` says all three for a model, and `CharacterAnimator` is one body.

**Dinosaurs** are bodies like any other, with more to them. A rig with a `SaurianFrame` (`Rig.Saurian`) has a jaw, a neck in two, a tail of five bones and toes, all of which move, and its limbs are built bent, as the animal stands, so each keeps how it was built (`Limb.RestUpperInverse`) and a knee bends the way that animal's does. Read `DINOSAURS.md` for how the body is grown.

`CharacterAnimator.PoseSaurian` treats a theropod as a beam balanced on its hips. It tips forward into speed and back as it stops, comes down as far as it has to to bite someone standing (`BiteHeight`, a person's chest, which is next to nothing for a raptor and a long way for a tyrant), and rears back on its tail to roar. The head looks about in quick turns held still between, as a bird's does, and stays level while the neck takes up whatever the body does beneath it, and a strike is the neck straightening and throwing the head forward, with the jaw going with the blow. The little arms are carried folded with the palms facing. The tail swings in time with the legs, as the muscle that pulls each thigh back runs down it, and the swing goes down it as a wave: it trails out on the outside of a turn, lifts with speed, and each joint follows on a spring of its own, slower towards the tip, which is all it takes for a lash to come off the end. On four legs it's any animal, with a heavy head carried low.

`MotionStyle` has a number for each of these (`TailSway`, `TailStiff`, `Gape`, `Pant`, `HeadLow`, `ToeIn`, `Gallop`), and a style for each kind: `Raptor`, `Scurrier`, `Stalker`, `Tyrant` and `Horned`, with `Bones` and `Rotting` for what the Ossuary raised. `SaurianForge.StyleOf` says which.

**A long neck** (`SaurianFrame.Long`, the Haven's giants) is posed by `PoseLongNeck`, from where its mouth should be: `AnimationInput.ReachFor`, and `Reaching`, how far into reaching for it it is. `SolveNeck` works out three things and no more, so the neck curves as one thing and never hinges: how far it lifts or lowers from its root, how much it curls along its length to make the distance, and how far it turns round along its length to face the goal, which fades out as the goal comes overhead, where any turn would be a jump. The goal is followed on a spring, as a head that size takes a few seconds to get anywhere, and the head stays level and looks where it's told on top of whatever the neck is doing. With nothing to reach for, the mouth goes where it was built to be and sways a little. `Chewing` works the jaw. A giant walks as an elephant does, rolling its weight over whichever side has more feet down (`MotionStyle.WeightShift`), and its round feet don't roll over their toes (`SaurianFrame.FootRoll`).

**Somewhere no foot comes down** is `AnimationInput.KeepOff`, with `KeepOffRadius`: someone standing by a giant's foot. `Gait` puts any landing that would fall inside it at its edge instead, and a foot standing in it is drawn up by the standing body's own step to where it should be, which is outside it. It's how the giants never put a foot down on anyone, whatever their legs were doing when someone walked under them: the herd says who's nearest, and the body does the rest.

## Feet

`Gait` is the only thing that moves a foot, and the rule is simple: **a foot on the ground stays exactly there, until it lifts.** That's what ends sliding. Feet aren't placed by a cycle: each leg has a clock, a share of the time it's down (duty) and a reason to step, and lands where Raibert's rule says, ahead of the hips by how fast they're going. Cadence comes from a table scaled by Froude number, so a big body strides and a small one scurries at the same speed.

The hips go as low as the feet need them to, so nothing stretches. Arm swing comes from how long the stride really is. Four legs are the same with walk, trot and gallop as triggers between the legs, blended by speed.

A dinosaur walks on its toes, which have a bone of their own. Down, they lie on the ground however the foot above them turns, so the foot rolls over them as a bird's does, and off the ground they curl under and open out again to land.

`IGround` is all that's asked of a world: `WorldGround` for a real one, and a lab can have a floor or a staircase. Slopes need nothing more.

## Hands

A `Gesture` is three keys: **Cock, Contact, Through**. `GesturePlayer.Sample` is a pure function of how long it's been and when it lands, so the Contact key is exactly on the tick the rules say, whatever else happens.

The blow itself always takes the same time (`Snap`), and what changes is how long the wind-up has. With little time it doesn't go all the way back (`drawn`), as moving a long way quickly would be faster than the blow, and it's the blow that's meant to be fast. So a book made quicker by a stat, or an enemy with a long telegraph, needs nothing new: the same gesture fits.

`Gesture.Next` is what a blow turns into if another follows within `ChainWithin`, which is how forehand and backhand alternate without anyone counting. `SkillMotions.Of` picks a gesture from a page's `PageSign`, so a new book looks right with nothing added. `FreeHand` says a gesture is done with an empty hand, so an arm with a shield on it stays where it is. With a weapon in each hand (`Hold`'s `second`), the hands take turns: every other blow is the gesture mirrored onto the other hand, body and all, and doesn't chain, as the other hand starts fresh. That's counted by the body, so two people watching may see different hands, which is fine as it's only for show. `WeaponTip` follows whichever hand's turn it is.

A dinosaur fights with its mouth, its tail, its weight and its voice: `Chomp` (and `ChompBack` after it), `Stomp`, `Pounce`, `Spit`, `TailSweep` and `Roar` on two legs, and `Gore` and `Trample` on four. Their keys move the jaw and the tail as well as the hands (`BodyKey.Jaw`, `BodyKey.Tail`), and `SkillMotions.Of` picks one by what the body is built like, so a book a dinosaur is given looks right with nothing added. A nova that takes long enough to look heavy is a roar and a quicker one a tail sweep, as the sign says nothing else to tell the two circles apart.

What isn't a book goes by `FlourishEvent`: drinking, and putting a hand out to use something. It's for show, so it can go missing.

## Falling

`Ragdoll` is what's left when nobody's in a body. It's Jakobsen's Verlet particles, as Hitman's corpses were: each point remembers where it was instead of how fast it's going, and every rod is put right by moving its ends. That can lose energy and can't gain any, so **nothing here can blow up, however hard it's thrown**, and it costs about 14 µs a body while falling and nothing once it's still.

It's read off the rig, so anything with bones can fall. It starts from the real pose and what each joint was doing, so going down is continuous with whatever came before. Bones get their turn from where their joints end up, and the twist along a limb is carried on from the moment it fell.

Bodies stop at what's standing there. `IGround.LookAround` is asked once a step for the whole body, and each point is then pushed out of what was found. A point looks along the way it came as well as where it got to, as something thrown hard goes further in a step than a fence is thick. `PlacementIndex` knows how tall things are for this, so a body can go over a fence and stop at a house. Nothing that walks cares.

A dinosaur falls as what it's made of. Each bone says how thick the body is there (`SaurianFrame.Thickness`), its toes are points of their own, as they reach well ahead of an ankle and would go through a wall the foot stopped at, and each point hangs from the nearest point above it. Bones that aren't part of the fall are carried by what they hang from, as a jaw goes with a head, and only a dinosaur's are: a bear's were once carried too, and it went through a wall.

How hard a body goes is how far past dead it was: `Combat.Throw` works it out from how much more than enough the blow was, and only just dying lets it drop. It's `EnemyComponent.Thrown` that reaches a client, and the fall itself is worked out there, so every player sees their own.

## What a fight shows

**There is one description of what an attack covers: `GroundShape`.** The server tests blows against it and a client draws it, so what's shown can't be anything but what lands. A new skill form only has to say what it covers in `GroundShape.Of`.

**When the fill reaches the edge, it lands.** A mark is a `TelegraphComponent` on an entity of its own, sent like anything else and read off the player's own clock, the one their hero moves by. So it's the last moment a key can still count, on any line. It isn't an event, as it can't be allowed to go missing, and each has a `Serial` of its own, as an entity's id goes to the next thing made the moment it's gone.

An enemy fixes what it's doing when it starts: where from, which way, how far, and on which tick. `EnemyBrainSystem.Commit` says so to everyone, and from there nothing is checked again. Whether anyone's still standing there is their business, and that's the whole of how a blow gets dodged. It's counted in ticks that really pass, so nothing that slows them afterwards can make it late. If they're stunned, frozen or killed the mark is withdrawn, and a hero's own blow is too if a dodge or a stun stops it (`GameServer.CutShort`).

Marks go through `GroundMarkRenderer` into a map of signed distances with a minimum blend, as the minimum of two distances is the distance to both: shapes that overlap become one shape with one outline, however many there are. A full-screen pass then takes each pixel's ray to the ground and shows what's marked there, so a mark follows the land, doesn't smear up a wall, and can be seen under a tree. Characters are kept out by a stencil bit, so they stand on it.

**Two colours and no more.** Whatever's in a player's way is one and whatever's theirs is the other, and that's all it ever takes to read. What kind of damage it is doesn't matter to whether you should move. `MarkTier` says how much attention something deserves, and what can't reach this hero stays quiet, as a pack can put a dozen down at once and a screen covered in them says nothing.

**A player can turn marks off by how rare the enemy is**, in Interface, as what the least of them do is seldom worth an eye, and normal enemies start off that way. `TelegraphComponent.From` says what's doing it, and `GroundMarks.Update` leaves it out only as it draws, so the blow lands all the same, and turning it back on in a fight shows what's already on its way. It's the promise a player has chosen not to be made, and nothing else may lean on it. Ground that's already doing something (`ZoneComponent`) isn't an attack on its way, so it's always shown.

A plain blow of a hero's isn't marked: nobody needs telling what a sword reaches. `HeroRules.ShowsArea` says what is. Their own area goes with them until it leaves their hands, as that's where the server lets it go from.

### How a blow feels

All of this is drawn on a client and changes nothing about the fight.

- **Hit-stop**, after Sakurai: both sides stop for the same moment, longer for a bigger hit and never for long. It's `CharacterAnimator.Connected`, the time is paid back in the rest of the swing, and the rules don't wait for it.
- **A flash** on what's struck, in `hitFlash`, kept low enough that it doesn't hide who it's on.
- **A flinch** from the way the blow came, by how much of them it took.
- **A jolt** of the camera for a hero who's hurt, along the blow.
- **Weapon trails** for heroes and for what's dangerous enough to watch.

What's real is the shove: `Combat.Shove` moves what's struck by how much of it the blow took, less for rare enemies and hardly at all for bosses. It only ever moves what the rules allow: a hero's feet are their own, or they couldn't predict them.

### Getting out of the way

A dodge goes the tick it's pressed, and nothing gets in its way. Whatever was going on is over, and if the blow hadn't landed it doesn't. It's a clean break: a button still held waits until it's over, and a click made during one comes straight after. Pressed a moment too soon, it goes by itself when it can (`DodgeQueueTime`).

## Adding things

**A new way of moving**: one more `MotionStyle`. **A new way of holding something**: one more `Carry`. Say which in `CharacterFactory.BearingOf`.

**A new gesture**: three keys in `Gestures`, and a line in `SkillMotions.Of` if it's for a page. Add it to the list in `AnimCheck.Hostile`, and to `Script.Named` to look at it. If it isn't for a page, add to `Flourish`.

**A new kind of animal**: a `BeastLook`. Its rig, its gait and how it falls all come from that. It needs a line in `CharacterFactory.Resolve` and `BearingOf`.

**A new kind of dinosaur**: a species in `SaurianCatalog`, as `DINOSAURS.md` says. Its rig, its gait and how it falls come from its body, and `CharacterFactory` already knows any model named `dino:`. A new way of moving is a style in `SaurianForge.StyleOf`. A long neck needs nothing more than its frame saying so: what it reaches for is up to whoever drives it.

**A new shape of attack**: a `ShapeKind`, a case in `Covers`, and the same in `Shaders/Marks/Mark_Shape.frag`. Those two have to agree.

**Something solid that's low**: give `PlacementIndex.Add` a height.

## What will break it

- **Reading an animation from a rule.** If a blow waits for a hand, responsiveness goes, and the server has no hands.
- **A hit that doesn't go through `GroundShape`.** `Within` is the only way anyone is found to be in an area, and that's what makes marks true. `Grid.Foes` adds a body's whole width: arrows did that once, and hit people well outside their strip.
- **Scaling a time twice.** A wind-up is in ticks that really pass. It was once divided by how slowed someone is and counted at that rate too, and a slowed enemy's blow came a whole factor late.
- **Keeping a mark's times.** The server moves them while someone alone has stopped the world, so they have to be read each frame.
- **Anything that goes by `Now` while stopped.** `Time` is ticks, and ticks go on. `Combat.Wait` is why what's on its way waits too.
- **Going by an entity's id on a client.** Ids are given out again at once, so a mark can seem to be one that's just gone.
- **Assuming a body has been posed.** One can be first seen frozen. It's why a frozen body gets one pose before it stops.
- **Anything allocating in `CharacterAnimator`.** `bench` says 0 bytes, and there can be hundreds of bodies.
- **Lengths of tube for a body.** Each ends square to itself, and they open up where it changes angle. It's a `Surface` for that reason.

## Testing

```
dotnet "Dino AnimLab/bin/Release/net8.0/Dino AnimLab.dll" check    # Does everything hold, whatever's thrown at it?
dotnet "Dino AnimLab/bin/Release/net8.0/Dino AnimLab.dll" bench    # What a body costs, and that it costs no memory
dotnet "Dino AnimLab/bin/Release/net8.0/Dino AnimLab.dll" sheet --body wolf --do run --view side
dotnet "Dino NetLab/bin/Release/net8.0/Dino NetLab.dll" combat     # What a fight promises, against a real server and clients
```

`check` is every body in every situation, 18,000 frames of the worst input a game could give, and bodies thrown at the buildings and trees of a real world. `sheet` draws frames with no GPU: `--body` is bare, sword, shield, spear, bow, robe, brute, cutthroat, skeleton, wolf, boar or bear, any of the six dinosaurs (raptor, skitter, spitter, ravager, tyrant, hornback) or any dinosaur's model name, such as `dino:tyrant.bones.0`, and `--do` is any script in `Script.Named`. `zoo` stands dinosaurs side by side.

`combat` is the one to run after changing anything here. Every edge of every shape is tried from both sides, each timing to the tick, and everything that can stop a blow. Every bug it was written for has been put back to see it fail. Keep it that way: a test that's only ever passed proves nothing.

A lab can't say a body moves well, and judging it takes a person. To look at a fight in the real game, `--brawl firebrand,bear,archer` stands those in front of the hero, awake. Remember a `--film` is no use for timing: its client runs on a fixed step while the server keeps real time, and a hero's own marks can pile up there in rings, which no real client shows on any line. To see one, time a real press off `--trace` so that a `--shot` falls inside the wind-up.

**A bot proves nothing about input.** Post real window messages, and read what they did with `--trace file`, which writes the hero's state each frame. It's how a swing was found starting again under a dodge with the button still held, which no lab had thought to try.

## Where it stands

**Nobody has played this.** Everything above is measured or was looked at in stills and short films. What it feels like in the hands is the whole point, and only a person can say.

**Nobody has heard it either.** Most hit sounds never played before this, as each new listener for an event replaced the last one. They all play now, and that mix is untested.

**Dinosaurs have only been seen in the lab's stills**, walking, running, roaring and biting, from the side and from the game's angle. Nobody has watched one move in the game, or fought one. The giants were seen walking and browsing in two short films of the Haven taken by machine, from the game's camera, and nobody has watched them.

Not built:

- Corpses don't collide with each other, or with anyone standing up.
- In deep water a body sinks to the bottom rather than floating.
- Bosses have no way of reacting to a blow of their own: they just give less.
- Marks for a hero's chains and bolts, which show for themselves as they go.

Rough edges, left as they are:

- A shield bash draws its trail off the sword, which is the wrong hand.
- A dead animal's belly pinches where the body folds. From the game's camera the body hides it.

## Decided, and open to change

Choices made without asking, as none of them block anything. Say if any are wrong.

- **A dodge is a low dash, not a roll.** It's 0.2 s, which isn't long enough for a roll to read, and it keeps a hero's outline where the eye expects it.
- **A dodge throws away a blow that hasn't landed.** Getting out of there was worth more. The alternative is that it lands anyway, which is more forgiving and less clean.
- **Enemies fix their aim when they start.** It's what makes a mark true. It also means they can miss someone who simply keeps walking.
- **The quickest enemy blow takes 0.6 s**, so that it's on screen for 0.47 s on a good line. It was 0.45 s, which left 0.32 s. This is one number, `ShortestWindUp`, and it's as much about pace as about fairness.
- **Two colours for marks**, as above.
- **A hero's body counts for 0.4 of its width** against what's hostile, so if a foot looks out of it, it is. Enemies count in full.
- **A chain has to start on what's marked.** It used to find anything within 13 m of the aim, which is no use as a mark: nobody can step out of 13 m. This goes for heroes too, so Forked Spark needs aiming now.
- **Shoves, staggers and throws** are all first guesses, in the constants at the top of `Combat`.
- **Corpses lie for 5 s** and take 1 s to go.

## Where this came from

- Daniel Holden, [Spring-It-On](https://theorangeduck.com/page/spring-roll-call): exact damped springs by half-life.
- David Bollo, "Inertialization" (GDC 2018), from Gears of War 4.
- Chris Hecker and others on Spore's animation, and Unreal's IK Rig: placing what matters and letting a solver do the rest.
- Marc Raibert, *Legged Robots That Balance*: where a foot should land.
- Thomas Jakobsen, "Advanced Character Physics" (GDC 2001), from Hitman: Codename 47.
- Masahiro Sakurai on hit-stop.
- Peter Bishop and others, on making theropods walk from their muscles in simulation: a tail that swings in time with the legs.
- How birds hold their heads still while their bodies move under them: a dinosaur's head.
- Inigo Quilez on signed distances: the shapes in `Mark_Shape.frag`.
