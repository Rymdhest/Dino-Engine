using Dino_Engine.Modelling.Procedural.Architecture.Places;
using Dino_Engine.World.Content;
using OpenTK.Mathematics;

namespace Dino_Engine.World.Generation.Places
{
    /// <summary>
    /// Lays out a wreck (PLACES.md). A ship came ashore here in a storm and broke its back: her bow lies up the beach where she drove in, or along it where the sea turned her, and her
    /// stern lies further out in the surf; her mast came down across the sand with its sail half off the yard; what was in her hold is strewn down the beach from the break, her anchor
    /// was dragged up after her and her boat lies upturned above the tide line, with the driftwood. Two in three are the Reich's iron ships, and what was in their cages got out and
    /// hunts the beach; the rest are the land's own, and the turncoats who wreck for the Reich are stripping them, with their sheerlegs over the break and their camp above the sand.
    /// </summary>
    internal sealed class WreckBuilder : PlaceBuilder
    {
        private ShipDesign _ship = null!;
        private bool _reich;
        private float _shore;
        private readonly List<(Vector2 Centre, float Yaw, Vector2 Half)> _halves = new();
        private Vector2 _break;             // Where she broke, between her halves.
        private Vector2 _heelSide;          // The way her bow half's deck faces as she lies.

        private WreckBuilder(SiteBuilder b) : base(b, "wreck") { }

        public static void Build(SiteBuilder b) => new WreckBuilder(b).Lay();

        private void Lay()
        {
            _reich = PlaceStory.WreckIsReich(Site.Story);
            _ship = ShipDesign.Of((uint)Random.Fork("ship").NextULong(), _reich);
            if (!B.FindWater(140f, out Vector2 toSea, out _shore))
            {
                toSea = -Site.RoadDirection;
                _shore = Site.Radius;
            }
            // The place's front, its +z, is inland: the sea is behind it.
            Plan.Yaw = SiteBuilder.YawFacing(-toSea);
            Plan.Half = new Vector2(Site.Radius * 1.15f, Site.Radius);

            LayHalves(Random.Fork("halves"));
            Beach();
            LayRig(Random.Fork("rig"));
            Strew(Random.Fork("strewn"));
            if (_reich) LeaveCages(Random.Fork("cages"));
            else Wreckers(Random.Fork("wreckers"));
            Driftwood(Random.Fork("driftwood"));
            Hold(Random.Fork("holders"));
        }

        // ---------------------------------------------------------------- Her two halves

        // Her bow where she drove in or where the sea turned her, and her stern out past it in the surf; each heeled so its deck faces up the beach to whoever comes, as a hull seen keel on
        // is a wall, and pitched along the fall of the sand under it.
        private void LayHalves(WorldRandom r)
        {
            float bowLength = _ship.LengthOf(0), sternLength = _ship.LengthOf(1);
            float gap = _ship.Gap * _ship.Length;
            bool broadside = r.Chance(0.42f);
            for (int attempt = 0; attempt < 14; attempt++)
            {
                WorldRandom a = r.Fork(attempt);
                float turn = broadside ? MathF.PI * 0.5f * (a.Chance(0.5f) ? 1f : -1f) + a.Range(-0.35f, 0.35f) : a.Range(-0.7f, 0.7f);
                Vector2 bowWay = new Vector2(MathF.Sin(turn), MathF.Cos(turn));
                // Where she broke: at the water's edge, or a little either side of it.
                float breakZ = broadside ? -_shore + a.Range(4f, 10f) : -_shore + a.Range(-2f, 6f);
                Vector2 breakLocal = new Vector2(a.Range(-6f, 6f), breakZ);
                Vector2 bowLocal = breakLocal + bowWay * (bowLength * 0.5f + gap * 0.3f);
                float twist = a.Range(0.25f, 0.8f) * (a.Chance(0.5f) ? 1f : -1f);
                Vector2 sternWay = new Vector2(MathF.Sin(turn + twist), MathF.Cos(turn + twist));
                Vector2 sternLocal = breakLocal - sternWay * (sternLength * 0.5f + gap * 0.7f) + new Vector2(a.Range(-2f, 2f), 0f);
                if (!TryHalf(0, W(bowLocal), YawOf(bowWay), bowLength, 1.6f, a.Fork("bow"), dryRun: true)) continue;
                if (!TryHalf(1, W(sternLocal), YawOf(sternWay), sternLength, 4f, a.Fork("stern"), dryRun: true)) continue;
                TryHalf(0, W(bowLocal), YawOf(bowWay), bowLength, 1.6f, a.Fork("bow"), dryRun: false);
                TryHalf(1, W(sternLocal), YawOf(sternWay), sternLength, 4f, a.Fork("stern"), dryRun: false);
                _break = W(breakLocal);
                return;
            }
            // A beach too crowded for her whole: her bow alone, where it fits.
            for (int attempt = 0; attempt < 24; attempt++)
            {
                WorldRandom a = r.Fork(100 + attempt);
                float turn = a.Float() * MathF.Tau;
                Vector2 way = new Vector2(MathF.Sin(turn), MathF.Cos(turn));
                Vector2 local = new Vector2(a.Range(-10f, 10f), -_shore + a.Range(4f, 14f));
                if (!TryHalf(0, W(local), YawOf(way), bowLength, 2f, a, dryRun: false)) continue;
                _break = W(local - way * bowLength * 0.5f);
                return;
            }
            _break = Site.Position;
        }

