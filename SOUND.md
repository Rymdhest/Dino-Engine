# Sound

How the engine makes sound, and how to add more. There are no audio files anywhere: everything heard is made by code while the game runs, and every take of a sound is different from the last.

## The one idea

**A sound is a recipe, not a recording.** A recipe is run again and again on worker threads, each time with new dice, so a footstep is never the same footstep twice. There are two kinds of sound, and which to use is decided by one question: does it have an end?

| | Takes | Live sounds |
|---|---|---|
| What | Things that happen: steps, blows, coins, clicks | Things that go on: wind, rain, fire, the sea, birds |
| Made | On worker threads, in advance, with all the time they need | On the audio thread, as they play |
| Class | `SoundDefinition` with a recipe | `LiveDefinition` with a `LiveSound` |
| Started with | `Audio.Play(Impacts.Flesh, position)` | `Audio.Start(Soundscape.Fire, position)` |
| Changes with | Pitch and level per playing | Parameters, eased towards, at any time |

```
   recipe ──> Take ──> mastering ──> SoundPool ─┐
   (worker threads, new dice each time)         │      SPSC queue
                                                ├──> Mixer ──> reverb ──> limiter ──> WASAPI
   LiveSound (audio thread) ────────────────────┘      (voices, buses, spatial)
```

The engine talks straight to WASAPI through COM interop, so sound adds no library and no native DLL, in keeping with the engine's rule. The device can vanish, change or never have existed: `AudioOutput` follows the default device, reconnects by itself, and with nothing to play on keeps time so the game can't tell.

## Adding a sound

Most sounds are one recipe and one line in a catalogue. Nothing is registered by hand: `SoundLab`, the F3 Sound window and the checks all find it.

```csharp
public static readonly SoundDefinition Anvil = Add("hit.anvil", take =>
{
    SoundRandom random = take.Random;       // This take's dice. Use nothing else.

    // A struck thing: modes and the noise between them, from what it's made of.
    var blow = new Blow { Material = Material.Iron, Shape = ModalShape.Bar, Fundamental = random.LogRange(900f, 1300f), Modes = 40, Rattle = 0.8f, Noise = -8f };
    take.Mix(Impact.Of(take, blow, 1.2f).Normalize(0f));

    // Every layer is a Signal: a chain of what's done to it, in order.
    take.Mix(take.Brown(0.2f).Lowpass(200f).Percussive(0.002f, 0.05f).Normalize(0f), 0f, -8f);
}, -14f);
```

Then play it: `Audio.Play(Impacts.Anvil, position);`. Then run `check`, and listen.

A new ground is one line in the table in `Footsteps.cs`. A new spell element is one case in each of `Magic.cs`'s three switches. A new material is one line in `Material`, and it works everywhere.

### The toolkit

Everything is in `Dino Engine/Sound/Synthesis/`. Each piece is the published model of that kind of sound, so reach for these before a bare filter.

| Use | For | From |
|---|---|---|
| `Impact.Of`, `ModalObject`, `Contact`, `Residual` | Anything struck: metal, wood, stone, glass, flesh | Modal synthesis (van den Doel, Kry and Pai), with the stochastic part from Aramaki and Kronland-Martinet |
| `Grains` | Crowds of small things: gravel, snow, coins, debris, crackle | PhISEM (Cook) |
| `Aero` | Anything swung or flying | Compact aeroacoustic sources (Selfridge, Moffat and Reiss) |
| `Liquid` | Water, in any quantity | Minnaert bubbles (van den Doel) |
| `Blast` | Explosions and thunder | The Friedlander wave, and N-waves along a tortuous channel (Few; Ribner and Roy) |
| `Vocal` | Throats | A glottal source through a formant tract |
| `Signal`, `Curve` | Whatever's left: filters, drive, envelopes, convolution | |

`Material` holds real loss factors, because how fast things ring out is most of how materials are told apart. Don't tune a metal by ear: move the loss factor.

## What makes a sound sound real

These were found by comparing synthesis with recordings, and each one fixed something that looked and sounded wrong.

