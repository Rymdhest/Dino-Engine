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
                    // A doorway is walked through: its wall is solid either side of it only.
                    if (gap == 1)
                    {
                        Vector2 wa = W(mid - along * (each * 0.5f)), wb = W(mid - along * 0.85f), wc = W(mid + along * 0.85f), wd = W(mid + along * (each * 0.5f));
                        World.Placement.Add(Footprint.Segment(wa, wb, 0.45f), PlacementLayer.Structure, solid: true, height: height);
                        World.Placement.Add(Footprint.Segment(wc, wd, 0.45f), PlacementLayer.Structure, solid: true, height: height);
                    }
                    else World.Placement.Add(Footprint.Segment(W(mid - along * (each * 0.5f)), W(mid + along * (each * 0.5f)), 0.45f), PlacementLayer.Structure, solid: true, height: height);
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
                Tries(6, () => Plant(tree.Asset, W(new Vector2(r.Range(-_hx + 3f, _hx - 3f), r.Range(-_hz + 2.5f, _hz - 2.5f))), tree.Scale.Lerp(r.Range(0.6f, 1f)), r.Float() * MathF.Tau));
            }
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
