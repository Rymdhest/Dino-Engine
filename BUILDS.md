# Builds

How heroes and enemies add up: stats, books, the talent tree, items, and the fights they all end in. This is the game's main idea, so read it before changing any of them.

## The one idea

**There is one kind of fighter.** A hero, an enemy and somebody's skeleton are each an `Actor`: a `Loadout` of modifiers, a compiled `ActorSheet`, and `Vitals`. `Combat` works on Actors and doesn't know which it has. A hero is just an Actor with a player on the other end, and an enemy is a list of stats in `Bestiary`. Anything a hero can do, an enemy can do, so every new phrase, rule and item is at once something the world can do to a player.

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

There are no skills. Everything anyone does in a fight is a book written out of phrases: read `BOOKS.md` before changing anything an actor can do. This is only how a book joins the sums.

`Scribe.Read` turns a book into a `BookPlan`, the same whoever holds it, and `ActorSheet.Profile` turns that into a `BookProfile` for one actor, with a `PageProfile` for each page. Damage is split in two, and each page is found as one or the other, whatever's in the hand. An Attack Page deals all your Attack Damage: the weapon's base damage, which is of the base's own type (a caster's weapon strikes softly, in its element), and whatever's added to Attacks. A Spell Page deals all your Spell Damage, which is only what's added to Spells: wands and staves roll it as suffixes (`spell-fire` and the rest), and nothing else does yet, so a caster lives on their weapon's suffixes. Added damage on rings and weapons (`added-fire` and the rest) is for Attacks. Each is dealt of every type it has, and conversions and the tree do the rest. On top of that a page's `Potency` scales everything it does. An enemy has as much Spell Damage as its Attack Damage (`Bestiary.SpellFor`), less its usual share for Spells.

Heroes carry books on a bar of six: the mouse buttons, Q, 1, 2 and 3. A book on the bar isn't in the pack (`HeroState.Bar`), as a ring on a finger isn't. Every hero has one from the start, on the left mouse button: an Attack Page in a Cone, that costs nothing. Books can also go off by themselves (`M.Trigger`). Those are on `Shelf`, and what sets them off comes from the tree or from what's worn.

A book works the same on any slot. The mouse buttons are held, and what the other keys send is a press, so that a quick tap is never lost and one made late in a cast waits for it. `HeroRules.KeyPresses` has a held key press again whenever its book could go, which is what makes them match. It waits for the cooldown as well as the hero, as there's one queue, and a key on cooldown would otherwise keep it from a second key held down.

## The tree

One tree, no classes, and it's grown rather than drawn: `TalentTree` places everything from the tables in `TalentContent` (one file for each side of it: `.Mage`, `.Rogue`, `.Warrior`, `.Battle`). Twelve regions stand round a root. Mages are at the top (the Arcanum, the Long Winter, the Storm), warriors at the bottom (the Forge, the Bulwark, the Marrow), rogues on the right (the Mire, the Hunt, the Wilds, the Knave), and between warrior and mage on the left, the battle mages (the Pyre, the Host). Each region shades into the next, and what's between two is a little of both, so where something stands is part of what it means. The Arcanum is opposite the Forge.

**The middle is six ways out, and nothing else** (`TalentContent.Beginnings`), evenly round the root, as a person found the twelve ways and a ring joining them all meaningless. They go round the attributes: Intellect, Intellect and Dexterity, Dexterity, Dexterity and Strength, Strength, Strength and Intellect. Each is a travel node, then a strong notable two points out that says what kind of hero this is going to be (The Still Mind, Storm in the Blood, The Keen Eye, Edge and Nerve, Unbowed, Ember and Bone), then a fork into two regions: the one it points into and the next going round. Each of those opens with a notable of its own right at the fork (`Region.Entry`, the twelve "First" notables), so the fork is a second choice, four points from the root. Nothing else joins onto a fork, no road comes inside the Old Road, cross links between two ways are only made past it, and so are bridges between two regions of different ways: the Old Road, eight points out, is the first place one way meets another. The middle is the most symmetric part of the tree, and it gets rougher going out.

