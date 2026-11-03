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
        public float Turbulence;       // Swirling cross-wind, relative to the mean speed.
        public float TurbulenceScale;  // Metres, size of the swirls.
        public float ResponseTime;     // Seconds the live wind takes to follow changes of Speed and Direction.

        // --- How vegetation responds. Each patch of plants is a damped spring driven by the wind's drag. ---
        public float VegetationFrequency;      // Natural sway frequency in Hz (varies ±20% between patches).
        public float VegetationDamping;        // Damping ratio: low values sway back and forth, 1 settles without overshoot.
        public float VegetationMaxBend;        // Radians plants lean at most in a strong steady wind.
        public float VegetationReferenceSpeed; // Wind speed in m/s at which plants lean about 63% of VegetationMaxBend.
        public float VegetationFlutter;        // Radians single grass blades flutter at VegetationReferenceSpeed.
        public float FlattenRecoveryTime;      // Seconds trampled plants take to stand back up (to about 37%).

        // --- Live state, advanced by WindSystem. ---
        public bool Initialized;
        public Vector2 Velocity;         // Live mean wind on the xz plane in m/s.
        public Vector2 GustOffset;       // Gust pattern offset in noise units, wrapped to WindField.NoisePeriod.
        public float GustPhase;          // Gust pattern evolution, wrapped likewise.
        public Vector2 TurbulenceOffset;
        public float TurbulencePhase;
        public List<WindEmitter> Blasts;   // Blasts in flight (Wind.AddBlast); WindSystem ages and removes them.
        public List<WindEmitter> Emitters; // Every emitter acting this frame: blasts plus WindEmitterComponents.

        public WindSingleton()
        {
            Speed = 3f;
            Direction = 45f;
            Gustiness = 0.6f;
            GustScale = 24f;
            Turbulence = 0.35f;
            TurbulenceScale = 6f;
            ResponseTime = 2f;

            VegetationFrequency = 1.3f;
            VegetationDamping = 0.25f;
            VegetationMaxBend = 1.3f;
            VegetationReferenceSpeed = 8f;
            VegetationFlutter = 0.08f;
            FlattenRecoveryTime = 12f;

            Initialized = false;
            Velocity = Vector2.Zero;
            GustOffset = Vector2.Zero;
            GustPhase = 0f;
            TurbulenceOffset = Vector2.Zero;
            TurbulencePhase = 0f;
            Blasts = new List<WindEmitter>();
            Emitters = new List<WindEmitter>();
        }

        /// <summary>The mean wind Speed and Direction ask for, on the xz plane in m/s.</summary>
        public Vector2 TargetVelocity
        {
            get
            {
                float angle = MathHelper.DegreesToRadians(Direction);
                return new Vector2(MathF.Sin(angle), MathF.Cos(angle)) * MathF.Max(Speed, 0f);
            }
        }
    }
}
