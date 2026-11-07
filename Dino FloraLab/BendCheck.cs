using Dino_Engine.Modelling.Model;
using Dino_Engine.Modelling.Procedural.Flora;
using Dino_Engine.World.Content;
using OpenTK.Mathematics;

namespace Dino_FloraLab
{
    /// <summary>
    /// Bends every plant in the game as hard as it can be bent, and fails if any of them comes apart. What's posed is <see cref="PlantRig"/>, the copy of what Plant.glsl does.
    /// A body is walked through each plant from every side, with a field as sharp as a real one: this is what used to tear them.
    /// </summary>
    internal static class BendCheck
    {
        private const float Radius = 2.7f;
        private const float MostStretch = 0.6f;     // How much longer the worst edge of anything may get, at the very limit of what it can bend.
        private const float ShortestEdge = 0.02f;   // Metres: slivers can do anything in proportion, and no one would see it.

        private static int _failures;

        public static int Run(Options options)
        {
            _failures = 0;
            int seeds = options.Int("count", 2);
            Console.WriteLine($"{"",-24}{"joints",8}{"depth",7}{"verts",9}{"reach m",9}{"walked through",16}{"at its limit",14}");

            foreach (PlantSpecies species in Program.Chosen(options))
            {
                if (!species.Sways) continue;
                for (int seed = 0; seed < seeds; seed++)
                {
                    Check(species.Id, FloraForge.Grow(species, (ulong)(seed * 977 + 3)).ToMesh(), species.Kind != WorldAssetKind.Tree && species.Kind != WorldAssetKind.Deadwood);
                }
            }

            Console.WriteLine(_failures == 0 ? "Every plant holds together." : $"{_failures} plant problem(s).");
            return _failures == 0 ? 0 : 1;
        }

        private static void Check(string id, Mesh mesh, bool walkable)
        {
            List<PlantJoint>? joints = mesh.joints;
            if (joints == null)
            {
                Fail(id, "has no joints");
                return;
            }

            int depth = 0;
            for (int i = 0; i < joints.Count; i++)
            {
                depth = Math.Max(depth, PlantRig.Depth(joints, i));
                if (joints[i].Parent >= i) Fail(id, $"joint {i} hangs from one that comes after it");
                if (joints[i].Length < 1e-4f) Fail(id, $"joint {i} has no length");
                if (!walkable && joints[i].Touch > 0f) Fail(id, "can be touched, and it's a tree");
            }
            if (depth > PlantRig.MostLevels) Fail(id, $"is {depth} joints deep");

            foreach (Face face in mesh.faces)
            {
                if ((face.A.joint < 0) != (face.B.joint < 0) || (face.A.joint < 0) != (face.C.joint < 0)) Fail(id, "has a face half held and half not");
            }

            _worst = 0f;
            int count = mesh.meshVertices.Count;
            Vector3[] rest = new Vector3[count];
            for (int i = 0; i < count; i++) rest[i] = mesh.meshVertices[i].position;
            Vector3[] posed = new Vector3[count];
            float reach = PlantRig.Reach(mesh);

            // A body going through it, from every side. A tree only ever feels the wind, so a shockwave goes through it instead, each joint from its own side.
            float walked = 0f;
            float limit = 0f;
            float moved = 0f;
            int step = walkable ? 1 : 2;
            for (int x = -6; x <= 6; x += step)
            {
                for (int z = -6; z <= 6; z += step)
                {
                    Vector2 body = new Vector2(x, z) * 0.5f;
                    Pose(mesh, posed, joint => walkable ? Walked(joints[joint], body) : Blast(joints[joint], body, 1.3f));
                    walked = MathF.Max(walked, Stretch(mesh, rest, posed));
                    moved = MathF.Max(moved, Furthest(rest, posed));

                    Pose(mesh, posed, joint => Blast(joints[joint], body, 40f));
                    limit = MathF.Max(limit, Stretch(mesh, rest, posed));
                    moved = MathF.Max(moved, Furthest(rest, posed));
                }
            }

            Console.WriteLine($"{id,-24}{joints.Count,8}{depth,7}{count,9}{reach,9:0.0}{walked,15:0%} {limit,13:0%}");
            if (limit > MostStretch)
            {
                MeshVertex a = mesh.meshVertices[_worstEdge.a], b = mesh.meshVertices[_worstEdge.b];
                Fail(id, $"stretches {limit:0%} at its limit: a {_worstEdge.length:0.00} m edge at {a.position.Y:0.0} m up, between joints {a.joint} ({joints[a.joint].Length:0.0} m, bends {joints[a.joint].MostBend:0.00}) and {b.joint} ({joints[b.joint].Length:0.0} m), material {a.materialTextureIndex}");
            }
            if (moved > reach) Fail(id, $"moves {moved:0.00} m, past the {reach:0.00} m culling allows it");
        }

