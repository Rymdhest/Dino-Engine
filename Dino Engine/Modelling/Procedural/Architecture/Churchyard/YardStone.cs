using Dino_Engine.Modelling.Model;
using Dino_Engine.Modelling.Procedural.Architecture.Waygates;
using Dino_Engine.Modelling.Procedural.Fence;
using Dino_Engine.Textures;
using Dino_Engine.Util;
using Dino_Engine.World;
using Dino_Engine.World.Content;
using OpenTK.Mathematics;

namespace Dino_Engine.Modelling.Procedural.Architecture.Churchyard
{
    /// <summary>
    /// What a churchyard is built of. The church, its gates and its cross are the wall's stone (<see cref="StoneFences.ChurchyardStone"/>), dressed, darker and greener towards the
    /// ground where the wet has had longest, and green on whatever faces the sky. Gravestones are each their own stone, as they were cut over centuries: limestone and sandstone in the
    /// oldest rows, slate and granite in the middle, marble in the newest. Iron here is weathered brown, never black, as black iron, oxblood and brass are the Reich's alone (LORE.md).
    /// Colours are albedos under a sun of about 13. Model y = 0 is where people stand, and what lies on the ground starts at <see cref="Ground"/>.
    /// </summary>
    internal static class YardStone
    {
        public const float Chamfer = 0.04f;
        public const float BigChamfer = 0.06f;

        /// <summary>Metres the ground is drawn above where people stand (TerrainRenderer.SurfaceOffset): what lies on the ground starts there.</summary>
        public const float Ground = 0.15f;

        private static readonly Vector3 Moss = new Vector3(0.74f, 0.9f, 0.56f);
        private static readonly Vector3 Damp = new Vector3(0.84f, 0.9f, 0.8f);

        /// <summary>The churchyard's own stone, from the shade and warmth (per mille) an id carries.</summary>
        public static Vector3 Colour(in YardSpec spec, int shade, int warmth) => StoneFences.DressedColour(spec.Mille(shade), spec.Mille(warmth));

        /// <summary>How much darker stone is at a height: the wet rises into a wall's foot and greens it.</summary>
        public static Vector3 Weather(float y)
        {
            float t = Math.Clamp(y / 2.6f, 0f, 1f);
            return Vector3.Lerp(Damp * 0.78f, Vector3.One, t * t * (3f - 2f * t));
        }

        /// <summary>A dressed stone of <paramref name="colour"/> at height <paramref name="y"/>, its own shade drawn from <paramref name="random"/>.</summary>
        public static VertexMaterial Dressed(Vector3 colour, WorldRandom random, float y, float spread = 0.05f)
        {
            float warm = random.Range(-0.02f, 0.02f);
            Vector3 c = colour * Weather(y) * (1f + random.Range(-spread, spread));
            return new VertexMaterial(MaterialCatalog.Rock.Index, new Colour(c.X * (1f + warm), c.Y, c.Z * (1f - warm)));
        }

        /// <summary>A dressed stone whose top has weathered green.</summary>
        public static Func<Vector3, VertexMaterial> Facing(Vector3 colour, WorldRandom random, float y, float moss = 0.35f, float spread = 0.05f)
        {
            VertexMaterial face = Dressed(colour, random, y, spread);
            VertexMaterial top = Tint(face, Vector3.Lerp(Vector3.One, Moss, moss));
            return normal => normal.Y > 0.6f ? top : face;
        }

        /// <summary>A wall laid in courses of the stone, in one face, for walls too big to lay a block at a time.</summary>
        public static VertexMaterial Coursed(Vector3 colour, float shade = 1f) => new VertexMaterial(MaterialCatalog.StoneWall.Index, new Colour(colour * shade));

        /// <summary>
        /// A roof of slates gone silver with age and lichen, from blue-grey to green-grey: pale, as a roof is most of what the camera sees of a church, and a dark one is a hole in
        /// the night, where moonlight finds a pale one.
        /// </summary>
        public static VertexMaterial Slate(WorldRandom random, float shade = 1f)
        {
            Vector3 c = Vector3.Lerp(new Vector3(0.46f, 0.48f, 0.54f), new Vector3(0.5f, 0.52f, 0.46f), random.Float()) * shade * random.Range(0.94f, 1.05f);
            return new VertexMaterial(MaterialCatalog.Slate.Index, new Colour(c));
        }

        /// <summary>Lead, along a ridge, a flashing or a spire, gone a pale grey.</summary>
        public static VertexMaterial Lead => new VertexMaterial(MaterialCatalog.Plate.Index, new Colour(0.36f, 0.37f, 0.38f));

        /// <summary>Oak, gone dark and silver with weather.</summary>
        public static VertexMaterial Oak(WorldRandom random, float shade = 1f) => new VertexMaterial(MaterialCatalog.Wood.Index, new Colour(new Vector3(0.3f, 0.23f, 0.17f) * shade * random.Range(0.9f, 1.08f)));

