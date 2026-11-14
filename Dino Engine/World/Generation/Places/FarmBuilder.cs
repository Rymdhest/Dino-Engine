using Dino_Engine.Modelling.Procedural.Architecture.Places;
using Dino_Engine.Modelling.Procedural.Fence;
using Dino_Engine.World.Content;
using OpenTK.Mathematics;
using static Dino_Engine.World.Content.PlaceStory;

namespace Dino_Engine.World.Generation.Places
{
    /// <summary>
    /// Lays out a rearing farm (PLACES.md), a farm of the land's the Reich took to raise what it catches: round a yard of trodden mud, the barn in the land's own way of building and the
    /// hatching shed; round them the paddocks, each in the farm's own field fencing with its gate towards the yard, chained by the Reich, and in each a few of one kind of grazer, born free and caught, wandering
    /// it and grazing; hay racks and the cistern by the gates; the Reich's tower and its banners at the way in, its tents, its crates and its fire. Once it's freed every gate swings open and
    /// what was penned goes back to the wild (CaptiveSystem), and the farmers whose farm it was come back.
    /// </summary>
    internal sealed class FarmBuilder : PlaceBuilder
    {
        private const float GateHalf = 1.7f, GateHigh = 1.45f;
        private Vector2 _away;
        private Side _gateSide;
        private Vector2 _gateLocal;

        private FarmBuilder(SiteBuilder b) : base(b, "farm") { }

        public static void Build(SiteBuilder b) => new FarmBuilder(b).Lay();

        private void Lay()
        {
            Plan.Yaw = MathF.PI + Random.Range(-0.15f, 0.15f);
            Plan.Half = new Vector2(Site.Radius * 0.9f);
            (_gateSide, _gateLocal) = GateOnRoad(Plan.Half);
            Vector2 outward = Plan.Dir(Outward(_gateSide));
            _away = W(_gateLocal) + outward * 38f;
            for (int k = 0; k < 12; k++)
            {
                Vector2 tried = W(_gateLocal) + SiteBuilder.Direction(MathF.Atan2(outward.Y, outward.X) + (k % 2 == 0 ? 1f : -1f) * k * 0.2f) * 40f;
                if (World.InBounds(tried) && World.Reachable.Sample(tried) > 0.5f) { _away = tried; break; }
            }
            Paint(GroundPaint.Mud, Footprint.Circle(Site.Position, Site.Radius * 0.3f), 6f);
            Paint(GroundPaint.Yard, Footprint.Circle(Site.Position, Site.Radius * 0.5f), 8f);
            ReichGate(_gateSide, _gateLocal, GateHalf + 1.5f, Random.Fork("gate"));
            Keep(Footprint.Box(W(_gateLocal - Outward(_gateSide) * 6f), new Vector2(3f, 6f), YawOf(-Outward(_gateSide))));
            // The paddocks first, as they're what the farm is for, then what stands in the yard between them.
            Paddocks(Random.Fork("paddocks"));
            Yard(Random.Fork("yard"));
            Camp(Random.Fork("camp"));
            Hold(Random.Fork("holders"));
        }

        // The barn at the back of the yard, its doors to it, and the hatching shed along a side, open towards it.
        private void Yard(WorldRandom r)
        {
            Tries(10, () => Structure(StructureCatalog.Barn, W(new Vector2(r.Range(-6f, 6f), -Site.Radius * r.Range(0.18f, 0.3f))), YawOf(Vector2.UnitY) + r.Range(-0.1f, 0.1f), 0.8f, 0.36f));
            Tries(24, () =>
            {
                float length = r.Range(8f, 12f);
                Vector2 local = new Vector2(r.Range(-Site.Radius * 0.35f, Site.Radius * 0.35f), r.Range(-Site.Radius * 0.35f, Site.Radius * 0.35f));
                // Open towards the camera, which is towards the place's front, so the eggs and the braziers under its roof are seen.
                return PutBox(new PlaceSpec(PlacePiece.HatchingShed, NextSeed(), PlaceSpec.Cm(length)), W(local), SiteBuilder.YawFacing(-Vector2.UnitY) + r.Range(-0.15f, 0.15f), new Vector2(length * 0.5f + 1.4f, 2.7f), 2.8f, 0.4f, 0.35f);
            });
            Tries(10, () => PutBox(new PlaceSpec(PlacePiece.WaterTank, NextSeed()), W(new Vector2(r.Range(-8f, 8f), r.Range(-2f, 8f))), r.Float() * MathF.Tau, new Vector2(2.6f, 2.8f), 3.5f, 0.4f, 0.4f));
        }

