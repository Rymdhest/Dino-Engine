# Builds

How heroes and enemies add up: stats, books, the talent tree, items, and the fights they all end in. This is the game's main idea, so read it before changing any of them.

## The one idea

**There is one kind of fighter.** A hero, an enemy and somebody's skeleton are each an `Actor`: a `Loadout` of modifiers, a compiled `ActorSheet`, and `Vitals`. `Combat` works on Actors and doesn't know which it has. A hero is just an Actor with a player on the other end, and an enemy is a list of stats in `Bestiary`. Anything a hero can do, an enemy can do, so every new sentence, rule and item is at once something the world can do to a player.

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

### What a hero starts with

**A naked hero with no talents has 100 Life, 100 Mana and nothing else, at any level** (`HeroBuild.Innate`, and `check` holds it). Levels give talent points and nothing more. Where something can't be nothing, it's a rule and not a stat: everyone walks at `BaseMoveSpeed`, dodges the same way, crits for 150%, refills `ManaRefill` and stops at the same limits, and what's on the sheet is the bonus on top of that.

Power comes from attributes. **Strength** gives Health and increased Physical Damage, **Dexterity** Accuracy, Evasion and increased Attack Speed, and **Intellect** Mana, Mana Regeneration and increased Spell Damage (`Attributes`, numbers in the `Rulebook`). They're ordinary modifiers counted per point, added to any loadout with an attribute on it, so an enemy with Strength gets them too. Every node on the tree gives `NodeAttribute` points of its sector's attribute (`TalentContent.AttributesOf`) and `NodeHealth`, and gear can roll them. `NodeHealth` is there because Life is what levels used to give, and only Strength gives it now: without it a caster at level 60 dies in two hits.

### Why it's fast

Modifiers are read when something changes, never when a blow lands.

- `Loadout` sorts an actor's modifiers by stat, once.
- `ActorSheet` is finished numbers for one state of things, and never changes. A loadout hands out one per combination of the conditions it actually cares about, and most actors care about none, so they have exactly one.
- `BookProfile` is one actor's finished numbers for one book, with a `PageProfile` for each page. A blow reads these and adds nothing up.
- Enemies of one kind, level and trait share a single loadout, so three hundred skeletons are one set of numbers.
- `Hits` is the whole of what happens per blow. It allocates nothing, and `bench` proves it.

On the development machine a blow from a 600-modifier build takes about 170 ns, which is nearly all the 60 modifiers in it that only count against particular foes. A build of two takes about 17 ns. A full recompile of 600 modifiers takes about 120 µs. A running server with five heroes and 800 enemies of eight kinds, on 151 sets of numbers, averages about 1.4 ms of its 16.7 ms tick. Its worst single tick was about 17 ms, just over budget, measured in a process that also runs the clients, and nobody has traced that yet.

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
| Hex | Magic | Takes away resistance, in stacks. Effect scales each stack |
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

So choosing a land at a waygate is a build decision, and a book or an item can change the answer: a Tidepool makes water for a Tidewalker, and the Drowned King's shell keeps them in it wherever they go. Height counts too, for Peakborn.

## Books

There are no skills. Everything anyone does in a fight is a book written out of sentences: read `BOOKS.md` before changing anything an actor can do. This is only how a book joins the sums.

`Scribe.Read` turns a book into a `BookPlan`, the same whoever holds it, and `ActorSheet.Profile` turns that into a `BookProfile` for one actor, with a `PageProfile` for each page. Your Damage is the weapon's, for every book. A wand or a staff makes a book a Spell and anything else an Attack, so a weapon's base damage is all a caster has to start from, and wands and staves are priced for that. On top of that a page's `Potency` scales everything it does.

Heroes carry books on a bar of six: the mouse buttons, Q, 1, 2 and 3. A book on the bar isn't in the pack (`HeroState.Bar`), as a ring on a finger isn't. Every hero has one from the start, on the left mouse button: a Cone of Your Damage that costs nothing. Books can also go off by themselves (`M.Trigger`). Those are on `Shelf`, and what sets them off comes from the tree or from what's worn.

