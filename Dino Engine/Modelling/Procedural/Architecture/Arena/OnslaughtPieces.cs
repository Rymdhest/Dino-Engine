using Dino_Engine.Animation;
using Dino_Engine.Modelling.Model;
using Dino_Engine.Modelling.Procedural.Architecture.Plaza;
using Dino_Engine.Modelling.Procedural.Gear;
using Dino_Engine.Modelling.Procedural.Saurian;
using Dino_Engine.Textures;
using Dino_Engine.Util;
using Dino_Engine.World;
using Dino_Engine.World.Content;
using OpenTK.Mathematics;

namespace Dino_Engine.Modelling.Procedural.Architecture.Arena
{
    /// <summary>
    /// The Fossilized Onslaught, the Ossuary's dig where the Reich's first charge on a land was buried in ash: the charge itself turned to stone where the ash caught it, war-beasts in harness grown by the
    /// same forge as any living one and posed mid-stride by the same animator, then baked and petrified; the war-wagons and standards that went down with them; the bones of far older beasts in the quarry's
    /// cut faces, the Ossuary's scaffolds and crane where it digs them out; and the cracks in the ash that still breathe fire. Model y = 0 is where people stand at a piece's foot, and +z its front. Headless.
    /// </summary>
    internal static class OnslaughtPieces
    {
        private static readonly Vector3 Petrified = new Vector3(0.5f, 0.47f, 0.43f);
        private static readonly Vector3 AshDust = new Vector3(0.62f, 0.6f, 0.57f);
        private static readonly Vector3 Fossil = new Vector3(0.74f, 0.66f, 0.52f);

        /// <summary>The kinds of the Reich's beasts that went down in the charge, as an index in a statue's id.</summary>
        public static readonly string[] Kinds = { "raptor", "hornback", "ravager", "spitter" };

        // ---------------------------------------------------------------- The charge, in stone

        private sealed class FlatGround : IGround
        {
            public float HeightAt(float x, float z) => 0f;
        }

