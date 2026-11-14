using Dino_Engine.Modelling.Model;
using Dino_Engine.Modelling.Procedural.Architecture.Arena;
using Dino_Engine.Modelling.Procedural.Flora;
using Dino_Engine.Modelling.Procedural.Rock;
using Dino_Engine.Textures;
using Dino_Engine.Util;
using Dino_Engine.World;
using Dino_Engine.World.Content;
using OpenTK.Mathematics;

namespace Dino_Engine.Modelling.Procedural.Architecture.Places
{
    /// <summary>
    /// What the places with a story of their own are made of (PLACES.md). Colours are albedos under a sun of about 13, multiplied with a material's own pale colour, as everywhere: anything
    /// pale goes white, so cloth and paint are deep. Black iron, oxblood and brass are the Reich's alone (LORE.md), and are exactly the values its harness and its arenas use
    /// (<see cref="ArenaStone"/>); the land's own iron is rust-brown, and its own red, where it has one, is the north's warm red, never oxblood. Headless.
    /// </summary>
    internal static class PlaceMaterials
    {
        /// <summary>Metres the ground is drawn above where people stand: whatever lies on the ground starts there.</summary>
        public const float Ground = 0.15f;

        // ---------------------------------------------------------------- The Reich's

        public static VertexMaterial Iron(WorldRandom random, float shade = 1f) => ArenaStone.Iron(random, shade);
        public static VertexMaterial Rusted(WorldRandom random) => ArenaStone.Rusted(random);
        public static VertexMaterial Brass(WorldRandom random, float shade = 1f) => ArenaStone.Brass(random, shade);
        public static VertexMaterial Oxblood(float shade = 1f) => ArenaStone.Oxblood(shade);
        public static VertexMaterial ReichLeather(float shade = 1f) => ArenaStone.Leather(shade);

        /// <summary>Oxblood laid on as paint, on iron or boards: a hull's band, a door's frame. Matt, as paint is: on anything metallic it shines scarlet in the sun, which reads as nobody's.</summary>
        public static VertexMaterial OxbloodPaint(float shade = 1f) => new VertexMaterial(MaterialCatalog.Plaster.Index, new Colour(ArenaStone.OxbloodColour * 0.9f * shade));

        /// <summary>The Reich's iron in plates, riveted in courses: its hulls, its doors, its walls.</summary>
        public static VertexMaterial Plating(WorldRandom random, float shade = 1f) => new VertexMaterial(MaterialCatalog.RivetedPlate.Index, new Colour(ArenaStone.IronColour * 1.25f * shade * random.Range(0.92f, 1.08f)));

        /// <summary>The Reich's plating gone to rust where the sea has had it.</summary>
        public static VertexMaterial RustedPlating(WorldRandom random) => new VertexMaterial(MaterialCatalog.RivetedPlate.Index, new Colour(Vector3.Lerp(ArenaStone.IronColour * 1.25f, new Vector3(0.24f, 0.1f, 0.05f), random.Range(0.35f, 0.7f))));

        // ---------------------------------------------------------------- The land's

        /// <summary>The land's iron, left out: rust-brown, never the Reich's black.</summary>
        public static VertexMaterial LandIron(WorldRandom random, float shade = 1f) => new VertexMaterial(MaterialCatalog.Iron.Index, new Colour(Vector3.Lerp(new Vector3(0.2f, 0.12f, 0.08f), new Vector3(0.27f, 0.18f, 0.11f), random.Float()) * shade));

        /// <summary>Timber, new and pale where it's been cut and sawn.</summary>
        public static VertexMaterial Fresh(WorldRandom random, float shade = 1f) => new VertexMaterial(MaterialCatalog.Wood.Index, new Colour(new Vector3(0.62f, 0.46f, 0.3f) * shade * random.Range(0.92f, 1.06f)));

        /// <summary>Timber a few years out in the weather, browning.</summary>
        public static VertexMaterial Timber(WorldRandom random, float shade = 1f) => new VertexMaterial(MaterialCatalog.Wood.Index, new Colour(new Vector3(0.42f, 0.32f, 0.23f) * shade * random.Range(0.9f, 1.08f)));

