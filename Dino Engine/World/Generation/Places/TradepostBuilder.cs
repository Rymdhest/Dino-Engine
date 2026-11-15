using Dino_Engine.Modelling.Procedural.Architecture.Places;
using Dino_Engine.Modelling.Procedural.Fence;
using Dino_Engine.World.Content;
using OpenTK.Mathematics;

namespace Dino_Engine.World.Generation.Places
{
    /// <summary>
    /// Lays out a trading post (PLACES.md) where the highway passes: a stockade with its gate where the road comes, the notice board outside it with the Reich's notices nailed over the land's;
    /// inside, the post's house in the land's own way of building and its storehouse, the beam balance in the yard, bales and sacks waiting for the Reich's wagons, the turncoats' tally desk by
    /// the house, the hitching rail by the gate, a cart, barrels and crates, and the turncoats' fire. The turncoats hold it, and once it's freed its trader comes back and trades again.
    /// </summary>
    internal sealed class TradepostBuilder : PlaceBuilder
    {
        private const float GateHalf = 2.8f;
        private Side _gateSide;
        private Vector2 _gateLocal;
        private Vector2 _house;

        private TradepostBuilder(SiteBuilder b) : base(b, "tradepost") { }

        public static void Build(SiteBuilder b) => new TradepostBuilder(b).Lay();

        private void Lay()
        {
            Plan.Yaw = MathF.PI + Random.Range(-0.2f, 0.2f);
            float hx = Site.Radius * Random.Range(0.62f, 0.72f), hz = Site.Radius * Random.Range(0.55f, 0.65f);
            Plan.Half = new Vector2(hx, hz);
            (_gateSide, _gateLocal) = GateOnRoad(Plan.Half);
            Paint(GroundPaint.Yard, Footprint.Box(Plan.Centre, Plan.Half - new Vector2(1f), Plan.Yaw), 4f);
            Enclose(FenceUse.Stockade, Plan.Half, _gateSide, _gateLocal, GateHalf);
            Buildings(Random.Fork("buildings"));
            Yard(Random.Fork("yard"));
            Outside(Random.Fork("outside"));
            Trade(Random.Fork("trade"));
            Hold(Random.Fork("holders"));
        }

        // The house across the yard from the gate, its door to it, and the storehouse beside it.
        private void Buildings(WorldRandom r)
        {
            Vector2 inward = -Outward(_gateSide);
            Vector2 across = new Vector2(-inward.Y, inward.X);
            Vector2 far = _gateLocal + inward * (Plan.Half.Length * 0.9f);
            // Back from the far side of the yard, clamped inside the stockade.
            Vector2 Inside(Vector2 local) => new Vector2(Math.Clamp(local.X, -Plan.Half.X + 7f, Plan.Half.X - 7f), Math.Clamp(local.Y, -Plan.Half.Y + 7f, Plan.Half.Y - 7f));
            _house = Inside(far - inward * 3f);
            Tries(10, () =>
            {
                Vector2 local = Inside(far - inward * r.Range(1f, 5f) + across * r.Range(-4f, 4f));
                if (!Structure(r.Chance(0.5f) ? StructureCatalog.Inn : StructureCatalog.House, W(local), YawOf(-inward), 0.8f, 0.36f)) return false;
                _house = local;
                return true;
            });
            foreach (float s in r.Chance(0.5f) ? new[] { 1f, -1f } : new[] { -1f, 1f })
            {
                if (Tries(6, () => Structure(StructureCatalog.Barn, W(Inside(_house + across * (s * r.Range(13f, 17f)) + inward * r.Range(0f, 4f))), YawOf(-across * s), 0.6f, 0.36f))) break;
            }
        }

