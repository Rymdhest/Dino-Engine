using Dino_Engine.Modelling.Model;
using Dino_Engine.Textures;
using Dino_Engine.Util;
using Dino_Engine.World;
using Dino_Engine.World.Content;
using OpenTK.Mathematics;
using static Dino_Engine.Modelling.Procedural.Architecture.Places.PlaceMaterials;

namespace Dino_Engine.Modelling.Procedural.Architecture.Places
{
    /// <summary>
    /// What whoever holds a place lives and works with, and what the land's people left (PLACES.md): racks of weapons, torches on poles, wood stacked with its ends to the camera, wagons,
    /// the Reich's gibbet and its barricades, a brood's command tent with its maps, signposts with the Reich's names nailed over the land's, braziers and a table to eat at. The Reich's
    /// are black iron, oxblood and brass, and nothing else is. Model +z is a piece's front, y = 0 where people stand. Headless.
    /// </summary>
    internal static class Camp
    {
        /// <summary>The frost cult's cold flame, a pale violet that's nobody else's (as the snow castle's altar burns).</summary>
        public static readonly Vector3 Frost = new Vector3(0.74f, 0.7f, 1f);

        /// <summary>A torch's light: warm, close, and restless, as a flame in the open is.</summary>
        public static AssetLight TorchLight(Vector3 at) => AssetLight.Lantern(at) with { Intensity = 70f, Radius = 11f, Flicker = 0.28f, FlickerSpeed = 8f };

        // ---------------------------------------------------------------- Weapons

        /// <summary>
        /// A rack of weapons on two A-frames: the Reich's spears with oxblood pennons under their heads, its hooked goads and its shields with the mark hung on the front; or the land's
        /// own, pitchforks, axes, a scythe and a bow, as its people armed themselves. A gap here and there where one was taken. Numbers: width, whose (1 the Reich's).
        /// </summary>
        public static void WeaponRack(MeshBuilder b, in PlaceSpec spec, WorldRandom random)
        {
            float width = Math.Clamp(spec.Metres(0), 1.6f, 5f);
            bool reich = spec.Int(1) == 1;
            VertexMaterial wood = reich ? Timber(random, 0.62f) : Weathered(random);
            VertexMaterial metal = reich ? Iron(random) : LandIron(random);
            float hx = width * 0.5f;
            foreach (float x in new[] { -hx, hx })
            {
                Kit.Plank(b, new Vector3(x, -0.2f, 0.5f), new Vector3(x, 1.78f, 0f), Vector3.UnitX, 0.1f, 0.1f, wood);
                Kit.Plank(b, new Vector3(x, -0.2f, -0.5f), new Vector3(x, 1.78f, 0f), Vector3.UnitX, 0.1f, 0.1f, wood);
            }
            Kit.Plank(b, new Vector3(-hx - 0.18f, 1.72f, 0f), new Vector3(hx + 0.18f, 1.72f, 0f), Vector3.UnitY, 0.11f, 0.11f, wood);
            foreach (float z in new[] { 0.34f, -0.34f }) Kit.Plank(b, new Vector3(-hx, 0.32f, z), new Vector3(hx, 0.32f, z), Vector3.UnitY, 0.08f, 0.12f, wood);
            int slots = Math.Max(3, (int)(width / 0.3f));
            for (int i = 0; i < slots; i++)
            {
                if (random.Chance(0.14f)) continue;
                bool front = i % 2 == 0;
                float x = -hx + 0.22f + (width - 0.44f) * i / (slots - 1f) + random.Range(-0.03f, 0.03f);
                Vector3 foot = new Vector3(x, Ground, front ? 0.46f : -0.46f);
                Vector3 rest = new Vector3(x + random.Range(-0.05f, 0.05f), 1.76f, front ? 0.07f : -0.07f);
                Vector3 dir = (rest - foot).Normalized();
                if (reich) ReichArm(b, foot, dir, random, wood, metal, i);
                else if (spec.Int(1) == 2) MasonArm(b, foot, dir, random, wood, metal, i);
                else LandArm(b, foot, dir, random, wood, metal, i);
            }
            if (!reich) return;
            // Its shields on the front rail, the mark on each.
            int shields = Math.Max(1, (int)(width / 1.2f));
            for (int k = 0; k < shields; k++)
            {
                float x = -hx + width * (k + 0.5f) / shields;
                Vector3 c = new Vector3(x, 0.95f, 0.5f);
                Quaternion lean = Quaternion.FromAxisAngle(Vector3.UnitX, -0.28f);
                Vector3 up = Vector3.Transform(Vector3.UnitY, lean), face = Vector3.Transform(Vector3.UnitZ, lean);
                Kit.Box(b, c, Vector3.UnitX, up, face, new Vector3(0.36f, 0.5f, 0.035f), 0.01f, Plating(random, 0.9f));
                Kit.Box(b, c + face * 0.04f, Vector3.UnitX, up, face, new Vector3(0.3f, 0.43f, 0.01f), 0f, OxbloodPaint());
                Kit.Mark(b, c + face * 0.056f, Vector3.UnitX, up, 0.42f, 0.01f, random);
            }
        }

        // One of the Reich's arms against the rack: a spear with its pennon, or a goad with its hook.
        private static void ReichArm(MeshBuilder b, Vector3 foot, Vector3 dir, WorldRandom random, VertexMaterial wood, VertexMaterial metal, int i)
        {
            float length = random.Range(2.5f, 3f);
            Vector3 tip = foot + dir * length;
            Kit.Pole(b, foot, tip, 0.028f, i % 3 == 2 ? ReichLeather(1.5f) : wood);
            Vector3 side = Vector3.Cross(dir, Vector3.UnitX).Normalized();
            if (i % 3 == 2)
            {
                // A goad: an iron hook and a spike.
                Kit.Pole(b, tip, tip + dir * 0.22f, 0.02f, metal, 0.004f);
                Kit.Pole(b, tip - dir * 0.05f, tip + dir * 0.08f + side * 0.14f, 0.018f, metal, 0.004f);
                return;
            }
            Kit.Lathe(b, tip, dir, 0.34f, Kit.Profile((0f, 0.035f), (0.25f, 0.055f), (1f, 0f)), 4, 2, metal);
            b.Tube(tip - dir * 0.02f, tip + dir * 0.03f, 0.04f, 0.04f, 6, Brass(random), capped: false);
            // Its pennon, a strip of oxblood hanging off it under the head.
            Vector3 hang = tip - dir * 0.08f;
            Vector3 across = Vector3.Cross(dir, side).Normalized();
            Kit.Sheet(b, hang, hang - dir * 0.34f, hang - dir * 0.3f - Vector3.UnitY * 0.28f + across * 0.05f, hang - Vector3.UnitY * 0.22f + across * 0.05f, Oxblood(), Oxblood(0.8f));
        }

