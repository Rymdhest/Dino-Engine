using Dino_Engine.Modelling.Procedural.Architecture.Churchyard;
using Dino_Engine.Modelling.Procedural.Architecture.Places;
using Dino_Engine.World.Content;
using OpenTK.Mathematics;

namespace Dino_Engine.World
{
    public static partial class Hitboxes
    {
        /// <summary>
        /// What's solid about a structure of the catalog, in the frame its model is built in, before it's scaled: <paramref name="parts"/> is cleared and filled. A building, a tower or
        /// a keep is solid all over its footprint (<see cref="HitKind.Layout"/>), as its walls stand on its edges; everything else is solid where its model stands above a knee.
        /// </summary>
        public static HitKind Of(StructureDefinition definition, List<HitPart> parts)
        {
            parts.Clear();
            if (definition.Loose) return HitKind.Loose;
            if (!definition.Solid) return HitKind.Open;
            if (definition.Styled || definition.OnPad) return HitKind.Layout;
            switch (definition.Id)
            {
                // Knee high or lower.
                case "woodpile":
                case "bench":
                case "trough":
                case "pen.trough":
                case "ruin.rubble":
                    return HitKind.Open;

                // Posts and poles.
                case "lantern": return Post(parts);
                case "signpost": return Post(parts, 0.2f);
                case "lantern.paper": return Post(parts, 0.18f);
                case "lamp.pillar": return Post(parts, 0.25f);
                case "pen.banner": return Post(parts);
                case "maypole": return Post(parts, 0.4f);
                case "lantern.stone": return Round(parts, 0.6f, 0f);
                case "shrine.wayside": return Round(parts, 0.5f, 0f);
                case "campfire": return Round(parts, 1f, 0f);
                case "stand.hunting":
                    foreach (float x in new[] { -1.25f, 1.25f })
                    {
                        Post(parts, 0.22f, x, -1.25f);
                        Post(parts, 0.22f, x, 1.25f);
                    }
                    return Rack(parts, 0.35f, 0.4f, 0f, 2f);

                // Racks and frames.
                case "rack": return Rack(parts, 2.8f, 0.45f);
                case "pen.rack": return Rack(parts, 2.45f, 0.5f, 0f, -0.15f);
                case "fossil": return Rack(parts, 4f, 1.3f, 0.4f, -0.1f);
                case "dig": return Rack(parts, 1.5f, 1f);

                // What stands in the way where it stands.
                case "well": return Round(parts, 1.65f, 1.3f);
                case "stall":
                    Box(parts, 3.4f, 0.75f, 1.8f, 0f, 1.75f);
                    Post(parts, 0.2f, -3.25f, -2.4f);
                    return Post(parts, 0.2f, 3.25f, -2.4f);
                case "cart": return Box(parts, 2.85f, 1.75f, 2.6f, 0.35f, 0f);
                case "haystack": return Round(parts, 2.05f, 3.2f);
                case "tent": return Box(parts, 2.3f, 3.2f, 4.4f);
                case "tent.large": return Box(parts, 4.3f, 3.5f, 6f);
                case "logs": return Box(parts, 4f, 1.95f, 2.55f);
                case "boat": return Box(parts, 4f, 1.45f, 1.5f);
                case "table.map": return Box(parts, 2.6f, 1.72f, 1.9f);
                case "strongbox": return Box(parts, 1.6f, 1f, 1.5f, -0.23f, -0.09f);
                case "arena.column": return Round(parts, 0.5f, 6.4f);
                case "altar": return Box(parts, 0.95f, 0.95f, 1.8f);
                case "monolith": return Round(parts, 1.15f, PlacementIndex.Tall);
                case "ruin.wall": return Box(parts, 4.5f, 0.7f, 3.5f);
                case "ruin.column": return Round(parts, 0.72f, 5f);
                case "cairn": return Round(parts, 0.68f, 2.1f);
                case "hives": return Box(parts, 1.9f, 0.52f, 2f);
                case "oven": return Box(parts, 1.3f, 1.45f, 2.25f, 0f, 0.2f);
                case "coop": return Box(parts, 1.66f, 1.26f, 2.68f);
                case "hayrack": return Box(parts, 7.6f, 0.48f, 3.6f);
                case "woodstack": return Round(parts, 1.72f, 3.4f);
                case "milestone": return Round(parts, 0.34f, 1.5f);
                case "mound.termite": return Round(parts, 1.3f, PlacementIndex.Tall);
                case "pen.cage": return Box(parts, 3.35f, 2.55f, 4.6f);
                case "well.sweep":
                    Box(parts, 1.25f, 1.25f, 1.6f, 0f, 2.25f);
                    Post(parts, 0.35f, 0f, -1.25f);
                    return Rack(parts, 0.3f, 0.8f, 0f, -3.25f);

                // An arena's gate and a dead pine keep the ground they're laid out on.
                default: return HitKind.Layout;
            }
        }

