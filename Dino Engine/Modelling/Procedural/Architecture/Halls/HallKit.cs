using Dino_Engine.Modelling.Model;
using Dino_Engine.Textures;
using Dino_Engine.Util;
using Dino_Engine.World;
using Dino_Engine.World.Content;
using OpenTK.Mathematics;

namespace Dino_Engine.Modelling.Procedural.Architecture.Halls
{
    /// <summary>
    /// What the halls are made of, as materials of <see cref="MaterialCatalog"/> with the colour each is stained, so that nothing here reads a GPU and the halls can be built on any thread.
    /// Colours are the albedo things are under a sun of about 13, deep as everything in the game is. The Haven is blossom and gold in the last light: oak, lime plaster, warm stone, wooden
    /// shingle and slate, with teal, green and saffron cloth. Never black iron, oxblood or brass, which are the Reich's alone (LORE.md).
    /// </summary>
    internal static class Hall
    {
        /// <summary>
        /// The top of a hall's floor above the levelled ground under it, which is where people stand, so that their feet are on the boards. The ground is drawn 0.15 m higher
        /// (TerrainRenderer.SurfaceOffset), so it isn't drawn in a hall's room at all (Halls.glsl, underHallFloor), and the floor stands just clear of the ground the sun's
        /// shadows are drawn from, which has no such lift.
        /// </summary>
        public const float FloorTop = 0.03f;

        /// <summary>The top of a door's sill, which crosses the wall over ground that is still drawn, and so stands above it: a step up from outside and down into the room.</summary>
        public const float SillTop = 0.21f;

        public static VertexMaterial Wood(int r, int g, int b) => new VertexMaterial(MaterialCatalog.Wood.Index, new Colour(r, g, b));
        public static VertexMaterial Stone(int r, int g, int b) => new VertexMaterial(MaterialCatalog.StoneWall.Index, new Colour(r, g, b));
        public static VertexMaterial Rough(int shade) => new VertexMaterial(MaterialCatalog.Rock.Index, new Colour(shade, shade, (int)(shade * 0.97f)));
        public static VertexMaterial Plaster(int r, int g, int b) => new VertexMaterial(MaterialCatalog.Plaster.Index, new Colour(r, g, b));
        public static VertexMaterial Brick(int r, int g, int b) => new VertexMaterial(MaterialCatalog.Brick.Index, new Colour(r, g, b));
        public static VertexMaterial Cobbles(int shade) => new VertexMaterial(MaterialCatalog.Cobble.Index, new Colour(shade, shade, (int)(shade * 0.95f)));
        public static VertexMaterial Shingle(int r, int g, int b) => new VertexMaterial(MaterialCatalog.Shingle.Index, new Colour(r, g, b));
        public static VertexMaterial Slate(int r, int g, int b) => new VertexMaterial(MaterialCatalog.Slate.Index, new Colour(r, g, b));
        public static VertexMaterial Iron(int shade = 104) => new VertexMaterial(MaterialCatalog.Iron.Index, new Colour(shade, shade, shade + 6));
        public static VertexMaterial Metal(int r, int g, int b) => new VertexMaterial(MaterialCatalog.Plate.Index, new Colour(r, g, b));
        public static VertexMaterial Wax => new VertexMaterial(MaterialCatalog.Wax.Index, new Colour(236, 222, 190));
        public static VertexMaterial Flat(int r, int g, int b) => new VertexMaterial(MaterialCatalog.Flat.Index, new Colour(r, g, b));
        public static VertexMaterial Glow(float r, float g, float b, float strength) => new VertexMaterial(MaterialCatalog.FlatGlow.Index, new Colour(r, g, b, strength));
        public static VertexMaterial Gem(float r, float g, float b, float strength) => new VertexMaterial(MaterialCatalog.Gem.Index, new Colour(r, g, b, strength));
        public static VertexMaterial Mirror(int r, int g, int b) => new VertexMaterial(MaterialCatalog.Mirror.Index, new Colour(r, g, b));

        // Cloth, leather and bone carry a colour of their own (GearMaterials.AlbedoOf), which what's drawn on them is divided by, so that the colour asked for is the one seen.
        private static readonly Vector3 ClothAlbedo = new Vector3(178f, 172f, 158f) / 255f;
        private static readonly Vector3 LeatherAlbedo = new Vector3(112f, 76f, 50f) / 255f;
        private static readonly Vector3 BoneAlbedo = new Vector3(0.78f, 0.74f, 0.66f);

