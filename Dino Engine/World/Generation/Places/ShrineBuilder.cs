using Dino_Engine.Modelling.Procedural.Architecture.Places;
using Dino_Engine.Modelling.Procedural.Flora;
using Dino_Engine.World.Content;
using OpenTK.Mathematics;

namespace Dino_Engine.World.Generation.Places
{
    /// <summary>
    /// Lays out a shrine of the land's (LORE.md: put out by the Reich, and kindled again by a Defender). Its altar stands on a paved forecourt with the shrine's hall behind it, on the
    /// north side so the camera looks into it, open to the altar, flags in the land's colours strung from its eaves; the path comes in from the road through the gateway, between tall
    /// lanterns of stone with more flags strung across it, past the basin whoever came washed at; the bell hangs in its frame to one side, the tree that's always been there stands behind
    /// with ribbons round its trunk, and little cairns stand where people stopped. The Reich holds it so that nobody can kindle it: its banner planted on the forecourt, its brazier and
    /// its rack of spears by the gate, its notices nailed up, the lanterns it knocked down, the offerings it kicked over, the bell it cut down, and what the fight for it left lying.
    /// </summary>
    internal sealed class ShrineBuilder : PlaceBuilder
    {
        private Vector2 _altar;         // Where the altar stands, in the place's frame.
        private Vector2 _hall;          // The middle of the hall's plinth.
        private Vector2 _hallHalf;      // Half the hall's size, eaves and all.
        private bool _hasHall;
        private float _eave;            // How high its eaves are.
        private Vector2 _gate;          // The gateway, on the precinct's edge.
        private Vector2 _in;            // From the gateway in.
        private (int R, int G, int B) _stone;
        private readonly List<(Vector2 At, float Top, float Side)> _lanterns = new();

        private ShrineBuilder(SiteBuilder b) : base(b, "shrine") { }

        public static void Build(SiteBuilder b) => new ShrineBuilder(b).Lay();

        private int[] Stone => new[] { _stone.R, _stone.G, _stone.B };

        private void Lay()
        {
            // Its hall to the north, open to the south, which is where the camera is.
            Plan.Yaw = MathF.PI + Random.Range(-0.18f, 0.18f);
            Plan.Half = new Vector2(Site.Radius * 0.88f);
            // Dressed from the land's own stone, a little paler for the hands that have worn it.
            (int r, int g, int bl) = LandStone;
            _stone = ((int)MathF.Min(r * 1.12f + 30f, 640f), (int)MathF.Min(g * 1.12f + 30f, 630f), (int)MathF.Min(bl * 1.12f + 26f, 610f));
            Altar(Random.Fork("altar"));
            Hall(Random.Fork("hall"));
            Approach(Random.Fork("approach"));
            Precinct(Random.Fork("precinct"));
            Flags(Random.Fork("flags"));
            Held(Random.Fork("held"));
            Hold(Random.Fork("holders"));
            Litter(Random.Fork("litter"));
        }

        // The altar first, as the shrine is nothing without it, a little south of the middle, the hall's room behind it.
        private void Altar(WorldRandom r)
        {
            Vector2 wanted = new Vector2(r.Range(-1.2f, 1.2f), Site.Radius * r.Range(0.08f, 0.16f));
            B.PlaceNear(StructureCatalog.Altar, W(wanted), YawOf(Vector2.UnitY), Site.Radius * 1.5f, out Vector2 at, padding: 0.5f);
            _altar = Plan.ToLocal(at);
            B.Spawn(SpawnRole.Altar, at + Dir(Vector2.UnitY) * 3f);
            Keep(Footprint.Circle(at + Dir(Vector2.UnitY) * 3f, 2.2f));
        }

