using Dino_Engine.ECS.ECS_Architecture;
using Dino_Engine.Physics;
using OpenTK.Mathematics;

namespace Dino_Engine.ECS.Components
{
    /// <summary>
    /// The engine's single wind: settings, plus the live state WindSystem advances each frame. Grass, other
    /// vegetation, fog and anything else that moves with the wind read it, on the GPU through Shaders/Wind.glsl
    /// and the wind map, on the CPU through <see cref="Wind"/>. Register one as a world singleton; edit it live
    /// from the F3 Render window.
    /// </summary>
    public struct WindSingleton : IComponent
    {
        // --- Ambient wind. The live wind eases towards Speed and Direction over ResponseTime. ---
        public float Speed;            // Mean wind speed near the ground in m/s (3 = light breeze, 8 = fresh, 15 = gale).
        public float Direction;        // Degrees, the direction the wind blows towards (0 = +Z, 90 = +X).
        public float Gustiness;        // How much gusts change the speed: 0.5 = gusts between half and one and a half times the mean.
        public float GustScale;        // Metres, size of gust patches. They travel with the wind and change shape as they go.
        public float Turbulence;       // Swirling cross-wind, relative to the mean speed. Broad and slow: quick swirls only make plants shiver.
        public float TurbulenceScale;  // Metres, size of the swirls.
        public float ResponseTime;     // Seconds the live wind takes to follow changes of Speed and Direction, turning the short way round.

        // --- Waves: long fronts rolling down the wind, leaning the plants over as they pass. ---
        public float WaveLean;         // Radians a crest leans plants over, in a 3 m/s wind.
        public float WaveLength;       // Metres between waves.

        // --- How vegetation responds. Each patch of plants is a damped spring driven by the wind's drag. ---
        public float VegetationFrequency;      // Natural sway frequency in Hz (varies ±20% between patches).
        public float VegetationDamping;        // Damping ratio: low values sway back and forth, 1 settles without overshoot.
        public float VegetationMaxBend;        // Radians plants lean at most in a strong steady wind.
        public float VegetationReferenceSpeed; // Wind speed in m/s at which plants lean about 63% of VegetationMaxBend.
        public float VegetationFlutter;        // Radians tufts of grass flutter at VegetationReferenceSpeed.
        public float FlattenRecoveryTime;      // Seconds the trail through plants takes to fade (to about 37%).
        public float PressRecoveryTime;        // Seconds plants take to rise from under a foot (to about 37%).
        public float TouchFrequency;           // Hz at which plants swing back and forth when something has gone through them.
        public float TouchDamping;             // Damping ratio of that swing. Nothing but touch drives it, so it can ring as gusts must not.

        // --- How trees respond: as vegetation, on a slower spring, as a trunk has too much mass to answer a gust at once. ---
        public float CanopyFrequency;          // Natural sway frequency of a trunk in Hz.
        public float CanopyDamping;            // Damping ratio.

        // --- Live state, advanced by WindSystem. ---
        public bool Initialized;
        public float Heading;            // Live direction in radians, measured as Direction is. Kept apart from the speed, so a turning wind swings round and doesn't die away through nothing.
        public float HeadingRate;        // Radians per second.
        public float LiveSpeed;          // Live mean speed in m/s.
        public float LiveSpeedRate;      // m/s per second.
        public Vector2 Velocity;         // Live mean wind on the xz plane in m/s.
        public Vector2 Prevailing;       // Unit vector, the way the weather has laid the land. It stays put when the wind turns for a while.
        public Vector2 GustOffset;       // Gust pattern offset in noise units, wrapped to WindField.NoisePeriod.
        public float GustPhase;          // Gust pattern evolution, wrapped likewise.
        public Vector2 TurbulenceOffset;
        public float TurbulencePhase;
        public float WaveCycles;         // Waves gone by, wrapped to WindField.WaveCyclesPeriod.
        public float WaveWander;         // How far the wavering of their fronts has come, wrapped to 1.
        public float FlutterCycles;      // Flutters gone by, wrapped to WindField.FlutterCyclesPeriod.
        public List<WindEmitter> Blasts;   // Blasts in flight (Wind.AddBlast); WindSystem ages and removes them.
        public List<WindEmitter> Emitters; // Every emitter acting this frame: blasts plus WindEmitterComponents.

        public WindSingleton()
        {
            Speed = 3f;
            Direction = 45f;
            Gustiness = 0.4f;
            GustScale = 32f;
            Turbulence = 0.25f;
            TurbulenceScale = 20f;
            ResponseTime = 2.5f;

            WaveLean = 0.36f;
            WaveLength = 11f;

            VegetationFrequency = 0.85f;
            VegetationDamping = 0.7f;
            VegetationMaxBend = 1.3f;
            VegetationReferenceSpeed = 8f;
            VegetationFlutter = 0.035f;
            FlattenRecoveryTime = 9f;
            PressRecoveryTime = 5f;
            TouchFrequency = 1.5f;
            TouchDamping = 0.2f;
            CanopyFrequency = 0.32f;
            CanopyDamping = 0.45f;

            Initialized = false;
            Heading = 0f;
            HeadingRate = 0f;
            LiveSpeed = 0f;
            LiveSpeedRate = 0f;
            Velocity = Vector2.Zero;
            Prevailing = Vector2.UnitY;
            GustOffset = Vector2.Zero;
            GustPhase = 0f;
            TurbulenceOffset = Vector2.Zero;
            TurbulencePhase = 0f;
            WaveCycles = 0f;
            WaveWander = 0f;
            FlutterCycles = 0f;
            Blasts = new List<WindEmitter>();
            Emitters = new List<WindEmitter>();
        }

        /// <summary>The unit vector on the xz plane of an angle in radians, measured as Direction is.</summary>
        public static Vector2 Towards(float angle) => new Vector2(MathF.Sin(angle), MathF.Cos(angle));
    }
}
