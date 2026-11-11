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

**The rain heard is the rain seen:** `SoundscapeSystem.ReadRain` reads `Weather.Here`, and what it lands on from the same canopy and shore it reads for the wind and the water. It's a steady wash with near drops laid over it, and most of how it changes is what it's landing on: a low patter on the ground, brighter with separate ticks on leaves, a softer rush with bubbles on water. Keep its top down, and it has to sit under a fight: a shower is about -40 LUFS, a downpour -28, and `check` holds it there. As first written it was louder than a gale, and sounded alike on everything.

**Thunder comes after the flash** by what sound really takes, 3 seconds a kilometre, as `UpdateThunder` queues every bolt the weather lets fall. Near ones are the strike itself, further ones only what's left of it, the low roll. All of it reads `WeatherSingleton.Struck`, so what's heard is the very bolt everyone in the world saw.

**Drips** are what's left to hear when the rain stops: `ReadDrips` follows the rule they're drawn by, near trees or anything solid, for as long as things stay wet. `DripSound` is a few places with a beat of their own rather than a texture, tapping on what they land on and plinking in standing water, and `check` holds them separate and well under a drizzle.

**What's underfoot** is `GroundUnder` in `CharacterSoundSystem`: water first, by depth (`Shallows`, then `Water` above the shin), then the painted surface, with sand near the water packed wet, a leaf layer under thick canopy, and bare ground where too little grass grows. A foot going in from dry land also makes `Splashes.Wade`. Most biomes are grass, so a world's variety underfoot depends on these rules more than on the table of surfaces. Then `RainedOn` has its say: earth goes to mud and sand packs down once the ground is wet, and a foot that lands where a puddle has filled lands in water, by the rule the ground is painted with, so a puddle that's seen is one that's heard.

**A good find is known by ear.** `Treasure.DropRare` and `DropUnique` are the one kind of sound here that doesn't wander in pitch, as they have to be learned like a name: the same notes every time, so that the sound alone says what's there. Each take still rings differently. They're for the moment a find lands, and what comes down also says what it is, by `LandMetal`, `LandJewel` or `LandCloth`. A unique's sound starts as it leaves the ground and breaks at `UniqueStrike`, which is also how long it takes to fall, and `check` proves it does. `Reached` plays the pack's sound the moment a player takes something, as a click that answers late feels broken.

**Dinosaurs roar as the creatures of films do**, not as the evidence says they might have: the bones say birds and crocodiles boom with their mouths shut, but these are the Reich's animals, bred and wrong (read `LORE.md`). So a tyrant's roar is a throat far bigger than any living thing's with a shriek riding on it, as film roars were built from an elephant's trumpet over a tiger and an alligator, and it's heard 160 m away. A raptor honks and hisses, a skitter chirps, a hornback bellows, the rotting hiss wetly, and bones moan as the dead do. They're in `Sound/Content/Saurians.cs`, and `GameSounds.DinosaurVoice` picks one from a model's species and what's been done to it, for when it notices someone, strikes and dies, and one seen roaring is heard roaring (`CharacterSoundSystem.Roars`). Ordinary ones are about as loud as the beasts were, as raptors are most of what anyone meets: a fight against a pack of them was once 6 dB louder than one against wolves and a bear, and touched the limiter, and now it's within what its extra blows account for. Only a tyrant is louder, as it's a land's boss. **Anything big shakes the ground**: a step from legs more than 1.6 times a person's (`GiantStride`) adds `Saurians.Tread` under the ordinary one, louder and lower the bigger it is, so a tyrant is heard coming before it's seen. None of these has been heard by a person.

**The Haven's giants boom as the evidence says**, with their mouths shut, as birds and crocodiles do, because they aren't the Reich's and aren't bred and wrong (`LORE.md`). They're in `Sound/Content/Giants.cs`: a call is a long boom swelling in the chest, a throat behind a shut mouth muffled by the body round it, throbbing a little as the air in it pulses, now and then with a shorter one leading in and a breath let out after, heard 320 m off (`Giants.Call`). A step is a thud deeper and longer than a tyrant's, with the ground ringing after it, turf giving and grass crushed (`Giants.Step`), and a pull at a tree is its leaves thrashing and springing back, the branch creaking by stick and slip, and now and then a twig going (`Giants.Crop`). `GiantShow` plays them where they happen, read off the bodies that did them. Nobody has heard them.

