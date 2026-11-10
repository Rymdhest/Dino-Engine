# Waygates

The old roads: every land's waygates, and the Haven's great one, which is the main stage of the village. A waygate is a frame of old stone, and what stands in it once it's open is **the way**: a throat of light bored into the gate, streaming down to the land it goes to, which shows at its far end under its own sky. Read this before changing a waygate's frame, what's seen in one, its lights or particles, or anything that names one.

Status: built 2026-11-10 and judged from stills and a film of hidden runs. **Nobody has watched a way move**, and motion is most of what it is. See "Where it stands" at the end.

## The one idea

**One description, two meshes, so they can't disagree.** A gate is a `WaygateDesign` (`Modelling/Procedural/Architecture/Waygates/`, headless): its opening as exact numbers (a jamb either side up from the sill to the springing, under a pointed arch of two arcs centred a little past the middle), its stones, its plinth, its rune slots and its lights, in its own frame with the way's face at z = 0 and its front towards +z. `WaygateForge.Frame` builds the frame the world stands (`waygate`, `waygate.great`, through `PropGenerator`), and `WaygateForge.Way` builds what the game stands in it: the way's face and the light of every rune. The way's face is the opening grown by `WaygateDesign.Tuck` (12 cm) all the way round, so its edge runs into the stone everywhere and there's nowhere for a gap to be.

```
 WaygateDesign ──> WaygateForge.Frame ──> the frame: a world object, like anything built
       │
       └─────────> WaygateForge.Way ───> WayMesh ──> WayRenderer: geometry pass, then light pass
                                                          ▲
 the land it goes to ──> WorldPlan ──> WayLook ──> WayComponent ──> WaySystem (lights, particles, gust, opening)
```

## The frame

**Dressed stone laid as stone is laid.** Every stone is a block of its own with its arrises taken off (`Masonry.Block`): each face drawn in from its edges, each edge a narrow bevel and each corner a small facet, so blocks laid face to face meet in a V that reads as a joint from the camera, and nothing anyone can see lies in the same plane as anything else, which is what z-fights. Faces that touch inside a wall are hidden by the bevels round them. A land's gate is a plinth of slabs, a jamb either side of coursed blocks on a base under an impost, and a ring of wedge-shaped stones on radial joints round a lifted keystone. The Haven's is the same road raised higher and kept better: three steps, piers with a column half sunk in each face, an arch of two rings, spandrels in courses that meet the outer ring wherever they're cut by it, a cornice, an attic, a pedestal, and a crystal held up on four claws, between stepped wings with a lamp on each.

- **Stones dip into what they stand on.** An outer ring dips 3.5 cm into the one under it and a spandrel 6 cm into the ring, as each is flat between its own joints and the two would otherwise leave slivers of daylight where they don't line up. A slab is the whole depth of its tier, so no joint ever runs along the way's face.
- **Runes are where the camera can read them.** The camera looks down at 67°, so an upright face shows at a third of its height and a tread at nearly all of it: there's a rune on each face of every stone of the inner ring, and lines of them along the treads, every other slab along the long outer ones. The script (`Glyphs`) is rings broken somewhere, with a dot, a smaller arc or one short stroke, so no letter can be a zigzag, a cross, an arrow or anything turned about on itself, which is what keeps any of them from looking like a real rune or mark (`LORE.md`). In the frame each is a dark inlay (`WaygateDesign.InlayLift`, 8 mm proud) in a land's gate and a cold glow in the Haven's, which is never quite dark; the way's runes stand 8 mm proud of that (`GlowLift`), far enough that the depth buffer never mistakes one for the other at any distance the camera goes.
- **The Haven's stone is laid as its paving, and its square is built of it.** The court round the great gate, the avenue below its terrace and the square are the gates' own stone in slabs (`GroundPaint.Paving`, painted by `HavenSquare`), and the terrace, its flight, the beds, the arch and everything else of stone in the square are the gate's stone too (`PlazaStone`, `SQUARE.md`). The paving is a terrain material, `MaterialCatalog.Flagstones`, whose faces are the rock the gates' blocks are faced with, at its own size, in the Haven gate's colour, with the same chamfer to every joint, so the steps run on into the paving as one stone and it follows every rise of the ground as no block could. Cobbles are left round the square's beds and at the halls' doors (`TEXTURES.md`).
- **The Haven's is wider, not much taller.** The camera sees 60° top to bottom from high in the south, so from where a hero arrives (9 m in front, at the head of the flight up to its terrace) and uses it (6 m) the whole way and its cornice are in view; its crown and crystal are for the village, seen from further off. Its footprint is `StructureCatalog.GreatWaygate`, which is its steps' size. A land's footprint never changed, so no world moved with this.

