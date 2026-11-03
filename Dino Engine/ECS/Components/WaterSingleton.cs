using Dino_Engine.ECS.ECS_Architecture;
using Dino_Engine.Physics;
using OpenTK.Mathematics;

namespace Dino_Engine.ECS.Components
{
    /// <summary>
    /// The engine's water: settings shared by every water surface, the ocean, and the live state WaterSystem advances
    /// each frame. Still water (puddles, ponds) are separate entities with a <see cref="WaterBodyComponent"/>; the ocean
    /// fills everything below <see cref="SeaLevel"/>. All water moves with the engine's wind (<see cref="WindSingleton"/>).
    /// Register one as a world singleton; edit it live from the F3 Render window. CPU access: <see cref="Water"/>.
    /// </summary>
    public struct WaterSingleton : IComponent
    {
        public bool Enabled;

        // --- Ocean. It covers every bit of loaded terrain below SeaLevel. ---
        public bool OceanEnabled;
        public float SeaLevel;          // World y of the calm sea surface.
        public WaterOptics OceanOptics;

        // --- Waves on the shore. They run in towards the nearest land, steepen as the water shoals and break. ---
        public float ShoreWaveHeight;       // Metres crest to trough of the swell breaking on an open coast in still air.
        public float ShoreWavePeriod;       // Seconds between waves.
        public float ShoreWaveLength;       // Metres between crests out at sea.
        public float ShoreWaveReach;        // Metres from the shoreline out to sea that the shore waves show.
        public float RunUp;                 // Multiplier on how far the swash runs up the beach (1 = Hunt's formula, about sqrt(wave height x deep-water wavelength) inland).
        public float SetVariation;          // 0..1, how much the waves of a set grow and shrink.
        public float WindWaveHeight;        // Multiplier on the sea the wind raises (1 = fully developed sea, Pierson-Moskowitz).
        public float SeaStateResponseTime;  // Seconds the sea takes to follow changes of the wind.

        // --- Foam and wet sand ---
        public float Foam;                  // Multiplier on breaking-wave and shoreline foam.
        public float Whitecaps;             // Multiplier on the whitecaps the wind raises out at sea (Monahan 1980).
        public float WetSandDarkening;      // 0..1, how much the swash darkens the sand it wets.
        public float DryingTime;            // Seconds the sand takes to dry once the swash has left it (to about 37%).

        // --- Every water surface ---
        public float RippleScale;           // Multiplier on wind ripples (1 = a clean sea surface, Cox and Munk 1954).
        public float RippleDrift;           // How fast the wind carries ripples along, as a fraction of the wind speed.
        public float CausticStrength;       // Multiplier on the light patterns waves focus onto the bottom.
        public float RefractionStrength;    // Multiplier on how much waves bend the view of the bottom.
        public bool ScreenSpaceReflections; // Still water reflects what is on screen (trees on the far shore); otherwise only the sky. The sea always reflects only the sky.

        // --- Ripples from things moving through the water (WaterDisturberComponent, Water.AddSplash) ---
        public float RippleSpeed;           // Metres per second the rings spread.
        public float RippleDecay;           // Seconds the rings take to die down (to about 37%).
        public float RippleNormalStrength;  // Multiplier on how much they tilt the surface (above 1 makes up for the short waves the grid can't hold).

        // --- Quality ---
        public float MaxShoreFieldSize;     // Metres, largest area around the camera the shoreline is analysed over.

        // --- Live state, advanced by WaterSystem. ---
        public bool Initialized;
        public float SeaState;              // Wind speed in m/s the sea has built up to.
        public Vector2 SeaDirection;        // Unit, direction the wind-raised sea travels on the xz plane.
        public float ShorePhase;            // Cycles, wrapped to WaterWaves.SetLength.
        public Vector4 SwellPhasesA;        // Cycles of the open-sea swell waves 0..3, wrapped to 1.
        public Vector4 SwellPhasesB;        // Waves 4..7.
        public Vector2 RippleOffsetA;       // Ripple pattern offsets in tiles, wrapped to 1.
        public Vector2 RippleOffsetB;
        public float RippleTimeA;           // Ripple animation phases, wrapped to 1.
        public float RippleTimeB;
        public Vector2 CausticOffsetA;      // Caustic pattern offsets in tiles, wrapped to 1.
        public Vector2 CausticOffsetB;
        public Vector2 FoamOffset;          // Foam pattern offset in tiles, wrapped to 1.
        public List<WaterSplash> Splashes;  // Splashes waiting for the ripple simulation (Water.AddSplash); WaterSystem consumes them.

        public WaterSingleton()
        {
            Enabled = true;

            OceanEnabled = true;
            SeaLevel = 0f;
            OceanOptics = WaterOptics.Ocean;

            ShoreWaveHeight = 0.5f;
            ShoreWavePeriod = 6f;
            ShoreWaveLength = 9f;
            ShoreWaveReach = 30f;
            RunUp = 1f;
            SetVariation = 0.45f;
            WindWaveHeight = 0.35f;
            SeaStateResponseTime = 20f;

            Foam = 1f;
            Whitecaps = 1f;
            WetSandDarkening = 0.45f;
            DryingTime = 12f;

            RippleScale = 1f;
            RippleDrift = 0.15f;
            CausticStrength = 0.6f;
            RefractionStrength = 1f;
            ScreenSpaceReflections = true;

            RippleSpeed = 0.9f;
            RippleDecay = 1.5f;
            RippleNormalStrength = 3f;

            MaxShoreFieldSize = 256f;

            Initialized = false;
            SeaState = 0f;
            SeaDirection = Vector2.UnitY;
            ShorePhase = 0f;
            SwellPhasesA = Vector4.Zero;
            SwellPhasesB = Vector4.Zero;
            RippleOffsetA = Vector2.Zero;
            RippleOffsetB = Vector2.Zero;
            RippleTimeA = 0f;
            RippleTimeB = 0f;
            CausticOffsetA = Vector2.Zero;
            CausticOffsetB = Vector2.Zero;
            FoamOffset = Vector2.Zero;
            Splashes = new List<WaterSplash>();
        }
    }
}
