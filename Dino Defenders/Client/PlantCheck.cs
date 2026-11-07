using Dino_Engine.Modelling.Model;
using Dino_Engine.Modelling.Procedural.Vegetation;
using Dino_Engine.World;
using Dino_Engine.World.Content;
using OpenTK.Mathematics;

namespace Dino_Defenders.Client
{
    /// <summary>
    /// Bends every plant in the game as hard as it can be bent, and fails if any of them comes apart: --plantcheck true --hidden true. It has to be here and not in
    /// a lab, as plants are built from materials that only exist with a GPU. What's posed is <see cref="PlantRig"/>, the copy of what Plant.glsl does.
    ///
    /// A body is walked through each plant from every side, with a field as sharp as a real one: this is what used to tear them. The way they used to bend is
    /// measured alongside, to say what the numbers mean.
    /// </summary>
    internal static class PlantCheck
    {
        private const float Radius = 2.7f;
        private const float MostStretch = 0.6f;     // How much longer the worst edge of anything may get, at the very limit of what it can bend.
        private const float ShortestEdge = 0.02f;   // Metres: slivers can do anything in proportion, and no one would see it.

        private static int _failures;

        public static int Run()
        {
            _failures = 0;
            Console.WriteLine($"{"",-16}{"joints",8}{"depth",7}{"verts",9}{"reach m",9}{"walked through",16}{"at its limit",14}{"as it was",12}");

            foreach (WorldAsset asset in NatureCatalog.All)
            {
                if (!asset.Sways) continue;
                Check(asset.Id, WorldAssetLibrary.BuildPlant(asset.Id), asset.Kind != WorldAssetKind.Tree, OldSway(asset.Id));
            }
            Check("crop.grain", PlantGenerator.GenerateGrainCrop(), true, 0f);
            Check("crop.vegetable", PlantGenerator.GenerateVegetableCrop(), true, 0f);

            Console.WriteLine(_failures == 0 ? "Every plant holds together." : $"{_failures} plant problem(s).");
            return _failures == 0 ? 0 : 1;
        }

        private static void Check(string id, Mesh mesh, bool walkable, float oldSway)
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

            int count = mesh.meshVertices.Count;
            Vector3[] rest = new Vector3[count];
            for (int i = 0; i < count; i++) rest[i] = mesh.meshVertices[i].position;
            Vector3[] posed = new Vector3[count];
            float reach = PlantRig.Reach(mesh);

            // A body going through it, from every side. A tree only ever feels the wind, so a shockwave goes through it instead, each joint from its own side.
            float walked = 0f;
            float limit = 0f;
            float moved = 0f;
            float old = 0f;
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

                    if (oldSway > 0f)
                    {
                        for (int i = 0; i < count; i++) posed[i] = OldPose(rest[i], body, oldSway);
                        old = MathF.Max(old, Stretch(mesh, rest, posed));
                    }
                }
            }

            Console.WriteLine($"{id,-16}{joints.Count,8}{depth,7}{count,9}{reach,9:0.0}{walked,15:0%} {limit,13:0%} {(oldSway > 0f ? old.ToString("0%") : "-"),11}");
            if (limit > MostStretch) Fail(id, $"stretches {limit:0%} at its limit");
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
            return (posed[a] - posed[b]).Length / before - 1f;
        }

        private static float Furthest(Vector3[] rest, Vector3[] posed)
        {
            float worst = 0f;
            for (int i = 0; i < rest.Length; i++) worst = MathF.Max(worst, (posed[i] - rest[i]).LengthSquared);
            return MathF.Sqrt(worst);
        }

        // The way a vertex used to bend: read for itself where it stands, and turned about the model's origin by however far it was from there.
        private static Vector3 OldPose(Vector3 position, Vector2 body, float sway)
        {
            Vector2 offset = position.Xz - body;
            float distance = offset.Length;
            if (distance >= 3f) return position;
            float push = 1.4f * MathF.Pow((3f - distance) / 3f, 0.6f);
            Vector2 bend = (distance > 1e-4f ? offset / distance : Vector2.Zero) * MathF.Min(push, 1.5f);

            float scale = (position.Y * 0.001f + position.Xz.Length * 0.01f) * sway;
            float rotateX = -bend.Y * scale;
            float rotateZ = bend.X * scale;
            Matrix3 x = new Matrix3(1f, 0f, 0f, 0f, MathF.Cos(rotateX), -MathF.Sin(rotateX), 0f, MathF.Sin(rotateX), MathF.Cos(rotateX));
            Matrix3 z = new Matrix3(MathF.Cos(rotateZ), -MathF.Sin(rotateZ), 0f, MathF.Sin(rotateZ), MathF.Cos(rotateZ), 0f, 0f, 0f, 1f);
            return position * z * x;
        }

        private static float OldSway(string id)
        {
            if (id.StartsWith("flower.")) return 45f;
            return id switch
            {
                "bamboo" => 22f,
                "bush.bigleaf" => 30f,
                "fern" => 30f,
                "reeds" => 40f,
                "pampas" => 55f,
                "heather" => 20f,
                "scrub" => 25f,
                _ => 0f,
            };
        }

        private static void Fail(string id, string problem)
        {
            Console.WriteLine($"  {id} {problem}");
            _failures++;
        }
    }
}
