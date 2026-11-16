# Churchyards

What a churchyard is, how one is laid out, and how its church, its graves and everything else in it are grown. Read `LORE.md` for what it means (the Ossuary digs here, and it's always night), `FENCES.md` for its wall, and `TEXTURES.md` before changing a material in it. It's meant as the standard a point of interest is held to: a place that tells something, laid out the way such places really are, and never the same twice.

Status: rebuilt on 2026-11-14, replacing a fixed church in a ring of fence with a grid of identical graves. Checked by machine and looked at in stills of the game by day and by night. **Nobody has walked through one.** See "Where it stands" at the end.

## The one idea

**A churchyard is a church in the middle of plots of graves, inside a wall, and every plot says something.** The church stands a little north of the middle, turned so its south side and its porch face the camera. Gravel paths run from each gate in to the church, round it, and on out to the wall, and cut the ground into square plots. The oldest graves are nearest the church, the newest out by the wall, and the north side, where nobody wanted to lie, is the poor's. A few plots are given to something else: a family's railed plot, a row of chest tombs, a mausoleum against the wall, an old tree, and the Ossuary's dig, with the graves round it opened. Lanterns light the paths, candles are left on the graves someone still visits, and the dead walk the paths.

Nothing is stored. Every piece is an id grown on any thread (`yard:stone:1f3a9c:1:2:40`), and the layout draws each decision from a fork of the site's own.

```
  SettlementStage            ChurchyardBuilder.PlanOf           ChurchyardBuilder.Build              WorldAssetLibrary.Grow
  a graveyard site   ---->   its wall's shape, fitted    ---->  church, gates, wall, paths,  ---->   YardForge.Make, on every core
                             to the land and the roads          plots and their stories,             (ChurchForge, Graves, YardPieces)
                             (roads ending here stop            lanterns, candles, trees,
                              at its wall)                      the dead
```

## The yard

`YardPlan` (headless, on `WorldSite.Yard`) is all the game and the checks need of one: a rectangle with its corners cut off, turned by its `Yaw`, its local u running along the church from tower to altar and v towards its porch. `PlanOf` works it out before roads are reserved, from the same fork the builder will have, so a road ending there stops at its wall (`EndRoadsAtWall`). It starts at about the site's radius by four fifths of it, slides up to 12 m off whatever's bad (water, a slope over 0.4, ground nobody can reach, and roads that pass by, as a road along a wall takes the wall), and only then pulls each side in until the strip under it is good, never below 58 by 42 m. Roads that don't end there go round: a churchyard's core costs 25 times as much to cross (`RoadStage.KeepCore`, out to 1.32 of its radius, past its corners).

The site is bigger than it was (`SiteCatalog.Graveyard`: 36 to 45 m, 40 m from its neighbours), and about as many lands have one as before (25 in 30).

**The church** is a design that fits: up to 48 seeds are tried for one whose bounds leave graves at both ends (11 m), behind it (10.5 m) and in front (16 m), and the nearest is taken if none does. It's set so what's spare behind it is a share of what's spare in all, and in a tight yard only the walk round the wall, so the south band, which the camera sees, gets the most. A yard too tight even for that still has its church, anywhere inside the walk. Its parts are solid as boxes that don't overlap (`ChurchDesign.Solids`), and the ground is levelled under all of it to one floor.

**Gates**: one where the road comes, one in front of the porch (a second on the south side if the road comes there too but more than 16 m along), and now and then one more, two at least and four at most, each where it leads straight in to the church: the west gate to the tower's door, the east to the chancel. The main gate is a lychgate half the time. A gate that won't stand where it's wanted slides along its side, and if two can't be had, any side is tried. The wall (`FenceUse.Churchyard`, `FENCES.md`) runs between the cut corners and the gates' piers, drawn into them (`Tuck`), and stops where a road crosses it.

**Paths**: gravel all round the church (its apron), its edges carried on out to the walk inside the wall, each gate's path straight in, and the bands round the church cut into plots of 15 to 19 m by 14.5 to 18.5 m, at the lines they have to have (the gates', the apron's) and evenly between. A path is laid four metres at a time and only on ground that can be walked, and is kept clear of anything built (`Keepout`). The walk inside the wall is turf.

**Plots**: rows of graves facing south, as the camera looks, in pairs back to back (`BackToBack`, 0.7 m) with a lane between one pair and the next (`Lane`, 2.6 m), so every grave can be walked up to from one side. A row shares a favourite stone, as a row was cut by one mason in one year. Each era of a churchyard has six to eight kinds of stone (form, dressing, age, seed) and a grave picks one, so a churchyard has about two dozen stone models however many graves it has. A kerb or a slab is only laid where the ground along the grave is near enough level (`GraveSlope`), as a model only turns about y, and otherwise the grave is a stone in the turf.

| Era | Where | Stones | What's in front |
|---|---|---|---|
| Old | Nearest the church | Slabs, rounded tablets, shouldered, broken, a few pointed: limestone and sandstone | Turf, mounds, fallen-in kerbs |
| Middle | Further out | Pointed, rounded, crosses, ringed crosses, shouldered, now and then an obelisk: slate, sandstone, granite | Mounds, kerbs, ledgers, turf |
| New | Out by the wall | Pointed, crosses, open books, rounded, shouldered: marble and granite | Kerbs of pale chippings with flowers, ledgers |
| North | Behind the church, mostly | Oak crosses gone grey, slabs, broken stones | Turf, mounds |

**Stories**, taken from the plots before the rest are rows:

- **The Ossuary's dig** (one, where the camera can see it): the dig from `SettlementStage` (its pit, hoist and the cloth its finds are laid on to be counted), two or three graves opened (`YardKind.Dug`: the pit going dark, the spoil heaped, the coffin's lid thrown up, the stone pulled over, a spade left in the heap) and the plot's other stones broken.
- **Mausoleums** (one to three, against the wall, the north side first, door to the yard): classical (columns, pediment, urns) or gothic (steep stone roof, pinnacles, rose window), a third of them broken open with the Ossuary's cold light inside, and the family's graves in front.
- **A family's plot** (one or two): an iron railing on a kerb with its gate standing open, an obelisk or a ringed cross on a ledger at its head, the family in kerbed graves in front of it.
- **Chest tombs** (one or two plots): a row of them, panelled, on columns or coped, some with their lids slid off.
- **An old tree** (one to three): at a corner of the plot, the graves kept off its trunk.