        private bool TryHalf(int part, Vector2 at, float yaw, float length, float wet, WorldRandom r, bool dryRun)
        {
            // Heeled so her deck faces the camera, which looks along +z, more often than not: a hull seen from below is only its bottom.
            float heel = r.Range(0.2f, 0.5f);
            Vector2 starboard = new Vector2(MathF.Cos(yaw), -MathF.Sin(yaw));
            float sign = starboard.Y < -0.15f ? 1f : starboard.Y > 0.15f ? -1f : (r.Chance(0.5f) ? 1f : -1f);
            if (r.Chance(0.2f)) sign = -sign;
            heel *= sign;
            Vector2 along = new Vector2(MathF.Sin(yaw), MathF.Cos(yaw));
            float fore = World.Elevation.Sample(at + along * length * 0.4f), aft = World.Elevation.Sample(at - along * length * 0.4f);
            float pitch = Math.Clamp(MathF.Atan2(fore - aft, length * 0.8f), -0.2f, 0.2f);
            float half = _ship.Beam * 0.5f * MathF.Cos(heel) + _ship.Depth * 0.45f * MathF.Abs(MathF.Sin(heel));
            Vector2 lean = starboard * (sign * _ship.Depth * 0.25f * MathF.Abs(MathF.Sin(heel)));
            Footprint f = Footprint.Box(at + lean, new Vector2(half, length * 0.5f), yaw);
            if (!Ground(f, 0.45f, wet) || !Clear(f, PlacementLayer.Built | PlacementLayer.Road, 1f)) return false;
            if (dryRun) return true;
            float sunk = r.Range(0.5f, 1.3f);
            var spec = new PlaceSpec(PlacePiece.Hull, _ship.Seed, part, PlaceSpec.Degrees(heel), PlaceSpec.Degrees(pitch), PlaceSpec.Cm(sunk), _reich ? 1 : 0);
            World.Placement.Add(f, PlacementLayer.Prop, solid: true, height: Tall);
            World.Pending.Add(new PendingObject(spec.Id, at, yaw, Vector3.One, null));
            _halves.Add((at, yaw, new Vector2(half, length * 0.5f)));
            if (part == 0) _heelSide = starboard * sign;
            Plan.Marks.Add(new PlaceMark(PlaceMarkKind.Hull, at, yaw, new Vector2(half, length * 0.5f)));
            return true;
        }

        // The beach she lies on, sand from the water up past her and round each half, its edge frayed into the grass, as the sea throws sand up a beach as far as the storms reach.
        private void Beach()
        {
            Vector2 top = W(new Vector2(0f, -_shore + 16f));
            Paint(GroundPaint.Sand, Footprint.Box(W(new Vector2(0f, -_shore + 5f)), new Vector2(Site.Radius * 1.1f, 11f), Plan.Yaw), 9f);
            Paint(GroundPaint.Sand, Footprint.Circle(top, 7f), 8f);
            foreach ((Vector2 centre, float yaw, Vector2 half) in _halves) Paint(GroundPaint.Sand, Footprint.Box(centre, half + new Vector2(4f), yaw), 6f);
        }