        /// <summary>
        /// One of the charge, turned to stone where the ash took it: a war-beast of a <paramref name="kind"/> in the Reich's harness, grown from its <paramref name="variant"/> as any is, run at a charge for
        /// <paramref name="stride"/> seconds by its animator so that it's caught mid-stride, its head down, then baked to stone, leaning <paramref name="lean"/> degrees to its side and sunk
        /// <paramref name="sink"/> into the ash. It faces +z, the way it was charging. Grey stone dusted pale where the ash lies, its harness darker, and rust where the iron bled.
        /// </summary>
        public static void Statue(MeshBuilder b, int kind, int variant, float stride, float lean, float sink, uint seed)
        {
            string key = $"dino:{Kinds[Math.Clamp(kind, 0, Kinds.Length - 1)]}.reich.{variant}";
            SaurianSpec spec = SaurianSpec.Parse(key) ?? SaurianSpec.Parse("dino:raptor.reich.0")!.Value;
            Mesh mesh = SaurianForge.Make(spec);
            Rig rig = SaurianForge.RigOf(spec);
            var animator = new CharacterAnimator(rig, SaurianForge.StyleOf(spec), (int)(seed & 0x7FFFFFFF));
            var ground = new FlatGround();
            const float Step = 1f / 60f, Speed = 8f, Warmup = 1.4f;
            float time = 0f;
            Vector3 at = Vector3.Zero;
            while (time < Warmup + stride)
            {
                time += Step;
                at = new Vector3(0f, 0f, Speed * time);
                var input = AnimationInput.At(at, 0f);
                input.Alert = 1f;
                animator.Step(input, Step, ground);
            }
            Matrix4[] bones = animator.Skinning;
            var random = new WorldRandom(seed).Fork("statue");
            Quaternion tilt = Quaternion.FromAxisAngle(Vector3.UnitZ, MathHelper.DegreesToRadians(lean));
            Vector3 Place(MeshVertex v)
            {
                Vector2 w = v.BoneWeights;
                float total = w.X + w.Y;
                w = total > 0.001f ? w / total : new Vector2(1f, 0f);
                Matrix4 skin = w.Y > 0f ? bones[(int)v.boneIndices.X] * w.X + bones[(int)v.boneIndices.Y] * w.Y : bones[(int)v.boneIndices.X];
                Vector3 posed = Vector3.TransformPosition(v.position, skin);
                return Vector3.Transform(posed, tilt) - Vector3.UnitY * sink;
            }

            // Each face of the skin, flat, as stone that's been worn: carved, not grown. What was harness is darker, and the iron of it rusted.
            float shade = random.Range(0.85f, 1.08f);
            foreach (Face face in mesh.faces)
            {
                Vector3 a = Place(face.A), c = Place(face.B), d = Place(face.C);
                if (a.Y < -0.3f && c.Y < -0.3f && d.Y < -0.3f) continue;     // Under the ash.
                Vector3 normal = Vector3.Cross(c - a, d - a);
                if (normal.LengthSquared < 1e-10f) continue;
                normal.Normalize();
                int material = face.A.materialTextureIndex;
                Vector3 colour = Petrified * shade * (0.82f + 0.36f * Luminance(face.A.colour));
                if (material == GearMaterials.Metal) colour = random.Chance(0.35f) ? new Vector3(0.24f, 0.14f, 0.09f) : Petrified * 0.48f;
                else if (material == GearMaterials.Cloth || material == GearMaterials.Leather) colour = Petrified * 0.62f;
                // The ash lies on whatever faces the sky.
                colour = Vector3.Lerp(colour, AshDust, Math.Clamp((normal.Y - 0.3f) * 1.2f, 0f, 0.75f));
                b.Triangle(a, c, d, new VertexMaterial(MaterialCatalog.Rock.Index, new Colour(colour)));
            }
            // The ash heaped round its feet, where it was buried.
            VertexMaterial ash = new VertexMaterial(MaterialCatalog.CrackedLava.Index, new Colour(AshDust * 0.85f));
            for (int k = 0; k < 3; k++)
            {
                Vector3 mound = new Vector3(random.Range(-0.8f, 0.8f), -0.2f, random.Range(-1.2f, 1.2f));
                b.Surface(8, 4, (u, v) =>
                {
                    float theta = u * MathF.Tau, r = v * random.Range(1.4f, 1.9f);
                    return mound + new Vector3(MathF.Cos(theta) * r, 0.45f * (1f - v * v) + ArenaStone.Ground, MathF.Sin(theta) * r);
                }, ash);
            }
        }

        // A face wound to look upwards.
        private static void Up(MeshBuilder b, Vector3 a, Vector3 c, Vector3 d, Vector3 e, VertexMaterial material)
        {
            Vector3 normal = Vector3.Cross(d - a, e - c);
            if (normal.Y < 0f) b.Quad(e, d, c, a, material);
            else b.Quad(a, c, d, e, material);
        }

        private static float Luminance(Colour c) => Math.Clamp(c.Red * 0.3f + c.Green * 0.55f + c.Blue * 0.15f, 0f, 1f);

        // ---------------------------------------------------------------- What went down with them