- **Never leave clean lines on black.** Modes alone are sterile. Real impacts have a dense floor between their partials, which is why `Impact` adds `Residual` and why every take is placed on a small `Stage`.
- **A hit starts with a line up the whole spectrum.** That's `Impacts.Crack`, and it can be quieter than you'd think.
- **Offset the layers by milliseconds**, or they read as three sounds and not one blow.
- **Measure, don't guess, the band.** Real coins are almost all above 6 kHz, a sword clash above 4 kHz, a punch below 150 Hz with a broadband crack, thunder between 100 and 400 Hz. Guesses were an octave out.
- **Crackle is far sparser than you'd think.** A real fire's 4 kHz band has an envelope kurtosis near 900: bright, thin clicks with silence between.
- **Snow crunches in pieces.** A step is a handful of separate breaks reaching down to 100 Hz, not a hiss.
- **Nothing that goes on sits still.** Real wind swells by a third or more over seconds, between the gusts, which is what `Wander` is for.
- **A footstep is its middle.** A thud from 150 to 600 Hz says how hard the ground is, and what's above it says what the ground is made of. A thump underneath and hiss on top with nothing between is what steps once were here: every ground sounded alike, and a player said so at once. Keep a step short as well: most of it is over in a tenth of a second.
- **Wind is the plants, not the air.** Real wind in a wood has most of its sound under 2 kHz and falls away above 4. Filtered noise with its top left in hisses at any level, and turning it down only makes a quieter hiss. Leaves and blades are many small events, so a stronger wind gets busier as well as louder.

## Rules, and what breaks them

- **The audio thread never allocates, locks or waits.** `check` proves the mixer allocates nothing, with every live sound going. Anything a `LiveSound` needs is made in its constructor, which runs on the game's thread. A pause there is heard as a gap.
- **Draw from `take.Random`, nothing else.** One seed must give one take: `check` renders each twice and compares. Don't draw from a `WorldRandom` either, or every world after that point changes.
- **Don't bypass the mastering.** Takes are levelled by loudness (BS.1770), not by peak, so a footstep and an explosion can be compared. `Mastering.Target` is how loud a thing is.
- **Loudness in the mix is `Presence`, not gain.** It's how many dB a sound stands above an everyday one, and the mix makes way for the loudest thing happening, as in DICE's HDR audio. A level raised by hand only pushes things into the limiter.
- **Long, rare sounds take few takes.** A take in a pool costs memory for as long as the sound is in use: `size` shows what each costs. Two takes with `PlaysPerTake = 1` vary as much as ten, as each is replaced once it's been heard.
- **Sounds wake on first use.** Every sound has one take from the start, and the rest are made once it's asked for, so a catalogue can grow without the game paying for what it doesn't play.
- **Filters that sweep are `Svf`.** Biquads pop and blow up under modulation.
- **A recursive filter on the audio thread needs `AudioFormat.Denormal`,** or a tail fading out turns into very slow arithmetic. .NET can't turn denormals off. `check` runs every live sound at nothing for a while to catch this.
- **`Footsteps` come from the animation,** at phases 0 and 0.5 of the run cycle, so what's seen and heard can't drift apart. A new gait needs its own phases in `CharacterSoundSystem`.
- **Events are for show.** A `SoundEvent` can be missed like any `[NetEvent]`. What can be read from what's drawn, such as a swing starting or an enemy falling, is read from what's drawn: `CharacterSoundSystem` does this, and it can't be lost.

## How the game uses it

`Dino Defenders/Client/GameSounds.cs` holds all of it. `GameSounds` turns what the server says into sound, `CharacterSoundSystem` listens to what's drawn, and `SoundscapeSystem` reads the land.

**The ears** are just above the hero, turned the way the camera faces. The camera is 33 m up, so listening from there would put everything equally far away, but left and right have to be the screen's.

**A hero's own blow is heard before the server answers.** Clients draw the world about 90 ms behind, and a hit that late feels soft. `GameSounds.Predict` runs the same test the server is about to, and the `DamageEvent` that follows only adds what couldn't be known, such as a critical.

**The wind heard is the wind seen:** `SoundscapeSystem` samples `Wind.Sample` at the hero, speed and `Crest`, so a wave going through the grass on screen is heard going through it, and between waves the plants rest. Keep random movement in `WindSound` small, or it stops following what's seen. A light breeze should hardly be there: 3 m/s is about -51 LUFS, 8 m/s -39, 16 m/s -30.

**Water is looked for in twelve directions.** The sea is heard from up to 220 m where the world has surf, and each wave breaks when `WaterSingleton.ShorePhase` says one has reached the shore on screen. Any water at all within 46 m, pond, lake or calm bay, is heard lapping, harder as the wind raises it. `StillWater` worlds have lapping and no surf.

**What's underfoot** is `GroundUnder` in `CharacterSoundSystem`: water first, by depth (`Shallows`, then `Water` above the shin), then the painted surface, with sand near the water packed wet, a leaf layer under thick canopy, and bare ground where too little grass grows. A foot going in from dry land also makes `Splashes.Wade`. Most biomes are grass, so a world's variety underfoot depends on these rules more than on the table of surfaces.

