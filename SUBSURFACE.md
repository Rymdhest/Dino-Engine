# Subsurface scattering: leaves, skin, cloth and snow

Light doesn't stop at most surfaces. It gets in, scatters about and comes out somewhere else: through a leaf to its other side, round the terminator of a
cheek, out of the thin web of a dinosaur's frill red with blood, a little way into snow and back out ahead of where it went in. Every surface here says how
much of that it does and what kind, and the light passes take it from there, so nothing after the light passes knows it exists.

## What a surface says

- The G-buffer keeps it in `gAlbedo.a` (`Shaders/Lighting/SubsurfaceKinds.glsl`): the kind in the top two bits and the amount, 0 to 1, in the six below. 0 is
  anything that scatters nothing, which is most things.
- There are four kinds (`Textures/SubsurfaceKind.cs`, the same numbers as the shaders' `SSS_` kinds), because what light does once it's in can't be said by
  how much of it does it:
  - a **sheet** is thin with a face of its own, cloth, paper, petals, a gem: what falls on its back comes out of its front;
  - a **canopy** is a mass of thin things turned every way whose normals are the mass's, leaves on cards, grass blades, moss and fur: light goes on
    through it mostly the way it was going;
  - **flesh** is skin and hide: thick, so light goes on past the terminator and out wherever the body is thin, red with blood;
  - **frost** is snow and ice: light goes a long way in it, comes back out mostly ahead of where it went in, and blue goes furthest.
- A material's kind is its line in `MaterialCatalog` (`MaterialSpec.Scatter`), sent to the shaders in the material table's third column
  (`Surface/MaterialTable.glsl`), and its amount is its recipe's `Subsurface`, texel by texel, kept in the normal array's blue. A card is a canopy whatever
  it is, as are grass blades, shells and imposters. The ground goes by its winning material: moss, turf, needles and leaf litter are canopies, snow and ice
  frost, rock nothing. Wax is flesh, for the warm light in a candle.
- That amount is baked exactly as the recipe says. It once wasn't, for any recipe whose roughness, emission, metal and subsurface were all constants:
  `MaterialBaker.WithAlpha` dropped a constant alpha and the colour was stored with an alpha of 1, so 21 materials let light through as a sheet does
  whatever they said, rock, bark, roof tiles and metal among them, and the old translucency added that light from every lamp whichever way a surface faced.
  Some looks were made by it and judged so, and keep it on purpose, said outright in their recipes: `FlatGlow` (a lantern's panes are lit only by the lamp
  inside them), `Flat` (the far land behind the main menu, candles' wax), dressed stone, `Rock` (the Haven's square and gate, as pale limestone takes light
  in), and cloth, bone, thatch, sand, gem, grain and the halls' leaves, whose recipes meant them to scatter and were only ever seen at 1. Bedrock (`Cliff`:
  boulders, the rock skin and the ground's rock), bark, roof tiles, mirror metal and cracked earth let nothing through, as their recipes say. Turning one of
  the first to 0 changes what people have seen: the Haven's gate went a third darker by its lanterns, and the lanterns went out.
- A thin thing's amount is what gets through it, less what its thickness takes (`gBufferUtil.glsl`'s `transmittance`), which is what the old single
  translucency number was. Flesh's and frost's is how far light goes in them, as a share of their kind's reach.
- Snow that settles out of the weather scatters as frost: `weatherWetten` says how much settled, and `encodeSubsurfaceUnderSnow` takes the light out of what's
  under a little snow and makes a lot of it frost, both going to nothing where one gives way to the other, so there's no line where the kind changes.
- Living things are drawn in materials of their own, appended to the catalogue so no other layer moved: `skin`, a person's, flat as the flat material
  people were drawn in before, and `hide`, a beast's or a dinosaur's, leather's grain and colour with light getting in under it. `GearMaterials.Skin` and
  `GearMaterials.Hide` are what generators use (`HumanoidGenerator`, the boars', wolves' and bears' dressers on `QuadrupedDress`, `SaurianPaint`, and the dinosaurs' crests, frills, toes and digits in
  `SaurianParts`); leather that's worn, a harness, a strap, stays leather, horn and spines stay leather, and an eye or a tusk is `GearMaterials.Plain`.