        /// <summary>
        /// A war-wagon of the charge, on its side in the ash and turned to stone with it: its bed of planks, iron-shod wheels, one broken, the iron plates along its side rusted through, and its pole
        /// snapped. It lies along +x.
        /// </summary>
        public static void Wagon(MeshBuilder b, uint seed)
        {
            var random = new WorldRandom(seed).Fork("wagon");
            VertexMaterial stone = new VertexMaterial(MaterialCatalog.Rock.Index, new Colour(Petrified * 0.9f));
            VertexMaterial dark = new VertexMaterial(MaterialCatalog.Rock.Index, new Colour(Petrified * 0.62f));
            VertexMaterial rust = new VertexMaterial(MaterialCatalog.Iron.Index, new Colour(0.24f, 0.13f, 0.08f));
            const float Length = 6f, Wide = 2.6f, Deep = 1.2f;
            // It lies on its side: its bed stands up, its floor towards +z.
            float tilt = MathHelper.DegreesToRadians(random.Range(70f, 95f));
            Matrix4 lie = Matrix4.CreateRotationX(tilt) * Matrix4.CreateTranslation(0f, Wide * 0.5f - 0.5f, 0f);
            Vector3 P(float x, float y, float z) => Vector3.TransformPosition(new Vector3(x, y, z), lie);
            int planks = 8;
            for (int p = 0; p < planks; p++)
            {
                float z0 = -Wide * 0.5f + Wide * p / planks + 0.01f, z1 = -Wide * 0.5f + Wide * (p + 1) / planks - 0.01f;
                PlazaWalls.Lay(b, new[]
                {
                    P(-Length * 0.5f, 0f, z1), P(Length * 0.5f, 0f, z1), P(Length * 0.5f, 0.12f, z1), P(-Length * 0.5f, 0.12f, z1),
                    P(-Length * 0.5f, 0f, z0), P(Length * 0.5f, 0f, z0), P(Length * 0.5f, 0.12f, z0), P(-Length * 0.5f, 0.12f, z0),
                }, 0.02f, _ => random.Chance(0.5f) ? stone : dark);
            }
            foreach (float side in new[] { -1f, 1f })
            {
                PlazaWalls.Lay(b, new[]
                {
                    P(-Length * 0.5f, 0f, side * Wide * 0.5f), P(Length * 0.5f, 0f, side * Wide * 0.5f), P(Length * 0.5f, Deep, side * Wide * 0.5f), P(-Length * 0.5f, Deep, side * Wide * 0.5f),
                    P(-Length * 0.5f, 0f, side * (Wide * 0.5f - 0.14f)), P(Length * 0.5f, 0f, side * (Wide * 0.5f - 0.14f)), P(Length * 0.5f, Deep, side * (Wide * 0.5f - 0.14f)), P(-Length * 0.5f, Deep, side * (Wide * 0.5f - 0.14f)),
                }, 0.025f, _ => stone);
                for (int k = 0; k < 4; k++)
                {
                    float x = -Length * 0.5f + 0.6f + (Length - 1.2f) * k / 3f;
                    b.Box(P(x, Deep * 0.5f, side * (Wide * 0.5f + 0.03f)), new Vector3(0.18f, Deep * 0.48f, 0.03f), rust);
                }
            }
            // Wheels on the axles under it, iron-shod, one of them broken half away.
            foreach (float x in new[] { -Length * 0.32f, Length * 0.32f })
            {
                foreach (float side in new[] { -1f, 1f })
                {
                    Vector3 hub = P(x, -0.55f, side * (Wide * 0.5f + 0.25f));
                    Vector3 axle = Vector3.TransformNormal(Vector3.UnitZ, lie) * side;
                    bool broken = random.Chance(0.3f);
                    Wheel(b, hub, axle, 1.15f, broken, stone, rust);
                }
            }
            // The pole, snapped, lying off the front.
            b.Tube(P(Length * 0.5f, 0.2f, 0f), P(Length * 0.5f + 2.4f, -0.3f, 0.4f), 0.14f, 0.1f, 6, dark);
        }

        private static void Wheel(MeshBuilder b, Vector3 hub, Vector3 axle, float radius, bool broken, VertexMaterial wood, VertexMaterial iron)
        {
            Vector3 u = Vector3.Cross(axle, Vector3.UnitY).Normalized();
            if (u.LengthSquared < 0.5f) u = Vector3.UnitX;
            Vector3 v = Vector3.Cross(axle, u).Normalized();
            int segments = 14, kept = broken ? 8 : segments;
            for (int k = 0; k < kept; k++)
            {
                float a0 = k * MathF.Tau / segments, a1 = (k + 1) * MathF.Tau / segments;
                Vector3 p0 = hub + (u * MathF.Cos(a0) + v * MathF.Sin(a0)) * radius, p1 = hub + (u * MathF.Cos(a1) + v * MathF.Sin(a1)) * radius;
                b.Tube(p0, p1, 0.11f, 0.11f, 4, iron, capped: false);
                if (k % 2 == 0) b.Tube(hub, p0, 0.06f, 0.05f, 4, wood);
            }
            b.Tube(hub - axle * 0.2f, hub + axle * 0.2f, 0.2f, 0.2f, 8, wood);
        }