Birds, crickets and frogs go by the warmth, wet and canopy of the place, and a world's birds are seeded from the world. Birds and crickets stop for rain, and frogs start. What's seen alive has its own sounds at its own place: a flock going up is `Life.WingsFlock`, which is wing beats, many small events and not a hiss, and crows, gulls and frogs have `Life.Caw`, `GullCry` and `Plop`. `WildlifeSystem.Sound` spaces each kind out, so a hero going through a village isn't a storm of pigeons. Reverb follows the land: open ground, wood, crags, snow, a yard, stonework or a churchyard.

## Music

Music is composed while it plays, and no two lands have the same piece. It's in `Dino Engine/Sound/Music/`. The game only ever says where it is and what the moment is like:

```csharp
Music.Begin(place);                 // A land, the Haven or an arena, each with a score of its own, built on its own thread.
Music.Mood = MusicMood.Boss;        // At once for a fight; for anything else when the tune can stop.
Music.Intensity = 0.7f;             // 0 to 1, live: adds players, loudness and a little speed from the next bar.
Music.Play(MusicCue.Victory);       // A few bars of its own, then back to what was going on.
```

**Where it changes, and where it doesn't.** A person asked for this, and it's the rule: a land plays one piece of its own the whole time the hero is in it, whatever's going on. Only its tyrant changes it, once the hero is inside the keep's walls or down on its arena's floor, and it comes back when the tyrant falls, after a few bars of victory. Every tenth level's arena is one long fight from the start. The Haven has a piece of its own, the same on every visit, quieter at night, and it's what plays behind the main menu. `MusicDirectorSystem` in `Dino Defenders/Client/MusicDirector.cs` is the game's side, on the client only: it works out a `MusicPlace` from the land's biomes, weather and level, and the mood from where the hero is. Its keep has two edges, 6 m past the wall to come in and 22 m to go out, so walking along a wall can't flick the music.

**Every land is different.** `Arranger` draws a land's piece from its seed. There are five kinds (`Idiom`), each what's common to a whole genre and never any one piece:

| | Built from | Where it leans |
|---|---|---|
| Reverie | A piano alone, a soft synthesiser under it, ninths in its chords, a sparse tune and silence between phrases | Bright and cold lands |
| Hearth | A fingerpicked twelve-string, a flute, bass flute, ocarina, fiddle or clarinet, hand drums, a dulcimer, an echo on it all, often in three | Warm lands |
| Vigil | Muted strings holding a drone, a solo cello, English horn or bass clarinet lamenting over it, voices far off, a drum off the beat | Grim lands |
| Wonder | A hymn in the low strings, clarinets, bassoons and horns moving with the tune, to the fourth and home, with a borrowed flat seventh, the voices coming in late | Open lands |
| Drift | Short phrases going round on loops of their own lengths, as Eno's tape loops did, never meeting the same way twice | Anywhere |

A second kind lends the first one or two players late in the form. On top of that the seed draws the key, mode, tempo, metre, harmonic language (`HarmonicLanguage`), who sings the tune and who answers, the accompaniment's figures (`Textures.PianoHands`, `Fingerpick`, `Hymn`, `Drone`, `Echo`, `Loops`, `Sparkle`), the drums' rhythms as Euclidean patterns (`Textures.Euclid`), the room and the echo (`Staging`), and a composer's own taste for each land's tunes (`Taste`). A land's character only changes the odds, so a grim land can still have a bright piece. Over 1000 lands `musiccheck` found no two with one tune and no two with the same band playing the same figures in the same key, mode, metre and speed.

**A score is a land's.** `Score` draws one key and one theme for a place, and its tyrant's fight is that theme in other clothes: the same shape of notes in a minor mode over driving strings, war drums and low brass circling a semitone (`Textures.Prowl`), with the land's own lead taking the middle. An arena's fight is bigger again, and coloured by its kind: the colosseum's fanfares, the cage's hand drums, the sun-temple's Phrygian dominant, the dig's buried voices, the cathedral's choir and bells. After a whole form the variations are written afresh and the tune passed to someone else (`Arranger.Rotate`), so a long stay keeps moving while it stays itself.

