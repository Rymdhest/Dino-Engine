using OpenTK.Mathematics;

namespace Dino_Engine.Textures.Synthesis
{
    /// <summary>
    /// A material as it's being made: a height and what the surface is at every point. Height runs from 0, the deepest the relief goes, to 1, where the mesh is, and
    /// is what the normals, the ambient occlusion and the parallax are made from, all at the material's own relief (<see cref="MaterialSpec.ReliefRatio"/>). A
    /// material is built up the way the real thing was: one surface laid over another where it stands higher (<see cref="Stack"/>), filled into what's low,
    /// painted, worn. Everything is immutable: <c>with</c> makes a changed copy. See TEXTURES.md.
    /// </summary>
    public sealed record Surface
    {
        public Field Height { get; init; } = 1f;
        public Paint Albedo { get; init; } = new Vector3(0.5f);
        public Field Alpha { get; init; } = 1f;
        public Field Roughness { get; init; } = 0.8f;
        public Field Metallic { get; init; } = 0f;
        public Field Emission { get; init; } = 0f;
        public Field Subsurface { get; init; } = 0f;

        /// <summary>Ambient occlusion besides what the height casts, which is baked from it: 1 for none.</summary>
        public Field Occlusion { get; init; } = 1f;

        public Surface()
        {
        }

        public Surface(Paint albedo, Field roughness, Field height)
        {
            Albedo = albedo;
            Roughness = roughness;
            Height = height;
        }

        /// <summary>
        /// <paramref name="top"/> laid over this wherever it stands higher, as stones lie in mortar, moss grows over rock, sand fills a crack. Where the two are within
        /// <paramref name="softness"/> of each other in height they blend, and the height between them rounds instead of breaking into a step.
        /// </summary>
        public Surface Stack(Surface top, float softness = 0.02f)
        {
            Field rise = top.Height - Height;
            Field share = rise.Smooth(-softness, softness);
            return Mix(top, share) with { Height = Height.SmoothMax(top.Height, softness * 2f) };
        }

        /// <summary>Everything of <paramref name="other"/> by <paramref name="share"/>, height and all.</summary>
        public Surface Mix(Surface other, Field share)
        {
            return new Surface
            {
                Height = Field.Lerp(Height, other.Height, share),
                Albedo = Paint.Lerp(Albedo, other.Albedo, share),
                Alpha = Field.Lerp(Alpha, other.Alpha, share),
                Roughness = Field.Lerp(Roughness, other.Roughness, share),
                Metallic = Field.Lerp(Metallic, other.Metallic, share),
                Emission = Field.Lerp(Emission, other.Emission, share),
                Subsurface = Field.Lerp(Subsurface, other.Subsurface, share),
                Occlusion = Field.Lerp(Occlusion, other.Occlusion, share),
            };
        }

        /// <summary>What <paramref name="other"/> is made of where <paramref name="mask"/> is, keeping this one's height: a stain, a coat of paint, a patch of lichen too thin to stand.</summary>
        public Surface Coat(Surface other, Field mask) => Mix(other, mask) with { Height = Height };

        /// <summary>The colour by <paramref name="mask"/>, darkened or lightened by <paramref name="factor"/> there: soot in a crack, dust on a crest.</summary>
        public Surface Shade(Field mask, float factor) => this with { Albedo = Albedo * Field.Lerp(1f, factor, mask) };
    }
}
