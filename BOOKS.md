# Books

There are no skills. Everything anyone does in a fight, from a sword swing to a meteor, is a book they have written, and a hero and an enemy write them the same way. Read this before changing anything about what a hero or an enemy can do.

## The one idea

**A book is a program, and a sentence is a line of it.** Nothing about what a book does is hand-made: `Scribe` reads the sentences and works out numbers from them, so any attack or spell from any ARPG is a few pages, and a new sentence is a line in a table.

**Books, pages and stickers are all items, found in the world, and each holds the next.** A book has places for pages and a page has places for stickers (`Item.Held`), and how many places it has is what makes one better than another. Anyone can write whatever they like on a page they have, but what they have to write on, how much of it, and what's stuck on it is what they've found.

```
Items (saved, sent)  --Scribe.Bind-->  Book  --Scribe.Read-->  BookPlan  --ActorSheet.Profile-->  BookProfile
book > page > sticker                  pages in order          what it says                       one actor's finished numbers
```

`BookPlan` is what a book says, whoever holds it. `BookProfile` is what it comes to in one actor's hands, with a `PageProfile` for each page, and that's what a blow reads. So everything `BUILDS.md` says about why blows are fast is still true: a hundred archers are one set of numbers.

## A page

A page is one of two kinds. Every page has an Outcome, and what comes before that says how it gets there.

| Part | Projectile page | Area page |
|---|---|---|
| Start | Where it begins: you, the cursor, the nearest Enemy, a random Enemy nearby | The same |
| Then | Towards: the cursor, you, the nearest Enemy, a random Enemy, in a random direction, the way it was going, or down from the sky | Shape: a cone, a circle, a line or a rectangle, and whether it happens at once or lingers |
| Within | That it happens somewhere within a radius of either | The same |
| Stickers | Whatever's been stuck on it, in as many places as it has | The same |
| Outcomes | What happens on impact, up to `MostOutcomes` | The same, to everyone in the shape |
| Its own numbers | Cost, Projectile Speed, Potency | Cost, Area, Potency |

**A follow-up page starts where the last one struck.** A book's pages go in order: when a projectile page has an impact, the book turns to the next page, from there. That's all a chain is. Going to the nearest Enemy and then back to you is a returning projectile, and nobody had to write one. An area page has no impact to turn from, so it can only be the last: `HeroState.Move` won't put anything after one, and a gap in a book is only a gap.

**Potency goes on from page to page.** A page starts from where the one before it left off, so a first page at 90% and a second that doubles it comes to 180% (`PagePlan.Carried`). It's what's on the page that goes on: anything a build adds is applied to each page where it stands and doesn't compound, or a little Potency on the tree would be worth more the longer the book.

**The book's Cost is every page's Cost added up.** It's paid when the book is used, and only then.

**Attack or Spell is what's in your hands.** Your Damage is your weapon's. With a sword, axe, mace, dagger, spear, bow or nothing at all, a book is an Attack: Attack Speed and Accuracy, and it can be Evaded. With a wand or staff it's a Spell: Cast Speed, and it can't. So the tree's Attack and Spell nodes mean what they always did, and swapping a weapon changes what a book is.

**A weapon also says what a page looks like.** A bow makes a projectile an arrow, a wand or a staff a ball of magic, and anything else is thrown.

## What's decided when

- **Nearest and random** look at the moment the page opens, and skip anyone this cast has struck, which is what makes a chain go on to someone new. With nobody there, it uses the cursor.
- **Direction is fixed at launch.** There's no homing. `BallisticComponent` is sent once and flies straight, and that's what makes hundreds of projectiles cost almost nothing.
- **A projectile flies past what it's aimed at**, to the end of its range. Reaching the end, or anything solid, counts as an impact, so the next page turns anyway. A sticker can make it stop at the target instead.
- **Down** comes out of the sky over wherever the Start says, and lands exactly when it says it will.
- **Lingering** spreads the Outcomes over the Duration, in pulses, and pays a premium for being something Enemies can walk out of.
- **What starts from an impact starts touching whoever took it**, so it goes straight through them. Without that (`off`, through `Combat.Turn`) a page that turned on someone would be stopped by them at once.
- **A leap keeps count.** A follow-up carries what's left of `Chains`, as one that started over would never end.