        /// <summary>A standard of the charge, its pole of iron driven into the ash at a lean, and the Reich's pennant on it turned to stone, grey where its oxblood was and dark where its brass was.</summary>
        public static void Standard(MeshBuilder b, float lean, uint seed)
        {
            var random = new WorldRandom(seed).Fork("standard");
            Vector3 up = Vector3.Transform(Vector3.UnitY, Quaternion.FromAxisAngle(Vector3.UnitX, MathHelper.DegreesToRadians(lean)));
            VertexMaterial rust = new VertexMaterial(MaterialCatalog.Iron.Index, new Colour(0.2f, 0.12f, 0.08f));
            Vector3 foot = Vector3.UnitY * -0.4f, top = foot + up * 7.4f;
            b.Tube(foot, top, 0.09f, 0.07f, 6, rust);
            b.Tube(top, top + up * 0.6f, 0.12f, 0f, 5, rust);
            // The pennant, stiff in stone as it was flying, in a few strips.
            VertexMaterial stone = new VertexMaterial(MaterialCatalog.Rock.Index, new Colour(Petrified * 0.95f));
            VertexMaterial under = new VertexMaterial(MaterialCatalog.Rock.Index, new Colour(Petrified * 0.7f));
            Vector3 back = Vector3.Cross(up, Vector3.UnitX).Normalized();
            Vector3 hoist = top - up * 0.3f;
            const int Strips = 5;
            for (int s = 0; s < Strips; s++)
            {
                float t0 = s / (float)Strips, t1 = (s + 1) / (float)Strips;
                float wave0 = MathF.Sin(t0 * 5f) * 0.18f, wave1 = MathF.Sin(t1 * 5f) * 0.18f;
                Vector3 a = hoist + back * (t0 * 2.6f) + Vector3.UnitX * wave0, c = hoist + back * (t1 * 2.6f) + Vector3.UnitX * wave1;
                float h0 = 1.6f * (1f - t0 * 0.45f), h1 = 1.6f * (1f - t1 * 0.45f);
                ArenaWalls.TwoSided(b, a, c, c - up * h1, a - up * h0, stone, under);
            }
            ArenaStone.Mark(b, hoist + back * 0.9f - up * 0.75f + Vector3.UnitX * 0.03f, back, up, 0.9f, 0.02f, new VertexMaterial(MaterialCatalog.Rock.Index, new Colour(Petrified * 0.55f)), stone);
        }

        // ---------------------------------------------------------------- The dig

        /// <summary>
        /// The bones of an old beast in a quarry's cut face, its face on z = 0 towards +z: a spine curving along it, ribs off it, a leg or two and a long skull, standing out of the rock as the Ossuary has
        /// cut them free, <paramref name="size"/> times the usual.
        /// </summary>
        public static void FossilBed(MeshBuilder b, float size, uint seed)
        {
            var random = new WorldRandom(seed).Fork("fossil");
            float s = size;
            VertexMaterial bone = new VertexMaterial(MaterialCatalog.Bone.Index, new Colour(Fossil * random.Range(0.9f, 1.04f)));
            VertexMaterial stained = new VertexMaterial(MaterialCatalog.Bone.Index, new Colour(Fossil * 0.66f));
            // The spine, an arc of vertebrae.
            int vertebrae = 16;
            float arc = random.Range(0.5f, 0.9f);
            Vector3 Spine(float t) => new Vector3((t - 0.5f) * 7f * s, (2.2f + MathF.Sin(t * MathF.PI) * arc - t * 0.6f) * s, 0.12f * s);
            for (int i = 0; i < vertebrae; i++)
            {
                float t = i / (float)(vertebrae - 1);
                Vector3 at = Spine(t);
                float r = (0.16f + 0.08f * MathF.Sin(t * MathF.PI)) * s;
                b.Tube(at - Vector3.UnitX * r * 0.6f, at + Vector3.UnitX * r * 0.6f, r, r, 6, i % 3 == 0 ? stained : bone);
                b.Tube(at, at + Vector3.UnitY * r * 2.2f, r * 0.35f, r * 0.15f, 4, bone);
            }
            // Ribs down from the middle of it.
            for (int i = 4; i < 12; i++)
            {
                float t = i / (float)(vertebrae - 1);
                Vector3 root = Spine(t);
                Vector3 previous = root;
                for (int k = 1; k <= 4; k++)
                {
                    float f = k / 4f;
                    Vector3 point = root + new Vector3(MathF.Sin(f * 1.2f) * 0.5f * s, -f * 1.9f * s, 0.05f * s * MathF.Sin(f * MathF.PI));
                    b.Tube(previous, point, 0.07f * s, 0.05f * s, 4, bone);
                    previous = point;
                }
            }
            // A leg, and the skull at the head of the spine.
            Vector3 hip = Spine(0.75f);
            b.Tube(hip, hip + new Vector3(0.6f, -1.4f, 0.04f) * s, 0.14f * s, 0.1f * s, 5, bone);
            b.Tube(hip + new Vector3(0.6f, -1.4f, 0.04f) * s, hip + new Vector3(0.2f, -2.4f, 0.06f) * s, 0.1f * s, 0.08f * s, 5, stained);
            Vector3 head = Spine(0f) - Vector3.UnitX * 0.4f * s;
            b.Tube(head + Vector3.UnitX * 0.2f * s, head - Vector3.UnitX * 1.7f * s, 0.42f * s, 0.16f * s, 7, bone);
            b.Tube(head - new Vector3(0.1f, 0.38f, -0.05f) * s, head - new Vector3(1.5f, 0.42f, -0.05f) * s, 0.12f * s, 0.07f * s, 6, stained);
        }