        // The paddocks round the yard, each fenced in the Reich's pens with its gate to the yard, its rack of hay, and a few of one kind it's raising.
        private void Paddocks(WorldRandom r)
        {
            int wanted = r.Int(2, 4);
            float start = r.Float() * MathF.Tau;
            int made = 0;
            for (int k = 0; k < 18 && made < wanted; k++)
            {
                float a = start + k * MathF.Tau / 6f + r.Range(-0.25f, 0.25f);
                Vector2 dir = new Vector2(MathF.Cos(a), MathF.Sin(a));
                // Not across the way in.
                if (Vector2.Dot(dir, Outward(_gateSide)) > 0.6f) continue;
                // Smaller and nearer in, the more tries it's taken.
                float shrink = 1f - 0.03f * k;
                Vector2 half = new Vector2(r.Range(7f, 10f), r.Range(5.5f, 7.5f)) * shrink;
                Vector2 centre = dir * (Site.Radius * r.Range(0.42f, 0.56f) + half.Y * 0.4f);
                if (Paddock(centre, half, r.Fork(k))) made++;
            }
        }

        private bool Paddock(Vector2 local, Vector2 half, WorldRandom r)
        {
            // Its long side across the way to the yard, its gate in the middle of the side facing it.
            Vector2 toYard = (-local).Normalized();
            float yaw = YawOf(toYard);
            Vector2 at = W(local);
            Footprint whole = Footprint.Box(at, half + new Vector2(0.6f), yaw);
            if (!Ground(whole, 0.32f) || !Clear(whole, PlacementLayer.Built | PlacementLayer.Road, 0.8f)) return false;
            World.Placement.Add(Footprint.Box(at, half, yaw), PlacementLayer.Keepout);
            Paint(GroundPaint.Mud, Footprint.Box(at, half - new Vector2(1f), yaw), 2.5f);
            Vector2 ax = new Vector2(MathF.Cos(yaw), -MathF.Sin(yaw)), az = new Vector2(MathF.Sin(yaw), MathF.Cos(yaw));
            Vector2 c00 = at - ax * half.X - az * half.Y, c10 = at + ax * half.X - az * half.Y, c11 = at + ax * half.X + az * half.Y, c01 = at - ax * half.X + az * half.Y;
            // The farm's own fence round it, low enough that what's in it shows, broken for the gate on the side towards the yard (+z, its own front).
            B.Wall(FenceUse.Field, c00, c10, at);
            B.Wall(FenceUse.Field, c10, c11, at);
            B.Wall(FenceUse.Field, c01, c00, at);
            Vector2 gate = at + az * half.Y;
            B.Wall(FenceUse.Field, c11, gate + ax * GateHalf, at);
            B.Wall(FenceUse.Field, gate - ax * GateHalf, c01, at);
            // The gate, shut and solid while it's held, swinging out once it's freed.
            World.Placement.Add(Footprint.Segment(gate - ax * GateHalf, gate + ax * GateHalf, 0.2f), PlacementLayer.Prop, solid: true, height: GateHigh);
            string door = new PlaceSpec(PlacePiece.FarmGate, (uint)(GateHalf * 200f) * 7u + (uint)(GateHigh * 100f), PlaceSpec.Cm(GateHalf * 2f), PlaceSpec.Cm(GateHigh)).Id;
            Vector2 hinge = gate - ax * GateHalf;
            Vector2 inside = gate - az * 2.2f, outside = gate + az * 3.2f;
            Keep(Footprint.Box(outside - az * 0.6f, new Vector2(GateHalf + 0.6f, 2.2f), yaw));
            // A rack of hay inside, along its back.
            Vector2 rack = at - az * (half.Y - 1.8f) + ax * r.Range(-half.X * 0.4f, half.X * 0.4f);
            Put(new PlaceSpec(PlacePiece.FeedRack, NextSeed(), PlaceSpec.Cm(r.Range(3f, 4.5f))), rack, yaw, Footprint.Box(rack, new Vector2(2.4f, 1.8f), yaw), 1.9f, PlacementLayer.Prop, 0.45f, 0.2f, PlacementLayer.Structure | PlacementLayer.Prop | PlacementLayer.Road);
            // What it's raising: a few of one kind, standing about in it.
            var (look, variants, scale, _) = Captives[Pick(r, (0, 3f), (3, 2.5f), (4, 2.5f), (1, 1.2f), (2, 1f))];
            int herd = r.Int(2, 3);
            int paddock = Plan.Paddocks.Count;
            Plan.Paddocks.Add(new PlacePaddock(at, half, yaw, gate, look, scale));
            int stood = 0;
            for (int k = 0; k < herd * 4 && stood < herd; k++)
            {
                Vector2 spot = at + ax * r.Range(-half.X + 2.5f, half.X - 2.5f) + az * r.Range(-half.Y + 2.8f, half.Y - 2.2f);
                if (Site.Spawns.Any(s => (s.Position - spot).Length < 4f)) continue;
                string model = look + r.Int(variants);
                Plan.Cages.Add(new PlaceCage(hinge, yaw, stood == 0 ? door : "", -1.75f, inside, spot, r.Float() * MathF.Tau, model, scale * r.Range(0.9f, 1.05f), outside, _away));
                B.Spawn(SpawnRole.Captive, spot);
                stood++;
            }
            if (stood == 0) Plan.Cages.Add(new PlaceCage(hinge, yaw, door, -1.75f, inside, inside, yaw, look + "0", scale, outside, _away));
            // A trough by the gate inside, and lanterns either side of it outside.
            Structure(StructureCatalog.FeedTrough, gate - az * 1.4f + ax * (GateHalf + 1.8f), yaw + MathF.PI * 0.5f, 0.2f, 0.45f);
            return true;
        }