        /// <summary>Where the tide reaches along the beach, <paramref name="across"/> along the shore in the place's frame: walked down from inland until the ground is a metre over the sea.</summary>
        private Vector2 TideLine(float across)
        {
            for (float z = Site.Radius * 0.6f; z > -_shore - 20f; z -= 1f)
            {
                Vector2 at = W(new Vector2(across, z));
                if (World.InBounds(at) && World.Elevation.Sample(at) < WorldPlan.SeaLevel + 1.1f) return at;
            }
            return W(new Vector2(across, -_shore + 3f));
        }

        // ---------------------------------------------------------------- Her rig

        // Her mainmast came down over the side her bow heels to, and lies across the sand from where it was stepped, its yards still on it and its sail spread.
        private void LayRig(WorldRandom r)
        {
            if (_halves.Count == 0) return;
            (Vector2 centre, float yaw, Vector2 half) = _halves[0];
            Vector2 along = new Vector2(MathF.Sin(yaw), MathF.Cos(yaw));
            float length = _ship.Length * r.Range(0.55f, 0.7f);
            for (int attempt = 0; attempt < 10; attempt++)
            {
                WorldRandom a = r.Fork(attempt);
                Vector2 side = attempt < 5 ? _heelSide : -_heelSide;
                float angle = a.Range(-0.6f, 0.6f);
                Vector2 way = Rotate(side, angle);
                Vector2 foot = centre + along * a.Range(-half.Y * 0.3f, half.Y * 0.5f) + side * (half.X + 1.2f);
                Vector2 mid = foot + way * length * 0.5f;
                float yawMast = SiteBuilder.YawFacing(way);
                Footprint f = Footprint.Box(mid, new Vector2(0.45f, length * 0.5f), yawMast);
                if (!Ground(f, 0.45f, 1.2f) || !Clear(f, PlacementLayer.Built | PlacementLayer.Road, 0.3f)) continue;
                World.Placement.Add(f, PlacementLayer.Prop, solid: true, height: 0.9f);
                World.Pending.Add(new PendingObject(new PlaceSpec(PlacePiece.Mast, NextSeed(), PlaceSpec.Cm(length), _reich ? 1 : 0).Id, foot, yawMast, Vector3.One, null));
                return;
            }
        }

        private static Vector2 Rotate(Vector2 v, float angle) => new Vector2(v.X * MathF.Cos(angle) - v.Y * MathF.Sin(angle), v.X * MathF.Sin(angle) + v.Y * MathF.Cos(angle));

        // ---------------------------------------------------------------- What came out of her