- **Travel nodes** are the weakest thing on it: +5 to the region's attribute and nothing else. They make up the lanes, which branch out across a region, bending and forking, and they stand `LaneSpacing` apart, further than anything that gives more, as getting about used to cost too much.
- **Small nodes give one thing, ever.** Only notables and keystones give more, and `check` holds that. A small node is called what it gives (`Cards.TitleOf`), and a notable or keystone has a name.
- **Pods** curl off a lane: a few small nodes that each give the same one thing, `Spacing` apart, then a notable that gives two or three.
- **Wheels** are rings of rarer and stronger things (penetration, maximum resistance, conversion, area, projectiles, each of the three pools and getting them back), with notables on the rim and sometimes a keystone at the heart.
- **Roads** are the highways: the Old Road, the Long Road and the Far Path at the edge, all three all the way round, and four ways out from the Old Road to the Long Road. Their nodes stand `RoadSpacing` apart, so a few points cross a lot of tree, for nothing much on the way. A lane only crosses a road where it stops on it.
- **Bridges** join neighbouring regions, with a notable in the middle for whoever walks both.
- **Keystones** stand where their idea lives, a couple by the middle, many between the roads and the rest out past the Far Path, and not evenly round a ring. Each is one rule that makes a different build possible, with a real cost. They aren't uniques: a unique is a trade on one item, and a keystone changes how something works for the whole build.
- **Waygates** come in three pairs, each opening onto the other side of the tree, with a little movement speed. Take one and its twin can be taken from there.

It has to stay a tree a person can read, so `check` holds its shape: six ways out, evenly round, each with its notable two points out and forking into its regions' opening notables, no two ways meeting inside the Old Road, the Far Path a whole loop, no two links crossing and no link passing through a node, judged as they're drawn with a road's or a wheel's arcs and not their chords (`TalentTree.Shape`), as the grower once tested chords and laid clusters across roads, no small node at a dead end (`Prune` takes back what a lane leaves past its last cluster), no two nodes closer than `Spacing` asks, every node reachable, a point going further on a road than on a lane and further on a lane than in a cluster, and keystones at many depths. Everything is placed from a stable hash and never `string.GetHashCode`, so every machine grows the same tree. From far off it's a circle with ragged edges, as lanes and pods go as far as they go.

There are no classes, so what a hero *is* gets worked out from where they've spent their points (`TalentTree.TitleOf`): the region they've given most to names them, the next colours it, and a keystone can have the last word.

Every node has a picture worked out from what it gives (`TalentIcons`), by stat and then by what it's narrowed to, never by a number, so two nodes that give the same thing look the same wherever they are. A keystone names its own (`Keystone.Emblem`), and nothing else uses it. How each is drawn is `UI/TalentArt.cs`, as shapes made into signed distance fields (`SdfPicture`) and baked into one atlas on a worker thread the first time the tree is opened (`TalentAtlas`), which `GUI.frag` draws crisp at any size (mode 6). The panel draws the tree over a night sky worked out each frame (`StarSky`, `Shaders/Studio/StarSky.frag`), kept dim on purpose, as the tree is what's being looked at, and names the six beginnings from the view it opens at.

Points: two a level, one for each land set free, and three to start with, which is a way's notable and its fork. Refunds are free for now, so that builds can be tried: right click a node, or Forget all.

Node ids are saved in heroes and come from where a node is grown: `start.{way}` for a way's notable with `.1` and `.2` before and after it, `{region}.{entry}` for a region's opening notable, `{region}.{lane}.{n}` along a lane, `{region}.{pod}` for a notable and `{region}.{pod}.{i}` for its small nodes, `key.{id}`, `gate.{id}.{end}` and `way.{road}.{n}`. So none of those ids can be renamed, and a lane can't be grown differently, without moving what someone has taken. Names, numbers and effects can change at any time. `TalentTree.Sanitize` looks after anything that goes missing, so a small change costs someone a few points back in their pocket, never their hero. When the tree is grown again so that nothing is where it was, a save version gives every hero all their points back (`SaveGame.TreeVersion`), keeping a copy of the old save.

### Adding to it

- **A notable**: a pod on a lane (`P`, `P2`, `P4`, a pod with three, two or four small nodes before it), or on a wheel's rim (`N`), in `TalentContent`.
- **A keystone**: a `Key` in a region's `Keystones`, at a radius and angle of its own, or a wheel's `Heart`. Its rule goes in `Bestiary.FitsABoss` if a boss can't use it.
- **A new rule**: a value in `Rule`, its words in `ModText` with its numbers in the `Rulebook`, and the code where it matters. `Combat` is where most belong. A keystone's rule needs a check in `Dino NetLab build`, or `books` if it's about pressing a book.
- **A new stat**: a value in `Stat`, a line in `StatNames`, a term in `Lexicon`, a line in `StatBook`, and read it. `check` fails on a stat that changes nothing, that nothing gives, or that nothing explains. `TOOLTIPS.md` says how all of that has to read.
- **Look at it**: `Dino BuildLab tree` draws the whole tree to a picture, with anything crossing or crowded marked (`--span` and `--x`, `--y` for part of it), `--node id` says where one node went, and `icons` draws every picture. `list --what smalls` reads every small node by name, `--what keystones` says how deep each keystone is, and `--what ids` lists every id. In the game, `--panel talents --treeview zoom,x,y` opens it at that zoom and place for a picture.

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

