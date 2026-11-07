# The main menu

The first thing anyone sees: a real place in a real world, with a hero standing in it, and as little laid over it as there can be. Read this before changing it.

## Heroes

A hero is one save file in `heroes/` beside the other saves, with all the world they were in, so nothing about one touches another (`HeroRoster`). Whoever was played last comes first. `SaveGame.PathOverride` is what points the rest of the game at the chosen one, so nothing else knows there's more than one.

The save from before there were several is moved in as the first hero, called Wanderer. It's copied first and only then moved aside to `save.json.before-heroes`, so there's no moment at which it's nowhere. Testers carry their saves across builds, so that has to keep working.

A hero who's deleted is moved to `heroes/deleted/`, as the game can't undo it and a slip of the hand shouldn't be final. A save that can't be read isn't shown, and isn't touched.

Someone new has no land yet (`SaveGame.NeverPlayed`). Their first step grows one, and they get the prologue.

**At the menu nobody is being played.** `_hero` is null and `Save` does nothing, as whoever's on the spot is only being looked at, and from the moment "Choose another hero" is pressed there's nowhere safe to write to.

## The place

`TitleScene` is a world like any other, drawn by everything that draws one, with no server and no session. Anything a client runs has to cope with `GameSession.Current` being null. The hero is one body, on the bones and in the gear they have in the game (`Wardrobe.Dressed`), standing at their ease by `CharacterAnimationSystem`, which needs nothing but a position.

It's in `TitleScene.Seed`'s world, chosen by looking at eight. Where in it isn't written down, as a change to how worlds grow would move anything that was: `FindSpot` looks for depth, which is open ground to stand on, nothing at all between them and the camera, water beyond them and the far shore a long way off, and light from the side. A view that ends at the first trees has nowhere to look. If worlds change, look at the picture again: this is the one place where a score can't be the judge.

They look away from the camera, out over the land. That's what third person is, it shows what they have on, and heroes have no faces, as the game's own camera has never needed one.

`ChunkRadius` is raised here, as this view is nearly level and the game's never is: terrain stops at that distance, and the sea with it, which is the soft band at the horizon. The weather's held clear, as a first look at a game shouldn't depend on the clock. The camera drifts a little, as a still picture looks like a held breath. `Engine.RenderEngine.context.focusPoint` is cleared every frame, or depth of field goes on focusing on wherever a game left it.

## The screen

Text on the picture, and a shade that fades to nothing well before the hero. Nothing has a box round it but what's being typed in. The menu's root is `HitTestMode.PassThrough`: `None` looks the same and stops the mouse reaching anything under it.

## The pages

Everything that isn't the game itself is one thing (`UI/MenuKit.cs`): words on a scrim, a rail of them down the left at the same place as the main menu's, and rows of a name and a control. There's one size for each kind of text in `Kit`, gold for what's chosen and parchment for what isn't, and nothing has an edge but what takes typing or a key. A new page is a `MenuPage` with rows on it, and a new setting is a `MenuRow`. Don't reach for `Ui.Action` or a dressed panel here: that's what the old pause menu was.

`GameUi.Into` opens a page from wherever this is, and `Back` goes to that. The game's HUD, the marks on things and the labels on finds all go while a page is up (`GameUi.Clear`), as a page is all there should be to read.

## Settings

Everything takes effect at once and is kept at once, so nothing has to be confirmed. They're in `sound.json`, `video.json` and `keys.json` beside the heroes (`SettingsStore`). A scripted run has the defaults and writes nothing, unless it's given `--settings folder`, so it can't read or change anyone's.

**Graphics quality is the only graphics setting**, on purpose. `GraphicsPresets` is what it comes to, and what goes in it is what buys frames for what can't be seen from the game's camera. Measured at 3440x1369 on a 7900 XTX: High 120 FPS, Medium 177, Low 207, with pictures from the game's camera hard to tell apart. Most of it is multisampling and grass. A world's own settings go with the world, so `WorldScene.Enter` applies it again, and the sun's map is remade when it changes. Multisampling doesn't go below two: grass leans on it. Another level is a value in `GraphicsQuality`, a line in `Presets` and a word on the page.

`DisplayMode` is `WindowHandler.ApplyDisplay`. Borderless is always the monitor's size, so Resolution has nothing to say there. A screen of its own is entered and left by hand, as OpenTK only remembers where a window was for what it did itself.

## Keys

Nothing reads a key but through `Bindings`, and nothing shows one but by asking it, so a change reaches everything at once: the bar, the mark on a thing that can be used, every hint and card. **Never write a key in a string, and never read `Keys` directly.** The server doesn't know what anyone's keys are, so it can't say them.

A key means one thing, so taking one swaps the two and nothing is ever left with none. Menu starts on Escape, which nobody else can have, so whatever it takes a key from moves to where it started, or else to a free key, and the page says where. Anything can be on the keyboard or the mouse, and what's on the mouse asks the interface first (`Bindings.Asked`), so a press on a window never swings or opens anything. The first two places on the bar repeat while held (`HeroRules.HeldSlots`), whichever key they're on, and the rest are presses.

Three things aren't anyone's: Escape always goes back, as someone has to be able to get out; F3 is the developer's; and Ctrl and F is the pack's search, by habit from everywhere else. Clicking in the interface is always left to pick and right to open. A new action is a value in `GameAction`, a default, and a row in `Bindings.Page`, which `check` holds.

## Testing

`Dino BuildLab check` holds the roster and the keys. Nothing else about this can be proved by a bot: post real mouse messages to a hidden run.

```
"Dino Defenders.exe" --menu true --heroes folder --hidden true --size 1720x960 --snap file --sound false
```

`--quality low` runs at a level, for `--perf`, and `--panel settings` or `pause` opens a page. Exclusive fullscreen can't be tried beside someone playing: even a hidden window comes to the front for it.

**`--heroes` is not optional.** Without it a run with `--menu true` is working on that person's own heroes, and it moves their old save. Any other scripted run skips the menu, and `--seed N` shows the spot in another world.
