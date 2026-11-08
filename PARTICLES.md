# Particles

Fire, smoke, spells, blood, frost, lightning and venom: everything in the game that's made of particles, how it works, and how to add more.

Status: built 2026-11-08, measured, and looked at in stills from hidden runs. **Nobody has watched it move.** See "Where it stands" at the end.

## The one idea

**An effect is a recipe, and a particle lives on the GPU.** A recipe (`ParticleEffect`) is layers (`ParticleEmitter`), each of one kind of particle (`ParticleStyle`), and it fills whatever shape it's given at whatever scale: a point, a ball, a body, or any area of ground. So one burning recipe sets a torch, a body or a whole field on fire, and a wider fire has more flames, not bigger ones.

The CPU only decides when a particle is born, where, and for how long. It keeps the slot and the hour of death (`ParticlePool`), and the GPU does everything else. A life is fixed at birth, so nothing is ever read back. Nothing in the rules may read a particle, and it's all this machine's alone, like wildlife.

```
  the game: Particles.Play, Particles.Begin, ParticleEffectComponent, Arcs.Strike, AssetLight.Burning
                                   |
                            EffectRun.Step                 what each layer is due, and where        (CPU)
                                   |
                             ParticlePool                  slots and deaths, nothing read back      (CPU)
                                   |
   ParticleRenderer:  spawn -> simulate -> sort -> draw what lies on the ground -> draw the rest     (GPU)
                                   |
                      flame and smoke flipbooks, grown at load (ParticleBaker)
```

`Dino Engine/Particles/` is headless, and so is its content, which is what lets `Dino FxLab` run every effect with no window.

## Where the ideas came from

| Source | What's used |
|---|---|
| Unity VFX Graph, six-way lighting (Unity blog and manual, 2023) | Smoke lit from six baked lightmaps, one per axis, so a puff is lit by a light from any side, with its own creases and self-shadow |
| Wrenninge, Kulla and Lundqvist, *Oz: The Great and Volumetric* (SIGGRAPH 2013) | Multiple scattering as a few octaves of single scattering, each with less extinction, so thick smoke isn't black inside |
| Henyey and Greenstein 1941 | The phase function in the bake: smoke scatters a little forward |
| Stam 1999, *Stable Fluids*; Fedkiw, Stam and Jensen 2001; Selle et al. 2008 (MacCormack) | The 2D fire simulation the flames are grown from: semi-Lagrangian advection corrected by MacCormack, vorticity confinement, a pressure solve |
| Nguyen, Fedkiw and Jensen 2002, *Physically based modeling and animation of fire* | Fuel burning into heat, buoyancy from temperature, cooling by radiation (T⁴) |
| Planck's law; Wyman, Sloan and Shirley 2013, *Simple analytic approximations to the CIE XYZ colour matching functions* | Fire's colour is a black body's, from 800 K to 2200 K (`Blackbody.Chromaticity`) |
| JangaFX EmberGen breakdowns (80.lv) | A realistic flame is temperature through a black-body ramp, with smoke kept apart and lit |
| Bill Rockenbeck, *inFAMOUS Second Son particle system architecture* (GDC 2014), and Ghost of Tsushima's wind (GDC 2021) | Particles simulated on the GPU, and carried by the same wind as the grass |
| Wicked Engine's GPU particles; AMD's compute particles (GDC 2014) | Spawn, simulate and sort in compute, one instanced draw |
| Ramy El Garawany, Uncharted 4 particle lighting (SIGGRAPH 2016) | Lighting worked out once a particle, not once a pixel |
| Lorach, *Soft Particles* (NVIDIA 2007) | Fading where a particle meets the scene |
| Kim, *Screen Space Decals in Space Marine* (SIGGRAPH 2012) | What lies on the ground is painted on whatever the depth buffer holds there, grass included |
| GDC 2017 *Visual Effects Bootcamp* | Read first, detail second: an effect's shape and value have to read at a glance |

## A frame

`ParticleEffectSystem` runs every effect: those on entities follow them, and those playing on their own run until they're over. A lasting effect emits nothing while it can't be seen, and when it comes back into view it fills in what would be there (`EffectRun.FillIn`), so walking up to a chimney doesn't show its smoke starting.

`ParticleRenderer` then, after the fog's composite:

1. **Spawn** scatters this frame's new particles into their slots.
2. **Simulate**, one thread a slot up to the highest in use: the wind map (at the speed its patterns go, `WindField.PatternSpeed`, so a gale leans a flame further rather than making it frantic), a curl field of swirls, drag, gravity, buoyancy that fades as heat goes, and the ground. A particle only ever asks about the ground where it was born, as a plane, so it can bounce, stick or vanish, and what lands lies down. Each particle's light is worked out here: the sun through the shadow cascades and the clouds, the 32 local lights nearest the hero, and the sky and ground, into six directions packed in halves.
3. **Sort** by distance, bitonic, 512 at a time in shared memory and then across them, with what lies on the ground first.
4. **Draw** in two instanced calls, premultiplied, into the multisampled scene. First what lies on the ground, with no depth test, painted on whatever the depth buffer holds within `DECAL_THICKNESS` of the ground and facing up, so grass doesn't hide a burning patch and it doesn't climb whoever stands in it. Then everything else, tested against the scene and soft where it meets it. Both are hazed by distance as the scene is.