        /// <summary>Timber the weather has had for good: silver-grey.</summary>
        public static VertexMaterial Weathered(WorldRandom random, float shade = 1f) => new VertexMaterial(MaterialCatalog.Wood.Index, new Colour(new Vector3(0.44f, 0.42f, 0.38f) * shade * random.Range(0.88f, 1.06f)));

        /// <summary>Timber the sea has bleached: driftwood, a wreck's ribs.</summary>
        public static VertexMaterial Bleached(WorldRandom random, float shade = 1f) => new VertexMaterial(MaterialCatalog.BarkBare.Index, new Colour(new Vector3(0.62f, 0.6f, 0.55f) * shade * random.Range(0.9f, 1.05f)));

        /// <summary>Boards tarred against the weather and the sea: not black, as nothing but the Reich's iron is, but the deep brown of pine tar.</summary>
        public static VertexMaterial Tarred(WorldRandom random, float shade = 1f) => new VertexMaterial(MaterialCatalog.Wood.Index, new Colour(new Vector3(0.13f, 0.09f, 0.06f) * shade * random.Range(0.9f, 1.1f)));

        /// <summary>Boards painted in a colour of the land's own, deep, as pale paint goes white.</summary>
        public static VertexMaterial Painted(Vector3 colour) => new VertexMaterial(MaterialCatalog.PaintedBoards.Index, new Colour(colour));

        /// <summary>What a land's boats and ships are painted: a deep blue, a green, an ochre, a warm red that's the land's and not the Reich's, and white for a band.</summary>
        public static readonly Vector3[] BoatPaints =
        {
            new Vector3(0.1f, 0.18f, 0.32f), new Vector3(0.12f, 0.26f, 0.18f), new Vector3(0.5f, 0.34f, 0.1f), new Vector3(0.5f, 0.17f, 0.09f), new Vector3(0.2f, 0.28f, 0.34f),
        };

        public static readonly Vector3 BoatWhite = new Vector3(0.66f, 0.65f, 0.6f);

        /// <summary>Canvas and sailcloth, undyed and weathered.</summary>
        public static VertexMaterial Canvas(WorldRandom random, float shade = 1f) => new VertexMaterial(MaterialCatalog.Cloth.Index, new Colour(Vector3.Lerp(new Vector3(0.6f, 0.56f, 0.46f), new Vector3(0.52f, 0.5f, 0.44f), random.Float()) * shade));

        /// <summary>Rope of hemp, brown with tar and wear.</summary>
        public static VertexMaterial Rope(WorldRandom random, float shade = 1f) => new VertexMaterial(MaterialCatalog.Cloth.Index, new Colour(new Vector3(0.36f, 0.28f, 0.18f) * shade * random.Range(0.9f, 1.08f)));

        public static VertexMaterial Straw(WorldRandom random, float shade = 1f) => new VertexMaterial(MaterialCatalog.Thatch.Index, new Colour(new Vector3(0.62f, 0.52f, 0.3f) * shade * random.Range(0.9f, 1.06f)));

        public static VertexMaterial Soil(float shade = 1f) => new VertexMaterial(MaterialCatalog.Soil.Index, new Colour(new Vector3(0.3f, 0.23f, 0.16f) * shade));

        public static VertexMaterial Sand(float shade = 1f) => new VertexMaterial(MaterialCatalog.Sand.Index, new Colour(new Vector3(0.72f, 0.64f, 0.48f) * shade));

        public static VertexMaterial Turf(float shade = 1f) => new VertexMaterial(MaterialCatalog.GrassySoil.Index, new Colour(new Vector3(0.26f, 0.3f, 0.15f) * shade));

        public static VertexMaterial Bone(WorldRandom random, float shade = 1f) => new VertexMaterial(MaterialCatalog.Bone.Index, new Colour(new Vector3(0.6f, 0.55f, 0.45f) * shade * random.Range(0.88f, 1.05f)));

