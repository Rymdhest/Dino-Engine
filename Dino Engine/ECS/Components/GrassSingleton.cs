using Dino_Engine.ECS.ECS_Architecture;
using OpenTK.Mathematics;

namespace Dino_Engine.ECS.Components
{
    /// <summary>
    /// Settings for the GPU grass (<see cref="Rendering.Renderers.Geometry.GrassRenderer"/>). Every frame a compute pass
    /// scatters blades over the visible terrain chunks, gathers them into tufts, thins them out with distance, culls them
    /// and builds each survivor's curve; nothing about single blades is stored. Where grass grows comes from the terrain's
    /// grass map. Register one as a world singleton; edit it live from the F3 Render window. Without one, grass renders
    /// with these defaults. The distances suit a camera some 30 metres out: everything it sees should be well inside
    /// <see cref="FalloffStart"/>, or the player only ever meets the field's thinned-out far end.
    /// </summary>
    public struct GrassSingleton : IComponent
    {
        public bool Enabled;

        // --- Field ---
        public float Density;               // Blades per square metre close to the camera, where the grass map is full.
        public float DrawDistance;          // Metres from the camera; the field thins away to nothing towards it.
        public float FalloffStart;          // Metres from the camera where the field starts to thin out (about as many blades per pixel as up close).
        public float MinDensity;            // Fraction of the blades still drawn far away (0..1). The survivors widen to keep the field covered.
        public float LodDistance;           // Metres where blades drop from 15 to 7 vertices. They morph into the low shape before the switch.

        // --- Blades ---
        public float Height;                // Metres, blade length where the grass map is full.
        public float HeightVariation;       // 0..1, random spread of single blade lengths.
        public float Width;                 // Metres, blade width at the root.
        public float Tilt;                  // Radians the tip leans from vertical on average.
        public float TiltVariation;         // Radians of random spread around Tilt.
        public float Droop;                 // Radians the tip of a full-length blade arches over on top of its tilt.
        public float Roundness;             // How far normals tilt out towards the blade's edges, so flat blades shade as if rounded.
        public float Thickening;            // 0..1, how much blades seen edge-on are widened across the view so they never vanish.

        // --- Tufts: blades gather in Voronoi cells and share the cell's traits ---
        public float ClumpSize;             // Metres between tuft centres.
        public float ClumpPull;             // 0..1, how far blades are pulled towards their tuft's centre.
        public float ClumpHeightVariation;  // 0..1, how much tufts differ in height.
        public float ClumpSplay;            // 0..1, 0 = blades lean every which way, 1 = they lean out from their tuft's centre.

        // --- The field as a whole, which is what the eye reads from above ---
        public float Swell;                 // 0..1, how far the top of the field rises and sinks, as a share of its height.
        public float SwellSize;             // Metres across a swell.
        public float Comb;                  // 0..1, how much blades lean the way the field is combed rather than their own way.
        public float CombSize;              // Metres across a sweep of the combing.

        // --- Wind, on top of what WindSingleton does to all plants ---
        public float WindGain;              // How much further grass bends than other plants do.
        public float WindSheen;             // 0..1, how much gusts and waves light the blades they lean over.

        // --- Colour ---
        public Vector3 RootColour;          // Shaded base of the blades.
        public Vector3 BodyColour;          // Healthy blade.
        public Vector3 TipColour;           // Sun-bleached tips.
        public Vector3 DryColour;           // Straw: dry patches and grass that barely grows.
        public float ColourVariation;       // 0..1, shade and hue differences between tufts and blades.
        public float DryPatches;            // 0..1, how much of the field is dry patches.
        public float Translucency;          // Light let through by backlit blades.
        public float Roughness;
        public float GroundTint;            // 0..1, how much the ground under the grass takes on the field's colour, which hides the gaps.
        public float HueDrift;              // 0..1, how far colour wanders between cool and warm across the field.
        public float CanopyShade;           // 0..1, how dark it gets deep inside the field.
        public float FieldNormal;           // 0..1, how much blades are lit as one soft surface rather than each on its own.

        // --- Flowers and seed heads. Where and how many is the biome's; these scale it ---
        public float BloomAmount;           // Multiplies the share of blades in flower.
        public float BloomSize;             // Metres across a flower.
        public float PlumeAmount;           // Multiplies the share of tufts in seed.
        public Vector3 PlumeColour;         // Seed heads.

        // --- Shadows ---
        public float ShadowDistance;        // Metres from the camera within which blades cast shadows.
        public float ShadowDensity;         // Fraction of the blades that cast shadows (0..1); they widen to keep the shadow as dense.

        public GrassSingleton()
        {
            Enabled = true;

            Density = 140f;
            DrawDistance = 115f;
            FalloffStart = 48f;
            MinDensity = 0.3f;
            LodDistance = 44f;

            Height = 1.2f;
            HeightVariation = 0.35f;
            Width = 0.07f;
            Tilt = 0.5f;
            TiltVariation = 0.3f;
            Droop = 1f;
            Roundness = 0.5f;
            Thickening = 0.6f;

            ClumpSize = 0.85f;
            ClumpPull = 0.35f;
            ClumpHeightVariation = 0.35f;
            ClumpSplay = 0.45f;

            Swell = 0.45f;
            SwellSize = 17f;
            Comb = 0.68f;
            CombSize = 30f;

            WindGain = 1.6f;
            WindSheen = 0.7f;

            RootColour = new Vector3(0.02f, 0.05f, 0.03f);
            BodyColour = new Vector3(0.13f, 0.2f, 0.05f);
            TipColour = new Vector3(0.5f, 0.55f, 0.14f);
            DryColour = new Vector3(0.46f, 0.32f, 0.1f);
            ColourVariation = 0.55f;
            DryPatches = 0.08f;
            Translucency = 0.8f;
            Roughness = 0.5f;
            GroundTint = 0.85f;
            HueDrift = 0.7f;
            CanopyShade = 0.6f;
            FieldNormal = 0.68f;

            BloomAmount = 1f;
            BloomSize = 0.17f;
            PlumeAmount = 1f;
            PlumeColour = new Vector3(0.6f, 0.48f, 0.26f);

            ShadowDistance = 70f;
            ShadowDensity = 0.35f;
        }
    }
}