        // One of the land's against the rack: a pitchfork, an axe, a scythe or a bow.
        private static void LandArm(MeshBuilder b, Vector3 foot, Vector3 dir, WorldRandom random, VertexMaterial wood, VertexMaterial metal, int i)
        {
            Vector3 side = Vector3.Cross(dir, Vector3.UnitX).Normalized();
            switch (i % 4)
            {
                case 0:
                {
                    Vector3 tip = foot + dir * random.Range(1.7f, 2f);
                    Kit.Pole(b, foot, tip, 0.025f, wood);
                    for (int k = -1; k <= 1; k++) Kit.Pole(b, tip + side * (k * 0.08f), tip + side * (k * 0.09f) + dir * 0.32f, 0.012f, metal, 0.004f);
                    Kit.Pole(b, tip - side * 0.1f, tip + side * 0.1f, 0.014f, metal);
                    break;
                }
                case 1:
                {
                    // An axe stood on its head, its handle up.
                    Vector3 head = foot + Vector3.UnitY * 0.06f;
                    Kit.Block(b, head + Vector3.UnitY * 0.08f, new Vector3(0.11f, 0.08f, 0.04f), 0f, 0.005f, metal);
                    Kit.Pole(b, head + Vector3.UnitY * 0.14f, head + dir * 0.9f + Vector3.UnitY * 0.1f, 0.022f, wood);
                    break;
                }
                case 2:
                {
                    Vector3 tip = foot + dir * random.Range(1.6f, 1.9f);
                    Kit.Pole(b, foot, tip, 0.024f, wood);
                    var blade = new List<Vector3>();
                    for (int k = 0; k <= 4; k++)
                    {
                        float t = k / 4f;
                        blade.Add(tip + side * (0.7f * t) - dir * (0.25f * t * t) + Vector3.UnitY * 0.02f);
                    }
                    Kit.Bend(b, blade, 0.025f, 0.006f, metal);
                    break;
                }
                default:
                {
                    var bow = new List<Vector3>();
                    for (int k = 0; k <= 6; k++)
                    {
                        float t = k / 6f;
                        bow.Add(foot + dir * (1.5f * t) + side * (MathF.Sin(t * MathF.PI) * 0.18f));
                    }
                    Kit.Bend(b, bow, 0.022f, 0.016f, Timber(random, 0.8f));
                    Kit.Pole(b, foot, foot + dir * 1.5f, 0.005f, Flat(new Vector3(0.6f, 0.58f, 0.5f)));
                    break;
                }
            }
        }

        // One of the quarrymen's tools against the rack: a pick, a sledgehammer or a crowbar, stood on its head.
        private static void MasonArm(MeshBuilder b, Vector3 foot, Vector3 dir, WorldRandom random, VertexMaterial wood, VertexMaterial metal, int i)
        {
            Vector3 side = Vector3.Cross(dir, Vector3.UnitX).Normalized();
            switch (i % 3)
            {
                case 0:
                {
                    Vector3 head = foot + Vector3.UnitY * 0.1f;
                    Kit.Pole(b, head - side * 0.32f + Vector3.UnitY * 0.05f, head + side * 0.32f + Vector3.UnitY * 0.05f, 0.03f, metal, 0.008f);
                    Kit.Pole(b, head + Vector3.UnitY * 0.05f, head + dir * 0.95f, 0.024f, wood);
                    break;
                }
                case 1:
                {
                    Vector3 head = foot + Vector3.UnitY * 0.11f;
                    Kit.Block(b, head, new Vector3(0.12f, 0.08f, 0.08f), 0f, 0.01f, metal);
                    Kit.Pole(b, head + Vector3.UnitY * 0.06f, head + dir * 0.95f, 0.025f, wood);
                    break;
                }
                default:
                    Kit.Pole(b, foot, foot + dir * random.Range(1.3f, 1.6f), 0.022f, metal);
                    break;
            }
        }

        // ---------------------------------------------------------------- Light

