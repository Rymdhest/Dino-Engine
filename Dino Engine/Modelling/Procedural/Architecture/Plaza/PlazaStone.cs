using Dino_Engine.Modelling.Model;
using Dino_Engine.Modelling.Procedural.Architecture.Waygates;
using Dino_Engine.Textures;
using Dino_Engine.Util;
using Dino_Engine.World;
using Dino_Engine.World.Content;
using OpenTK.Mathematics;

namespace Dino_Engine.Modelling.Procedural.Architecture.Plaza
{
    /// <summary>
    /// What a square is built of. Its stone is the great waygate's own (<see cref="WaygateDesign.GreatStone"/>), dressed for steps, copes and piers and left as it came out of the ground
    /// for the walls of beds, as a square is built round its gate and out of what it's built of. Darker towards the ground, where the weather has had longest
    /// (<see cref="WaygateDesign.WeatherShade"/>), and greener on whatever faces the sky, where moss takes. Everything else is the timber, canvas, clay and soil of a town. Colours are
    /// albedos under a sun of about 13, and nothing here is black iron, oxblood or brass, which are the Reich's alone (LORE.md).
    /// </summary>
    internal static class PlazaStone
    {
        public const float Chamfer = 0.045f;
        public const float BigChamfer = 0.06f;

        /// <summary>Metres the ground is drawn above where people stand (TerrainRenderer.SurfaceOffset): what lies on the ground starts there.</summary>
        public const float Ground = 0.15f;

        private static readonly Vector3 Moss = new Vector3(0.84f, 0.95f, 0.72f);

        /// <summary>A dressed stone at height <paramref name="y"/>, its own shade drawn from <paramref name="random"/>.</summary>
        public static VertexMaterial Dressed(WorldRandom random, float y, float spread = 0.045f)
        {
            float shade = (1f + random.Range(-spread, spread)) * WaygateDesign.WeatherShade(y);
            float warm = random.Range(-0.025f, 0.025f);
            Vector3 c = WaygateDesign.GreatStone * shade;
            return new VertexMaterial(MaterialCatalog.Rock.Index, new Colour(c.X * (1f + warm), c.Y, c.Z * (1f - warm)));
        }

        /// <summary>A dressed stone whose top has weathered: what faces the sky goes a little green.</summary>
        public static Func<Vector3, VertexMaterial> DressedFacing(WorldRandom random, float y, float moss = 0.35f, float spread = 0.045f)
        {
            VertexMaterial face = Dressed(random, y, spread);
            VertexMaterial top = Tint(face, Vector3.Lerp(Vector3.One, Moss, moss));
            return normal => normal.Y > 0.6f ? top : face;
        }

        /// <summary>
        /// A field stone at height <paramref name="y"/>: the same rock, but each stone as it came, some warmer, some greyer, a few darker, and its top mossed over more the
        /// more it faces the sky.
        /// </summary>
        public static Func<Vector3, VertexMaterial> Rubble(WorldRandom random, float y, float moss)
        {
            float shade = random.Range(0.8f, 1.06f) * WaygateDesign.WeatherShade(y + 0.4f);
            if (random.Chance(0.12f)) shade *= random.Range(0.78f, 0.9f);
            float warm = random.Range(-0.045f, 0.05f);
            float grey = random.Range(0f, 0.35f);
            Vector3 c = Vector3.Lerp(WaygateDesign.GreatStone, new Vector3(1.12f, 1.13f, 1.1f), grey) * shade;
            var face = new VertexMaterial(MaterialCatalog.Rock.Index, new Colour(c.X * (1f + warm), c.Y, c.Z * (1f - warm)));
            float mossed = moss * random.Range(0.5f, 1.2f);
            VertexMaterial top = Tint(face, Vector3.Lerp(Vector3.One, Moss, Math.Clamp(mossed, 0f, 1f)));
            return normal => normal.Y > 0.55f ? top : face;
        }

        /// <summary>The dark between stones, which shows in every joint.</summary>
        public static VertexMaterial Hearting => new VertexMaterial(MaterialCatalog.Rock.Index, new Colour(WaygateDesign.GreatStone * 0.32f));

        /// <summary>The edge of a tended bed: moss and short grass over its soil, the colour of a bed's grass (TerrainSurface's garden).</summary>
        public static VertexMaterial BedEdge => new VertexMaterial(MaterialCatalog.Moss.Index, new Colour(0.2f, 0.31f, 0.1f));

        public static VertexMaterial Soil(float shade = 1f) => new VertexMaterial(MaterialCatalog.Soil.Index, new Colour(new Vector3(0.3f, 0.22f, 0.15f) * shade));

        /// <summary>Oak, weathered silver-brown where it's out in the sun.</summary>
        public static VertexMaterial Timber(WorldRandom random, float shade = 1f)
        {
            float s = shade * random.Range(0.9f, 1.08f);
            return new VertexMaterial(MaterialCatalog.Wood.Index, new Colour(new Vector3(0.52f, 0.4f, 0.29f) * s));
        }

        public static VertexMaterial DarkTimber(WorldRandom random) => new VertexMaterial(MaterialCatalog.Wood.Index, new Colour(new Vector3(0.34f, 0.25f, 0.18f) * random.Range(0.92f, 1.06f)));

        public static VertexMaterial Cloth(Vector3 colour) => new VertexMaterial(MaterialCatalog.Cloth.Index, new Colour(colour));

        /// <summary>Fired clay, from pale to dark as kilns go.</summary>
        public static VertexMaterial Terracotta(WorldRandom random)
        {
            float s = random.Range(0.86f, 1.08f);
            return new VertexMaterial(MaterialCatalog.Plaster.Index, new Colour(new Vector3(0.66f, 0.36f, 0.22f) * s));
        }

        public static VertexMaterial Glaze(Vector3 colour) => new VertexMaterial(MaterialCatalog.Flat.Index, new Colour(colour));

        public static VertexMaterial Glow(float r, float g, float b, float strength) => new VertexMaterial(MaterialCatalog.FlatGlow.Index, new Colour(r, g, b, strength));

        public static VertexMaterial Tint(VertexMaterial material, Vector3 by)
        {
            Colour c = material.colour;
            return new VertexMaterial(material.materialTextureIndex, new Colour(c.Red * by.X, c.Green * by.Y, c.Blue * by.Z, c.Intensity, c.Alpha));
        }

        /// <summary>
        /// A lantern hung in a square: a street lantern's warmth, but reaching only as far as the pool it throws, as a square has several in view and a light's cost is
        /// every pixel it reaches (SQUARE.md).
        /// </summary>
        public static AssetLight HungLantern(Vector3 at) => AssetLight.Lantern(at) with { Intensity = 130f, Radius = 17f };

        /// <summary>A block square to the axes but for its own small turn, from its middle and half its size, its arrises taken off.</summary>
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
    }
}
