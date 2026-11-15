using Dino_Engine.Modelling.Procedural.Architecture.Places;
using Dino_Engine.World.Content;
using OpenTK.Mathematics;

namespace Dino_Engine.World.Generation.Places
{
    /// <summary>
    /// Lays out a ring of old stones (LORE.md: raised by no people anyone remembers, over bones nobody was meant to find; the stones hum faintly, like held breath). A ring of standing
    /// stones of the land's own rock round the altar stone, tallest to the north so the camera looks in over the low ones to the south, with stones laid across pairs of them on the
    /// north side, a few fallen or broken, and a way in between two taller stones where the road comes, an avenue of paired stones running out to it. Beyond the ring to the north is
    /// the barrow it was raised over, which the Ossuary has dug into in some lands. Its turf is kept short inside the ring, and worn where the cult walks. The cult that holds it, of
    /// whichever of the land's powers its country serves (<see cref="PlaceStory.CultOf"/>), has its heart by the altar, its totems between the stones and its torches at the way in,
    /// and what it offers lies at the altar's foot.
    /// </summary>
    internal sealed class StonesBuilder : PlaceBuilder
    {
        private Cult _cult;
        private (int R, int G, int B) _stone;
        private float _ring;
        private Vector2 _altar;
        private float _entrance;        // The angle of the way in, in the place's frame.
        private readonly List<Vector2> _stones = new();

        private StonesBuilder(SiteBuilder b) : base(b, "stones") { }

        public static void Build(SiteBuilder b) => new StonesBuilder(b).Lay();

        private void Lay()
        {
            Plan.Yaw = MathF.PI + Random.Range(-0.3f, 0.3f);
            Plan.Half = new Vector2(Site.Radius * 0.95f);
            _cult = PlaceStory.CultOf(Biome, Site.Story);
            // Old stones are the land's rock as it weathers, not fresh-cut: its own colour, a little lifted to read against the turf.
            Vector3 rock = World.Geology?.Colour ?? new Vector3(0.32f, 0.31f, 0.29f);
            // Pale enough to stand out of the turf from 33 m up, as a stone is mostly its top from there.
            Vector3 old = Vector3.Clamp(rock * 1.25f + new Vector3(0.12f), new Vector3(0.4f), new Vector3(0.64f));
            _stone = ((int)(old.X * 1000f), (int)(old.Y * 1000f), (int)(old.Z * 1000f));
            _ring = Site.Radius * Random.Range(0.54f, 0.6f);
            Vector2 road = Plan.ToLocal(Site.Position + Site.RoadDirection);
            road = road.LengthSquared > 1e-4f ? road.Normalized() : Vector2.UnitY;
            _entrance = MathF.Atan2(road.Y, road.X);
            Altar(Random.Fork("altar"));
            Ring(Random.Fork("ring"));
            Avenue(Random.Fork("avenue"));
            Barrow(Random.Fork("barrow"));
            CultOf(Random.Fork("cult"));
            Hold(Random.Fork("holders"));
            Litter(Random.Fork("litter"));
        }

        private int[] Stone => new[] { _stone.R, _stone.G, _stone.B };

        private static Vector2 At(float angle, float distance) => new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * distance;

        // The altar stone in the middle, which the hero kindles, and the turf kept short inside the ring.
        private void Altar(WorldRandom r)
        {
            B.PlaceNear(StructureCatalog.Altar, W(RingCentre()), YawOf(At(_entrance, 1f)), Site.Radius * 1.4f, out Vector2 at, padding: 0.4f);
            _altar = Plan.ToLocal(at);
            B.Spawn(SpawnRole.Altar, at + Dir(At(_entrance, 1f)) * 3f);
            Keep(Footprint.Circle(at + Dir(At(_entrance, 1f)) * 3f, 2f));
            Paint(GroundPaint.Sward, Footprint.Circle(W(_altar), _ring + 1.5f), 2.5f);
            // A ring worn inside the stones where the cult walks round, and the way from the gap to the altar.
            for (int k = 0; k < 14; k++)
            {
                float a0 = k * MathF.Tau / 14f, a1 = (k + 1) * MathF.Tau / 14f;
                Trail(_altar + At(a0, _ring - 2.6f), _altar + At(a1, _ring - 2.6f), 1.6f, GroundPaint.Plaza, 1.2f);
            }
            Trail(_altar + At(_entrance, _ring + 1f), _altar + At(_entrance, 2.4f), 2f, GroundPaint.Yard, 1.4f);
        }

