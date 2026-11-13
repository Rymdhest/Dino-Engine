# Shells: moss and fur

Moss and fur are drawn as shells: the surface they grow on drawn again a few times, each layer pushed a little further out along it, and each cut from one
height field of what grows there. It's the oldest way of drawing fur in real time (Lengyel, Praun, Finkelstein and Hoppe, "Real-Time Fur over Arbitrary
Surfaces", 2001), and what moss on stone and the coats of animals are in most games since. Everything here is in the G-buffer like anything solid, so the sun,
the sky, lamps, shadows, bounced light, fog, reflections and rain see it with nothing more said, and nothing after the geometry pass knows shells exist.

## What grows where

- A growth is a `ShellStyle` (`Rendering/Shells/`, headless), by a number in `ShellStyles`: rock moss, ground moss, bark moss, the coats of wolves, dire
  wolves, bears, boars and rabbits, and three for dinosaurs: `Plumage` (the reaper's and the strider's feathers), `Shag` (the shagback's coat of long filaments,
  whose locks hang and sway) and `SpiderHair` (the weaver's bristles, in rows down its legs and abdomen, pale at their tips). A model carries its number (`glModel.Shell`, set by the loader from `Mesh.ShellStyle` or `FloraMesh.ShellStyle`), and 0 is
  nothing. A new growth is a number and a line in `ShellStyles.All`, at the same place.
- A style's `Mask` says where on a surface it grows. `Vertex` reads each vertex's own amount (`Vertex.Shell`, `FloraVertex.Shell`, from a `VertexMaterial`'s
  `Shell`): a bear's everywhere but its pads, nose and lips (`BearDress.Shell`), a wolf's shorter down its legs and muzzle and none on its pads, nose or lips (`WolfDress.Shell`), longer on a dire wolf (`DireFur`), with a dire wolf's hackles laid on as tufts, a wild boar's thinner down its legs and its snout and none on its hooves or the disc of its snout (`BoarDress.Shell`), with its mane laid on as tufts, a rabbit's everywhere but its eyes and ears (`BodyMesh.Shell`),
  a dinosaur's as its species says (`SaurianSpecies.Coat`, with how far it goes down the legs, over the head and under the belly, or along the top as a mane,
  worked out per vertex by `SaurianDress.Shell`, so a snout, jaws, shins and feet stay scaled; it grows on the living and the rotting, and bones and ghosts have none),
  and bark moss is exactly as much as `BarkSkin` paints into the bark, on wood thick enough for a cushion (`ColourAt`'s `moss`). `Rock` grows wherever
  Rock.glsl paints moss on stone, by the same function (`RockGrowth.glsl`'s `rockGrowthAt`, which Rock.glsl paints by), so the painted moss and the moss
  standing on it are never in different places; it goes under the rock's snow. Stones but pebbles and chips (`RockForge.Forge`) and every tile of the rock's
  skin (`RockSkin.Build`) carry it. `Ground` is the terrain's moss and its mossy rock, each as a share of what the ground is made of there, read per vertex,
  as the chunk's maps have a texel to a vertex. Settled snow lies over moss rather than letting it stand through.
- What grows takes the colour of what it grows from, where it grows: its material's texture at its texture coordinates times its vertex colour (the ground's
  moss as Terrain.frag reads it, stone moss in geology's moss colour), so a growth and the surface under it are one at the root and where it thins out. A tree's
  is tinted as its tree is (`plantTint`), and a wild animal's as it is.
- Where a growth thins, the carpet goes first and then the clumps, from the smallest, so that a share of it covers about that share of the surface, as the
  painted colour under it does: an edge of moss is the odd cushion going out into bare stone. Only thick moss is a carpet.

## The layers

- Each renderer that draws something that grows draws its shells after its own geometry, from the same commands (`GeometryCommandDrivenRenderer.ShellPass`):
  `InstancedModelRenderer` (stone, skin, trees, bending exactly as their own pass bent them), `CharacterRenderer` (beasts, writing the characters' stencil bit so
  ground marks go under fur as under bodies), `WildlifeRenderer` and `TerrainRenderer`. `ShellRenderer` holds the programs, the styles on the GPU (UBO 13,
  `Shells/ShellStyle.glsl`, seven rows a style: change both together) and how many layers a growth needs.
- One draw covers every layer of every instance: an instanced model's matrix is read once for all its layers (a divisor of the layer count), and layers go
  outermost first. Only triangles anything can grow on are drawn at all: the loader puts them first in the index buffer (`glLoader.ShellsFirst`) and the shells
  draw that much of it, so a tree's leaves and the underside of a stone cost nothing.
- A layer stands at a share `h` of the length, from the style's `Floor` up: below it a layer would only ever be inside what grows. Its offset is along each
  vertex's shell direction (`glModel.ShellAttribute`, location 12): the welded normal of every vertex in one place, as long as the face leant furthest from needs
  to keep the full depth, so a seam or a hard edge never opens a crack through moss (`RockForge.ShellNormals`, `glLoader.ShellData`). A rock skin tile's points
  on its edge go by the field's gradient, as its normals do, so tiles agree. A strand bends from its root: lean along the comb, droop and sway with the plants'
  springs (`vegetationBend`) go with the square of `h`.
- How many layers: the growth's length on screen in pixels, from its floor up, times `ShellSettings.LayersPerPixel` (1.5 on High, 1.25 on Medium, 1 on Low),
  within the style's fewest and most. From the game's camera that's three to six. Past `FadeFrom` a growth lies down to nothing by `FadeTo`.
- Whatever cuts the surface cuts its growth by the same rule: trees opening for the hero, halls the camera looks into, a body thinning in or out, wildlife's fade.
  The ground's moss is pressed down where something walks, and comes up as the plants' touch lets go.

## The field (Shells/Shell.glsl)

- It's read at the anchor, the point of the surface a layer stands over, in a space that moves with the surface: the world for what stands still (a tree's
  where it rests), the bind pose at the instance's size for a body. Every layer agrees where each clump is, and nothing swims as a body moves or a tree sways.
- Clumps are domes, found by Worley's search cut to the eight cells nearest (four on the ground's own plane, `SHELL_PLANAR`): a clump is never wider than half a
  cell and never more than a quarter from its cell's middle, so nothing further can reach. A clump's middle within half a cell of the surface counts, the smaller
  the further off, which keeps the clumps of a curved surface whole. Fur's locks are stretched along the comb (back and down a body) in the frame of the comb
  where the point is, and highest at their root, so they overlap as shingles; drawing the whole pattern out along the comb sheared it by how far a point is from
  the model's middle, and a head came out in needles. A coat and stone moss have a second lattice laid between the first's (`Lattices`).