Then **the cross**, on its calvary of three steps beside the path to the porch; **lanterns** down that path, at the apron's corners, at the inner end of every other gate's path and where paths cross among the plots, the south's first (twelve at most, two models); **candles** on the graves someone still comes to, at the foot of the stone where the grave's turf lets them stand, so the stone is lit from in front (sixteen at most, spread out, a few models), a bank of them at the porch and some at the cross; a bench or two against the church; **trees** by the land (cypresses where it's warm, dead pines and silver snags where it's cold, ancient oaks with a snag or two elsewhere) and more along the walk inside the wall; fallen leaves under them and pale fungus glowing among the oldest graves; and the yard's **turf** (`GroundPaint.Sward`, `TerrainSurface.Mow`): short enough that the stones show, dark, cool and in no flower, longer in drifts where the scythe doesn't reach.

**The dead** stand on the paths, more on the south side, seven metres apart and never by a gate, eight to eleven of them, and the pack stage's reinforcements are scattered inside the wall (`PackStage.Reinforce`). A spawn the repair has to move is moved within the wall, or dropped (`FinalizeStage.RepairSpawns`). They're bones and ghosts, the Ossuary's raising and the land's own dead, never what rots, which is dumped in the wet, not buried (`Warbands`, 45% ghosts). The chest of what they took is by the porch.

## The church

`ChurchDesign.Of(seed)` (headless) is the plan that the layout and the forge both read. Its nave runs along x, the tower (or bellcote) at -x, its west end, and the chancel at +x, the east, as a church is oriented; its south side, with the porch, faces +z. Every number is at the world's scale, twice life, and from a fork of its own by name.

| What | How it varies |
|---|---|
| Tower | A spire inside a parapet with pinnacles; a broach spire straight off the tower; battlements with tall pinnacles and gargoyles; or no tower and a bellcote of two bells on the west gable. Always well clear of the nave's ridge, with diagonal buttresses, a west door of three orders, a window over it, belfry openings with louvres on every face, and half the time a stopped clock |
| Nave | Three to six bays; aisles with lean-to roofs and a clerestory over them on the bigger; plain eaves or a parapet with pinnacles on its buttresses |
| Windows | Pairs of lancets, two lights with a circle over them, or three lights with three, in long-and-short jambs and voussoirs under a hood mould, the glass in quarries with lead between, a border, a figure and highlights in one of four palettes, blue leading in every one, as oxblood and amber are the Reich's cathedral's |
| Chancel | Two or three bays, narrower and lower, a priest's door with its lantern in the south side, and a flat east end with a great window or a half-octagon apse |
| Roof | Pale slate weathered in soft patches and greener towards the eaves, or stone slates of the walls' stone; coped gables with kneelers and crosses |
| Porch | In the first or second bay, its arch open and going dark, two orders round it, a lantern beside it |