- A kind can't be averaged and an amount can, so the MSAA resolve (`Msaa_Resolve_GBuffer.frag`) averages a pixel's amounts under whichever kind scatters most
  among its samples. It doesn't make a pixel of mixed kinds an edge pixel: tried, it made every blade over cobbles with moss in their joints, or over earth with
  straw in it, an edge, and the light passes shaded fields and road edges four times over, a tenth of a millisecond, for a difference less than a pixel wide.
  An edge pixel's samples are lit one by one anyway, each by its own kind.

## How each kind is lit (Lighting/lightingCalc.glsl)

- A **sheet** lets through what falls on its back as a Lambertian sheet does (`sssBackLit`), wrapped a little as light that's crossed something is spread,
  and giving the same over a whole sphere of light as an unwrapped one. What comes out is the surface's own colour, deeper (`Saturation`), as what crosses
  cloth or a petal has been through more of it than what bounces off.
- A **canopy** doesn't look at its normal at all for what it lets through, as a card's normal is the crown's and says nothing of its leaves. Leaves turned
  every way, each a Lambertian sheet, pass light on by the angle between the way it travels and the way to the eye: the area scattering phase function of
  a uniform spread of bi-Lambertian leaves (Ross 1981; Shultis and Myneni 1988), `sssCanopyPhase`, 1 straight through, a third sideways and nearly nothing
  back. To that is added what leaves pass among themselves (`Fill`), the same every way. The crown's own shadow comes from the sun's shadow map, so a backlit
  crown glows where it's thin, at its edges and through its gaps. What comes through is a warmer green than what bounces off (`Colour`), as a leaf lets yellow
  through better than blue. From the game's camera, 67 degrees down, light never comes straight through: the angle between the light's way and the eye's is 67
  degrees at the least, with the sun on the northern horizon. The old translucency glowed most on a card facing the light, which is the wrong way round; this
  glows least there and most against the sun, and comes to about the same over a crown.
- **Flesh** and **frost** have their own diffuse light carried on past the terminator, each colour as far as it gets under the surface (`sssWrappedDiffuse`,
  `Wrap`): eased in with no edge where it starts, and keeping what a sphere of light gives, so what reaches the shadowed side is taken from the lit one.
  Flesh's red goes furthest and frost's blue.
- **Frost** sends back what gets into it mostly on the way it was going (`sssFrostLight`): a half-space of bright grains each scattering once, Lommel and
  Seeliger's law with the canopy's forward-leaning phase, so a snowfield shines brighter looking towards the sun than away from it, and brightest where the light
  comes in low. It's what the old translucency was standing in for on snow, by accident: real snow's reflectance leans forward.