- Over the clumps the fine strands thin out to nothing: the fringe, which only grows out of what's there, as a fringe over bare stone between cushions was a
  carpet that wasn't. Its wisps only show where one is more than a pixel across.
- Everything is band-limited by a pixel's footprint, worked out from the distance and how far the surface is turned (never taken from the screen, whose
  derivatives step from one triangle to the next and stepped the clumps in and out in blocks): a clump under about half a pixel across goes to what the clumps
  come to on average. Slopes are worked out with the field, so they mean something wherever a neighbour was discarded.
- A layer covers a sample where the density at that sample (carried from the pixel's middle along its worked-out slope on the screen) passes a threshold of the
  sample's own that turns with each layer (`shellCoverage`). Edges fall between samples as a polygon's would, and where the strands are too fine to see the
  layers fill different samples. It's the same in every pixel and every frame, so it never crawls: there's no noise in it at all.
- Every layer over a point writes the same normal (the clumps' slope, held within 42 degrees of the surface), so the resolve finds a pixel's samples on one
  surface and lights it once, with its layers' colours averaged. Down in a crease between clumps less sky and sun get in (`Crease`), the root is darker than
  the tip, and the tips let light through. What grows gets wet as anything does, but nothing stands on it.

## Performance

- Measured at 3440 by 1369 on the 7900 XTX, in the mossiest places there are: a cloud forest, moss on every metre of ground, every stone and every trunk,
  0.8 ms for the ground and 0.7 ms for stone and trees (1.8 in all in the rain), and a pinewood under a mossy cliff, 0.7 and 1.05. The Haven pays 0.2 ms,
  and where nothing grows it costs nothing. `--render shells=0`, `shellground=0`,
  `shelllayers=N` and `shellmost=N` change it on a scripted run, and F3's Render window has all of them. Low leaves the ground's moss out (`QualityPreset.ShellGround`).
- What a layer costs is mostly being drawn at all, then its field: a frame of ground is three full-screen layers. What took it from 6 ms to 1.5: the ground's
  clumps on its own plane, a floor under which no layers are drawn, a cheaper hash, the ground's moss read per vertex, and only what each kind needs carried
  from the vertices.
- Each kind of thing is a pair of files (`Shells/Shell_Instanced.vert` and the rest) that declares only its own uniforms and includes `ShellVertex.glsl` or
  `ShellFragment.glsl`, as `ShaderProgram` looks up every uniform in the text it's given, whatever an `#ifdef` says.

## Checks

After any change here run `Dino FloraLab check`, `rockcheck` and `bend`, `Dino WorldLab skin` (tiles still meet), `Dino AnimLab check` and `Dino LifeLab check`,
and look at it in the game, with shells on and off: `--seed 1 --archetype archipelago --biome cloudforest` for ground moss, `--garden rock.granite-boulder,rock.granite-tor`
with `--weather clear` for stone, `--haven true --parade wolf,direwolf,boar,bear --look 24 --calm true` for fur, `--parade shagback,reaper,strider,weaver` for the
dinosaurs' coats, and `--perf` against the build before. The main
menu's picture is settled, so nothing grows there (`ShellRenderer.Held`).

Nobody has seen any of it move: it was judged from stills, on one GPU.