        // Her cargo down the beach from the break, thickest by it; wreckage of her side; her anchor beside her bow with its chain to her; her boat above the tide line.
        private void Strew(WorldRandom r)
        {
            int groups = r.Int(4, 7);
            for (int k = 0, tries = 0; k < groups && tries < 40; tries++)
            {
                float reach = 4f + tries * 0.9f;
                Vector2 at = _break + SiteBuilder.Direction(r.Float() * MathF.Tau) * r.Range(3f, reach);
                int form = Pick(r, (0, 3f), (1, 2.5f), (2, _reich ? 1.4f : 0f), (3, 1.2f));
                var spec = new PlaceSpec(PlacePiece.Cargo, NextSeed(), form, r.Int(2, 5), _reich ? 1 : 0);
                if (PutRound(spec, at, r.Float() * MathF.Tau, form == 2 ? 2.6f : 2.2f, form == 3 ? 1.2f : 1.4f, 0.4f)) k++;
            }
            // Wreckage: a run of her side near the break, a heap of timbers, and, far along the sand, the ribs of a wreck older than this one.
            foreach ((int form, float near, float far) in new[] { (1, 4f, 12f), (2, 6f, 16f), (0, 16f, Site.Radius * 0.95f) })
            {
                if (form == 0 && r.Chance(0.4f)) continue;
                for (int attempt = 0; attempt < 10; attempt++)
                {
                    float length = form == 0 ? r.Range(9f, 13f) : r.Range(4f, 7f);
                    Vector2 at = form == 0 ? W(new Vector2(r.Range(-1f, 1f) * Site.Radius, -_shore + r.Range(2f, 7f))) : _break + SiteBuilder.Direction(r.Float() * MathF.Tau) * r.Range(near, far);
                    float yaw = r.Float() * MathF.Tau;
                    if (PutBox(new PlaceSpec(PlacePiece.Wreckage, NextSeed(), form, PlaceSpec.Cm(length), _reich ? 1 : 0), at, yaw, new Vector2(form == 1 ? 2.2f : 2.6f, length * 0.5f), form == 1 ? 0.5f : 1.1f, 0.3f, 0.5f)) break;
                }
            }
            // Her anchor, dragged up beside her bow, its chain back to her.
            if (_halves.Count > 0)
            {
                (Vector2 centre, float yaw, Vector2 half) = _halves[0];
                Vector2 along = new Vector2(MathF.Sin(yaw), MathF.Cos(yaw));
                for (int attempt = 0; attempt < 10; attempt++)
                {
                    Vector2 side = new Vector2(along.Y, -along.X) * (attempt % 2 == 0 ? 1f : -1f);
                    Vector2 at = centre + along * (half.Y + r.Range(2f, 8f)) + side * r.Range(2f, 6f);
                    Vector2 toBow = centre + along * half.Y - at;
                    float chain = MathF.Min(toBow.Length, 12f);
                    float yaw2 = SiteBuilder.YawFacing(toBow);
                    if (PutBox(new PlaceSpec(PlacePiece.Anchor, NextSeed(), PlaceSpec.Cm(chain), _reich ? 1 : 0), at, yaw2, new Vector2(1.7f, 2.4f), 0.7f, 0.3f, 0.5f)) break;
                }
            }
            // Her boat, upturned above the tide line, inland of her.
            for (int attempt = 0; attempt < 12; attempt++)
            {
                Vector2 at = W(new Vector2(r.Range(-1f, 1f) * Site.Radius * 0.7f, -_shore + r.Range(9f, 16f)));
                float length = r.Range(5.5f, 7.5f);
                if (PutBox(new PlaceSpec(PlacePiece.ShipBoat, NextSeed(), PlaceSpec.Cm(length), _reich ? 1 : 0), at, r.Float() * MathF.Tau, new Vector2(1.6f, length * 0.5f), 1.6f, 0.4f, 0.4f)) break;
            }
        }

        // The Reich's ship carried beasts in cages in her hold, and they burst out: a cage or two lies broken open by the break, and the beach is theirs.
        private void LeaveCages(WorldRandom r)
        {
            int cages = r.Int(1, 2);
            for (int k = 0, tries = 0; k < cages && tries < 40; tries++)
            {
                // Near the break first, up the beach the more tries it takes, as the sea's side of it is water.
                Vector2 at = _break + SiteBuilder.Direction(r.Float() * MathF.Tau) * r.Range(5f, 9f + tries * 0.6f) + Plan.Az * MathF.Min(tries * 0.4f, 8f);
                float width = r.Range(3f, 4.2f);
                float yaw = r.Float() * MathF.Tau;
                Footprint f = Footprint.Box(at, new Vector2(width * 0.5f + 1.6f, width * 0.45f + 1f), yaw);
                if (Put(new PlaceSpec(PlacePiece.BurstCage, NextSeed(), PlaceSpec.Cm(width)), at, yaw, f, 2.6f, maxSlope: 0.5f, padding: 0.3f, wet: 0.6f)) k++;
            }
        }