- Thin things also let through the light from all round that reaches their backs (`sssAmbientThin`): the sky and ground behind them, or what global
  illumination gathered from that half of the world (`giIrradiance`'s `behind`). In the shade and under cloud it's most of what makes foliage glow.

## The sun through flesh (FleshDepth, Lighting/SunShadow.glsl)

How thick something is along the light is what decides how much gets through it, which needs where the light went in and where it comes out. The pixel being lit
is where it comes out; where it went in is the first flesh the sun meets along the same ray, which a depth map from the sun's side holds. The difference is how far
it went through, as Unity's HDRP and Unreal measure thick transmission, after Jimenez et al., "Real-Time Realistic Skin Translucency" (2010).

- The sun's own shadow map would seem to do, and was tried first. It can't: the shadow pass's polygon offset, which keeps a surface from shadowing itself, pushes
  each surface back by its slope to the sun, 16 to 23 centimetres at 45 degrees depending on how its slope lies across the texels, and red light
  only gets seven centimetres into flesh. Put back from the slope where the light comes out, it was still out by several centimetres, and thin parts came out
  speckled. So flesh has a map of its own (`FleshDepth`): 2048 texels over 64 m round the hero, three centimetres each, drawn each frame with no offset, of
  characters near the hero alone, and of their skin and hide alone (`Shadow_Flesh.frag` discards the rest, by the material table). It costs one more drawing of
  the nearby characters into a small depth map.
- Only what faces away from the sun lets any of it out: whatever faces the sun at all is lit by it directly, and the ambient pass carries that round the
  terminator as flesh does (`Wrap`). It comes in over a fifth of a cosine past the terminator (`SSS_FACING_AWAY`), as the normal is the shading one, bumps and
  all, and a hard cut would follow every bump.
- It's read over the four by four texels round the point, weighted as the shadow is, and each texel's thickness goes through `exp(-thickness / reach)` for
  each colour before they're averaged, not after, so a thin edge isn't lost in a thick average.
- Only flesh in front counts, a centimetre of it and more in full (`THICKNESS_FULL`): where the map shows none, or shows the point's own surface, the light went
  past the body or straight to the point, not through. And each texel counts by how squarely the sun met the flesh there, read from how fast the map's depth
  changes across it, as light that meets a surface edge on hardly gets in. That's also what keeps it honest: the only texels that read thin where nothing is
  are those where the map grazes a body's outline from the sun or steps from one part to another, and there the depth runs away. The first version counted a
  texel up to its own slope behind the point as no thickness at all, which where the sun skims a surface (a big body's whole flank under a high or cloudy sun)
  made the surface its own thin flesh, and painted it in orange stripes along the map's texels, seen close on a giant in the rain. `sssdebug=2` showed those
  bodies white; now only what's really thin and turned from the sun shows.
- What comes out is the same every way, as light is that comes out from under a surface, in blood's colour with some of the surface's hue, and as bright as the
  skin is pale (`SSS_SKIN_LUMA`, a person's): light coming out through skin crosses the same pigment it would have bounced off, so a dark hide glows far less
  than a person's ear.
- Armour, a harness, a tree or a wall between the sun and the flesh isn't in that map, so the sun's own shadow map is asked whether anything stands in front of
  where the light went in, along the same ray, by more than its offset and the two maps' texels allow (`THICKNESS_ENTRY_SLACK`). A tyrant's gorget throws its
  shadow on its neck, and a crest in a tree's shade doesn't glow.
- It's added as a pass of its own after the ambient pass, on characters alone, by their stencil bit (`Flesh_Light.frag`, `AmbientLightRenderer.DrawFlesh`), per pixel
  and per sample as the light passes are, and only where they face away from the sun, so that nothing else ever carries its five gathers of depth and
  forty-eight exponentials.
- It's the sun's alone. A lamp's cube map is kept still for most lamps, without whoever walks by in it, and from 33 m up a hand by a lantern is two pixels,
  so flesh by a lamp only has its light wrapped.
- Flesh's red goes seven centimetres (`Reach`, at an amount of 1; hide is 0.8), ten times what real skin's does at the world's size, as from up here a frill,
  a crest, a toe or the tip of a tail is a few pixels and the red through it has to be seen there; a limb a span thick lets a few hundredths through, and a
  body none. Nothing gets through a wall.

## What isn't here, and why

- **Screen-space diffusion** (Jimenez's separable SSS, Golubev's Burley sampling): it blurs the diffuse light of skin over the screen by its diffusion
  profile. From the game's camera a metre is thirty pixels, so what skin scatters is under a pixel; it would split every light pass's diffuse from its
  specular, blur per sample on edge pixels, and cost most of a millisecond for nothing that can be seen.
- **Pre-integrated skin** (Penner and Borshukov 2011): a table of how the terminator softens by curvature, which needs a curvature per pixel and a channel
  the G-buffer hasn't got. The flesh map's thickness softens thin parts and leaves big ones sharp, which is what curvature would have said.
- **Volumetric scattering** (random walks through the volume): that's ray tracing, and the flesh map's thickness gives what it would of a body seen from here.
- **Light through a crown from leaf to leaf**: what reaches a leaf through others ahead of it is a second transmission, a few percent, and the sun's map can't
  tell leaves from a roof, so a bush under the eaves would glow. The gaps the shadow filter lets through do it instead.
- **Ghosts**: their flesh is light added over the scene, not lit, and its look is settled; nothing scatters in it.
- **Nothing behind what lies on something**: the ground's turf and moss, and moss and fur shells, take the light from the half of the world behind them as a
  leaf does, which is wrong where the earth, a stone or a body is there. Measured, it's about two percent on fur and nothing that shows on ground under grass,
  which isn't worth a kind of its own in the G-buffer.

## Elsewhere

- The hero's picture on the character screen is drawn under the studio's own lights (`Shaders/Studio/Item.frag`): skin and hide there soften the key's edge
  and take the rim light from behind through their edges, warm. Nothing carried is either, so no icon changed.
- `Dino FloraLab`'s picture of a plant (`Preview.cs`) lets light through its leaves by the canopy's phase and reads the same starting numbers, so the two can't
  drift apart.

## Settings and debugging

`SubsurfaceSettings` (`RenderEngine.Subsurface`): a profile for each kind, sent to the shaders once a frame as the `SubsurfaceData` block, binding 14
(`SubsurfaceProfiles`, the block in `Lighting/lightingCalc.glsl`: four rows a kind and the switches, change both together). F3's Render window has a foldout for
it, and a scripted run takes `--render sss=0` (all of it off), `sssthick=0` (the flesh map off, and with it the sun through flesh), `sssdebug=1` (each surface's kind
and amount in place of the scene: sheets blue, canopies green, flesh red, frost cyan) and `sssdebug=2` (what of the sun gets through flesh, white where all of it
would), and `canopy.direct=1.8`, `flesh.reach=0.07`, `sheet.colour=1:0.9:0.8` and the like for any profile's numbers (`SubsurfaceSettings.Set`). A debug picture still
has whatever comes after the ambient pass laid over it: lamps, reflections and fog.

## Performance

Measured at 3440 by 1369 on the 7900 XTX against the build before, two runs each, with `--meshseed 11 --wind 0:0:0`: the Haven at 17 with five dinosaurs and
its lanterns, 9.94 ms of GPU a frame against 9.79 (98.6 frames a second against 100.1), and a wildwood, 11.01 against 10.80 (89.7 against 91.4). The flesh map
is 0.016 ms, the flesh pass a few hundredths on five creatures, the point lights within three hundredths of where they were, and the rest is the ambient pass's
light through every leaf and blade, 0.07 to 0.11 ms. The first version cost twice that. Most of it was edge pixels for mixed kinds, a tenth of a millisecond over
a field, found by putting the old shaders back one at a time; the rest was a kind's profile read by an index that differs from pixel to pixel, a load per pixel
for every lamp, which is why `sssProfileOf` reads each row at an index it knows. `--render sss=0` turns it all off.

## Checks

After any change here run the black test (`--render sun=0,sky=0,lights=0,emission=0`), look with and without it (`--render sss=0`) at a backlit wood (the Haven at
`--hour 17` is lit from the north, against the camera, and at noon from the south), at creatures (`--haven true --hour 17 --parade
war-raptor,hornback,strider,reaper,domehead`), and at them close and low as well (`--parade hornback,clubtail,war-raptor --look 18 --angle 40` at noon, at 17 and in
`--weather rain`, with `sssdebug=2`), as the striped flanks were all but invisible from 33 m up and plain to anyone who zoomed in on a giant, at snow (`--seed 2
--archetype highlands --biome snow`, and the Haven with `--weather snow`), and at the Haven's cloth, and `--perf` in a wood and at a parade against the build before,
with `--meshseed` and `--wind 0:0:0` so two builds grow and move the same.

The main menu's camera stands low and looks across its meadow, where light comes through blades and petals further than it ever can from the game's camera,
67 degrees down: its grass and flowers are about a fifth brighter than before this, by blades and flowers about equally, while the rest of the picture is
within a few percent. `GrassSingleton.Translucency` (F3's grass) and `canopy.direct` and `canopy.ambient` turn it down if it's too much.

Nobody has seen any of it move: it was judged from stills, on one GPU.