**Books and pages** roll lines by the same rarities, from a catalogue of their own (`FolioAffixes`), with room for more Pages or Sentences among their prefixes: read Rolled lines in `BOOKS.md`.

**Old items.** `ItemGenerator.Fix` brings whatever's in a save up to these rules. A line stays if its slot can have it and its side has room, and what's missing is drawn from the item's own seed, so a hero's rare is still a rare, and comes out the same each time. Anything already within the rules isn't touched. A save from before this is kept beside itself as `.before-affixes`. A unique answers to none of it.

Uniques have a section of their own, below. Bases come in kinds: weapons by `WeaponKind`, armour by which of armour, evasion and barrier it gives. A hero has ten places: `ItemSlot` is what something is worn as, and `WornSlot` where it's worn, so a ring goes on either of two. Two-handed weapons and the off hand don't mix.

**Dual Wielding.** A one-handed weapon goes in either hand (`WornSlots.For(item)`), but only into the off hand when it's put there, as a new weapon is usually meant to replace the old. One in each hand is `Condition.DualWielding`, and the hero attacks with both as one weapon (`Item.Paired`): they take turns, so it's their damage, attack time and critical chance averaged, then `Rulebook.DualWieldSpeed` more attacks a second, and it carries both kinds' tags, so a sword and a dagger are both a Sword and a Dagger, and a wand in either hand makes every book a Spell. Both weapons give what their kind does and everything rolled on them. That's the trade against a shield: two weapons' suffixes and leech instead of a shield's defences, and a weapon in the off hand isn't `HoldingShield`. A weapon alone in the off hand is simply the weapon, and isn't Bare Hands. A body with two weapons swings them in turn (`CharacterAnimator`, which mirrors every other gesture onto the other hand), which is only for show. Twist the Knife is the one node that reads it so far.

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
| More `Per` counters | Enemies near, Ailments on you, empty places, Normal and Unique items worn, items carried, the Land Level, Omens, Rares slain, seconds Fighting, the same Book in a row, different Books Recently | A sheet is keyed on only the counters it reads (`Loadout.CountersRead`), so gold changing doesn't remake a sheet that only counts enemies |
| `Rule`s | Everything else, in `Combat`, `HeroRules`, `GameServer` and the sheet | `Rule` is a number and a sheet keeps a `RuleSet`, so there's room for as many as there are uniques |

