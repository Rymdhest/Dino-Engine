# Books

There are no skills. Everything anyone does in a fight, from a sword swing to a meteor, is a book of pages, and a hero and an enemy use them the same way. Read this before changing anything about what a hero or an enemy can do.

## The one idea

**A book is a program, and a page is a line of it.** Nothing about what a book does is hand-made: `Scribe` reads the pages and works out numbers from them, so any attack or spell from any ARPG is a few pages and a few sentences, and a new phrase is a line in a table.

**Books, pages and sentences are all items, found in the world, and each holds the next.** A book has places for pages and a page has places for sentences (`Item.Held`). Every book holds one Page and every page one Sentence, and anything more is a line rolled on it, as gear rolls its lines: read Rolled lines, below. Nobody writes anything: a page is found saying what it says (`Scribe.Roll`) and says it for good, so a build is what's been found, in what order, with which sentences on what.

```
Items (saved, sent)  --Scribe.Bind-->  Book  --Scribe.Read-->  BookPlan  --ActorSheet.Profile-->  BookProfile
book > page > sentence                  pages in order          what it says                       one actor's finished numbers
```

`BookPlan` is what a book says, whoever holds it. `BookProfile` is what it comes to in one actor's hands, with a `PageProfile` for each page, and that's what a blow reads. So everything `BUILDS.md` says about why blows are fast is still true: a hundred archers are one set of numbers.

## A page

A page is found as a Projectile Page or an Area Page, and as an Attack or a Spell (`Page.Hand`), unless it raises Minions, when it's a Summon Page (below). What else it says depends on its kind.

| Part | Projectile page | Area page |
|---|---|---|
| Start | From you, if it's the first page, and from where the last struck if it isn't | The same |
| Then | Towards the cursor, if it's the first, and on the way the last was going if it isn't | A shape, a cone, a circle, a line or a rectangle, and whether it happens at once or lingers, found with it |
| Sentences | Whatever's been written on it, in as many places as it has | The same |
| Outcome | What happens on impact, which is one thing | The same, to everyone in the shape |
| Its own numbers | Cost, Projectile Speed, Potency | Cost, Area, Potency |

**Nearly every page deals damage.** An Attack Page deals 100% of your Attack Damage and a Spell Page 100% of your Spell Damage, before Potency, of every type that Damage has. Raising Minions and moving you there are rarer finds, and always Spells: what a found page can do is the phrases with a `Found` share in `Phrases`, and how often is that share. Moving only comes on a projectile, as an area that starts from you would leave you where you are.

**A Summon Page only summons.** Raising Minions is a kind of page of its own (`PageKind.Summon`), and nothing else is: it doesn't fly and has no shape, so it has no Projectile or Area tag, and it raises its Minions at once: beside whoever used it if it's the first page, and where the last page struck if it isn't, as an area would, so a projectile can carry them somewhere else (`Combat.Call`, `Rulebook.MinionRing`). It needs nobody to reach itself, though a page before it that reaches nobody turns no page to it, and it has no impact to turn from, so nothing can come after it. It's always a Spell. A sentence goes on one only if both its sides are about Cost, Use Time or a Cooldown, as Potency, flight and where a page starts mean nothing to it, and what's rolled on one is never about a blow (`FolioAffix.Blow`): its suffixes are one more Minion (`page-minions`) and Minion Duration, which a page now carries (`PageProfile.MinionDuration`). `Scribe.Fix` makes any page that raises a Summon Page, so a save's raising projectile is one, its lines that can't be on it are rolled again, and a sentence that no longer goes on it comes back to the pack (`SaveGame`'s spill). Its card and the book panel say how many Minions, for how long.