        // Where the ring's middle goes: the site's middle, unless the ring round it would stand partly on a beach or off its hill, when it's moved to wherever nearby most of it
        // stands on good ground, as whoever raised it would have raised it there.
        private Vector2 RingCentre()
        {
            int Good(Vector2 centre)
            {
                int good = 0;
                for (int i = 0; i < 16; i++)
                {
                    if (Ground(Footprint.Circle(W(centre + At(i * MathF.Tau / 16f, _ring)), 2.2f), 0.45f)) good++;
                }
                return good;
            }
            Vector2 best = Vector2.Zero;
            int most = Good(best);
            if (most >= 15) return best;
            foreach (float distance in new[] { 3f, 6f, 9f })
            {
                for (int k = 0; k < 8; k++)
                {
                    Vector2 tried = At(k * MathF.Tau / 8f, distance);
                    int good = Good(tried);
                    if (good > most) { most = good; best = tried; }
                }
            }
            return best;
        }

        // The ring itself: tall to the north, low to the south, a way in where the road comes between two taller stones, lintels across pairs on the north side, and gaps where stones fell.
        private void Ring(WorldRandom r)
        {
            int count = r.Int(9, 13);
            float step = MathF.Tau / count;
            // A ring that would stand half on a beach or off the edge of its hill is drawn in until most of it stands on good ground, but never smaller than a ring.
            for (int shrink = 0; shrink < 3 && _ring > 8.5f; shrink++)
            {
                int good = 0;
                for (int i = 0; i < count; i++)
                {
                    if (Ground(Footprint.Circle(W(_altar + At(_entrance + step * (i + 0.5f), _ring)), 1.3f), 0.45f)) good++;
                }
                if (good >= count * 0.85f) break;
                _ring = MathF.Max(8.5f, _ring * 0.86f);
            }
            // Every gap between two stones wide enough to walk through, as anyone has to be able to get in to the altar from any side: a stone is never wider than its
            // share of the ring less a body's width and some.
            // A small ring, squeezed into a crowded land or drawn in off a beach, stands fewer stones rather than closing up: a body has to get between any two of them to the altar.
            count = Math.Min(count, Math.Max(7, (int)(MathF.Tau * _ring / 5.2f)));
            step = MathF.Tau / count;
            float widest = MathF.Max(1.2f, MathF.Tau * _ring / count - 3.4f);
            // Turn the ring so the way in falls between two stones.
            float start = _entrance + step * 0.5f;
            var heights = new float[count];
            int lintels = 0;
            for (int i = 0; i < count; i++)
            {
                float a = start + i * step + r.Range(-0.06f, 0.06f);
                Vector2 dir = At(a, 1f);
                // North in the place's frame is -z; the camera looks over the south's low stones at the north's tall ones.
                float north = MathF.Max(0f, -dir.Y);
                bool portal = i == 0 || i == count - 1;
                float high = portal ? r.Range(5.6f, 6.8f) : MathHelper.Lerp(r.Range(3f, 4.2f), r.Range(5.2f, 7.4f), north);
                float wide = MathF.Min(high * r.Range(0.38f, 0.5f) + 0.5f, widest);
                heights[i] = high;
                Vector2 local = _altar + dir * (_ring + r.Range(-0.4f, 0.4f));
                // A pair on the north arc with a stone across them, as the builders did where it would be seen from the middle.
                if (!portal && north > 0.6f && lintels < 2 && i + 1 < count - 1 && r.Chance(0.55f))
                {
                    float b2 = a + step;
                    Vector2 mid = _altar + At((a + b2) * 0.5f, _ring);
                    float span = (At(b2, _ring) - At(a, _ring)).Length - 1.6f;
                    var tri = new PlaceSpec(PlacePiece.Trilithon, NextSeed(), PlaceSpec.Cm(r.Range(5f, 6.4f)), PlaceSpec.Cm(MathF.Max(1.6f, span * 0.7f)), r.Chance(0.25f) ? 1 : 0, _stone.R, _stone.G, _stone.B);
                    // Its face turned in to the middle.
                    Vector2 inward = (_altar - mid).Normalized();
                    if (PutBox(tri, W(mid), YawOf(inward), new Vector2(span * 0.35f + 1.6f, 1f), Tall, 0.2f, 0.42f))
                    {
                        _stones.Add(mid);
                        lintels++;
                        i++;
                        continue;
                    }
                }
                // Some fell, or were broken, the most of them on the low south side where the ground took them.
                int form = portal ? 0 : Pick(r, (0, 3f), (1, 1.1f), (2, north < 0.3f ? 0.9f : 0.3f), (3, north < 0.3f ? 0.8f : 0.25f));
                float lean = PlaceSpec.Degrees(r.Range(-0.12f, 0.12f));
                var spec = new PlaceSpec(PlacePiece.Menhir, NextSeed(), PlaceSpec.Cm(high), PlaceSpec.Cm(wide), form, (int)lean, _stone.R, _stone.G, _stone.B);
                // Its broad face turned in to the middle, or along the ring's line as many were set.
                float yaw = YawOf((_altar - local).Normalized()) + (r.Chance(0.3f) ? MathF.PI * 0.5f : 0f);
                if (form == 3)
                {
                    // Lying where it fell, outwards: what it covers is low and walked over.
                    Vector2 laid = local + dir * (high * 0.35f);
                    if (Put(spec, W(local), yaw, Footprint.Box(W(laid), new Vector2(wide * 0.5f + 0.2f, high * 0.5f), YawOf(dir)), 0.9f, PlacementLayer.Prop, 0.45f, 0.2f)) _stones.Add(local);
                    continue;
                }
                // What it stands on is its slab, broad one way and thin the other, not the circle round it, which would close the ring.
                // Where the ground won't take it, a little further in or out: never round the ring, which would close a gap.
                for (int nudge = 0; nudge < 5; nudge++)
                {
                    Vector2 spot = nudge == 0 ? local : _altar + At(a, _ring + r.Range(-1.4f, 1.4f));
                    float turned = YawOf((_altar - spot).Normalized()) + (yaw - YawOf((_altar - local).Normalized()));
                    if (!Put(spec, W(spot), turned, Footprint.Box(W(spot), new Vector2(wide * 0.5f + 0.15f, wide * 0.4f + 0.15f), turned), form == 2 ? 2f : Tall, PlacementLayer.Prop, 0.45f, 0.2f)) continue;
                    _stones.Add(spot);
                    break;
                }
            }
            // The way in, kept open.
            Keep(Footprint.Box(W(_altar + At(_entrance, _ring)), new Vector2(2f, 3.2f), YawOf(At(_entrance, 1f))));
        }

