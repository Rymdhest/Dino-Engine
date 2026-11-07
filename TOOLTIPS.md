# Tooltips

What a player reads about how the game works: books, items, talents, buffs, omens, every line of the hero's sheet. Read this before changing any of them, or before adding a stat, a rule, a sentence or anything else that a player will want to understand.

## The one idea

**A tooltip is the ground truth, or it's a bug.** Nothing a player reads is written by hand. Every number comes from where the rules read it, every sentence is built from what it describes, and every word that means something means exactly one thing. Change a number in the rules and every tooltip that mentions it changes. Add a stat, a rule or a tag and forget to say what it does, and `Dino BuildLab check` fails.

So when something reads wrong, the fix is almost never in the text. Either the number is in the wrong place, or the rules don't do what the sentence says, and that's what gets fixed.

## How it fits together

| Where | What it is |
|---|---|
| `Stats/Rulebook.cs` | Every number in the rules that isn't on a stat. `Combat` reads these, and so does the text |
| `Tooltips/Say.cs` | The only way a number becomes text: `8 m`, `0.6 s`, `25%` |
| `Tooltips/ModText.cs` | Turns a `Mod` into a sentence, and holds each `Rule`'s words |
| `Tooltips/Lexicon.cs` | Every word that means something, and what it means |
| `Tooltips/StatBook.cs` | Every line of the hero's sheet |
| `Tooltips/Cards.cs` | Builds a `Card` for anything that can be looked at, from finished numbers |
| `UI/Tips.cs` | Draws cards. `TipLayer` is the one place they go on screen |

`Tooltips/` is headless, so BuildLab reads what a player reads. Nothing in it decides how the game works.

A `Card` is built fresh every frame it's shown, from the live `ActorSheet` and `BookProfile`. That's the whole of why tooltips update live: there's nothing to go stale. A book's card reads the same `BookProfile` a blow reads, so what's on it is what lands. `ActorSheet.Preview` gives the same for a book that isn't on the bar, for what's being written or what an item would set off.

## The language

Each part of a modifier has one way of being said, and `check` fails if a sentence says otherwise, or drops a part.

| Written | Means |
|---|---|
| `+5 Health`, `-5 Health` | **Flat.** Goes onto the base, so everything else scales it |
| `20% increased`, `20% reduced` | All of these on a stat are summed, and the sum multiplies it once |
| `20% more`, `20% less` | Each is a multiplier of its own, one after another, on top of increased |
| `is set to` | Replaces the stat. If two disagree, the lowest stands |
| `on Hit`, `on Kill` | Comes at once, for each one |
| `Chance` | Rolled for each Hit on each target, and never otherwise |
| `Recently` | In the last `Rulebook.Recently` seconds |
| `per 50 Armour, up to 200%` | Counted in whole steps, stopping there. A card adds `(now 40%)` |
| `8 m`, `0.6 s`, `25%` | Metres, seconds, and a share. Never "nearby", "a moment" or "some" |

A sentence names everything its modifier is narrowed to: every tag in its scope, every condition on who has it and who it's against, and what it's counted per.

**Capitals are the rule.** A capitalised word is a defined term, always. Anything that shows text finds them with `Lexicon.Find`, so nobody marks a term up and a sentence can't forget to. `check` reads every sentence the game can show and fails on:

- a capitalised word that isn't a term (the start of a sentence and the names of books and buffs aside), which is what a magic word is
- a term written in lower case, which is a magic word in disguise: it means something and nothing says so
- a digit typed into a string in `Tooltips/` or `Offering.cs`
- a number in the code of a fight that isn't in the `Rulebook`
- a line that changes no number: every modifier a player can read is tried on a plain hero, and has to change something. That's how `increased` on a stat that starts from nothing was found, which reads well and does nothing

If `check` objects to an ordinary English word because it's also a term ("reach", "cost"), say it another way. That's the point: a player can't tell which you meant either.

## Keep it readable

A person read the first version of this and called it unreadable: every word was explained beside every card, and a skill's card ran to thirty lines. So there are two rules that matter as much as the truth does.

**Nothing opens by itself, and nothing sticks.** A card shows what it says, only while the mouse is on the thing itself, and it never stops the mouse. Holding left Ctrl holds it still: only then does a marked word or a tag open that one word's meaning when the mouse rests on it, the name of a book or buff open its card, and `Details` open the rest (`TipLayer.Held`). Anything calling `Show` has to pass the rect of the thing itself, never the mouse: a card that follows the mouse can't be reached.