What only uniques do is listed after the keystones in `Rule`. A new one is a value there, a `RuleInfo` in `ModText` with its numbers in the `Rulebook` (and `Hurts` if it's a cost), the code where it matters, and a check in `Dino NetLab uniques`, which does every unique's rule for real on a running server and has been seen to fail with each of a sample of them broken. None of these numbers has been played by a person.

## Enemies

`Bestiary` holds what creatures are: books, mods, and how strength grows with level. A boss wears real items and takes a real keystone, fights with them, and drops what it wore, so a player has usually seen a unique used before they ever hold it.

**Most of what a hero fights is the Saurian Reich's** (read `LORE.md`): war-bands of raptors, spitters, ravagers and hornbacks with greycoats among them, the Ossuary's bones and rotting beasts among the dead, and broods that got out in the wild. A dinosaur is a kind like any other, and `Combat` doesn't know it has one. Its books on `Shelf` are what it does with its jaws, tail, weight and voice (`RakingBite`, `CrushingJaws`, `TailSweep`, `Stomp`, `Gore`, `Roar`, `HungryBite`), and each reaches as far as the animal does from its hips, where it stands: a raptor's head is five metres out in front of them, and a tyrant's ten. An enemy does the first thing it has that's ready and reaches, so the big moves that come first in a list rest after use (`Cooldown` on the page): a tyrant without that roared its stun at anyone near every second and a half, and now roars, sweeps its tail and stomps between bites. A blow is a cone: a circle is a nova and a rectangle a beam, and both go off with a flash and a burst as spells do, which a gore did once. `Bulk` is how far its body reaches at a scale of one, as a dinosaur is far thicker than a person, and `RadiusAt` makes that its body on the server and in the motor.

**Every land's boss is a tyrant.** The keep's boss is whatever `Chronicle.BossKind` names, from a fork of the seed of its own (`BossKindOf`): a Tyrant seven times in ten, otherwise a Bone Tyrant or a Rotting Tyrant, so the story and the fight are the same animal, which `check` holds. It's Unique, so it's 1.7 times its size, and it wears real gear and takes a keystone as any boss does, under the name the chronicle gave it. Greycoats stand guard at the keep (`Legionary`, `Marksman`). The old Warlord is no longer anyone's boss, and stays as the boss the labs test with.

`HealthAt` and `DamageAt` are fitted to the `balance` table: a sample hero who knows nothing of keystones and doesn't choose their gear needs about three hits and survives about ten. Nothing on the tree gives health on every node, so the sample takes the notables that give its pool, Health or Barrier, as a sensible player would, and its Barrier counts towards how many hits it survives. `Outfitted` is what keeps that true with legs, hands and a second ring on the sample hero, and comes in as they fill those places. Anything a real build does better is the build working. They climb the way a hero does, fast early on, and what's exponential in them keeps lands past the level cap getting harder. That's an ordinary enemy, which a pack is mostly not: its core has about half the health and under half the damage, so a build that's working goes through them.

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

**Clicking.** `LootLabels` draws a box for each find and is the only part of the screen that stops the mouse for it. A label only says what something was made from (`Item.PlainName`), as a fountain of full names is too much to read: what was rolled on it, and so its full name, is on its card, and a name that wasn't rolled, which is a unique's or a book's, stays as it is. A book, a page or a sentence has an icon after its name, told apart by outline and in the colour of its rarity, as they're what a fight is made of and were lost among the gear. Labels push each other up rather than overlap, best first. An attack begun elsewhere goes straight across them, the right mouse button always does, and holding shift goes through them altogether, for fighting over what's on the ground. Clicking starts `LootFetch`, which walks the hero there through `HeroIntent` like any other movement, so it can't disagree with the server, and calls itself off if the player does anything else. The server only checks the hero is close enough. It has to arrive, so it uses an exact `WorldPathfinder`: the ordinary one is coarse, which suits a crowd but steps over fences and the metre-wide strips of ground nobody can cross. For the same reason `Landing` won't put a find on the far side of a wall from what dropped it.

Loot is shared: whoever gets there first has it. What's left on the ground is lost on leaving a world or quitting, as it always was.

To see it: `--loot all` drops one of every rarity in front of the hero, and `--loot boss`, `rare`, `magic`, `chest` or `hoard` what that tier gives. `--loot folios` adds books, pages and sentences among them.

## Lands

What a land holds beyond its seed and level is in `LANDS.md`: the climb, omens, orders and the Haven. What it means for builds is here.

**Danger is what enemies wear.** A land's omens of what enemies are (`LandTerms.FoeMods`) go into the session's `Bestiary` and onto every loadout it hands out, bosses too, so there's no new rule in `Combat` and nothing a hero couldn't meet anyway. The tyrant's wrath goes on the tyrant alone (`TyrantMods`). `TierOf` reads a find's worth from real health, which omens have just changed, so `DropLoot` divides `EnemyHealth`, and the tyrant's wrath, back out. Forget that and omens turn fodder into champions and pay for them twice. `Volatile` and `Rallying` are `Rule`s, and `Die` sets them off through the deferred list, as it can be running inside a loop over what's nearby.

**What a land does to heroes** goes on last, through `HeroBuild.Compile(hero, buffs, land)`, on both ends, as it's part of every step.

**What it pays** is a `LootBoon`, handed to `Loot.Roll` and `RollGold`. `LootBoon.None` leaves a roll exactly as it was. Sure things, a boss or a chest, take half of the quantity, or the top of the climb is a carpet rather than a heap. `MostUnique` caps a single find's chance, as past that a boss drops nothing else.

**The sums are what stands behind this.** `Dino BuildLab lands` prints what a land of each level holds and what each omen comes to over whole worlds, and `check` holds that deeper lands hold more omens, each worse, and pay more, that every omen pays for itself somehow, and that whatever an omen names an enemy or a hero really wears. None of these numbers have been played by a person.

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
dotnet "Dino BuildLab/bin/Release/net8.0/Dino BuildLab.dll" books      # What every phrase comes to on a plain page, and what's on the shelf comes to
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
dotnet "Dino NetLab/bin/Release/net8.0/Dino NetLab.dll" books          # Every phrase, for real on a running server
```

Run `check` and `build` after any change here, `uniques` if a unique or anything one reads moved, `game` if the hero's step or the protocol moved, `loot` and `pace` if anything about drops did, and `lands` if anything about a land's terms, the climb or the stash did. A bot proves nothing about clicking: post real mouse messages for that. To look at a build in the real game: `--hero 30 --talents key.tidewalker,pyre.firebolt --equip drowned-king`, with `--panel talents`, `hero` or `skills`.