        // The hall behind the altar, its front to it; as near as it'll go, or a little to either side, or smaller.
        private void Hall(WorldRandom r)
        {
            float width = r.Range(6.2f, 8.4f), depth = r.Range(4.4f, 5.8f);
            int roof = Pick(r, (0, 2f), (1, 1.2f), (2, 1.5f)), damage = Pick(r, (0, 0.6f), (1, 1.5f), (2, 1.2f));
            foreach (float shrink in new[] { 1f, 0.85f, 0.72f })
            {
                foreach (float side in new[] { 0f, -2.5f, 2.5f, -5f, 5f })
                {
                    foreach (float back in new[] { 0f, 1.5f })
                    {
                        float w = width * shrink, d = depth * shrink;
                        Vector2 local = new Vector2(_altar.X + side, _altar.Y - 2.9f - (d * 0.5f + 1.3f) - back);
                        var spec = new PlaceSpec(PlacePiece.ShrineHall, NextSeed(), PlaceSpec.Cm(w), PlaceSpec.Cm(d), roof, damage, _stone.R, _stone.G, _stone.B);
                        if (!PutBox(spec, W(local), YawOf(Vector2.UnitY), new Vector2(w * 0.5f + 0.8f, d * 0.5f + 0.8f), Tall, 0.4f, 0.32f)) continue;
                        _hall = local;
                        _hallHalf = new Vector2(w * 0.5f + 1.25f, d * 0.5f + 1.2f);
                        _hasHall = true;
                        _eave = 3.6f;
                        // The forecourt between it and the altar, and round the altar, laid in the old roads' slabs.
                        Paint(GroundPaint.Paving, Footprint.Box(W(new Vector2((_hall.X + _altar.X) * 0.5f, (_hall.Y + _altar.Y) * 0.5f + 1.5f)), new Vector2(_hallHalf.X + 1.2f, (_altar.Y - _hall.Y) * 0.5f + 4.2f), Plan.Yaw), 1.8f);
                        return;
                    }
                }
            }
            // No room for a hall: the forecourt is round the altar alone.
            _hall = _altar - new Vector2(0f, 6f);
            _hallHalf = new Vector2(3f, 2f);
            Paint(GroundPaint.Paving, Footprint.Circle(W(_altar), 5.5f), 1.8f);
        }

        // The way in: the gateway where the path from the road comes to the precinct's edge, the path paved from it to the forecourt, and lanterns either side of it.
        private void Approach(WorldRandom r)
        {
            Vector2 road = Plan.ToLocal(Site.Position + Site.RoadDirection);
            road = road.LengthSquared > 1e-4f ? road.Normalized() : Vector2.UnitY;
            // Never in behind the hall: a road from the north comes round to the side.
            if (road.Y < -0.25f) road = Vector2.Normalize(new Vector2(road.X >= 0f ? 1f : -1f, 0.15f));
            float reach = Site.Radius * 0.8f;
            bool placed = false;
            Vector2 gate = _altar + road * reach;
            var spec = new PlaceSpec(PlacePiece.ShrineGate, NextSeed(), PlaceSpec.Cm(r.Range(3f, 3.8f)), PlaceSpec.Cm(r.Range(3.4f, 4.2f)), _stone.R, _stone.G, _stone.B);
            // Where the road comes, or turned a little either way, a little further in each time.
            foreach (float turn in new[] { 0f, 0.35f, -0.35f, 0.7f, -0.7f })
            {
                Vector2 dir = new Vector2(road.X * MathF.Cos(turn) - road.Y * MathF.Sin(turn), road.X * MathF.Sin(turn) + road.Y * MathF.Cos(turn));
                if (dir.Y < -0.3f) continue;
                for (int k = 0; k < 5 && !placed; k++)
                {
                    Vector2 tried = _altar + dir * (reach - k * 1.3f);
                    float yaw = YawOf(dir);
                    // Only its posts are solid: the way through it is open.
                    Vector2 across = new Vector2(-dir.Y, dir.X);
                    float half = spec.Metres(0) * 0.5f + 0.22f;
                    Footprint left = Footprint.Circle(W(tried + across * half), 0.5f), right = Footprint.Circle(W(tried - across * half), 0.5f);
                    if (!Ground(left, 0.4f) || !Ground(right, 0.4f) || !Clear(left) || !Clear(right) || !Clear(Footprint.Box(W(tried), new Vector2(half, 0.6f), yaw), PlacementLayer.Built, 0.1f)) continue;
                    World.Placement.Add(left, PlacementLayer.Prop, solid: true, height: Tall);
                    World.Placement.Add(right, PlacementLayer.Prop, solid: true, height: Tall);
                    World.Pending.Add(new PendingObject(spec.Id, W(tried), yaw, Vector3.One, null));
                    gate = tried;
                    road = dir;
                    placed = true;
                }
                if (placed) break;
            }
            _gate = gate;
            _in = (_altar - gate).LengthSquared > 1f ? (_altar - gate).Normalized() : -road;
            // The path, paved, from the gateway to the forecourt, and worn on out past it to the road.
            Trail(gate, _altar + _in * -2.5f, r.Range(2.2f, 2.8f), GroundPaint.Paving, 1.2f);
            Trail(gate - _in * 1.5f, gate - _in * 6f, 2.2f, GroundPaint.Yard, 1.8f);
            Keep(Footprint.Box(W((gate + _altar) * 0.5f), new Vector2(1.3f, (gate - _altar).Length * 0.5f), YawOf(_in)));
            // Lanterns in pairs along it, the Reich having knocked down what it could.
            Vector2 side = new Vector2(-_in.Y, _in.X);
            float length = (_altar - gate).Length - 3f;
            float height = r.Range(2.3f, 2.9f);
            for (float t = 1.8f; t < length; t += r.Range(3.2f, 4.2f))
            {
                foreach (float s in new[] { -1f, 1f })
                {
                    Vector2 local = gate + _in * t + side * (s * r.Range(2.3f, 2.7f));
                    int form = Pick(r, (0, 1.6f), (1, 0.9f), (2, 0.6f));
                    float reachOver = form == 1 ? 1.2f : 0.55f;
                    if (!PutRound(new PlaceSpec(PlacePiece.ShrineLantern, NextSeed(), PlaceSpec.Cm(height), form, _stone.R, _stone.G, _stone.B), W(local), r.Float() * MathF.Tau, reachOver, form == 1 ? 0.6f : 3f, 0.25f, 0.4f)) continue;
                    // Standing, its firebox's head is what flags can be tied to.
                    if (form == 0) _lanterns.Add((local, height * 0.8f, s));
                }
            }
            // The basin just inside, where whoever came washed before going on.
            Tries(10, () => PutBox(new PlaceSpec(PlacePiece.Basin, NextSeed(), Stone), W(gate + _in * r.Range(2.5f, 4f) + side * ((r.Chance(0.5f) ? 1f : -1f) * r.Range(3.6f, 4.6f))), YawOf(-side) + r.Range(-0.2f, 0.2f), new Vector2(1f, 0.85f), 0.9f, 0.3f, 0.4f));
        }