        public static VertexMaterial Silvered(WorldRandom random) => new VertexMaterial(MaterialCatalog.Wood.Index, new Colour(new Vector3(0.42f, 0.4f, 0.37f) * random.Range(0.88f, 1.06f)));

        /// <summary>Wrought iron left out a century: rust-brown, never the Reich's black.</summary>
        public static VertexMaterial Rust(WorldRandom random, float shade = 1f) => new VertexMaterial(MaterialCatalog.Iron.Index, new Colour(Vector3.Lerp(new Vector3(0.2f, 0.12f, 0.08f), new Vector3(0.26f, 0.17f, 0.11f), random.Float()) * shade));

        /// <summary>The dark inside a doorway, a vault or a pit.</summary>
        public static VertexMaterial Dark => new VertexMaterial(MaterialCatalog.Flat.Index, new Colour(0.018f, 0.017f, 0.016f));

        public static VertexMaterial Soil(float shade = 1f) => new VertexMaterial(MaterialCatalog.Soil.Index, new Colour(new Vector3(0.24f, 0.18f, 0.13f) * shade));

        /// <summary>Grass grown over a mound, the colour a churchyard's turf is (TerrainSurface's sward).</summary>
        public static VertexMaterial Turf(float shade = 1f) => new VertexMaterial(MaterialCatalog.GrassySoil.Index, new Colour(new Vector3(0.24f, 0.29f, 0.14f) * shade));

        /// <summary>Pale stone chippings, as a kept grave is filled with.</summary>
        public static VertexMaterial Chippings(float shade = 1f) => new VertexMaterial(MaterialCatalog.Gravel.Index, new Colour(new Vector3(0.72f, 0.71f, 0.68f) * shade));

        public static VertexMaterial Glow(float r, float g, float b, float strength) => new VertexMaterial(MaterialCatalog.FlatGlow.Index, new Colour(r, g, b, strength));

        public static VertexMaterial Wax => new VertexMaterial(MaterialCatalog.Wax.Index, new Colour(0.82f, 0.78f, 0.68f));

        public static VertexMaterial Bone(WorldRandom random) => new VertexMaterial(MaterialCatalog.Bone.Index, new Colour(new Vector3(0.56f, 0.52f, 0.44f) * random.Range(0.85f, 1.05f)));

        public static VertexMaterial Tint(VertexMaterial material, Vector3 by)
        {
            Colour c = material.colour;
            return new VertexMaterial(material.materialTextureIndex, new Colour(c.Red * by.X, c.Green * by.Y, c.Blue * by.Z, c.Intensity, c.Alpha));
        }

        /// <summary>The light a lantern on a path throws: warm, and only as far as the pool it makes, as a churchyard has a dozen in view and a light's cost is every pixel it reaches.</summary>
        public static AssetLight PathLantern(Vector3 at) => AssetLight.Lantern(at) with { Intensity = 80f, Radius = 11.5f, Flicker = 0.14f, FlickerSpeed = 6f };

        /// <summary>Candles on a grave: a small pool that lights the stone they're left at and little else, as a churchyard has a score of them in view.</summary>
        public static AssetLight GraveCandles(Vector3 at) => AssetLight.Candles(at) with { Intensity = 16f, Radius = 7.5f };

        // ---------------------------------------------------------------- Gravestones

        /// <summary>What a gravestone is cut from. A row is mostly one, as a parish bought from one quarry at a time.</summary>
        public enum Quarry { Limestone, Sandstone, Slate, Granite, Marble }

        /// <summary>The quarry a stone of an age is likeliest cut from: the oldest soft and local, the newest marble and granite brought in.</summary>
        public static Quarry QuarryFor(float age, WorldRandom random)
        {
            float roll = random.Float();
            if (age > 0.7f) return roll < 0.6f ? Quarry.Limestone : Quarry.Sandstone;
            if (age > 0.35f) return roll < 0.35f ? Quarry.Slate : roll < 0.65f ? Quarry.Sandstone : roll < 0.85f ? Quarry.Granite : Quarry.Limestone;
            return roll < 0.45f ? Quarry.Marble : roll < 0.8f ? Quarry.Granite : Quarry.Slate;
        }

        /// <summary>The colour of a quarry's stone, new.</summary>
        // Pale, as a churchyard is seen by moonlight and a lantern's pool, and its stones have to stand out of the dark turf: even slate is lighter than the grass.
        public static Vector3 ColourOf(Quarry quarry, WorldRandom random) => quarry switch
        {
            Quarry.Limestone => new Vector3(0.7f, 0.68f, 0.62f) * random.Range(0.92f, 1.04f),
            Quarry.Sandstone => Vector3.Lerp(new Vector3(0.66f, 0.56f, 0.43f), new Vector3(0.6f, 0.52f, 0.45f), random.Float()) * random.Range(0.92f, 1.04f),
            Quarry.Slate => new Vector3(0.3f, 0.32f, 0.35f) * random.Range(0.92f, 1.08f),
            Quarry.Granite => new Vector3(0.52f, 0.52f, 0.53f) * random.Range(0.9f, 1.05f),
            _ => new Vector3(0.78f, 0.78f, 0.76f) * random.Range(0.95f, 1.02f),
        };