**A tune is written, not walked.** `Songwriter` writes a skeleton of one chord tone a bar, rising to a peak, then decorates it with one small figure per rhythm, which gives it motifs and sequences. It writes many and keeps one of the best by what real melodies do: a thousand folk tunes gave the targets in `Melody.cs` for steps, leaps, turning back after a leap, chord tones on strong beats, and how much bars repeat. Rhythms can rest, as slow piano music and a lament leave room. It takes one near the best by chance, not the best: the single best of hundreds turned out to be the same tune in unrelated lands.

**Harmony speaks a language.** `Harmony.Write` draws from tables written in the plain major and minor, by what film and game music does: a hymn goes to the fourth and home and borrows a flat seventh, ambient music holds two soft chords a phrase with ninths in them, dread stays on the tonic with the flat second and sixth leaning on it, a fight drives down by thirds to a dominant. A borrowed chord (`Chord.Borrow`) takes its whole bar into the mode it comes from, the tune included, so nothing grinds. Where a piece's own mode makes a table's chord diminished, as the dorian does of the flat sixth, the chord is taken from the plain mode instead.

**Three things move the music.** `Composer` writes a bar just before it's due. Vertically, intensity decides who plays. Horizontally, the mood decides what's played next: a fight cuts in on the next beat of the bar that's playing, and anything else waits for a two bar line. And `Composer.Tide` gives the form a shape of its own: the first statement holds back, the repeat has it all, the middle drops away and builds bar by bar, and the return lands.

**A change is played, not cut.** `Bridge` writes what goes between two pieces, leaning on what's next. Into a fight there's a bar counted in at the fight's speed: a roll closing up on the fifth, low strings and horns swelling, a cymbal timed to peak on the downbeat, violins running up to where the tune starts, while what was playing dies away under it. Out of one, the fight's accompaniment slows and thins with its harmony already the land's, a pivot chord (`Bridge.Pivot`) rises under it and sounds on into the land's first bars, and the land's own players lead in. `MoodHold` keeps calm changes from following every flicker.

**Instruments are measurements.** `MeasuredSpectra.cs` is the SHARC analysis of real players, by register, written by a script: don't tune it by hand. `Timbre` turns it into wavetables at three loudnesses, as a louder note is a different sound and not the same one turned up. Sections are several players with their own pitch and timing. Plucked strings are Karplus-Strong loops started by the pulse a real pluck makes; a twelve-string's every note is two strings, an octave apart on its lower courses, met a moment apart and never quite in tune. The piano (`PianoVoice`) is modal, after Bank, Zambon and Fontana: its partials stretched by the string's stiffness as measured across a concert grand, the hammer taking out the partials it strikes and leaving more top the harder it's struck, a second string beside each low partial a fraction of a cent away for its double decay and beating, and dampers below F6. Percussion is made with the same toolkit as sound effects.

**A sequencer plays what's written; `Performer` plays it.** Phrases arch and slow at their ends, strong beats lean, short notes get shorter, and timing drifts as a player's does. These are the KTH rules.

**It can't hurt the rest.** The band runs on its own thread, about 0.3 s ahead, and `MusicStream` only reads what's ready: if it fell behind there'd be a gap (`Music.Gaps`) and every other sound would carry on. Something that isn't a number never leaves a mix once it's in, so the band drops any note that isn't made of numbers, starts its room afresh if one ever gets through (`Orchestra.Faults`), and the mixer silences and names a live sound that makes one (`Mixer.Faults`): a scripted run's report says so. One such fault was seen once in the Haven, before these guards, and never again in a dozen runs, its cause not found.

Rules for music:

- **A land's music doesn't follow what's happening in it.** Only the tyrant changes it, at its keep, and the Haven and arenas have their own. Don't add a mood without asking: a person found the music changing with every situation too much.
- **`Level` is how hard it's played, `Fader` is where it sits.** Level changes the timbre, so use `Fader` to balance, and keep whoever has the tune above every other group: `musiccheck` fails it.
- **Keep the drums' low end down.** Left alone, timpani and bass drum were the whole of the bottom two octaves and drove the limiter down on everyone at each stroke.
- **Stay in range.** `Textures.Ranges` is what the instrument can play, and `musiccheck` fails anything outside it, against a list of its own.
- **What sounds together must agree.** Parts follow one chord. A voicing never puts two notes a semitone apart, as a major seventh's root does over its seventh, and an accompaniment keeps under the tune where its own part really plays it (`BarContext.MelodyShift`).
- **Nothing stops at a handover.** What was keeping time plays through, and long notes in a bridge belong to its pivot chord.
- **Variety is checked, so keep it so.** A new kind of piece or figure should add choices, not take them away: `musiccheck` fails two lands with one tune or one arrangement.
- **Don't recreate a real piece or game's music.** The kinds of piece are what's common to a genre, the references for them were read about and never listened to or transcribed, and the corpus only gave aggregate statistics.

## Checking it

`Dino SoundLab` needs no window. After any change to sound, run `check`. It's everything that can be known without ears, and a sound that fails it is wrong for certain, but a sound that passes it may still be bad, so listen as well.

```
dotnet "Dino SoundLab/bin/Release/net8.0/Dino SoundLab.dll" check              Clicks, clipping, DC, NaN, loudness, variation, cost, and what the mixer promises.
dotnet "Dino SoundLab/bin/Release/net8.0/Dino SoundLab.dll" render --out folder [--only text] [--takes N]     WAV files to listen to.
dotnet "Dino SoundLab/bin/Release/net8.0/Dino SoundLab.dll" live --out folder [--only wind] [--set 16,0.6,0.1,0,1] [--seconds N]
dotnet "Dino SoundLab/bin/Release/net8.0/Dino SoundLab.dll" sheet --out sounds.png   Every sound as a labelled spectrogram.
dotnet "Dino SoundLab/bin/Release/net8.0/Dino SoundLab.dll" size               What every sound costs in memory.
dotnet "Dino SoundLab/bin/Release/net8.0/Dino SoundLab.dll" device             Plays quietly on the real device, and proves by loopback that it arrived.
dotnet "Dino SoundLab/bin/Release/net8.0/Dino SoundLab.dll" musiccheck [--count N] [--transitions N] [--part variety]     Melodies against real ones, the piano measured, every note in range, clashes, how different lands are, adapting, balance, cost, and fights in and out.
dotnet "Dino SoundLab/bin/Release/net8.0/Dino SoundLab.dll" music --land N [--mood Boss] [--story true] --out file.wav      The Nth of the lands the checks use. Also --place haven, --place arena --arena Cathedral, or --seed N --dark 0.7 --cold 0 --open 0.4.
dotnet "Dino SoundLab/bin/Release/net8.0/Dino SoundLab.dll" tunes [--mode dorian] [--show N]      Melodies alone, with how they score against the corpus. music also takes --intensity, --seconds, --solo Horns,Trumpets and --sweep true, and writes every note as a .csv.
```

`musiccheck` proves music isn't plainly wrong, and says nothing about whether it's good. Run it over more worlds than seems needed: clashes that only come up once in twenty worlds did. To judge a mix, compare its long-term spectrum with an orchestra's, render each section with `--solo`, and look at how far the level swings: a fight that stays within two decibels is a wall, however good each part of it is.

In the game, F3 opens the **Sound** window: every sound at a click, every live sound with its parameters, bus levels, and the meters. A scripted run keeps quiet unless given `--sound true`; `--volume v` sets the level, and `--record file.wav` saves exactly what the mixer sends out, which is the way to hear or measure a whole mix after the fact. Its last lines say what it read off the land and how many steps were heard on what. The bot gets about by being moved, so to prove anything about feet use `--bot true --walk x,z`, which runs the hero straight there: from a beach into a lake it goes Sand, WetSand, Shallows, Water.

A spectrogram is the best thing there is for judging a sound with no ears, so when one looks wrong, believe it. Compare with real recordings where you can, by long-term spectrum and envelope statistics. Those are what a texture is recognised by.