        // An avenue of paired stones from the way in out towards the road, lower the further out.
        private void Avenue(WorldRandom r)
        {
            Vector2 way = At(_entrance, 1f), side = new Vector2(-way.Y, way.X);
            int pairs = r.Int(2, 4);
            for (int k = 0; k < pairs; k++)
            {
                float out_ = _ring + 3.2f + k * r.Range(3.2f, 3.8f);
                if (out_ > Site.Radius * 0.98f) break;
                foreach (float s in new[] { -1f, 1f })
                {
                    if (r.Chance(0.15f)) continue;
                    float high = r.Range(1.6f, 2.6f) * (1f - k * 0.12f);
                    Vector2 local = _altar + way * out_ + side * (s * r.Range(2.2f, 2.6f));
                    PutRound(new PlaceSpec(PlacePiece.Menhir, NextSeed(), PlaceSpec.Cm(high), PlaceSpec.Cm(high * 0.45f + 0.3f), r.Chance(0.15f) ? 2 : 0, (int)PlaceSpec.Degrees(r.Range(-0.1f, 0.1f)), _stone.R, _stone.G, _stone.B), W(local), YawOf(way), high * 0.25f + 0.35f, Tall, 0.2f, 0.45f);
                }
            }
            Trail(_altar + way * (_ring + 1f), _altar + way * Site.Radius, 2.2f, GroundPaint.Yard, 1.6f);
        }