        /// <summary>
        /// A torch on a pole, burning: the land's, a pole of timber with a pitch-soaked head bound in rope; the Reich's, an iron cresset on a black post with a brass collar; a fire cult's,
        /// a crooked branch with a beast's horns and red rags lashed under its flame; or the frost cult's, bleached wood hung with icicles and a cold violet flame that gives no heat.
        /// Numbers: height, form (0 the land's, 1 the Reich's, 2 a fire cult's, 3 the frost cult's).
        /// </summary>
        public static void Torch(MeshBuilder b, in PlaceSpec spec, WorldRandom random, List<AssetLight> lights)
        {
            float height = Math.Clamp(spec.Metres(0), 1.4f, 4.2f);
            int form = Math.Clamp(spec.Int(1), 0, 4);
            Vector3 head = new Vector3(0f, height, 0f);
            switch (form)
            {
                case 1:
                {
                    VertexMaterial iron = Iron(random), brass = Brass(random);
                    Kit.Pole(b, new Vector3(0f, -0.4f, 0f), head, 0.07f, iron, 0.05f);
                    Kit.Lathe(b, Vector3.Zero, Vector3.UnitY, 0.35f, Kit.Profile((0f, 0.22f), (1f, 0.09f)), 6, 1, iron);
                    b.Tube(head - Vector3.UnitY * 0.5f, head - Vector3.UnitY * 0.4f, 0.09f, 0.09f, 8, brass, capped: false);
                    // The cresset: an iron basket of bars, coals in it.
                    for (int k = 0; k < 6; k++)
                    {
                        float a = k * MathF.Tau / 6f;
                        Kit.Pole(b, head, head + new Vector3(MathF.Cos(a) * 0.26f, 0.42f, MathF.Sin(a) * 0.26f), 0.018f, iron);
                    }
                    b.Tube(head + Vector3.UnitY * 0.4f, head + Vector3.UnitY * 0.44f, 0.27f, 0.27f, 8, iron, capped: false);
                    Kit.Heap(b, head + Vector3.UnitY * 0.12f, new Vector2(0.2f), 0.14f, 0f, Coals, random, 2, 7);
                    lights.Add(AssetLight.Burning(head + Vector3.UnitY * 0.2f, 0.32f));
                    lights.Add(TorchLight(head + Vector3.UnitY * 0.7f));
                    break;
                }
                case 2:
                {
                    VertexMaterial wood = Timber(random, 0.65f);
                    var points = new List<Vector3>();
                    for (int k = 0; k <= 4; k++) points.Add(new Vector3(random.Range(-0.12f, 0.12f) * k / 4f, -0.4f + (height + 0.4f) * k / 4f, random.Range(-0.12f, 0.12f) * k / 4f));
                    Kit.Bend(b, points, 0.07f, 0.05f, wood);
                    Vector3 top = points[^1];
                    // A beast's horns lashed on under the flame, red rags off them.
                    foreach (float s in new[] { -1f, 1f })
                    {
                        var horn = new List<Vector3> { top - Vector3.UnitY * 0.45f, top - Vector3.UnitY * 0.4f + new Vector3(s * 0.3f, 0.12f, 0f), top - Vector3.UnitY * 0.2f + new Vector3(s * 0.45f, 0.42f, 0.05f) };
                        Kit.Bend(b, horn, 0.05f, 0.01f, Bone(random, 0.9f));
                        Vector3 rag = top - Vector3.UnitY * 0.42f + new Vector3(s * 0.2f, 0.05f, 0f);
                        Kit.Sheet(b, rag, rag + new Vector3(s * 0.08f, -0.5f, 0.04f), rag + new Vector3(s * 0.16f, -0.45f, 0.06f), rag + new Vector3(s * 0.1f, 0f, 0.02f), Painted(new Vector3(0.42f, 0.08f, 0.04f)), Painted(new Vector3(0.32f, 0.06f, 0.03f)));
                    }
                    Kit.Lathe(b, top - Vector3.UnitY * 0.1f, Vector3.UnitY, 0.32f, Kit.Profile((0f, 0.08f), (0.5f, 0.13f), (1f, 0.1f)), 6, 2, Flat(new Vector3(0.06f, 0.05f, 0.04f)));
                    lights.Add(AssetLight.Burning(top + Vector3.UnitY * 0.2f, 0.36f));
                    lights.Add(TorchLight(top + Vector3.UnitY * 0.7f) with { Colour = new Vector3(1f, 0.55f, 0.22f) });
                    break;
                }
                case 4:
                {
                    // The Ossuary's lamp: a cage of black iron on its post, and in it the cold light it raises its dead with.
                    VertexMaterial iron = Iron(random), brass = Brass(random);
                    Kit.Pole(b, new Vector3(0f, -0.4f, 0f), head, 0.06f, iron, 0.045f);
                    Kit.Pole(b, head, head + new Vector3(0f, 0f, 0.55f), 0.035f, iron);
                    Vector3 cage = head + new Vector3(0f, -0.35f, 0.55f);
                    for (int k = 0; k < 4; k++)
                    {
                        float a = k * MathF.Tau / 4f + MathF.PI * 0.25f;
                        Kit.Pole(b, cage + new Vector3(MathF.Cos(a) * 0.14f, -0.2f, MathF.Sin(a) * 0.14f), cage + new Vector3(MathF.Cos(a) * 0.14f, 0.22f, MathF.Sin(a) * 0.14f), 0.015f, iron);
                    }
                    Kit.Lathe(b, cage + Vector3.UnitY * 0.22f, Vector3.UnitY, 0.14f, Kit.Profile((0f, 0.2f), (1f, 0.02f)), 4, 1, iron);
                    Kit.Lathe(b, cage - Vector3.UnitY * 0.26f, Vector3.UnitY, 0.06f, _ => 0.19f, 4, 1, brass);
                    Kit.Ball(b, cage, new Vector3(0.1f, 0.16f, 0.1f), 6, 4, OssuaryGlow(2f));
                    lights.Add(Cold(cage, 30f, 9f) with { Flicker = 0.08f, FlickerSpeed = 1.1f });
                    break;
                }
                case 3:
                {
                    VertexMaterial wood = Bleached(random, 0.95f), ice = Ice(1.1f);
                    Kit.Pole(b, new Vector3(0f, -0.4f, 0f), head, 0.06f, wood, 0.045f);
                    for (int k = 0; k < 5; k++)
                    {
                        float a = k * MathF.Tau / 5f + random.Range(-0.3f, 0.3f);
                        Vector3 at = head - Vector3.UnitY * random.Range(0.15f, 0.4f) + new Vector3(MathF.Cos(a) * 0.08f, 0f, MathF.Sin(a) * 0.08f);
                        Kit.Lathe(b, at, -Vector3.UnitY, random.Range(0.2f, 0.45f), Kit.Profile((0f, 0.035f), (1f, 0f)), 4, 1, ice);
                    }
                    // The cold flame: tongues of light, which nothing burns in.
                    for (int k = 0; k < 3; k++)
                    {
                        Vector3 at = head + new Vector3(random.Range(-0.06f, 0.06f), 0.02f, random.Range(-0.06f, 0.06f));
                        Kit.Lathe(b, at, Vector3.UnitY + new Vector3(random.Range(-0.15f, 0.15f), 0f, random.Range(-0.15f, 0.15f)), random.Range(0.35f, 0.6f), Kit.Profile((0f, 0.08f), (0.35f, 0.1f), (1f, 0f)), 5, 2, Glow(Frost.X, Frost.Y, Frost.Z, 2.2f));
                    }
                    lights.Add(AssetLight.Glow(head + Vector3.UnitY * 0.5f, Frost, 34f, 9f) with { Flicker = 0.12f, FlickerSpeed = 3f });
                    break;
                }
                default:
                {
                    VertexMaterial wood = Timber(random, 0.85f);
                    Kit.Pole(b, new Vector3(0f, -0.4f, 0f), head, 0.055f, wood, 0.045f);
                    b.Tube(head - Vector3.UnitY * 0.32f, head - Vector3.UnitY * 0.18f, 0.06f, 0.06f, 6, Rope(random), capped: false);
                    Kit.Lathe(b, head - Vector3.UnitY * 0.18f, Vector3.UnitY, 0.3f, Kit.Profile((0f, 0.06f), (0.5f, 0.1f), (1f, 0.08f)), 6, 2, Flat(new Vector3(0.07f, 0.055f, 0.04f)));
                    lights.Add(AssetLight.Burning(head + Vector3.UnitY * 0.12f, 0.24f));
                    lights.Add(TorchLight(head + Vector3.UnitY * 0.6f));
                    break;
                }
            }
        }

        /// <summary>The Reich's brazier standing by itself, its coals burning, for its people to warm their hands at. Numbers: height.</summary>
        public static void Brazier(MeshBuilder b, in PlaceSpec spec, WorldRandom random, List<AssetLight> lights)
        {
            float height = Math.Clamp(spec.Metres(0), 0.8f, 1.6f);
            Kit.Brazier(b, Vector3.Zero, height, random, lights);
            // A few logs ready beside it.
            VertexMaterial wood = Timber(random, 0.8f);
            for (int k = 0; k < 3; k++) Kit.Pole(b, new Vector3(0.75f, Ground + 0.09f + (k == 2 ? 0.15f : 0f), -0.3f + k * 0.2f - (k == 2 ? 0.1f : 0f)), new Vector3(1.45f, Ground + 0.09f + (k == 2 ? 0.15f : 0f), -0.2f + k * 0.18f), 0.09f, wood);
        }

        // ---------------------------------------------------------------- Wood and wheels