## The way

**Two passes** (`WayRenderer`). In the geometry pass the way is an opaque surface that takes no light at all: black, wholly occluded and never in the sun, so nothing lights it like paint, and depth for everything after it, from either side, so it hides what's behind it and fog, depth of field and particles all know it's there. Its light comes after the light passes, as radiance straight into the multisampled scene on the same depth, before the reflections, so wet ground shows it, and in floats, as the G-buffer's eight bits of emission band anything so smooth. Both passes run the same vertex shader, `invariant`. It's drawn from either side and never thinned by the focus cutout: from behind it's the same throat, going the other way.

**The throat** (`Shaders/Way/Way.frag`) is the opening carried back along an axis and drawn in towards its heart, down to a far end a third of its size (`END_SHARE`), so its walls go on from the stone with no seam. Each ray is followed in oblique coordinates, where it would cross the face and how deep it is, in which the walls are the opening's own edge and a ray's way out of them is a straight line, so where it strikes the wall is a march through the opening's distance field in two dimensions (`wayExit`). That's real parallax: walk past a way and its throat turns with you. Its axis is mostly the way to the way and partly the camera's forward (`WaySystem.Parallax`, 0.3): all of the camera's, and a way at the top of the screen, looked into 17° shallower than one in the middle, had its far end slide off the bottom of the opening.

- **The walls**: a few broad streams spiralling down, each of fine threads, with dark between them, pouring down and turning as they go, stardust carried with them, brightest where they leave the stone and darkening down the throat towards the light at its end, which is what makes it read as deep.
- **The far end**: the land itself, as far again past the end as the throat is deep and square to the throat, so it stands up to whoever looks down it: its sky warm along the horizon and deepening overhead, its sun at its hour, three ridges in the colours of its ground fading into its air, snow if it's cold, and the sea with the sun's path on it if it stands in one. Where the end meets the walls it's exactly as dark as they are there, so it has no edge, and the light at the end is round, the land seen through a soft glow in its middle: lit to the arch's own shape, a bright end read from the game's camera as a pale stone standing in the way.
- **The face**: the edge burning where it meets the stone and crackling along it, ripples running out from the heart that bend what's seen through it, glowing mist down the throat, and a sheen where it's seen edge on.

Everything there is in the gate's frame, and `Way/WayShape.glsl`'s `wayDistance` is `WaygateDesign.Distance`: change them together.

## What's in a way

