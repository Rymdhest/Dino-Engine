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
    /// A nesting ground (PLACES.md), where what got out of the pens found it could breed: raptors' mounds of earth and torn branches each with its clutch laid in a ring, a ravager's one great
    /// scrape, a wader's heap of reeds and mud; what's been dragged back to feed the young, picked to the ribs; the shells of what's hatched; and a weaver's lair, its webs strung between the
    /// trunks, its egg sacs hung in them and what it caught wrapped and hung up. These are animals, and nothing here is anyone's but theirs. Headless.
    /// </summary>
    internal static class Nests
    {
        private static VertexMaterial Branch(WorldRandom random) => new VertexMaterial(MaterialCatalog.BarkFurrowed.Index, new Colour(new Vector3(0.36f, 0.3f, 0.24f) * random.Range(0.85f, 1.1f)));
        private static VertexMaterial Silk(float shade = 1f) => new VertexMaterial(MaterialCatalog.Cloth.Index, new Colour(new Vector3(0.66f, 0.66f, 0.62f) * shade));

        /// <summary>A nest: a mound heaped up round a hollow, rimmed with torn branches and lined with fern, its eggs in the hollow. Its kind is a <see cref="NestKind"/>.</summary>
        public static void Mound(MeshBuilder b, in PlaceSpec spec, WorldRandom random)
        {
            var kind = (NestKind)Math.Clamp(spec.Int(0), 0, (int)NestKind.Wader);
            int eggs = Math.Clamp(spec.Int(1), 0, 16);
            float radius = Math.Clamp(spec.Metres(2), 1f, 6f);
            bool reeds = kind == NestKind.Wader;
            VertexMaterial earth = reeds ? Soil(0.7f) : Soil(0.95f);
            VertexMaterial lining = reeds ? Straw(random, 0.7f) : new VertexMaterial(MaterialCatalog.Litter.Index, new Colour(0.42f, 0.36f, 0.2f));
            float rim = kind == NestKind.Ravager ? radius * 0.18f : radius * 0.32f;
            // The mound: a ring of earth round a hollow, lower on the ravager's, which scrapes rather than heaps.
            const int Around = 16, Rings = 4;
            float[] bumps = new float[Around];
            for (int k = 0; k < Around; k++) bumps[k] = random.Range(0.85f, 1.15f);
            Vector3 At(int ring, int k)
            {
                float t = ring / (float)Rings;
                float a = k * MathF.Tau / Around;
                float r = radius * t * bumps[k % Around];
                // Up from the hollow's floor to the rim, and down its outside to the ground.
                float y = t < 0.55f ? MathF.Sin(t / 0.55f * MathF.PI * 0.5f) * rim * 0.65f : rim * 0.65f * (1f - (t - 0.55f) / 0.45f) + rim * 0.35f * MathF.Sin((t - 0.55f) / 0.45f * MathF.PI);
                return new Vector3(MathF.Cos(a) * r, Ground - 0.08f + y, MathF.Sin(a) * r);
            }
            for (int ring = 0; ring < Rings; ring++)
            {
                for (int k = 0; k < Around; k++)
                {
                    Vector3 p0 = At(ring, k), p1 = At(ring, k + 1), p2 = At(ring + 1, k + 1), p3 = At(ring + 1, k);
                    if (ring == 0) Kit.Face(b, p0, p2, p3, Vector3.UnitY, lining);
                    else Kit.Face(b, p0, p1, p2, p3, Vector3.UnitY, ring == 1 ? lining : earth);
                }
            }
            // Torn branches round its rim, the leaves still on some, and reeds instead on a wader's.
            int sticks = (int)(radius * 7f);
            for (int k = 0; k < sticks; k++)
            {
                float a = random.Float() * MathF.Tau;
                float r = radius * random.Range(0.45f, 0.75f);
                Vector3 centre = new Vector3(MathF.Cos(a) * r, Ground + rim * 0.6f, MathF.Sin(a) * r);
                Vector3 along = new Vector3(-MathF.Sin(a), random.Range(-0.15f, 0.25f), MathF.Cos(a)).Normalized();
                float l = random.Range(0.8f, 2f) * (reeds ? 0.8f : 1f);
                if (reeds) Kit.Pole(b, centre - along * l * 0.5f, centre + along * l * 0.5f, 0.03f, Straw(random, 0.8f));
                else Kit.Pole(b, centre - along * l * 0.5f, centre + along * l * 0.5f, random.Range(0.035f, 0.07f), Branch(random), 0.02f);
            }
            // The clutch, laid in a ring in the hollow, the raptors' in pairs.
            float eggSize = kind == NestKind.Ravager ? 0.36f : kind == NestKind.Wader ? 0.28f : 0.24f;
            for (int k = 0; k < eggs; k++)
            {
                float a = k * MathF.Tau / Math.Max(eggs, 1) + (kind == NestKind.Raptors && k % 2 == 1 ? 0.18f : 0f);
                float r = radius * (kind == NestKind.Ravager ? 0.22f : 0.2f) + (kind == NestKind.Raptors ? (k % 2) * 0.12f : random.Range(-0.08f, 0.08f));
                Vector3 egg = new Vector3(MathF.Cos(a) * r, Ground - 0.02f, MathF.Sin(a) * r);
                if (random.Chance(0.12f))
                {
                    // One hatched: its shell split and fallen in two.
                    Shell(b, egg, eggSize, random);
                    continue;
                }
                Laboratory.Egg(b, egg, eggSize, random, new VertexMaterial(MaterialCatalog.Bone.Index, new Colour(EggColour(kind, random))));
            }
        }

        // What colour a kind lays: speckled earth for a raptor, a greenish blue for a ravager, mud-brown for a wader.
        private static Vector3 EggColour(NestKind kind, WorldRandom random) => kind switch
        {
            NestKind.Ravager => new Vector3(0.42f, 0.5f, 0.48f) * random.Range(0.9f, 1.05f),
            NestKind.Wader => new Vector3(0.46f, 0.4f, 0.32f) * random.Range(0.9f, 1.05f),
            _ => new Vector3(0.6f, 0.54f, 0.44f) * random.Range(0.9f, 1.05f),
        };

        // Half a shell lying on its side, and the other half beside it.
        private static void Shell(MeshBuilder b, Vector3 at, float size, WorldRandom random)
        {
            VertexMaterial shell = new VertexMaterial(MaterialCatalog.Bone.Index, new Colour(0.62f, 0.58f, 0.5f));
            for (int k = 0; k < 2; k++)
            {
                float turn = random.Float() * MathF.Tau;
                Vector3 axis = new Vector3(MathF.Cos(turn), 0.15f, MathF.Sin(turn));
                Vector3 c = at + new Vector3(random.Range(-0.3f, 0.3f), 0f, random.Range(-0.3f, 0.3f));
                Kit.Lathe(b, c, axis, size * 1.1f, Kit.Profile((0f, size * 0.8f), (0.6f, size * 0.62f), (1f, 0f)), 7, 3, shell);
            }
        }

        /// <summary>What's been dragged back to feed the young: a great beast's ribcage picked half clean, its flesh dark on the bones and the hide torn off in scraps; or a smaller one's.</summary>
        public static void Carcass(MeshBuilder b, in PlaceSpec spec, WorldRandom random)
        {
            int form = spec.Int(0);
            float length = Math.Clamp(spec.Metres(1), 2f, 10f);
            VertexMaterial bone = Bone(random), flesh = Flesh(0.85f), hide = Hide(new Vector3(0.3f, 0.28f, 0.22f));
            float thick = length * 0.035f;
            // The spine along z, a little curled.
            var spine = new List<Vector3>();
            for (int k = 0; k <= 8; k++)
            {
                float t = k / 8f;
                spine.Add(new Vector3(MathF.Sin(t * 2.4f) * length * 0.08f, Ground + thick * 0.8f, -length * 0.5f + t * length));
            }
            Kit.Bend(b, spine, thick, thick * 0.7f, bone);
            // Its ribs, arching up, the flesh still on the near ones.
            int ribs = form == 0 ? 10 : 7;
            for (int k = 0; k < ribs; k++)
            {
                float t = 0.3f + 0.45f * k / ribs;
                Vector3 root = spine[(int)(t * 8f)];
                float span = length * (form == 0 ? 0.18f : 0.13f) * MathF.Sin(MathF.PI * (0.2f + 0.8f * k / ribs));
                foreach (float side in new[] { -1f, 1f })
                {
                    if (random.Chance(0.15f)) continue;
                    var arc = new List<Vector3>();
                    for (int j = 0; j <= 5; j++)
                    {
                        float a = j / 5f * MathF.PI * 0.85f;
                        arc.Add(root + new Vector3(side * MathF.Sin(a * 0.6f) * span, MathF.Sin(a) * span * 0.9f, 0f));
                    }
                    Kit.Bend(b, arc, thick * 0.32f, thick * 0.18f, random.Chance(0.35f) ? flesh : bone);
                }
            }
            // A leg torn off and lying near, the skull further off, and scraps of hide and the dark of what's been eaten on the ground.
            Vector3 leg = new Vector3(length * 0.25f, Ground + 0.15f, length * random.Range(-0.2f, 0.2f));
            Kit.Pole(b, leg, leg + new Vector3(length * 0.2f, 0.05f, length * 0.15f), thick * 0.5f, bone, thick * 0.35f);
            Kit.Lathe(b, spine[^1] + new Vector3(0f, 0f, thick), new Vector3(0.2f, 0f, 1f), length * 0.18f, Kit.Profile((0f, thick * 1.4f), (0.5f, thick * 1.1f), (1f, thick * 0.4f)), 8, 3, bone);
            for (int k = 0; k < 3; k++)
            {
                Vector3 c = new Vector3(random.Range(-1f, 1f) * length * 0.3f, Ground + 0.02f, random.Range(-1f, 1f) * length * 0.4f);
                Kit.Heap(b, c, new Vector2(random.Range(0.6f, 1.2f), random.Range(0.5f, 0.9f)), 0.05f, random.Float() * MathF.Tau, k == 0 ? hide : Flat(new Vector3(0.12f, 0.04f, 0.03f)), random, 2, 8);
            }
        }

        /// <summary>The shells of what hatched, broken and trodden, scattered over the ground.</summary>
        public static void Shells(MeshBuilder b, in PlaceSpec spec, WorldRandom random)
        {
            int count = random.Int(6, 11);
            for (int k = 0; k < count; k++)
            {
                Vector3 at = new Vector3(random.Range(-1.8f, 1.8f), Ground, random.Range(-1.8f, 1.8f));
                Shell(b, at, random.Range(0.18f, 0.3f), random);
            }
        }

        /// <summary>
        /// A weaver's web strung between two points either side of it along x, at heights between the ground and its top: threads out from its middle and round in a spiral, silver, and on a
        /// sheet web a hammock of silk slung under it as well, sagging between the trunks, which is what the camera sees of a lair from up there, as a thread is a pixel at most.
        /// </summary>
        public static void Web(MeshBuilder b, in PlaceSpec spec, WorldRandom random)
        {
            float width = Math.Clamp(spec.Metres(0), 2f, 16f), height = Math.Clamp(spec.Metres(1), 2f, 9f);
            VertexMaterial thread = Silk(1.05f);
            if (spec.Int(2) == 1) Sheet(b, width, height, random);
            Vector3 hub = new Vector3(random.Range(-0.15f, 0.15f) * width, height * random.Range(0.45f, 0.6f), 0f);
            // Its frame: from the anchors either side, over its top and under it.
            Vector3 leftTop = new Vector3(-width * 0.5f, height, 0f), rightTop = new Vector3(width * 0.5f, height * random.Range(0.85f, 1f), 0f);
            Vector3 leftFoot = new Vector3(-width * 0.45f, height * 0.18f, 0f), rightFoot = new Vector3(width * 0.42f, 0.4f, 0f);
            Vector3[] frame = { leftTop, rightTop, rightFoot, leftFoot };
            for (int k = 0; k < 4; k++) Kit.Rope(b, frame[k], frame[(k + 1) % 4], 0.1f, 0.07f, thread, 4);
            int spokes = random.Int(11, 15);
            var ends = new Vector3[spokes];
            for (int k = 0; k < spokes; k++)
            {
                float a = k * MathF.Tau / spokes + random.Range(-0.08f, 0.08f);
                Vector3 dir = new Vector3(MathF.Cos(a), MathF.Sin(a), 0f);
                // Out to where the frame is, as far as the box of it reaches that way.
                float reach = MathF.Min(MathF.Abs(dir.X) > 1e-3f ? width * 0.45f / MathF.Abs(dir.X) : 99f, MathF.Abs(dir.Y) > 1e-3f ? (dir.Y > 0f ? height - hub.Y : hub.Y - 0.3f) / MathF.Abs(dir.Y) : 99f);
                ends[k] = hub + dir * reach;
                Kit.Rope(b, hub, ends[k], 0f, 0.05f, thread, 1);
            }
            for (float t = 0.15f; t < 0.95f; t += 0.11f)
            {
                for (int k = 0; k < spokes; k++)
                {
                    Vector3 a = Vector3.Lerp(hub, ends[k], t), c = Vector3.Lerp(hub, ends[(k + 1) % spokes], t + 0.11f / spokes);
                    Kit.Rope(b, a, c, 0f, 0.04f, thread, 1);
                }
            }
        }

        // A sheet of silk slung between the anchors either side along x, sagging to a metre or so over the ground in its middle, ragged at its edges, with guy threads up to the trunks
        // above it and down to the ground under it. Seen from both sides, so drawn twice, a hair apart.
        private static void Sheet(MeshBuilder b, float width, float height, WorldRandom random)
        {
            float depth = random.Range(2.6f, 4.2f), top = MathF.Min(height * 0.42f, 2.8f), sag = MathF.Min(top - 0.9f, width * 0.12f);
            const int Across = 10, Deep = 5;
            var lip = new float[Across + 1];
            for (int i = 0; i <= Across; i++) lip[i] = random.Range(0.75f, 1f);
            Vector3 At(int i, int j)
            {
                float u = i / (float)Across, v = j / (float)Deep - 0.5f;
                float x = (u - 0.5f) * width * 0.92f;
                // Hung from its ends and its middle line: lowest in its middle, and lower again towards its ragged front and back edges.
                float y = top - sag * MathF.Sin(MathF.PI * u) - MathF.Abs(v) * 0.5f * sag;
                float z = v * depth * (0.55f + 0.45f * MathF.Sin(MathF.PI * u)) * lip[i];
                return new Vector3(x, y, z);
            }
            VertexMaterial veil = Silk(0.82f), under = Silk(0.7f);
            for (int i = 0; i < Across; i++)
            {
                for (int j = 0; j < Deep; j++)
                {
                    Vector3 p0 = At(i, j), p1 = At(i + 1, j), p2 = At(i + 1, j + 1), p3 = At(i, j + 1);
                    Kit.Face(b, p0, p1, p2, p3, Vector3.UnitY, veil);
                    Vector3 down = -Vector3.UnitY * 0.012f;
                    Kit.Face(b, p0 + down, p1 + down, p2 + down, p3 + down, -Vector3.UnitY, under);
                }
            }
            VertexMaterial thread = Silk(1.05f);
            for (int i = 1; i < Across; i += 2)
            {
                Vector3 edge = At(i, random.Chance(0.5f) ? 0 : Deep);
                Kit.Rope(b, edge, new Vector3(edge.X * 1.05f, Ground, edge.Z * 1.4f), 0f, 0.04f, thread, 1);
                if (i % 4 == 1) Kit.Rope(b, At(i, Deep / 2), new Vector3(At(i, Deep / 2).X * 0.8f, height * random.Range(0.8f, 1f), 0f), 0f, 0.04f, thread, 1);
            }
        }

        /// <summary>Silk wound round a trunk from its roots to as high as the id says, thick and lumpy at the bottom where it's wound over and over, thinning upwards in loose bands.</summary>
        public static void Wrap(MeshBuilder b, in PlaceSpec spec, WorldRandom random)
        {
            float r = Math.Clamp(spec.Metres(0), 0.1f, 1.5f), high = Math.Clamp(spec.Metres(1), 1f, 7f);
            Kit.Lathe(b, new Vector3(0f, -0.1f, 0f), Vector3.UnitY, high * 0.45f, Kit.Profile((0f, r + 0.22f), (0.4f, r + 0.14f), (1f, r + 0.05f)), 12, 4, Silk(0.86f));
            for (float y = high * 0.45f; y < high; y += random.Range(0.3f, 0.6f))
            {
                float tilt = random.Range(-0.12f, 0.12f), thick = random.Range(0.06f, 0.14f);
                Vector3 axis = new Vector3(tilt, 1f, random.Range(-0.12f, 0.12f)).Normalized();
                Kit.Lathe(b, new Vector3(0f, y, 0f), axis, thick, Kit.Profile((0f, r + 0.04f), (0.5f, r + 0.07f), (1f, r + 0.04f)), 12, 2, Silk(0.92f));
            }
        }

        /// <summary>
        /// A weaver's floor: a sheet of silk spun over the ground among the grass and the roots out to the radius the id says, ragged where its guy threads pull its edge down to the earth,
        /// and on its lair's (form 1) its retreat in the middle of it, a collar of silk standing up round the dark mouth of the burrow it lives down. What it caught lies on it, wrapped. A
        /// thread is a pixel from up there at most, and a web strung between trunks is seen edge on as often as not, so this is what the camera sees of a lair: pale silk spread over dark
        /// trodden earth, and a hole in the middle of it nobody would put their arm down.
        /// </summary>
        public static void SilkFloor(MeshBuilder b, in PlaceSpec spec, WorldRandom random)
        {
            float radius = Math.Clamp(spec.Metres(0), 1.2f, 7f);
            bool burrow = spec.Int(1) == 1;
            const int Around = 20, Rings = 5;
            var edge = new float[Around];
            for (int k = 0; k < Around; k++) edge[k] = radius * random.Range(0.7f, 1.05f);
            var lift = new float[Around, Rings + 1];
            for (int k = 0; k < Around; k++)
            {
                for (int i = 0; i <= Rings; i++) lift[k, i] = random.Range(0f, 0.1f);
            }
            float mouth = burrow ? MathF.Min(1f, radius * 0.3f) : 0f;
            float rise = burrow ? 0.42f : 0.2f;
            // Highest at its middle, held up by the grass and its own threads, and down to the tops of the grass at its edge, where it's pulled down to the earth: the ground under a
            // piece is never as flat as the piece, and a sheet laid on it would be under it as often as on it.
            float Height(float t) => Ground + 0.12f + rise * (1f - t) * (1f - t);
            Vector3 At(int i, int k)
            {
                k %= Around;
                if (!burrow && i == 0) return new Vector3(0f, Height(0f) + 0.05f, 0f);
                float t = i / (float)Rings;
                float a = k * MathF.Tau / Around;
                float r = mouth + (edge[k] - mouth) * t;
                return new Vector3(MathF.Cos(a) * r, Height(t) + lift[k, i] * (1f - t), MathF.Sin(a) * r);
            }
            VertexMaterial veil = Silk(0.94f);
            for (int i = 0; i < Rings; i++)
            {
                for (int k = 0; k < Around; k++)
                {
                    if (!burrow && i == 0) Kit.Face(b, At(0, k), At(1, k), At(1, k + 1), Vector3.UnitY, veil);
                    else Kit.Face(b, At(i, k), At(i + 1, k), At(i + 1, k + 1), At(i, k + 1), Vector3.UnitY, veil);
                }
            }
            VertexMaterial thread = Silk(1.05f);
            // Its guy threads, from its edge out and down to the earth, and the tangle over it that knocks down what flies into it.
            for (int k = 0; k < Around; k += 2)
            {
                Vector3 from = At(Rings, k);
                Vector3 to = new Vector3(from.X * random.Range(1.2f, 1.45f), Ground - 0.05f, from.Z * random.Range(1.2f, 1.45f));
                Kit.Rope(b, from, to, 0f, 0.03f, thread, 1);
            }
            for (int n = 0; n < 6; n++)
            {
                Vector3 low = At(random.Int(1, Rings - 1), random.Int(Around));
                Vector3 high = low * 0.6f + new Vector3(random.Range(-0.6f, 0.6f), 0f, random.Range(-0.6f, 0.6f));
                high.Y = random.Range(0.9f, 1.6f);
                Kit.Rope(b, low, high, 0f, 0.025f, thread, 1);
            }
            if (burrow)
            {
                // The retreat: a collar of silk thickened by being spun over and over, standing round the burrow's mouth and going down into it, and the dark of the burrow.
                Vector3 Throat(int k)
                {
                    float a = (k % Around) * MathF.Tau / Around;
                    return new Vector3(MathF.Cos(a) * mouth * 0.45f, Ground + 0.1f, MathF.Sin(a) * mouth * 0.45f);
                }
                VertexMaterial collar = Silk(0.8f);
                for (int k = 0; k < Around; k++)
                {
                    Vector3 middle = (At(0, k) + At(0, k + 1)) * 0.5f;
                    Vector3 inward = new Vector3(-middle.X, 0f, -middle.Z).Normalized() * 0.7f + Vector3.UnitY * 0.3f;
                    Kit.Face(b, At(0, k), At(0, k + 1), Throat(k + 1), Throat(k), inward, collar);
                    Kit.Face(b, Vector3.UnitY * (Ground + 0.1f), Throat(k), Throat(k + 1), Vector3.UnitY, Dark);
                }
            }
            // What it caught, wrapped where it was brought down: on its lair's, something the size of a beast as well.
            int bundles = burrow ? random.Int(2, 3) : random.Int(0, 2);
            for (int n = 0; n < bundles; n++)
            {
                float length = burrow && n == 0 ? random.Range(1.6f, 2.4f) : random.Range(0.5f, 1.1f);
                float a = random.Float() * MathF.Tau, d = random.Range(mouth + 0.8f, MathF.Max(mouth + 1f, radius * 0.7f));
                float girth = length * 0.2f;
                Vector3 along = new Vector3(MathF.Cos(a + 1.4f), random.Range(-0.05f, 0.05f), MathF.Sin(a + 1.4f)).Normalized();
                float t = Math.Clamp((d - mouth) / MathF.Max(radius - mouth, 0.1f), 0f, 1f);
                Vector3 centre = new Vector3(MathF.Cos(a) * d, Height(t) + girth * 0.7f, MathF.Sin(a) * d);
                Vector3 foot = centre - along * length * 0.5f;
                Kit.Lathe(b, foot, along, length, Kit.Profile((0f, 0.04f), (0.15f, girth * 0.75f), (0.45f, girth), (0.8f, girth * 0.75f), (1f, 0.04f)), 9, 6, Silk(0.86f));
                for (float s = 0.2f; s < 0.85f; s += 0.16f)
                {
                    float r = girth * MathF.Sin(MathF.PI * s) + 0.02f;
                    Vector3 p = foot + along * length * s;
                    b.Tube(p - along * 0.02f, p + along * 0.02f, r, r, 9, Silk(1.04f), capped: false);
                }
            }
        }

        /// <summary>A weaver's egg sac: a pale silk ball hung on a thread from <paramref name="spec"/>'s size up, a little lumpy with what's in it.</summary>
        public static void EggSac(MeshBuilder b, in PlaceSpec spec, WorldRandom random)
        {
            float size = Math.Clamp(spec.Metres(0), 0.4f, 2f);
            float hang = random.Range(2.2f, 4f);
            Kit.Pole(b, new Vector3(0f, hang + size, 0f), new Vector3(0f, hang + size + 3f, 0f), 0.03f, Silk());
            Kit.Lathe(b, new Vector3(0f, hang - size, 0f), Vector3.UnitY, size * 2f, Kit.Profile((0f, 0f), (0.2f, size * 0.85f), (0.5f, size), (0.8f, size * 0.8f), (1f, size * 0.15f)), 10, 6, Silk(0.95f));
            for (int k = 0; k < 5; k++)
            {
                float a = random.Float() * MathF.Tau, y = random.Range(-0.5f, 0.5f);
                Vector3 bump = new Vector3(MathF.Cos(a) * size * 0.85f, hang + y * size, MathF.Sin(a) * size * 0.85f);
                Kit.Lathe(b, bump, new Vector3(MathF.Cos(a), 0f, MathF.Sin(a)), size * 0.3f, Kit.Profile((0f, size * 0.3f), (1f, 0f)), 6, 1, Silk(0.9f));
            }
        }

        /// <summary>Something a weaver caught, wound in silk and hung up by a thread, the shape of what it was still plain.</summary>
        public static void Cocoon(MeshBuilder b, in PlaceSpec spec, WorldRandom random)
        {
            float length = Math.Clamp(spec.Metres(0), 1f, 5f);
            float top = random.Range(4.5f, 6.5f);
            Kit.Pole(b, new Vector3(0f, top, 0f), new Vector3(0f, top + 3f, 0f), 0.03f, Silk());
            Kit.Lathe(b, new Vector3(0f, top - length, 0f), Vector3.UnitY, length, Kit.Profile((0f, 0.05f), (0.12f, length * 0.14f), (0.4f, length * 0.2f), (0.75f, length * 0.16f), (0.9f, length * 0.1f), (1f, 0.04f)), 9, 7, Silk(0.85f));
            // Threads of it wound round, standing a little proud.
            for (float y = 0.15f; y < 0.9f; y += 0.12f)
            {
                float r = length * 0.19f * MathF.Sin(MathF.PI * y);
                b.Tube(new Vector3(0f, top - length + y * length - 0.02f, 0f), new Vector3(0f, top - length + y * length + 0.02f, 0f), r + 0.02f, r + 0.02f, 9, Silk(1.05f), capped: false);
            }
        }
    }
}
