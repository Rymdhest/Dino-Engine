# Shores

Where the sea meets the land: the waves running in, breaking and running up the beach as a thin sheet that slides back, the sand it leaves glassy, then dark, then damp, then dry, and the foam on all of it.

Status: rebuilt 2026-11-14, judged from stills and films of hidden runs, and measured walking (the shore field's stability). **Nobody has walked a beach in it yet.**

## The one idea

**The beach is ground, and the ground draws it.** The swash, the wet sand and the foam on it are worked out in the geometry pass by whatever draws the ground (`Terrain.frag`) and whatever stands on it (`Model.frag`), from the same model of the waves the sea pass uses, so they're lit as the ground is, reflect what's round them through the screen-space reflections, and never touch anything but the ground. The sea pass (`Water_Ocean.frag`) only draws the sea's own surface, and leaves every pixel where the ground comes first.

```
  WaterSystem: the shore field's square, fixed to the world        (CPU, every frame)
        |  WaterRenderCommand
  WaterRenderer.SimulationPass: build the field if the terrain changed, ease what's shown towards it,
        |                       write ShoreData (UBO 15) for everything this frame draws
  geometry pass  ->  Terrain.frag: ShoreGround (swash, wet sand, foam, glints)
                     Model.frag:   ShoreWet (what stands where the sea reaches gets wet)
  water pass     ->  Water_Ocean.frag: the sea's surface and its foam, where the ground isn't first
```

The shaders: `Water/ShoreData.glsl` is the model (the field, the coast, the waves, the swash), `Water/ShoreWet.glsl` what water does to a surface, `Water/ShoreFoam.glsl` the foam, and `Water/ShoreGround.glsl` the beach as the ground draws it.

## The shore field

`WaterShoreField` works out, on the GPU, a signed distance to the shoreline (+ at sea), its gradient, and how open the water is, over a square round the camera: a capture of the terrain mesh from above, sea and land seeds, a jump flood, a blur, and the openness as the share of known sea among the known ground within `OpennessRadius` (a Gaussian on the field's own 8x8 mip, so a fixed reach in the world).

- **It doesn't move under the world.** `WaterSystem.ChooseShoreSquare` puts it on a grid fixed to the world in steps of `WaterShoreField.MoveTexels` (128 texels, 32 m in a land), its texels are always the same size (192 m over 768 texels in a land, so 0.25 m, exact in a float), and it only moves when what's loaded no longer fits in it. So a build finds every texel where the last one had it, and only what really changed can change: ground streaming in or out at the far edge of the loaded square. The old field was laid over whatever chunks happened to be loaded, so every crossing of an 8 m chunk shifted its texels by a fraction of one and its openness mips with them, and the waves' phase and the swash's reach jumped: the stutter people saw walking along a beach.
- **What's shown is eased towards each build** over `SettleSeconds` (`Water_Shore_Blend.frag`), at the same place in the world when the square has moved, so even what does change never jumps.
- Builds wait while chunks are still streaming in (a frame without a change, or `MostFramesStale` frames at most), as each build would only be eased into the next.
- `--render shorecheck=1` reads every build back and logs how much it changed from the one before within 40 m of the middle of the view. Walking 110 m along the coast of seed 303's isle it was 0.0000 m in 15 builds of 17, and 8 mm and 3 cm in the other two, with openness moving by a thousandth at most.
- The terrain capture is the same square's, and is what the sea, the ground marks and the waterline read the ground's height from (`WaterRenderer.TerrainHeightTexture`).

## The water's edge

There's no line where the sea ends: the sea and the sheet on the sand hand over across `SHORE_SHALLOWS` (0.3 m) of water, by one measure both read at the same depth (`shoreDrowned`, the sea from the terrain capture and the ground from its own mesh, over the same waves). As the water deepens the sea pass fades in, and with it its colour, reflections, glints, caustics and foam, while the ground goes from the sheet's look (dark, a mirror, its ripples and caustics, the swash's foam) to the sea bed's (paler and rough, as the sea pass now draws the surface over it). So the dark glassy sheet runs on into the shallows and the shallows' colour comes in as they deepen. Round anything standing in the water the sea still fades in over the scene's first few centimetres (`WaterRenderer.OceanEdgeFade`).

The sea used to draw a ring of foam where it met the sand, at full strength right up to the ground's edge, and the ground under the shallowest water went from the sheet's look to the sea bed's in 10 cm: together a hard white line round every shore, which a person saw. Seaward of the shoreline the swash's reach goes on below nothing (`shoreSwash`'s `inland` is signed), as with it held at the shoreline the whole of the shallows was the water's edge at the end of every wave.

## The waves

`shoreWaveAt`: waves run in towards the nearest land along the field, shoal (Green's law), break once they're 0.78 times as high as the water is deep, and come in sets of `WaterWaves.SetLength`. Every point a pixel's ray tries on its way down to the surface asks about itself (field, depth from the capture, the coast's noise), so the surface is the world's and not the pixel's: taken once a pixel from where its quad was, a crest swam as the camera moved. The walk starts where the ray meets the calm sea, which is near the surface everywhere, and takes `SURFACE_STEPS`.

The phase counts on over the sets (`WaterSingleton.ShoreSets`, wrapping at `WaterWaves.SetsCounted`), so every wave of a session has random numbers of its own (`shoreWaveRandom`).

## The swash

`shoreSwash` looks back over the last `SHORE_LOOKBACK` waves, newest first. Each runs up as far as Hunt's formula says for its own size, its set's and one of its own (`SHORE_WAVE_SPREAD`), capped at `WATER_RUNUP_VERTICAL` (2) wave heights up a steep beach, and wanders along the shore in lobes of its own (`SHORE_LOBES`, a mix of four smooth noises a few metres and a stride across), so the edge of the wet sand is the ragged edge of the last few waves and never a line. The climb is fast and slows to a stop in the first quarter of a wave's cycle, then the water slides back.

From that, for any point: whether a sheet is over it now and how deep, how near its edge it is (anti-aliased across a pixel), how long ago the water last ran off it, how much foam it left, and how far the water has carried what floats on it up the beach (`sheetFlow`), which is what the foam on the sheet and the ripples it drags move with.

## Wet sand

`shoreWetness` and `shoreWetten`, in the geometry pass, after the weather (`Weather/Wetness.glsl`), never lighter than the rain left it:

- **The sheet**: smooth as water (`SHORE_SHEET_ROUGHNESS`), the sand's ripples drowned, shallow ripples dragged with the flow, and on the ground the caustics its ripples focus onto the sand under it, moving with the water. Both read the water's pattern (`shoreNoise`), which tiles: each layer is read turned off the world's axes and off the other, and the pattern's smooth noises (b and a) are gradient noise with every octave's lattice shifted (`WaterTextureBaker.SmoothNoise`). Value noise with its octaves on one lattice, read square to the world, laid the sun's highlight on the sheet in a perfect grid, which a person saw.
- **The film** the water leaves as it runs off: glassy, draining off the grains in `FilmTime` and out of the material's hollows last, where they hold it, more in some patches of a beach than others (`weatherPooling`). The hollows are the relief no finer than 30 cm (`SETTLE_METRES`), so the last of it lies in the beach's broad dips, its edge a smooth line: read at the finest relief it lay in the same ripple troughs of every tile, perfect chevrons across the whole beach (WEATHER.md, Wet surfaces). It darkens the sand further.
- **Soaked**: dark and richer in colour, satin (`SHORE_SOAKED_ROUGHNESS`), drying over `DryingTime`. The edge of a wave's reach, where its water was thinnest, takes less in.
- **The seep**: low on the beach the sand's own water comes out (`SHORE_SEEP`), and its hollows stay full.
- **Damp**: what the seas of the last few hours reached (the mean wave's need from `SHORE_DAMP_FROM` to `SHORE_DAMP_TO`), and the capillary fringe, as fine sand draws the sea up through it to `SHORE_FRINGE_TOP` above the sea within `SHORE_FRINGE_REACH` of the shoreline, however far the waves come. That's what gives a beach whose sand stands above the waves (the Haven's) a soft gradient from wet to dry.
- **Under the sea**: the sheet's look under the shallowest of it, going over to the sea bed's as it deepens (`shoreDrowned`, above): rough, as the sea pass draws the wave's surface over it and nothing of the ground's shine may show through, and only a little darker than dry (0.15), as the sea pass darkens and tints what it sees through the water on the way down and back, and pale sand under the water is what makes the shallows glow. The sea pass doesn't darken what's under the sea again (`bedDarkening` is 0 for the sea; still water keeps it, as its bed isn't wetted).
- **Glints**: freshly wet sand glitters (`shoreGlitter`): a few tiny facets on fixed spots, tipped up to `SHORE_GLINT_TILT`, which the light pass only lights while one is nearly a mirror between the sun and the eye, as snow's crystals are. So they twinkle as the camera moves, wherever the sun is, and go out as the film drains. Lower `SHORE_GLINT_SHARE` if they're too much.

From this camera, 67 degrees down, a flat wet beach can only mirror the sky near the zenith, so it reads as darker and richer rather than shiny, and shines when the sun is in front (about half of lands, as a land's sun is anywhere round the sky, and the Haven in the afternoon): the broad sheen of soaked sand and the glints are what carry it otherwise. `WATER_SHEEN` (`Lighting/lightingCalc.glsl`) is what any surface as smooth as water gets, and the beach uses it as puddles do.

Models standing in the swash (rocks, posts, wrecks, the pebbles) get wet the same way (`Model.frag`), but for the capillary fringe, which is the sand's. What moves would slide through a wetness that lies still in the world, so characters and wildlife are only wet where the water touches them now.

## Foam

One texture, grown at start (`WaterTextureBaker.BakeFoam`, tile `WaterRenderer.ShoreFoamTile`): **r** a lace of strands round holes of every size, wandering at two scales, each strand its own width and some broken; **g** bubbles of two sizes; **b** broad patches; **a** streaks, long along u, which nothing uses now: foam is never drawn out along the way the water runs, as that turns along a curved shore and a pattern turned by it about the world's far-off origin was sheared into hatching. `shoreFoamAt` turns an amount into cover: a little shows the strongest strands and their bubbles, more fills in round them, a lot is a sheet with holes where its patches thin. Thin foam is read a little softer, or its last bubbles sparkle as single pixels. It has a body (`SHORE_FOAM_RELIEF`) the light falls across, and the sea lights it from the sky and from the shore round it (`foamSkyLight`, `foamGroundLight`), or in shade it went the sky's blue.

- On the sea (`seaFoam`), in layers: the roller, the white water a breaking crest spills down its own face, from the crest part way to the foot, its toe reaching further down in some places than others and heaped in lumps along its length, running in with its wave and thinning fast behind the crest, where what it leaves takes over: that stays where it fell, a pattern of its own every wave, bursting over most of a wave's cycle. What lingers in the surf and the whitecaps drift with the wind. Taken at the crest alone the white water was a line a few centimetres wide along every wave. Each wave breaks in sections of its own along its length (a noise 19 m across, in the wave's own frame), some white and some hardly at all, and small waves in sheltered water churn up less of it: on a steep shore, where the water deepens within a stride, the surf is a narrow strip along the shore, and breaking all along it at once ringed the shore in white. Towards the beach it thins out into lace as the swash's comes in, by the measure the water's edge goes by.
- The roller's pattern is carried a wavelength over each wave's cycle from the foot of its face (`through`), where it starts as a pattern of its own with none of it showing, so it moves with the crest at half its speed, as foam does in a spilling breaker. Carried from the shoreline, as it was, it was sheared wherever the shore curves by as far as the crest was out.
- On the beach (`shoreSwashFoam`): the bore's front, heaped in lumps and thinner as it runs back; lace on the sheet thinning as it bursts (`SHORE_SHEET_FOAM`), carried with the water; and what's still there as the water runs off, left on the sand (`SHORE_FOAM_LEFT`), most of it in a line where each wave stopped, as the water there was there least long. Each wave's foam is a pattern of its own.
- The beach's foam thins out under the sea as the sea's own comes in on its surface, so neither ends in a line.

## What's on a beach

- There's no wrack along the top of a beach. A line of weed, sticks and shells drawn on the ground (`ShoreGround.glsl`) read from the game's camera as a dotted line of dark stains along every shore, on sand and on snow and ice alike, and a person called it a glitch three times, so it was taken out. Don't put it back as flat paint on the ground.
- Nothing grows where the swash runs: `WorldShore.Swashed` (headless) is how much of a point the waves of an ordinary day run over, by the sea only, and `TerrainSurface` keeps grass off it and `WorldGroundCover` plants (the vista, behind the main menu, is left as composed). What grows under the water is the sea floor's (`SEA.md`). On a sand, gravel or shingle beach the ground cover scatters rolled pebbles there instead, in drifts.
- The sand itself (`MaterialRecipes.Sand`): ripples running a little across the tile, bent, in patches with sand planed smooth between them; broad soft swells under everything; the dark grains in the troughs; and the odd shell or pebble. Its albedo is under half, or a beach in the sun goes white.

## Tuning

`WaterSingleton` (F3, Render, Water): `ShoreWaveHeight`, `ShoreWavePeriod`, `ShoreWaveLength`, `RunUp`, `SetVariation`, `Foam`, `WetSandDarkening`, `DryingTime`, `FilmTime`. The constants of the model are at the top of `Water/ShoreData.glsl`, and those marked so mirror `WaterWaves` (`WATER_RUNUP_VERTICAL` is `MaxRunUpHeight`): change both together.

## Looking and checking

- `--render shoredebug=1`: red how wet, green the film, blue under water. `2`: how far up the mean wave's run-up a point is (red ramps 0 to 1 and again), green past it, blue seconds since the water left. `3`: red the waves' height of the most they'd be, green how open the water is, blue metres inland over 10. `4`: metres inland and height as ramps, and the mean wave's need, everywhere the field covers. `5`: the sea's foam green and the beach's magenta, for telling them apart. `6`: red how much of the water is the sea's to draw (`shoreDrowned`), green its foam, blue the depth over the terrain's mesh, of a metre. `7`: how fast the water under the waves runs along the bed (`shoreSurgeAt`, what sways what grows under it, `SEA.md`), red shorewards and blue back out.
- `--render shorecheck=1`: logs each build's change near the view (above), which should stay at nothing.
- Beaches to look at: `--seed 303 --archetype isle --at 200,712` (a spit by the wreck, a steep shore), `--seed 12 --archetype isle --at 832,600` (an open beach by the landing, the sun in front), and the Haven's west coast `--haven true --at 62,200 --hour 17`, lit from in front. Add `--calm true --nolife true` to look without anything moving in the way. Judge the swash moving (`--film`), and the field's stability walking (`--bot true --walk x,z` with `shorecheck`).
- Cost: at a beach filling the view at 3440 x 1369, the ground's geometry pass costs about 0.26 ms more than before and the plants no longer growing in the swash about 0.1 to 0.35 ms less. Inland nothing changes. Ground under the sea deeper than the troughs of the biggest waves skips everything.

After any change here run `Dino WorldLab check` (and `--archetype isle`, and the Haven), look at a beach standing and walking, and `--perf` at one against the build before.