        /// <summary>
        /// Split wood stacked between end posts with its ends to the front, the camera's way, so a stack is a wall of pale log ends in the dark of its bark; a chopping block with the axe
        /// left in it and the next logs waiting by it. Numbers: length, height.
        /// </summary>
        public static void Firewood(MeshBuilder b, in PlaceSpec spec, WorldRandom random)
        {
            float length = Math.Clamp(spec.Metres(0), 1.2f, 8f);
            float high = Math.Clamp(spec.Metres(1), 0.6f, 2.2f);
            const float Depth = 0.9f;
            float hx = length * 0.5f;
            VertexMaterial post = Weathered(random, 0.85f), bark = Timber(random, 0.7f);
            foreach (float x in new[] { -hx - 0.1f, hx + 0.1f })
            {
                foreach (float z in new[] { -Depth * 0.5f, Depth * 0.5f }) Kit.Pole(b, new Vector3(x, -0.2f, z), new Vector3(x, high + 0.25f, z), 0.06f, post);
            }
            // Bearers off the ground, and the rows on them.
            foreach (float z in new[] { -Depth * 0.32f, Depth * 0.32f }) Kit.Pole(b, new Vector3(-hx, Ground + 0.06f, z), new Vector3(hx, Ground + 0.06f, z), 0.06f, post);
            float y = Ground + 0.12f;
            int row = 0;
            while (y < high)
            {
                float x = -hx + random.Range(0.08f, 0.14f);
                while (x < hx - 0.08f)
                {
                    float r = random.Range(0.07f, 0.12f);
                    if (x + r > hx) break;
                    Vector3 foot = new Vector3(x + r, y + r, -Depth * 0.5f + random.Range(-0.04f, 0.04f));
                    // A split piece: a few sides, its ends cut pale.
                    Kit.Lathe(b, foot, Vector3.UnitZ, Depth + random.Range(-0.06f, 0.06f), _ => r, 5, 1, bark);
                    Kit.Lathe(b, foot + Vector3.UnitZ * (Depth + 0.07f), Vector3.UnitZ, 0.008f, _ => r * 0.92f, 5, 1, Fresh(random, random.Range(0.8f, 1.1f)));
                    x += r * 2f + random.Range(0f, 0.02f);
                }
                y += 0.19f;
                row++;
            }
            // The chopping block, the axe in it, and the next to split.
            Vector3 block = new Vector3(hx * 0.4f, 0f, Depth * 0.5f + 1.1f);
            Kit.Pole(b, block - Vector3.UnitY * 0.2f, block + Vector3.UnitY * 0.62f, 0.3f, Timber(random, 0.75f));
            Kit.Lathe(b, block + Vector3.UnitY * 0.62f, Vector3.UnitY, 0.01f, _ => 0.29f, 9, 1, Fresh(random, 0.85f));
            Vector3 bite = block + new Vector3(0.06f, 0.66f, 0f);
            Kit.Block(b, bite, new Vector3(0.1f, 0.06f, 0.03f), 0.4f, 0.004f, LandIron(random, 1.1f));
            Kit.Pole(b, bite + new Vector3(0f, 0.04f, 0f), bite + new Vector3(-0.5f, 0.45f, 0.2f), 0.022f, Timber(random, 1.1f));
            for (int k = 0; k < 3; k++) Kit.Pole(b, block + new Vector3(-0.9f + k * 0.15f, Ground + 0.14f, -0.3f + k * 0.25f), block + new Vector3(-0.3f + k * 0.12f, Ground + 0.14f, 0.1f + k * 0.2f), 0.14f, bark);
        }

        // A spoked wheel, standing in the plane square to `axle` about `hub`.
        private static void Wheel(MeshBuilder b, Vector3 hub, Vector3 axle, float radius, VertexMaterial wood, VertexMaterial tyre)
        {
            axle = axle.Normalized();
            Vector3 u = MathF.Abs(axle.Y) < 0.9f ? Vector3.Cross(axle, Vector3.UnitY).Normalized() : Vector3.UnitX;
            Vector3 v = Vector3.Cross(u, axle).Normalized();
            const int Rim = 14;
            for (int k = 0; k < Rim; k++)
            {
                float a0 = k * MathF.Tau / Rim, a1 = (k + 1) * MathF.Tau / Rim;
                Vector3 p0 = hub + (u * MathF.Cos(a0) + v * MathF.Sin(a0)) * radius, p1 = hub + (u * MathF.Cos(a1) + v * MathF.Sin(a1)) * radius;
                Kit.Plank(b, p0, p1, (p0 + p1) * 0.5f - hub, 0.09f, 0.07f, tyre, 0.008f);
                Kit.Plank(b, hub + (u * MathF.Cos(a0) + v * MathF.Sin(a0)) * (radius - 0.07f), hub + (u * MathF.Cos(a1) + v * MathF.Sin(a1)) * (radius - 0.07f), axle, 0.09f, 0.06f, wood, 0.008f);
            }
            for (int k = 0; k < 8; k++)
            {
                float a = k * MathF.Tau / 8f;
                Kit.Pole(b, hub, hub + (u * MathF.Cos(a) + v * MathF.Sin(a)) * (radius - 0.08f), 0.03f, wood);
            }
            Kit.Lathe(b, hub - axle * 0.13f, axle, 0.26f, Kit.Profile((0f, 0.08f), (0.5f, 0.12f), (1f, 0.08f)), 8, 2, wood);
        }