        // The yard: the tally desk by the house, the balance in the middle, the bales and sacks waiting by the store, the rail by the gate, a cart, barrels, crates and the fire.
        private void Yard(WorldRandom r)
        {
            Vector2 inward = -Outward(_gateSide);
            Vector2 middle = Vector2.Zero;
            Vector2 Anywhere() => new Vector2(r.Range(-Plan.Half.X + 4f, Plan.Half.X - 4f), r.Range(-Plan.Half.Y + 4f, Plan.Half.Y - 4f));
            Tries(16, () => PutBox(new PlaceSpec(PlacePiece.Ledger, NextSeed()), W(r.Chance(0.6f) ? _house - inward * r.Range(7f, 10f) + new Vector2(r.Range(-5f, 5f), r.Range(-2f, 2f)) : Anywhere()), YawOf(-inward) + r.Range(-0.2f, 0.2f), new Vector2(1.6f, 1.4f), 2.6f, 0.4f, 0.45f));
            Tries(16, () => PutRound(new PlaceSpec(PlacePiece.Scales, NextSeed()), W(r.Chance(0.5f) ? middle + new Vector2(r.Range(-6f, 6f), r.Range(-6f, 6f)) : Anywhere()), r.Float() * MathF.Tau, 1.5f, 2.6f, 0.4f, 0.45f));
            int bales = r.Int(2, 3);
            for (int k = 0; k < bales; k++) Tries(12, () => PutBox(new PlaceSpec(PlacePiece.Bales, NextSeed(), r.Int(4, 8)), W(new Vector2(r.Range(-Plan.Half.X + 4f, Plan.Half.X - 4f), r.Range(-Plan.Half.Y + 4f, Plan.Half.Y - 4f))), r.Float() * MathF.Tau, new Vector2(3f, 1.1f), 1.8f, 0.4f, 0.4f));
            Tries(8, () => PutBox(new PlaceSpec(PlacePiece.HitchingRail, NextSeed(), PlaceSpec.Cm(r.Range(4f, 6f))), W(_gateLocal + inward * r.Range(4f, 6f) + new Vector2(-inward.Y, inward.X) * ((r.Chance(0.5f) ? 1f : -1f) * r.Range(5f, 8f))), YawOf(inward), new Vector2(3.2f, 0.9f), 1.2f, 0.4f, 0.4f));
            Tries(8, () => Structure(StructureCatalog.Cart, W(new Vector2(r.Range(-Plan.Half.X * 0.6f, Plan.Half.X * 0.6f), r.Range(-Plan.Half.Y * 0.6f, Plan.Half.Y * 0.6f))), r.Float() * MathF.Tau, 0.5f));
            for (int k = 0; k < 2; k++) Tries(6, () => Structure(StructureCatalog.Sacks, W(new Vector2(r.Range(-Plan.Half.X + 4f, Plan.Half.X - 4f), r.Range(-Plan.Half.Y + 4f, Plan.Half.Y - 4f))), r.Float() * MathF.Tau, 0.3f));
            for (int k = 0; k < 2; k++) Tries(8, () => PutBox(new PlaceSpec(PlacePiece.Barrels, NextSeed(), r.Int(2, 5)), W(new Vector2(r.Range(-Plan.Half.X + 3f, Plan.Half.X - 3f), r.Range(-Plan.Half.Y + 3f, Plan.Half.Y - 3f))), r.Float() * MathF.Tau, new Vector2(1.6f), 1.4f, 0.4f, 0.4f));
            Tries(8, () => PutBox(new PlaceSpec(PlacePiece.Crates, NextSeed(), r.Int(3, 5), 0), W(new Vector2(r.Range(-Plan.Half.X + 3f, Plan.Half.X - 3f), r.Range(-Plan.Half.Y + 3f, Plan.Half.Y - 3f))), r.Float() * MathF.Tau, new Vector2(2.2f, 1.3f), 1.8f, 0.4f, 0.4f));
            Tries(8, () => PutRound(new PlaceSpec(PlacePiece.Campfire, NextSeed()), W(new Vector2(r.Range(-6f, 6f), r.Range(-6f, 6f))), 0f, 1.4f, 0.8f, 0.5f, 0.4f));
            for (int k = 0; k < 2; k++) Tries(6, () => PutRound(new PlaceSpec(PlacePiece.Lantern, NextSeed(), PlaceSpec.Cm(r.Range(3.6f, 4.1f)), 0), W(new Vector2(r.Range(-Plan.Half.X + 3f, Plan.Half.X - 3f), r.Range(-Plan.Half.Y + 3f, Plan.Half.Y - 3f))), 0f, 0.4f, 0f, 0.3f, 0.5f));
        }