Birds, crickets and frogs go by the warmth, wet and canopy of the place, and a world's birds are seeded from the world. Reverb follows the land: open ground, wood, crags, snow, a yard, stonework or a churchyard.

## Music

Music is composed while it plays, and no two worlds have the same score. It's in `Dino Engine/Sound/Music/`. The game only ever says what the moment is like:

```csharp
Music.Begin(worldSeed);             // One score per world, built on its own thread.
Music.Mood = MusicMood.Town;        // Follows at the end of the phrase, the next bar or the next beat, by what it is.
Music.Intensity = 0.7f;             // 0 to 1, live: adds players, loudness and a little speed from the next bar.
Music.Play(MusicCue.Victory);       // A few bars of its own, then back to what was going on.
```

**A score is a world's.** `Score` draws one key and one theme for a seed, and every mood is that theme in other clothes: the same shape of notes in another mode, metre and rhythm. That's what makes a town's dance and a boss fight sound like the same game. After a whole form the variations are written afresh, so it doesn't wear out, and the theme stays.

**A tune is written, not walked.** `Songwriter` writes a skeleton of one chord tone a bar, rising to a peak, then decorates it with one small figure per rhythm, which gives it motifs and sequences. It writes many and keeps the best by what real melodies do: a thousand folk tunes gave the targets in `Melody.cs` for steps, leaps, turning back after a leap, chord tones on strong beats, and how much bars repeat. Note by note, with no plan, a melody only wanders.

**Styles are tables.** A `Style` is a list of `Part`s: who plays, which `Textures` writer they use, from which intensity, and at what `Fader`. Adding a kind of music is adding an entry to `Styles`, as nothing else has to know. Intensity brings parts in, which is how games layer recordings, but these parts are written live, so they follow the harmony wherever it goes.

**Three things move the music.** `Composer` writes a bar just before it's due, so the music can answer the game: that's all of the adapting. Vertically, intensity decides who plays. Horizontally, moods decide what's played next: `Urgent` styles cut in on the next beat of the bar that's playing, `Prompt` ones at the next bar, and calm ones wait for a two bar line. And `Composer.Tide` gives the form a shape of its own on top of all this: the first statement holds back, the repeat has it all, the middle drops away and builds bar by bar, and the return lands.

**A change is played, not cut.** `Bridge` writes what goes between two styles, and everything in it leans on what's next. Into a fight there's a bar counted in at the fight's speed: a roll closing up on the fifth, low strings and horns swelling, a cymbal timed to peak on the downbeat, violins running up to where the tune starts, while what was playing dies away under it (`Orchestra.Cut` fades, it doesn't stop). Anything else slows or quickens over a bar in which the old style's accompaniment keeps going and thins out, its rhythm the old style's and its harmony already the new one's, which is how a band modulates. A pivot chord (`Bridge.Pivot`) rises under that and goes on sounding, by its `Note.Tail`, under the new style's first bars, the new style's own plucked players turn the page, and whoever takes the tune leads into it. The new style then builds up over `ArrivalBars`. Tempo glides across a bridge, and into a fight the old beat's half or third is taken as the pulse, as 72 and 138 are nearly one and two. `MoodHold` stops the music following every flicker: a fight goes straight through, a calm style has to be wanted for a moment and what's just arrived stays a while.

**Instruments are measurements.** `MeasuredSpectra.cs` is the SHARC analysis of real players, by register, written by a script: don't tune it by hand. `Timbre` turns it into wavetables at three loudnesses, as a horn's eighth harmonic gains 40 dB from pianissimo to fortissimo and its fundamental hardly moves, so a louder note is a different sound and not the same one turned up. Sections are several players with their own pitch and timing. Plucked strings are Karplus-Strong loops started by the pulse a real pluck makes, as noise is far too bright. Percussion is made with the same toolkit as sound effects.

**A sequencer plays what's written; `Performer` plays it.** Phrases arch and slow at their ends, strong beats lean, short notes get shorter, and timing drifts as a player's does, each moment close to the last. These are the KTH rules, and listeners measurably prefer that drift to the same amount of random jitter.

**It can't hurt the rest.** The band runs on its own thread, about 0.3 s ahead, and `MusicStream` only reads what's ready. If it ever fell behind there would be a gap in the music (`Music.Gaps`) and every other sound would carry on.

`MusicDirectorSystem` in `Dino Defenders/Client/MusicDirector.cs` is the game's side: it reads what this player has been told, so it's a client's business, and works out threat, boss, hurt and place. Intensity goes up fast and down slowly, as a fight that flickers shouldn't make the music flicker. Night outranks unease, or a churchyard, where something is always near, would never sound like one. A scripted run's last line lists every mood it reached, as where it ended up says little.