        /// <summary>
        /// A four-wheeled wagon, its tongue to the front: the Reich's, iron-bound, with an oxblood tilt over hoops and the mark on its side, loaded at the back; the land's, open, with
        /// sacks and a heap of hay; or either broken down, a wheel off and lying by it and its corner on the ground, what it carried spilled. Numbers: form (0 loaded, 1 broken), whose (1 the
        /// Reich's), length.
        /// </summary>
        public static void Wagon(MeshBuilder b, in PlaceSpec spec, WorldRandom random)
        {
            bool broken = spec.Int(0) == 1;
            bool reich = spec.Int(1) == 1;
            float length = Math.Clamp(spec.Metres(2), 3f, 5.5f);
            float hz = length * 0.5f, hx = 0.85f;
            VertexMaterial wood = reich ? Timber(random, 0.7f) : Weathered(random);
            VertexMaterial iron = reich ? Iron(random) : LandIron(random);
            const float Bed = 1.05f, WheelR = 0.62f;
            // Broken, it drops at its front left corner, where the wheel came off.
            float drop = broken ? 0.55f : 0f;
            float Lift(Vector3 p) => -drop * Math.Clamp((p.Z / hz + 1f) * 0.5f, 0f, 1f) * Math.Clamp((hx - p.X) / (2f * hx), 0f, 1f);
            Vector3 L(Vector3 p) => p + Vector3.UnitY * Lift(p);
            // The bed and its sides.
            Kit.Solid(b, new[]
            {
                L(new Vector3(-hx, Bed - 0.1f, hz)), L(new Vector3(hx, Bed - 0.1f, hz)), L(new Vector3(hx, Bed, hz)), L(new Vector3(-hx, Bed, hz)),
                L(new Vector3(-hx, Bed - 0.1f, -hz)), L(new Vector3(hx, Bed - 0.1f, -hz)), L(new Vector3(hx, Bed, -hz)), L(new Vector3(-hx, Bed, -hz)),
            }, 0.01f, wood);
            foreach (float s in new[] { -1f, 1f })
            {
                Kit.Solid(b, new[]
                {
                    L(new Vector3(s * hx, Bed, hz)), L(new Vector3(s * (hx - 0.06f), Bed, hz)), L(new Vector3(s * (hx - 0.06f), Bed + 0.55f, hz)), L(new Vector3(s * hx, Bed + 0.55f, hz)),
                    L(new Vector3(s * hx, Bed, -hz)), L(new Vector3(s * (hx - 0.06f), Bed, -hz)), L(new Vector3(s * (hx - 0.06f), Bed + 0.55f, -hz)), L(new Vector3(s * hx, Bed + 0.55f, -hz)),
                }, 0.01f, wood);
                for (float z = -hz + 0.3f; z < hz; z += 0.9f) Kit.Plank(b, L(new Vector3(s * (hx + 0.02f), Bed - 0.1f, z)), L(new Vector3(s * (hx + 0.02f), Bed + 0.56f, z)), new Vector3(s, 0f, 0f), 0.06f, 0.02f, iron, 0.004f);
            }
            foreach (float z in new[] { -hz, hz }) Kit.Plank(b, L(new Vector3(-hx, Bed + 0.28f, z)), L(new Vector3(hx, Bed + 0.28f, z)), new Vector3(0f, 0f, MathF.Sign(z)), 0.55f, 0.06f, wood, 0.01f);
            // Axles and wheels; broken, its front left wheel lies on the ground beside it.
            VertexMaterial tyre = iron;
            foreach (float z in new[] { -hz + 0.75f, hz - 0.75f })
            {
                Kit.Pole(b, L(new Vector3(-hx - 0.2f, WheelR, z)), L(new Vector3(hx + 0.2f, WheelR, z)), 0.06f, wood);
                foreach (float s in new[] { -1f, 1f })
                {
                    bool off = broken && s < 0f && z > 0f;
                    if (off) continue;
                    Wheel(b, L(new Vector3(s * (hx + 0.2f), WheelR, z)), Vector3.UnitX, WheelR, wood, tyre);
                }
            }
            if (broken) Wheel(b, new Vector3(-hx - 1.3f, Ground + 0.12f, hz - 0.2f), new Vector3(0.15f, 1f, 0.1f), WheelR, wood, tyre);
            // Its tongue out in front.
            Kit.Pole(b, L(new Vector3(0f, WheelR, hz - 0.6f)), L(new Vector3(0f, 0.45f, hz + 2.2f)) + Vector3.UnitY * (broken ? -0.3f : 0f), 0.07f, wood);
            Kit.Pole(b, L(new Vector3(-0.55f, 0.5f, hz + 1.9f)), L(new Vector3(0.55f, 0.5f, hz + 1.9f)), 0.05f, wood);

            if (reich)
            {
                // The tilt over its hoops, oxblood, the mark on its side.
                VertexMaterial tilt = Oxblood(0.95f), under = Oxblood(0.55f);
                float back = -hz + 0.15f, front = hz - 0.2f;
                const int Steps = 6;
                Vector3 Hoop(float z, float t) => L(new Vector3(MathF.Cos(t * MathF.PI) * (hx - 0.02f), Bed + 0.55f + MathF.Sin(t * MathF.PI) * 0.95f, z));
                for (int k = 0; k < Steps; k++)
                {
                    float t0 = k / (float)Steps, t1 = (k + 1f) / Steps;
                    Kit.Sheet(b, Hoop(front, t0), Hoop(back, t0), Hoop(back, t1), Hoop(front, t1), tilt, under);
                }
                foreach (float z in new[] { back, (back + front) * 0.5f, front })
                {
                    for (int k = 0; k < Steps; k++) Kit.Pole(b, Hoop(z, k / (float)Steps) + Vector3.UnitY * 0.02f, Hoop(z, (k + 1f) / Steps) + Vector3.UnitY * 0.02f, 0.025f, iron);
                }
                foreach (float s in new[] { -1f, 1f })
                {
                    Vector3 side = L(new Vector3(s * (hx + 0.012f), Bed + 0.95f, 0f));
                    Kit.Mark(b, side, new Vector3(0f, 0f, -s), Vector3.UnitY, 0.7f, 0.01f, random);
                }
            }
            // What it carries: crates and sacks at the open back, or the land's sacks and hay, spilled if it broke.
            VertexMaterial boards = Timber(random, 0.95f);
            for (int k = 0; k < 3; k++)
            {
                Vector3 at = L(new Vector3(random.Range(-0.4f, 0.4f), Bed + 0.32f, -hz + 0.45f + k * 0.62f));
                if (reich) Kit.Crate(b, at, random.Range(-0.2f, 0.2f), new Vector3(0.3f), boards, Iron(random), random, marked: k == 0);
                else Kit.Sack(b, at - Vector3.UnitY * 0.3f, random.Float() * MathF.Tau, random.Chance(0.4f), 0.5f, Canvas(random));
            }
            if (!reich) Kit.Heap(b, L(new Vector3(0f, Bed, hz * 0.35f)), new Vector2(0.7f, 1f), 0.6f, 0f, Straw(random), random, 3, 10);
            if (broken)
            {
                for (int k = 0; k < 3; k++)
                {
                    Vector3 at = new Vector3(-hx - random.Range(0.4f, 1.6f), Ground, hz * random.Range(0.1f, 0.9f));
                    if (reich) Kit.Crate(b, at + Vector3.UnitY * 0.3f, random.Float(), new Vector3(0.3f), boards, Iron(random), random, marked: true);
                    else Kit.Sack(b, at, random.Float() * MathF.Tau, true, 0.5f, Canvas(random));
                }
            }
        }

        // ---------------------------------------------------------------- The Reich's

