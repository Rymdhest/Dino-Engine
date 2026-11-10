# Weather

How the world's weather works, how wet things get, and how reflections are made. There are no weather states, timers or authored storms anywhere: every world's weather comes from its own climate and seed, like everything else in it.

## The one idea

**Weather is a function, not a simulation.** `WeatherField` (`Dino Engine/World/WeatherField.cs`) takes a place and a moment and says what the weather is doing there. It's built from the world and nothing is stored or stepped, which gives four things for free:

- **Every machine agrees with nothing sent.** The seed and the server's clock are enough, so a friend across the map sees the same front coming.
- **Any part of the world can be asked at any time**, so what's raining over there is already known before anyone goes.
- **The ground's memory costs nothing to keep.** How wet a place is comes from looking back along the same function, so a place first seen after a storm is already wet, and nothing has to be simulated for land nobody is on.
- **It can be tested with no window.** `Dino WorldLab weather` runs every kind of world through hours of it.

```
 WorldData ──> WeatherField ──> WeatherSystem ──> WeatherRenderer ──> sky map, cloud shadows, rain rings, rain shadow
 (biomes,        (place, time)    (live state,                          │
  height)                          map of the world)                    ├── every geometry shader (wetness)
                                        │                               ├── light passes, fog, water
                                        └── Weather.Here ──> sound, wind, ambience
```

## Where weather comes from

Systems of wet air drift across the land on the world's own wind, changing shape as they go: two scales of noise carried along by `Drift`, and that's all that moves. **Strength** at a point is those plus a bias from the climate there, and each kind of weather is a range of strength: cloud forms first, rain starts higher up, a storm builds at the top.

What the climate adds comes from data the world already has:

| What | From |
|---|---|
| How wet a place runs | `BiomeDefinition.Moisture`, weighted by the biomes there |
| How warm it is | `BiomeDefinition.Temperature`, height, and the whole world's average, as a world is small next to real climate zones: a frozen march is cold all over |
| Rain on the windward side, and a rain shadow behind | How far the land climbs upwind |
| How unsettled the world is | `WorldMood.Unsettled`: an overcast world runs wetter than a high summer one |
| Rain or snow | Warmth |
| Ash in place of water | Biomes with `AmbientMotes.Embers` |
| Dust in place of rain | A front crossing land too dry to give it any water, seen as low fog |

Note that `world.Temperature` and `world.Moisture` are ranks within a world (`ClimateStage` stretches them over 0 to 1), so they can't say whether a place is dry in absolute terms. Read biomes for that.

Nothing here touches world generation. `WeatherField` draws from its own fork, so adding to it changes no world.

**Time** is `WeatherSingleton.Clock`, which the game sets from the server's clock (`AmbienceSystem.UpdateWeather`). Worlds start at their own point in their weather, and a saved world starts from that point again.

**Lightning** is part of the function too. `WeatherField.Strikes` turns a span of time into bolts: each slot of time has one chance, at a place drawn from its number, and it comes to something if there's a storm there. So everyone sees the same bolt at the same moment, and asking for the same span in pieces gives the same bolts.

## What a frame costs

`WeatherSystem` bakes the whole world into a 64 by 64 map (`WeatherField.Bake`) on a worker every 0.75 s and eases towards it, so a frame only blends a few thousand numbers. The map is the only way the GPU learns about weather. What's happening at the focus (`WeatherSingleton.Here`) is eased too, and it's what sound and light read.

| GPU time, 2560x1440, RX 7900 XTX | Fair | Rain |
|---|---|---|
| Weather (maps, sky, shadows, rings) | 0.02 ms | 0.08 ms |
| Precipitation, drips included | none | 0.03 ms |
| Reflections | 0.43 ms | 0.63 ms |
| Reflections as they were | 0.32 ms | (no rain) |

Rain adds about 0.1 ms across the geometry passes. Everything in `Wetness.glsl` is skipped while nothing near the camera is wet (`weatherKind.w`), so dry land pays for one uniform branch. The view from overhead that drips come from is redrawn with it, only while anything is wet. The rain shadow is redrawn only when the view has moved 12 m or every 3 s.

## The sky

`Weather/Sky_Map.frag` draws one hemisphere into a paraboloid map, as Ghost of Tsushima does (Patry, "Real-Time Samurai Cinema", 2021), every few frames. It marches the cloud layer, with Patry's phase function: light scatters forward in thin cloud and back in thick cloud, by how deep it has come, which gives backlit clouds their silver lining and front-lit ones their dark edges with no special case for either.

