using Dino_Engine.ECS.ECS_Architecture;
using Dino_Engine.Physics;
using Dino_Engine.World;
using OpenTK.Mathematics;

namespace Dino_Engine.ECS.Components
{
    /// <summary>
    /// The engine's weather: how it looks, and the live state WeatherSystem advances each frame. What the weather is doing comes from the world's <see cref="WeatherField"/>, which
    /// is a function of place and time, so everything held here is either a setting or something that follows from that field. Sky, sun, ground, water, fog, grass and sound all
    /// read it. Register one as a world singleton; edit it live from the F3 Render window. CPU access: <see cref="Weather"/>.
    /// </summary>
    public struct WeatherSingleton : IComponent
    {
        public bool Enabled;

        // --- Clouds ---
        public float CloudAltitude;     // Metres to the base of the clouds. Worlds are small, so the sky is too.
        public float CloudThickness;    // Metres from their base to their top.
        public float CloudSize;         // Metres across the largest.
        public float CloudSpeed;        // How much faster than the weather itself they cross the land.
        public float CloudShadow;       // How much of the sun the thickest cloud lets through, 0..1.

        // --- Light under cloud. Colours are radiance, as the sky's own are. ---
        public Vector3 OvercastSky;     // What the sky becomes under full cloud.
        public float RainGloom;         // How much of that is left under the heaviest rain.
        public float StormGloom;        // And on top of that in a thunderstorm.

        // --- Air. All of it is the volumetric fog (VolumetricFogSystem): nothing here may lay a veil over the whole picture, as there's a game to see. Extinction per metre at the heart of a bank. ---
        public float MistFog;           // Wet air, lying low in banks with clear ground between them.
        public float SteamFog;          // The wisps wet ground gives off where the sun reaches it.
        public float DustFog;           // Dust rolling along the ground.
        public float DriftFog;          // Snow blown along the ground.
        public float AshFog;            // Ash settling through the air.
        public Vector3 DustColour;
        public float FogClearing;       // Metres around the hero that weather's fog sinks away.
        public float FogPooling;        // How strongly mist gathers in hollows, 0..1.

        // --- What falls ---
        public int RainDrops;           // Streaks in the air at the heaviest rain.
        public int Flakes;              // Snowflakes, ash or dust at their heaviest.
        public int Splashes;            // Drops seen hitting things, at the heaviest rain.
        public float RainSpeed;         // Metres per second a raindrop falls.
        public float RainExposure;      // Seconds of a drop's fall the eye holds as a streak.
        public float RainOpacity;       // How strongly a streak shows.
        public float SnowSpeed;
        public float VolumeSize;        // Metres across the box of air around the camera that things fall through.

        // --- Wet surfaces ---
        public float WetDarkening;      // What the thirstiest dry surface darkens to when soaked, 0..1 of itself (Lagarde's 0.2 was a measured low).
        public float WetGloss;          // How far water smooths a surface, 0..1.
        public float PuddleLevel;       // Multiplier on how far puddles spread.
        public float RippleSize;        // Metres across a tile of rain rings.
        public float RippleStrength;    // How much they tilt what they fall on.

        // --- Water coming off things ---
        public float DripRate;          // How freely things drip, 0..1.
        public float DripOpacity;       // How strongly a falling drop shows.

        // --- Lightning ---
        public float FlashPower;        // Radiance of the sky in the brightest flash.
        public float BoltReach;         // Metres from the hero beyond which a bolt is only a flash in the sky.

        // --- Live state, advanced by WeatherSystem. ---
        public WeatherField? Field;         // This world's weather; none, and the sky stays fair.
        public double Clock;                // Seconds into the world's weather. The game sets this from the server's clock, so that everyone shares a sky.
        public bool ClockGiven;             // The game owns the clock; otherwise WeatherSystem runs it.
        public WeatherSample? Forced;       // Weather on order, in place of the field's, everywhere at once.
        public WeatherSample Here;          // What's happening at the camera's focus, as it's seen and heard.
        public Vector2 CloudOffset;         // Cloud pattern offset in tiles, wrapped to 1.
        public float CloudPhase;            // How far the clouds have changed shape, wrapped to 1.
        public float RainFallen;            // Metres raindrops have fallen, wrapped to a whole number of volumes for every speed of drop.
        public float SnowFallen;
        public Vector2 AirDrift;            // Metres the air has carried what's in it, wrapped likewise.
        public float SplashClock;           // Seconds, wrapped to a whole number of splashes.
        public float RippleClock;           // Seconds, wrapped likewise.
        public float DripClock;             // Seconds, wrapped at a whole number of every drip's period and every bead's cycle.
        public Vector3 Flash;               // The light of the lightning at this moment.
        public Vector3 FlashFrom;           // Unit vector towards it.
        public List<Bolt> Bolts;            // Those still in the air, near enough to see.
        public List<LightningStrike> Struck; // Whatever struck this frame, anywhere in the world. Sound reads this.

        public WeatherSingleton()
        {
            Enabled = true;

            CloudAltitude = 420f;
            CloudThickness = 260f;
            CloudSize = 1400f;
            CloudSpeed = 3f;
            CloudShadow = 0.05f;

            OvercastSky = new Vector3(1.28f, 1.36f, 1.5f);
            RainGloom = 0.72f;
            StormGloom = 0.6f;

            MistFog = 0.5f;
            SteamFog = 0.3f;
            DustFog = 0.5f;
            DriftFog = 0.5f;
            AshFog = 0.3f;
            DustColour = new Vector3(0.92f, 0.72f, 0.48f);
            FogClearing = 11f;
            FogPooling = 0.8f;

            RainDrops = 60000;
            Flakes = 14000;
            Splashes = 3600;
            RainSpeed = 9f;
            RainExposure = 0.045f;
            RainOpacity = 0.55f;
            SnowSpeed = 1.3f;
            VolumeSize = 72f;

            WetDarkening = 0.3f;
            WetGloss = 1f;
            PuddleLevel = 1f;
            RippleSize = 3.2f;
            RippleStrength = 0.75f;

            DripRate = 1f;
            DripOpacity = 0.7f;

            FlashPower = 9f;
            BoltReach = 420f;

            Field = null;
            Clock = 0.0;
            ClockGiven = false;
            Forced = null;
            Here = new WeatherSample { Gale = 1f };
            CloudOffset = Vector2.Zero;
            CloudPhase = 0f;
            RainFallen = 0f;
            SnowFallen = 0f;
            AirDrift = Vector2.Zero;
            SplashClock = 0f;
            RippleClock = 0f;
            DripClock = 0f;
            Flash = Vector3.Zero;
            FlashFrom = Vector3.UnitY;
            Bolts = new List<Bolt>();
            Struck = new List<LightningStrike>();
        }
    }

    /// <summary>A bolt on its way to the ground, for as long as it's in the air.</summary>
    public struct Bolt
    {
        public LightningStrike Strike;
        public Vector3 Ground;      // Where it lands.
        public float Age;           // Seconds since it struck.
    }
}