        /// <summary>
        /// The Reich's gibbet: a tall post in a cairn of stones with an arm and its brace, an iron cage hung from it on a chain, empty, its door hanging open, and the Reich's notice
        /// nailed to the post under its seal. Numbers: height.
        /// </summary>
        public static void Gibbet(MeshBuilder b, in PlaceSpec spec, WorldRandom random)
        {
            float height = Math.Clamp(spec.Metres(0), 3.5f, 6f);
            VertexMaterial wood = Timber(random, 0.55f), iron = Iron(random), dark = Iron(random, 0.7f);
            Kit.Span(b, new Vector3(-0.17f, -0.5f, -0.17f), new Vector3(0.17f, height, 0.17f), 0.02f, wood);
            Kit.Plank(b, new Vector3(-0.1f, height - 0.2f, 0f), new Vector3(1.9f, height - 0.2f, 0f), Vector3.UnitY, 0.16f, 0.16f, wood);
            Kit.Plank(b, new Vector3(0.1f, height - 1.25f, 0f), new Vector3(1.05f, height - 0.28f, 0f), Vector3.UnitZ, 0.12f, 0.1f, wood);
            // Its foot in a cairn of stones.
            for (int k = 0; k < 11; k++)
            {
                float a = k * 2.4f, r = 0.32f + (k % 3) * 0.12f;
                Kit.Block(b, new Vector3(MathF.Cos(a) * r, Ground + 0.08f + (k / 6) * 0.14f, MathF.Sin(a) * r), new Vector3(random.Range(0.14f, 0.22f), random.Range(0.09f, 0.14f), random.Range(0.12f, 0.2f)), a, 0.03f, Dressed(new Vector3(0.42f, 0.41f, 0.38f), random, 0.5f));
            }
            // The cage on its chain: rings and bars in a body's shape, its door hung open.
            Vector3 hook = new Vector3(1.75f, height - 0.28f, 0f);
            float top = height - 1.4f, foot = MathF.Max(0.9f, top - 2.1f);
            Kit.Chain(b, hook, new Vector3(1.75f, top + 0.12f, 0f), 0f, 0.16f, dark);
            const int Bars = 8;
            foreach (float y in new[] { top, (top + foot) * 0.5f + 0.25f, foot + 0.15f })
            {
                float r = y > top - 0.1f ? 0.3f : 0.38f;
                for (int k = 0; k < Bars; k++)
                {
                    float a0 = k * MathF.Tau / Bars, a1 = (k + 1) * MathF.Tau / Bars;
                    b.Tube(new Vector3(1.75f + MathF.Cos(a0) * r, y, MathF.Sin(a0) * r), new Vector3(1.75f + MathF.Cos(a1) * r, y, MathF.Sin(a1) * r), 0.02f, 0.02f, 4, iron, capped: false);
                }
            }
            for (int k = 0; k < Bars; k++)
            {
                if (k == 2) continue;
                float a = k * MathF.Tau / Bars;
                Kit.Pole(b, new Vector3(1.75f + MathF.Cos(a) * 0.3f, top, MathF.Sin(a) * 0.3f), new Vector3(1.75f + MathF.Cos(a) * 0.38f, foot + 0.15f, MathF.Sin(a) * 0.38f), 0.018f, iron);
            }
            Kit.Lathe(b, new Vector3(1.75f, foot + 0.1f, 0f), Vector3.UnitY, 0.06f, _ => 0.38f, 8, 1, dark);
            // The door, swung out on its hinge.
            Vector3 hinge = new Vector3(1.75f + MathF.Cos(2.36f) * 0.36f, foot + 0.15f, MathF.Sin(2.36f) * 0.36f);
            Vector3 swung = new Vector3(0.3f, 0f, 0.95f).Normalized();
            for (int k = 0; k < 3; k++) Kit.Pole(b, hinge + swung * (0.15f * k), hinge + swung * (0.15f * k) + Vector3.UnitY * 1.6f, 0.016f, iron);
            // The notice under its seal.
            Vector3 n = new Vector3(0f, 1.7f, 0.18f);
            Kit.Face(b, n + new Vector3(-0.18f, -0.25f, 0f), n + new Vector3(0.18f, -0.25f, 0f), n + new Vector3(0.18f, 0.25f, 0f), n + new Vector3(-0.18f, 0.25f, 0f), Vector3.UnitZ, Plaster(1.02f));
            Kit.Face(b, n + new Vector3(-0.05f, -0.2f, 0.004f), n + new Vector3(0.05f, -0.2f, 0.004f), n + new Vector3(0.05f, -0.1f, 0.004f), n + new Vector3(-0.05f, -0.1f, 0.004f), Vector3.UnitZ, OxbloodPaint());
        }

        /// <summary>A barricade of stakes through a log, crossing each other, sharpened to whoever comes: the Reich's banded in iron, the land's bare. Numbers: length, whose (1 the Reich's).</summary>
        public static void Barricade(MeshBuilder b, in PlaceSpec spec, WorldRandom random)
        {
            float length = Math.Clamp(spec.Metres(0), 2f, 7f);
            bool reich = spec.Int(1) == 1;
            VertexMaterial wood = reich ? Timber(random, 0.6f) : Weathered(random), iron = reich ? Iron(random) : LandIron(random);
            float hx = length * 0.5f;
            Kit.Pole(b, new Vector3(-hx, 0.75f, 0f), new Vector3(hx, 0.75f, 0f), 0.22f, wood);
            if (reich) foreach (float x in new[] { -hx * 0.6f, 0f, hx * 0.6f }) b.Tube(new Vector3(x - 0.06f, 0.75f, 0f), new Vector3(x + 0.06f, 0.75f, 0f), 0.24f, 0.24f, 8, iron, capped: false);
            for (float x = -hx + 0.35f; x < hx; x += 0.62f)
            {
                foreach (float s in new[] { -1f, 1f })
                {
                    Vector3 dir = new Vector3(0f, 0.85f, s * 0.55f).Normalized();
                    Vector3 mid = new Vector3(x + (s > 0f ? 0f : 0.18f), 0.75f, 0f);
                    Vector3 lo = mid - dir * 1.05f, hi = mid + dir * 1.05f;
                    Kit.Pole(b, lo, hi, 0.07f, wood);
                    Kit.Lathe(b, hi, dir, 0.3f, Kit.Profile((0f, 0.07f), (1f, 0f)), 5, 1, reich ? iron : wood);
                }
            }
        }