        // What's always been there: the bell in its frame, the old tree behind the hall with its ribbons, cairns, a pair of lanterns by the altar, and the offerings beside it.
        private void Precinct(WorldRandom r)
        {
            float bellSide = r.Chance(0.5f) ? 1f : -1f;
            Tries(12, () => PutBox(new PlaceSpec(PlacePiece.Bell, NextSeed(), r.Chance(0.6f) ? 1 : 0), W(new Vector2(_altar.X + bellSide * r.Range(5.2f, 7f), _altar.Y + r.Range(-3.5f, 0f))), YawOf(Vector2.UnitY) + r.Range(-0.3f, 0.3f), new Vector2(1.5f, 1f), 3.4f, 0.3f, 0.4f));
            // The tree behind the hall, to the side away from the bell, so its crown stands over the roof from the camera and hides nothing.
            if (CanopyTree() is PlantSpecies tree)
            {
                Tries(14, () =>
                {
                    Vector2 local = new Vector2(_hall.X - bellSide * r.Range(_hallHalf.X * 0.2f, _hallHalf.X + 2f), _hall.Y - _hallHalf.Y - r.Range(1.8f, 4f));
                    float scale = tree.Scale.Lerp(r.Range(0.8f, 1f));
                    if (!Plant(tree.Asset, W(local), scale, r.Float() * MathF.Tau)) return false;
                    float trunk = MathF.Max(tree.Asset.SolidRadius * scale, 0.2f);
                    World.Pending.Add(new PendingObject(new PlaceSpec(PlacePiece.Ribbons, NextSeed(), PlaceSpec.Cm(trunk), PlaceSpec.Cm(r.Range(2.2f, 3f))).Id, W(local), r.Float() * MathF.Tau, Vector3.One, null));
                    // The grass kept under it, flowering still, and cairns left at its foot.
                    Paint(GroundPaint.Garden, Footprint.Circle(W(local), r.Range(2.8f, 3.6f)), 1.6f);
                    Lay(new PlaceSpec(PlacePiece.Cairns, NextSeed(), r.Int(3, 6), PlaceSpec.Cm(1.6f), _stone.R, _stone.G, _stone.B), W(local + new Vector2(0f, 2.4f)), r.Float() * MathF.Tau, 1.8f);
                    return true;
                });
            }
            // A pair of lanterns flanking the altar, at the forecourt's front corners.
            foreach (float s in new[] { -1f, 1f })
            {
                PutRound(new PlaceSpec(PlacePiece.ShrineLantern, NextSeed(), PlaceSpec.Cm(r.Range(2.6f, 3.1f)), r.Chance(0.3f) ? 2 : 0, _stone.R, _stone.G, _stone.B), W(_altar + new Vector2(s * r.Range(3.4f, 4.2f), r.Range(1.2f, 2.4f))), r.Float() * MathF.Tau, 0.55f, 3f, 0.25f, 0.4f);
            }
            // The offerings' table beside the altar, on the side away from the bell, mostly kicked over.
            Tries(10, () => PutBox(new PlaceSpec(PlacePiece.Offerings, NextSeed(), r.Chance(0.65f) ? 1 : 0, _stone.R, _stone.G, _stone.B), W(_altar + new Vector2(-bellSide * r.Range(3.6f, 4.8f), r.Range(-1.5f, 0.8f))), YawOf(Vector2.UnitY) + r.Range(-0.3f, 0.3f), new Vector2(1f, 0.55f), 0.8f, 0.2f, 0.4f));
            // Cairns left along the way.
            for (int k = 0; k < 2; k++)
            {
                Vector2 along = _gate + _in * r.Range(2f, 6f) + new Vector2(-_in.Y, _in.X) * ((r.Chance(0.5f) ? 1f : -1f) * r.Range(4f, 6f));
                Lay(new PlaceSpec(PlacePiece.Cairns, NextSeed(), r.Int(2, 4), PlaceSpec.Cm(1f), _stone.R, _stone.G, _stone.B), W(along), r.Float() * MathF.Tau, 1.2f);
            }
        }

