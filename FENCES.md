# Fences

Fences and walls: what kinds there are, how they're made, and how they stand on the land. Read `TERRAIN.md` for the land, and `LORE.md` before giving one a new look.

Status: rebuilt on 2026-11-08, checked by machine and looked at in the lab and in stills of the game. See "Where it stands" at the end for what nobody has judged yet.

## The one idea

**A fence is posts and spans, and a span is built for the slope it stands on.** A post always stands upright on the ground where it is. A span reaches from one post to the next, exactly. Nothing is stored: a model id says the kind, the style, the piece and the slope, and that's all it takes to build it again on any thread.

```
  run (SiteBuilder.Wall)            FenceLayout.Lay                      FenceForge.Make
  kind, style, from, to  ---->  posts where runs meet or stop  ---->  fence:rails:1f3a9c:e0   (an end post)
                                posts between bays                   fence:rails:1f3a9c:p1   (a post)
                                a span from each post to the next    fence:rails:1f3a9c:s0+3 (a span, built 3 steps uphill)
```

## Slopes

A world object is only moved, turned and stretched, which can't keep an upright upright on a slope. So each span is built already leaning into a slope, in steps of `FenceKind.Step` (rise over run): what runs along it, a rail, a band, a wall, is sheared up the slope with its ends plumb, and what stands, a stake, a pale, a stone, is only lifted by the slope where it stands (`FenceBuilder.Upright`). The layout picks the nearest step, stretches the model to the length, and turns it about its foot by whatever's left, which is a degree or so. Its far end then lands exactly on the next post, at the height that post stands, so rails meet posts at the height they leave them. Finer steps are straighter and cost more models: walls use finer ones, as they're tall.

Every span reaches `FenceKind.Sink` below the line from post to post, so a dip between two posts shows no daylight. Where the ground strays further from straight than a share of that, the run gets more posts, down to `LeastStretch` of a bay apart.

## Joins

Ends of runs of one style within `Join` of each other are one post. Two runs carrying on in a line share an ordinary post, or none at all for a wall. Where they turn, at a corner, and where a run stops, at a gate or a loose end, the post is an end post: heavier, taller, a pier or a cheek of big stones, which hides the joint whatever the angle.

- A rail runs into the middle of its posts, tapering like a tenon, so it meets any post of its style, at any angle.
- Palings, bamboo canes and the logs of a stockade are spaced half a pitch in from each end of a span, so the rhythm runs on across a post as if it weren't there.
- A wall's spans are never tilted: a tilt leans a span's end, and two neighbours leaning apart opened wedges a quarter of a metre wide at the top of a curtain wall. A wall is chained instead, each span starting where the last ended and rising by the slope step nearest the ground at its far end, so two spans share their joint's plane exactly and the ground strays from a foot by half a step at most. Two runs carrying straight on share the height between them (`FenceLayout.LayRun`).
- A wall's spans meet with nothing between them. They're built to the same section at both ends, their weathering comes round again at a span's end (`FenceLook.AlongWall`), and their texture repeats a whole number of times along a span, so its courses run on into the next. Each span's ends are closed across its section in the colour of a joint, so two spans on different slopes show a mortar line where they meet, never daylight.
- Dry stone needs none of that: each course's stones run a little past the span's ends, so two spans' stones lock together.

A run's inside is to the left of the way it runs, which is the spans' +z. `SiteBuilder.Wall` turns a run so its inside faces the site: a paling's pales face out, a stockade's rails are behind it, and a keep's battlements face away.

A wall that meets a tower can't stand in it, so it's tried up to the tower's foot and drawn on into it (`tuckFrom`, `tuckTo`). Keeps had almost no walls before this, as nearly every one overlapped its own towers and was refused.

## Kinds

| Kind | Form | Where | What it is |
|---|---|---|---|
| `rails` | Rails | Fields, and yards wherever they don't build in mud or on posts | Split rails let into hewn or round posts, two to four of them |
| `paling` | Paling | Gardens, and now and then a field | Pointed, rounded, clipped or flat pales on two rails, sometimes limewashed |
| `wattle` | Wattle | Gardens, and now and then a field, where they build in timber or logs; fields and yards in the dry country | Hurdles of rods woven in and out of stakes, lashed to stout posts |
| `bamboo` | Bamboo | Gardens, fields and yards in the East and over the wet, where they build on posts, and the Haven's training yard | A lattice of culms tied with black cord at every crossing |
| `stockade` | Stockade | A fortified village | Sharpened logs, lashed with rope, pinned to rails inside |
| `pen` | Pen | A camp, which is a brood's pen | The Reich's: bigger, tarred logs with fire-hardened points, black iron straps, hewn posts capped and spiked in iron, oxblood on the gateposts |
| `dry-stone` | DryStone | Some gardens where they build in stone, mud or clay tiles, which wall their gardens with the stones cleared from them | Field stones in courses, narrowing as they rise, copes on edge along the top, all of the world's own rock |
| `churchyard` | Churchyard | A churchyard | Coursed stone on a plinth under a saddleback coping, with piers capped by a pyramid, a ball or a step |
| `roofed` | Roofed | A churchyard in the East | Plaster over a stone plinth between dark posts, sometimes earthen with white lines, under a little roof with hipped caps at its ends |
| `curtain` | Curtain | A keep | Battered at its foot, a walk along the top behind merlons with arrow slits, a low parapet inside |