        // The Reich's tower by the way in, its tents along a side, its crates and its fire in the yard, and its lanterns.
        private void Camp(WorldRandom r)
        {
            Vector2 inward = -Outward(_gateSide);
            Vector2 across = new Vector2(-inward.Y, inward.X);
            foreach (float s in r.Chance(0.5f) ? new[] { 1f, -1f } : new[] { -1f, 1f })
            {
                if (Tries(5, () => Structure(StructureCatalog.GuardTower, W(_gateLocal + inward * r.Range(5f, 8f) + across * (s * r.Range(6f, 9f))), YawOf(-inward), 0.4f))) break;
            }
            int tents = r.Int(1, 2);
            for (int k = 0; k < tents; k++) Tries(10, () => PutBox(new PlaceSpec(PlacePiece.Tent, NextSeed(), PlaceSpec.Cm(r.Range(4.4f, 5.4f)), 1), W(new Vector2(r.Range(-Site.Radius * 0.4f, Site.Radius * 0.4f), r.Range(-Site.Radius * 0.4f, Site.Radius * 0.4f))), r.Float() * MathF.Tau, new Vector2(3.9f, 3.6f), 3.4f, 0.5f, 0.4f));
            for (int k = 0; k < 2; k++) Tries(8, () => PutBox(new PlaceSpec(PlacePiece.Crates, NextSeed(), r.Int(3, 5), 1), W(new Vector2(r.Range(-12f, 12f), r.Range(-10f, 10f))), r.Float() * MathF.Tau, new Vector2(2.2f, 1.3f), 1.8f, 0.4f, 0.4f));
            Tries(8, () => PutRound(new PlaceSpec(PlacePiece.Campfire, NextSeed()), W(new Vector2(r.Range(-7f, 7f), r.Range(-6f, 8f))), 0f, 1.4f, 0.8f, 0.5f, 0.4f));
            Tries(6, () => Structure(StructureCatalog.Cart, W(_gateLocal + inward * r.Range(6f, 10f) + across * r.Range(-4f, 4f)), r.Float() * MathF.Tau, 0.5f));
            for (int k = 0; k < 2; k++) Tries(6, () => PutRound(new PlaceSpec(PlacePiece.Lantern, NextSeed(), PlaceSpec.Cm(r.Range(3.8f, 4.3f)), 1), W(new Vector2(r.Range(-10f, 10f), r.Range(-10f, 10f))), 0f, 0.4f, 0f, 0.3f, 0.5f));
        }

        // The greycoats about the yard, the guards at the way in, the farmers who'll come back once it's free, and the chest in the yard.
        private void Hold(WorldRandom r)
        {
            Stand(SpawnRole.Raider, Around(Site.Position, Site.Radius * 0.45f, r.Fork("yard"), 60), r.Int(4, 6), 4.5f, r.Fork("greycoats"));
            Vector2 inward = -Outward(_gateSide);
            Stand(SpawnRole.Guard, Around(W(_gateLocal + inward * 5f), 6f, r.Fork("gate"), 30), 2, 3.5f, r.Fork("guards"));
            Stand(SpawnRole.Returning, Around(Site.Position, Site.Radius * 0.3f, r.Fork("farmers"), 30), r.Int(2, 3), 4f, r.Fork("farmer stand"));
            Chest(Site.Position + Plan.Az * -3f, Site.Radius * 0.4f);
        }
    }
}
