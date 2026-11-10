# Flora

Every tree, shrub, fern, flower, mushroom, log and leaf on the ground: how they're grown, what they're made of, and how to add more.

Status: built, and looked at by machine and in stills. See "Where it stands" at the end for what nobody has judged yet.

## The one idea

**A plant is a species and a seed.** No model is ever stored. The same two things grow the same plant on any machine, on any thread, as often as it's asked, so a model id is all there is to know: `oak#1f3a9c`. A species is data, a page of ranges, so a new one is new numbers in a catalogue and no code at all. A new *kind* of plant, something that grows a new way, is a new `PlantForm`.

```
   a species (ranges)      seed
           \               /
            \             /                     per world, several of each
             Genes (named draws)  ------------> "oak#1f3a9c"
                   |
     skeleton: scaffold + bud competition       Weber & Penn, then Palubicki & Runions
                   |
     thickness: pipe model                      Shinozaki
                   |
     light: shadow propagation                  Palubicki
                   |
       axes -> skin -> joints -> cards          what's drawn, and how it moves
```

Everything in `Modelling/Procedural/Flora/` is headless and pure. It knows nothing of GL and it can't read `TextureGenerator`, as any static on that class compiles shaders: what it needs from the material arrays comes through `FloraMaterials`, a few numbers the client fills in. That's what lets plants grow on worker threads while a world loads, and what lets `Dino FloraLab` look at them with no window at all.

## Where the ideas came from

The reference is Ghost of Tsushima. Its public sources say plants are rigged with joints, that trunks and branches are controlled apart, and that leaves belong in forests by biome. How it grows its trees isn't public, so the growing is from the papers.

| Source | What's used |
|---|---|
| Weber & Penn 1995, *Creation and Rendering of Realistic Trees* | The scaffold: levels of stems with counts, down angles, divergence, ShapeRatio crown shapes, stems stopped at the crown's edge, Flare, Lobes |
| Palubicki et al. 2009, *Self-organizing Tree Models* | Fine wood: the growth cycle, shadow propagation, the extended Borchert-Honda split of resource, bud fate, shedding, and a shoot's direction as a weighted sum |
| Runions et al. 2007, *Modeling Trees with a Space Colonization Algorithm* | Markers of free space, thicker at the surface of a crown; relaxing a skeleton |
| Shinozaki 1964, by way of both | The pipe model: `r^n = r1^n + r2^n` |
| Bloomenthal 1985, *Modeling the Mighty Maple* | Generalized cylinders on parallel transport frames |
| Runions et al. 2005 and Infinigen 2023 | Leaf margin terms; fern and leaf structure; logs made from trees |
| Castaño, *Computing Alpha Mipmaps* | Keeping a cutout's coverage down the mips |

What was learned by trying, which the papers don't say:

- **Shade alone doesn't guarantee a crown.** Left to it, one seed fills out and another leaves a whole side bare. Markers go where a crown should be, so wood keeps growing until it's filled. They also make the shape: noise takes holes out of them, and what grows comes in masses with air between, which is what a crown looks like from above.
- **Markers can't decide shedding.** They're a yes or a no, so a branch would go the moment it stopped growing (Palubicki says so). Buds read markers, shedding reads the shadow grid.
- **Trees here are shorter than the camera allows, not than they should be.** The camera is some 33 m up. A crown that reaches it is a wall, so redwoods are all girth.

## Forms

| Form | What it grows |
|---|---|
| `TreeForm` | Anything with wood: every tree, every shrub, bamboo, palms, saguaro, snags. Seven steps, below |
| `RosetteForm` | Leaves from one crown on a golden angle: ferns, agave, banana, hosta, and the head of a palm (`TreeForm.Head`) |
| `TuftForm` | Blades and stalks with something on top: reeds, pampas, bulrush, grain |
| `HerbForm` | Leafy stems with a flower or a spike on each |
| `FungusForm` | Lathed mushrooms in a group |
| `GroundForm` | A card laid on the ground, for two triangles: leaf litter, moss |
| `DeadwoodForm` | Logs and stumps, in the bark of whatever fell, through the same skin as a tree |
| `PadForm` | Prickly pear |

**What a tree does**, in `TreeForm.Grow`:

1. **Scaffold** (`Scaffold`). What a species *is*: the trunk and the levels of `LimbForm`. For a conifer this is nearly everything, as its architecture is rigid enough to write down.
2. **Fine wood** (`BudGrowth`), if there's a `BudForm`. Each year buds sense free space and light, a resource earned by light is shared back through the tree, shoots grow, and what can't pay for itself is shed. `Apical` is lambda: above a half it's excurrent, a main stem, and below it decurrent, a spreading crown.
3. **Thickness** (`PlantGraph.Thicken`). The pipe model, scaled so the foot comes to the trunk's `Girth`. `LimbGirth` thins limbs on a trunk far too big for them.
4. **Light** (`TreeForm.Light`). Shade from everything, read back at everything, which later says what's dark.
5. **Axes** (`PlantAxis.From`). The graph split into runs with no fork. Wood finer than `WoodCut` isn't skinned at all: that's what's on the cards.
6. **Skin and joints** (`BarkSkin`, `Rig`).
7. **Foliage** (`Crowning`).

**A skeleton is always a `PlantGraph`**, however it was grown, so everything after it is written once. A node always comes after its parent, so going through the list is going from the root out.

## Skin

Generalized cylinders on parallel transport frames, with rings spaced by how thick a stem is and closer at the foot, where all the shape is, and as many sides as that's worth. Rings have to stay close enough on a tall trunk, or it's a cone between its foot and the first thing that changes it (`LongestRing`).

**Bark materials are pale.** What colour a bark is goes in the vertices, so one is every bark of its kind. Each costs 64 MB, so a new `BarkKind` should be a new surface and never a new colour. A kind is a line in `MaterialCatalog`, a recipe in `MaterialRecipes.Bark.cs` (read `TEXTURES.md`) and a line in `TextureGenerator.BindFloraMaterials`. Cells read as crazy paving: an oak's fissures are where a noise stretched up the trunk crosses its middle, so they braid and never close, and a pine's plates have wandering edges, faces terraced where flakes came away and furrows with a bottom of their own.

Ridges at the foot face the way roots do, or they look stuck on. Moss likes the damp foot, the upper side of a lean, and one side more than the other. Wood inside a crown is darkened by the light it stands in, or limbs shine through.

## Cards

A crown can't be leaves, so it's cards, as any game's is. What's different is that **cards are painted on the CPU**, from real parts, in the same pure way as everything else.

- `LeafForm` is a leaf as numbers: an outline as a half width along a midrib, or a radius about the stalk for a palmate leaf, with its veins following the same geometry so the two can't disagree. Terms are a botanist's: `Lobes`, `Teeth`, `DripTip`.
- `CardForm`s paint leaves onto twigs on a `CardCanvas`, back to front. It's a painter's job and not a rasteriser's, and leaf outlines are soft, so everything comes out antialiased with no extra samples.
- A layer of the model texture arrays holds **one kind of card in four variations**, one to a corner, and a plant picks between them. Four pictures of one kind break up repetition where four kinds of one picture wouldn't.
- **Cards are pale**, so one serves a green tree, a red one and a gold one. A leaf differs from its neighbour in light far more than in colour.
- `CardImage` builds its own mips. Averaged alpha falls below the cutoff going down a mip chain and a crown thins as the camera backs off, so alpha is scaled at each level to keep the coverage of the full picture. **It has to come out under and never over**: a small level can't hit it exactly, and over makes a sparse card a solid square in the far shadow cascades, which read those levels.

Nothing about a card touches the GPU until `TextureGenerator.LoadCards` copies it up, and painting them runs on other cores while the materials are made. A card is worked on as ten floats a texel, on a canvas each thread uses again, and only its bytes are kept (`CardLevel`): as floats a library is 2.4 GB, which once took the game to a peak of 4.3 GB and made routine collections take 20 ms. A layer costs 16 MB, so cards are about 700 MB in all.

**Cards aren't solid.** Three things follow, each of which was a bug:

- `Model.frag` doesn't turn a card's normal over on its back face. Its normals are the mass it's part of, which is the same from either side.
- `GrassCarve` skips anything on a card layer. It draws triangles with no alpha, so a leaf on the ground stamped a rectangle out of the field.
- A card that droops onto the ground lies on it (`LowestCard`), as under it, it's only paying to be drawn and it's pinched by `PlantRig.Floor`.

## Foliage

