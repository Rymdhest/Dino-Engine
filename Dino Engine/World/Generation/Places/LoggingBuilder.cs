using Dino_Engine.Modelling.Procedural.Architecture.Places;
using Dino_Engine.World.Content;
using OpenTK.Mathematics;

namespace Dino_Engine.World.Generation.Places
{
    /// <summary>
    /// Lays out a logging camp (PLACES.md) at the edge of a wood it's felling for the Reich's pens. Behind it, towards the wood, the felling: stumps where the trees were, their bark the wood's
    /// own, and the trunks lying where they fell, limbed; in front, the camp: the bunkhouse in the land's own way of building, the fire with its pot, the sawpit with a trunk over it, boards
    /// stacked to season, the rack of tools and the grindstone; by the road the logs decked to be carted and the high wheels they're dragged out under; at its edge a charcoal clamp smoking under
    /// its turf. The ground is chips and sawdust. The greycoats who never leave hold it, and the land's woodcutters come back to it once it's freed.
    /// </summary>
    internal sealed class LoggingBuilder : PlaceBuilder
    {
        private int[] _wood = null!;
        private Vector2 _toWoods;

        private LoggingBuilder(SiteBuilder b) : base(b, "logging") { }

        public static void Build(SiteBuilder b) => new LoggingBuilder(b).Lay();

        private void Lay()
        {
            _wood = WoodOf();
            _toWoods = TowardsWoods();
            // Its front, its +z, away from the wood: the felling is behind it.
            Plan.Yaw = SiteBuilder.YawFacing(-_toWoods);
            Plan.Half = new Vector2(Site.Radius);
            Paint(GroundPaint.Yard, Footprint.Box(W(new Vector2(0f, Site.Radius * 0.2f)), new Vector2(Site.Radius * 0.6f, Site.Radius * 0.42f), Plan.Yaw), 6f);
            Felling(Random.Fork("felling"));
            Camp(Random.Fork("camp"));
            Worked(Random.Fork("worked"));
            Hold(Random.Fork("holders"));
        }

        // Which way the wood is thickest, a stone's throw out.
        private Vector2 TowardsWoods()
        {
            Vector2 best = -Site.RoadDirection;
            float most = -1f;
            for (int k = 0; k < 16; k++)
            {
                Vector2 way = SiteBuilder.Direction(k * MathF.Tau / 16f);
                float woods = 0f;
                foreach (float d in new[] { 35f, 55f, 75f })
                {
                    Vector2 at = Site.Position + way * d;
                    if (!World.InBounds(at)) continue;
                    for (int b = 0; b < World.BiomeWeights.Length; b++) woods += World.Plan.Biomes[b].ForestCover * World.BiomeWeights[b].Sample(at);
                }
                if (woods <= most) continue;
                most = woods;
                best = way;
            }
            return best;
        }

        // The felling, between the camp and the wood: stumps, and trunks lying where they fell, pointing away from the wood, as a tree is felled out into the open.
        private void Felling(WorldRandom r)
        {
            int stumps = r.Int(9, 14);
            var cut = new List<Vector2>();
            for (int k = 0, tries = 0; k < stumps && tries < 60; tries++)
            {
                Vector2 local = new Vector2(r.Range(-Site.Radius * 0.85f, Site.Radius * 0.85f), -r.Range(Site.Radius * 0.25f, Site.Radius * 0.95f));
                if (cut.Any(c => (c - local).Length < 3.4f)) continue;
                float radius = r.Range(0.4f, 0.75f);
                if (!PutRound(new PlaceSpec(PlacePiece.Stump, NextSeed(), With(new[] { PlaceSpec.Cm(radius), PlaceSpec.Cm(r.Range(0.5f, 1.1f)) }, _wood)), W(local), r.Float() * MathF.Tau, radius + 0.3f, 1f, 0.3f, 0.5f)) continue;
                cut.Add(local);
                k++;
            }
            // Some fell and lie there still, limbed, their butts by their stumps.
            int felled = Math.Min(r.Int(3, 5), cut.Count);
            for (int k = 0; k < felled; k++)
            {
                Vector2 stump = cut[k];
                float length = r.Range(9f, 15f), radius = r.Range(0.45f, 0.7f);
                Vector2 fall = Rotate(Vector2.UnitY, r.Range(-0.7f, 0.7f));
                Vector2 middle = stump + fall * (length * 0.5f + 1.2f);
                float yaw = YawOf(fall) - MathF.PI * 0.5f;
                PutBox(new PlaceSpec(PlacePiece.Log, NextSeed(), With(new[] { PlaceSpec.Cm(length), PlaceSpec.Cm(radius) }, _wood)), W(middle), yaw, new Vector2(length * 0.5f, radius + 0.2f), 0.9f, 0.2f, 0.45f);
            }
        }