        /// <summary>
        /// The Ossuary's scaffold against a cut face whose face is at z = 0, towards +z: poles, ledgers and braces lashed together, a deck of planks at each lift, and a ladder up it.
        /// </summary>
        public static void Scaffold(MeshBuilder b, float width, float height, uint seed)
        {
            var random = new WorldRandom(seed).Fork("scaffold");
            VertexMaterial pole = ArenaStone.Timber(random, 0.75f), plank = ArenaStone.Timber(random, 0.9f), rope = ArenaStone.Leather(1.4f);
            int bays = Math.Max(1, (int)MathF.Round(width / 2.6f));
            float bay = width / bays;
            for (int k = 0; k <= bays; k++)
            {
                float x = -width * 0.5f + k * bay;
                foreach (float z in new[] { 0.3f, 1.9f }) b.Tube(new Vector3(x, -0.3f, z), new Vector3(x, height + 0.8f, z), 0.08f, 0.07f, 5, pole);
            }
            for (float y = 1.6f; y < height; y += 1.8f)
            {
                foreach (float z in new[] { 0.3f, 1.9f }) b.Tube(new Vector3(-width * 0.5f - 0.2f, y, z), new Vector3(width * 0.5f + 0.2f, y, z), 0.06f, 0.06f, 4, pole);
                for (int p = 0; p < (int)(width / 0.36f); p++)
                {
                    float x0 = -width * 0.5f + p * 0.36f + 0.01f;
                    b.Box(new Vector3(x0 + 0.17f, y + 0.08f, 1.1f), new Vector3(0.16f, 0.03f, 0.95f), plank);
                }
                for (int k = 0; k < bays; k++)
                {
                    float x0 = -width * 0.5f + k * bay;
                    b.Tube(new Vector3(x0, y - 1.6f, 1.95f), new Vector3(x0 + bay, y, 1.95f), 0.04f, 0.04f, 4, pole);
                    b.Tube(new Vector3(x0, y - 0.05f, 0.3f), new Vector3(x0, y + 0.06f, 0.36f), 0.1f, 0.1f, 4, rope);
                }
            }
            // A ladder up the outer face.
            float lx = width * 0.5f - 0.6f;
            foreach (float s in new[] { -0.3f, 0.3f }) b.Tube(new Vector3(lx + s, -0.2f, 2.2f), new Vector3(lx + s, height, 2.05f), 0.05f, 0.05f, 4, pole);
            for (float y = 0.3f; y < height; y += 0.42f) b.Tube(new Vector3(lx - 0.3f, y, 2.2f - y * 0.02f), new Vector3(lx + 0.3f, y, 2.2f - y * 0.02f), 0.03f, 0.03f, 4, plank);
        }