        // Flags in the land's colours: from the hall's eaves out to poles at the forecourt's corners, and across the path between the lanterns still standing.
        private void Flags(WorldRandom r)
        {
            if (_hasHall)
            {
                foreach (float s in new[] { -1f, 1f })
                {
                    if (r.Chance(0.2f)) continue;
                    Vector2 from = _hall + new Vector2(s * (_hallHalf.X - 0.3f), _hallHalf.Y - 0.2f);
                    Vector2 to = _altar + new Vector2(s * r.Range(5.5f, 7.5f), r.Range(1.5f, 3.5f));
                    Flagline(from, to, _eave, r.Range(2.8f, 3.4f), r, poleAtEnd: true);
                }
            }
            // Across the path, between a standing lantern and the one opposite it.
            var left = _lanterns.Where(l => l.Side < 0f).ToList();
            var right = _lanterns.Where(l => l.Side > 0f).ToList();
            foreach (var a in left)
            {
                var b = right.OrderBy(o => (o.At - a.At).LengthSquared).FirstOrDefault();
                if (b.At == default || (b.At - a.At).Length > 7f || r.Chance(0.25f)) continue;
                Flagline(a.At, b.At, a.Top, b.Top, r, poleAtEnd: false);
            }
        }

        // A line of flags from one point of the frame to another, at those heights, sagging; nothing stands on it, so it's put down with no footprint.
        private void Flagline(Vector2 from, Vector2 to, float h0, float h1, WorldRandom r, bool poleAtEnd)
        {
            Vector2 d = to - from;
            float length = d.Length;
            if (length < 1.5f) return;
            if (poleAtEnd)
            {
                Footprint pole = Footprint.Circle(W(to), 0.3f);
                if (!Ground(pole, 0.45f) || !Clear(pole, PlacementLayer.Built | PlacementLayer.Road, 0.1f)) return;
                World.Placement.Add(pole, PlacementLayer.Prop, solid: false, height: 0f);
            }
            var spec = new PlaceSpec(PlacePiece.PrayerLine, NextSeed(), PlaceSpec.Cm(length), PlaceSpec.Cm(h0), PlaceSpec.Cm(h1), PlaceSpec.Cm(MathF.Min(0.9f, length * r.Range(0.05f, 0.09f))), poleAtEnd ? 1 : 0, 0);
            World.Pending.Add(new PendingObject(spec.Id, W(from), YawOf(d / length), Vector3.One, null));
        }

