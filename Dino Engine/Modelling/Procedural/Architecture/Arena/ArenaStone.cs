using Dino_Engine.Modelling.Model;
using Dino_Engine.Modelling.Procedural.Architecture.Waygates;
using Dino_Engine.Textures;
using Dino_Engine.Util;
using Dino_Engine.World;
using Dino_Engine.World.Content;
using OpenTK.Mathematics;

namespace Dino_Engine.Modelling.Procedural.Architecture.Arena
{
    /// <summary>The stone an arena is built of: what the land it's dug into gives, or what the Reich brought.</summary>
    public enum ArenaStoneKind
    {
        Sandstone,      // Warm ochre, in thick beds: the Pit, dug out of its own hill.
        Limestone,      // Pale and cream: the Colosseum's seats and podium.
        Granite,        // Cool grey: the Jurassic Cage's footings and the Institute's walls.
        Basalt,         // Near black: the Onslaught's quarry and the Ossuary's work.
        TempleGold,     // The Sun-Temple's own stone, golden, older than anyone remembers.
        DarkGranite,    // The Cathedral's, dark and close-grained, as the Reich builds what it means to last.
    }

    /// <summary>
    /// What the Reich builds its arenas of: dressed stone (on <see cref="MaterialCatalog.Rock"/>, its colour in the vertices, as the Haven's is), darker towards the foot where blood, sand and weather have had
    /// longest; and its own three colours, which nothing else in the game wears: black iron, oxblood and brass (LORE.md, the same values the war-beasts' harness is in). Colours are albedos under a sun of about
    /// 13, multiplied with a material's own pale colour. Headless.
    /// </summary>
    internal static class ArenaStone
    {
        public const float Chamfer = 0.045f;
        public const float BigChamfer = 0.065f;

        /// <summary>Metres the ground is drawn above where people stand: what lies on the ground starts there.</summary>
        public const float Ground = 0.15f;

        // The Reich's own (SaurianHarness), a little lifted for cloth that's seen whole and in the sun.
        public static readonly Vector3 IronColour = new Vector3(0.075f, 0.075f, 0.085f);
        public static readonly Vector3 OxbloodColour = new Vector3(0.36f, 0.035f, 0.035f);
        public static readonly Vector3 BrassColour = new Vector3(0.5f, 0.36f, 0.13f);

        /// <summary>Each stone's colour, as a multiplier of the rock material's own, as the waygates' stone is (<see cref="WaygateDesign.GreatStone"/>).</summary>
        public static Vector3 ColourOf(ArenaStoneKind kind) => kind switch
        {
            ArenaStoneKind.Sandstone => new Vector3(1.3f, 1.02f, 0.72f),
            ArenaStoneKind.Limestone => new Vector3(1.36f, 1.3f, 1.16f),
            ArenaStoneKind.Granite => new Vector3(0.96f, 0.96f, 0.99f),
            ArenaStoneKind.Basalt => new Vector3(0.5f, 0.5f, 0.53f),
            ArenaStoneKind.TempleGold => new Vector3(1.42f, 1.1f, 0.68f),
            _ => new Vector3(0.6f, 0.58f, 0.6f),
        };

        /// <summary>How much darker a stone is at the foot of a wall than up it: sand kicked against it, blood, the weather.</summary>
        public static float Grime(float y) => 0.8f + 0.2f * Math.Clamp(y / 2.2f, 0f, 1f);

        /// <summary>A dressed stone at height <paramref name="y"/>, its own shade drawn from <paramref name="random"/>.</summary>
        public static VertexMaterial Dressed(ArenaStoneKind kind, WorldRandom random, float y, float spread = 0.05f)
        {
            float shade = (1f + random.Range(-spread, spread)) * Grime(y);
            float warm = random.Range(-0.03f, 0.03f);
            Vector3 c = ColourOf(kind) * shade;
            return new VertexMaterial(MaterialCatalog.Rock.Index, new Colour(c.X * (1f + warm), c.Y, c.Z * (1f - warm)));
        }

        /// <summary>A dressed stone whose top has taken the dust: what faces the sky is a little paler, as sand and ash settle on it.</summary>
        public static Func<Vector3, VertexMaterial> DressedFacing(ArenaStoneKind kind, WorldRandom random, float y, float dust = 0.12f, float spread = 0.05f)
        {
            VertexMaterial face = Dressed(kind, random, y, spread);
            VertexMaterial top = Tint(face, new Vector3(1f + dust, 1f + dust * 0.95f, 1f + dust * 0.8f));
            return normal => normal.Y > 0.6f ? top : face;
        }

        /// <summary>The dark between stones and down a passage.</summary>
        public static VertexMaterial Hearting(ArenaStoneKind kind) => new VertexMaterial(MaterialCatalog.Rock.Index, new Colour(ColourOf(kind) * 0.3f));

        /// <summary>Nothing at all: the back of a passage into the dark, which takes no light.</summary>
        public static VertexMaterial Dark => new VertexMaterial(MaterialCatalog.Flat.Index, new Colour(0.012f, 0.011f, 0.011f));

