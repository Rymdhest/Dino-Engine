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

**The picture is settled: don't change it unless a person asks.** On 2026-11-12 a person asked for it once and for all, after the old way kept moving it: lush grass, water, trees, a mountain far off and the giants, after a shot of sauropods across a lake from a meadow, with the hero in front, their back to the camera. Before that it was a spot searched for in a seed's land, and every change to how lands grow moved it, the last time onto a keep's paving.

So it's a world of its own kind, `vista` (`ArchetypeCatalog.Vista`, never drawn from a seed), laid out by `World/Vista.cs` rather than drawn: only its noise comes from `TitleScene.Seed`. The hero stands on a meadow 8.5 m over a lake, the camera behind them looking north as the game's does, and the meadow falls away from their feet steeply and then more gently to the shore, as ground that fell gently first would be a brow and the long grass on it a horizon hiding the water. Across the water is a level meadow where the giants graze (`Vista.Herd`), then woods everywhere, hills rising behind them, and a mountain 700 m off with low peaks behind it along the north edge, raised by `MountainStage` as any land's rock is. Its one place is `SiteKind.Overlook`, where the hero stands, with nothing built; it has one biome (Wildwood), no roads, camps or packs, and a clear morning's light from behind the camera's left. `WorldValidator` checks what the picture needs of it (`--archetype vista --count 1`).

Terrain is a square of `ChunkRadius` chunks a side (22 here) whose middle is `ChunkAhead` (96 m) in front of the camera (`TerrainQuadTreeSingleton.Ahead`), as nothing behind it is seen: that reaches the far meadow. No square can be wider than 22, as every chunk alive has a layer of the terrain's arrays and there are 2048 of them, the most NVIDIA's cards allow (`TerrainQuadTreeSingleton.MostRadius`); a square of 26 round the camera once handed out layers that didn't exist, and the land entered after the menu was drawn with its chunks' textures crossed, in squares. What's further is `World/FarLand.cs`: the world's own heights as plain meshes in 128 m tiles, its woods a canopy laid over the ground with crowns in it, rock darker on its faces and green on its ledges, and the air's colour taken on with distance, as the fog hugs the ground and leaves a mountain clear. It's grown on another thread the first time the picture's shown, kept for the game, and meets the chunks at the world's own heights, with nothing inside their square, as anything there would be carved out of the grass.

**The Haven's giants are in the picture**, on the far meadow, between the menu and the hero; `FindHerd` gives them `Vista.Herd` and keeps them inside the picture's edges and no nearer than 55 m (`GiantShow`, in `WILDLIFE.md`). They're grown the moment the game starts, and take three seconds, so they're there from the first frame on any machine with a few cores, and thin in otherwise. What's heard here is heard from the camera, which is where their calls carry to. `FindSpot` and `FindHerd` still search any other world given with `--archetype`, for looking at others.

They look away from the camera, out over the land. That's what third person is, it shows what they have on, and heroes have no faces, as the game's own camera has never needed one.

`ChunkRadius` is raised here, as this view is nearly level and the game's never is. The weather's held clear, as a first look at a game shouldn't depend on the clock. The camera drifts a little, as a still picture looks like a held breath. `Engine.RenderEngine.context.focusPoint` is cleared every frame, or depth of field goes on focusing on wherever a game left it.

## The screen

Text on the picture, and a shade that fades to nothing well before the hero. Nothing has a box round it but what's being typed in. The menu's root is `HitTestMode.PassThrough`: `None` looks the same and stops the mouse reaching anything under it.

The game's name at the top is its logo, grown in code as everything else is and never imported (`UI/Logo.cs`): the mark, the tyrant posed by the game's own animator part way through a roar and cut to facets on a hill before the land's mountains, with one Defender on it, and the title set with the engine's own type, all a flat print in the land's red, bone and ink. It takes a second or two, so it's begun as the game starts (`Logo.Begin`, from `ArpgGame.Begin`) and the menu draws it once it's grown. `"Dino Defenders.exe" --logo folder` prints it with no window, the mark at every size a social page takes and the title over nothing, and `Marketing/Social/make_social.py` lays the banners from those, so a change to it in the game is a change everywhere.

## The pages