        public static VertexMaterial Cloth(int r, int g, int b) => new VertexMaterial(MaterialCatalog.Cloth.Index, new Colour(new Vector3(r, g, b) / 255f / ClothAlbedo));
        public static VertexMaterial Leather(int r, int g, int b) => new VertexMaterial(MaterialCatalog.Leather.Index, new Colour(new Vector3(r, g, b) / 255f / LeatherAlbedo));
        public static VertexMaterial Bone(int r, int g, int b) => new VertexMaterial(MaterialCatalog.Bone.Index, new Colour(new Vector3(r, g, b) / 255f / BoneAlbedo));

        // The colours that recur: oak in three stains, pewter and copper, paper and ink.
        public static VertexMaterial Oak => Wood(150, 108, 72);
        public static VertexMaterial DarkOak => Wood(92, 64, 44);
        public static VertexMaterial Walnut => Wood(78, 52, 36);
        public static VertexMaterial PaleWood => Wood(184, 148, 108);
        public static VertexMaterial Pewter => Metal(132, 134, 138);
        public static VertexMaterial Copper => Metal(176, 98, 60);
        public static VertexMaterial Silver => Metal(196, 198, 204);
        public static VertexMaterial Paper => Flat(214, 200, 168);
        public static VertexMaterial Ink => Flat(40, 32, 26);
        public static VertexMaterial Ember => Glow(1f, 0.38f, 0.08f, 3.2f);

        public static readonly Vector3[] Cloths =
        {
            new Vector3(28, 96, 98),     // teal
            new Vector3(36, 86, 54),     // green
            new Vector3(186, 128, 40),   // saffron
            new Vector3(44, 58, 116),    // deep blue
            new Vector3(206, 196, 168),  // cream
            new Vector3(190, 120, 132),  // blossom
        };

        public static VertexMaterial ClothOf(Vector3 colour) => Cloth((int)colour.X, (int)colour.Y, (int)colour.Z);

        /// <summary>A flame on a wick: a candle's, or a lamp's, which burns as particles and is lit by whatever it stands in.</summary>
        public static void Flame(List<AssetLight> lights, Vector3 at, float width) => lights.Add(AssetLight.Burning(at, width));

        /// <summary>A candle standing on <paramref name="foot"/>, with a drip or two down its side, and its flame.</summary>
        public static void Candle(MeshBuilder b, Vector3 foot, float height, float radius, List<AssetLight> lights, bool lit = true)
        {
            b.Tube(foot, foot + Vector3.UnitY * height, radius, radius * 0.94f, 6, Wax);
            b.Box(foot + new Vector3(radius * 0.85f, height * 0.72f, 0f), new Vector3(radius * 0.22f, height * 0.18f, radius * 0.22f), Wax);
            if (lit) Flame(lights, foot + Vector3.UnitY * (height + 0.02f), radius * 0.9f);
        }

        /// <summary>A post turned on a lathe: beads and coves up its length, from <paramref name="foot"/>. <paramref name="profile"/> is pairs of height above the foot and radius.</summary>
        public static void Turned(MeshBuilder b, Vector3 foot, VertexMaterial material, int sides, params (float y, float r)[] profile)
        {
            for (int i = 1; i < profile.Length; i++)
            {
                (float y0, float r0) = profile[i - 1];
                (float y1, float r1) = profile[i];
                if (y1 - y0 < 1e-3f) continue;
                b.Tube(foot + Vector3.UnitY * y0, foot + Vector3.UnitY * y1, r0, r1, sides, material, capped: i == profile.Length - 1);
            }
        }

        /// <summary>A leg turned in the usual way, <paramref name="height"/> tall and <paramref name="radius"/> at its widest.</summary>
        public static void Leg(MeshBuilder b, Vector3 foot, float height, float radius, VertexMaterial material)
        {
            Turned(b, foot, material, 7,
                (0f, radius * 0.7f), (height * 0.08f, radius * 0.85f), (height * 0.12f, radius * 0.6f), (height * 0.45f, radius * 0.75f), (height * 0.55f, radius),
                (height * 0.62f, radius * 0.62f), (height * 0.86f, radius * 0.72f), (height, radius * 0.9f));
        }