        // The barrow beyond the ring, to the north if there's room, its portal to the stones; dug into by the Ossuary in some lands.
        private void Barrow(WorldRandom r)
        {
            float length = r.Range(9f, 13f), width = r.Range(5f, 7f);
            bool opened = r.Chance(0.4f);
            var spec = new PlaceSpec(PlacePiece.Barrow, NextSeed(), PlaceSpec.Cm(length), PlaceSpec.Cm(width), PlaceSpec.Cm(r.Range(1.8f, 2.6f)), opened ? 1 : 0, _stone.R, _stone.G, _stone.B);
            // North first, then round to either side, never across the way in.
            foreach (float turn in new[] { 0f, 0.5f, -0.5f, 0.9f, -0.9f, 1.3f, -1.3f })
            {
                float a = -MathF.PI * 0.5f + turn;
                if (MathF.Abs(MathF.IEEERemainder(a - _entrance, MathF.Tau)) < 1f) continue;
                Vector2 dir = At(a, 1f);
                foreach (float extra in new[] { 1.5f, 3f, 0.5f })
                {
                    Vector2 centre = _altar + dir * (_ring + length * 0.5f + extra);
                    // Its portal faces the ring: its own +z towards the altar.
                    float yaw = YawOf(-dir);
                    if (!PutBox(spec, W(centre), yaw, new Vector2(width * 0.5f + 0.3f, length * 0.5f + 0.3f), 2.4f, 0.6f, 0.36f)) continue;
                    Plan.Marks.Add(new PlaceMark(PlaceMarkKind.Pit, W(centre), yaw, new Vector2(width * 0.5f, length * 0.5f)));
                    if (opened)
                    {
                        // The Ossuary's dig: the ground worn bare at its portal, and the bones laid out to be counted.
                        Vector2 portal = centre - dir * (length * 0.5f - 0.5f);
                        Paint(GroundPaint.Yard, Footprint.Circle(W(portal), 3.5f), 2f);
                        Strew(LitterKind.Bones, portal - dir * 1.5f + new Vector2(-dir.Y, dir.X) * 2f, 1.6f, r.Int(8, 14));
                        Strew(LitterKind.Spalls, portal - dir * 0.5f, 2f, r.Int(10, 16), _stone);
                    }
                    return;
                }
            }
        }