        public static VertexMaterial Iron(WorldRandom random, float shade = 1f) => new VertexMaterial(MaterialCatalog.Iron.Index, new Colour(IronColour * shade * random.Range(0.9f, 1.1f)));

        /// <summary>Iron that's stood out in the weather: rust coming through the black where water runs.</summary>
        public static VertexMaterial Rusted(WorldRandom random) => new VertexMaterial(MaterialCatalog.Iron.Index, new Colour(Vector3.Lerp(IronColour, new Vector3(0.2f, 0.09f, 0.045f), random.Range(0.25f, 0.6f))));

        public static VertexMaterial Brass(WorldRandom random, float shade = 1f) => new VertexMaterial(MaterialCatalog.Iron.Index, new Colour(BrassColour * shade * random.Range(0.92f, 1.06f)));

        public static VertexMaterial Oxblood(float shade = 1f) => new VertexMaterial(MaterialCatalog.Cloth.Index, new Colour(OxbloodColour * shade));

        public static VertexMaterial Leather(float shade = 1f) => new VertexMaterial(MaterialCatalog.Leather.Index, new Colour(new Vector3(0.16f, 0.07f, 0.045f) * shade));

        public static VertexMaterial Timber(WorldRandom random, float shade = 1f) => new VertexMaterial(MaterialCatalog.Wood.Index, new Colour(new Vector3(0.36f, 0.27f, 0.2f) * shade * random.Range(0.9f, 1.08f)));

        public static VertexMaterial Glow(float r, float g, float b, float strength) => new VertexMaterial(MaterialCatalog.FlatGlow.Index, new Colour(r, g, b, strength));

        /// <summary>A brazier's bed of embers under its coals, which burn as particles above them (<see cref="AssetLight.Burning"/>): deep, as anything glowing that's pale goes white.</summary>
        public static VertexMaterial Coals => Glow(1f, 0.32f, 0.07f, 1.35f);

        /// <summary>A coal on top of the embers, burnt dark.</summary>
        public static VertexMaterial Coal => new VertexMaterial(MaterialCatalog.Rock.Index, new Colour(0.06f, 0.05f, 0.045f));

        public static VertexMaterial Tint(VertexMaterial material, Vector3 by)
        {
            Colour c = material.colour;
            return new VertexMaterial(material.materialTextureIndex, new Colour(c.Red * by.X, c.Green * by.Y, c.Blue * by.Z, c.Intensity, c.Alpha));
        }

        /// <summary>A brazier's fire: a little less than a campfire, as an arena has a ring of them in view, and a light's cost is every pixel it reaches.</summary>
        public static AssetLight BrazierLight(Vector3 at) => AssetLight.Campfire(at) with { Intensity = 105f, Radius = 21f };

        /// <summary>A torch on a wall.</summary>
        public static AssetLight TorchLight(Vector3 at) => AssetLight.Lantern(at) with { Intensity = 70f, Radius = 15f, Flicker = 0.24f, FlickerSpeed = 9f };

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

        /// <summary>
        /// The Reich's mark (<see cref="ReichMark"/>) as a brass plate <paramref name="thick"/> deep standing out of whatever's behind it, facing <paramref name="normal"/>, its snout towards
        /// <paramref name="right"/>, with its eye and openings cut through to <paramref name="field"/>, the colour behind it.
        /// </summary>
        public static void Mark(MeshBuilder b, Vector3 centre, Vector3 right, Vector3 up, float size, float thick, VertexMaterial brass, VertexMaterial field)
        {
            Vector3 normal = Vector3.Cross(right, up).Normalized();
            Vector3 At(float x, float y, float out_) => centre + right * (x * size) + up * (y * size) + normal * out_;
            (float X, float Y)[] skull = ReichMark.Skull;
            Vector3 middleFront = At(ReichMark.Middle.X, ReichMark.Middle.Y, thick);
            for (int i = 0; i < skull.Length; i++)
            {
                (float x0, float y0) = skull[i];
                (float x1, float y1) = skull[(i + 1) % skull.Length];
                Vector3 a = At(x0, y0, thick), c = At(x1, y1, thick);
                // The outline runs clockwise seen from in front, so its fan is wound the other way to face out, and its edge faces to the left of the way round.
                b.Triangle(middleFront, c, a, brass);
                b.Quad(At(x1, y1, 0f), At(x0, y0, 0f), a, c, brass);
            }
            foreach ((float x, float y, float rx, float ry) in ReichMark.Holes)
            {
                const int Sides = 10;
                Vector3 hole = At(x, y, thick + 0.004f);
                for (int k = 0; k < Sides; k++)
                {
                    float a0 = k * MathF.Tau / Sides, a1 = (k + 1) * MathF.Tau / Sides;
                    b.Triangle(hole, At(x + MathF.Cos(a0) * rx, y + MathF.Sin(a0) * ry, thick + 0.004f), At(x + MathF.Cos(a1) * rx, y + MathF.Sin(a1) * ry, thick + 0.004f), field);
                }
            }
        }
    }
}
