# Books

There are no skills. Everything anyone does in a fight, from a sword swing to a meteor, is a book of pages, and a hero and an enemy use them the same way. Read this before changing anything about what a hero or an enemy can do.

## The one idea

**A book is a program, and a page is a line of it.** Nothing about what a book does is hand-made: `Scribe` reads the pages and works out numbers from them, so any attack or spell from any ARPG is a few pages and a few stickers, and a new sentence is a line in a table.

**Books, pages and stickers are all items, found in the world, and each holds the next.** A book has places for pages and a page has places for stickers (`Item.Held`), and how many places it has is what makes one better than another. Nobody writes anything: a page is found saying what it says (`Scribe.Roll`) and says it for good, so a build is what's been found, in what order, with what stuck on it.

```
Items (saved, sent)  --Scribe.Bind-->  Book  --Scribe.Read-->  BookPlan  --ActorSheet.Profile-->  BookProfile
book > page > sticker                  pages in order          what it says                       one actor's finished numbers
```

`BookPlan` is what a book says, whoever holds it. `BookProfile` is what it comes to in one actor's hands, with a `PageProfile` for each page, and that's what a blow reads. So everything `BUILDS.md` says about why blows are fast is still true: a hundred archers are one set of numbers.

## A page

A page is found as one of two kinds, and as an Attack or a Spell (`Page.Hand`). What else it says depends on its kind.

| Part | Projectile page | Area page |
|---|---|---|
| Start | From you, if it's the first page, and from where the last struck if it isn't | The same |
| Then | Towards the cursor, if it's the first, and on the way the last was going if it isn't | A shape, a cone, a circle, a line or a rectangle, and whether it happens at once or lingers, found with it |
| Stickers | Whatever's been stuck on it, in as many places as it has | The same |
| Outcome | What happens on impact, which is one thing | The same, to everyone in the shape |
| Its own numbers | Cost, Projectile Speed, Potency | Cost, Area, Potency |

**Nearly every page deals damage.** An Attack Page deals 100% of your Attack Damage and a Spell Page 100% of your Spell Damage, before Potency, of every type that Damage has. Raising Minions and moving you there are rarer finds, and always Spells: what a found page can do is the sentences with a `Found` share in `Sentences`, and how often is that share. Moving only comes on a projectile, as an area that starts from you would leave you where you are. Everything else a sentence can say, healing, buffs, ailments, pushing, water and doing nothing but turning the page, is only on what's already written (`Shelf`), until something brings it to a player.

**A follow-up page starts where the last one struck, and an impact is reaching someone.** A book's pages go in order: when a projectile reaches an Enemy, or a friend for one that only helps, the book turns to the next page, from there, the same way. That's all a chain is: two projectile pages one after the other are a projectile that pierces one. A projectile that misses, runs out or hits a wall has no impact and the book stops there, and so does one that comes down on nobody. A sticker can say that the ground or anything solid counts. An area page has no impact to turn from, so it can only be the last: `HeroState.Move` won't put anything after one, and a gap in a book is only a gap.

**Potency goes on from page to page.** A page starts from where the one before it left off, so a first page at 90% and a second that doubles it comes to 180% (`PagePlan.Carried`). It's what's on the page that goes on: anything a build adds is applied to each page where it stands and doesn't compound, or a little Potency on the tree would be worth more the longer the book.

**The book's Cost is every page's Cost added up.** It's paid when the book is used, and only then.

**Attack or Spell is the page's, whatever's in your hands.** Damage is in two kinds. Attack Damage is your weapon's, of whatever type its base deals, and whatever's added to Attacks: Attack Speed, Accuracy, and it can be Evaded. Spell Damage is only what's added to Spells, which wands and staves roll as suffixes: Cast Speed, and it can't be Evaded. Nothing gives Spell Damage for nothing, so a Spell Page in the hands of someone with none does nothing at all. The tree's Attack and Spell nodes mean what they say, and a book can be both.

**How long a book takes is all its pages together.** If its first page is an Attack, the weapon's own pace is the base of it (`PageUseTime` at bare hands, by `AttackTime`, less by Attack Speed), and Attack Pages after it add nothing. Every Spell Page adds `CastTime`, less by Cast Speed. Then whatever any page says of Use Time, on a sticker, goes for the whole book (`PagePlan.UseScale`, `BookProfile.UseTime`).

**What's in the hand says what a page looks like.** A Spell is a ball of what it deals, and so is anything done with a wand or a staff. An Attack with a bow is an arrow, and with anything else it's what's in the hand, thrown.

## What's decided when

