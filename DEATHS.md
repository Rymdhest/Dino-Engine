# Deaths

When a hero falls, they're told what ended it, in a sentence or two, and what to do next: **Details**, or getting up or going home. Details opens the last moments, newest first, one line each, and every line's card says everything about it. It stays until they choose, because it's there to be learnt from. Read this before changing anything that touches a hero's Health, or the screen.

**Keep it short.** The first version had a Health graph, shares by enemy, type and delivery, a summary of every defence and what more of each would have saved, and a person called it a Bloomberg terminal: a game has to be easy to read. What's on the screen now is all that's wanted. Anything more goes on a line's card, one hover away, or nowhere.

## The one idea

**The account is written down where the rules act, from the numbers they used, and never worked out again.** A death recap that guesses is worse than none: a player would build against the wrong thing. So nothing on the screen is reconstructed from what was drawn or from the hero's sheet afterwards. The server writes each line at the moment it happens, and the client only adds it up and says it.

And it checks itself: Health is read every tick, and whatever it did that no line explains is counted as `DeathRecap.Unaccounted` and shown in the defences ("Health was lost to something this account doesn't name"). It ought to be nothing, and `Dino NetLab deaths` fails if it isn't.

## How it fits together

| Where | What it is |
|---|---|
| `Stats/Hits.cs` | `Mitigate` with a `Mitigation`: what Resistance, Armour, Physical Damage Reduction, Damage from Hits, Damage Taken and Shock each took off a blow, by type, as the same arithmetic. `OverTime` is damage over time's, which `Combat.Linger` uses too. `Recover` says what was drained, what fell due and what was regenerated |
| `Server/Ledger.cs` | One per hero (`ServerPlayer.Ledger`): the last `Ledger.Window` seconds of lines, and Health read every tick for what no line names. `Fall` makes the `DeathRecap` |
| `Server/Combat.cs` | Writes every line: a Hit (`Struck`, with what left them, each defence, what a friend, a Minion, Gold, a cap or the ice took, and what each pool paid), a miss (`Missed`, with the odds they had), damage over time and owed Debt and Health Drain (`OverTime`, one line a run), saves from death and Last Gasp running out |
| `Server/GameServer.cs` | Book costs, Flasks, the sample each tick, and sending the recap the moment someone falls (`SendRecap`) |
| `Gameplay/Recap.cs` | What a `DeathRecap` holds and how it goes over the wire (`Message.Recap`), with the cards of the Books used on the hero as the server built them, since the client has no sheet for an enemy |
| `Tooltips/RecapText.cs` | Everything the screen says, headless, in the game's own words, so BuildLab reads it: the headline, the rows and each row's card |
| `UI/DeathScreen.cs` | The screen |

## What it says

- **First**, what ended it: the last line that took Health to nothing (`DeathRecap.Fatal`), named by who and with what ("Slain by Raptor with Raking Bite.", "Burned to death, by a Burn Firebrand left on you.", "Your Debt fell due."), then what got through and what they had to take it ("A Hit of 317 Cold got through your defences. You had 122 Health."), Barrier too if they had any, as Health alone would make the sum look wrong. Nothing about how far past it went or how long it took.
- **Details**: the last moments, newest first, each a row of when, who, what, what came of it and the Health left. Each row's card has what left them, each defence's share, what got through, who else took some, what each pool paid, what it left on them, and the odds they had to Evade or Block it. The Book's name opens its card, and every term reads with Ctrl held.

## Falling, and getting up

Nobody is stood up or sent home by the clock any more: the screen is the choice.

- **In the Haven** (and the asset lexicon) the button is **Rise**: `HeroAction.Rise` stands them up full (`GameServer.StandUp`).
- **In a land**, one life to it holds. While anyone still stands, a fallen hero's button is **Watch your friends**, which folds the screen into a strip that opens it again. Once everyone has fallen the land is over (`ServerSession.Over`, sent with progress as `GameClient.Over`), and any of them can press **Return to the Haven**, which is `HeroAction.ReturnHome` allowed to the fallen only then.
- The bot presses them after a few seconds (`HeroBot.FramesDown`), so sweeps go on.

## Adding to it

Anything new that changes a hero's Health has to say so to their ledger, or it shows as unaccounted:

- **Damage**: go through `Combat.Hit` or `Combat.Harm`, which write it. A new kind of Indirect Damage passes a `HarmCause`, with its words in `RecapText`.
- **Something that takes Health by itself**: a `RecapKind` with an `OverTime` line, or an instant one like `Paid`.
- **Healing**: `Ledger.Leeched`, `OnKill`, `Regenerated`, `Drank` or `Turned`. Anything that doesn't is counted as Health from "anything else", which is true but says less.
- **A save from death**: `Ledger.Saved` with a `RecapSave`.
- **New words** go through BuildLab's vocabulary like any card: `TooltipChecks.AnyFall` is a recap with one of every kind of line, every one the last in turn.

## Testing

```
dotnet "Dino NetLab/bin/Release/net8.0/Dino NetLab.dll" deaths      # Real falls on a real server, held against Health, the rules and the server's own copy; --show true prints one to read
dotnet "Dino NetLab/bin/Release/net8.0/Dino NetLab.dll" lands       # Rise in the Haven, nobody sent home by the clock, Return to the Haven once everyone's down
dotnet "Dino BuildLab/bin/Release/net8.0/Dino BuildLab.dll" check   # Every sentence the screen can show
```

Judge the screen in the real game: `--brawl` a few creatures at a hero who doesn't fight back (`--seed 303 --archetype isle --brawl war-raptor,hornback,raptor --shot out.png --frames 1500`), and real mouse messages over its lines and words, with Ctrl held, are what prove the cards. Nobody has fallen to it in play yet.