        // Outside the gate, the board of notices, facing whoever comes up the road.
        private void Outside(WorldRandom r)
        {
            Vector2 outward = Outward(_gateSide);
            Vector2 across = new Vector2(-outward.Y, outward.X);
            Tries(8, () => PutBox(new PlaceSpec(PlacePiece.NoticeBoard, NextSeed()), W(_gateLocal + outward * r.Range(3f, 5f) + across * ((r.Chance(0.5f) ? 1f : -1f) * (GateHalf + r.Range(2f, 4f)))), YawOf(outward), new Vector2(1.5f, 0.7f), 2.8f, 0.3f, 0.45f));
        }

        // What a trading post is for, and what the turncoats have made of it (PLACES.md): its yard packed hard by carts and the ways worn from the gate to the door and the store, the land's
        // merchants' stalls standing empty along a side, the Reich's wagons by the bales waiting to take them, sacks in a line to the scales, the turncoats' table, woodpile and rack of
        // the land's weapons they turned, their torches at the gate, and outside it, where the roads meet, the signpost with the Reich's names nailed over the land's and its gibbet.
        private void Trade(WorldRandom r)
        {
            Vector2 inward = -Outward(_gateSide);
            Vector2 across = new Vector2(-inward.Y, inward.X);
            Paint(GroundPaint.Plaza, Footprint.Box(Plan.Centre, Plan.Half - new Vector2(2.5f), Plan.Yaw), 3f);
            Trail(_gateLocal, _house - inward * 4f, 3.2f, GroundPaint.Plaza, 1.4f);
            // The land's merchants' stalls along the side of the yard, empty.
            float side = r.Chance(0.5f) ? 1f : -1f;
            int stalls = r.Int(2, 3);
            for (int k = 0; k < stalls; k++)
            {
                Tries(6, () => Structure(StructureCatalog.MarketStall, W(_gateLocal + inward * r.Range(5f, 9f + k * 4.5f) + across * (side * r.Range(Plan.Half.X * 0.45f, Plan.Half.X * 0.7f))), YawOf(-across * side), 0.4f, 0.4f));
            }
            // The Reich's wagons waiting by the bales for what's been taken.
            for (int k = 0; k < 2; k++) Tries(10, () => PutBox(new PlaceSpec(PlacePiece.Wagon, NextSeed(), 0, 1, PlaceSpec.Cm(r.Range(3.8f, 4.6f))), W(_gateLocal + inward * r.Range(6f, 12f) + across * (-side * r.Range(4f, 9f))), YawOf(-inward) + r.Range(-0.25f, 0.25f), new Vector2(1.6f, 4f), 2.6f, 0.4f, 0.4f));
            // A line of sacks waiting for the scales.
            for (int k = 0; k < 2; k++) Tries(6, () => Structure(StructureCatalog.Sacks, W(new Vector2(r.Range(-6f, 6f), r.Range(-4f, 4f))), r.Float() * MathF.Tau, 0.3f));
            Tries(8, () => PutBox(new PlaceSpec(PlacePiece.Bales, NextSeed(), r.Int(4, 7)), W(_gateLocal + inward * r.Range(9f, 13f) + across * (-side * r.Range(2f, 6f))), r.Float() * MathF.Tau, new Vector2(3f, 1.1f), 1.8f, 0.4f, 0.4f));
            // The turncoats' corner by the house.
            Vector2 corner = _house - inward * r.Range(6f, 8f) + across * (side * r.Range(6f, 9f));
            Tries(8, () => PutBox(new PlaceSpec(PlacePiece.MessTable, NextSeed(), PlaceSpec.Cm(r.Range(2.4f, 3.2f))), W(corner), YawOf(across), new Vector2(2.2f, 1.3f), 1.2f, 0.3f, 0.4f));
            Tries(8, () => PutBox(new PlaceSpec(PlacePiece.Firewood, NextSeed(), PlaceSpec.Cm(r.Range(2.4f, 3.4f)), PlaceSpec.Cm(r.Range(1.1f, 1.5f))), W(corner + across * side * r.Range(3f, 4.5f)), YawOf(-across * side), new Vector2(2.2f, 1.4f), 1.6f, 0.3f, 0.4f));
            Tries(8, () => PutBox(new PlaceSpec(PlacePiece.WeaponRack, NextSeed(), PlaceSpec.Cm(r.Range(2f, 2.6f)), 0), W(corner - inward * r.Range(2.5f, 3.5f)), YawOf(inward), new Vector2(1.5f, 0.6f), 2f, 0.3f, 0.4f));
            foreach (float s in new[] { -1f, 1f }) PutRound(new PlaceSpec(PlacePiece.Torch, NextSeed(), PlaceSpec.Cm(r.Range(2.4f, 2.8f)), 0), W(_gateLocal + inward * 1.8f + across * (s * (GateHalf + 0.9f))), 0f, 0.35f, 0f, 0.2f, 0.45f);
            // Outside, where the roads meet: the signpost and the gibbet.
            Vector2 outward = Outward(_gateSide);
            Tries(8, () => PutRound(new PlaceSpec(PlacePiece.Signpost, NextSeed(), r.Int(2, 4), 1), W(_gateLocal + outward * r.Range(5f, 7f) + across * ((r.Chance(0.5f) ? 1f : -1f) * r.Range(4f, 6f))), r.Float() * MathF.Tau, 0.35f, 0f, 0.2f, 0.45f));
            Tries(8, () => PutRound(new PlaceSpec(PlacePiece.Gibbet, NextSeed(), PlaceSpec.Cm(r.Range(4.2f, 5f))), W(_gateLocal + outward * r.Range(6f, 9f) + across * ((r.Chance(0.5f) ? 1f : -1f) * r.Range(6f, 9f))), r.Float() * MathF.Tau, 0.7f, Tall, 0.3f, 0.45f));
            for (int k = 0; k < 2; k++) LayFlat(new PlaceSpec(PlacePiece.Puddle, NextSeed(), PlaceSpec.Cm(r.Range(0.7f, 1.2f))), _gateLocal + inward * r.Range(2f, 6f) + across * r.Range(-2f, 2f), YawOf(inward), 1f, 0.15f);
            Strew(LitterKind.Papers, _house - inward * r.Range(6f, 9f), 2.4f, r.Int(6, 10));
            Strew(LitterKind.Straw, _gateLocal + inward * r.Range(5f, 9f) + across * r.Range(-4f, 4f), 2.6f, r.Int(14, 22));
            Strew(LitterKind.Papers, _gateLocal + outward * 4f, 1.6f, r.Int(4, 7));
        }

        // The turncoats about the yard and at the gate, the trader who comes back once it's free, a porter of the post's, and the chest by the house.
        private void Hold(WorldRandom r)
        {
            Vector2 inward = -Outward(_gateSide);
            Stand(SpawnRole.Raider, Around(Plan.Centre, Plan.Half.Length * 0.55f, r.Fork("yard"), 60), r.Int(4, 6), 4.5f, r.Fork("turncoats"));
            Stand(SpawnRole.Raider, Around(W(_gateLocal + inward * 4f), 5f, r.Fork("gate"), 20), 2, 3.5f, r.Fork("gatemen"));
            // Its trader, who has to have somewhere to stand, by the house.
            StandNear(SpawnRole.Trader, W(_house - inward * 6.5f), Plan.Half.Length);
            Stand(SpawnRole.Returning, Around(Plan.Centre, Plan.Half.Length * 0.4f, r.Fork("porters"), 30), r.Int(1, 2), 4f, r.Fork("porter stand"));
            Chest(W(_house - inward * 5f + new Vector2(-inward.Y, inward.X) * 3f), Site.Radius * 0.4f);
        }
    }
}