A book works the same on any slot. The mouse buttons are held, and what the other keys send is a press, so that a quick tap is never lost and one made late in a cast waits for it. `HeroRules.KeyPresses` has a held key press again whenever its book could go, which is what makes them match. It waits for the cooldown as well as the hero, as there's one queue, and a key on cooldown would otherwise keep it from a second key held down.

## The tree

One tree, no classes. `TalentTree` is a wheel of twelve sectors around a root, each forking twice as it goes out, so the further a build reaches the more its choices narrow. It's built from the tables in `TalentContent`: a sector is a row of small-node mods, ten pods (the Forge has an eleventh, Whirl, on the free side of its trunk), and three keystones.

- **Pods** loop out from a branch and back to it further on, so going through one never strands anyone.
- **Bridges** join each sector to its neighbours on two rings, with a notable in the middle for those who walk both.
- **The rim** joins every twig to the next, all the way round.
- **Waygates** stand at the rim. Light one and its twin across the wheel can be lit from there. The most common complaint about big trees is the points spent getting anywhere, so a gate is a real shortcut: from the far side of the Forge's gate, a Pyre keystone is 9 points instead of 15.
- **Small nodes are always worth having**, for the same reason.

There are no classes, so what a hero *is* gets worked out from where they've spent their points (`TalentTree.TitleOf`): the sector they've given most to names them, the next colours it, and a keystone can have the last word.

A new hero knows Strike and nothing else, and everything on their bar after that is learned. Whirl is four points from the root, so it's within reach at level 2. The guiding wind comes with the first land set free (`HeroState.HasGuidingWind`).

Points: two a level, one for each land set free, and three to start with. Refunds are free for now, so that builds can be tried: right click a node, or Forget all.

Node ids are saved in heroes and come from where a node stands in a sector's table, so **don't reorder a sector's pods**. Names, numbers and effects can change at any time. `TalentTree.Sanitize` looks after anything that goes missing, so a change to the tree costs someone a few points back in their pocket, never their hero.

### Adding to it

- **A notable or a keystone**: a line in `TalentContent`.
- **A new rule**: a value in `Rule`, its words in `ModText` with its numbers in the `Rulebook`, and the code where it matters. `Combat` is where most belong.
- **A new stat**: a value in `Stat`, a line in `StatNames`, a term in `Lexicon`, a line in `StatBook`, and read it. `check` fails on a stat that changes nothing, that nothing gives, or that nothing explains. `TOOLTIPS.md` says how all of that has to read.

## Items

An item holds lines of an affix id and a rolled value, never modifiers. What a line means is the affix's business, so rebalancing an affix reaches old items, and a stranger can't send a modifier that doesn't exist. Affix ids are saved, so they stay. Unknown ones are ignored, so a removed affix costs a line, not a hero.

**Rarity.** There are four, and there will only ever be four (`Rarity`):

| Rarity | Has |
|---|---|
| Normal | Nothing but what its base gives: its armour, its damage, what its kind of weapon always does |
| Magic | One prefix and one suffix, always |
| Rare | Two prefixes and two suffixes, always |
| Unique | No rules at all. Lines of its own, and something no other item can do |

**A prefix keeps you alive and a suffix is what you kill with.** That's not a convention in a table: every `Stat` is on one side or the other (`StatSides`), and an affix is a prefix or a suffix by what it builds, so a defensive suffix can't be written. Defensive means pools and getting them back, leech and everything else that refills them, not being hurt, and getting away. Offensive means everything about a blow, how a page reaches, what it leaves behind, thorns, the attributes power comes from, and what's got out of a kill, which is gold, rarity and experience. A new stat goes in one list or the other, or `check` fails.

**A stat goes where it belongs.** Each affix names the slots that can have it, and nothing can go everywhere, so what's on an item says what kind of item it is. Resistances are for rings and nothing else, movement speed for boots, and attack and cast speed for gloves. A weapon gives its wielder what it takes out of what it cuts, as weapons need prefixes too. Attributes go on anything worn, which is also why there's something to kill with on every piece of armour.