- **Nearest and random** look at the moment the page opens, and skip anyone this cast has struck, which is what makes a chain go on to someone new. With nobody there, it uses the cursor.
- **Direction is fixed at launch.** There's no homing. `BallisticComponent` is sent once and flies straight, and that's what makes hundreds of projectiles cost almost nothing.
- **A projectile flies past what it's aimed at**, to the end of its range. Only reaching someone is an impact: the end of its range or a wall is nothing, unless the page is `Grounded`.
- **What falls** comes out of the sky onto where the page is going, and lands exactly when it says it will. It's an impact if someone's under it, or if the ground counts.
- **Lingering** spreads the Outcome over the Duration, in pulses, and pays a premium for being something Enemies can walk out of.
- **What starts from an impact starts touching whoever took it**, so it goes straight through them. Without that (`off`, through `Combat.Turn`) a page that turned on someone would be stopped by them at once.
- **A leap keeps count.** A follow-up carries what's left of `Chains`, as one that started over would never end.

## Sentences pay for themselves

Every sentence has `Weights`: flat Cost, and `more` or `less` of Cost, Potency, Speed, Area, Range, Duration and Use Time. Each is a multiplier of its own, which is what those words already mean everywhere else, and can't reach zero the way `reduced` can. A Circle costs more than a Cone, as it reaches everything around you, and lingering costs more still.

**Potency** is the one number every Outcome goes by. A page's Potency scales its Damage, healing and ailments alike.

`MostPotency` is the ceiling on what stacked drawbacks can come to. `BuildLab books` tables what every shape and sticker is worth, as nobody has played any of this, and a person's opinion outranks that table.

## Stickers

A sticker is an item with one good thing and one bad thing on it, and that's all it is. Nothing about one is decided by hand: `Stickers.Roll` picks anything from `Stickers.Lines` that can be good, anything that can be bad, and how far each goes, so any combination can turn up. A rarer one goes further on its good side and less far on its bad. A line is a new thing a sticker can do, and `PageEffect` values go on the end, as they're saved by name and sent by number.

Each side has to mean something on the same kind of page, so a sticker is for a projectile page, an area page or either (`Stickers.Fits`), and it won't go on the other. What adds up does (Projectiles, Pierce, Chains, a Cooldown), and the rest are multipliers of their own as sentences are, so anything can be stuck beside anything. Cost as a drawback is flat, as a share of nothing is nothing and most of what starts out is free.

**A Unique sticker** has on its good side what nothing else can, and its bad side is rolled as a rare's would be. It's what changes where a page starts, where it goes, or what counts as an impact, as no page says anything of those:

| Unique | What it does |
|---|---|
| Grounded | Hitting the ground or anything solid is an impact, so what misses, or falls on nobody, still turns the page |
| From the cursor, the nearest Enemy, or a random Enemy nearby | Where the first page starts. On any other page it means nothing, as those start where the last struck |
| Towards the nearest Enemy, towards you, or in a random direction | Where a projectile goes, first page or not |
| Falls | Comes down out of the sky onto where it's going, rather than flying there |

What's left to chance pays for itself by itself (`Stickers.Weighting`), and counts as Wild. Only a sticker can be Unique for anything in a book, and only for one of these (`Stickers.IsUnique`, which `ItemGenerator.Fix` and `Stickers.Fix` hold): `Loot` makes `UniqueStickerShare` of what's found Unique one of these instead of something worn.

`Stickers.Written` is for what's already written and has to come out the same: creatures fight with those, and the Unique ones by the names above. They go by the names clauses had, as a save from before stickers does too.

## Three kinds of book

A book is found as one of three, and stays that way (`Item.Mode`). All of them go on the bar, which is where a book has to be to do anything.

| Kind | How it goes off | What it costs |
|---|---|---|
| Active | Pressed | Its Cost, each use |
| Trigger | By itself, when what it's waiting for has happened enough. A press does nothing | Its Cost, each time. With nothing to pay with it waits, full, and goes the moment there is |
| Toggle | A press turns it on and another off. While on it pulses every page it has | Its Cost, off Mana Regeneration, for as long as it's on |

**A Trigger Book** rolls what it waits for and how much of it (`BookTriggers`), the rarer the less. `Combat.Watch` only counts, and that's all that happens in a blow: what's ready goes off in `GameServer.GoOff`, between blows, so a blow can't set off a book that sets off a blow. What goes off by itself doesn't count as used by hand, or one waiting for that would never stop. How far each has got is sent as `HeroSimState.Charged` and the bar fills with it. It aims where the hero's aiming. Something new to wait for is a line in `BookTriggers.Lines`, a value on the end of `BookWhen` and its words in `BookText.Waits`, and a call to `Watch`. Damage is counted from Hits, not from ailments ticking.