        /// <summary>
        /// A brood's command tent: a wall tent of the Reich's charcoal canvas with oxblood along its eaves and the mark on its gable, its fly stretched out in front on two poles, a table
        /// under it with the maps of the land pinned out and a lantern, and its standard planted at the door. Numbers: length.
        /// </summary>
        public static void CommandTent(MeshBuilder b, in PlaceSpec spec, WorldRandom random, List<AssetLight> lights)
        {
            float length = Math.Clamp(spec.Metres(0), 4.5f, 9f);
            float hx = random.Range(2.2f, 2.6f), hz = length * 0.5f, wall = 1.9f, ridge = random.Range(3.5f, 3.9f);
            VertexMaterial canvas = new VertexMaterial(MaterialCatalog.Cloth.Index, new Colour(0.23f, 0.23f, 0.24f));
            VertexMaterial inside = Tint(canvas, new Vector3(0.5f)), trim = Oxblood(), wood = Timber(random, 0.6f), iron = Iron(random);
            // Its walls, closed but for the front, where the door's flaps are tied back.
            foreach (float s in new[] { -1f, 1f })
            {
                Vector3 a = new Vector3(s * hx, 0.05f, -hz), c = new Vector3(s * hx, 0.05f, hz);
                if (s > 0f) Kit.Sheet(b, c, a, a + Vector3.UnitY * wall, c + Vector3.UnitY * wall, canvas, inside);
                else Kit.Sheet(b, a, c, c + Vector3.UnitY * wall, a + Vector3.UnitY * wall, canvas, inside);
                // The roof's sides, sagging a little.
                Vector3 e0 = new Vector3(s * hx, wall, -hz), e1 = new Vector3(s * hx, wall, hz), r0 = new Vector3(0f, ridge, -hz), r1 = new Vector3(0f, ridge, hz);
                if (s > 0f) Kit.Cloth(b, r0, r1, e1, e0, 0.1f, 3, canvas, inside);
                else Kit.Cloth(b, r1, r0, e0, e1, 0.1f, 3, canvas, inside);
                // Oxblood along the eave.
                Kit.Plank(b, new Vector3(s * (hx + 0.02f), wall - 0.12f, -hz), new Vector3(s * (hx + 0.02f), wall - 0.12f, hz), new Vector3(s, 0f, 0f), 0.26f, 0.01f, trim, 0f);
                // Guys out to their pegs.
                foreach (float z in new[] { -hz * 0.7f, 0f, hz * 0.7f })
                {
                    Vector3 from = new Vector3(s * hx, wall, z), peg = new Vector3(s * (hx + 1.5f), Ground, z);
                    Kit.Rope(b, from, peg + Vector3.UnitY * 0.25f, 0.05f, 0.022f, Rope(random), 2);
                    Kit.Pole(b, peg - Vector3.UnitY * 0.2f, peg + Vector3.UnitY * 0.3f, 0.045f, wood);
                }
            }
            // The back closed, its gable and the front's.
            Kit.Sheet(b, new Vector3(hx, 0.05f, -hz), new Vector3(-hx, 0.05f, -hz), new Vector3(-hx, wall, -hz), new Vector3(hx, wall, -hz), canvas, inside);
            foreach (float z in new[] { -hz, hz }) Kit.Face(b, new Vector3(-hx, wall, z), new Vector3(hx, wall, z), new Vector3(0f, ridge, z), new Vector3(0f, 0f, MathF.Sign(z)), canvas);
            Kit.Face(b, new Vector3(-hx, wall, hz - 0.02f), new Vector3(hx, wall, hz - 0.02f), new Vector3(0f, ridge, hz - 0.02f), -Vector3.UnitZ, inside);
            Kit.Mark(b, new Vector3(0f, wall + (ridge - wall) * 0.38f, hz + 0.02f), Vector3.UnitX, Vector3.UnitY, 0.7f, 0.01f, random);
            // The door: the dark inside, and its flaps tied back.
            Kit.Face(b, new Vector3(-hx * 0.55f, 0.05f, hz - 0.04f), new Vector3(hx * 0.55f, 0.05f, hz - 0.04f), new Vector3(hx * 0.55f, wall, hz - 0.04f), new Vector3(-hx * 0.55f, wall, hz - 0.04f), Vector3.UnitZ, Dark);
            foreach (float s in new[] { -1f, 1f })
            {
                Kit.Face(b, new Vector3(s * hx, 0.05f, hz), new Vector3(s * hx * 0.55f, 0.05f, hz), new Vector3(s * hx * 0.55f, wall, hz), new Vector3(s * hx, wall, hz), Vector3.UnitZ, canvas);
                Kit.Sheet(b, new Vector3(s * hx * 0.55f, 0.1f, hz + 0.02f), new Vector3(s * hx * 0.8f, 0.2f, hz + 0.25f), new Vector3(s * hx * 0.75f, wall - 0.2f, hz + 0.15f), new Vector3(s * hx * 0.55f, wall, hz + 0.02f), canvas, inside);
            }
            // The poles, the ridge and the fly out in front on two more.
            foreach (float z in new[] { -hz, hz }) Kit.Pole(b, new Vector3(0f, -0.3f, z), new Vector3(0f, ridge + 0.3f, z), 0.07f, wood);
            float fly = 2.4f;
            foreach (float s in new[] { -1f, 1f }) Kit.Pole(b, new Vector3(s * hx * 0.9f, -0.3f, hz + fly), new Vector3(s * hx * 0.9f, wall + 0.35f, hz + fly), 0.06f, wood);
            Kit.Cloth(b, new Vector3(-hx * 0.95f, wall + 0.6f, hz), new Vector3(hx * 0.95f, wall + 0.6f, hz), new Vector3(hx * 0.95f, wall + 0.35f, hz + fly), new Vector3(-hx * 0.95f, wall + 0.35f, hz + fly), 0.14f, 3, canvas, inside);
            Kit.Plank(b, new Vector3(-hx * 0.95f, wall + 0.3f, hz + fly + 0.02f), new Vector3(hx * 0.95f, wall + 0.3f, hz + fly + 0.02f), Vector3.UnitZ, 0.22f, 0.01f, trim, 0f);
            // The table under the fly, its maps pinned out, a lantern on it and stools round it.
            Vector3 table = new Vector3(0f, 0f, hz + fly * 0.5f);
            Kit.Span(b, table + new Vector3(-1f, 0.86f, -0.45f), table + new Vector3(1f, 0.92f, 0.45f), 0.01f, wood);
            foreach (float x in new[] { -0.9f, 0.9f }) foreach (float z in new[] { -0.36f, 0.36f }) Kit.Span(b, table + new Vector3(x - 0.04f, 0f, z - 0.04f), table + new Vector3(x + 0.04f, 0.86f, z + 0.04f), 0.004f, wood);
            for (int k = 0; k < 3; k++)
            {
                Vector3 at = table + new Vector3(-0.55f + k * 0.42f, 0.925f + k * 0.002f, random.Range(-0.12f, 0.12f));
                float turn = random.Range(-0.2f, 0.2f);
                Vector3 ax = new Vector3(MathF.Cos(turn), 0f, -MathF.Sin(turn)), az = new Vector3(MathF.Sin(turn), 0f, MathF.Cos(turn));
                Kit.Face(b, at - ax * 0.3f - az * 0.22f, at + ax * 0.3f - az * 0.22f, at + ax * 0.3f + az * 0.22f, at - ax * 0.3f + az * 0.22f, Vector3.UnitY, Plaster(0.95f + k * 0.03f));
                // Lines of the land drawn on it, in ink and oxblood.
                Kit.Plank(b, at - ax * 0.2f + az * 0.05f + Vector3.UnitY * 0.002f, at + ax * 0.22f - az * 0.08f + Vector3.UnitY * 0.002f, Vector3.UnitY, 0.012f, 0.001f, Dark, 0f);
                if (k == 1) Kit.Plank(b, at - ax * 0.1f - az * 0.12f + Vector3.UnitY * 0.003f, at + ax * 0.12f + az * 0.1f + Vector3.UnitY * 0.003f, Vector3.UnitY, 0.016f, 0.001f, OxbloodPaint(), 0f);
            }
            Kit.Lantern(b, table + new Vector3(0.7f, 1.38f, 0.2f), Brass(random), lights, 0.8f);
            foreach (float x in new[] { -0.6f, 0.6f }) Kit.Lathe(b, table + new Vector3(x, 0f, 0.8f), Vector3.UnitY, 0.48f, Kit.Profile((0f, 0.16f), (0.9f, 0.16f), (1f, 0.19f)), 7, 2, wood);
            // Its standard at the door: the brood's banner, swallow-tailed, on black iron.
            Vector3 pole = new Vector3(hx + 0.7f, 0f, hz + 0.6f);
            float high = random.Range(5f, 5.8f);
            Kit.Pole(b, pole - Vector3.UnitY * 0.4f, pole + Vector3.UnitY * high, 0.07f, iron, 0.05f);
            Kit.Lathe(b, pole + Vector3.UnitY * high, Vector3.UnitY, 0.4f, Kit.Profile((0f, 0.08f), (0.4f, 0.12f), (1f, 0f)), 6, 2, Brass(random));
            Kit.Pole(b, pole + new Vector3(-0.6f, high - 0.25f, 0.05f), pole + new Vector3(0.6f, high - 0.25f, 0.05f), 0.035f, iron);
            Vector3 t0 = pole + new Vector3(-0.58f, high - 0.27f, 0.1f), t1 = pole + new Vector3(0.58f, high - 0.27f, 0.1f);
            float drop = 2.1f;
            Kit.Sheet(b, t0 - Vector3.UnitY * drop, t1 - Vector3.UnitY * drop, t1, t0, Oxblood(), Oxblood());
            Vector3 notch = (t0 + t1) * 0.5f - Vector3.UnitY * drop * 0.8f;
            Kit.Sheet(b, t0 - Vector3.UnitY * drop, notch, notch, t0 - Vector3.UnitY * (drop + 0.45f), Oxblood(), Oxblood());
            Kit.Sheet(b, notch, t1 - Vector3.UnitY * drop, t1 - Vector3.UnitY * (drop + 0.45f), notch, Oxblood(), Oxblood());
            Kit.Mark(b, (t0 + t1) * 0.5f - Vector3.UnitY * (drop * 0.45f) + Vector3.UnitZ * 0.03f, Vector3.UnitX, Vector3.UnitY, 0.85f, 0.01f, random);
        }