        // The land's own ship is being stripped: sheerlegs over the break dragging her cargo up, and the wreckers' camp above the tide line with what they've taken.
        private void Wreckers(WorldRandom r)
        {
            for (int attempt = 0; attempt < 10; attempt++)
            {
                Vector2 at = _break + SiteBuilder.Direction(r.Float() * MathF.Tau) * r.Range(4f, 8f);
                Vector2 toSea = _break - at;
                float yaw = SiteBuilder.YawFacing(toSea.LengthSquared > 0.01f ? toSea : Plan.Az);
                // Its legs are posts: walked round, and in nobody's way in the air.
                if (!Ground(Footprint.Circle(at, 2.5f), 0.45f, 1f) || !Clear(Footprint.Circle(at, 2.5f), PlacementLayer.Built | PlacementLayer.Road, 0.3f)) continue;
                World.Placement.Add(Footprint.Circle(at, 2.5f), PlacementLayer.Prop, solid: true, height: 0f);
                World.Pending.Add(new PendingObject(new PlaceSpec(PlacePiece.Salvage, NextSeed(), PlaceSpec.Cm(r.Range(7f, 9f))).Id, at, yaw, Vector3.One, null));
                break;
            }
            // The camp, up the beach.
            Vector2 camp = W(new Vector2(r.Range(-0.4f, 0.4f) * Site.Radius, MathF.Min(-_shore + 20f, Site.Radius * 0.6f)));
            Paint(GroundPaint.Yard, Footprint.Circle(camp, 9f), 5f);
            PutBox(new PlaceSpec(PlacePiece.Tent, NextSeed(), PlaceSpec.Cm(r.Range(4f, 5.5f)), 0), camp + Plan.Az * 4f, Plan.Yaw + MathF.PI, new Vector2(4.4f, 3.4f), 3.4f, 0.5f);
            PutRound(new PlaceSpec(PlacePiece.Campfire, NextSeed()), camp - Plan.Az * 1.5f, 0f, 1.4f, 0.8f, 0.5f);
            PutBox(new PlaceSpec(PlacePiece.Crates, NextSeed(), r.Int(3, 5), 0), camp + Plan.Ax * 6f, r.Float() * MathF.Tau, new Vector2(2.2f, 1.2f), 1.6f, 0.4f);
            PutRound(new PlaceSpec(PlacePiece.Barrels, NextSeed(), r.Int(3, 5)), camp - Plan.Ax * 6f, 0f, 1.8f, 1.4f, 0.4f);
        }

        // Driftwood along the tide line, bleached.
        private void Driftwood(WorldRandom r)
        {
            int logs = r.Int(3, 6);
            for (int k = 0, tries = 0; k < logs && tries < 30; tries++)
            {
                Vector2 at = TideLine(r.Range(-1f, 1f) * Site.Radius) + Plan.Az * r.Range(0.5f, 4f);
                float length = r.Range(3f, 7f);
                if (PutBox(new PlaceSpec(PlacePiece.Driftwood, NextSeed(), PlaceSpec.Cm(length)), at, Plan.Yaw + MathF.PI * 0.5f + r.Range(-0.6f, 0.6f), new Vector2(0.6f, length * 0.5f), 0.7f, 0.2f, 0.5f)) k++;
            }
        }

        // ---------------------------------------------------------------- Who holds it

        // On the Reich's wreck, what got out of its cages; on the land's, the wreckers. The chest is the ship's strongbox, by her stern, or what the wreckers have put by in their camp.
        private void Hold(WorldRandom r)
        {
            SpawnRole role = _reich ? SpawnRole.Beast : SpawnRole.Raider;
            var spots = new List<Vector2>();
            foreach ((Vector2 centre, _, Vector2 half) in _halves) spots.AddRange(Around(centre, half.Y + 8f, r.Fork(spots.Count), 30));
            spots.AddRange(Around(_break, 14f, r.Fork("break"), 30));
            spots.AddRange(Around(Site.Position, Site.Radius * 0.8f, r.Fork("about"), 30));
            int wanted = r.Int(5, 7);
            Stand(role, spots, wanted, 4.5f, r.Fork("stand"));
            Vector2 chest = _halves.Count > 1 ? _halves[1].Centre + Plan.Az * 6f : _reich ? _break + Plan.Az * 6f : W(new Vector2(0f, MathF.Min(-_shore + 20f, Site.Radius * 0.6f)));
            Chest(chest, Site.Radius);
        }
    }
}