        /// <summary>
        /// Books standing on a shelf from <paramref name="start"/> along <paramref name="along"/> for <paramref name="length"/> metres, spines out towards <paramref name="out_"/>, each its own
        /// height, thickness and leather, with now and then one leaning or a gap.
        /// </summary>
        public static void Books(MeshBuilder b, Vector3 start, Vector3 along, Vector3 out_, float length, float depth, float tallest, WorldRandom random)
        {
            float at = random.Range(0.02f, 0.12f);
            while (at < length - 0.1f)
            {
                float thick = random.Range(0.07f, 0.18f);
                if (at + thick > length) break;
                if (random.Chance(0.06f))
                {
                    at += random.Range(0.12f, 0.3f);
                    continue;
                }
                float height = tallest * random.Range(0.62f, 1f);
                float deep = depth * random.Range(0.8f, 1f);
                VertexMaterial cover = random.Pick(BookCovers);
                Vector3 middle = start + along * (at + thick * 0.5f) + out_ * (deep * 0.5f - depth * 0.5f + 0.02f);
                if (random.Chance(0.08f) && at > 0.4f)
                {
                    // Leaning on the one before.
                    Vector3 up = (Vector3.UnitY * 0.92f - along * 0.4f).Normalized();
                    Vector3 foot = start + along * (at + thick * 0.5f);
                    b.Beam(foot + out_ * 0.01f, foot + up * height + out_ * 0.01f, thick * 0.5f, cover);
                    at += thick + height * 0.38f;
                    continue;
                }
                AddBook(b, middle, along, out_, thick, height, deep, cover, random);
                at += thick + random.Range(0f, 0.015f);
            }
        }

        private static void AddBook(MeshBuilder b, Vector3 middle, Vector3 along, Vector3 out_, float thick, float height, float deep, VertexMaterial cover, WorldRandom random)
        {
            float yaw = MathF.Atan2(out_.X, out_.Z);
            Vector3 saved = b.Origin;
            float savedYaw = b.Yaw;
            b.Origin = b.Origin + RotateY(middle, b.Yaw);
            b.Yaw = savedYaw + yaw;
            b.Box(new Vector3(0f, height * 0.5f, 0f), new Vector3(thick * 0.5f, height * 0.5f, deep * 0.5f), cover);
            // The pages, pale, set in from the cover.
            b.Box(new Vector3(0f, height * 0.5f, -0.012f), new Vector3(thick * 0.42f, height * 0.47f, deep * 0.5f - 0.002f), Paper);
            // Bands across the spine, now and then.
            if (random.Chance(0.5f))
            {
                VertexMaterial band = random.Chance(0.5f) ? Flat(150, 120, 70) : Flat(56, 44, 34);
                b.Box(new Vector3(0f, height * 0.8f, deep * 0.5f + 0.004f), new Vector3(thick * 0.5f, 0.018f, 0.006f), band);
                b.Box(new Vector3(0f, height * 0.2f, deep * 0.5f + 0.004f), new Vector3(thick * 0.5f, 0.018f, 0.006f), band);
            }
            b.Origin = saved;
            b.Yaw = savedYaw;
        }

        public static readonly VertexMaterial[] BookCovers =
        {
            Leather(86, 46, 26), Leather(58, 32, 20), Leather(36, 54, 40), Leather(30, 44, 72), Leather(110, 78, 40), Leather(70, 30, 30),
            Leather(46, 62, 66), Leather(120, 98, 62), Leather(52, 40, 58), Leather(96, 70, 44),
        };

        /// <summary>A point turned about the vertical by a yaw, the way <see cref="MeshBuilder"/> turns its shapes.</summary>
        public static Vector3 RotateY(Vector3 p, float yaw)
        {
            float cos = MathF.Cos(yaw), sin = MathF.Sin(yaw);
            return new Vector3(p.X * cos + p.Z * sin, p.Y, -p.X * sin + p.Z * cos);
        }

        /// <summary>A direction on the ground turned by a yaw, as <see cref="RotateY"/> turns it.</summary>
        public static Vector2 RotateY(Vector2 p, float yaw)
        {
            float cos = MathF.Cos(yaw), sin = MathF.Sin(yaw);
            return new Vector2(p.X * cos + p.Y * sin, -p.X * sin + p.Y * cos);
        }

        /// <summary>Builds what <paramref name="build"/> builds about <paramref name="at"/>, turned by <paramref name="yaw"/>, in the frame the builder already has.</summary>
        public static void At(MeshBuilder b, Vector3 at, float yaw, Action build)
        {
            Vector3 savedOrigin = b.Origin;
            float savedYaw = b.Yaw;
            b.Origin = savedOrigin + RotateY(at, savedYaw);
            b.Yaw = savedYaw + yaw;
            build();
            b.Origin = savedOrigin;
            b.Yaw = savedYaw;
        }

        /// <summary>The same point in the hall's frame that a builder placed with <see cref="At"/> would put <paramref name="local"/>.</summary>
        public static Vector3 Placed(MeshBuilder b, Vector3 local) => b.Origin + RotateY(local, b.Yaw);
    }