        /// <summary>Old bone out of the rock, stained the rock's colour, and still paler than the dug earth it lies in, or a pit's floor swallows it from the camera.</summary>
        public static VertexMaterial Fossil(WorldRandom random, float shade = 1f) => new VertexMaterial(MaterialCatalog.Bone.Index, new Colour(Vector3.Lerp(new Vector3(0.68f, 0.58f, 0.43f), new Vector3(0.6f, 0.55f, 0.47f), random.Float()) * shade));

        public static VertexMaterial Plaster(float shade = 1f) => new VertexMaterial(MaterialCatalog.Plaster.Index, new Colour(new Vector3(0.74f, 0.72f, 0.66f) * shade));

        public static VertexMaterial Flesh(float shade = 1f) => new VertexMaterial(MaterialCatalog.Leather.Index, new Colour(new Vector3(0.4f, 0.12f, 0.1f) * shade));

        public static VertexMaterial Hide(Vector3 colour) => new VertexMaterial(MaterialCatalog.Hide.Index, new Colour(colour));

        public static VertexMaterial Snow(float shade = 1f) => new VertexMaterial(MaterialCatalog.Snow.Index, new Colour(new Vector3(0.86f, 0.88f, 0.92f) * shade));

        public static VertexMaterial PackedSnow(float shade = 1f) => new VertexMaterial(MaterialCatalog.PackedSnow.Index, new Colour(new Vector3(0.84f, 0.86f, 0.9f) * shade));

        public static VertexMaterial Ice(float shade = 1f) => new VertexMaterial(MaterialCatalog.Ice.Index, new Colour(new Vector3(0.5f, 0.66f, 0.76f) * shade));

        public static VertexMaterial Glass(Vector3 tint) => new VertexMaterial(MaterialCatalog.Gem.Index, new Colour(tint));

        public static VertexMaterial Net(WorldRandom random, float shade = 1f) => new VertexMaterial(MaterialCatalog.Netting.Index, new Colour(new Vector3(0.36f, 0.32f, 0.26f) * shade * random.Range(0.85f, 1.1f)));

        /// <summary>Stone freshly cut, of a colour an id carries (<see cref="StoneOf"/>), paler than the weathered cliff it came out of.</summary>
        public static VertexMaterial CutStone(Vector3 colour, float y = 2f) => new VertexMaterial(MaterialCatalog.CutStone.Index, new Colour(colour * Grime(y)));

        /// <summary>The same stone dressed, as a block is once a mason's had it.</summary>
        public static VertexMaterial Dressed(Vector3 colour, WorldRandom random, float y = 2f) => new VertexMaterial(MaterialCatalog.Rock.Index, new Colour(colour * 0.95f * Grime(y) * random.Range(0.94f, 1.05f)));

        /// <summary>A stone's colour from the three per mille an id carries.</summary>
        public static Vector3 StoneColour(int red, int green, int blue) => new Vector3(red, green, blue) / 1000f;

        /// <summary>
        /// The colour of a world's stone freshly cut, per mille: its rock's own (<see cref="Geology.Colour"/>, what the cliffs are drawn in), brought up from weathered to new, so a quarry's blocks
        /// are the cliff they came out of, as every stone in a land is (TERRAIN.md).
        /// </summary>
        public static (int R, int G, int B) StoneOf(Geology? geology)
        {
            Vector3 rock = geology?.Colour ?? new Vector3(0.32f, 0.31f, 0.29f);
            Vector3 cut = Vector3.Clamp(rock * 1.55f, Vector3.Zero, new Vector3(0.6f));
            return ((int)MathF.Round(cut.X * 1000f), (int)MathF.Round(cut.Y * 1000f), (int)MathF.Round(cut.Z * 1000f));
        }

        /// <summary>How much darker anything is near the ground, where the wet and the dirt have had longest.</summary>
        public static float Grime(float y) => 0.82f + 0.18f * Math.Clamp(y / 2.4f, 0f, 1f);

        public static VertexMaterial Glow(float r, float g, float b, float strength) => new VertexMaterial(MaterialCatalog.FlatGlow.Index, new Colour(r, g, b, strength));