Everything that looks at or reflects the sky reads this map (`Weather/Sky.glsl`): `sky.frag`, the water, reflections. Its mips stand in for roughness. The sun isn't in it: the light passes draw the sun's highlight. Lightning is added as it's read, so a flash doesn't need the map redrawn.

Clouds are `Weather/Clouds.glsl`: the weather map says how much of the sky is covered, tiling noise says what that cover looks like. `Cloud_Shadow.frag` looks through the same clouds along the sun's rays for the shadows on the land, so what's overhead is what's shading the ground. From this camera those shadows sliding across the land are the first sign of a change.

**Cloud is applied as light is read**, as night is: nothing writes to the sun or sky. `SkySYstem` works out the sky and ambient, `Ambient_Light.frag` multiplies the sun by the cloud shadow per pixel, and fog and god rays take `Weather.SunThrough` at the focus. Two tests of this: with sun, sky, lights and emission off the frame must still be black, weather or not, and a light with cascades is taken to be the sun, so lightning, which has none, doesn't get dimmed by the clouds it's under.

Exposure is fixed, so `OvercastSky` has to be bright. Real overcast is about a tenth of a clear day, and at that level this game reads as night. It's about a third.

## What falls

Rain, snow and ash are one renderer and two shaders (`PrecipitationRenderer`, `Weather/Precipitation.vert`), with nothing simulated. A drop's place follows from its number and a few running totals, wrapped in a box of air around the view, so drops stay where they are in the world as the box moves through them. Where it isn't raining the drops aren't there, so a front has an edge, and the rain shadow puts out whatever would be falling under a roof.

Keep these rules, as they're the wind's:

- **Whatever moves keeps a running total, and adds to it.** `WeatherSystem.Advance` does this. Never multiply a clock by a live speed.
- **Totals wrap at a whole number of cycles of everything that reads them.** A drop falls at one of four shares of the speed, in eighths, so wrapping at eight volumes moves none of them.
- **Streaks come from real 3D lines**, so they fall away from a camera looking down, and converge on the point below it, as rain does from a balcony. A drop falling straight away from the eye is a dot.

Splashes (`Weather/Splash.vert`) come up at random points of the rain shadow's own map, so they land on whatever the rain really lands on, with nothing having to know what that is.

Rain picks up the light of what it's falling past from `RenderEngine.SceneBlurTexture`, so it glows near a fire with no list of lights.

## Clarity

**There's a game to see, and it comes first.** A person called the first version's wind "lines across the whole screen" and its air foggy, and both were true: dust was drawn as long streamers over the whole frame, and rain, snow and dust each added a uniform haze. From a camera 33 m up, a haze has no far distance to give depth to, and all it can do is take the contrast out of everything. So:

- **Nothing may draw across the whole picture but the rain and snow themselves.** No weather adds to `HazeDensity`. Wind shows by what it moves. Dust isn't particles at all, it's fog.
- **Nothing carried by the wind crosses the screen at the wind's speed.** Motes and weather's fog ride `WindField.PatternSpeed`, which levels off. Rain and wetness take dust and pollen out of the air (`LeftInTheAir`).
- **Flakes fade out well before the eye** (`NEAR_FADE_FLAKE`), where they'd be blobs over what's being looked at.

To judge any change here, compare detail with a clear shot of the same place, and take a film and look at what moved: a still can't show a line that's a speck moving fast.

## Fog

Weather's air is the volumetric fog, shaped by `VolumetricFogSystem.Thicken` for that frame only, as `FogGlow` is. Each kind has a `FogLook`, and they're blended by how much of each there is, so one turns into another as a front crosses:

| Look | When | How it lies |
|---|---|---|
| Mist | Wet air, thickest as rain lets up over soaked ground | Low pale banks, gathering in hollows |
| Steam | Sun reaching wet ground in still air | Thin wisps that rise and curl quickly |
| Ash | Ash coming down | Dull, heavy, slow |
| Snow drift | Snow with wind on it | A metre deep, blown along the ground |
| Dust | A dry front | Warm banks rolling along the ground |

Still air is what ground fog needs and wind tears it up, so `Gale` thins mist and steam, and what lifts dust and drives snow is the same wind. Four things keep it fog rather than a veil:

- **Banks.** The noise's broad swells part it into banks with bare ground between (`Banks`, `BANK_EDGES`). That's what reads as fog from above: the billows sit around 0.55, so a look's `Coverage` of about 0.85 is dense in a bank and the bank's edges do the opening.
- **Clearing.** It sinks to the ankles around the hero (`FogClearing`), so a fight is never lost in it.
- **Pooling.** Comparing the ground with what's 14 m around it says what's a hollow, and mist fills those and thins on rises (`hollowAt`). It's worked out once per ray.
- **Glow.** It has to be brighter than what it lies on, or it has no shape and only flattens the ground. `Glow` is how much sky light it scatters, so with the sky off it's as black as anything else.