        /// <summary>
        /// What's solid about a piece of a churchyard (CHURCHYARD.md) in its own frame: <paramref name="parts"/> is cleared and filled. A grave is walked over, and so is any stone a
        /// person could step over or would brush through, a tablet, a slab, a book or what's left of one, as a person found they couldn't get about a churchyard for its low
        /// stones; a cross is a post, an obelisk and a chest tomb stand in the way, and an opened grave's pit is walked round. The church, its gates, its wall, a mausoleum and a family's
        /// railing are made solid by the churchyard as they stand.
        /// </summary>
        public static HitKind Of(in YardSpec spec, List<HitPart> parts)
        {
            parts.Clear();
            float head = -YardForge.GraveLength * 0.5f + 0.35f;
            switch (spec.Kind)
            {
                case YardKind.Stone:
                    return (StoneForm)spec.Int(0) switch
                    {
                        StoneForm.Obelisk => Box(parts, 0.62f, 0.62f, 3.2f, 0f, head - 0.05f),
                        StoneForm.Cross or StoneForm.Ringed => Post(parts, 0.25f, 0f, head),
                        StoneForm.Wooden => Post(parts, 0.2f, 0f, head - 0.1f),
                        _ => HitKind.Open,
                    };
                case YardKind.Tomb:
                {
                    Vector3 size = YardForge.TombSize(spec.Seed);
                    return Box(parts, size.X * 0.5f, size.Z * 0.5f, size.Y);
                }
                case YardKind.Dug: return Rack(parts, 1.1f, 1.8f);
                case YardKind.Cross: return Round(parts, 0.75f, 0f);
                case YardKind.Lamp: return Post(parts, 0.3f);
                case YardKind.Bench:
                case YardKind.Candles:
                    return HitKind.Open;
                default: return HitKind.Layout;
            }
        }

        // ---------------------------------------------------------------- Clutter that breaks

        /// <summary>
        /// What a placed thing breaks into if it's clutter that breaks, by its asset id, or null: the catalog's loose structures, and the crates, barrels, bales and pots of a place
        /// with a story, which the client breaks the same way.
        /// </summary>
        public static LooseKind? BreaksAs(string assetId)
        {
            if (StructureCatalog.TryGetPlain(assetId, out StructureDefinition definition))
            {
                return definition.Id switch
                {
                    "barrels" => new LooseKind(StructureCatalog.Barrels, 1.4f, 1.9f),
                    "crates" => new LooseKind(StructureCatalog.Crates, 1.5f, 2.3f),
                    "sacks" => new LooseKind(StructureCatalog.Sacks, 1.2f, 1.2f),
                    "jars" => new LooseKind(StructureCatalog.Jars, 1.3f, 1.8f),
                    "barrow" => new LooseKind(StructureCatalog.Crates, 1.5f, 1.3f),
                    _ => null,
                };
            }
            if (!PlaceSpec.TryParse(assetId, out PlaceSpec spec)) return null;
            int count = Math.Max(spec.Int(0), 1);
            return spec.Kind switch
            {
                PlacePiece.Barrels => new LooseKind(StructureCatalog.Barrels, count == 1 ? 0.55f : 1.6f, 1.35f),
                PlacePiece.Crates => new LooseKind(StructureCatalog.Crates, 0.9f + 0.35f * count, count > 2 ? 2.4f : 1.6f),
                PlacePiece.BoneCrates => new LooseKind(StructureCatalog.Crates, 1.9f, 1.6f),
                PlacePiece.Bales => new LooseKind(StructureCatalog.Sacks, 0.9f + 0.3f * count, 1.9f),
                PlacePiece.Pots => new LooseKind(StructureCatalog.Crates, 1.3f, 1.4f),
                PlacePiece.Cargo => new LooseKind(spec.Int(0) switch { 1 => StructureCatalog.Barrels, 3 => StructureCatalog.Sacks, _ => StructureCatalog.Crates }, 0.9f + 0.35f * Math.Max(spec.Int(1), 1), 1.4f),
                _ => null,
            };
        }
    }
}
