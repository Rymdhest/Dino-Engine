# Builds

How heroes and enemies add up: stats, skills, the talent tree, items, and the fights they all end in. This is the game's main idea, so read it before changing any of them.

## The one idea

**There is one kind of fighter.** A hero, an enemy and somebody's skeleton are each an `Actor`: a `Loadout` of modifiers, a compiled `ActorSheet`, and `Vitals`. `Combat` works on Actors and doesn't know which it has. A hero is just an Actor with a player on the other end, and an enemy is a list of stats in `Bestiary`. Anything a hero can do, an enemy can do, so every new skill, rule and item is at once something the world can do to a player.

That is why bosses can wear what they drop and walk the same tree, why a slain enemy can get up and fight for you with everything it had, and why a mimic can use what hit it.

## Stats

Everything that changes a number is a `Mod`. Talents, items, buffs and enemy traits are all lists of these.

```
(base + all flat) x (1 + all increased and reduced added together) x each more and less on its own
```

| Kind | Reads as | How it stacks |
|---|---|---|
| `Flat` | +5 Health | Adds to the base, so everything else scales it |
| `Increased` | 20% increased Damage. Below zero, reduced | All of them add into one pool |
| `More` | 20% more Damage. Below zero, less | Each is a multiplier of its own |
| `Override` | Critical Chance is 50% | Sets it outright. The lowest wins |

`More` is rare on purpose. On the tree only keystones have it, and BuildLab fails if a notable does.

**Scope.** A mod names `Tags`, and applies wherever all of them are present. "Increased fire spell damage" is `Inc(Stat.Damage, 20, Tags.Fire | Tags.Spell)`. One stat covers everything it can be narrowed to, so there is no `FireSpellDamage`.

**Conditions.** `.While(Condition.Moving)`, `.Unless(...)` ask about the one who has it, and `.Against(Condition.Burning)` about whoever they're hitting. `.For(Per.Armour, 100f)` counts it per hundred armour. Conditions include the land: see below.

**Stats pointed at other stats.** `Divert` sends a stat's increases to another stat instead, and `Echo` sends them to both. `Per` measures one total against another. Both read what a total was *before* such things are counted, so they can't feed each other, and two pointed at each other swap rather than loop. It doesn't matter what order anything was picked up in.

### Why it's fast

Modifiers are read when something changes, never when a blow lands.

- `Loadout` sorts an actor's modifiers by stat, once.
- `ActorSheet` is finished numbers for one state of things, and never changes. A loadout hands out one per combination of the conditions it actually cares about, and most actors care about none, so they have exactly one.
- `SkillProfile` is one actor's finished numbers for one skill. A blow reads these and adds nothing up.
- Enemies of one kind, level and trait share a single loadout, so three hundred skeletons are one set of numbers.
- `Hits` is the whole of what happens per blow. It allocates nothing, and `bench` proves it.

On the development machine a blow from a 600-modifier build takes about 170 ns, which is nearly all the 60 modifiers in it that only count against particular foes. A build of two takes about 17 ns. A full recompile of 600 modifiers takes about 120 µs. A running server with five heroes and 800 enemies of eight kinds, on 151 sets of numbers, averages about 1.2 ms of its 16.7 ms tick. Its worst single tick was about 15 ms, measured in a process that also runs the clients, and nobody has traced that yet.

### Damage

Damage is a set of `DamageFlow`s, each remembering everything it has been. Physical damage converted to fire is helped by "increased physical" and "increased fire", and a chain of three by all three. Conversion goes from any type to any other, and can't turn back into what it has been, so however conversions point at each other it ends. Past 100%, conversions share. "Gained as extra" comes from the whole, before any of it moves.

Types: Physical, Fire, Cold, Lightning, Magic, Poison. Fire, cold and lightning also count as `Elemental`.

A blow goes out: roll the base, add what counts against this target, crit.

It comes in, in this order:

1. Evasion (attacks only), then block.
2. What arrives as another type does so (`TakenAs`).
3. Resistance, less penetration and hex.
4. Armour against what got past that, as it's the size of the blow it fights: `A / (A + 5 x hit)`, with physical reduction added, stopping at 90%.
5. Flat reduction, by type and then off the whole hit.
6. Whatever scales damage taken: by type, by how it arrives, by who threw it, and shock.
7. Paid from barrier (poison seeps past it), then the share taken from mana, then health.

### Ailments

| Ailment | From | Does |
|---|---|---|
| Burn | Fire | Damage over time. The strongest stands |
| Bleed | Physical | Damage over time, worse while the bleeder moves |
| Venom | Poison, physical | Damage over time, in stacks |
| Chill | Cold, always | Slows everything they do |
| Freeze | Cold | Stops them |
| Shock | Lightning | They take more damage |
| Hex | Magic | Takes away resistance, in stacks |
| Stun | Physical | Stops them for a moment |

An ailment feeds on the hit that caused it, so whatever made that hit stronger has done its work. What's added on top is whatever names lingering damage or the ailment itself, which never touched the hit. So nothing counts twice.

### Reactions

Two things meeting on someone do what neither does alone. Anyone can set one off, enemies and friends in a party included.

| Reaction | What meets what | Does |
|---|---|---|
| Scald | Fire on the chilled or frozen | Ends it, and scalds what's around |
| Crackle | Cold on the burning | Puts the fire out, and cracks what's around |
| Flashfire | Fire on venom | Takes all the venom at once, around the target |
| Shatter | A blow on the frozen | Breaks the ice, for far more damage |
| Conduction | Lightning in water | Runs through everyone in the same water |

`ReactionDamage` and `ReactionArea` scale them.

### The land

**This is the idea other ARPGs don't have.** The world is generated, and a build can be made for what it's standing on. `LandReader` turns the same fields a world is made of into `Condition`s: `InWater`, `UnderTrees`, `OnRoad`, `InDarkness` (near a graveyard), `HotLand`, `ColdLand`, `WetLand`, `HighLand`. Biomes are classified from their own temperature, moisture and altitude, so a new biome needs nothing here.

So choosing a land at a waygate is a build decision, and a skill or an item can change the answer: a Tidepool makes water for a Tidewalker, and the Drowned King's shell keeps them in it wherever they go. Height counts too, for Peakborn.

## Skills

`SkillCatalog` holds every skill, used by heroes and enemies alike. A `SkillDefinition` is the start of a calculation: its form, its tags, its base damage at level 1 or its share of the weapon, and its own mods. New skills need no code unless they have a new form.

| Form | Is |
|---|---|
| Strike | A sweep in front. A full circle if `Arc` is tau |
| Nova | A ring around the user |
| Bolt | Projectiles: pierce, chain and burst all follow from stats |
| Burst | A blast at a place, a moment later |
| Zone | Ground that pulses for a while |
| Chain | Leaps from one target to the next |
| Beam | A line, at once |
| Buff | On the user, from `BuffCatalog` |
| Summon | Minions, from `Bestiary` |
| Leap | Carries the user, and strikes on landing. Done in `HeroRules.Step`, as moving always is |

Attacks use the weapon, and spells use `SkillDefinition.SkillPower` of the actor's level plus any `SkillLevel`. `AddedEffect` is how much flat damage a skill gets: quick skills get less.

Heroes learn skills from the tree and from what they wear (`M.Skill`), and put them on a bar of seven: the mouse buttons, Q and 1 to 4. A newly learned one goes in the first empty slot. Skills can also go off by themselves (`M.Trigger`).

A skill works the same on any slot. The mouse buttons are held, and what the other keys send is a press, so that a quick tap is never lost and one made late in a cast waits for it. `HeroRules.KeyPresses` has a held key press again whenever its skill could go, which is what makes them match. It waits for the cooldown as well as the hero, as there's one queue, and a key on cooldown would otherwise keep it from a second key held down.

## The tree

One tree, no classes. `TalentTree` is a wheel of twelve sectors around a root, each forking twice as it goes out, so the further a build reaches the more its choices narrow. It's built from the tables in `TalentContent`: a sector is a row of small-node mods, ten pods, and three keystones.