Ctrl is also what a click in a pack is held down with, and a held card stops the mouse, so a hand going along a row with Ctrl down would find the last card sitting on the next thing. A card there is `loose`: it's only held by a Ctrl that went down while it was showing, and `TipLayer.LetGo` ends that the moment such a click has done something. So Ctrl pressed on a card still reads it, and Ctrl held down the whole way still clicks.

Holding Alt sets whatever an item would replace beside it, in full (`alongside`), to read the two against each other: both rings for a ring, and both hands for something that needs two. Those are only there to be read, and have nothing to rest on.

**Say it plainly.** A definition is a line or two, in the words any ARPG would use: "Reduces Physical Hits", not an explanation of how the sum goes. Short and ordinary beats complete. What's left out of a definition can be on a stat's card, in `Details`, or in the numbers themselves, and `check` fails past `LongestTerm` and `MostLinesOnACard`.

## Terms

A `Basic` term is plain enough to leave unmarked: Health, Cooldown, Radius. Everything else is marked, and opens on hover. Both are in the glossary (G, or `--panel glossary`), so no word is ever a dead end. A definition reads its numbers from where the rules do.

## What a card shows

A card is what's wanted at a glance, and `Details` is the rest.

- **A book's card** has what each page deals, how it's used, what it can leave behind and what it has of its own, and nothing it doesn't really do. A Hit with no fire in it can't Burn, so no Chance to Burn is shown, whatever the sheet says: that's `AilmentRules.Causes`. `PageCells` goes by kind of page, and says what `Combat` reads for that kind.
- **Its `Details`** have what the rest of the build gives it, the rules that touch it, the reactions it can set off, and `Breakdown`, which is everything between what it starts from and what it deals. A hero's Health on Kill is not a fact about Bone Spear, so it never goes on the card itself.
- **A stat** shows its total, what it does, what that means against a normal enemy, then the sum: all the flat added up, all the increased and reduced added up, each more and less on its own line, and the sum written out. What scales with something is under `Scaling`, and what only counts for some books or at some times is under `Situational`, as neither is part of the sum.
- **What's set off** goes in `Card.Attached` by `Attach`, from a trigger, `RuleInfo.Casts` or `RuleInfo.Gives`, and opens from its name in the sentence. The number a reader needs most goes in the sentence as well: Carrier says "within 8 m".
- **Flavour** (lore) says nothing about what something does. A claim belongs on the card, where a number can back it.

## Adding to it

- **A stat**: a value in `Stat`, a line in `StatNames`, a term in `Lexicon` by `On`, and a line in `StatBook`. `check` fails on any of them missing.
- **A rule**: a value in `Rule`, and a `RuleInfo` in `ModText`, whose numbers come from the `Rulebook`. `Touches` says which books it shows on, and `Casts` or `Gives` what goes beside it.
- **A condition, a tag, an ailment**: a term by `On`, and for a condition its lines in `ModText.Conditions`.
- **A sentence**: a line in `Sentences`. Its words are its text, and `BookText.Trade` says what it comes to from its `Weights`. Something a stat can't say, such as making water, is a `Deed`, which both `Combat` and `Cards` read.
- **A number in a rule**: a name in the `Rulebook`, never a literal in the code that uses it.
- **Something new to look at**: a method in `Cards`, then `GameUi.Current.Tips.Show(() => card, rect)` each frame it's looked at, with the rect of the thing itself. Never draw one by hand.

## Testing

```
dotnet "Dino BuildLab/bin/Release/net8.0/Dino BuildLab.dll" check               # All of the above
dotnet "Dino NetLab/bin/Release/net8.0/Dino NetLab.dll" build                   # "What a tooltip says is what happens": cards against what a running server does
dotnet "Dino BuildLab/bin/Release/net8.0/Dino BuildLab.dll" list --what cards   # Every book's card, to read
dotnet "Dino BuildLab/bin/Release/net8.0/Dino BuildLab.dll" list --what sheet   # Every line of the sheet
dotnet "Dino BuildLab/bin/Release/net8.0/Dino BuildLab.dll" list --what terms   # The whole lexicon
dotnet "Dino BuildLab/bin/Release/net8.0/Dino BuildLab.dll" list --what tree    # Where every node is, for hovering one with real mouse messages
```

`check` can say a sentence is well made and true. Only reading it can say it reads well, and a card has to be judged in the real game: `--hero 30 --talents a,b --equip c --panel book`, with real mouse messages, as nothing else proves a hover. `--panel` opens at frame 90, and "joined" prints about 5 s before the world is up, so wait for that before moving the mouse, or it leaves before there's anything to hover. To reach a word on a card, move the mouse from the thing onto the card in small steps, as a hand would.

Every check here has been seen to fail with its bug put back: keep it that way.