        // What the Reich has made of it: its banner planted on the forecourt, its brazier and spears by the gate, crates of what it took, and cressets of its own at the gate.
        private void Held(WorldRandom r)
        {
            Vector2 side = new Vector2(-_in.Y, _in.X);
            Tries(10, () => PutRound(new PlaceSpec(PlacePiece.Banner, NextSeed(), PlaceSpec.Cm(r.Range(5.6f, 6.6f)), 1), W(_altar + new Vector2((r.Chance(0.5f) ? 1f : -1f) * r.Range(4.4f, 6f), r.Range(-1.5f, 1.5f))), YawOf(Vector2.UnitY), 0.45f, 0f, 0.2f, 0.4f));
            Vector2 camp = _gate + _in * r.Range(4.5f, 6.5f) + side * ((r.Chance(0.5f) ? 1f : -1f) * r.Range(4f, 5.5f));
            Tries(10, () =>
            {
                Vector2 at = camp + new Vector2(r.Range(-1.5f, 1.5f), r.Range(-1.5f, 1.5f));
                if (!PutRound(new PlaceSpec(PlacePiece.Brazier, NextSeed(), PlaceSpec.Cm(r.Range(1f, 1.2f))), W(at), r.Float() * MathF.Tau, 0.8f, 1.4f, 0.4f, 0.4f)) return false;
                camp = at;
                return true;
            });
            Paint(GroundPaint.Yard, Footprint.Circle(W(camp), 3.2f), 2f);
            Tries(10, () => PutBox(new PlaceSpec(PlacePiece.WeaponRack, NextSeed(), PlaceSpec.Cm(r.Range(2f, 2.8f)), 1), W(camp + new Vector2(r.Range(-3f, 3f), r.Range(-3f, -1.8f))), YawOf(Vector2.UnitY) + r.Range(-0.3f, 0.3f), new Vector2(1.6f, 0.6f), 2f, 0.3f, 0.4f));
            Tries(8, () => PutBox(new PlaceSpec(PlacePiece.Crates, NextSeed(), r.Int(2, 4), 1), W(camp + new Vector2(r.Range(-3.5f, 3.5f), r.Range(1.5f, 3f))), r.Float() * MathF.Tau, new Vector2(1.8f, 1.1f), 1.6f, 0.3f, 0.4f));
            // A cresset either side of the gate, outside, so its holders see who comes up the road.
            foreach (float s in new[] { -1f, 1f })
            {
                PutRound(new PlaceSpec(PlacePiece.Torch, NextSeed(), PlaceSpec.Cm(r.Range(2.4f, 2.8f)), 1), W(_gate - _in * 1.6f + side * (s * r.Range(2.8f, 3.4f))), r.Float() * MathF.Tau, 0.35f, 0f, 0.2f, 0.45f);
            }
        }

        // Those it holds the shrine with, about the forecourt and the gate, and the chest of what they took by the hall.
        private void Hold(WorldRandom r)
        {
            Stand(SpawnRole.Raider, Around(W(_altar + _in * -1f), Site.Radius * 0.55f, r.Fork("forecourt"), 50), r.Int(3, 4), 3.5f, r.Fork("raiders"));
            Chest(W(_altar + new Vector2((r.Chance(0.5f) ? 1f : -1f) * r.Range(2.5f, 3.5f), -r.Range(1.5f, 2.4f))), Site.Radius * 0.5f);
        }

        // What lies about: the notices torn down by the gate, what the fight for it left on the forecourt, and the rubble of the lanterns knocked down.
        private void Litter(WorldRandom r)
        {
            Strew(LitterKind.Papers, _gate + _in * 1.5f, 1.8f, r.Int(5, 9));
            Strew(LitterKind.Gear, _altar + new Vector2(r.Range(-3f, 3f), r.Range(1f, 4f)), 3.2f, r.Int(6, 10));
            Strew(LitterKind.Gear, _gate + _in * r.Range(3f, 5f), 2.4f, r.Int(4, 7));
            Strew(LitterKind.Rubble, _gate + _in * r.Range(4f, 8f) + new Vector2(-_in.Y, _in.X) * 2.5f, 1.4f, r.Int(5, 9), _stone);
        }
    }
}