**A way is grown from the land it goes to** (`WayLook.Of`, from that land's `WorldPlan` and a seed). The streams and the edge are its first biome's colour, made vivid; some streams its second's; the walls between them that colour and its sky's; the far end its mood's sky and sun, its ridges its biomes' ground, sea by its landform, peaks by its mountains, steps by its terraces and snow by its coldest biome. How the throat moves comes from `Genes`, each number by a name of its own: how many streams, how they twist, which way it turns and how fast, how fast it pours, how fine its threads are, how much mist, and how long its breath is. The higher a land's level the more its throat churns. What drifts in a land's air comes through after the way's own motes: petals, leaves, snow, embers, fireflies, dust, pollen or wisps.

- **Colour has rules.** A pale biome is pushed out until it's a colour at all, the way it already leans (`WayLook.Vivid`, `LeastChroma`), so a snowfield's way is ice and not white; and a deep red gets enough orange in it to be fire and leaves (`Warm`), as blood-red light reads as the Reich's oxblood. Light that's near white goes white once it's bright, so nothing in a way is pale.
- **Brightness has rules.** Sunlit ground is about 1.2 radiance: the throat glows a little brighter than that, so it keeps its colour once mapped instead of going white, and only the edge, the light flooding in as it opens and the far sun bloom (the constants at the top of `Way.frag`).
- **Names** (`Gameplay/WayKeys.cs`). The Haven's way is `portal:seed:archetype:level`, the request that grows that land; a land's gates go home, so each shows the Haven, as `waygate:lit:seed`, the gate's own seed from where it stands in which land, so no two turn alike. A client grows the look from the name with nothing else sent (`CharacterFactory.WayOf`), and a name it can't read still opens, as the way home.

## Opening, light and air

`WaySystem` runs every way. The first time it sees one it opens it, on one clock both passes and its lights read (`Way/WayShape.glsl`): the runes light one after another from the treads to the keystone, the edge kindles from the sill up both sides to the point, the light floods in from the edge and meets in the middle with a flash, a burst of motes and a gust out through the grass (`Wind.AddBlast`, too weak to break anything), and the throat opens up behind it. A land's gate opens when it's first lit, and the Haven's when the way is opened at the map table; a site's way is made again whenever a hero comes within 100 m of it, which is far off screen, so it's only ever seen opening when it really does.

- **Its lights are its own**: one in front, which may hold a shadow map, and one behind, both given to global illumination as every light is, coloured as the way softened towards white so they tint what they fall on rather than paint it, and breathing with it (`WayLook.Breathing`, which `Way.frag` reads too). Each is tagged with its way (`WayLightComponent`), so the system never takes a light that isn't its own once ids are given out again, and they go the frame after the way does.
- **Its particles** are `Particles/Content/Ways.cs`, filling the way as an upright face (`EffectShape.Across`, an `IEffectFace`, which turns with the effect's Forward): motes out of both faces and glints round the edge (`Shimmer`), the burst as it opens (`Kindle`), and what comes through from the other side, tinted by the run where it has no colour of its own.

## Rules

- **The way and the frame come from the same design.** Change the opening in `WaygateDesign` and both follow; never draw either by hand.
- **Nothing visible is coplanar with anything else.** Inlays and their light stand proud by `InlayLift` and `GlowLift`; stones meet in bevels; a slab is its tier's whole depth.
- **With the sun, sky, lights and emission off, the frame is black**, open ways included: their light is scaled by the emission switch like any glow.
- **A way reads from the game's camera**, 33 m away and 67° up, where it's a few hundred pixels across and a third as tall as it is wide: judge it there and at the closest zoom (55%), never only close up.
- **What moves with the way keeps the wind's rules**: speeds are a way's own and never change, so nothing in it is a clock times a live speed.

After any change here run `Dino WorldLab check --seed 1610 --count 1 --archetype haven` and a land check, `Dino FxLab check` (the way's effects are measured filling the Haven's way, under a budget of their own), `Dino NetLab lands` and `game`, and the black test, and look in the game: `--haven true --openway 1 --at 211.3,158.6` stands a hero at the Haven's way (`--look 16.6 --angle 67` is the closest zoom, and `--film` its opening), and `--site FacingWaygate` stands one in front of whichever of a land's gates the camera sees most squarely. Measure with `--perf` at the Haven's way, open and shut.

## What it costs

At 3440x1369 on a Radeon RX 7900 XTX, standing at the Haven's way, one run each: 8.42 ms a frame shut and 8.76 ms open (114 FPS). The way's own light pass is 0.025 ms and its geometry pass doesn't make the report; its two lights are 0.13 ms in the point-light pass, global illumination's voxel light 0.03 ms more for them, and its particles 0.01 ms: about 90 motes and glints at once in the Haven, and up to 60 more of what comes through, fewer in a land.

## Where it stands

Built on 2026-11-10. Looked at in stills of hidden runs, from the game's camera, its closest zoom and low looks, in the Haven with ways to an isle, a snowfield, a forest, badlands, a fen and an ash land, and at land gates face on, side on and from behind, and in a film of the Haven's opening. **Nobody has watched a way move or walked past one**, and its parallax, its streams and its breathing are all motion: walk past one, and watch one open, before tuning anything.