        /// <summary>
        /// A signpost where ways part, its arms pointing down them with the land's names cut in: where the Reich holds the road its own boards are nailed over them, new, oxblood with
        /// brass letters. Numbers: arms, whose (1 the Reich's over the land's).
        /// </summary>
        public static void Signpost(MeshBuilder b, in PlaceSpec spec, WorldRandom random)
        {
            int arms = Math.Clamp(spec.Int(0), 1, 4);
            bool reich = spec.Int(1) == 1;
            VertexMaterial wood = Weathered(random), dark = Weathered(random, 0.7f);
            Kit.Span(b, new Vector3(-0.09f, -0.4f, -0.09f), new Vector3(0.09f, 3f, 0.09f), 0.015f, wood);
            Kit.Lathe(b, new Vector3(0f, 3f, 0f), Vector3.UnitY, 0.18f, Kit.Profile((0f, 0.13f), (1f, 0f)), 4, 1, dark);
            for (int k = 0; k < arms; k++)
            {
                float a = random.Float() * MathF.Tau;
                float y = 2.6f - k * 0.38f;
                Vector3 dir = new Vector3(MathF.Cos(a), 0f, MathF.Sin(a));
                Vector3 side = Vector3.Cross(Vector3.UnitY, dir);
                float l = random.Range(0.9f, 1.3f);
                Vector3 o = new Vector3(0f, y, 0f) + dir * 0.1f;
                Kit.Box(b, o + dir * (l * 0.5f), dir, Vector3.UnitY, side, new Vector3(l * 0.5f, 0.13f, 0.03f), 0.01f, wood);
                Kit.Face(b, o + dir * (l + 0.12f), o + dir * l + Vector3.UnitY * 0.13f + side * 0.031f, o + dir * l - Vector3.UnitY * 0.13f + side * 0.031f, side, wood);
                // The name cut in, pale where the knife went.
                Kit.Plank(b, o + dir * 0.15f + side * 0.035f, o + dir * (l - 0.1f) + side * 0.035f, side, 0.06f, 0.004f, Fresh(random, 0.8f), 0f);
                if (!reich || random.Chance(0.25f)) continue;
                // The Reich's board over it, and its letters in brass.
                Kit.Box(b, o + dir * (l * 0.48f) + side * 0.05f, dir, Vector3.UnitY, side, new Vector3(l * 0.44f, 0.12f, 0.012f), 0.004f, OxbloodPaint());
                Kit.Plank(b, o + dir * 0.18f + side * 0.066f, o + dir * (l - 0.2f) + side * 0.066f, side, 0.05f, 0.004f, Brass(random), 0f);
            }
        }

        /// <summary>A trestle table to eat at, benches either side, bowls and mugs and a loaf on it, a pot, and a lantern hung over it on a pole. Numbers: length.</summary>
        public static void MessTable(MeshBuilder b, in PlaceSpec spec, WorldRandom random, List<AssetLight> lights)
        {
            float length = Math.Clamp(spec.Metres(0), 1.8f, 4f);
            float hx = length * 0.5f;
            VertexMaterial wood = Weathered(random, 0.9f), dark = Timber(random, 0.7f), pot = new VertexMaterial(MaterialCatalog.Plaster.Index, new Colour(0.42f, 0.27f, 0.17f));
            Kit.Span(b, new Vector3(-hx, 0.82f, -0.42f), new Vector3(hx, 0.88f, 0.42f), 0.01f, wood);
            foreach (float x in new[] { -hx + 0.35f, hx - 0.35f })
            {
                Kit.Plank(b, new Vector3(x, 0f, -0.38f), new Vector3(x, 0.82f, 0f), Vector3.UnitX, 0.08f, 0.06f, dark);
                Kit.Plank(b, new Vector3(x, 0f, 0.38f), new Vector3(x, 0.82f, 0f), Vector3.UnitX, 0.08f, 0.06f, dark);
            }
            foreach (float z in new[] { -0.85f, 0.85f })
            {
                Kit.Span(b, new Vector3(-hx + 0.1f, 0.44f, z - 0.16f), new Vector3(hx - 0.1f, 0.5f, z + 0.16f), 0.008f, wood);
                foreach (float x in new[] { -hx + 0.35f, hx - 0.35f }) Kit.Span(b, new Vector3(x - 0.04f, 0f, z - 0.12f), new Vector3(x + 0.04f, 0.44f, z + 0.12f), 0.004f, dark);
            }
            for (float x = -hx + 0.3f; x < hx - 0.2f; x += random.Range(0.4f, 0.6f))
            {
                foreach (float s in new[] { -1f, 1f })
                {
                    if (random.Chance(0.35f)) continue;
                    Vector3 at = new Vector3(x + random.Range(-0.05f, 0.05f), 0.88f, s * 0.24f);
                    if (random.Chance(0.5f)) Kit.Lathe(b, at, Vector3.UnitY, 0.07f, Kit.Profile((0f, 0.05f), (1f, 0.08f)), 7, 1, pot);
                    else Kit.Lathe(b, at, Vector3.UnitY, 0.13f, _ => 0.04f, 6, 1, dark);
                }
            }
            Kit.Ball(b, new Vector3(random.Range(-hx * 0.5f, hx * 0.5f), 0.93f, 0f), new Vector3(0.16f, 0.07f, 0.1f), 7, 3, new VertexMaterial(MaterialCatalog.Grain.Index, new Colour(0.56f, 0.38f, 0.2f)));
            Kit.Lathe(b, new Vector3(hx - 0.4f, 0.88f, 0.05f), Vector3.UnitY, 0.24f, Kit.Profile((0f, 0.1f), (0.4f, 0.17f), (1f, 0.14f)), 9, 2, LandIron(random));
            // The lantern over it, on a pole at its end.
            Kit.Pole(b, new Vector3(-hx - 0.25f, -0.3f, 0f), new Vector3(-hx - 0.25f, 2.4f, 0f), 0.05f, dark);
            Kit.Pole(b, new Vector3(-hx - 0.25f, 2.35f, 0f), new Vector3(-hx + 0.5f, 2.35f, 0f), 0.035f, dark);
            Kit.Lantern(b, new Vector3(-hx + 0.45f, 2.33f, 0f), LandIron(random), lights, 0.8f);
        }
    }
}