        private static void Pose(in Mesh mesh, Vector3[] posed, Func<int, Vector3> leanOf)
        {
            List<PlantJoint> joints = mesh.joints!;
            var known = new Vector3?[joints.Count];
            Func<int, Vector3> cached = joint => known[joint] ??= leanOf(joint);
            for (int i = 0; i < posed.Length; i++)
            {
                MeshVertex vertex = mesh.meshVertices[i];
                posed[i] = PlantRig.Pose(joints, vertex.joint, vertex.position, cached);
            }
        }

        // As Wind_Vegetation.frag leaves a patch as a body goes by, with no part of the field held back.
        private static Vector3 Walked(in PlantJoint joint, Vector2 body)
        {
            Vector2 feel = Vector2.Lerp(joint.Base.Xz, joint.Tip.Xz, 0.6f);
            Vector2 offset = feel - body;
            float distance = offset.Length;
            if (distance >= Radius * 1.2f) return Vector3.Zero;

            float near = 1f - distance / (Radius * 1.2f);
            float weight = MathHelper.Clamp(near / 0.55f, 0f, 1f);
            weight = weight * weight * (3f - 2f * weight);
            float soft = 0.3f * Radius;
            Vector2 aside = offset / MathF.Sqrt(distance * distance + soft * soft);
            Vector2 lean = aside * (1.3f * weight * joint.Touch);
            float press = 1.15f * weight * joint.Touch;
            return new Vector3(lean.X, -press, lean.Y);
        }

        // A front of wind going out from a point, as hard as any wind can lean anything.
        private static Vector3 Blast(in PlantJoint joint, Vector2 centre, float strength)
        {
            Vector2 offset = joint.Tip.Xz - centre;
            float distance = offset.Length;
            if (distance < 1e-3f) return Vector3.Zero;
            Vector2 lean = offset / distance * (strength * joint.Give);
            return new Vector3(lean.X, 0f, lean.Y);
        }

        private static float Stretch(in Mesh mesh, Vector3[] rest, Vector3[] posed)
        {
            float worst = 0f;
            foreach (Face face in mesh.faces)
            {
                int a = face.A.index.index;
                int b = face.B.index.index;
                int c = face.C.index.index;
                worst = MathF.Max(worst, EdgeStretch(rest, posed, a, b));
                worst = MathF.Max(worst, EdgeStretch(rest, posed, b, c));
                worst = MathF.Max(worst, EdgeStretch(rest, posed, c, a));
            }
            return worst;
        }

        private static float EdgeStretch(Vector3[] rest, Vector3[] posed, int a, int b)
        {
            float before = (rest[a] - rest[b]).Length;
            if (before < ShortestEdge) return 0f;
            float stretch = (posed[a] - posed[b]).Length / before - 1f;
            if (stretch > _worst)
            {
                _worst = stretch;
                _worstEdge = (a, b, before);
            }
            return stretch;
        }

        private static float _worst;
        private static (int a, int b, float length) _worstEdge;

        private static float Furthest(Vector3[] rest, Vector3[] posed)
        {
            float worst = 0f;
            for (int i = 0; i < rest.Length; i++) worst = MathF.Max(worst, (posed[i] - rest[i]).LengthSquared);
            return MathF.Sqrt(worst);
        }

        private static void Fail(string id, string problem)
        {
            Console.WriteLine($"  {id} {problem}");
            _failures++;
        }
    }
}
