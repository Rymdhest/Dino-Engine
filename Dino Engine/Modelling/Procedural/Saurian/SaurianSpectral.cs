using Dino_Engine.ECS.Components;
using Dino_Engine.Modelling.Procedural.Gear;
using OpenTK.Mathematics;

namespace Dino_Engine.Modelling.Procedural.Saurian
{
    /// <summary>
    /// What the Ossuary calls up where the bones it finds are too far gone to stand on their own: the cold light that moves its skeletons, with nothing to hold it but what's left of the bones,
    /// and wearing the shape the animal had, as a skin of light round them. Its bones are solid, and its flesh is light, drawn after the lights over everything else and casting nothing
    /// (glModel.SpectralIndexCount, Spectral.frag): brightest where it's seen edge on, with light running up and back through it, and thinning to a wisp at its feet. Its colour is a cold
    /// blue that nothing else is, to be told from the bones' cyan-green and the rot's yellow-green at a glance. Read LORE.md.
    /// </summary>
    public static class SaurianSpectral
    {
        /// <summary>The cold blue a ghost is made of, as radiance for every unit of how brightly it's lit (Spectral.frag).</summary>
        public static readonly Vector3 Light = new Vector3(0.32f, 0.7f, 1f);

        private const float FleshBudget = 0.55f;   // Of its kind's skin, as light shows its outline and none of its detail.

        public static void Build(SaurianMesh mesh, SaurianBody body)
        {
            SaurianBones.Build(mesh, body, ghost: true);

            // Everything from here on is light (SaurianMesh.BeginSpectral).
            mesh.BeginSpectral();
            var dress = new Dress(body);
            int budget = Math.Max((int)(body.Species.Budget * FleshBudget), 1500);
            float cell = SaurianForge.CellFor(body, budget);
            float longest = body.Length / 30f;
            SaurianSkin.Grow(mesh, SaurianSculpt.Flesh(body), cell, budget, longest, dress);
            SaurianSkin.Grow(mesh, SaurianSculpt.Jaw(body), cell * 0.6f, Math.Max(budget / 12, 200), longest * 0.5f, dress);
        }

        /// <summary>A skin of light: the cold blue, brighter along its back and over its head and dimmer below, and fading out towards the ground, so that what it stands on is only a wisp.</summary>
        private sealed class Dress : SaurianDress
        {
            public Dress(SaurianBody body) : base(body, new SaurianPaint(body)) { }

            public override Vector3 Colour(Vector3 p, Vector3 normal, SaurianPart part, out int material, out float glow)
            {
                material = GearMaterials.Glow;
                glow = 1f;
                float hips = MathF.Max(Body.HipJoint, 0.1f);
                float low = Smooth(0.04f, 0.75f, p.Y / hips);
                float top = 0.75f + 0.25f * MathF.Max(normal.Y, 0f);
                float head = part == SaurianPart.Head ? 1.25f : 1f;
                return Light * (low * top * head);
            }

            public override float Shell(Vector3 p, Vector3 normal, SaurianPart part) => 0f;

            private static float Smooth(float from, float to, float value)
            {
                float t = Math.Clamp((value - from) / (to - from), 0f, 1f);
                return t * t * (3f - 2f * t);
            }
        }
    }
}