**A Toggle Book** is turned on in `HeroRules.Step`, so a client knows at once, and what it gives up comes off in the same step, so Mana is predicted like anything else. It can only go on with more Mana Regeneration left than it Costs, which is your whole regeneration less whatever else is on. Taking off what paid for one turns off the last until what's left can be kept up (`Settle`). Only the press counts, or a mouse button held down would flip it every tick. It's a press that's on the wire and not a state, so it can't be lost.

It holds only Area Pages, as an Area ends a book and a book that could only ever have one page would have no use for more places. Every page goes at once in each pulse, each its own last, and Potency still carries from one to the next. Nothing on it lingers, as pulsing is what it does. `AuraPulse` is at bare hands' pace, so what quickens a use quickens it and what makes a book rest holds it back, and each pulse does `AuraStrength` of what the page would by hand. A pulse shows a ring and embers but none of the noise a spell makes, as it's going to go on making it. Nothing costs less than `LeastToggleCost`, or a free cone could be left on for nothing. What's on stops with the world: arriving in a new one, everything's off.

Everyone regenerates `ManaRefill` of their Mana a second, which is 2.2 at the start, so a toggle is something a build has to make room for. If that's too hard it's that number or the sentences to change, and not this rule.

A Trigger or a Toggle Book's pages are Attacks or Spells like any other, and take the same Damage.

## What's found

`Loot.Fresh` makes `FolioShare` of everything that drops something for a book, by `ItemGenerator.Folio`, stickers most and books least, and one in `GameServer`'s `FolioEvery` on a table. Rarity is places: `LeastPages`, `MostPages`, `LeastStickers` and `MostStickers` say how many by rarity, and only a rare can have all there can be, which comes up less often than fewer (`PlacesOf`). A page or a book goes no further than rare. A find that comes up Unique is a Unique sticker `UniqueStickerShare` of the time, and otherwise something worn. What's in something is sold with it.

A found page is half projectiles and half areas, and half Attacks and half Spells, with what's rarer than dealing as `Sentences` weighs it. Its name says what it is: a Fine Spell Area Page, a Plain Raising Projectile Page.

Of books, `TriggerShare` are Trigger Books and `ToggleShare` Toggle Books.

Everyone starts with a Worn Book of two places, an Attack Area Page in a Cone in the first of them with one place for a sticker. Nothing makes a book or a page but finding one.

## Limits

These are hard limits, as balance numbers can't stop a book branching out of control: Noita and Mages of Mystralia both learned that.

- `MostPages` pages, `MostStickers` stickers and one Outcome on a page, and `MostProjectiles` from one.
- `MostMade` projectiles and areas from one cast. Past that a page simply doesn't turn.
- `MostZones` lingering areas from one caster, and `MostMinions` minions, the oldest going first.

## Who it happens to

| Outcome | Falls on |
|---|---|
| Deal, ailments, push, pull | Enemies it touches |
| Heal, give a buff | You, or friends in the shape, as the sentence says. A projectile that only helps flies through Enemies and stops at a friend |
| Raise a minion, make water | The place |
| Move you there | You |
| Nothing | Nobody. The page is only on its way to the next (`Deed.Pass`), which is how a meteor falls and only its blast does anything |

## Enemies

