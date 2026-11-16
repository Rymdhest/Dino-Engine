using Dino_Engine.Modelling.Procedural.Architecture.Places;
using Dino_Engine.Modelling.Procedural.Flora;
using Dino_Engine.World.Content;
using OpenTK.Mathematics;

namespace Dino_Engine.World.Generation.Places
{
    /// <summary>
    /// Lays out ruins (PLACES.md) of the waygates' builders: the hall they raised, its back wall still standing high towards the camera and its walls broken lower towards the front, a doorway in
    /// it and windows; the colonnade along its front, some columns standing to where they broke and others lying where they fell; the arch of its old gateway; the steps up to it; the rune
    /// stones and what's left of its figures lying about; and the land growing back over it, shrubs among the stones and a tree inside the walls. The dead walk there, or the Reich's diggers.
    /// </summary>
    internal sealed class RuinsBuilder : PlaceBuilder
    {
        private int _shade, _warm;
        private float _hx, _hz;

        private RuinsBuilder(SiteBuilder b) : base(b, "ruins") { }

        public static void Build(SiteBuilder b) => new RuinsBuilder(b).Lay();

        private void Lay()
        {
            _shade = Random.Int(900, 1060);
            _warm = Random.Int(-25, 25);
            Plan.Yaw = MathF.PI + Random.Range(-0.3f, 0.3f);
            _hx = Random.Range(8f, 11.5f);
            _hz = Random.Range(6f, 8.5f);
            Plan.Half = new Vector2(_hx + 6f, _hz + 9f);
            Hall(Random.Fork("hall"));
            Colonnade(Random.Fork("colonnade"));
            Fragments(Random.Fork("fragments"));
            Overgrowth(Random.Fork("overgrowth"));
            Fallen(Random.Fork("fallen"));
            Hold(Random.Fork("holders"));
        }

        private int[] Stone(params int[] numbers) => numbers.Concat(new[] { _shade, _warm }).ToArray();

