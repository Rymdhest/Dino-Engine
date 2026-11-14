# Occlusion: what the sky can't reach

Ambient occlusion is how much of the light from all round a point, the sky and what it bounces off, the things beside it keep from it: the dark in a
crease, at the foot of a wall, under a crate, round a boot on the ground. It's worked out from the screen's depth each frame (`OcclusionRenderer`,
`Shaders/Occlusion/`), and the light passes take it off each surface's own occlusion, the material's, which its relief bakes in (TEXTURES.md). With
global illumination on, which it is by default, it only reaches 0.35 m, as GI has everything further (`GlobalIlluminationSettings.ContactReach`,
`OcclusionRenderer.ReachOf`, GI.md), and 0.9 m without it (`Radius`).

## Ground truth with visibility bitmasks

It's Jimenez, Wu, Pesce and Jarabo's ground truth ambient occlusion ("Practical Real-Time Strategies for Accurate Indirect Occlusion", 2016), with
Therrien, Levesque and Gilet's visibility bitmasks ("Screen Space Indirect Lighting with Visibility Bitmask", 2023), in `Occlusion_Trace.comp`. GTAO
takes slices through the hemisphere over a point, each a plane through the eye, walks out along each side over the depth to find what's in the way,
and integrates the cosine-weighted visibility between the horizons in closed form: it's the integral the occlusion is meant to be, not an estimate
of it, which the old SSAO's random kernel never was. But it keeps only the highest horizon on each side, which takes everything in front of the
depth to be infinitely deep, so a blade of grass, a fence rail or a post hides all the sky behind it and leaves a dark halo round itself. A
bitmask gives every sample a thickness instead and keeps which of 32 sectors of the slice anything covers, so light gets in behind something thin.
Here the sectors are spaced so that each carries the same share of the cosine-weighted visibility, so counting them is GTAO's integral, and with
nothing thin about, the answer is GTAO's own: checked against a wall through the point (exactly a half) and open ground (one).

Anything solid is taken to be `Thickness` (0.5 m) deep behind what's seen of it, and a canopy, leaves, blades, moss and fur (`SSS_CANOPY`, read from
the G-buffer's albedo alpha), `ThinThickness` (8 cm). A canopy hides nothing from a canopy: a field already darkens itself by how deep in it each
blade is (`fragCanopy`, in `Grass/Grass.frag`), in the world and steadily, and blade by blade through the screen the same darkening was noise that
swayed with the blades and doubled what the field has. So on a blade or a leaf only what's solid counts, a wall, a stone, a foot, the lie of the
ground, while blades still hide the sky from the ground and the stones among them. Films of a field in the wind flicker no more with the occlusion
this way than with it off; with blades hiding the sky from blades, it added about a twentieth.

The rest was weighed and left: HBAO's horizons aren't cosine-weighted and halo as GTAO's do; XeGTAO's noise and bent normals need a temporal filter
to average them, which this engine doesn't have, and nothing would read a bent normal; nothing here traces rays.

## Nothing random

There's no temporal accumulation to average noise away, so the trace is the same every frame for the same picture. Each pixel of a 4 x 4 tile takes
its slices at angles offset by a sixteenth of the gap between them and its steps at distances likewise, in a Bayer order (`TILE_ORDER`), so the
tile between them takes sixteen times as many, evenly spread. `Occlusion_Denoise.comp` is a `[1 2 2 2 1]` filter across and then up, which weighs
each of the four places in a row of the tile alike and so takes the pattern out exactly, wherever there are no edges in the way. Its edges are
XeGTAO's (`occlusionEdges`, which measures each neighbour from where the slope through all of them says it would be, so a surface seen edge on is
still one), packed two bits each, and a pixel boxed in by edges lets a quarter in from either side, or its own pattern would stay in it. A field is one
surface to it: wherever a canopy is on either side the edges are four times as tolerant (`OCCLUSION_THIN_EDGES`), as nearly every pixel of a field is a
blade against the ground or another blade, and an edge between each left the pattern in the grass, moving with it.

Slices and steps are fixed when the trace is compiled (`OcclusionRenderer.TraceShader` keeps a program for each pair it's asked for), so its loops
unroll. One slice a pixel already comes to sixteen directions a tile; a second was measured at a fifth of a millisecond more at 3440 x 1369 and
couldn't be told from one. Steps are 2 at Low and 3 above (`GraphicsPresets`).

## The depth, and the normal

`Occlusion_Depth.comp` makes what the trace reads: metres along the view, one texel a pixel, negative on a canopy and `OCCLUSION_SKY` where nothing
was drawn (`Occlusion/Occlusion.glsl`), with three coarser levels, each texel its four averaged with the farthest counting most (XeGTAO's filter), as a
thin thing in front of what's behind it at one texel is less of what's there at a coarser one. Samples further than about fourteen pixels out read
a coarser level (`CoarseFrom`). It's the resolved depth, each pixel's farthest sample, which is where the light passes shade a pixel whose samples lie on
one surface, so the occlusion is for the point that's lit: the mean of the samples was tried, and flickered more in a field. The contact shadows and
the sun's shadow mask read the same depth (SHADOWS.md).

The normal is worked out from the depth, from the four triangles round the pixel weighted by how surely each neighbour is on the same surface: the
G-buffer's normal has the material's bumps in it, which aren't in the depth, and against them flat ground would seem to rise above its own horizon.

## How it's read

The light passes read it a pixel at a time at `GBufferSample.screenPixel` (`Lighting/gBufferSample.glsl`): an edge pixel's samples each take the pixel
of the nine round it whose depth is nearest their own (`screenTermPixel`). `g.ambient` is the material's occlusion times this. `Ambient_Light.frag`
gives back what bounces between a surface and what occludes it, by Jimenez's fit to the ground truth by albedo (`occlusionMultiBounce`), so occlusion
doesn't grey out what's bright: a whitewashed wall's foot stays lighter than a dark stone's.

It runs in `RenderEngine.ScreenTerms`, in stages beside the contact shadows and the sun's shadow mask (SHADOWS.md): the depth, the trace, the denoise
across, the denoise up, with one barrier between stages.

## Settings and views

`RenderEngine.Occlusion` (`OcclusionSettings`) is live in F3's Render window under Shadows and occlusion, and takes `--render` keys: `ao`, `aoradius`,
`aofade`, `aothickness`, `aothin`, `aoslices` and `aosteps`. `--render aodebug=1` shows it alone on white (at 2.4 times, through the tone mapping), from the ambient pass, so lamps and reflections still add
on top as they do over GI's pictures: look by day, or with `lights=0`.

## What it costs

At 3440 x 1369 on a Radeon 7900 XTX: the depth 0.11 ms, the trace 0.18 to 0.27 ms (a pine wood, a village, the Haven), and the denoise 0.08 ms both
ways, so 0.37 to 0.45 ms in all, where the SSAO it replaced took 0.36 to 0.41 ms with its blur. What the whole change cost a frame is in SHADOWS.md.

## After any change

Run the black test (`--render sun=0,sky=0,lights=0,emission=0`), look at `aodebug=1` beside the picture in a village and a wood, close up and from
the game's camera, with GI on and off (`--render gi=0`), and look for the tile's pattern at five times, which should never show. Then film a field in
the wind and count what flickers, comparing runs of one build (SHADOWS.md says how), and `--perf` at a village, the Haven and a wood against the build
before. Nobody has seen it move but in films.