`NoiseScale` can't be part of a look. Changing it scales the pattern about the far-off world origin, and every billow on screen slides, which is the wind's rule again. The fog's cost doesn't change with any of this, as the slab it marches is thin.

## Drips

Water comes off things, and nothing has to be told where. While anything is wet the shadow passes also draw the world from straight overhead (`WantsDripDepth`), as the rain shadow leans with the wind and a roof's windward edge doesn't show as an edge in it. `Drip_Sources.frag` looks through that for steps down, a tile a metre across at a time, keeping the best: a surface with air beside it and something lower under the air. Eaves, branches, a stall's canvas, a well's roof and the top of a post are all that, and a drop falls down the low side.

Each of those places keeps its own time (`Weather/Drip.glsl`): a period of a few quarter seconds, drawn from the metre of the world it's on, so a place drips the same way when the map comes back to it, and a roof has a rhythm instead of a shimmer. A drop's height is a function of the clock, so nothing is simulated or stored. It swells on the edge, falls under gravity, leans a little with the wind, and lands.

How often is `rate`: rain coming down, and after that how wet things are, squared. **So they keep dripping after the rain stops**, slower and slower, for as long as `Wet` takes to go. It's the same rule for sound (`ReadDrips`).

- **In the air** it's `Drip.vert`: a bead, then a streak that lengthens as it speeds up.
- **On the ground** `Drip_Surface.frag` draws a map around the view that every surface reads in `weatherWetten`: slopes for a ring from each landing, and a soaked patch under each edge, even where no rain gets in. Being read in the geometry pass, the rings are real ripples and bend what a puddle reflects. `Drip.vert` adds a small ring over it, which is what shows on ground too dry to ripple.
- **On walls** beads run down whatever's steep (`weatherBead`), each leaving a dark trail in anything that drinks. Water doesn't sheet down a wall (Lagarde is firm on that), so most paths stay empty. They follow how wet the place is, not only whether the rain reaches that wall, or this camera, which only sees one side of anything, would show them only in some winds. Judge them moving: a still shows faint marks.

A drop landing in open water draws its ring but doesn't ripple the surface, as the water shaders have no texture unit left to read the map with.

## The rain shadow

The world seen from the way the rain falls, as depth: `WeatherRenderer` draws it with the same shadow passes the sun uses (`DirectionalCascadeShadowSystem`), then puts the ground in with `TerrainRenderer.RenderHeightCapture`. It leans with the wind. It answers three questions:

- Is this point sheltered? (`rainReach`: what's under a roof or a tree stays dry.)
- Where does the rain land? (Splashes and streaks.)
- What's over a reflection's head when it's off the screen? (`skyOpenness`, below.)

Geometry reads last frame's map, as geometry draws before shadows, so the new matrix only goes live after the new map is finished (`FinishRainShadow`). Change that order and the whole world flickers dry whenever the view crosses a step.

## Wet surfaces

Every material gets wet, in the geometry pass, so that sun, sky, lamps and reflections all see the same surface (`Weather/Wetness.glsl`). After Lagarde's "Water drop" series:

- **Water in the pores darkens a surface** and deepens its colour, because light that would have come back out is caught in there. How much is the material's **porosity**. That's the wetness factor every material has: its `Porosity` in `MaterialCatalog`, read by index in the shader.
- **It also gets smoother**, as the water fills in what scattered the light.
- **A film forms** over time. Thirsty things drink it. It stands on metal, glass and ice, which take in nothing.
- **Water stands where it's low**: first between the stones of whatever's rough (the material's own height), then in the ground's hollows as puddles, flat and nearly mirror-smooth.
- **Rain rings** run over anything wet enough to carry them, and every water surface: one tile drawn each frame (`Rain_Ripples.frag`).

A new material needs a line in `SetPorosity`, or it behaves like packed earth. Materials past the world's own (leaf and twig cards) are foliage, and grass does the same per blade in `Grass_Generate.comp`, once a blade and not once a pixel.

Where puddles form is in the alpha of the terrain's grass map, worked out by `TerrainSurface.Hollow`: trodden ground and real dips, not sand and not slopes. `TerrainSurface.HollowAt` is the same rule for the CPU, which is how a foot that lands in a puddle you can see is heard splashing. `PuddleLevel` in `GameSounds.cs` has to agree with `Wetness.glsl`.

`weatherFilterRoughness` is geometric specular anti-aliasing. Without it a smooth wet surface over a bumpy normal map glitters and crawls.

**A wet surface reflects more than physics says, on purpose.** From this camera's 67° water reflects about 2%, and physically correct wet ground is simply dark: it's the grazing angle of a low camera that makes wet ground shine in other games. So `surfaceF0` in `lightingCalc.glsl` gives anything as smooth as water a higher reflectance, by `WATER_SHEEN`. Smoothness is how it knows, as the G-buffer has no room for a wetness channel, and little that's dry is that smooth.

## Reflections

`ScreenSpaceReflectionRenderer`, after Stachowiak's stochastic screen-space reflections (SIGGRAPH 2015) and AMD's FidelityFX SSSR. The old pass marched in fixed steps, only for metals, took its roughness from a hash of the world position, and fell back on the unreflected pixel.

1. **A depth pyramid** (`Reflection_HiZ.comp`): rays skip what they're clearly in front of.
2. **A blurred copy of the scene**, for the width a rough surface's reflection has at a distance.
3. **One ray per pixel at half resolution** (`Reflection_Trace.frag`), drawn from the surface's own GGX lobe by visible normals (Heitz 2018). Mirrors shoot straight, and have no noise at all.
4. **Each pixel uses its neighbours' rays**, weighed by its own lobe against how likely each ray was (`Reflection_Resolve.frag`).
5. **A history** (`Reflection_Temporal.frag`) that follows what's seen in the mirror, not the surface. It's clamped to what's around the pixel now, so it can't leave a ghost.
6. **Composite** (`Reflection_Composite.frag`), per sample, by each material's own reflectance.

**From this camera the fallback matters more than the rays.** The camera looks down, so a ray off the ground goes up and out of the screen. What most of wet ground shows isn't a ray's hit at all. So a ray that finds nothing doesn't give up:

1. **The rain shadow** says what's overhead even off the screen. `skyOpenness` walks the ray up through it, so a puddle under a tree shows the dark of the tree.
2. **The sky map**, clouds and all, at the surface's roughness.

**Rough surfaces never get this far.** `reflectionWanted` turns away anything too rough or thin to show its surroundings. Those reflect the sky's average in the ambient pass for one lookup, and the composite discards them after reading one texel: that's what brought the composite from 0.56 ms to 0.15 ms. Every pass has to ask `reflectionWanted` of the same stored values, or a pixel gets its reflection twice or not at all.

## How the game uses it

**Sound** is in `SoundscapeSystem` and `CharacterSoundSystem` (`Dino Defenders/Client/GameSounds.cs`). See `SOUND.md`.

**Wind**: `WeatherSample.Gale` multiplies the region's wind in `AmbienceSystem.ReadRegion`, so a storm's wind is the same wind bending the grass, and the wave rules still hold.

**Fog** is shaped by the weather each frame: see Fog above.

The server doesn't read weather. Nothing in the game's rules depends on it, so it costs the network nothing and can't cause a desync. If a rule ever does need it, `WeatherField` has no GL in it and works on a server.

## Checking it

```
dotnet "Dino WorldLab/bin/Release/net8.0/Dino WorldLab.dll" weather [--count N]    Hours of every kind of world: how often it rains, and anything a player would catch.
dotnet "Dino WorldLab/bin/Release/net8.0/Dino WorldLab.dll" weather --out file.png --seed N [--archetype id] [--clock s] [--every minutes] [--maps N]    A row of maps of a world's weather moving across it.
dotnet "Dino SoundLab/bin/Release/net8.0/Dino SoundLab.dll" check                  Rain's levels, spectrum and surfaces are in here with the rest.
```

Run `weather` after any change to the field, and `Dino WorldLab check` after any change to `TerrainSurface`.

For looking at it, on a scripted run:

```
--weather name          Holds it everywhere: clear, cloudy, overcast, wet, drizzle, rain, storm, snow, blizzard, dust, ash or fog.
--weatherclock s        Starts this far into the world's own weather.
--render reflectiondebug=N   1 where rays found something, 2 how much sky a ray can see, 3 everything reflected, 4 the sky map.
--render reflectionrays=0    Sky and rain shadow alone.
--hidden true           Draws with no window on screen and never takes the focus, for running beside someone playing.
```

`wet` is the best test of reflections: puddles with no rain falling on them. F3 has a Weather and a Reflections foldout, with every number here and a way to hold any weather. A shot says nothing about how reflections hold up moving: film it.

Values were tuned by screenshot, on one GPU. Nobody has played in any of this yet.