        /// <summary>
        /// The Ossuary's crane on a quarry's rim: an A-frame of timber, a boom out towards +z over the dig, a rope from its end to a block of the cut face hanging with bones in it, and a windlass.
        /// </summary>
        public static void Crane(MeshBuilder b, float reach, uint seed)
        {
            var random = new WorldRandom(seed).Fork("crane");
            VertexMaterial timber = ArenaStone.Timber(random, 0.7f), iron = ArenaStone.Iron(random), rope = ArenaStone.Leather(1.3f);
            float height = 8f;
            foreach (float s in new[] { -1f, 1f }) b.Tube(new Vector3(s * 1.6f, -0.3f, -1f), new Vector3(0f, height, 0f), 0.18f, 0.14f, 6, timber);
            b.Tube(new Vector3(0f, -0.3f, -3.2f), new Vector3(0f, height, 0f), 0.16f, 0.13f, 6, timber);
            Vector3 tip = new Vector3(0f, height + 1.2f, reach);
            b.Tube(new Vector3(0f, height - 0.5f, -1.6f), tip, 0.16f, 0.1f, 6, timber);
            b.Tube(new Vector3(0f, height, 0f), tip, 0.04f, 0.04f, 4, iron);
            Vector3 block = tip - Vector3.UnitY * 5.4f;
            b.Tube(tip, block + Vector3.UnitY * 1f, 0.03f, 0.03f, 4, rope);
            ArenaStone.Block(b, block, new Vector3(1f, 0.9f, 0.8f), random.Range(0f, 0.6f), 0.08f, ArenaStone.Dressed(ArenaStoneKind.Basalt, random, 1f, 0.1f));
            b.Tube(block + new Vector3(-0.9f, 0.3f, 0.82f), block + new Vector3(0.9f, 0.1f, 0.82f), 0.12f, 0.1f, 6, new VertexMaterial(MaterialCatalog.Bone.Index, new Colour(Fossil)));
            b.Tube(new Vector3(-1f, 1.2f, -1.4f), new Vector3(1f, 1.2f, -1.4f), 0.22f, 0.22f, 8, timber);
        }

        // ---------------------------------------------------------------- Vents

        /// <summary>A crack in the ash that breathes fire, <paramref name="size"/> long: a ragged split with a crust of cooled ash heaved up either side, and the glow of what's under it.</summary>
        public static void Vent(MeshBuilder b, float size, uint seed, List<AssetLight> lights)
        {
            var random = new WorldRandom(seed).Fork("vent");
            VertexMaterial crust = new VertexMaterial(MaterialCatalog.CrackedLava.Index, new Colour(0.2f, 0.19f, 0.18f));
            VertexMaterial glow = ArenaStone.Glow(1f, 0.36f, 0.06f, 2.2f);
            float half = size * 0.5f, y = ArenaStone.Ground;
            const int Steps = 9;
            Vector3 previous = default;
            for (int i = 0; i <= Steps; i++)
            {
                float t = i / (float)Steps;
                float x = -half + size * t;
                float z = MathF.Sin(t * 5.3f + random.Range(0f, 1f)) * 0.45f;
                float w = 0.12f + 0.32f * MathF.Sin(t * MathF.PI);
                Vector3 at = new Vector3(x, y + 0.02f, z);
                if (i > 0)
                {
                    // Everything of it faces up, whichever way round its corners come.
                    Up(b, previous - Vector3.UnitZ * w, at - Vector3.UnitZ * w, at + Vector3.UnitZ * w, previous + Vector3.UnitZ * w, glow);
                    foreach (float s in new[] { -1f, 1f })
                    {
                        Vector3 edge0 = previous + Vector3.UnitZ * s * w, edge1 = at + Vector3.UnitZ * s * w;
                        Vector3 lip0 = edge0 + new Vector3(0f, 0.22f, s * 0.35f), lip1 = edge1 + new Vector3(0f, 0.22f, s * 0.35f);
                        Vector3 out0 = edge0 + new Vector3(0f, -0.04f, s * 0.9f), out1 = edge1 + new Vector3(0f, -0.04f, s * 0.9f);
                        Up(b, edge0, edge1, lip1, lip0, crust);
                        Up(b, lip0, lip1, out1, out0, crust);
                    }
                }
                previous = at;
            }
            lights.Add(AssetLight.Glow(new Vector3(0f, 0.6f, 0f), new Vector3(1f, 0.38f, 0.08f), 38f, 9f) with { Flicker = 0.3f, FlickerSpeed = 4f });
        }
    }
}