**Enemies have books too**, already written, in `Shelf`, made of the same pages and stickers, as anything else would make a hero and an enemy different kinds of thing. A page has no element, so what's written to deal fire is a conversion of all its user's physical damage, on each page (`Shelf.Write`'s `type`). An enemy has as much Spell Damage as Attack Damage (`Bestiary.SpellFor`), and whatever it's made of turns that into its own kind, as it does a blow. What other things set off is an Attack, as everyone has some Attack Damage and not everyone has Spell Damage. Their first page has to start from them or from where they're aiming, as a mark on the ground is a promise and can't be made about someone who hasn't been chosen yet. `check` holds that. What a keystone or an item sets off is on `Shelf` as well.

## The bar

Six slots: both mouse buttons, Q, 1, 2 and 3. Another is `HeroState.BarSlots`, a value in `HeroButtons`, and its key in `SlotKeys` and `ActionBar.Keys`. A book moves onto the bar and out of the pack, as a ring goes on a finger, so it can't be sold by mistake. `HeroState.Bar` holds the items, and `ItemPlace.OnBar` is where they are. Everything that moves one goes through `HeroState.Move`, like anything else that's carried: read the part about that in `NETWORKING.md`.

## What the tree and items do with it

A stat that names a tag reads it off a page: `Opening` is a book's first page, `Turned` a page that follows another, `Closing` its last, `Wild` a page that leaves something to chance, and `Falling` one that comes out of the sky. So a build can be about the shape of a book, not only what's in it. The keystones Palimpsest (`Rule.OneCost`: only the first page costs anything), The Last Chapter and Wild Margins are that, and took the place of the three that only meant anything with skills: Unbound Hour, Homing Flight and Borrowed Tongue. What taught a skill on the tree is now a notable in the same place, so the wheel's shape didn't move. The Eye of the Mockingbird no longer mimics anything, as there's nothing to mimic, and holds a hand steady instead (`Rule.Steady`: nothing is left to chance).

## Old saves

`SaveGame.BooksVersion` is where this came in. An older save keeps a copy of itself beside it (`.before-books`), loses its talents and what was on its bar, and comes out with its items, its gold, its points to spend again, and the starter on the left mouse button.

`StickersVersion` is where pages and stickers became items. What's written stays written: an older save keeps a copy (`.before-stickers`), and each book comes out as a book with exactly as many places as it had pages, each page with as many as it had clauses, each clause a sticker by its name, and at least one place for a sticker on every page. Nothing but `Item.Book` and `Page.Clauses` knows the old shape, and only `SaveGame.Fix` reads them.

`FoundPagesVersion` is where pages became what they're found as. An older save keeps a copy (`.before-found-pages`), and `SaveGame.FoundPages` makes every page what a found page could have been, before anything is read: its first Outcome if a found page can do that, and dealing if not, a Spell if it dealt anything but physical damage or does anything but deal, and an Attack otherwise. Where it started and went are gone. Only an old save has `Outcome.Element`, and only for that.

## Adding to it

- **A sentence**: a line in `Sentences`. Ids are saved, so they stay, and they go on the end, as the order is what's sent. A `Found` share is what lets a found page have it.
- **A new kind of Outcome**: a value in `Deed`, a case in `Combat.Do`, and its words.
- **A written book**: a line in `Shelf`.
- **Something a sticker can do**: a line in `Stickers.Lines`, a value on the end of `PageEffect`, its words in `BookText.Good` or `Bad`, and what it does in `Stickers.WeightsOf` or `Scribe.Stick`. Something only a Unique one can do is a line marked `Unique`, a name in `ItemGenerator.StickerName`, and a name in `Stickers.Written` for what's already written to use.

## Testing

```
dotnet "Dino BuildLab/bin/Release/net8.0/Dino BuildLab.dll" check    # Every page reads, what's found is within the rules, and nonsense is put right
dotnet "Dino BuildLab/bin/Release/net8.0/Dino BuildLab.dll" books    # What each shape and sticker is worth, on an Attack and on a Spell
dotnet "Dino NetLab/bin/Release/net8.0/Dino NetLab.dll" books        # Every page and sticker, done for real on a running server
dotnet "Dino NetLab/bin/Release/net8.0/Dino NetLab.dll" build        # Everything the tree and items do to one
```

In the real game `--books a,b` puts books from `Shelf` on the bar, by id, `--panel book` opens the first one, and K does the same for a person. A right click on a book in the pack or on the bar opens it, and another on the same one shuts it. The bar takes that right click with nothing else open, but only a press that starts on a book (`BarCell`), as otherwise the mouse is what's aimed with, so a held attack dragged across the bar has to stay an attack and a left click on it has to attack. Ctrl and a click puts a book on the bar.

An open book is one book and nothing else: to open another, right click it, and a loose page opens the same way. The pack is beside it, as everything that goes in a book comes from there, and carrying is `HeroPanel`'s whether that's showing or not (`Active`, `Tend`, `DrawOver`). Everything inside something is an `ItemPlace` with `In`, so what's in a book moves by `HeroState.Move` like anything else, and `ItemCell` draws it. Its two pages have a rule to them: the left is the page turned to and everything about it, and the right is the whole book, which always says what the book comes to: that's the book's own card (`CardView`), less what's already at the top of the page. A page is only read, as it's what it was found as: `PageView` reads what it comes to (`BookText.Lines`), stickers and all, so a Unique sticker that moves where it starts shows there. Everything written in it is marked and can be looked up with Ctrl, as on any card, so a new line on a page goes through `Vellum.Mark` or `PageLine`: read the part of TOOLTIPS.md on words in a panel. Nothing about moving pages and stickers can be proved by a bot: post real mouse messages. `--folios N` carries that many things for a book, of every rarity in turn.
