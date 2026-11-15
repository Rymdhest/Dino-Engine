using Dino_Engine.Animation;
using Dino_Engine.ECS.Components;
using Dino_Engine.Modelling.Model;
using Dino_Engine.Modelling.Procedural.Gear;
using Dino_Engine.Modelling.Procedural.Saurian;
using OpenTK.Mathematics;

namespace Dino_Engine.Modelling.Procedural.Character
{
    /// <summary>
    /// Grows the land's dead, as the wolves and the dinosaurs are grown: a body drawn from a seed (<see cref="SkeletonBody"/>), and its bones made from that, each a rigid piece on the bone of a person's
    /// rig it belongs to, so a skeleton shares every animation with the living and nothing on it stretches. What has a shape too subtle for tubes, the skull, the jaw and the pelvis, is a field of
    /// signed distances skinned by surface nets and quadric decimation (<see cref="SaurianSkin"/>), and the rest is long bones with knobbed ends, vertebrae, ribs and the small bones of the hands and
    /// feet. Then what it was buried in, and the Ossuary's cold light in its eyes and where its heart was. What it holds is a hero's kind of gear, put where a hero's goes (<see cref="Outfit.Hold"/>), and
    /// its hands close round it. Everything is sized to be seen from where the camera is, at 30 pixels to a metre and 55 at the closest: ribs and the gaps between them are each two pixels or more, as
    /// anything finer only shimmers. Nothing is stored and nothing touches a GPU: it's safe on any thread, and a lab can grow one with no window.
    /// </summary>
    public static partial class SkeletonForge
    {
        /// <summary>What each part cost, for a lab to print. Nothing is written unless something's listening.</summary>
        public static Action<string>? Report;

        /// <summary>The skeleton a name means, holding what it holds.</summary>
        public static Mesh Make(SkeletonSpec spec) => Make(spec, HeldBy(spec));

        /// <param name="held">What it holds, and in which hand, 1 being the left, as <see cref="Outfit.Hold"/> takes it: a Minion's is what its page gave it.</param>
        public static Mesh Make(SkeletonSpec spec, IReadOnlyList<(GearSpec spec, float side)> held)
        {
            var body = new SkeletonBody(spec);
            var mesh = new SaurianMesh();

            bool leftHolds = false, rightHolds = false;
            foreach ((GearSpec piece, float side) in held)
            {
                if (piece.Kind is GearKind.OffHand or GearKind.Bow || side > 0f) leftHolds = true;
                else rightHolds = true;
            }

            int counted = 0;
            void Count(string part)
            {
                if (Report == null) return;
                Report($"    {part,-10} {mesh.TriangleCount - counted,6} triangles");
                counted = mesh.TriangleCount;
            }

            Skull(mesh, body); Count("skull");
            Neck(mesh, body);
            Spine(mesh, body); Count("spine");
            Ribs(mesh, body); Count("ribs");
            Pelvis(mesh, body); Count("pelvis");
            Shoulders(mesh, body); Count("shoulders");
            for (int side = -1; side <= 1; side += 2) Arm(mesh, body, side, side > 0 ? leftHolds : rightHolds);
            Count("arms");
            for (int side = -1; side <= 1; side += 2) Leg(mesh, body, side);
            Count("legs");
            Earth(mesh);
            Grave(mesh, body); Count("grave");
            Glow(mesh, body); Count("glow");

            // What's held keeps its own normals, and the bones theirs: merging meshes works them all out again from the faces, which would lose what the fields gave the skull.
            if (held.Count > 0) Append(mesh, Outfit.Hold(null, held));
            Count("held");
            return mesh.Build();
        }

        /// <summary>How a skeleton of this name carries what it holds.</summary>
        public static (Carry Carry, bool Shield) CarryOf(SkeletonSpec spec) => spec.Arms switch
        {
            SkeletonArms.SwordAndShield => (Carry.Sword, true),
            SkeletonArms.Sword => (Carry.Sword, false),
            SkeletonArms.Spear => (Carry.Spear, false),
            SkeletonArms.Bow => (Carry.Bow, false),
            _ => (Carry.Nothing, false),
        };