**What its Minions fight with.** A Summon Page has three places after its Sentences (`Item.MinionPlaces`): a weapon, the other hand, and a book. The other hand takes a shield or a weapon that takes one hand, and nothing while the weapon takes both, as on a hero (`HeroState`'s places, `Item.FitsOffHand`). Its Minions hold them as a hero does: two weapons are wielded as one (`Item.Paired`, `DualWielding`), a shield gives what it gives (`HoldingShield`), and a weapon alone in the other hand is simply the weapon. Their Attack Damage is the weapon's and their Spell Damage only what it rolls (`Bestiary.MinionOf`), and they're seen holding it all: a skeleton's model is named for the looks of both hands (`Gameplay/MinionLooks.cs`, `risen:seed-traits:seed-traits`), and the client grows a bare skeleton with those very meshes in its hands (`Outfit.Hold`, `CharacterFactory`), carried as a hero carries them. A turret isn't drawn holding anything. `Dino AnimLab sheet --body risen-pair` and `risen-shield` show it. and the book is all they do, used on Enemies as often as they can: again as soon as its Use Time is over and they can pay for it (`EnemyBrainSystem`, `Actor.Pays`). They pay as a hero does, at the start of each use, out of a hero's Mana: `Rulebook.BaseMana`, coming back by `ManaRefill`, plus whatever their owner has for Minions, and never what they haven't got. Their Mana is sent (`ManaComponent`) and drawn under each one's Health. Without a book a Minion does what its kind always has, for nothing; without a weapon it strikes with its kind's own. The book has to be pressed (Active), and can't hold a Summon Page, or Minions would raise Minions without end (`Item.FitsMinions`, which `HeroState`'s places, `SaveGame` and `Scribe.ForMinions` all hold). A place goes as deep as a Sentence on a Page of that book in a Summon Page in a Book (`ItemPlace.Deepest`), and the wire as deep (`Protocol.MostDepth`). A Minion lasts `MinionSeconds`, 10 s before anything makes it longer, which the page's card says. Nobody has watched one fight.

**A Turret Page** is a Summon Page whose kin is `turret` (`Bestiary.Turret`, found as often as Risen Bones, the only other thing a page raises): one Turret, set down where a Summon Page raises, with the same weapon and book places. It never moves (`EnemyKind.Rooted`, `Actor.Rooted`: no walking, no heel, and no blow pushes it), and uses the book it was given as often as it can (`Actor.Eager`) without paying any Mana, so it has none to show. Without a book it looses arrows (`Shelf.Shot`). What it was given hangs in the air over a stand, and swings, thrusts and looses as if someone nobody can see were holding it: the turret is a body on a person's rig with nothing of the body drawn (`MinionLooks.HoverPrefix`, `Outfit.Hold` with no body), carried as a hero carries what's in their hands, with no feet to part grass or make a sound, and a plain bow when it was given nothing. The stand is a prop of its own that never turns (`Actor.Stand`, `CharacterFactory.Turret`: a timber tripod under a banded drum), which goes when the turret does, and what it held falls. It isn't in `Bestiary.Everyone` for the same reason the training dummy isn't, and isn't solid to anyone walking. `Dino AnimLab sheet --body hover-sword --do forehand` and `hover-bow` show the weapon on its own. Nobody has seen one. Everything else a phrase can say, healing, buffs, ailments, pushing, water and doing nothing but turning the page, is only on what's already written (`Shelf`), until something brings it to a player.

**A follow-up page starts where the last one struck, and an impact is reaching someone.** A book's pages go in order: when a projectile reaches an Enemy, or a friend for one that only helps, the book turns to the next page, from there, the same way. That's all a chain is: two projectile pages one after the other are a projectile that pierces one. A projectile that misses, runs out or hits a wall has no impact and the book stops there, and so does one that comes down on nobody. A sentence can say that the ground or anything solid counts. An area turns the page from everyone it reaches, as a projectile turns it from whoever it reaches: each Enemy in it is an impact, and the next page starts from them, on the way out from where the area was, so an area that reaches five turns five pages, which `MostMade` holds as it holds any branching (`Combat.Cover`'s `turns`). It does that once, as it lands, and a lingering one only on its first pulse (`ZoneState.Turned`); one that reaches nobody turns nothing, and only an Enemy turns it. A Summon Page has no impact to turn from, so it can only be the last (`Scribe.Ends`): `HeroState.Move` won't put anything after one, and a gap in a book is only a gap.

**Potency goes on from page to page.** A page starts from where the one before it left off, so a first page at 90% and a second that doubles it comes to 180% (`PagePlan.Carried`). It's what's on the page that goes on: anything a build adds is applied to each page where it stands and doesn't compound, or a little Potency on the tree would be worth more the longer the book.

**The book's Cost is every page's Cost added up.** It's paid when the book is used, and only then.

**Attack or Spell is the page's, whatever's in your hands.** Damage is in two kinds. Attack Damage is your weapon's, of whatever type its base deals, and whatever's added to Attacks: Attack Speed, Accuracy, and it can be Evaded. Spell Damage is only what's added to Spells, which wands and staves roll as suffixes: Cast Speed, and it can't be Evaded. Nothing gives Spell Damage for nothing, so a Spell Page in the hands of someone with none does nothing at all. The tree's Attack and Spell nodes mean what they say, and a book can be both.

**How long a book takes is all its pages together.** If its first page is an Attack, the weapon's own pace is the base of it (`PageUseTime` at bare hands, by `AttackTime`, less by Attack Speed), and Attack Pages after it add nothing. Every Spell Page adds `CastTime`, less by Cast Speed. Then whatever any page says of Use Time, on a sentence, goes for the whole book (`PagePlan.UseScale`, `BookProfile.UseTime`).

**What's in the hand says what a page looks like.** A Spell is a ball of what it deals, and so is anything done with a wand or a staff. An Attack with a bow is an arrow, and with anything else it's what's in the hand, thrown.

## What's decided when

- **Nearest and random** look at the moment the page opens, and skip anyone this cast has struck, which is what makes a chain go on to someone new. With nobody there, it uses the cursor.
- **Direction is fixed at launch.** There's no homing. `BallisticComponent` is sent once and flies straight, and that's what makes hundreds of projectiles cost almost nothing.
- **A projectile flies past what it's aimed at**, to the end of its range. Only reaching someone is an impact: the end of its range or a wall is nothing, unless the page is `Grounded`.
- **What falls** comes out of the sky onto where the page is going, and lands exactly when it says it will. It's an impact if someone's under it, or if the ground counts.
- **Lingering** spreads the Outcome over the Duration, in pulses, and pays a premium for being something Enemies can walk out of. How long is one base, `Rulebook.LingerTime` (4 s), whatever page it's found on, and only a sentence makes it longer or shorter (`PageEffect.Duration`, on an Area Page, "Lingers 50% longer"), on top of Page Duration from a build. There were once two lengths, and a page found as the long one lingers as any other.
- **What starts from an impact starts touching whoever took it**, so it goes straight through them. Without that (`off`, through `Combat.Turn`) a page that turned on someone would be stopped by them at once.
- **A leap keeps count.** A follow-up carries what's left of `Chains`, as one that started over would never end.

## Phrases pay for themselves

Every phrase has `Weights`: flat Cost, and `more` or `less` of Cost, Potency, Speed, Area, Range, Duration and Use Time. Each is a multiplier of its own, which is what those words already mean everywhere else, and can't reach zero the way `reduced` can. A Circle costs more than a Cone, as it reaches everything around you, and lingering costs more still.

**Potency** is the one number every Outcome goes by. A page's Potency scales its Damage, healing and ailments alike.

`MostPotency` is the ceiling on what stacked drawbacks can come to. `BuildLab books` tables what every shape and sentence is worth, as nobody has played any of this, and a person's opinion outranks that table.

## Sentences

A sentence is an item with one good thing and one bad thing on it, and that's all it is. Nothing about one is decided by hand: `Sentences.Roll` picks anything from `Sentences.Lines` that can be good, anything that can be bad, and how far each goes, so any combination can turn up. A rarer one goes further on its good side and less far on its bad. A line is a new thing a sentence can do, and `PageEffect` values go on the end, as they're saved by name and sent by number.

Each side has to mean something on the same kind of page, so a sentence is for a projectile page, an area page or either (`Sentences.Fits`), and it won't go on the other. What adds up does (Projectiles, Pierce, Chains, a Cooldown), and the rest are multipliers of their own as phrases are, so anything can be stuck beside anything. Cost as a drawback is flat, as a share of nothing is nothing and most of what starts out is free.

**A Unique sentence** has on its good side what nothing else can, and its bad side is rolled as a rare's would be. It's what changes where a page starts, where it goes, or what counts as an impact, as no page says anything of those:

| Unique | What it does |
|---|---|
| Grounded | Hitting the ground or anything solid is an impact, so what misses, or falls on nobody, still turns the page |
| From the cursor, the nearest Enemy, or a random Enemy nearby | Where the first page starts. On any other page it means nothing, as those start where the last struck |
| Towards the nearest Enemy, towards you, or in a random direction | Where a projectile goes, first page or not |
| Falls | Comes down out of the sky onto where it's going, rather than flying there |

What's left to chance pays for itself by itself (`Sentences.Weighting`), and counts as Wild. Only a sentence can be Unique for anything in a book, and only for one of these (`Sentences.IsUnique`, which `ItemGenerator.Fix` and `Sentences.Fix` hold): `Loot` makes `UniqueSentenceShare` of what's found Unique one of these instead of something worn.

`Sentences.Written` is for what's already written and has to come out the same: creatures fight with those, and the Unique ones by the names above. They go by the names clauses had, as a save from before sentences does too.

## Rolled lines

A book and a page roll lines exactly as gear does (`Progression/FolioAffixes.cs`): Normal has none, Magic a prefix and a suffix, Rare two of each, and a line is an affix id and a value, saved and sent as gear's are. A book or a page is never Unique. What's rolled is modifiers, and they reach no further than the book: what's on a book counts on every page in it, and what's on a page counts on that page wherever it goes. `Scribe.Bind` gathers them (`Book.Mods`, `Page.Mods`) and `Scribe.Read` puts both on each `PagePlan.Mods`, so a blow reads them like anything else a build has: where the page stands, and not carried on to the next, as Potency on the tree isn't.

**A prefix is what it holds and what using it asks of you, and a suffix what its pages do.** This is the one place the sides don't go by `StatSides`, as a book is neither worn nor a hero's: its prefixes are room and the economy of using it, and its suffixes the blow. Every card says what it holds before anything's rolled ("Holds 1 Page"), and the room a prefix gives reads "Holds 2 more Pages". A page on its own shows no damage and no sums (`Cards.LoosePage`): only its base Cost (at your level, before anything of yours) and Use Time (a Spell's own, or as your Weapon for an Attack), how many Sentences it holds, and what's rolled on it, as a person asked. What it deals is on the card of the book it goes in.

| | Prefixes | Suffixes |
|---|---|---|
| Book | **More Pages** (`book-pages`), reduced Cost, increased Use Speed, Cooldown Recovery | Potency, Damage, Critical Chance, Area, Projectile Speed, Page Duration |
| Page | **More Sentences** (`page-sentences`), reduced Cost, increased Use Speed | Potency, Damage, Critical Chance, and Area on an Area Page or Projectile Speed on a Projectile Page alone; on a Summon Page only more Minions and Minion Duration |

**A sentence rolls lines too**, by the same rarities: on top of its one good thing and one bad thing, a Magic one has a prefix and a suffix and a Rare one two of each, and a Unique one none, as it's what it says. Its prefixes are what using its page asks (reduced Cost, Use Speed, Cooldown Recovery) and its suffixes what the page does (Potency, Damage, Critical Chance, and for every ailment its Chance, its Effect and its Duration wherever each means anything: no chance to Chill, as every cold hit chills, and no Effect for a Freeze or a Stun, which are only as long as they last, 21 lines in all), and they count on the page it's written on (`Scribe.Bind` adds them to `Page.Mods`). Nothing only one kind of page could use, as a sentence can go on either. Its name is what it says with its first prefix before it ("Terse Sentence of Might"). A sentence from before this rolls its lines from its own seed, the first time it's loaded.

Room is rolled `RoomWeight` times as often as anything else on its side, as it's what a book is worth. It's whole numbers, one more at the start and one more again at deeper levels, so all the room there can be (`MostPages`, `MostSentences`) is only on the deepest Rares. Every roll is whole, as a card reads better for it. A new line is a `FolioAffix` with a new id; ids are saved, and names and numbers can change freely. `FolioAffixes.Fix` brings anything from a save up to the rules, and `Copied` books are brought up when they're made, so a save gives back what was copied. Nobody has played any of this: the numbers are where to start.

## Three kinds of book

A book is found as one of three, and stays that way (`Item.Mode`). All of them go on the bar, which is where a book has to be to do anything.

| Kind | How it goes off | What it costs |
|---|---|---|
| Active | Pressed | Its Cost, each use |
| Trigger | By itself, when what it's waiting for has happened enough. A press does nothing | Its Cost, each time. With nothing to pay with it waits, full, and goes the moment there is |
| Toggle | A press turns it on and another off. While on it pulses every page it has | Its Cost, off Mana Regeneration, for as long as it's on |

**A Trigger Book** rolls what it waits for and how much of it (`BookTriggers`), the rarer the less. `Combat.Watch` only counts, and that's all that happens in a blow: what's ready goes off in `GameServer.GoOff`, between blows, so a blow can't set off a book that sets off a blow. What goes off by itself doesn't count as used by hand, or one waiting for that would never stop. How far each has got is sent as `HeroSimState.Charged` and the bar fills with it. It aims where the hero's aiming. Something new to wait for is a line in `BookTriggers.Lines`, a value on the end of `BookWhen` and its words in `BookText.Waits`, and a call to `Watch`. Damage is counted from Hits, not from ailments ticking.

**A Toggle Book** is turned on in `HeroRules.Step`, so a client knows at once, and what it gives up comes off in the same step, so Mana is predicted like anything else. It can only go on with more Mana Regeneration left than it Costs, which is your whole regeneration less whatever else is on. Taking off what paid for one turns off the last until what's left can be kept up (`Settle`). Only the press counts, or a mouse button held down would flip it every tick. It's a press that's on the wire and not a state, so it can't be lost.

It holds only Area Pages, as nothing else goes by pulses. Every page goes at once in each pulse, each its own last, and Potency still carries from one to the next. Nothing on it lingers, as pulsing is what it does. `AuraPulse` is at bare hands' pace, so what quickens a use quickens it and what makes a book rest holds it back, and each pulse does `AuraStrength` of what the page would by hand. A pulse shows a ring and embers but none of the noise a spell makes, as it's going to go on making it. Nothing costs less than `LeastToggleCost`, or a free cone could be left on for nothing. What's on stops with the world: arriving in a new one, everything's off.

Everyone regenerates `ManaRefill` of their Mana a second, which is 2.2 at the start, so a toggle is something a build has to make room for. If that's too hard it's that number or the phrases to change, and not this rule.

A Trigger or a Toggle Book's pages are Attacks or Spells like any other, and take the same Damage.

## What's found

`Loot.Fresh` makes `FolioShare` of everything that drops something for a book, by `ItemGenerator.Folio`, sentences most and books least, and one in `GameServer`'s `FolioEvery` on a table. A book or a page rolls its lines by its rarity and level, and holds as many places as they say (`FolioAffixes.PlacesOf`). A page or a book goes no further than rare. A find that comes up Unique is a Unique sentence `UniqueSentenceShare` of the time, and otherwise something worn. What's in something is sold with it.

A found page that deals is half projectiles and half areas, and half Attacks and half Spells, with what's rarer than dealing as `Phrases` weighs it, and one that raises is a Summon Page. Its name says what it is, with the first line of each side round it as gear's are: a Plain Raising Page, a Wide Spell Area Page of Ink, a Thick Trigger Book of Harm.

Of books, `TriggerShare` are Trigger Books and `ToggleShare` Toggle Books.

Everyone starts with a Worn Book, Normal, holding an Attack Area Page in a Cone with one place for a sentence. A second Page needs a book with room for it. Nothing makes a book or a page but finding one.

## Limits

These are hard limits, as balance numbers can't stop a book branching out of control: Noita and Mages of Mystralia both learned that.

- `MostPages` pages, `MostSentences` sentences and one Outcome on a page, and `MostProjectiles` from one.
- `MostMade` projectiles and areas from one cast. Past that a page simply doesn't turn.
- `MostZones` lingering areas from one caster, and `MostMinions` minions, the oldest going first.

## Who it happens to

| Outcome | Falls on |
|---|---|
| Deal, ailments, push, pull | Enemies it touches |
| Heal, give a buff | You, or friends in the shape, as the phrase says. A projectile that only helps flies through Enemies and stops at a friend |
| Raise a minion, make water | The place |
| Move you there | You |
| Nothing | Nobody. The page is only on its way to the next (`Deed.Pass`), which is how a meteor falls and only its blast does anything |

## Enemies

**Enemies have books too**, already written, in `Shelf`, made of the same pages and sentences, as anything else would make a hero and an enemy different kinds of thing. A page has no element, so what's written to deal fire is a conversion of all its user's physical damage, on each page (`Shelf.Write`'s `type`). An enemy has as much Spell Damage as Attack Damage (`Bestiary.SpellFor`), and whatever it's made of turns that into its own kind, as it does a blow. What other things set off is an Attack, as everyone has some Attack Damage and not everyone has Spell Damage. Their first page has to start from them or from where they're aiming, as a mark on the ground is a promise and can't be made about someone who hasn't been chosen yet. `check` holds that. What a keystone or an item sets off is on `Shelf` as well.

## The bar

Six slots: both mouse buttons, Q, 1, 2 and 3. Another is `HeroState.BarSlots`, a value in `HeroButtons`, and its key in `SlotKeys` and `ActionBar.Keys`. A book moves onto the bar and out of the pack, as a ring goes on a finger, so it can't be sold by mistake. `HeroState.Bar` holds the items, and `ItemPlace.OnBar` is where they are. Everything that moves one goes through `HeroState.Move`, like anything else that's carried: read the part about that in `NETWORKING.md`.

## What the tree and items do with it

A stat that names a tag reads it off a page: `Opening` is a book's first page, `Turned` a page that follows another, `Closing` its last, `Wild` a page that leaves something to chance, and `Falling` one that comes out of the sky. So a build can be about the shape of a book, not only what's in it. The keystones Palimpsest (`Rule.OneCost`: only the first page costs anything), The Last Chapter and Wild Margins are that, and took the place of the three that only meant anything with skills: Unbound Hour, Homing Flight and Borrowed Tongue. What taught a skill on the tree is now a notable in the same place, so the wheel's shape didn't move. The Eye of the Mockingbird no longer mimics anything, as there's nothing to mimic, and holds a hand steady instead (`Rule.Steady`: nothing is left to chance).

## Old saves

`SaveGame.BooksVersion` is where this came in. An older save keeps a copy of itself beside it (`.before-books`), loses its talents and what was on its bar, and comes out with its items, its gold, its points to spend again, and the starter on the left mouse button.

`SentencesVersion` is where pages and sentences became items. What's written stays written: an older save keeps a copy (`.before-sentences`), and each book comes out as a book with exactly as many places as it had pages, each page with as many as it had clauses, each clause a sentence by its name, and at least one place for a sentence on every page. Nothing but `Item.Book` and `Page.Clauses` knows the old shape, and only `SaveGame.Fix` reads them.

When books and pages began to roll lines, nothing needed a save version: a book or a page with no lines that's Magic or Rare, or holds more than one, is from before, and `FolioAffixes.Fix` gives it a line with the room it had, makes it Magic if it was Normal and held more, and draws the rest from its own seed. Nobody loses a place. A name it was found with says what it now is, and one someone gave it stays.

`FoundPagesVersion` is where pages became what they're found as. An older save keeps a copy (`.before-found-pages`), and `SaveGame.FoundPages` makes every page what a found page could have been, before anything is read: its first Outcome if a found page can do that, and dealing if not, a Spell if it dealt anything but physical damage or does anything but deal, and an Attack otherwise. Where it started and went are gone. Only an old save has `Outcome.Element`, and only for that.

## Adding to it

- **A phrase**: a line in `Phrases`. Ids are saved, so they stay, and they go on the end, as the order is what's sent. A `Found` share is what lets a found page have it.
- **A new kind of Outcome**: a value in `Deed`, a case in `Combat.Do`, and its words.
- **A written book**: a line in `Shelf`.
- **A line a book or a page can roll**: a `FolioAffix` in `FolioAffixes`, with a new id, its side, its slots, and a `Kind` if it only means anything on one kind of page. What it builds is modifiers like any other, so its words come from `ModText`.
- **Something a sentence can do**: a line in `Sentences.Lines`, a value on the end of `PageEffect`, its words in `BookText.Good` or `Bad`, and what it does in `Sentences.WeightsOf` or `Scribe.Apply`. Something only a Unique one can do is a line marked `Unique`, a name in `ItemGenerator.SentenceName`, and a name in `Sentences.Written` for what's already written to use.

## Testing

```
dotnet "Dino BuildLab/bin/Release/net8.0/Dino BuildLab.dll" check    # Every page reads, what's found is within the rules, and nonsense is put right
dotnet "Dino BuildLab/bin/Release/net8.0/Dino BuildLab.dll" books    # What each shape and sentence is worth, on an Attack and on a Spell
dotnet "Dino NetLab/bin/Release/net8.0/Dino NetLab.dll" books        # Every page and sentence, done for real on a running server
dotnet "Dino NetLab/bin/Release/net8.0/Dino NetLab.dll" build        # Everything the tree and items do to one
```

In the real game `--books a,b` puts books from `Shelf` on the bar, by id, `--panel book` opens the first one, and K does the same for a person. A right click on a book in the pack or on the bar opens it, and another on the same one leaves it open, as a person found losing the window to it a nuisance: going back, or the key for Books (`GameAction.Books`), shuts it. Dropping a Sentence on a Page, or a Page on a Book, wherever either is, puts it in the first place there that takes it (`HeroPanel.FirstPlaceIn`), and so does a Weapon or a Book dropped on a Summon Page; with none, the cursor says why. The bar takes that right click with nothing else open, but only a press that starts on a book (`BarCell`), as otherwise the mouse is what's aimed with, so a held attack dragged across the bar has to stay an attack and a left click on it has to attack. Ctrl and a click puts a book on the bar.

An open book is one book and nothing else: to open another, right click it, and a loose page opens the same way. The pack is beside it, as everything that goes in a book comes from there, and carrying is `HeroPanel`'s whether that's showing or not (`Active`, `Tend`, `DrawOver`). Everything inside something is an `ItemPlace` with `In`, so what's in a book moves by `HeroState.Move` like anything else, and `ItemCell` draws it. Its two pages have a rule to them: the left is the page turned to and everything about it, and the right is the whole book, which always says what the book comes to: that's the book's own card (`CardView`), less what's already at the top of the page. A page is only read, as it's what it was found as: `PageView` reads what it comes to (`BookText.Lines`), sentences and all, so a Unique sentence that moves where it starts shows there. Everything written in it is marked and can be looked up with Ctrl, as on any card, so a new line on a page goes through `Vellum.Mark` or `PageLine`: read the part of TOOLTIPS.md on words in a panel. Nothing about moving pages and sentences can be proved by a bot: post real mouse messages. `--folios N` carries that many things for a book, of every rarity in turn.
