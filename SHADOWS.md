# Shadows: the sun's

What of the sun reaches a point is three things, each working where the one before can't see: the shadow map's shadow, soft by how far off whatever
casts it is (`Lighting/SunShadow.glsl`); the contact shadows, traced through the screen where the map is too coarse and too biased to see anything
(`Shadows/ContactShadows.comp`); and a material's own relief, which shadows itself as it's drawn (`Surface/Relief.glsl`, `gMaterials.a`). Clouds come
on top (`weatherSunVisibility`). `Ambient_Light.frag` puts them together, and nothing is random or kept from frame to frame in any of it: the engine has
no temporal accumulation, so anything that flickered would flicker for good.

## Umbra, penumbra and antumbra

The sun isn't a point. It's a disc about half a degree across (0.53°), and how much of it a point sees is what stands in front of each part of the disc.
Right under whatever casts a shadow, all of it is hidden: the shadow is hard (the umbra). Further from what casts it, the disc stands wider over what's
in the way, so the edge softens (the penumbra), and the further off the caster, the wider: a stone's shadow is sharp to its foot and a tree crown's 15 m
up has a soft edge. Where something small stands high, it never hides the whole disc at all, and leaves only a faint soft patch (the antumbra): a twig
high in a crown. Shadows that harden towards contact are all three of these, and they come out of one rule rather than being made separately.

## How soft a shadow is

`Lighting/SunShadow.glsl` reads the sun's shadow map as depths. Seen from a point, a texel of the map h metres above it along the light hides the part
of the sun's disc it stands in front of, and the disc there is h times the tangent of its angular radius across. So each texel in the way is spread
over a disc of its own that wide, a smooth kernel `(1 - (r/R)^2)^2` over `pi R^2 / 3`, and what all of them hide is the sum: Soler and Sillion's
convolution ("Fast Calculation of Soft Shadow Textures Using Convolution", 1998), done a texel at a time. Its radius is `R^2 = Sharpest^2 + 2 (tan(sun's
radius) h / texel)^2` (the 2 gives the kernel the spread of a uniform disc that wide), never more than `Softest`, which bounds what a pixel costs.

It's that and not PCSS (Fernando, 2005) because PCSS takes one average height for everything round a point: under a tree, a stone's contact shadow is
blurred as widely as the crown's shadow over it, and its search for that height pops as things move through its edge. Filtered maps (variance, moment)
leak light where casters overlap, which is everywhere in a wood, would change the shadow pass and everything that reads its depths, as the light through
flesh does (SUBSURFACE.md), and still need a search for contact hardening. Nothing here traces rays (GL 4.3), and there's no temporal accumulation
to spread a sparse search over frames.

Three things make the sum right everywhere:

- A map has no sides. Next to a step from something low to something tall, both in the way, the tall one's discs from beyond the step are missing and the
  low one's are too narrow to make up for them, and a solid shadow let through up to a quarter of the light along such a step. So each texel that isn't
  in the way has a disc too, as wide as the widest that reaches the point (`open2`), and what's hidden is never less than the share of all the discs that
  are in the way (`sunShadowFinish`): all of it wherever everything round the point is in the way, whatever their heights, and the sum on one caster's edge.
- A field of texels sums to the whole disc only to within a few tenths of a percent, so 1% at either end is taken as all or nothing (`SHADOW_SNAP`), and a
  shadow is wholly dark where everything is in the way and wholly lit where nothing is.
- Every texel that can reach the point is read: none is skipped and nothing is jittered, so the shadow is a function of where the point is alone, the same
  on every pixel that sees it, and as still in the world as the map, which is snapped to its texels.

Reading every texel would cost a lot, so most pixels don't. `Shadows/SunShadow_Ranges.comp` keeps each block of 16 x 16 texels' nearest and farthest depth
(`DirectionalLightRenderer.PrepareRanges`), and `sunShadowSetup` reads the blocks within `Softest` of a point: if nothing in them is above it, it's lit;
if everything within reach is in the way, it's inside a shadow (`deep`); otherwise how far out anything reaches it (`foot`) bounds the disc of texels read.
Within four texels (`SHADOW_INNER`) each texel is weighed on its own; further out, where only a wide disc reaches, the four of a gather are weighed together
at the middle of those in the way, which is right to the second order. A long shadow is lifted by how far off its caster is, as air fills it with light
(`sunShadowLift`, from 16 m to 96 m, at most half), by a small fixed filter that's the same whether the texels are read or the shadow is known from the
block map, so the two meet with no seam.

`Sharpest` is 4 texels, as the fixed filter before this was: at 2.75 a field in the wind flickered a tenth more, its blades' shadows being that much
sharper, and the contact shadows have the crisp contacts now. `Softest` is the same in metres at every quality (8 texels of a 4096 map, 6 of 3072, 4 of
2048, about 29 cm), and at the sun's own size what stands more than about 39 m above what it shades softens no further at High, 33 m at Medium. At Low
that's the sharpest too, so Low's shadows are as they were. Neither can go past `DirectionalLightRenderer.MostSoftness` (16 texels), whatever the
settings say, as every pixel near an edge reads every texel within the softest of it. The sun's height matters: what's measured is along the light, so
in a low evening sun a tree's shadow is soft for most of its length and at noon it's hard nearly to its tip.

## Once a pixel