## Sentences pay for themselves

Every sentence has `Weights`: flat Cost, and `more` or `less` of Cost, Potency, Speed, Area, Range, Duration and Use Time. Each is a multiplier of its own, which is what those words already mean everywhere else, and can't reach zero the way `reduced` can. A sentence that's bad for you pays for it: a random direction Costs half as much. One that's good for you costs something: finding the nearest Enemy by itself takes some of the Potency.

**Potency** is the one number all Outcomes share. A page's Potency scales its Damage, healing and ailments alike.

`MostPotency` is the ceiling on what stacked drawbacks can come to. `BuildLab books` tables what every sentence is worth, as nobody has played any of this, and a person's opinion outranks that table.

## Stickers

A sticker is an item with one good thing and one bad thing on it, and that's all it is. Nothing about one is decided by hand: `Stickers.Roll` picks anything from `Stickers.Lines` that can be good, anything that can be bad, and how far each goes, so any combination can turn up. A rarer one goes further on its good side and less far on its bad. A line is a new thing a sticker can do, and `PageEffect` values go on the end, as they're saved by name and sent by number.

Each side has to mean something on the same kind of page, so a sticker is for a projectile page, an area page or either (`Stickers.Fits`), and it won't go on the other. What adds up does (Projectiles, Pierce, Chains, a Cooldown), and the rest are multipliers of their own as sentences are, so anything can be stuck beside anything. Cost as a drawback is flat, as a share of nothing is nothing and most of what starts out is free.

`Stickers.Written` is for what's already written and has to come out the same: creatures fight with those. They go by the names clauses had, as a save from before stickers does too.

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

## What's found

`Loot.Fresh` makes `FolioShare` of everything that drops something for a book, by `ItemGenerator.Folio`, stickers most and books least, and one in `GameServer`'s `FolioEvery` on a table. Grade is places: `LeastPages`, `MostPages`, `LeastStickers` and `MostStickers` say how many by grade, and only a legendary has all there can be. `GradeOf` reads it back from the item, as it does for anything else, so a masterwork is a rare with `MasterworkPages` or `MasterworkStickers` places, and a sticker can't be one. What's in something is sold with it.

Of books, `TriggerShare` are Trigger Books and `ToggleShare` Toggle Books.

Everyone starts with a Worn Book of two places, an Area Page in the first of them with one place for a sticker. Nothing makes a book or a page but finding one.

## Limits

These are hard limits, as balance numbers can't stop a book branching out of control: Noita and Mages of Mystralia both learned that.

- `MostPages` pages, `MostStickers` stickers and `MostOutcomes` Outcomes on a page, and `MostProjectiles` from one.
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

**Enemies write books too**, in `Shelf`, as anything else would make a hero and an enemy different kinds of thing. Their first page has to start from them or from where they're aiming, as a mark on the ground is a promise and can't be made about someone who hasn't been chosen yet. `check` holds that. What a keystone or an item sets off is on `Shelf` as well.

## The bar

Six slots: both mouse buttons, Q, 1, 2 and 3. Another is `HeroState.BarSlots`, a value in `HeroButtons`, and its key in `SlotKeys` and `ActionBar.Keys`. A book moves onto the bar and out of the pack, as a ring goes on a finger, so it can't be sold by mistake. `HeroState.Bar` holds the items, and `ItemPlace.OnBar` is where they are. Everything that moves one goes through `HeroState.Move`, like anything else that's carried: read the part about that in `NETWORKING.md`.

## What the tree and items do with it