Rules for music:

- **`Level` is how hard it's played, `Fader` is where it sits.** Level changes the timbre, so use `Fader` to balance. Played level with the rest, a melody came out the quietest thing in the band, and `musiccheck` now fails that.
- **Keep the drums' low end down.** Left alone, timpani and bass drum were the whole of the bottom two octaves and drove the limiter down on everyone at each stroke.
- **Stay in range.** `Textures.Ranges` is what the instrument can play, and `musiccheck` fails anything outside it. Timpani really don't go down to C2.
- **What sounds together must agree.** Parts follow one chord, and chords can only be coloured where it's sweet: a ninth on a minor chord lands a semitone from its third. A phrase that ends off the tonic is a half close, and its tune has to stop on a note of that chord.
- **Nothing stops at a handover.** Stopping the band and starting a thin pad in its place measured worse than the hard cut it replaced: 22 dB gone in a second. What was keeping time plays through.
- **In a bridge, long notes belong to the pivot chord.** In slow music a passing note lasts long enough to grind on it, and `musiccheck` counts held semitones.
- **Don't recreate a real piece or game's music.** Styles are written from what's common to a genre, and the corpus only gave aggregate statistics: no tune, phrase or rhythm from it is in the code.

## Checking it

`Dino SoundLab` needs no window. After any change to sound, run `check`. It's everything that can be known without ears, and a sound that fails it is wrong for certain, but a sound that passes it may still be bad, so listen as well.

```
dotnet "Dino SoundLab/bin/Release/net8.0/Dino SoundLab.dll" check              Clicks, clipping, DC, NaN, loudness, variation, cost, and what the mixer promises.
dotnet "Dino SoundLab/bin/Release/net8.0/Dino SoundLab.dll" render --out folder [--only text] [--takes N]     WAV files to listen to.
dotnet "Dino SoundLab/bin/Release/net8.0/Dino SoundLab.dll" live --out folder [--only wind] [--set 16,0.6,0.1,0,1] [--seconds N]
dotnet "Dino SoundLab/bin/Release/net8.0/Dino SoundLab.dll" sheet --out sounds.png   Every sound as a labelled spectrogram.
dotnet "Dino SoundLab/bin/Release/net8.0/Dino SoundLab.dll" size               What every sound costs in memory.
dotnet "Dino SoundLab/bin/Release/net8.0/Dino SoundLab.dll" device             Plays quietly on the real device, and proves by loopback that it arrived.
dotnet "Dino SoundLab/bin/Release/net8.0/Dino SoundLab.dll" musiccheck [--count N] [--transitions N]     Melodies against real ones, every note in range, clashes, adapting, balance, cost, over N worlds, and every change of style over N more.
dotnet "Dino SoundLab/bin/Release/net8.0/Dino SoundLab.dll" music --mood Town --to Battle --at 14 --back 34 --seconds 54 --out file.wav      A change of style and back, to hear a bridge.
dotnet "Dino SoundLab/bin/Release/net8.0/Dino SoundLab.dll" music --mood Boss --intensity 0.8 [--seed N] [--seconds N] [--solo Horns,Trumpets] --out file.wav    Also writes every note as a .csv.
dotnet "Dino SoundLab/bin/Release/net8.0/Dino SoundLab.dll" music --story true --out file.wav      Through wandering, unease and a fight. --sweep true runs intensity from 0 to 1.
dotnet "Dino SoundLab/bin/Release/net8.0/Dino SoundLab.dll" tunes [--mode dorian] [--show N]      Melodies alone, with how they score against the corpus.
```

`musiccheck` proves music isn't plainly wrong, and says nothing about whether it's good. Run it over more worlds than seems needed: clashes that only come up once in twenty worlds did. To judge a mix, compare its long-term spectrum with an orchestra's, render each section with `--solo`, and look at how far the level swings: a fight that stays within two decibels is a wall, however good each part of it is.

In the game, F3 opens the **Sound** window: every sound at a click, every live sound with its parameters, bus levels, and the meters. A scripted run keeps quiet unless given `--sound true`; `--volume v` sets the level, and `--record file.wav` saves exactly what the mixer sends out, which is the way to hear or measure a whole mix after the fact. Its last lines say what it read off the land and how many steps were heard on what. The bot gets about by being moved, so to prove anything about feet use `--bot true --walk x,z`, which runs the hero straight there: from a beach into a lake it goes Sand, WetSand, Shallows, Water.

A spectrogram is the best thing there is for judging a sound with no ears, so when one looks wrong, believe it. Compare with real recordings where you can, by long-term spectrum and envelope statistics. Those are what a texture is recognised by.