        // The hall: its walls in stretches round its box, the back standing highest and the front lowest, a doorway in the front and windows in the back and sides.
        private void Hall(WorldRandom r)
        {
            Vector2[] corners = { new(-_hx, -_hz), new(_hx, -_hz), new(_hx, _hz), new(-_hx, _hz) };
            // Back, right, front, left: how high each stands, as the camera looks at the back from the front.
            float[] high = { r.Range(4.2f, 5.8f), r.Range(2.4f, 4.4f), r.Range(0.8f, 1.7f), r.Range(2.4f, 4.4f) };
            int doorSide = 2;
            for (int side = 0; side < 4; side++)
            {
                Vector2 a = corners[side], c = corners[(side + 1) % 4];
                float length = (c - a).Length;
                Vector2 along = (c - a) / length;
                int stretches = Math.Max(2, (int)MathF.Round(length / r.Range(3.6f, 5f)));
                float each = length / stretches;
                for (int k = 0; k < stretches; k++)
                {
                    // Some stretches are gone to their footings, never at a corner.
                    if (k > 0 && k < stretches - 1 && r.Chance(side == 2 ? 0.35f : 0.18f)) continue;
                    Vector2 mid = a + along * (each * (k + 0.5f));
                    // Lower towards the front along the sides.
                    float t = side == 1 ? (k + 0.5f) / stretches : side == 3 ? 1f - (k + 0.5f) / stretches : 0f;
                    float height = side is 1 or 3 ? MathHelper.Lerp(high[side], high[2], t) : high[side];
                    int gap = side == doorSide && k == stretches / 2 ? 1 : (side != doorSide && r.Chance(0.3f) && height > 2.4f ? 2 : 0);
                    float yaw = YawOf(new Vector2(along.Y, -along.X));
                    Footprint f = Footprint.Box(W(mid), new Vector2(each * 0.5f, 0.5f), yaw);
                    if (!Ground(f, 0.5f) || !Clear(f, PlacementLayer.Built | PlacementLayer.Road, 0.2f)) continue;
                    // A doorway is walked through: its wall is solid either side of it only. Each stretch's solid stops short of its ends, so that it never touches the next one's,
                    // or the one round a corner, with a gap between them nobody can get through.
                    const float Short = 0.8f;
                    bool solid = height >= Hitboxes.StepOver;   // What's down to its footings is stepped over.
                    if (gap == 1)
                    {
                        Vector2 wa = W(mid - along * (each * 0.5f - Short)), wb = W(mid - along * 0.85f), wc = W(mid + along * 0.85f), wd = W(mid + along * (each * 0.5f - Short));
                        World.Placement.Add(Footprint.Segment(wa, wb, 0.45f), PlacementLayer.Structure, solid, height);
                        World.Placement.Add(Footprint.Segment(wc, wd, 0.45f), PlacementLayer.Structure, solid, height);
                    }
                    else World.Placement.Add(Footprint.Segment(W(mid - along * (each * 0.5f - Short)), W(mid + along * (each * 0.5f - Short)), 0.45f), PlacementLayer.Structure, solid, height);
                    World.Pending.Add(new PendingObject(new PlaceSpec(PlacePiece.RuinWall, NextSeed(), Stone(PlaceSpec.Cm(each + 0.02f), PlaceSpec.Cm(height), gap)).Id, W(mid), yaw, Vector3.One, null));
                }
            }
            // The steps up to its doorway, from the front.
            if (r.Chance(0.6f))
            {
                float width = r.Range(3f, 4.5f);
                Tries(4, () => PutBox(new PlaceSpec(PlacePiece.RuinSteps, NextSeed(), Stone(PlaceSpec.Cm(width), PlaceSpec.Cm(r.Range(0.7f, 1.1f)))), W(new Vector2(0f, _hz + 2.4f)), YawOf(Vector2.UnitY), new Vector2(width * 0.5f + 0.2f, 1.8f), 1.1f, 0.2f, 0.4f));
            }
        }

        // The colonnade along the front, before the steps, and the old gateway's arch beyond it.
        private void Colonnade(WorldRandom r)
        {
            int columns = r.Int(5, 8);
            float z = _hz + r.Range(5f, 6.5f);
            float spacing = _hx * 2f / (columns - 1);
            bool fluted = r.Chance(0.5f);
            for (int k = 0; k < columns; k++)
            {
                Vector2 local = new Vector2(-_hx + k * spacing, z);
                if (Math.Abs(local.X) < 2.4f) continue;
                float roll = r.Float();
                if (roll < 0.25f)
                {
                    // Fallen, lying out from where it stood.
                    float length = r.Range(3.5f, 6f);
                    Vector2 way = new Vector2(r.Range(-0.6f, 0.6f), 1f).Normalized();
                    Vector2 middle = local + way * (length * 0.5f + 0.6f);
                    PutBox(new PlaceSpec(PlacePiece.FallenColumn, NextSeed(), Stone(PlaceSpec.Cm(length))), W(middle), YawOf(way) - MathF.PI * 0.5f, new Vector2(length * 0.5f + 0.9f, 0.7f), 0.8f, 0.1f, 0.45f);
                }
                else
                {
                    float height = roll > 0.85f ? r.Range(5.6f, 6.4f) : r.Range(1.2f, 4.6f);
                    PutRound(new PlaceSpec(PlacePiece.RuinColumn, NextSeed(), Stone(PlaceSpec.Cm(height), fluted ? 1 : 0)), W(local), r.Float() * MathF.Tau, 0.65f, height, 0.1f, 0.45f);
                }
            }
            // The gateway's arch, out in front, facing the way in.
            float span = r.Range(3f, 4.6f);
            Tries(6, () => PutBox(new PlaceSpec(PlacePiece.RuinArch, NextSeed(), Stone(PlaceSpec.Cm(span))), W(new Vector2(r.Range(-3f, 3f), z + r.Range(5f, 8f))), YawOf(Vector2.UnitY) + r.Range(-0.08f, 0.08f), new Vector2(span * 0.5f + 0.8f, 0.6f), 4f, 0.2f, 0.4f));
        }

