# The map

A land's map is an old chart of what's really in it, drawn on the CPU when the land loads (`Dino Engine/World/Chart/`, headless and deterministic: the same land always draws the same picture). The game shows it as the map (`UI/Panels.cs`, `MapPanel`) and as each land's picture on the map table (`UI/LandPanels.cs`), and `Dino WorldLab chart` writes it to a file with no game.

## What's drawn, and from what

Nothing on the chart is invented. Everything is read from the `WorldData`:

- **The sheet**: an old paper with mottling, stains and fibres, browned at its edges, inside a frame with the old checkered neat line, and a compass rose in whichever corner has least of the land in it. North is up, as the camera always looks north.
- **The ground**: each biome's `MapColour` as a watercolour wash, darker where one meets the next, shaded by the relief from the top left. Bare rock, yards, sand, mud and ash have washes of their own, every field is hatched its own way, beaches are pale, and paint gathers along every coast.
- **Water**: the sea and every pond, darker away from the shore, inked at the water's edge and ringed by ripples drawn along it, with little waves out on open water. Where the edge is comes from the height between pixels, so the coastline doesn't step.
- **Roads** are inked by class (a highway a double line, a track a line, a footpath dashed), and fences are dotted.
- **Trees** are the land's real trees: one a cell, the biggest standing in it, so a wood is as thick as it is and a lone tree is still there. Each is drawn in its own species' silhouette and colours, worked out from what grows it (`ChartTrees`): a crown's shape, how high it sits and how wide it is, and its palette brought up from an albedo to a watercolour. So a pinewood is spires, a cedar wood columns, a crimson wood red domes, a desert saguaros, a dead wood bare snags, and a new species is drawn right with nothing added.
- **Mountains** stand on each summit of the rock and the massif, the highest first and nothing too near one, in the form its biome's rock breaks into (`MountainForms.At`): teeth for jagged, benches for ledged, domes, towers, mesas and buttes for canyons, basalt columns and leaning blades. They're coloured by the world's `Geology`, hatched down their shaded face and snow-capped where the ground is snow or a cold biome's peaks are high. A mountain is made smaller until it hides nowhere anyone can walk.
- **Buildings** are their own footprints from the `PlacementIndex` (and the Haven's halls), stood up as the game's camera sees them from the south, at its foreshortening, coloured by how their site builds (`WorldData.StyleOf`), with the Reich's keeps in dark stone.

Whatever stands is drawn from the top of the sheet down, so what's nearer the bottom is in front. Nothing may stand over a road or a building: a tree whose crown would is left off.

The chart only shows a square round everywhere anyone can go (`WorldChart.FrameOf`), as the mountains round most lands would otherwise be most of the picture. `ChartFrame` turns a world position into the chart's: +x runs to the left and +z up, as in play.

## Where the hero has been

There's no fog of war, but what the hero has walked is plain: the walkable land is an uncoloured sepia sketch of the same drawing (`Chart.Sketch`) until the hero has seen it, about what the camera shows round them (`ChartDiscovery.Sight`), and then it's in colour, its edge ragged and a little darker, as a wash dries (`ChartFog`). The sea and the mountains round a land can't be walked, so they're always in colour, and the coloured part of the land is exactly where the hero has been. The Haven is home and known from the first step.

`ChartDiscovery` is this player's alone: kept by the client (`Client/Discoveries.cs`, on `GameSession.Explored`), never sent or saved, and remembered for the last few lands, so coming back into one finds the map as it was left. The map redraws the fog only where something new has been seen and sends only those rows to the GPU.

## The minimap

The top right of the screen is a round window on the same chart and fog, centred on the hero and turned as the land is seen, about 105 m to its rim (`UI/Minimap.cs`). It shows what the map does and nothing more: the hero, the lit waygates, and what the land asks, which stays on the rim pointing the way when it's further off. What the land asks and what it holds, its objectives, level, omens and orders, are listed under it (`Hud.DrawChronicle`). The minimap is what keeps the fog up to date while the map is shut (`MapPanel.TendFog`).

## Names

Each biome's name is spaced across where it has most room (`Chart.Labels`), and each village, hamlet, keep, ruin, graveyard and tower the hero has been to is written under it in ink, by the GUI, so they stay sharp. What the land asks, the lit waygates and the hero are drawn over all of it as before.

## Costs and checks

A 2048 chart takes about a second, on the loading task beside the models growing; the map table's are 512. After any change here run `Dino WorldLab chart --seed N [--archetype id] [--walked share]` on several kinds of land (it writes the chart and, beside it, how it looks with only the ground within that share of the walk from the landing seen), and look at the map in the game with `--panel map --bot true --walk x,z`. Nobody has played with it yet.