`Shadows/SunShadow_Mask.comp` works the shadow out once for each pixel, at the resolved depth (the farthest sample, which is where the light passes shade
a pixel whose samples lie on one surface), into what reaches it directly and what fills its shadow (RG16F) and how soft it is (R8, for the debug view).
The light passes read it a pixel at a time (`gBufferSample.glsl`). An edge pixel's samples are each lit on their own, and each takes the shadow of
whichever pixel of the nine round it has a depth nearest its own (`screenTermPixel`). A solid sample further from all of them than 0.6% of its distance,
facing the sun, a wire or a twig in front of everything round it, works its own out (`calcShadowParts`, `SUN_SHADOW_APART`); a canopy's, a blade or a
leaf, takes the nearest, as a field's light is its masses' and not its blades'. Working every edge sample's own out instead was measured to flicker no
less, and costs more. A version that shared the texels of a tile through shared memory was slower: don't go back to it.

## Contact shadows

`Shadows/ContactShadows.comp` is Bend Studio's screen space shadows (Graham Aldridge, "Screen Space Shadows", SIGGRAPH 2023, as in Days Gone), ported to
GLSL under the Apache License 2.0, whose notice and a list of what was changed head the file. Every thread of a 64 wide group is one pixel of one ray
towards the sun's projection on the screen, the rays of a group are lined up so that each thread reads one sample and they share them through shared
memory, and `ContactShadowRenderer.BuildDispatchList` works out the groups on the CPU, in OpenGL's conventions. It catches what the map can't: a foot on
the ground, a stone in the grass, the folds of a body, a post's foot.

What was changed from Bend's, and why:

- It reads the occlusion's depth (OCCLUSION.md), where a canopy is marked, and a canopy casts nothing: leaves and blades are the map's to shadow softly,
  and blade by blade they're noise.
- Anything standing less than `MinHeight` (3 cm) in front of the ray casts nothing, as at this distance the ground's parallax relief in the depth would
  sprinkle it with single pixels of shadow that the relief draws itself.
- Only the first two samples can shade a pixel outright, not four, for the same reason.
- A sample's weight fades over the ray, from `FadeFrom` (10 samples) to `FadeTo` (32), so the contact shadow hands over to the map's, which is soft where
  it's cast from far, rather than carrying a hard edge out over it.
- What comes out is smoothed over the three by three pixels round each that are on one surface (`ContactShadows_Blur.comp`), as a shadow a pixel sharp
  crawls as the camera moves.

A contact shadow only takes what reaches a point directly, never the fill in a long shadow: `lit = min(direct, contact) + fill`.

## The order of things

`RenderEngine.ScreenTerms` runs everything read of the screen and of the sun's shadow in four stages, with one barrier between stages, as a barrier waits
for everything before it:

1. The occlusion's depth (OCCLUSION.md), the sun's block maps, and the contact shadows' target cleared.
2. The occlusion's trace, the contact shadows' rays and the sun's shadow mask, side by side.
3. The occlusion's denoise across and the contact shadows' smoothing.
4. The occlusion's denoise up.

The textures are bound for every light pass by `MsaaRenderer.BindGeometrySamples` (units 13 to 17) and named by `MsaaRenderer.LinkScreenTerms`.

## Settings and views

`RenderEngine.SunShadows` (`SunShadowSettings`) and `RenderEngine.ContactShadows` (`ContactShadowSettings`) are live in F3's Render window under Shadows
and occlusion, and take `--render` keys: `softshadows`, `sunsize`, `shadowsharpest`, `shadowsoftest`, `contact`, `contactthickness`, `contactedge`,
`contactcontrast`, `contactmin`, `contactfadefrom` and `contactfadeto`. `--render shadowdebug=1` shows how soft each shadow is (black sharpest, white
softest), `shadowdebug=2` the sun's shadow alone on white, and `contactdebug=1` the contact shadows alone, all from the ambient pass, so lamps and
reflections still add on top: look by day, or with `lights=0`. `Softest` is a quality's (`GraphicsPresets`).

## What it costs

At 3440 x 1369 on a Radeon 7900 XTX, the soft shadow takes 0.41 ms in a village and 0.63 ms in a pine wood and in the Haven's evening sun, where
most of the screen is near a shadow's edge, and its block maps 0.05 ms; the fixed filter it replaced took about 0.2 ms inside the ambient pass. The
contact shadows take 0.16 ms with their smoothing. With the occlusion (OCCLUSION.md), a frame went from 9.3 to 9.9 ms in a village, from 9.7 to 10.3 ms
in a wood and from 9.5 to 10.4 ms in the Haven's evening, untimed (`--perfgpu false`), against the build with the old filter and SSAO.

## After any change

Run the black test (`--render sun=0,sky=0,lights=0,emission=0`), and look at a village by day and the Haven in an evening sun (`--haven true --hour 17`),
with `shadowdebug` and `contactdebug` beside the picture. Flicker is measured, not judged from stills: film a field in the wind (`--seed 303 --site Village
--weather clear --size 1280x720 --filmstart 500 --filmframes 48 --filmrate 30`) and count pixels that change and change back from one frame to the next,
comparing runs of one build only, as two builds film the wind at different moments. Then `--perf` at a village, the Haven's evening and a wood against the
build before. Nobody has seen any of this move but in films.
