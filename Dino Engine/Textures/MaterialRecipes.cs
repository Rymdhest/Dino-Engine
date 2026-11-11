using Dino_Engine.Textures.Synthesis;
using OpenTK.Mathematics;
using static Dino_Engine.Textures.Synthesis.Colours;

namespace Dino_Engine.Textures
{
    /// <summary>
    /// How every material in <see cref="MaterialCatalog"/> looks: a recipe draws on a <see cref="Canvas"/> of one tile and returns the finished <see cref="Surface"/>.
    /// A height runs from 0, the bottom of the relief, to 1, where the mesh is, and is the shape of the thing: everything else follows from it, as the light, the
    /// shadows in the relief and the parallax all read the same height at the same depth. Make a material the way it was made: what's underneath first, then what
    /// lies on it, then what time did to it. Seen from the game's camera a metre is about thirty pixels, so the large shapes are what carry a material; the fine
    /// ones are for close up. See TEXTURES.md.
    /// </summary>
    public static partial class MaterialRecipes
    {
        public static Surface Make(Canvas c)
        {
            MaterialSpec spec = c.Spec;
            if (spec == MaterialCatalog.Flat) return Flat(c);
            if (spec == MaterialCatalog.FlatGlow) return FlatGlow(c);
            if (spec == MaterialCatalog.Mirror) return Mirror(c);
            if (spec == MaterialCatalog.Gem) return Gem(c);
            if (spec == MaterialCatalog.Wax) return Wax(c);
            if (spec == MaterialCatalog.Grain) return GrainSurface(c);
            if (spec == MaterialCatalog.FoliageWhite) return FoliageWhite(c);

            if (spec == MaterialCatalog.Soil) return Soil(c);
            if (spec == MaterialCatalog.Grass) return GrassGround(c);
            if (spec == MaterialCatalog.GrassySoil) return GrassySoil(c);
            if (spec == MaterialCatalog.Sand) return Sand(c);
            if (spec == MaterialCatalog.SandDunes) return Dunes(c);
            if (spec == MaterialCatalog.CrackedDesert) return CrackedEarth(c);
            if (spec == MaterialCatalog.CrackedLava) return Ash(c);
            if (spec == MaterialCatalog.Snow) return Snow(c);
            if (spec == MaterialCatalog.Ice) return Ice(c);
            if (spec == MaterialCatalog.Mud) return Mud(c);
            if (spec == MaterialCatalog.Moss) return Moss(c);
            if (spec == MaterialCatalog.Gravel) return Gravel(c);
            if (spec == MaterialCatalog.Pebbles) return Pebbles(c);
            if (spec == MaterialCatalog.Cobble) return Cobble(c);
            if (spec == MaterialCatalog.Setts) return Setts(c);
            if (spec == MaterialCatalog.RoadDirt) return RoadDirt(c);
            if (spec == MaterialCatalog.Farmland) return Farmland(c);
            if (spec == MaterialCatalog.Cliff) return Cliff(c);
            if (spec == MaterialCatalog.Rock) return RockSurface(c);

            if (spec == MaterialCatalog.StoneWall) return StoneWall(c);
            if (spec == MaterialCatalog.Brick) return Brick(c);
            if (spec == MaterialCatalog.Plaster) return Plaster(c);
            if (spec == MaterialCatalog.Wood) return Wood(c);
            if (spec == MaterialCatalog.Thatch) return Thatch(c);
            if (spec == MaterialCatalog.Shingle) return Shingle(c);
            if (spec == MaterialCatalog.Slate) return Slate(c);
            if (spec == MaterialCatalog.MetalFloor) return MetalFloor(c);
            if (spec == MaterialCatalog.Iron) return Iron(c);

            if (spec == MaterialCatalog.BarkFurrowed) return BarkFurrowed(c);
            if (spec == MaterialCatalog.BarkPlated) return BarkPlated(c);
            if (spec == MaterialCatalog.BarkPapery) return BarkPapery(c);
            if (spec == MaterialCatalog.BarkSmooth) return BarkSmooth(c);
            if (spec == MaterialCatalog.BarkFibrous) return BarkFibrous(c);
            if (spec == MaterialCatalog.BarkRinged) return BarkRinged(c);
            if (spec == MaterialCatalog.BarkBare) return BarkBare(c);
            if (spec == MaterialCatalog.BarkCharred) return BarkCharred(c);
            if (spec == MaterialCatalog.PineBark) return PineBark(c);

            if (spec == MaterialCatalog.Plate) return Plate(c);
            if (spec == MaterialCatalog.Mail) return Mail(c);
            if (spec == MaterialCatalog.Leather) return Leather(c);
            if (spec == MaterialCatalog.Cloth) return Cloth(c);
            if (spec == MaterialCatalog.Bone) return Bone(c);
            if (spec == MaterialCatalog.Flagstones) return Flagstones(c);
            if (spec == MaterialCatalog.Needles) return Needles(c);
            if (spec == MaterialCatalog.RedEarth) return RedEarth(c);
            if (spec == MaterialCatalog.Litter) return LeafLitter(c);
            if (spec == MaterialCatalog.RoofTiles) return RoofTiles(c);

            throw new InvalidOperationException($"No recipe for {spec.Name}.");
        }

        // ---------------------------------------------------------------- Plain

        private static Surface Flat(Canvas c) => new Surface { Albedo = Grey(255), Roughness = 0.5f };

        private static Surface FlatGlow(Canvas c) => new Surface { Albedo = Grey(255), Roughness = 1f, Emission = 0.5f };

        private static Surface Mirror(Canvas c) => new Surface { Albedo = Grey(255), Roughness = 0.08f, Metallic = 1f };

        private static Surface Gem(Canvas c) => new Surface { Albedo = Grey(255), Roughness = 0.08f, Metallic = 0.2f, Subsurface = 0.5f };

        private static Surface FoliageWhite(Canvas c) => new Surface { Albedo = Grey(255), Roughness = 0.9f, Subsurface = 0.5f };

        // Tallow, a little uneven where it's run.
        private static Surface Wax(Canvas c)
        {
            Field runs = c.Noise(new Vector2(3f, 1f), octaves: 4, warp: 0.05f);
            return new Surface
            {
                Albedo = Paint.Lerp(Rgb(108, 106, 86), Rgb(98, 94, 74), runs),
                Roughness = Field.Lerp(0.55f, 0.75f, runs),
                Subsurface = 0.35f,
                Height = runs,
            };
        }

        private static Surface GrainSurface(Canvas c)
        {
            Field speckle = c.Noise(8f, octaves: 6);
            return new Surface { Albedo = Rgb(255, 10, 25) * Field.Lerp(0.85f, 1f, speckle), Roughness = 0.4f, Subsurface = 0.8f };
        }

        // ---------------------------------------------------------------- Shared pieces

        /// <summary>A mask of patches, about <paramref name="cover"/> of the tile, edges eased over <paramref name="softness"/>: where moss grows, where dust lies.</summary>
        private static Field Patches(Canvas c, float frequency, float cover, float softness = 0.08f, float warp = 0.05f)
        {
            return c.Noise(frequency, octaves: 5, warp: warp).Equalize().Threshold(1f - cover, softness);
        }
    }
}