        private static Vector2 Rotate(Vector2 v, float a) => new Vector2(v.X * MathF.Cos(a) - v.Y * MathF.Sin(a), v.X * MathF.Sin(a) + v.Y * MathF.Cos(a));

        // The camp, in front of the felling: the bunkhouse and the fire, the sawpit and the stacked boards, the tools, the logs decked by the road with the wheels, and the clamp at its edge.
        private void Camp(WorldRandom r)
        {
            // The logs decked beside the road, along it, to be carted to the Reich: first, as the road is what they're there for.
            Vector2 road = Plan.ToLocal(Site.Position + Site.RoadDirection);
            road = road.LengthSquared > 1e-4f ? road.Normalized() : Vector2.UnitY;
            Vector2 across = new Vector2(-road.Y, road.X);
            Tries(12, () =>
            {
                float length = r.Range(7f, 10f);
                Vector2 local = road * Site.Radius * r.Range(0.4f, 0.75f) + across * ((r.Chance(0.5f) ? -1f : 1f) * r.Range(7f, 9f));
                return PutBox(new PlaceSpec(PlacePiece.LogDeck, NextSeed(), With(new[] { PlaceSpec.Cm(length), r.Int(3, 4) }, _wood)), W(local), YawOf(road), new Vector2(3.4f, length * 0.5f + 0.6f), 3.2f, 0.5f, 0.35f);
            });
            // The bunkhouse, in the land's own way of building, its door to the camp, and one who slept there back by it once it's free.
            Tries(12, () =>
            {
                Vector2 local = new Vector2(r.Range(-Site.Radius * 0.5f, Site.Radius * 0.5f), r.Range(Site.Radius * 0.35f, Site.Radius * 0.6f));
                if (!Structure(StructureCatalog.Hut, W(local), YawOf(-local), 1f, 0.34f)) return false;
                Stand(SpawnRole.Returning, Around(W(local * 0.55f), 4f, r.Fork("sleeper"), 16), 1, 3f, r.Fork("sleeper stand"));
                return true;
            });
            // The fire, the sawpit, the stacked boards, the tools and the wheels in the camp; the clamp off to a side, where its smoke blows away from the beds.
            var pieces = new (PlacePiece Kind, Vector2 Half, float Height, float Padding)[]
            {
                (PlacePiece.Campfire, new Vector2(1.4f, 1.4f), 0.8f, 0.6f),
                (PlacePiece.Sawpit, new Vector2(4.4f, 1.5f), 1.2f, 0.6f),
                (PlacePiece.PlankStack, new Vector2(2.7f, 1.3f), 1.6f, 0.5f),
                (PlacePiece.ToolRack, new Vector2(3.6f, 0.8f), 2f, 0.5f),
            };
            foreach ((PlacePiece kind, Vector2 half, float height, float padding) in pieces)
            {
                Tries(30, () =>
                {
                    Vector2 local = CampSpot(r);
                    var spec = kind == PlacePiece.PlankStack ? new PlaceSpec(kind, NextSeed(), PlaceSpec.Cm(r.Range(4f, 5f))) : new PlaceSpec(kind, NextSeed());
                    return PutBox(spec, W(local), r.Float() * MathF.Tau, half, height, padding, 0.4f);
                });
            }
            // The high wheels at the edge of the felling with a log slung under them, their tongue towards the road, halfway through dragging it out.
            Tries(30, () =>
            {
                Vector2 local = new Vector2(r.Range(-Site.Radius * 0.8f, Site.Radius * 0.8f), r.Range(-Site.Radius * 0.35f, Site.Radius * 0.4f));
                float yaw = YawOf(road) + r.Range(-0.5f, 0.5f);
                return PutBox(new PlaceSpec(PlacePiece.Skidder, NextSeed(), 1), W(local), yaw, new Vector2(1.9f, 7.2f), 3f, 0.4f, 0.4f);
            });
            Tries(30, () =>
            {
                float radius = r.Range(2.6f, 3.2f);
                Vector2 local = new Vector2((r.Chance(0.5f) ? -1f : 1f) * Site.Radius * r.Range(0.55f, 0.8f), r.Range(-Site.Radius * 0.2f, Site.Radius * 0.3f));
                return PutRound(new PlaceSpec(PlacePiece.CharcoalClamp, NextSeed(), PlaceSpec.Cm(radius)), W(local), r.Float() * MathF.Tau, Logging.ClampReach(radius), 2.4f, 0.6f, 0.4f);
            });
            Tries(24, () => Structure(StructureCatalog.Cart, W(CampSpot(r)), r.Float() * MathF.Tau, 0.5f));
            for (int k = 0; k < 2; k++) Tries(20, () => PutBox(new PlaceSpec(PlacePiece.Barrels, NextSeed(), r.Int(2, 4)), W(CampSpot(r)), r.Float() * MathF.Tau, new Vector2(1.6f), 1.4f, 0.4f));
            for (int k = 0; k < 2; k++) Tries(20, () => PutRound(new PlaceSpec(PlacePiece.Lantern, NextSeed(), PlaceSpec.Cm(3.8f), 0), W(CampSpot(r)), r.Float() * MathF.Tau, 0.4f, 0f, 0.3f, 0.5f));
        }

