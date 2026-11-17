using Dino_Engine.Animation;
using Dino_Engine.Modelling.Model;
using Dino_Engine.Modelling.Procedural.Gear;
using Dino_Engine.Modelling.Procedural.Saurian;
using Dino_Engine.Util;
using OpenTK.Mathematics;

namespace Dino_Engine.Modelling.Procedural.Architecture.Seats
{
    /// <summary>
    /// A tyrant as a seat shows one that isn't alive (SEATS.md): the game's own animal, grown by the forge that grows every living one and posed by the animator that moves them, and
    /// then taken as it stands, triangle by triangle, to be cast in iron on a plinth or sewn in canvas as the dummy its regalia is fitted on. So the colossus over a triumph is the
    /// animal the hero is about to fight, and the armour in a foundry would fit it. Headless, and safe on any thread, as everything it uses is.
    /// </summary>
    internal static class SeatBeasts
    {
        private sealed class FlatGround : IGround
        {
            public float HeightAt(float x, float z) => 0f;
        }

        /// <summary>One triangle of a posed animal: where it is, what it's made of on the animal, and its colour there.</summary>
        public readonly record struct Tri(Vector3 A, Vector3 B, Vector3 C, int Material, Colour Colour)
        {
            public Vector3 Middle => (A + B + C) / 3f;
        }

        /// <summary>Whether a triangle of what an animal wears is of its brass rather than its iron.</summary>
        public static bool Brassy(in Tri tri) => tri.Colour.Red > tri.Colour.Blue * 1.5f + 0.04f;

        /// <summary>
        /// A tyrant in its commander's regalia, standing at the origin and facing +z: every triangle of it, where its bones put it. Roaring, it's taken part way through its roar, as
        /// the game's logo has it, and otherwise as it stands at rest.
        /// </summary>
        public static List<Tri> Tyrant(int variant, uint seed, bool roar)
        {
            SaurianSpec spec = SaurianSpec.Parse($"dino:tyrant.reich.{Math.Abs(variant) % 8}") ?? SaurianSpec.Parse("dino:tyrant.reich.0")!.Value;
            Mesh mesh = SaurianForge.Make(spec);
            var animator = new CharacterAnimator(SaurianForge.RigOf(spec), SaurianForge.StyleOf(spec), (int)(seed & 0x7FFFFFFF));
            // The roar as the lab shows it: repeated with a gap, two of them to settle into, and the frame five twelfths through the third.
            const float Impact = 0.7f, Length = 1.9f, Gap = 0.3f, Cycle = Length + Gap, Step = 1f / 60f;
            float until = roar ? Cycle * 2f + Cycle * 5f / 12f : 2f;
            var ground = new FlatGround();
            for (float time = Step; time < until; time += Step)
            {
                AnimationInput input = AnimationInput.At(Vector3.Zero, 0f);
                if (roar)
                {
                    input.Alert = 1f;
                    int serial = (int)MathF.Floor(time / Cycle);
                    float into = time - serial * Cycle;
                    if (into < Length) input.Action = new ActionInput { Gesture = Gestures.Roar, Elapsed = into, Impact = Impact, Length = Length, Serial = serial };
                }
                animator.Step(input, Step, ground);
            }
            Matrix4[] bones = animator.Skinning;
            Vector3 Place(MeshVertex v)
            {
                Vector2 w = v.BoneWeights;
                float total = w.X + w.Y;
                w = total > 0.001f ? w / total : new Vector2(1f, 0f);
                int first = Math.Clamp((int)v.boneIndices.X, 0, bones.Length - 1), second = Math.Clamp((int)v.boneIndices.Y, 0, bones.Length - 1);
                Vector3 p = Vector3.TransformPosition(v.position, bones[first]) * w.X;
                if (w.Y > 0f) p += Vector3.TransformPosition(v.position, bones[second]) * w.Y;
                return p;
            }
            var tris = new List<Tri>(mesh.faces.Count);
            foreach (Face face in mesh.faces)
            {
                Vector3 a = Place(face.A), c = Place(face.B), d = Place(face.C);
                if (Vector3.Cross(c - a, d - a).LengthSquared < 1e-12f) continue;
                tris.Add(new Tri(a, c, d, face.A.materialTextureIndex, face.A.colour));
            }
            return tris;
        }

        /// <summary>The box round some triangles.</summary>
        public static (Vector3 Min, Vector3 Max) Bounds(IEnumerable<Tri> tris)
        {
            Vector3 min = new Vector3(float.MaxValue), max = new Vector3(float.MinValue);
            foreach (Tri t in tris)
            {
                min = Vector3.ComponentMin(min, Vector3.ComponentMin(t.A, Vector3.ComponentMin(t.B, t.C)));
                max = Vector3.ComponentMax(max, Vector3.ComponentMax(t.A, Vector3.ComponentMax(t.B, t.C)));
            }
            return (min, max);
        }

        /// <summary>
        /// Stands a posed animal in a model: as long as it's told from snout to tail, its feet on <paramref name="foot"/> and the middle of them over it, turned by
        /// <paramref name="turn"/> from facing +z towards +x, every triangle in whatever <paramref name="material"/> makes of it, or left out. Returns the box round it as it stands.
        /// </summary>
        public static (Vector3 Min, Vector3 Max) Stand(MeshBuilder b, List<Tri> tris, float length, Vector3 foot, Func<Tri, VertexMaterial?> material, float turn = 0f)
        {
            (Vector3 min, Vector3 max) = Bounds(tris);
            float scale = length / MathF.Max(max.Z - min.Z, 0.1f);
            float low = min.Y + (max.Y - min.Y) * 0.04f;
            Vector3 sum = Vector3.Zero;
            int count = 0;
            foreach (Tri t in tris)
            {
                if (t.Middle.Y > low) continue;
                sum += t.Middle;
                count++;
            }
            Vector3 feet = count > 0 ? sum / count : (min + max) * 0.5f;
            Vector3 origin = new Vector3(feet.X, min.Y, feet.Z);
            float cos = MathF.Cos(turn), sin = MathF.Sin(turn);
            Vector3 P(Vector3 p)
            {
                p = (p - origin) * scale;
                return foot + new Vector3(p.X * cos + p.Z * sin, p.Y, p.Z * cos - p.X * sin);
            }
            Vector3 least = new Vector3(float.MaxValue), most = new Vector3(float.MinValue);
            foreach (Tri t in tris)
            {
                Vector3 a = P(t.A), c = P(t.B), d = P(t.C);
                least = Vector3.ComponentMin(least, Vector3.ComponentMin(a, Vector3.ComponentMin(c, d)));
                most = Vector3.ComponentMax(most, Vector3.ComponentMax(a, Vector3.ComponentMax(c, d)));
                if (material(t) is VertexMaterial made) b.Triangle(a, c, d, made);
            }
            return (least, most);
        }
    }
}