| Slot | Prefixes | Suffixes |
|---|---|---|
| Weapon | Leech, on hit, on kill | Damage of every kind, crit, penetration, ailment chances, potency, projectile speed, reactions |
| Shield | Armour, evasion, barrier, block, reduction, physical taken as, health | Thorns, attributes |
| Head | Armour, evasion, barrier, health, mana, mana regeneration, taken from mana, stealth | Area, duration, cooldown, cost, accuracy, minion damage, attributes |
| Amulet | Health, mana, regeneration, recharge, recovery, flasks, on kill, taken from mana | Damage by kind, crit, penetration, area, duration, cooldown, cost, potency, ailments, reactions, experience, attributes |
| Body | Armour, evasion, barrier, health, regeneration, reduction, physical taken as, recharge, recovery, ailments, stun | Thorns, attributes |
| Legs | Armour, evasion, barrier, health, regeneration, physical reduction, ailments, stun, dodge, stealth | Lingering damage, ailment duration, attributes |
| Boots | **Movement speed**, armour, evasion, barrier, ailments, stun, dodge, stealth | Gold, attributes |
| Gloves | Armour, evasion, barrier, mana, mana regeneration, on hit | **Attack speed**, **cast speed**, crit chance, accuracy, projectile speed, attributes |
| Ring | **Every resistance**, and nothing else | Added damage, gold, rarity, experience, attributes |

`Dino BuildLab list --what slots` prints this by affix. `check` rolls every rarity on every slot at every level, and fails on a side with the wrong number on it, a line on a slot that can't have it, or a slot without enough of each to make a rare at level 1.

**Old items.** `ItemGenerator.Fix` brings whatever's in a save up to these rules. A line stays if its slot can have it and its side has room, and what's missing is drawn from the item's own seed, so a hero's rare is still a rare, and comes out the same each time. Anything already within the rules isn't touched. A save from before this is kept beside itself as `.before-affixes`. A unique answers to none of it.

Uniques have a section of their own, below. Bases come in kinds: weapons by `WeaponKind`, armour by which of armour, evasion and barrier it gives. A hero has ten places: `ItemSlot` is what something is worn as, and `WornSlot` where it's worn, so a ring goes on either of two. Two-handed weapons and the off hand don't mix.

**Families.** Armour is heavy, evasion is leather, and barrier is cloth, which holds `ManaPerBarrier` mana as well, and something giving two of those is that much of each (`Item.Shares`). Weapons go by their kind. A family leans what can roll, so that what something looks like says what it's for: over half of a piece's lines suit it, whichever family it is, and anything its slot takes can still roll on it. It's a share of each side's draw and not a weight on each affix, as more affixes suit heavy armour on a body than suit leather (`AffixCatalog.Weigh`). How all of this looks is in `GEAR.md`.

### Uniques

There are 141 (`UniqueCatalog`), and every one of them is a trade. A unique has what it **gives** and what it **costs**, kept apart (`UniqueDefinition.Boon` and `Burden`), and its card shows the cost apart, in red. `check` holds all of this:

- **It changes what a build is.** Each does something no other item can: a rule, a grant, a trigger, or a stat no affix gives or a condition on one, that no other unique has. What it costs can be what makes it so, and more of something and less of it count as different things.
- **It costs something real.** Every line of what it costs makes something worse by enough to notice, at least 15% of what it touches (20% for a resistance), or is a rule nobody would want for itself (`RuleInfo.Hurts`). The best costs land on what the build that wants the item needs most: All In spends every last point of Mana, the Glass Heart leaves 1 Health, Dead Weight can't dodge.
- **It isn't a keystone.** Nothing a unique gives is a rule a keystone gives, as what the tree already has is no reason to want an item. A keystone's rule can be what one costs.
- **A boss can wear it.** Bosses wear what they drop, so anything that only means something in a player's hands, or would make a boss no boss, is `HeroOnly`, and `Bestiary.Boss` never gives one out.

What they're made of, so that a new one is mostly a line:

| Part | Is | For |
|---|---|---|
| `Gain`, `Lose` | A trigger that gives a stack of a buff, or takes all of it away | Anything that builds up. A player can see a buff and can't see a count kept in their head |
| `Every`, `EveryGain`, `EveryMetres` | A trigger by the clock for as long as something holds, or by the step | Auras, and what standing still or walking builds |
| More `TriggerEvent`s | Stun, Reaction, BarrierBroke, Dash, MinionDied, Used | Nothing past `Flask` is something a Trigger Book can wait for |
| Foe conditions | Marked, Unaware, Up Close, At Range, Isolated and Wounded, and their opposites | Worked out by the blow, and only when something asks (`Condition.Relations`) |
| Own conditions | Surrounded and Alone, which a hero counts once everyone's moved. In Rain and In Snow, from the same weather function a client draws, a few times a second for those who care | |
| More `Per` counters | Enemies near, Ailments on you, empty places, Normal and Unique items worn, items carried, Peril, Omens, Rares slain, seconds Fighting, the same Book in a row, different Books Recently | A sheet is keyed on only the counters it reads (`Loadout.CountersRead`), so gold changing doesn't remake a sheet that only counts enemies |
| `Rule`s | Everything else, in `Combat`, `HeroRules`, `GameServer` and the sheet | `Rule` is a number and a sheet keeps a `RuleSet`, so there's room for as many as there are uniques |