        // The camp as it's lived and worked in (PLACES.md): the skid road worn from the felling down through the camp to the road, chips and bark thick round the sawpit, the deck and
        // the stumps, the greycoats who never leave in their tents with their table, brazier and spears, a barricade where the road comes in, their cressets, a woodpile by the hut,
        // and the Reich's wagon waiting by the deck.
        private void Worked(WorldRandom r)
        {
            Vector2 road = Plan.ToLocal(Site.Position + Site.RoadDirection);
            road = road.LengthSquared > 1e-4f ? road.Normalized() : Vector2.UnitY;
            Vector2 across = new Vector2(-road.Y, road.X);
            Trail(new Vector2(0f, -Site.Radius * 0.75f), new Vector2(0f, Site.Radius * 0.3f), 3.4f, GroundPaint.Yard, 2f);
            Trail(new Vector2(0f, Site.Radius * 0.3f), road * Site.Radius, 3f, GroundPaint.Yard, 1.8f);
            // The greycoats' tents along a side of the camp, their table, brazier and spears.
            float side = r.Chance(0.5f) ? 1f : -1f;
            for (int k = 0, tries = 0; k < 2 && tries < 30; tries++)
            {
                Vector2 at = tries < 10 ? new Vector2(side * r.Range(Site.Radius * 0.45f, Site.Radius * 0.75f), r.Range(Site.Radius * 0.05f, Site.Radius * 0.55f)) : CampSpot(r);
                if (PutBox(new PlaceSpec(PlacePiece.Tent, NextSeed(), PlaceSpec.Cm(r.Range(4.2f, 5f)), 1), W(at), YawOf(new Vector2(-side, 0f)), new Vector2(3.8f, 3.4f), 3.4f, 0.4f, 0.4f)) k++;
            }
            // Their table where it's wanted if the road lets it be, and wherever in the camp it will be if not; the brazier and the spears by the table.
            Vector2 mess = new Vector2(side * r.Range(Site.Radius * 0.2f, Site.Radius * 0.4f), r.Range(Site.Radius * 0.2f, Site.Radius * 0.45f));
            for (int k = 0; k < 24; k++)
            {
                Vector2 tried = k < 6 ? mess + new Vector2(r.Range(-2f, 2f), r.Range(-2f, 2f)) : CampSpot(r);
                if (!PutBox(new PlaceSpec(PlacePiece.MessTable, NextSeed(), PlaceSpec.Cm(r.Range(2.4f, 3.2f))), W(tried), YawOf(Vector2.UnitX), new Vector2(2.2f, 1.3f), 1.2f, 0.3f, 0.4f)) continue;
                mess = tried;
                break;
            }
            Tries(16, () => PutRound(new PlaceSpec(PlacePiece.Brazier, NextSeed(), PlaceSpec.Cm(r.Range(1f, 1.2f))), W(mess + SiteBuilder.Direction(r.Float() * MathF.Tau) * r.Range(2.5f, 4.5f)), 0f, 0.8f, 1.4f, 0.3f, 0.4f));
            Tries(16, () => PutBox(new PlaceSpec(PlacePiece.WeaponRack, NextSeed(), PlaceSpec.Cm(r.Range(2f, 2.6f)), 1), W(mess + SiteBuilder.Direction(r.Float() * MathF.Tau) * r.Range(3f, 5f)), YawOf(new Vector2(-side, 0f)), new Vector2(1.5f, 0.6f), 2f, 0.3f, 0.4f));
            // A barricade where the road comes in, a gap left to pass, and the Reich's cressets either side of the way.
            Tries(6, () => PutBox(new PlaceSpec(PlacePiece.Barricade, NextSeed(), PlaceSpec.Cm(r.Range(3.4f, 4.4f)), 1), W(road * Site.Radius * 0.85f + across * ((r.Chance(0.5f) ? 1f : -1f) * r.Range(2.6f, 3.4f))), YawOf(road), new Vector2(2.2f, 1.1f), 1.5f, 0.2f, 0.45f));
            foreach (float s in new[] { -1f, 1f }) PutRound(new PlaceSpec(PlacePiece.Torch, NextSeed(), PlaceSpec.Cm(r.Range(2.4f, 2.8f)), 1), W(road * Site.Radius * 0.7f + across * (s * 3f)), 0f, 0.35f, 0f, 0.2f, 0.45f);
            Tries(24, () => PutBox(new PlaceSpec(PlacePiece.Firewood, NextSeed(), PlaceSpec.Cm(r.Range(2.4f, 3.6f)), PlaceSpec.Cm(r.Range(1.1f, 1.6f))), W(CampSpot(r)), YawOf(Vector2.UnitY), new Vector2(2.2f, 1.4f), 1.6f, 0.3f, 0.4f));
            Tries(24, () => PutBox(new PlaceSpec(PlacePiece.Wagon, NextSeed(), 0, 1, PlaceSpec.Cm(r.Range(3.8f, 4.6f))), W(CampSpot(r)), YawOf(road) + r.Range(-0.3f, 0.3f), new Vector2(1.6f, 4f), 2.6f, 0.4f, 0.4f));
            // The slash the felling left, heaped among the stumps.
            int heaps = r.Int(3, 5);
            for (int k = 0; k < heaps; k++)
            {
                Tries(8, () =>
                {
                    float radius = r.Range(1.6f, 2.6f);
                    Vector2 local = new Vector2(r.Range(-Site.Radius * 0.8f, Site.Radius * 0.8f), -r.Range(Site.Radius * 0.3f, Site.Radius * 0.9f));
                    return PutRound(new PlaceSpec(PlacePiece.Slash, NextSeed(), With(new[] { PlaceSpec.Cm(radius) }, _wood)), W(local), r.Float() * MathF.Tau, radius * 0.8f, 0.9f, 0.2f, 0.5f);
                });
            }
            // Chips and bark: thick where the work is.
            for (int k = 0; k < 6; k++) Strew(LitterKind.Chips, new Vector2(r.Range(-Site.Radius * 0.6f, Site.Radius * 0.6f), r.Range(-Site.Radius * 0.7f, Site.Radius * 0.45f)), r.Range(2.2f, 3.6f), r.Int(24, 40));
            Strew(LitterKind.Embers, mess + new Vector2(0f, 2.5f), 1.4f, r.Int(6, 10));
        }

        // Anywhere in the camp's half of the clearing, for what has no one place it must be: a road through where it was wanted doesn't leave it out.
        private Vector2 CampSpot(WorldRandom r) => new Vector2(r.Range(-Site.Radius * 0.85f, Site.Radius * 0.85f), r.Range(-Site.Radius * 0.2f, Site.Radius * 0.85f));

        // The greycoats about the camp and the felling, the woodcutters who'll come back once it's free, and what's been taken in a chest by the fire.
        private void Hold(WorldRandom r)
        {
            Stand(SpawnRole.Raider, Around(W(new Vector2(0f, Site.Radius * 0.15f)), Site.Radius * 0.6f, r.Fork("camp"), 60), r.Int(4, 6), 4.5f, r.Fork("greycoats"));
            Stand(SpawnRole.Returning, Around(W(new Vector2(0f, -Site.Radius * 0.4f)), Site.Radius * 0.4f, r.Fork("felling"), 30), r.Int(1, 2), 5f, r.Fork("woodcutters"));
            Chest(W(new Vector2(r.Range(-4f, 4f), Site.Radius * 0.25f)), Site.Radius * 0.5f);
        }
    }
}