What a site puts up for a use is `FenceCatalog.Choose`, from how the site builds (`WorldData.StyleOf`, which is its land's way where its biome has one) and a fork of the site's own, once per use, so a village's gardens all match. A world draws `StylesPerWorld` styles of each kind from its seed, and a site takes one of them.

**A field is fenced light and in wood**: rails mostly, wattle or a paling now and then, bamboo or rails in the East, rails or wattle where they build in logs, wattle or rails in the dry country, cane or wattle over the wet, and rails or paling in the tile country. Stone was tried round fields and read as a churchyard's, and a stone wall's wider footing took a waygate's ground. A field's gate faces the nearest road, and a footpath runs from it to that road when there's one within `LongestFieldTrack` and nothing built is in the way (`SettlementStage.LayFieldTrack`), so a farm is somewhere people walk to.

**A run stops where nobody could stand**, on rock or ground too steep to walk, and starts again beyond with an end post each side (`SiteBuilder.Wall`), as a village's stockade once climbed a cliff and its spans, out of slope steps, leaned 42 degrees.

Black iron and oxblood are on the pen alone, as nothing but the Reich wears them. Dry stone uses the stone material, which the shader draws as the world's own rock at that height, so a wall matches the boulders it was cleared from.

## Styles

A style is a seed, and its genes say everything pieces of it have to agree on: how high the rails are, how thick the wall is, the course heights, the coping's profile, the post's size. What's a piece's own comes from genes of its own: its stones, its knots, how each log leans. A span's own genes differ with the slope it's built for, so a run over uneven ground never repeats. Every number comes from `Genes` by a name of its own, so a new one moves nothing else.

## Where it is

| What | Where |
|---|---|
| Kinds, uses, styles | `Dino Engine/Modelling/Procedural/Fence/FenceCatalog.cs` |
| Model ids | `FenceSpec.cs` |
| Building | `FenceForge.cs`, `FenceBuilder.cs` (logs, flat-faced timbers, dressed blocks, field stones, panels), and one file per family: `TimberFences.cs`, `BambooFence.cs`, `LogFences.cs`, `StoneFences.cs` |
| Standing them on the land | `Dino Engine/World/FenceLayout.cs`, from `FinalizeStage` |
| Putting a run down | `SiteBuilder.Wall`, `Ring`, `FenceAround` |
| Turning ids into models | `WorldAssetLibrary`, which builds them on worker threads with the plants and stones while a world loads |
| Materials | `FenceMaterials`, filled by `TextureGenerator.BindFenceMaterials` |

`Modelling/Procedural/Fence/` is headless: no GPU, nothing shared, safe on any thread.

## Testing

- `Dino FloraLab fencecheck`: every piece of every kind in several styles is sound (no bad numbers, normals with their faces), within budget and quick to build; no wider at a body's height than what's solid, past an overhang; every span of a laid-out scene reaches its post within 2 cm and leans under 2 degrees; every run's end has a post; and styles really differ, in shape and colour. Exit code 1 if not.
- `Dino FloraLab fences [--kinds a,b] [--styles N] [--span m] [--focus x,z]`: every kind laid out over a hill, round a right angle and a shallow turn, through a gate and on in a line, from the game's height and low down.
- `Dino WorldLab check`, which fails any post or span leaning more than 3 degrees, and `plan --site Kind`, which draws fences in white.

A tall wall near the edge of a wide screen looks knocked flat: it's seen nearly side on, as the camera's verticals fan out from straight below it. Check the lean in `check`, not by eye, before chasing it.

## Where it stands

**Nobody has seen these in a played game.** They were judged from the lab's pictures and from stills. Ask how they look first.

- Their cost on a slow machine is unknown. A dry stone span is about 4,000 triangles, the most of any kind, and a world builds a couple of hundred fence models in about 30 ms.
- Keeps are now walled in, which they almost never were before, as their walls overlapped their towers and were refused. A keep's fight now comes in through its gatehouse.
- A wall 7 m tall or more hides a hero standing just behind it from the camera, and isn't thick enough for `FocusOccluder` to open it. That was so of the old palisade too.
- There are no gates in the openings: a gap between two end posts is the way in.