        // Rune stones and what's left of the figures, lying about the hall.
        private void Fragments(WorldRandom r)
        {
            int runes = r.Int(2, 3);
            for (int k = 0; k < runes; k++) Tries(8, () => PutBox(new PlaceSpec(PlacePiece.RuneStone, NextSeed(), Stone()), W(Somewhere(r)), r.Float() * MathF.Tau, new Vector2(1.4f, 0.9f), 0.9f, 0.2f, 0.45f));
            if (r.Chance(0.75f)) Tries(8, () => PutBox(new PlaceSpec(PlacePiece.Statue, NextSeed(), Stone(r.Chance(0.55f) ? 0 : 1)), W(Somewhere(r)), r.Float() * MathF.Tau, new Vector2(1.4f, 1.2f), 1.4f, 0.2f, 0.45f));
        }

        // Somewhere about the hall, inside it or round it.
        private Vector2 Somewhere(WorldRandom r) => new Vector2(r.Range(-_hx - 4f, _hx + 4f), r.Range(-_hz - 3f, _hz + 4f));

        // The land growing back over it: shrubs among the stones, and a tree inside the walls, older than anyone remembers it standing.
        private void Overgrowth(WorldRandom r)
        {
            Species[] shrubs = Biome.Shrubs;
            if (shrubs.Length > 0)
            {
                int count = r.Int(4, 8);
                for (int k = 0; k < count; k++)
                {
                    Species shrub = shrubs[r.Int(shrubs.Length)];
                    Plant(shrub.Asset, W(Somewhere(r)), shrub.Asset.Scale.Lerp(r.Float()), r.Float() * MathF.Tau);
                }
            }
            if (r.Chance(0.6f) && CanopyTree() is PlantSpecies tree)
            {
                // Grown up against the back wall, as one seeded there in its lee would have, its crown over the wall rather than over the floor the camera wants to see.
                Tries(6, () => Plant(tree.Asset, W(new Vector2((r.Chance(0.5f) ? 1f : -1f) * r.Range(_hx * 0.35f, _hx - 3f), r.Range(-_hz + 2.5f, -_hz * 0.35f))), tree.Scale.Lerp(r.Range(0.5f, 0.85f)), r.Float() * MathF.Tau));
            }
        }