        /// <summary>The Institute's amber, lit from inside: deep, as glass that glows pale goes white.</summary>
        public static VertexMaterial AmberGlow(float strength = 1.6f) => Glow(1f, 0.46f, 0.06f, strength);

        /// <summary>A flame's glow in a lantern's glass.</summary>
        public static VertexMaterial LampGlow(float strength = 2f) => Glow(1f, 0.62f, 0.26f, strength);

        /// <summary>The Ossuary's cold light, in a skull's eyes and where its heart was.</summary>
        public static VertexMaterial OssuaryGlow(float strength = 2.2f) => Glow(0.35f, 1f, 0.82f, strength);

        public static VertexMaterial Coals => ArenaStone.Coals;

        /// <summary>The dark inside a hold, a pit or a doorway.</summary>
        public static VertexMaterial Dark => new VertexMaterial(MaterialCatalog.Flat.Index, new Colour(0.02f, 0.018f, 0.016f));

        public static VertexMaterial Flat(Vector3 colour) => new VertexMaterial(MaterialCatalog.Flat.Index, new Colour(colour));

        public static VertexMaterial Tint(VertexMaterial material, Vector3 by)
        {
            Colour c = material.colour;
            return new VertexMaterial(material.materialTextureIndex, new Colour(c.Red * by.X, c.Green * by.Y, c.Blue * by.Z, c.Intensity, c.Alpha));
        }

        /// <summary>The bark of a kind of tree, as its own plants have it, and the colour it's drawn in.</summary>
        public static VertexMaterial Bark(BarkKind kind, Vector3 colour) => new VertexMaterial(BarkLayer(kind), new Colour(colour));

        private static int BarkLayer(BarkKind kind) => kind switch
        {
            BarkKind.Plated => MaterialCatalog.BarkPlated.Index,
            BarkKind.Papery => MaterialCatalog.BarkPapery.Index,
            BarkKind.Smooth or BarkKind.Cane => MaterialCatalog.BarkSmooth.Index,
            BarkKind.Fibrous => MaterialCatalog.BarkFibrous.Index,
            BarkKind.Ringed => MaterialCatalog.BarkRinged.Index,
            BarkKind.Bare => MaterialCatalog.BarkBare.Index,
            BarkKind.Charred => MaterialCatalog.BarkCharred.Index,
            BarkKind.Fissured => MaterialCatalog.BarkFissured.Index,
            BarkKind.Flaking => MaterialCatalog.BarkFlaking.Index,
            _ => MaterialCatalog.BarkFurrowed.Index,
        };

        // ---------------------------------------------------------------- Lights

        /// <summary>A lantern hung at a place: a warm pool only as big as it needs, as a place has several in view and a light's cost is every pixel it reaches.</summary>
        public static AssetLight Lantern(Vector3 at) => AssetLight.Lantern(at) with { Intensity = 85f, Radius = 13f, Flicker = 0.12f, FlickerSpeed = 6f };

        public static AssetLight Brazier(Vector3 at) => ArenaStone.BrazierLight(at) with { Intensity = 90f, Radius = 17f };

        public static AssetLight Fire(Vector3 at) => AssetLight.Campfire(at) with { Intensity = 110f, Radius = 20f };

        /// <summary>The Institute's amber shining out of a vat or a window.</summary>
        public static AssetLight Amber(Vector3 at, float intensity = 70f, float radius = 13f) => AssetLight.Glow(at, new Vector3(1f, 0.5f, 0.12f), intensity, radius);

        /// <summary>The Ossuary's cold light, faint, as it's what's left in the bones.</summary>
        public static AssetLight Cold(Vector3 at, float intensity = 24f, float radius = 8f) => AssetLight.Glow(at, new Vector3(0.35f, 1f, 0.8f), intensity, radius);

        /// <summary>A candle in a lantern of snow: a small warm pool on the snow, as a snow castle is lit by a dozen.</summary>
        public static AssetLight SnowCandle(Vector3 at) => AssetLight.Candles(at) with { Intensity = 22f, Radius = 8f, Flicker = 0.22f };
    }
}
