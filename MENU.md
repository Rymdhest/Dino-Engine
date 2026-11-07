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

## Testing

`Dino BuildLab check` holds the roster. Nothing else about this can be proved by a bot: post real mouse messages to a hidden run.

```
"Dino Defenders.exe" --menu true --heroes folder --hidden true --size 1720x960 --snap file --sound false
```

**`--heroes` is not optional.** Without it a run with `--menu true` is working on that person's own heroes, and it moves their old save. Any other scripted run skips the menu, and `--seed N` shows the spot in another world.