- **Pods** loop out from a branch and back to it further on, so going through one never strands anyone.
- **Bridges** join each sector to its neighbours on two rings, with a notable in the middle for those who walk both.
- **The rim** joins every twig to the next, all the way round.
- **Waygates** stand at the rim. Light one and its twin across the wheel can be lit from there. The most common complaint about big trees is the points spent getting anywhere, so a gate is a real shortcut: from the far side of the Forge's gate, a Pyre keystone is 9 points instead of 15.
- **Small nodes are always worth having**, for the same reason.

There are no classes, so what a hero *is* gets worked out from where they've spent their points (`TalentTree.TitleOf`): the sector they've given most to names them, the next colours it, and a keystone can have the last word.

Points: two a level, one for each land set free, and three to start with. Refunds are free for now, so that builds can be tried: right click a node, or Forget all.

Node ids are saved in heroes and come from where a node stands in a sector's table, so **don't reorder a sector's pods**. Names, numbers and effects can change at any time. `TalentTree.Sanitize` looks after anything that goes missing, so a change to the tree costs someone a few points back in their pocket, never their hero.

### Adding to it

- **A notable or a keystone**: a line in `TalentContent`.
- **A new rule**: a value in `Rule`, its text in `ModText`, and the code where it matters. `Combat` is where most belong.
- **A new stat**: a value in `Stat`, a line in `StatNames`, and read it. `check` fails on a stat that changes nothing, or that nothing gives.

## Items

An item holds lines of an affix id and a rolled value, never modifiers. What a line means is the affix's business, so rebalancing an affix reaches old items, and a stranger can't send a modifier that doesn't exist. Affix ids are saved, so they stay. Unknown ones are ignored, so a removed affix costs a line, not a hero.

Uniques (`UniqueCatalog`) are items with lines of their own, and should change what a build *is*: `check` fails if most of them only add to it. Bases come in kinds: weapons by `WeaponKind`, armour by which of armour, evasion and barrier it gives. Two-handed weapons and shields don't mix.

## Enemies

`Bestiary` holds what creatures are: skills, mods, and how strength grows with level. Elites get a trait, which is the same modifiers gear is made of. A boss wears real items and takes a real keystone, fights with them, and drops what it wore, so a player has usually seen a unique used before they ever hold it.

`HealthAt` and `DamageAt` are fitted to the `balance` table: a sample hero who knows nothing of keystones and doesn't choose their gear needs about three hits and survives about ten. Anything a real build does better is the build working. They climb the way a hero does, fast early on, and what's exponential in them keeps lands past the level cap getting harder.

## Networking

`HeroRules.Step` is still a pure function of the command, the hero's kit and the world. Both ends compile the same `ActorSheet` from the same `HeroState`, so they agree on every number without sending any.

What only the server can know reaches the client in `HeroSimState`: the `State` and `Counters` that pick which sheet is in force, and `MoveRate`, `ActionRate` and `Held` for what's being done to the hero. So a new conditional stat needs no networking at all. Buffs and a mimicked skill come with `HeroUpdate`.

What a blow does is the server's business. What a client sees of it comes as `DamageEvent`, `SkillEvent`, `LinkEvent` and `ReactionEvent`.

## Testing

```
dotnet "Dino BuildLab/bin/Release/net8.0/Dino BuildLab.dll" check      # The sums, the tree, items, skills, old saves
dotnet "Dino BuildLab/bin/Release/net8.0/Dino BuildLab.dll" bench      # What it costs, and that blows allocate nothing
dotnet "Dino BuildLab/bin/Release/net8.0/Dino BuildLab.dll" balance    # How a fight goes at each level
dotnet "Dino BuildLab/bin/Release/net8.0/Dino BuildLab.dll" list       # Everything there is, with its text as the player reads it
dotnet "Dino NetLab/bin/Release/net8.0/Dino NetLab.dll" build          # Every skill, ailment, reaction and keystone, for real on a running server
```

Run `check` and `build` after any change here, and `game` if the hero's step or the protocol moved. To look at a build in the real game: `--hero 30 --talents key.tidewalker,pyre.firebolt --equip drowned-king`, with `--panel talents`, `hero` or `skills`.