        // What the old hall's floor and its fall left (PLACES.md): the old roads' slabs still showing through inside it, rubble and spalls of its stone about every wall and column, and
        // whoever holds it now: the Reich's diggers' camp at its foundations, their tents, wagon, crates and cressets, or the Ossuary's dead among bones and its cold lamps.
        private void Fallen(WorldRandom r)
        {
            (int R, int G, int B) stone = RuinStone();
            Paint(GroundPaint.Paving, Footprint.Box(W(new Vector2(0f, -_hz * 0.15f)), new Vector2(_hx * 0.8f, _hz * 0.7f), Plan.Yaw), 2.5f);
            for (int k = 0; k < 8; k++) Strew(LitterKind.Rubble, new Vector2(r.Range(-_hx - 3f, _hx + 3f), r.Range(-_hz - 3f, _hz + 6f)), r.Range(1.6f, 2.6f), r.Int(6, 12), stone);
            for (int k = 0; k < 3; k++) Strew(LitterKind.Spalls, new Vector2(r.Range(-_hx, _hx), r.Range(-_hz, _hz)), 2f, r.Int(12, 20), stone);
            bool diggers = !Random.Fork("holders").Chance(0.5f);
            if (diggers)
            {
                float side = r.Chance(0.5f) ? 1f : -1f;
                Vector2 camp = new Vector2(side * (_hx + r.Range(4f, 6f)), r.Range(0f, _hz + 4f));
                Tries(10, () => PutBox(new PlaceSpec(PlacePiece.Tent, NextSeed(), PlaceSpec.Cm(r.Range(4.2f, 5f)), 1), W(camp), YawOf(new Vector2(-side, 0f)), new Vector2(3.8f, 3.4f), 3.4f, 0.4f, 0.45f));
                Tries(10, () => PutBox(new PlaceSpec(PlacePiece.Wagon, NextSeed(), 0, 1, PlaceSpec.Cm(r.Range(3.8f, 4.4f))), W(camp + new Vector2(0f, r.Range(5f, 7f))), YawOf(Vector2.UnitY) + r.Range(-0.4f, 0.4f), new Vector2(1.6f, 4f), 2.6f, 0.4f, 0.45f));
                Tries(8, () => PutBox(new PlaceSpec(PlacePiece.Crates, NextSeed(), r.Int(2, 4), 1), W(camp + new Vector2(-side * r.Range(2f, 4f), r.Range(-3f, -1f))), r.Float() * MathF.Tau, new Vector2(1.8f, 1.1f), 1.6f, 0.3f, 0.45f));
                Tries(8, () => PutRound(new PlaceSpec(PlacePiece.Brazier, NextSeed(), PlaceSpec.Cm(r.Range(1f, 1.2f))), W(camp + new Vector2(-side * r.Range(3f, 4f), r.Range(1f, 3f))), 0f, 0.8f, 1.4f, 0.3f, 0.45f));
                for (int k = 0; k < 3; k++) PutRound(new PlaceSpec(PlacePiece.Torch, NextSeed(), PlaceSpec.Cm(r.Range(2.4f, 2.8f)), 1), W(new Vector2(r.Range(-_hx, _hx), _hz + r.Range(2f, 5f))), 0f, 0.35f, 0f, 0.2f, 0.45f);
                Trail(camp, new Vector2(0f, _hz + 2f), 2.4f, GroundPaint.Yard, 1.6f);
            }
            else
            {
                for (int k = 0; k < 4; k++) Strew(LitterKind.Bones, new Vector2(r.Range(-_hx, _hx), r.Range(-_hz, _hz + 4f)), r.Range(1.8f, 2.6f), r.Int(8, 14));
                for (int k = 0; k < 3; k++) PutRound(new PlaceSpec(PlacePiece.Torch, NextSeed(), PlaceSpec.Cm(r.Range(2.4f, 2.8f)), 4), W(new Vector2(r.Range(-_hx - 2f, _hx + 2f), r.Range(-_hz, _hz + 4f))), 0f, 0.35f, 0f, 0.2f, 0.45f);
            }
        }

        // The ruins' stone as rubble wants it: the waygates' grey at this place's shade and warmth.
        private (int R, int G, int B) RuinStone()
        {
            float shade = _shade / 1000f, warm = _warm / 1000f;
            var c = new OpenTK.Mathematics.Vector3(0.52f, 0.51f, 0.49f) * shade;
            return ((int)(c.X * (1f + warm) * 1000f), (int)(c.Y * 1000f), (int)(c.Z * (1f - warm) * 1000f));
        }

        // The dead the Ossuary raises among the old stones, or the Reich's diggers at its foundations, and the chest inside the hall.
        private void Hold(WorldRandom r)
        {
            SpawnRole holders = r.Chance(0.5f) ? SpawnRole.Undead : SpawnRole.Raider;
            Stand(holders, Around(Plan.Centre, Plan.Half.Length * 0.55f, r.Fork("about"), 60), r.Int(4, 6), 4.5f, r.Fork("stand"));
            Chest(W(new Vector2(r.Range(-_hx * 0.5f, _hx * 0.5f), r.Range(-_hz * 0.5f, 0f))), _hx);
        }
    }
}