`Crowning` puts cards on a tree. Tips of the wood are where cards can go; those in the light are taken first and the rest thinned, so they end up where a real crown has its leaves. A card stands on its own twig, about its length back from the tip, and hangs from the nearest wood with a joint. Standing them on the wood itself gives pom-poms on limb ends.

What makes it a mass and not a pile of pictures:

- Cards are gathered into clumps by k-means, and each clump is shaded as one soft shape. Normals bend towards the clump's surface and the crown's (`Shell`), which is what artists do by transferring normals from a sphere.
- **Colour is painted in**, from deep underneath to warm on top (`Palette.Shade`, `Body`, `Sun`), by clump and light. From this camera most of a crown is seen from above, and real shadow alone won't model it.
- A card is creased down its middle (`Fold`) and droops at its end, as a flat one reads as flat from any side.

## Colour

**Colours are albedos under a sun of about 13.** Anything meant to read as bright is still deep and saturated: gold is `(0.4, 0.24, 0.02)`, not `(0.9, 0.7, 0.1)`, and the second goes pale. Judge colour in the game, as the lab's picture is brighter than it. Set palettes next to the game's own grass, which is the reference.

No two plants off one mesh are the same colour: `plantTint` in `Plant.glsl` shifts each a little by where it stands.

## Joints

Plants bend as `PlantRig` and `Plant.glsl` say, and that has its own rules (AGENTS.md). What `Rig` adds for trees:

- A vertex walks up at most `PlantRig.MostLevels` joints, so chains are kept to what adds up to that with a card on the end: two on a trunk, two on a limb, one on a branch, one on a card.
- **Thick wood bends less**, by `StiffRadius`. A limb as thick as a trunk doesn't whip like a twig.
- **`BendThatHolds` limits a link by how far its wood strays from its line.** A joint is a straight line, and the outside of a bend stretches by how far it stands off that line for how long the line is, so what's stubby can hardly bend, and nor can a limb that curls back on itself.
- **Fine wood rides on a `PlantJoint.Rigid`**, which never bends. Bound to its parent's link, each of its vertices would work out for itself where it sits on that link, and an arc shears anything that reaches out from it. Anything growing from fine wood hangs from what carries it, or it would go deeper than a vertex can walk.
- `TreeForm.Walkable` is for what a body goes through. Its stems answer to touch, and nothing up in a tree does.

## In the world

A biome lists species and doesn't know about seeds. `NatureCatalog.ModelAt` says which plant stands somewhere from where it is, so nothing is drawn from a stream that anything else depends on, and `Variant` makes a model id from the species, the world and which one. **A new world is new seeds, and so plants nobody has seen.** A species' `Variants` is how many a world grows.

`WorldAssetLibrary.Grow` grows a world's plants on every core while it loads, and `WorldScene.Enter` uploads them all at once, so that walking into a meadow doesn't cost a frame. What neither this world nor the last uses is let go (`Retain`): by then nothing can still be drawing it. `--travels` is what exercises that.

Logs are solid along their whole length (`SolidLength`), as what looks solid has to be.

A biome's `MoteColour` is what falls in it, and leaves and petals only fall where there's canopy overhead.

## How to add a species

Find the catalogue in `Flora/Content/` and add a field. There's nothing to register: `[FloraCollection]` is found by reflection, sorted by id, and a card's layer is its place on that list.

```csharp
/// <summary>A rowan: small, upright, with a hard frost of white in spring.</summary>
public static readonly PlantSpecies Rowan = new PlantSpecies("rowan", WorldAssetKind.Tree, Spreading(11f, 0.42f, 0.4f, 0.03f, Leaves(SmallLeaves, Summer()), GreyBark(), CrownShape.Flame, 0.52f))
{ Footprint = 6f, SolidRadius = 0.6f, Variants = 5 };
```

Then put it in a biome's list and run `Dino FloraLab sheet --plants rowan`. A new leaf is a `LeafForm` and a new card is a `LeafSpray` (or any `CardForm`) field beside it. `check` fails a species that grows in no biome.

Numbers worth knowing, as they're what change a tree most:

- `Trunk.Reach`: how far up the trunk goes. Near 1 is a conifer, under a half is a spreading tree.
- `Crown`, `CrownBase`, `CrownRadius`: the outline. Limbs stop at it and buds turn back from it.
- `BudForm.Apical`, `Gaps`, `ClumpSize`, `Hollow`, `Years`: excurrent or decurrent, how broken up, how filled.
- `FoliageForm.Spacing`, `MostCards`, `CardSize`, `Pose`: how much leaf.

## Budgets

A thing's triangles are paid for thousands of times over, so `check` holds these:

| Kind | Most triangles | Typical |
|---|---|---|
| Tree | 13,000 | 4,000 to 8,000 |
| Shrub | 2,600 | 1,000 |
| Ground cover | 1,400 | 200 to 400 |
| Deadwood | 2,000 | 800 |
| Litter, moss | | 8 to 16 |

A crown near the camera is seen at about 100 pixels a metre, as trees reach to within about 10 m of it. That's why a card is a few metres and 512 texels, and not ten metres.

## Testing

`Dino FloraLab` needs no GPU:

- `sheet --plants a,b --wide N`: real plants with real cards, from the side and from above. Judges shape in seconds.
- `cards`: every card there is.
- `check`: every plant at many seeds is sound, the same plant twice, within budget, and growing somewhere.
- `variety`: two plants of a species really are different, by how little their silhouettes overlap. Fails on a clone.
- `bend`: every plant bent as hard as it goes. The numbers mean something: trees come to 28% at the very limit.
- `find`: a seed and an archetype for each biome.
- `list`: what each costs.

**The lab's picture is for shape.** Its light is roughly `lightingCalc.glsl` and much brighter: judge colour and how anything sits in the world from the game. `--biome id` starts in the thickest wood of a biome, and `--gardengap 17` with `--garden` makes an arboretum, for looking at trees from the game's own camera.

After any change here run `Dino FloraLab check`, `variety` and `bend`, then `Dino WorldLab check` and `Dino WorldLab wind`.

## Where it stands

Measured on a 7900 XTX at 3440 by 1369, seed 1 of the isle, two rounds of alternating builds at each place, old plants then these:

| | Pinewood | Meadow | Birch wood |
|---|---|---|---|
| Frame rate | 106 to 123 | 97 to 100 | 99 to 116 |
| Worst frame | 14.3 to 13.2 ms | 15.8 to 15.3 ms | 16.0 to 13.9 ms |
| Model geometry | 1.86 to 1.09 ms | 1.44 to 0.64 ms | 1.92 to 0.64 ms |
| Model shadows | 0.61 to 0.35 ms | 0.57 to 0.13 ms | 0.64 to 0.24 ms |
| Triangles drawn | 830,000 to 498,000 | 912,000 to 229,000 | 1,343,000 to 449,000 |
| Shadow triangles | 1,736,000 to 1,038,000 | 1,573,000 to 305,000 | 2,164,000 to 685,000 |
| Draws | 15 to 62 | 15 to 72 | 6 to 50 |

A world has 239 models, where it had 66. Growing them takes a world from 1.4 s to 1.9 s to make, off the main thread, and the upload on the main thread went from 1.8 s to 1.7 s. Draws went up with the models, and that's what they buy: nothing in a wood is a copy of what's next to it.

What it costs is memory: about 700 MB of VRAM for cards, and a peak of about 2.4 GB of RAM where it was 1.3 GB, as all the cards wait as bytes until the materials are done. The heap in play is the same as it was, about 1 GB.

**Nobody has seen any of this move, or played in it.** Everything was judged from stills, from a program looking at stills. In particular:

- How crowns move in wind. Joints are checked by `bend`, not by eye, and cards are bigger or smaller than the plants that were tuned against.
- Palettes are tuned to a few places at a few hours. Autumn golds were washing out until they came down by a third, and there may be more of that under a harder sun.
- The bark materials are new and were only seen from the game camera and a low look.
- `Wisps` motes in the Blackwood, and how often anyone finds a biome: they're on the end of each archetype's list, as adding an archetype would make every unnamed seed a different world.
- Saved lands moved. Six new biomes shifted every weight in `DrawBiomes`, so 50 of 60 seeds drew other biomes, and their terrain, sites and roads moved with them. A seed keeps its archetype and name, and its kinds of site stay in order (59 of 60), so what a save marks by number still means something. What it holds by place goes through `TryFindGround`, and `Dino NetLab build` holds that.
