using Dino_Engine.ECS.ECS_Architecture;
using OpenTK.Mathematics;

namespace Dino_Engine.ECS.Components
{
    /// <summary>
    /// Settings for the volumetric ground fog (<see cref="Rendering.Renderers.PostProcessing.VolumetricFogRenderer"/>).
    /// The fog is a layer of <see cref="Height"/> metres sitting on the ground: either on the terrain
    /// (<see cref="TerrainFollow"/> = 1) or on the flat plane y = <see cref="BaseHeight"/> (= 0, pools in valleys).
    /// Register one as a world singleton; edit it live from the F3 Render window.
    /// </summary>
    public struct VolumetricFogSingleton : IComponent
    {
        public bool Enabled;

        // --- Shape ---
        public float Density;         // Extinction per metre where the fog is thickest.
        public float Height;          // Thickness of the fog layer in metres.
        public float HeightFalloff;   // 1 = linear fade towards the top, higher = hugs the ground more.
        public float TerrainFollow;   // 0 = flat layer at BaseHeight, 1 = layer follows the terrain surface.
        public float BaseHeight;      // World y of the flat layer's ground (used where TerrainFollow < 1 or no terrain is captured).
        public float Coverage;        // 0..1, lower values open up clearings between fog banks.
        public float EdgeSoftness;    // Small = crisp billow edges, 1 = soft wispy edges.

        // --- Noise / motion ---
        public float NoiseScale;      // World size in metres of one noise tile; billows are about a quarter of this.
        public float Turbulence;      // Domain-warp strength; makes the billows churn as they travel.
        public float Detail;          // Erosion of the billow edges by high-frequency noise.
        public float TopVariation;    // How much the top of the layer heaves up and down.
        public float WindInfluence;   // How much the fog drifts with the engine's wind (WindSingleton): 1 = moves with the air.
        public float GustResponse;    // Seconds: gusts and blasts push the fog by this times how much faster than the mean the local wind blows.
        public float RollSpeed;       // How fast the fog churns and the wisps rise, independent of the wind.

        // --- Lighting ---
        public Vector3 Albedo;        // Scattering colour, (1,1,1) = pure white water droplets.
        public float DirectionalScattering; // Multiplier on light scattered from directional lights (the sky light excluded).
        public float LocalLightScattering;  // Multiplier on light scattered from point lights and spotlights.
        public float AmbientScattering; // Multiplier on in-scattered sky light.
        public float Anisotropy;      // Henyey-Greenstein g, > 0 scatters forward (bright when looking towards the sun).

        // --- Distance haze (cheap analytic fog over the whole view, replaces the old distance fog) ---
        public float HazeDensity;     // Extinction per metre.

        // --- Quality ---
        public int MaxSteps;          // Ray-march steps through the fog layer.
        public float TemporalBlend;   // History weight, higher = smoother but more smearing of fast motion.
        public float MaxDistance;     // Rays are never marched further than this.
        public float TerrainCaptureSize; // Side length in metres of the top-down terrain height capture around the camera.

        public VolumetricFogSingleton()
        {
            Enabled = true;

            Density = 0.6f;
            Height = 4f;
            HeightFalloff = 1.6f;
            TerrainFollow = 1f;
            BaseHeight = 0f;
            Coverage = 0.75f;
            EdgeSoftness = 0.35f;

            NoiseScale = 36f;
            Turbulence = 0.35f;
            Detail = 0.35f;
            TopVariation = 0.5f;
            WindInfluence = 1f;
            GustResponse = 0.6f;
            RollSpeed = 0.4f;

            Albedo = new Vector3(0.95f, 0.96f, 1f);
            DirectionalScattering = 1f;
            LocalLightScattering = 1f;
            AmbientScattering = 1f;
            Anisotropy = 0.35f;

            HazeDensity = 0.003f;

            MaxSteps = 32;
            TemporalBlend = 0.9f;
            MaxDistance = 400f;
            TerrainCaptureSize = 128f;
        }
    }
}