        // What the dead dug up with their arms hold: old, plain, nobody's best work, from the same smithy as a hero's.
        private static IReadOnlyList<(GearSpec spec, float side)> HeldBy(SkeletonSpec spec)
        {
            var form = new GearForm(0.45f, 0.5f, 0.15f, 0.4f, 0.12f, 0.45f);
            ulong seed = spec.Seed ^ 0xA2_3D5EUL;
            GearSpec Of(GearKind kind, ulong salt) => new GearSpec(seed + salt, kind, kind == GearKind.Bow ? 0f : 1f, kind == GearKind.Bow ? 1f : 0f, 0f, form, 0, GearTheme.None);
            return spec.Arms switch
            {
                SkeletonArms.SwordAndShield => new[] { (Of(GearKind.Sword, 0), -1f), (Of(GearKind.OffHand, 1), 1f) },
                SkeletonArms.Sword => new[] { (Of(GearKind.Sword, 0), -1f) },
                SkeletonArms.Spear => new[] { (Of(GearKind.Spear, 0), -1f) },
                SkeletonArms.Bow => new[] { (Of(GearKind.Bow, 0), -1f) },
                _ => Array.Empty<(GearSpec, float)>(),
            };
        }

        // Another mesh's vertices as they are, normals and all.
        private static void Append(SaurianMesh mesh, Mesh other)
        {
            int first = mesh.VertexCount;
            foreach (MeshVertex v in other.meshVertices)
            {
                // A mesh's colours are what's multiplied into its material's own, and the SaurianMesh's are albedos.
                Vector3 albedo = new Vector3(v.colour.Red, v.colour.Green, v.colour.Blue) * GearMaterials.AlbedoOf(v.materialTextureIndex);
                var hang = new Hang((BoneType)(int)v.boneIndices.X, (BoneType)(int)v.boneIndices.Y, v.BoneWeights.X);
                Vector2 uv = v.UVs != null && v.UVs.Length > 0 ? v.UVs[0] : Vector2.Zero;
                mesh.Vertex(v.position, v.normal, v.tangent, uv, albedo, v.materialTextureIndex, hang, v.colour.Intensity);
            }
            foreach (vIndex index in other.getAllIndicesArray()) mesh.Indices.Add(first + index.index);
        }

        // Earth gets into whatever faces down, and the top of a bone is bleached: a little of each, as it's lit by everything else too.
        private static void Earth(SaurianMesh mesh)
        {
            for (int i = 0; i < mesh.VertexCount; i++)
            {
                if (mesh.Materials[i] != GearMaterials.Bone) continue;
                float up = mesh.Normals[i].Y * 0.5f + 0.5f;
                mesh.Colours[i] *= 0.8f + 0.24f * up;
            }
        }

        // ---- Helpers.

        /// <summary>A long bone: a shaft that narrows to its middle, between two knobbed ends.</summary>
        internal static void LongBone(SaurianMesh mesh, Vector3 from, Vector3 to, float thick, Vector3 colour, Hang hang, int sides = 6)
        {
            Vector3 d = to - from;
            mesh.Tube(new[] { from, from + d * 0.07f, from + d * 0.22f, from + d * 0.5f, from + d * 0.78f, from + d * 0.93f, to },
                new[] { thick * 1.05f, thick * 1.3f, thick * 0.95f, thick * 0.78f, thick * 0.95f, thick * 1.3f, thick * 1.05f }, sides, colour, GearMaterials.Bone, hang);
        }

        /// <summary>A small bone between two joints, a finger's or a toe's: a little thicker at the knuckle it starts from.</summary>
        internal static void Phalanx(SaurianMesh mesh, Vector3 from, Vector3 to, float thick, Vector3 colour, Hang hang)
        {
            mesh.Tube(new[] { from, to }, new[] { thick * 1.1f, thick * 0.85f }, 4, colour, GearMaterials.Bone, hang);
        }

        /// <summary>What a field is told to make of each point on it: one material, its colour and the bone it hangs from.</summary>
        internal sealed class Dresser : ISkinDresser
        {
            private readonly Func<Vector3, Vector3, Vector3> _colour;
            private readonly Func<Vector3, Hang> _hang;
            private readonly int _material;

            public Dresser(int material, Func<Vector3, Vector3, Vector3> colour, Func<Vector3, Hang> hang)
            {
                _material = material;
                _colour = colour;
                _hang = hang;
            }

            public int Mapping(Vector3 p, SaurianPart part) => 0;

            // Seen from the front and above, as the camera sees it, which is all that matters.
            public Vector2 Uv(Vector3 p, int mapping, out float period)
            {
                period = 0f;
                return new Vector2(p.X, p.Y + p.Z * 0.4f);
            }

            public Vector3 Tangent(Vector3 p, Vector3 normal, int mapping) => Frames.Across(Vector3.UnitX, normal, Vector3.UnitZ);

            public Hang Hang(Vector3 p, SaurianPart part, ReadOnlySpan<float> parts) => _hang(p);

            public Vector3 Colour(Vector3 p, Vector3 normal, SaurianPart part, out int material, out float glow)
            {
                material = _material;
                glow = 1f;
                return _colour(p, normal);
            }
        }
    }
}