        // The cult's heart beside the altar, its totems between the stones, its torches at the way in, and the lean-to it keeps its things under, outside the ring.
        private void CultOf(WorldRandom r)
        {
            Vector2 way = At(_entrance, 1f);
            Vector2 heart = _altar - way * r.Range(3.4f, 4.2f) + new Vector2(-way.Y, way.X) * r.Range(-1.5f, 1.5f);
            float radius = r.Range(1.5f, 2.2f);
            if (Tries(8, () => PutRound(new PlaceSpec(PlacePiece.CultHeart, NextSeed(), (int)_cult, PlaceSpec.Cm(radius)), W(heart + new Vector2(r.Range(-0.8f, 0.8f), r.Range(-0.8f, 0.8f))), r.Float() * MathF.Tau, radius + 0.3f, _cult == Cult.Frost ? Tall : 1.2f, 0.3f, 0.4f)))
            {
                if (_cult == Cult.Fire) Paint(GroundPaint.Ash, Footprint.Circle(W(heart), radius + 1.6f), 1.5f);
                if (_cult == Cult.Mire) Paint(GroundPaint.Mud, Footprint.Circle(W(heart), radius + 1.8f), 1.5f);
            }
            int totems = r.Int(2, 4);
            for (int k = 0; k < totems; k++)
            {
                float a = _entrance + MathF.PI + (k - (totems - 1) * 0.5f) * r.Range(0.7f, 0.95f);
                Tries(5, () => PutRound(new PlaceSpec(PlacePiece.Totem, NextSeed(), PlaceSpec.Cm(r.Range(2.8f, 4f)), (int)_cult), W(_altar + At(a + r.Range(-0.15f, 0.15f), _ring - r.Range(2f, 3.2f))), r.Float() * MathF.Tau, 0.6f, Tall, 0.2f, 0.45f));
            }
            Vector2 side = new Vector2(-way.Y, way.X);
            int torch = _cult switch { Cult.Fire => 2, Cult.Frost => 3, _ => 0 };
            foreach (float s in new[] { -1f, 1f }) PutRound(new PlaceSpec(PlacePiece.Torch, NextSeed(), PlaceSpec.Cm(r.Range(2.2f, 2.8f)), torch), W(_altar + way * (_ring + 1.6f) + side * (s * 2.6f)), r.Float() * MathF.Tau, 0.35f, 0f, 0.2f, 0.45f);
            // Where the cult keeps its things, under a lean-to outside the ring, its fire before it.
            Tries(10, () =>
            {
                float a = _entrance + (r.Chance(0.5f) ? 1f : -1f) * r.Range(1.2f, 2f);
                Vector2 at = _altar + At(a, _ring + r.Range(4.5f, 6.5f));
                if (!PutBox(new PlaceSpec(PlacePiece.Shelter, NextSeed(), PlaceSpec.Cm(r.Range(3.6f, 4.6f)), PlaceSpec.Cm(r.Range(2.6f, 3.2f))), W(at), YawOf((_altar - at).Normalized()), new Vector2(2.6f, 2f), 4f, 0.4f, 0.4f)) return false;
                Vector2 fire = at + (_altar - at).Normalized() * 3.6f;
                PutRound(new PlaceSpec(PlacePiece.Campfire, NextSeed()), W(fire), 0f, 1.4f, 0.8f, 0.4f, 0.4f);
                Paint(GroundPaint.Yard, Footprint.Circle(W((at + fire) * 0.5f), 3.6f), 2f);
                return true;
            });
        }

        // The cult about the stones, and the chest of what they took at the foot of the altar.
        private void Hold(WorldRandom r)
        {
            Stand(SpawnRole.Cultist, Around(W(_altar), _ring * 0.85f, r.Fork("ring"), 50), r.Int(3, 4), 3.5f, r.Fork("cultists"));
            Chest(W(_altar - At(_entrance, 2.6f) + new Vector2(r.Range(-1.5f, 1.5f), 0f)), Site.Radius * 0.4f);
        }

        // What lies about: what the cult offers at the altar's foot, and what's come off the broken stones.
        private void Litter(WorldRandom r)
        {
            LitterKind offered = _cult switch { Cult.Fire => LitterKind.Embers, Cult.Frost => LitterKind.Snow, Cult.Storm => LitterKind.Spalls, _ => LitterKind.Bones };
            Strew(offered, _altar + new Vector2(r.Range(-1f, 1f), r.Range(-1f, 1f)), 2.4f, r.Int(8, 14), _stone);
            Strew(LitterKind.Bones, _altar - At(_entrance, 3f), 1.8f, r.Int(5, 9));
            for (int k = 0; k < 2 && _stones.Count > 0; k++) Strew(LitterKind.Rubble, _stones[r.Int(_stones.Count)] + new Vector2(r.Range(-1f, 1f), r.Range(-1f, 1f)), 1.6f, r.Int(5, 8), _stone);
        }
    }
}