## What a particle can be

- **Smoke** (`ParticleSprite.Smoke`): puffs grown at load, four of them, 32 frames each. Each is a pyroclastic volume (billows of Worley noise) marched along each axis into six lightmaps, a density and a core that fire lights from inside. A frame swells into the next, so they're read where the smoke was. **Smoke thins as it spreads**: its `Erosion` takes from the depth through it (alpha is 1 − e^−depth), never from its alpha, as a puff is nearly solid inside and taking from its alpha left a hard-edged disc of card.
- **Flame** (`ParticleSprite.Flame`): a fluid simulation run at load into looping flipbooks, a hearth's and a wick's, 48 frames each, and three tongues that break off and burn out. A frame keeps how much is burning, how hot times that, and which way it's flowing, so that a mip keeps its temperature, and frames are blended along the flow. It's drawn through the Fire palette, a black body's colour. Flames stand upright on their fuel and lean with the air (`ParticleFacing.Upright`), and are drawn a little nearer the eye (`Toward`) so the ground under them doesn't cut their foot.
- **Glow**, **Streak**, **Glint** and **Droplet**: a soft light, a spark drawn out along its path, a twinkle, and a drop that splashes into a decal where it lands.
- **Arc**: lightning, a jagged line by midpoint displacement with forks (`Arcs.Strike`), each piece a segment with a white core in a glow of its colour.
- **Decal**, by `DecalPattern`: a splash (blood, venom), a bed of coals, rime, a venom pool, a ring going out, scorch.

## Light and colour

A style either **glows** or **is lit** (`Lit`). What glows is a palette (`ParticlePalette`) times its `Emission`, which is 1 unless it says otherwise; what's lit is its `Colour` as an albedo under a sun of about 13, and 0 emission unless it says so (smoke over a fire glows a little underneath). Palettes are radiance, deep and saturated, as anything pale goes white once mapped. With sun, sky, local lights and emission all off (`--render sun=0,sky=0,lights=0,emission=0`) a frame full of effects is black, which is the test for anything new.

**Whatever glows also shines**: an effect can have an `EffectLight`, which goes to the lights, the fog, the water and the particles. A light costs its circle on screen, so `ParticleEffectSystem` gives only the `MostLights` (12) nearest the hero, and fades those past them out towards where the last one kept is, which moves smoothly however things move. A flame that's part of a model, a candle's, a torch's or a brazier's, is `AssetLight.Burning` in the generator, and `TerrainChunkSystem` brings it in and out with its object, by its width.

## Scale

Everything is in metres at the world's scale, and a person here is about 4.4 m tall. The camera is some 33 m from the hero and 67° up, where a metre is about 30 pixels, so judge any effect from there, and a detail under a pixel at the closest zoom is a fault. `Fire.CandleWidth`, `TorchWidth` and `BrazierWidth` say how wide each flame is. Rates can be per square metre or per metre of height (`RateBasis`), and an emitter that `Spreads` fills a bigger ball or area with more of the same (`EffectRun.SpreadRadius`), while one that `Fills` is sized to it, as a scorch is. Coal beds have chunks the same size in any bed (`COAL_CHUNKS` to a metre), or a wide burning patch read as lava tiles.

## Using it

```csharp
// Something that happens once, which runs itself until it's over.
Particles.Play(Fire.Burst, position, scale, EffectShape.Ball(radius));

// Something that goes on, which whoever started it moves, and stops.
EffectRun run = Particles.Begin(Fire.Burning, position, EffectShape.Body(radius, height));
run.Position = where; run.Velocity = howFast;
Particles.Stop(run);    // what it made lives out its life

// An effect on an entity, for as long as it's there.
new ParticleEffectComponent(Fire.Ground, EffectShape.Over(area));

// A whole area on fire: any IEffectArea. The game's GroundArea is a GroundShape, the same one a fight's marks are.
EffectShape.Over(new GroundArea(GroundShape.Circle(centre, 6f)));

// A flame that's part of a model, in its generator.
lights.Add(AssetLight.Burning(at, width));
```