What only uniques do is listed after the keystones in `Rule`. A new one is a value there, a `RuleInfo` in `ModText` with its numbers in the `Rulebook` (and `Hurts` if it's a cost), the code where it matters, and a check in `Dino NetLab uniques`, which does every unique's rule for real on a running server and has been seen to fail with each of a sample of them broken. None of these numbers has been played by a person.

## Enemies

`Bestiary` holds what creatures are: books, mods, and how strength grows with level. A boss wears real items and takes a real keystone, fights with them, and drops what it wore, so a player has usually seen a unique used before they ever hold it.

**Most of what a hero fights is the Saurian Reich's** (read `LORE.md`): war-bands of raptors, spitters, ravagers and hornbacks with greycoats among them, the Ossuary's bones and rotting beasts among the dead, and broods that got out in the wild. A dinosaur is a kind like any other, and `Combat` doesn't know it has one. Its books on `Shelf` are what it does with its jaws, tail, weight and voice (`RakingBite`, `CrushingJaws`, `TailSweep`, `Stomp`, `Gore`, `Roar`, `HungryBite`), and each reaches as far as the animal does from its hips, where it stands: a raptor's head is five metres out in front of them, and a tyrant's ten. An enemy does the first thing it has that's ready and reaches, so the big moves that come first in a list rest after use (`Cooldown` on the page): a tyrant without that roared its stun at anyone near every second and a half, and now roars, sweeps its tail and stomps between bites. A blow is a cone: a circle is a nova and a rectangle a beam, and both go off with a flash and a burst as spells do, which a gore did once. `Bulk` is how far its body reaches at a scale of one, as a dinosaur is far thicker than a person, and `RadiusAt` makes that its body on the server and in the motor.

**Every land's boss is a tyrant.** The keep's boss is whatever `Chronicle.BossKind` names, from a fork of the seed of its own (`BossKindOf`): a Tyrant seven times in ten, otherwise a Bone Tyrant or a Rotting Tyrant, so the story and the fight are the same animal, which `check` holds. It's Unique, so it's 1.7 times its size, and it wears real gear and takes a keystone as any boss does, under the name the chronicle gave it. Greycoats stand guard at the keep (`Legionary`, `Marksman`). The old Warlord is no longer anyone's boss, and stays as the boss the labs test with.

`HealthAt` and `DamageAt` are fitted to the `balance` table: a sample hero who knows nothing of keystones and doesn't choose their gear needs about three hits and survives about ten. `Outfitted` is what keeps that true with legs, hands and a second ring on the sample hero, and comes in as they fill those places. Anything a real build does better is the build working. They climb the way a hero does, fast early on, and what's exponential in them keeps lands past the level cap getting harder. That's an ordinary enemy, which a pack is mostly not: its core has about half the health and under half the damage, so a build that's working goes through them.

### Rarity

An enemy has a `Rarity`, the same four as an item and meaning the same thing: how much more there is to it. `Bestiary.PowerOf` is the whole table.

| Rarity | Health | Damage | Size | Traits | Is |
|---|---|---|---|---|---|
| Normal | x1 | x1 | As it comes | None | What it is, and nothing else |
| Magic | x1.6 | x1.2 | x1.1 | One | Some followers, about half of those who lead, and some of the people of a place |
| Rare | x2.6 | x1.4 | x1.22 | Two | A pack's hard leader, or a hard case at a site |
| Unique | x11 | x1.9 | x1.7 | Three | A boss, which also wears real gear and takes a keystone |

**A trait is something it can do.** An `EnemyTrait` is modifiers and a book, so Burning sets it alight and gives it Immolate, and Swift quickens it and gives it thrown knives. That's what "how many things it can do" comes to: a rare knows two things a normal one doesn't. Those books come first in its list, as books are listed best first, and `EnemyBrainComponent.Abilities` says how many there are, so each waits `AbilityCooldown` after use, or it would do nothing else. What it could already do isn't a new thing to do. A book a trait gives has to be something an enemy will really use, which `check` holds. `Comfort` only counts what's ready, or a swordsman with a thrown knife stands off between throws and does nothing.

Nothing normal has a trait, so followers who take after their leader's trait are magic. Traits are a set (`Bestiary.Trait`), and enemies of one kind, level, rarity and set share a loadout. `Bestiary.Fill` tops a set up to what a rarity has, keeping what it was given.

**It has to be seen.** A line goes round anything better than normal in the colour of what it might drop (`GhostPresenter.OutlineOf`, `OutlineRenderer`), and its name and bar are the same colour. It's drawn from a mask with no depth test, so it can be seen behind a tree. Normal has none, or nothing would stand out. To see one of each: `--parade rarities`.

### Packs

What a player feels is the time between fights, not the head count, so a world is built around that. Between sites stand a few hundred packs (`WorldData.Haunts`), and each has a reason to be where it is: `Ambush` beside a road, `Picket` outside a stronghold, `Den` out in the wild, or a `Camp` with tents, graves or standing stones of its own and often a chest. The world says where and whose, and the game says who. `Territory` gives each stretch of country to a folk, from what's near and from slow noise of its own, so that the Reich's war-bands hold the roads, the dead gather around graves, beasts keep to the deep woods, and cults hold the old stones and harsh land. Those cults serve whatever the land is: fire in the badlands, frost on the snow. `CampStage` runs before the ground is baked and trees are planted, and `PackStage` runs last, on the finished `Passable`, so nothing it places needs repairing.

Spacing is what makes it work. Packs stand about a short run apart, but the gap has to clear two creatures' range of notice, so a hero can take one on at a time. `PackStage.Spacing` closes up with the world's level, and noise opens it out here and there, because an even carpet of packs reads as wallpaper. `UsualSize` starts at three and climbs fast at first, because worlds go up one at a time and the second and third should feel different, then levels off. Some packs are stragglers and a few are out in force.

`Warbands` holds what they're made of, as a `Recipe` of leaders, core and support, and `RecipeFor` says whose a pack is, from a fork of its own. What holds the roads is the Reich's (`Reich`: ravagers, hornbacks and wranglers leading war raptors and legionaries, with spitters and marksmen behind them), or outlaws who sold the land to it (`TurncoatShare`, about a quarter). The dead have the Ossuary's bone raptors and bone ravagers among them, and where the land is wet its rotting beasts instead (`RotShare`). In the wild, two packs in five are broods that got out (`Feral`), one in five a swarm of skitters led by a raptor (`Swarm`), and the rest the land's own animals, while the snow stays mostly the wolves'.

A recipe's shape is the same for anyone. The core dies quickly and is there to be plowed through, and what makes a pack worth stopping for is what's mixed in: someone shooting from behind it, or something large at the head of it. A leader is a different creature from its followers, because that's what a player reads first. Leaders are rarer in early worlds, and in the first stretch of the first world there are none and no support at all. Some leaders are rare, and at higher levels a leader's trait can run through the whole pack, which makes a Burning band of outlaws one thing to deal with: those followers are magic, as nothing normal has a trait.

A pack acts as one. Whoever notices a hero or is struck calls out (`Actor.Alarm`), and it's passed on once, as far as `CallReach`. That has to stay below the gap between packs, or one fight wakes the next. Around a foe they keep `PersonalSpace` from each other (`EnemyBrainSystem.Spread`), as nothing else stops a pack closing into one body with many heads. Only `EncounterSystem.MostWakings` packs wake at a check, as a hero stepping out of a waygate is surrounded by them.

A world holds a thousand enemies or more, so what they give is worked out for that. `ExperienceAt` goes by how much health a kill had, so followers are worth less than what leads them, and what they drop is the next section's business. Flasks fill from kills (`KillsPerFlask`), as a fight now goes on far from any shrine. Bosses and sites work as they did, but their garrisons grow with the world, added on the end of `Spawns`: what's dead is kept by number, so numbers mustn't move. A place that was cleared stays clear, whatever a newer build would have put there.

## Loot

Finding something good is the reward the rest of the game is for, so read this before changing how anything drops. Nothing is ever picked up by walking over it: a find lies where it fell until a player clicks it, and only gold comes by itself.

**A find has a rarity**, and it's what everything that shows one goes by. Colour is `Ui.RarityColour`, and the rest is `LootShow`.

| Rarity | Shows |
|---|---|
| Normal | A faint label |
| Magic | A blue label and a small glow |
| Rare | A gold beam and a rising fifth (`DropRare`) |
| Unique | An orange beam, a sound that rises for its whole flight and breaks as it lands (`DropUnique`), the grass thrown back, its name across the screen |

**A source has a tier.** `LootTables` is one row each, and it's the only place any of this is decided. `TierOf` reads an enemy's real health against what's ordinary at its level, so a new creature is paid for what it takes to kill with nothing to fill in: `Fodder` is what a pack is mostly made of, `Soldier` an ordinary enemy, and `Champion` something large at the head of one. Anything better than normal goes by its rarity instead, as `Magic`, `Rare` and `Boss`, each paying at least twice the last. Chests are `Cache` in a camp, `Chest` at a place on the map, and `Hoard` in a keep.

A row says how many finds, the chance of each rarity, what nothing can be worse than (`Least`), what the first find is sure to be (`Promised`), how far above the source's level its items are (`ExtraLevels`), and how well they roll (`Quality`). Quality is what makes a boss's rare better than a wolf's rare: higher tiers, the top of each roll, the newest base. A boss always gives five rare finds or better, one of them unique, on top of what it wore. Each step up pays at least twice the last, and `check` fails if one doesn't.

Two things move the odds on top of a row. `Loot.Daring` pays up to double for going after what's above you and as little as 0.4 for what's beneath you, going by the strongest hero there. `LootLuck` shortens a dry spell: the odds climb the longer something hasn't come, and fall back when it does, so a player can't walk through a hundred enemies for nothing. A world has one of these, shared by everyone in it.

**How it drops.** `GroundLoot` owns what's lying about. `Spill` sends it out one after another, gold first and the best last, so a fountain ends on what matters. Everything about a drop is worked out on the client from `PickupComponent`, which is sent once: where it came from, when it left, and where it lands. So nothing can be missed the way an event can, and someone who walks up later just finds it lying there, with its sound if they haven't heard it. A find is only ever announced once to a player (`GameSession.Heard`). Better finds go higher and take longer to come down, because the wait is part of it, and `Treasure.UniqueStrike` is both a unique's flight time and the moment its sound breaks, so the two have to stay one number.

**Clicking.** `LootLabels` draws a box for each find and is the only part of the screen that stops the mouse for it. A label only says what something was made from (`Item.PlainName`), as a fountain of full names is too much to read: what was rolled on it, and so its full name, is on its card, and a name that wasn't rolled, which is a unique's or a book's, stays as it is. A book, a page or a sticker has an icon after its name, told apart by outline and in the colour of its rarity, as they're what a fight is made of and were lost among the gear. Labels push each other up rather than overlap, best first. An attack begun elsewhere goes straight across them, the right mouse button always does, and holding shift goes through them altogether, for fighting over what's on the ground. Clicking starts `LootFetch`, which walks the hero there through `HeroIntent` like any other movement, so it can't disagree with the server, and calls itself off if the player does anything else. The server only checks the hero is close enough. It has to arrive, so it uses an exact `WorldPathfinder`: the ordinary one is coarse, which suits a crowd but steps over fences and the metre-wide strips of ground nobody can cross. For the same reason `Landing` won't put a find on the far side of a wall from what dropped it.

Loot is shared: whoever gets there first has it. What's left on the ground is lost on leaving a world or quitting, as it always was.

To see it: `--loot all` drops one of every rarity in front of the hero, and `--loot boss`, `rare`, `magic`, `chest` or `hoard` what that tier gives. `--loot folios` adds books, pages and stickers among them.

## Offerings

Gold burned at a waygate makes the land beyond it worse and richer, in the way a Mythic+ key and a rolled map do. It's the game's reason to want gold, and it's chosen on the way to a new land, never for the one a hero is standing in. Everything comes from a few choices in `Offering` (`Progression/Offering.cs`), which is all that's saved or sent. There are three kinds of thing to pay for, and they're kept apart on purpose.

| Choice | Is | Costs a player | Pays |
|---|---|---|---|
| Peril, 0 to 30 | The key level. Enemies get 15% more health and 8% more damage a step, compounding | Fights | More finds, rarer finds, better rolls, higher item levels, more gold and experience. Nothing normal from Peril 3 |
| Teeming and Overrun, 0 to 3 each | Packs up to 150% larger, and up to 100% more of them: about 4.3 times the enemies at the top of both | Gold alone | Nothing more per enemy. There are simply more of them, none any stronger, and that's the reward |
| Omens, 1 to 5 by peril | What a map's mods are. Dangers that pay in what drops, and things that change the land | Whatever each does | What each says |

**A crowd is part of the world.** `WorldRequest.MorePeople` and `MorePacks` are read by `PackStage` and nothing else, so the ground, the sites and everything before it come out exactly the same, and both ends grow one world from what they're told. `Offering.Shape` makes the request, and it's the only thing that does. `LeastSpacing` holds packs clear of `CallReach` however many are paid for, so a fight can spread to the next pack but can't wake the whole land.

**Danger is what enemies wear.** `Offering.EnemyMods` goes into the session's `Bestiary` and onto every loadout it hands out, bosses too, so there's no new rule in `Combat` and nothing a hero couldn't meet anyway. `TierOf` reads a find's worth from real health, which peril has just changed, so `DropLoot` divides `EnemyHealth` back out. Forget that and peril turns fodder into champions and pays for them twice. `Volatile` and `Rallying` are `Rule`s, and `Die` sets them off through the deferred list, as it can be running inside a loop over what's nearby.

**What it pays** is a `LootBoon`, handed to `Loot.Roll` and `RollGold`. `LootBoon.None` leaves a roll exactly as it was. Sure things, a boss or a chest, take half of the quantity, or the top of the climb is a carpet rather than a heap. `MostUnique` caps a single find's chance, as past that a boss drops nothing else.

**It can be failed.** Everyone in a world shares `Offering.AllowedFalls`. One more than that and the offering is `Broken`: the land stays as it is and pays no more than any other, and nothing is proven. That's what a timer is in a dungeon, on a game with no dungeons. An offering of more enemies alone can't break, as there's nothing there to take back.

**It's a climb.** A hero can call down `PerilProven + 2`. Freeing a land under an unbroken offering proves its peril, and doing it without a fall proves one more and pays two hoards instead of one, on the spot. Everyone there at the time gets the credit. Whoever steps through pays for everyone with them, and it's `GameServer.Pay` that decides: what a client shows is only what it thinks that will say.

**The sums are what stands behind this.** `Dino BuildLab offering` runs whole worlds at each step, and `check` holds that each step pays more than the last and that through Peril 15 a kill pays more than it takes, for a hero no stronger than before. Past that it's the hero who has to have grown. A step of peril is priced against about half a world's gold, and grows faster than gold does. A crowd is priced so that it pays for itself several times over, as it's the one thing here with no risk in it. None of these numbers have been played by a person.

An omen's id is saved, and the order of `Omens` is what's sent, so a new one goes on the end. To see one: `--peril 5 --teeming 2 --overrun 1 --omens volatile,night`, with `--gold` and `--proven` for walking up to a waygate with `--panel waygate`.

## Networking

`HeroRules.Step` is still a pure function of the command, the hero's kit and the world. Both ends compile the same `ActorSheet` from the same `HeroState`, so they agree on every number without sending any.

What only the server can know reaches the client in `HeroSimState`: the `State` and `Counters` that pick which sheet is in force, and `MoveRate`, `ActionRate` and `Held` for what's being done to the hero. So a new conditional stat needs no networking at all. Buffs come with `HeroUpdate`.

What a blow does is the server's business. What a client sees of it comes as `DamageEvent`, `PageEvent`, `HealEvent`, `LinkEvent` and `ReactionEvent`.

## Testing

```
dotnet "Dino BuildLab/bin/Release/net8.0/Dino BuildLab.dll" check      # The sums, the tree, items, books, old saves
dotnet "Dino BuildLab/bin/Release/net8.0/Dino BuildLab.dll" bench      # What it costs, and that blows allocate nothing
dotnet "Dino BuildLab/bin/Release/net8.0/Dino BuildLab.dll" balance    # How a fight goes at each level
dotnet "Dino BuildLab/bin/Release/net8.0/Dino BuildLab.dll" list       # Everything there is, with its text as the player reads it. --what cards, sheet or terms for what TOOLTIPS.md covers
dotnet "Dino BuildLab/bin/Release/net8.0/Dino BuildLab.dll" books      # What every sentence comes to on a plain page, and what's on the shelf comes to
dotnet "Dino BuildLab/bin/Release/net8.0/Dino BuildLab.dll" density    # How much there is to fight in a world, and how long between fights
dotnet "Dino BuildLab/bin/Release/net8.0/Dino BuildLab.dll" loot       # What each source gives, what a whole world comes to, and the longest dry spells
dotnet "Dino BuildLab/bin/Release/net8.0/Dino BuildLab.dll" offering   # What each step of peril costs and pays, and what more enemies come to
dotnet "Dino NetLab/bin/Release/net8.0/Dino NetLab.dll" pace           # A real client on foot, from pack to pack: kills, time in fights, falls, and how often something good drops
dotnet "Dino NetLab/bin/Release/net8.0/Dino NetLab.dll" loot           # What's on the ground: who sees it, who gets it, and that nobody gets it without asking
dotnet "Dino NetLab/bin/Release/net8.0/Dino NetLab.dll" offering       # What's paid at a waygate, for real: who pays, what it does to the land, what falling costs, what's paid out
dotnet "Dino WorldLab/bin/Release/net8.0/Dino WorldLab.dll" packs      # Who stands where, as a map
dotnet "Dino NetLab/bin/Release/net8.0/Dino NetLab.dll" foes           # What an enemy's rarity comes to: who turns up as what, that a trait's book gets used, and what a client is told
dotnet "Dino NetLab/bin/Release/net8.0/Dino NetLab.dll" build          # Every ailment, reaction and keystone, for real on a running server
dotnet "Dino NetLab/bin/Release/net8.0/Dino NetLab.dll" uniques        # Every unique's rule, for real on a running server
dotnet "Dino NetLab/bin/Release/net8.0/Dino NetLab.dll" books          # Every sentence, for real on a running server
```

Run `check` and `build` after any change here, `uniques` if a unique or anything one reads moved, `game` if the hero's step or the protocol moved, `loot` and `pace` if anything about drops did, and `offering` if anything about a waygate or what's paid at one did. A bot proves nothing about clicking: post real mouse messages for that. To look at a build in the real game: `--hero 30 --talents key.tidewalker,pyre.firebolt --equip drowned-king`, with `--panel talents`, `hero` or `skills`.