    /// <summary>The materials of one hall, chosen together.</summary>
    internal sealed class HallKit
    {
        public VertexMaterial OuterWall;    // The face of its walls, outside.
        public VertexMaterial InnerWall;    // Inside, above the panelling.
        public VertexMaterial Plinth;       // The course it stands on.
        public VertexMaterial Dressing;     // Round its doors and windows, and its corners.
        public VertexMaterial Timber;       // Its frame, outside and in.
        public VertexMaterial Joinery;      // Window frames, doors and shutters' frames.
        public VertexMaterial DoorLeaf;
        public VertexMaterial Shutter;
        public VertexMaterial Floor;
        public VertexMaterial FloorEdge;    // What runs round the floor at the walls.
        public VertexMaterial Roof;
        public VertexMaterial Ridge;
        public VertexMaterial Soffit;       // Under the eaves.
        public VertexMaterial Panelling;    // Inside, below the plaster.
        public VertexMaterial Lead;         // The cames of leaded windows.
        public bool Framed;                 // Timber framing over plaster, outside.
        public float PlinthTop = 1.2f;      // Metres above the floor its plinth reaches, outside.
        public float RoofPitch = 0.8f;      // Rise over run.
        public float Overhang = 1.6f;
        public Vector3 Moss = new Vector3(0.82f, 0.9f, 0.74f);      // What a roof's patches of lichen and moss do to its colour.

        public static HallKit For(HallKind kind) => kind switch
        {
            HallKind.Forge => new HallKit
            {
                OuterWall = Hall.Stone(170, 160, 146),
                InnerWall = Hall.Plaster(214, 200, 176),
                Plinth = Hall.Stone(134, 128, 118),
                Dressing = Hall.Stone(196, 186, 168),
                Timber = Hall.DarkOak,
                Joinery = Hall.Wood(104, 74, 50),
                DoorLeaf = Hall.Wood(118, 82, 52),
                Shutter = Hall.Wood(58, 100, 96),
                Floor = Hall.Cobbles(150),
                FloorEdge = Hall.Stone(140, 132, 120),
                Roof = Hall.Slate(176, 172, 170),
                Ridge = Hall.Stone(120, 116, 110),
                Soffit = Hall.Wood(96, 70, 50),
                Panelling = Hall.Wood(96, 68, 46),
                Lead = Hall.Iron(78),
                Framed = false,
                PlinthTop = 1.1f,
                RoofPitch = 0.86f,
                Overhang = 1.3f,
                Moss = new Vector3(0.86f, 0.94f, 0.8f),
            },
            HallKind.GamingHouse => new HallKit
            {
                OuterWall = Hall.Plaster(226, 210, 182),
                InnerWall = Hall.Plaster(222, 204, 172),
                Plinth = Hall.Stone(150, 142, 130),
                Dressing = Hall.Stone(184, 174, 156),
                Timber = Hall.Wood(86, 58, 38),
                Joinery = Hall.Wood(96, 66, 44),
                DoorLeaf = Hall.Wood(112, 76, 48),
                Shutter = Hall.Wood(44, 92, 70),
                Floor = Hall.Wood(156, 112, 72),
                FloorEdge = Hall.Wood(88, 60, 40),
                Roof = Hall.Shingle(214, 186, 158),
                Ridge = Hall.Wood(96, 72, 54),
                Soffit = Hall.Wood(104, 74, 50),
                Panelling = Hall.Wood(92, 60, 40),
                Lead = Hall.Iron(80),
                Framed = true,
                PlinthTop = 1.3f,
                RoofPitch = 0.92f,
                Overhang = 1.8f,
                Moss = new Vector3(0.84f, 0.92f, 0.76f),
            },
            _ => new HallKit
            {
                OuterWall = Hall.Stone(192, 182, 164),
                InnerWall = Hall.Plaster(224, 212, 188),
                Plinth = Hall.Stone(148, 140, 128),
                Dressing = Hall.Stone(214, 204, 184),
                Timber = Hall.Wood(82, 56, 38),
                Joinery = Hall.Wood(92, 64, 44),
                DoorLeaf = Hall.Wood(104, 70, 44),
                Shutter = Hall.Wood(40, 70, 104),
                Floor = Hall.Wood(146, 104, 68),
                FloorEdge = Hall.Stone(150, 142, 128),
                Roof = Hall.Slate(184, 180, 182),
                Ridge = Hall.Stone(132, 128, 120),
                Soffit = Hall.Wood(98, 70, 48),
                Panelling = Hall.Wood(84, 56, 38),
                Lead = Hall.Iron(76),
                Framed = false,
                PlinthTop = 1.3f,
                RoofPitch = 0.72f,
                Overhang = 1.4f,
                Moss = new Vector3(0.86f, 0.93f, 0.82f),
            },
        };
    }
}