        /// <summary>
        /// A gravestone's stone at height <paramref name="y"/> on it: its quarry's colour, darkened and greened by <paramref name="age"/>, and patched with lichen (pale grey-green or
        /// yellow) on faces that look up, more the older it is. A slate or marble face stays cleaner than a soft stone's, as it always has.
        /// </summary>
        public static Func<Vector3, VertexMaterial> Weathered(Vector3 colour, Quarry quarry, float age, WorldRandom random)
        {
            float soft = quarry is Quarry.Limestone or Quarry.Sandstone ? 1f : 0.45f;
            Vector3 aged = colour * Vector3.Lerp(Vector3.One, new Vector3(0.84f, 0.87f, 0.8f), age * soft);
            Vector3 lichen = random.Chance(0.5f) ? new Vector3(0.95f, 1.02f, 0.86f) : new Vector3(1.05f, 1.0f, 0.72f);
            Vector3 top = aged * Vector3.Lerp(Vector3.One, Vector3.Lerp(lichen, Moss, age), MathF.Min(age * soft * 1.2f, 0.9f));
            var face = new VertexMaterial(MaterialCatalog.Rock.Index, new Colour(aged));
            var up = new VertexMaterial(MaterialCatalog.Rock.Index, new Colour(top));
            return normal => normal.Y > 0.55f ? up : face;
        }

        // ---------------------------------------------------------------- Blocks

        /// <summary>A block from its middle and half its size, turned by <paramref name="yaw"/> about y, its arrises taken off.</summary>
        public static void Block(MeshBuilder b, Vector3 centre, Vector3 half, float yaw, float chamfer, Func<Vector3, VertexMaterial> facing)
        {
            float c = MathF.Cos(yaw), s = MathF.Sin(yaw);
            Vector3 Corner(float x, float y, float z) => centre + new Vector3(x * c + z * s, y, -x * s + z * c);
            Masonry.Block(b, new[]
            {
                Corner(-half.X, -half.Y, half.Z), Corner(half.X, -half.Y, half.Z), Corner(half.X, half.Y, half.Z), Corner(-half.X, half.Y, half.Z),
                Corner(-half.X, -half.Y, -half.Z), Corner(half.X, -half.Y, -half.Z), Corner(half.X, half.Y, -half.Z), Corner(-half.X, half.Y, -half.Z),
            }, chamfer, facing);
        }

        public static void Block(MeshBuilder b, Vector3 centre, Vector3 half, float yaw, float chamfer, VertexMaterial material) => Block(b, centre, half, yaw, chamfer, _ => material);

        /// <summary>A block square to the axes, from its least corner to its most.</summary>
        public static void Box(MeshBuilder b, Vector3 min, Vector3 max, float chamfer, Func<Vector3, VertexMaterial> facing) => Block(b, (min + max) * 0.5f, (max - min) * 0.5f, 0f, chamfer, facing);

        /// <summary>
        /// A block of any eight corners making a convex solid, the front four and then the back four behind them in the same order, whichever way round they were given: its winding is
        /// put right before it's laid, so a mirrored set of corners doesn't come out inside out.
        /// </summary>
        public static void Solid(MeshBuilder b, Vector3[] c, float chamfer, Func<Vector3, VertexMaterial> facing)
        {
            Vector3 middle = Vector3.Zero;
            for (int i = 0; i < 8; i++) middle += c[i];
            middle /= 8f;
            Vector3 front = (c[0] + c[1] + c[2] + c[3]) * 0.25f;
            Vector3 normal = Vector3.Cross(c[2] - c[0], c[3] - c[1]);
            if (Vector3.Dot(normal, front - middle) < 0f)
            {
                c = new[] { c[1], c[0], c[3], c[2], c[5], c[4], c[7], c[6] };
            }
            Masonry.Block(b, c, chamfer, facing);
        }

        public static void Solid(MeshBuilder b, Vector3[] c, float chamfer, VertexMaterial material) => Solid(b, c, chamfer, _ => material);

        /// <summary>A quad wound to face <paramref name="want"/>, whichever way round its corners were given.</summary>
        public static void Face(MeshBuilder b, Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3, Vector3 want, VertexMaterial m)
        {
            if (Vector3.Dot(Vector3.Cross(p2 - p0, p3 - p1), want) < 0f) b.Quad(p0, p3, p2, p1, m);
            else b.Quad(p0, p1, p2, p3, m);
        }

        /// <summary>A triangle wound to face <paramref name="want"/>.</summary>
        public static void Face(MeshBuilder b, Vector3 p0, Vector3 p1, Vector3 p2, Vector3 want, VertexMaterial m)
        {
            if (Vector3.Dot(Vector3.Cross(p1 - p0, p2 - p0), want) < 0f) b.Triangle(p0, p2, p1, m);
            else b.Triangle(p0, p1, p2, m);
        }
    }
}