The church is the wall's stone (`StoneFences.ChurchyardStone`, passed in its id), as are the gates, the cross and the mausoleums. Windows are lit from inside, a few throw coloured light out onto the ground (`AssetLight.StainedGlass`, six at most), and the south side carries a lantern on every other buttress, as a church at night is seen by its windows and its lanterns.

## Ids

`YardSpec`: `yard:<kind>:<seed>:<numbers>`, whole centimetres, per cent, per mille or choices, no dots and no hash. Each kind's numbers are listed with it in `YardKind`. `YardForge.Make` builds any of them, headless and touching nothing shared, and `WorldAssetLibrary.Grow` grows them on every core while a land loads, the church first. A churchyard's models go when no world uses them, as an arena's do.

## Readable at night

It's always night in a churchyard (`Ambience.NightAt`), with fog on it, and from 33 m up a stone is a few dozen pixels. What was learned getting it to read:

- **A dark roof is a hole in the night.** The slate is a pale silver-grey, as the old church's was, or moonlight never finds it.
- **The moon can be behind everything.** It shines from where the sun is, which in some lands is north, so every face the camera sees is in its own shadow. Stones are pale, and what lights them is warm and near: candles at the foot of the stone, lanterns among the plots.
- **A surface built upside down renders black.** `MeshBuilder.Surface` winds counter-clockwise as u and then v increase, so a surface over the ground runs v towards -z, or it faces down (the mounds did, and every plot looked full of open graves).
- **Glass that glows pale goes white.** Its colours are deep and its strength low.

## Checks

- `Dino WorldLab check`: every churchyard has its church (`HasChurch`), two gates at least that can be walked through, a porch that can be walked to, its dead inside its wall, and graves for its size (one a 90 m² of its plots, 12 at least) (`CheckChurchyard`).
- `Dino WorldLab yard --seed N --count M`: grows every piece of the churchyards of those lands, and fails a piece that builds nothing, has a point that isn't a number or reaches too far, a church over 160,000 triangles or anything else over 12,000, and a churchyard of over 90 models or 60 lights.
- `Dino WorldLab plan --seed N --site Graveyard`: draws one from above.
- The black test, and `--perf` at a churchyard against the build before.

## Costs

Measured on 2026-11-14 at 3440 x 1369 with another copy of the game open: about 1.3 ms of GPU more than the old churchyard (26.0 against 24.8 ms), most of it point lights (2.8 against 1.8 ms), as there are some forty of them, each small. A churchyard is 100 to 145 pieces of 36 to 51 models; a church about 35,000 triangles and 40 ms to grow on one thread.

## Where it stands

**Nobody has walked through a churchyard, fought in one or seen one move.** It was judged from stills by day and by night in two lands. Open:

- **The church from the ground.** Only the game's camera and a few lower looks; a close look may show seams where a roof meets a wall, a cope meets a roof or an apse's facets meet.
- **Fights among the graves.** A grave is walked over, and so is any stone a person could step over or brush through, a tablet, a slab, a book or what's left of one, as a person found they couldn't get about a churchyard for its low stones (`Hitboxes.Of`); a cross is a post, an obelisk, a chest tomb and an opened grave's pit are walked round. None of them stops a blow but the obelisk (`Sight.Above`, a person's hip); mausoleums and the church do. Whether lanes between rows are wide enough to fight in has only been reasoned about.
- **The church hides whoever's behind it**, as any building does, and nothing opens for the focus cutout (`FocusCutout`'s whitelist).
- **Inscriptions** aren't there: a line of lettering is under a pixel from the camera.
- **The undead camps in the wild** use the same stones, tombs and opened graves now, and nothing else about them changed.

## Decided, and open to change

- **Headstones face south, as the camera looks**, not east as real ones do: a stone seen edge on is a line.
- **Rows in pairs, back to back, with a lane between pairs**, so a churchyard is dense and still walkable.
- **The north side is the poor's**, wooden crosses and old slabs, as it was in many parishes.
- **Bones and ghosts only**, 45% ghosts, in a churchyard; the rotting are the wet country's.
- **The iron is rust-brown**, never black, which is the Reich's.
- **The glass leads with blue**, so a parish church is never taken for the Iron Apex.