In the game, `ParticleShow` says what each kind of damage looks like (a burst, a patch of ground, a bolt's trail, a blow landing), what a blow on flesh, bone or armour throws off (`Wound`), and how big a body is (`BodyOf`). `Ailments` keeps an effect on everyone suffering from something, sized to them. `GhostPresenter` starts them all from what the server says: a zone gets its element's `Ground` over its own shape, a missile its `Bolt`, a blast its `Burst`.

## Adding to it

A style or an effect is a `public static readonly` field in a `[ParticleCollection]` class in `Particles/Content/`, found by reflection with nothing to register, and named for its class and field: `Fire.Campfire`. Styles are given an index as they're first used, `ParticleStyles.Most` (256) at most, the `Glows` four to a palette among them.

- Give an effect a `Reach` that covers where its particles go, smoke included, or it's culled while it's still on screen.
- What a player has to see, blows and blasts, is `ParticlePriority.Vital`. What can go first when the pool is full is `Ambient`. The pool is 65,536 particles: Ambient may fill 60% of it, Normal 92%.
- A new effect gets a budget in `Dino FxLab` (`BudgetOf`), or the generic one.
- New smoke that the wind carries gets drawn out by it (`EffectRun.DrawnOut`): it comes out as often as the air carries it a third of a puff's width, each puff fainter by the square root of that, or a plume is a row of balls.

## Rules

- Nothing in the rules reads a particle, and nothing is read back from the GPU. A particle's life is fixed when it's born.
- Nothing draws across the whole picture: no heat haze, no full-screen smoke (read Clarity in `WEATHER.md`).
- Smoke thins its depth, never its alpha.
- A decal's card lies on the ground and reaches past it only as far as the slant it's seen at needs (`DECAL_MOST_SLANT`): its fragments are what that pass costs, and a fixed 2.4 m once made it most of the particles' time. Shapes that wander, a spill or a pool, wander only inwards from the card's edge, and everything fades out before it, or a decal is cut round.
- A venom pool is dark, and lit only in veins and bubbles: lit all over, it read as green paint.
- A flipbook keeps temperature times coverage, never temperature alone, or its mips go wrong at the edges.
- How `Particle_Simulate.comp` moves a particle (drag towards the wind at its patterns' speed, buoyancy that fades, gravity) has to agree with `EffectRun.Drift` and `DrawnOut`, which work out the same on the CPU to fill in a lasting effect coming into view and to draw a plume out: change them together.

After any change here run `dotnet "Dino FxLab/bin/Release/net8.0/Dino FxLab.dll" check`, look at it in the game (`--fx Fire.Campfire,Fire.Ground --look 18`, `--particlesheet folder` for the grown flipbooks), and measure with `--perf` at a camp (`--site BanditCamp`) against the build before.

## Tools

- `--fx a,b` (or `all`) stands effects in rows in front of the hero: lasting ones go on, bolts go round in a ring, bodies are a person's shape, ground is a 6 m circle, and the rest happen every few seconds (`EffectStage`). `--fxat x,z` is where the first of them stands, in metres across and away from the camera.
- `--particlesheet folder` writes the grown flames, smoke and smoke lightmaps as pictures.
- `Dino FxLab check`: every recipe makes sense, everything dies once stopped, no effect is over its budget in still air or a gale, and a fight at its worst (thirty foes burning, six patches of ground alight, eight blasts a second) fits in half the pool. It proves nothing about how anything looks.
- The perf report counts `Particles` alive each frame, and times them as one GPU pass.

## What it costs

At 3440x1369 on a Radeon RX 7900 XTX, old build and new run one after the other:

| Where | Before | After |
|---|---|---|
| A bandit camp, three campfires with smoke and coals | 7.55 ms a frame | 7.64 ms, of which particles 0.12 to 0.14 ms |
| Fourteen effects in one place, every element's ground, bursts, a burning body | | particles 0.37 ms, and their seven lights 0.47 ms |
| No effects in view | 8.90 ms | 8.90 ms |

Growing the flipbooks takes about 0.2 s while the game loads. A candle is about five particles and a campfire under a hundred, even in a gale.

## Considered and not done

- **Particles at half resolution**, off screen (Cantlay, GPU Gems 3): the whole pass is a tenth of a millisecond to a few tenths, and an upsampled edge against multisampled geometry costs more to get right than it would save.
- **Order-independent transparency** (McGuire and Bavoil 2013): smoke needs its order to look like smoke, and sorting is cheap at these counts.
- **Heat haze**: it distorts the whole picture, which seeing the game comes before.
- **Lighting per vertex on tessellated particles** (Frostbite): at these sizes, light once a particle in six directions is enough.

## Where it stands

Built in one session on 2026-11-08 and judged from stills of hidden runs: the campfire, a torch, candles in a churchyard at night, each element's burst, ground and bolt, blood, burning ground on sand and in grass, windblown smoke. Every check passes, and the measurements are above. **Nobody has watched it move**, and motion is most of what fire and smoke are: look at it moving first (`--film`), from the game's own camera, and in a real fight.