Everything that isn't the game itself is one thing (`UI/MenuKit.cs`): words on a scrim, a rail of them down the left at the same place as the main menu's, and rows of a name and a control. There's one size for each kind of text in `Kit`, gold for what's chosen and parchment for what isn't, and nothing has an edge but what takes typing or a key. A new page is a `MenuPage` with rows on it, and a new setting is a `MenuRow`. Don't reach for `Ui.Action` or a dressed panel here: that's what the old pause menu was.

`GameUi.Into` opens a page from wherever this is, and `Back` goes to that. The game's HUD, the marks on things and the labels on finds all go while a page is up (`GameUi.Clear`), as a page is all there should be to read.

## Settings

Everything takes effect at once and is kept at once, so nothing has to be confirmed. They're in `sound.json`, `video.json`, `interface.json` and `keys.json` beside the heroes (`SettingsStore`). A scripted run has the defaults and writes nothing, unless it's given `--settings folder`, so it can't read or change anyone's.

**Graphics quality is the only graphics setting**, on purpose, but for Global illumination, a toggle of its own beside it until it's been played enough to go into the presets (`VideoSettings.GlobalIllumination`, read `GI.md`). `GraphicsPresets` is what quality comes to, and what goes in it is what buys frames for what can't be seen from the game's camera. Measured at 3440x1369 on a 7900 XTX: High 120 FPS, Medium 177, Low 207, with pictures from the game's camera hard to tell apart. Most of it is multisampling and grass. A world's own settings go with the world, so `WorldScene.Enter` applies it again, and the sun's map is remade when it changes. Multisampling doesn't go below two: grass leans on it. It also says how many creatures and leaves a world can have about at once, though what they cost is hardly worth saving (see `WILDLIFE.md`). Another level is a value in `GraphicsQuality`, a line in `Presets` and a word on the page.

`DisplayMode` is `WindowHandler.ApplyDisplay`. Borderless is always the monitor's size, so Resolution has nothing to say there. A screen of its own is entered and left by hand, as OpenTK only remembers where a window was for what it did itself.

`InterfaceSettings` is how the interface reads, and whatever's in it is read as it's drawn, so a new one is a property and a row. Health, Mana and Barrier are on the action bar and can also be over the hero, which is on unless turned off. Both are `Hud.DrawPools`, so they can't come to disagree. Whether an attack is marked on the ground goes by the rarity of what's throwing it, a setting each (`InterfaceSettings.Marks`), and normal enemies start off, as a crowd of those is what hides what matters.

## Keys

Nothing reads a key but through `Bindings`, and nothing shows one but by asking it, so a change reaches everything at once: the bar, the mark on a thing that can be used, every hint and card. **Never write a key in a string, and never read `Keys` directly.** The server doesn't know what anyone's keys are, so it can't say them.

A key means one thing, so taking one swaps the two and nothing is ever left with none. Menu starts on Escape, which nobody else can have, so whatever it takes a key from moves to where it started, or else to a free key, and the page says where. Anything can be on the keyboard or the mouse, and what's on the mouse asks the interface first (`Bindings.Asked`), so a press on a window never swings or opens anything. The first two places on the bar repeat while held (`HeroRules.HeldSlots`), whichever key they're on, and the rest are presses.

Three things aren't anyone's: Escape always goes back, as someone has to be able to get out; F3 is the developer's; and Ctrl and F is the pack's search, by habit from everywhere else. Clicking in the interface is always left to pick and right to open. A new action is a value in `GameAction`, a default, and a row in `Bindings.Page`, which `check` holds.

## Testing

`Dino BuildLab check` holds the roster and the keys. Nothing else about this can be proved by a bot: post real mouse messages to a hidden run.

```
"Dino Defenders.exe" --menu true --heroes folder --hidden true --size 1720x960 --snap file --sound false
```

**Only the pause menu stops the world.** Playing alone, the game pauses for the pause menu and the pages reached from it or covering everything (settings, the glossary, travel with friends: `GameUi.OnPage`) and for nothing else: with the hero's panel, a book, the talent tree, the map or anything used in the world open, the hero still walks and fights, as a click that isn't on a window still reaches the world (`Gui.WantsMouse`). What's opened by using something closes once the hero is `GameUi.UseLeash` from where they used it.

`--quality low` runs at a level, for `--perf`, and `--panel settings` or `pause` opens a page. Exclusive fullscreen can't be tried beside someone playing: even a hidden window comes to the front for it.

**`--heroes` is not optional.** Without it a run with `--menu true` is working on that person's own heroes, and it moves their old save. Any other scripted run skips the menu, and `--seed N` shows the spot in another world.