A stat that names a tag reads it off a page: `Opening` is a book's first page, `Turned` a page that follows another, `Closing` its last, `Wild` a page that leaves something to chance, and `Falling` one that comes out of the sky. So a build can be about the shape of a book, not only what's in it. The keystones Palimpsest (`Rule.OneCost`: only the first page costs anything), The Last Chapter and Wild Margins are that, and took the place of the three that only meant anything with skills: Unbound Hour, Homing Flight and Borrowed Tongue. What taught a skill on the tree is now a notable in the same place, so the wheel's shape didn't move. The Eye of the Mockingbird no longer mimics anything, as there's nothing to mimic, and holds a hand steady instead (`Rule.Steady`: nothing is left to chance).

## Old saves

`SaveGame.BooksVersion` is where this came in. An older save keeps a copy of itself beside it (`.before-books`), loses its talents and what was on its bar, and comes out with its items, its gold, its points to spend again, and the starter on the left mouse button.

`StickersVersion` is where pages and stickers became items. What's written stays written: an older save keeps a copy (`.before-stickers`), and each book comes out as a book with exactly as many places as it had pages, each page with as many as it had clauses, each clause a sticker by its name, and at least one place for a sticker on every page. Nothing but `Item.Book` and `Page.Clauses` knows the old shape, and only `SaveGame.Fix` reads them.

## What's still free

Nobody has decided how sentences are earned, so everyone can write anything on a page they've found. When sentences become something to find, `Sentences` is the only list to narrow.

## Adding to it

- **A sentence**: a line in `Sentences`. Ids are saved, so they stay, and they go on the end, as the order is what's sent.
- **A new kind of Outcome**: a value in `Deed`, a case in `Combat.Do`, and its words.
- **A written book**: a line in `Shelf`.
- **Something a sticker can do**: a line in `Stickers.Lines`, a value on the end of `PageEffect`, its words in `BookText.Good` or `Bad`, and what it does in `Stickers.WeightsOf` or `Scribe.Stick`.

## Testing

```
dotnet "Dino BuildLab/bin/Release/net8.0/Dino BuildLab.dll" check    # Every sentence reads, pays for itself, and survives nonsense
dotnet "Dino BuildLab/bin/Release/net8.0/Dino BuildLab.dll" books    # What each sentence is worth
dotnet "Dino NetLab/bin/Release/net8.0/Dino NetLab.dll" books        # Every sentence, done for real on a running server
dotnet "Dino NetLab/bin/Release/net8.0/Dino NetLab.dll" build        # Everything the tree and items do to one
```

In the real game `--books a,b` puts books from `Shelf` on the bar, by id, `--panel book` opens the first one, and K does the same for a person. A right click on a book in the pack or on the bar opens it, and another on the same one shuts it. The bar takes that right click with nothing else open, but only a press that starts on a book (`BarCell`), as otherwise the mouse is what's aimed with, so a held attack dragged across the bar has to stay an attack and a left click on it has to attack. Ctrl and a click puts a book on the bar.

An open book is one book and nothing else: to open another, right click it, and a loose page opens the same way. The pack is beside it, as everything that goes in a book comes from there, and carrying is `HeroPanel`'s whether that's showing or not (`Active`, `Tend`, `DrawOver`). Everything inside something is an `ItemPlace` with `In`, so what's in a book moves by `HeroState.Move` like anything else, and `ItemCell` draws it. Its two pages have a rule to them: the left is the page turned to and everything about it, and the right is the whole book. Each part of a page that was chosen is one box however many lines it runs to (`PageView.Flow`), lit as a whole, so a part of it is never lit alone. What could go in a box comes on a slip over the left page, beside the box (`ChoiceSlip`), and not on the right, which always says what the book comes to: that's the book's own card (`CardView`), less what's already at the top of the page. Everything written in it is marked and can be looked up with Ctrl, as on any card, so a new line on a page goes through `Vellum.Mark` or `PageLine`: read the part of TOOLTIPS.md on words in a panel. Nothing about writing one can be proved by a bot: post real mouse messages. `--folios N` carries that many things for a book, of every grade in turn.
