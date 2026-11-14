using Dino_Engine.Modelling.Procedural.Architecture.Places;
using Dino_Engine.World.Content;
using OpenTK.Mathematics;

namespace Dino_Engine.World.Generation.Places
{
    /// <summary>
    /// Lays out a quarry (PLACES.md), cut into the hillside the camera looks at: the ground behind the face laid level at its lip, the floor in front of it laid level at its foot, and
    /// under the benches a ramp half a metre below the foot of every riser, so that the ground stays under the face's treads the whole way up (<see cref="Quarry.Face"/>); on the floor the
    /// treadwheel crane at the foot of the face, blocks levered down, one on its sledge, the split line, the bankers with their chips, the rubble heaps along its sides, and the dressed blocks
    /// stacked by the way out to be carted; and at its mouth the Reich's camp and its tower over the floor. The stone is the land's own. The Reich's greycoats work it with the land's hands,
    /// who come back to it once it's freed.
    /// </summary>
    internal sealed class QuarryBuilder : PlaceBuilder
    {
        private int[] _stone = null!;
        private float _floor, _height, _faceZ, _width, _floorDepth;
        private int _benches;
        private float _ledge;

        private QuarryBuilder(SiteBuilder b) : base(b, "quarry") { }

        public static void Build(SiteBuilder b) => new QuarryBuilder(b).Lay();

        private void Lay()
        {
            (int red, int green, int blue) = PlaceMaterials.StoneOf(World.Geology);
            _stone = new[] { red, green, blue };
            Plan.Yaw = Uphill();
            Plan.Half = new Vector2(Site.Radius);
            Cut(Random.Fork("cut"));
            Works(Random.Fork("works"));
            Camp(Random.Fork("camp"));
            Hold(Random.Fork("holders"));
        }

        private int[] Stone(params int[] numbers) => numbers.Concat(_stone).ToArray();

        // Which way the ground climbs most, within a little either side of north: the face is cut into it, facing the camera.
        private float Uphill()
        {
            float best = MathF.PI, most = float.MinValue;
            float here = World.Elevation.Sample(Site.Position);
            foreach (float turn in new[] { -0.4f, -0.2f, 0f, 0.2f, 0.4f })
            {
                Vector2 way = new Vector2(MathF.Sin(turn), MathF.Cos(turn));
                float rise = 0f;
                foreach (float d in new[] { 14f, 22f, 30f })
                {
                    Vector2 at = Site.Position + way * d;
                    if (World.InBounds(at)) rise += World.Elevation.Sample(at) - here;
                }
                if (rise <= most) continue;
                most = rise;
                // Its local -z, its back, up the hill.
                best = turn + MathF.PI;
            }
            return best;
        }

        // The cut: the face's size from the ground it's in, the ground behind it laid to its lip and the floor to its foot, and the face itself.
        private void Cut(WorldRandom r)
        {
            _width = Math.Clamp(Site.Radius * r.Range(0.85f, 1.1f), 22f, 38f);
            _faceZ = -Site.Radius * 0.32f;
            // Narrower where a road passes close by, so that neither the face nor the rubble at its ends is ever on it.
            for (int k = 0; k < 6 && !FaceClear(_width, 4f * 2.1f); k++) _width = MathF.Max(_width * 0.86f, 14f);
            _floorDepth = Site.Radius * 0.86f;
            Vector2 floorCentre = new Vector2(0f, _faceZ + _floorDepth * 0.5f);
            Vector2 floorHalf = new Vector2(_width * 0.5f + 3f, _floorDepth * 0.5f + 0.3f);
            Footprint floor = Footprint.Box(W(floorCentre - new Vector2(0f, 0.3f)), floorHalf, Plan.Yaw);
            // The floor goes in at the foot of where the hill starts to climb, a little below the ground there, and the face is as high as the hill above it, within reason.
            _floor = MathF.Min(AverageGround(floor), World.Elevation.Sample(W(new Vector2(0f, _faceZ + _floorDepth * 0.7f)))) - 0.25f;
            float rim = 0f;
            foreach (float x in new[] { -_width / 3f, 0f, _width / 3f }) rim += World.Elevation.Sample(W(new Vector2(x, _faceZ - 14f)));
            _height = Math.Clamp(rim / 3f - _floor + 1f, 6f, 9.5f);
            _benches = Math.Clamp((int)MathF.Round(_height / 1.9f), 2, 5);
            _ledge = r.Range(1.6f, 2.3f);
            float depth = _benches * _ledge;
            const float Over = 1.8f, Under = 0.6f;
            // The ground blending out from the floor to the land round it, then laid to the lip behind the face, to a ramp under its benches, and to its foot in front, each over the
            // one before (TerrainBakeStage.ApplyPads), the ramp half a metre under the foot of every riser, as the drawn ground stands 0.15 m over where people stand.
            World.Pads.Add(new GroundPad(floor, _floor, 8f));
            World.Pads.Add(new GroundPad(Footprint.Box(W(new Vector2(0f, _faceZ - (depth + 12f) * 0.5f)), new Vector2(_width * 0.5f + 1f, (depth + 12f) * 0.5f), Plan.Yaw), _floor + _height, 7f));
            float slope = _height / depth;
            World.Pads.Add(new GroundPad(Footprint.Box(W(new Vector2(0f, _faceZ - depth * 0.5f)), new Vector2(_width * 0.5f + 1f, depth * 0.5f + 0.2f), Plan.Yaw), _floor - Under + _height * 0.5f, 1.5f, Dir(-Vector2.UnitY) * slope));
            World.Pads.Add(new GroundPad(floor, _floor, 1.2f));
            float over = Over;
            Paint(GroundPaint.Chippings, floor, 3f);
            Keep(floor);
            // The face, solid to its full height over its benches, and its ends, where the rubble is tipped.
            var face = new PlaceSpec(PlacePiece.Face, NextSeed(), Stone(PlaceSpec.Cm(_width), PlaceSpec.Cm(_height), _benches, PlaceSpec.Cm(_ledge), PlaceSpec.Cm(over)));
            World.Placement.Add(Footprint.Box(W(new Vector2(0f, _faceZ - depth * 0.5f + 0.3f)), new Vector2(_width * 0.5f, depth * 0.5f + 0.3f), Plan.Yaw), PlacementLayer.Structure, solid: true, height: _height);
            foreach (float s in new[] { -1f, 1f }) World.Placement.Add(Footprint.Box(W(new Vector2(s * (_width * 0.5f + 3.15f), _faceZ - depth * 0.5f + 0.8f)), new Vector2(3f, depth * 0.5f + 2.2f), Plan.Yaw), PlacementLayer.Structure, solid: true, height: _height * 0.6f);
            World.Pending.Add(new PendingObject(face.Id, W(new Vector2(0f, _faceZ)), Plan.Yaw, Vector3.One, _floor));
            Plan.Marks.Add(new PlaceMark(PlaceMarkKind.Pit, W(floorCentre), Plan.Yaw, floorHalf));
        }

        // Whether a face this wide, its benches this deep at most, and the rubble at its ends keep clear of every road.
        private bool FaceClear(float width, float depth)
        {
            Footprint face = Footprint.Box(W(new Vector2(0f, _faceZ - depth * 0.5f)), new Vector2(width * 0.5f + 6.2f, depth * 0.5f + 2.6f), Plan.Yaw);
            return !World.Placement.Overlaps(face, PlacementLayer.Road, 0.6f);
        }

        // On the floor: the crane at the face's foot, what's been cut lying where it came down, the sledge, the split line, the bankers, the rubble along the sides.
        private void Works(WorldRandom r)
        {
            float foot = _faceZ + 1.2f;
            // The crane, its jib over the face, as it lowers blocks off the benches.
            Tries(10, () =>
            {
                Vector2 local = new Vector2(r.Range(-_width * 0.3f, _width * 0.3f), foot + r.Range(3.2f, 4.5f));
                return Floor(new PlaceSpec(PlacePiece.Crane, NextSeed(), Stone(PlaceSpec.Cm(r.Range(7f, 9f)))), local, YawOf(-Vector2.UnitY), new Vector2(3.6f, 3f), 2.5f);
            });
            int blocks = r.Int(4, 7);
            for (int k = 0; k < blocks; k++)
            {
                Tries(6, () =>
                {
                    Vector3 size = new Vector3(r.Range(0.9f, 2.2f), r.Range(0.6f, 1.2f), r.Range(0.7f, 1.4f));
                    Vector2 local = new Vector2(r.Range(-_width * 0.45f, _width * 0.45f), foot + r.Range(1.5f, 9f));
                    var spec = new PlaceSpec(PlacePiece.Block, NextSeed(), PlaceSpec.Cm(size.X), PlaceSpec.Cm(size.Y), PlaceSpec.Cm(size.Z), _stone[0], _stone[1], _stone[2], r.Chance(0.3f) ? 1 : 0, r.Chance(0.5f) ? 1 : 0);
                    return Floor(spec, local, r.Float() * MathF.Tau, new Vector2(size.X, size.Z) * 0.5f + new Vector2(0.35f), size.Y);
                });
            }
            Tries(10, () => Floor(new PlaceSpec(PlacePiece.SplitLine, NextSeed(), Stone(PlaceSpec.Cm(r.Range(2.4f, 3.6f)))), new Vector2(r.Range(-_width * 0.4f, _width * 0.4f), foot + r.Range(2f, 8f)), r.Float() * MathF.Tau, new Vector2(2f, 1.6f), 1.4f));
            // A block on its sledge, being hauled out towards the mouth.
            Tries(10, () => Floor(new PlaceSpec(PlacePiece.Sledge, NextSeed(), Stone()), new Vector2(r.Range(-_width * 0.35f, _width * 0.35f), foot + r.Range(8f, 13f)), YawOf(Vector2.UnitY) + r.Range(-0.4f, 0.4f), new Vector2(1.2f, 3f), 1.8f));
            int bankers = r.Int(1, 3);
            for (int k = 0; k < bankers; k++) Tries(8, () => Floor(new PlaceSpec(PlacePiece.Banker, NextSeed(), Stone()), new Vector2(r.Range(-_width * 0.42f, _width * 0.42f), foot + r.Range(9f, _floorDepth - 6f)), r.Float() * MathF.Tau, new Vector2(1.8f, 1.4f), 1.1f));
            // Rubble heaped along the floor's sides.
            foreach (float s in new[] { -1f, 1f })
            {
                Tries(8, () =>
                {
                    float radius = r.Range(2f, 3.4f);
                    Vector2 local = new Vector2(s * (_width * 0.5f + r.Range(-1.5f, 1.5f)), foot + r.Range(6f, _floorDepth - 5f));
                    return Floor(new PlaceSpec(PlacePiece.Rubble, NextSeed(), Stone(PlaceSpec.Cm(radius))), local, r.Float() * MathF.Tau, new Vector2(radius), radius * 0.4f);
                });
            }
            // The dressed blocks stacked by the mouth, towards where the road comes, to be carted to the Reich, and the cart that takes them.
            Vector2 road = Plan.ToLocal(Site.Position + Site.RoadDirection);
            float side = road.X >= 0f ? 1f : -1f;
            int stacks = r.Int(1, 2);
            for (int k = 0; k < stacks; k++)
            {
                Tries(10, () => Floor(new PlaceSpec(PlacePiece.BlockStack, NextSeed(), Stone(r.Int(5, 9))), new Vector2(side * r.Range(_width * 0.15f, _width * 0.45f), _faceZ + _floorDepth - r.Range(4f, 9f)), YawOf(Vector2.UnitX) + r.Range(-0.2f, 0.2f), new Vector2(2.6f, 1.2f), 1.8f));
            }
            Tries(10, () => Structure(StructureCatalog.Cart, W(new Vector2(side * r.Range(_width * 0.1f, _width * 0.4f), _faceZ + _floorDepth + r.Range(1f, 5f))), YawOf(road) + r.Range(-0.4f, 0.4f), 0.5f));
        }

        // Something on the floor, which is laid level, standing at its height.
        private bool Floor(PlaceSpec spec, Vector2 local, float yaw, Vector2 half, float height)
        {
            Footprint f = Footprint.Box(W(local), half, yaw);
            if (!Clear(f, PlacementLayer.Structure | PlacementLayer.Prop | PlacementLayer.Fence | PlacementLayer.Road, 0.3f) || !Inside(local, half.Length)) return false;
            World.Placement.Add(f, PlacementLayer.Prop, solid: true, height: height);
            World.Pending.Add(new PendingObject(spec.Id, W(local), yaw, Vector3.One, _floor));
            return true;
        }

        // Whether a point and the room round it are on the floor.
        private bool Inside(Vector2 local, float room) => MathF.Abs(local.X) <= _width * 0.5f + 3f - room * 0.5f && local.Y >= _faceZ + 0.5f && local.Y <= _faceZ + _floorDepth - room * 0.5f;

        // At the floor's mouth: the Reich's tower over it, its tents, its fire, its crates and its banners where the way in is, and lanterns along the foot of the face.
        private void Camp(WorldRandom r)
        {
            float mouth = _faceZ + _floorDepth;
            foreach (float s in r.Chance(0.5f) ? new[] { 1f, -1f } : new[] { -1f, 1f })
            {
                if (Tries(6, () => Structure(StructureCatalog.GuardTower, W(new Vector2(s * (_width * 0.5f + r.Range(2f, 5f)), mouth - r.Range(2f, 8f))), YawOf(-Vector2.UnitY) + r.Range(-0.3f, 0.3f), 0.4f))) break;
            }
            int tents = r.Int(1, 3);
            for (int k = 0; k < tents; k++)
            {
                Tries(10, () => PutBox(new PlaceSpec(PlacePiece.Tent, NextSeed(), PlaceSpec.Cm(r.Range(4.4f, 5.6f)), 1), W(new Vector2(r.Range(-_width * 0.55f, _width * 0.55f), mouth + r.Range(2f, 9f))), YawOf(-Vector2.UnitY) + r.Range(-0.5f, 0.5f), new Vector2(3.9f, 3.6f), 3.4f, 0.5f, 0.4f));
            }
            Tries(10, () => PutRound(new PlaceSpec(PlacePiece.Campfire, NextSeed()), W(new Vector2(r.Range(-8f, 8f), mouth + r.Range(1f, 6f))), 0f, 1.4f, 0.8f, 0.5f, 0.4f));
            for (int k = 0; k < 2; k++) Tries(8, () => PutBox(new PlaceSpec(PlacePiece.Crates, NextSeed(), r.Int(3, 5), 1), W(new Vector2(r.Range(-_width * 0.5f, _width * 0.5f), mouth + r.Range(0f, 8f))), r.Float() * MathF.Tau, new Vector2(2.2f, 1.3f), 1.8f, 0.4f));
            // The banners either side of the way in, facing out of it.
            Vector2 road = Plan.ToLocal(Site.Position + Site.RoadDirection);
            float gateX = Math.Clamp(road.X * Site.Radius * 0.5f, -_width * 0.3f, _width * 0.3f);
            foreach (float s in new[] { -1f, 1f })
            {
                PutRound(new PlaceSpec(PlacePiece.Banner, NextSeed(), PlaceSpec.Cm(r.Range(6.4f, 7.4f)), 1), W(new Vector2(gateX + s * 5f, mouth + 1.5f)), YawOf(Vector2.UnitY), 0.5f, 0f, 0.2f, 0.5f);
            }
            // Lanterns at the foot of the face, for the hours the work goes on after dark.
            int lanterns = r.Int(2, 3);
            for (int k = 0; k < lanterns; k++)
            {
                float x = -_width * 0.4f + k * (_width * 0.8f / Math.Max(lanterns - 1, 1)) + r.Range(-2f, 2f);
                Tries(4, () => Floor(new PlaceSpec(PlacePiece.Lantern, NextSeed(), PlaceSpec.Cm(r.Range(3.6f, 4.2f)), 1), new Vector2(x + r.Range(-1f, 1f), _faceZ + r.Range(1.2f, 2f)), r.Float() * MathF.Tau, new Vector2(0.4f), 0f));
            }
        }

        // The greycoats on the floor and at its mouth, the guards by the tower, the land's quarrymen who'll come back to the face once it's free, and the chest by the tents.
        private void Hold(WorldRandom r)
        {
            Vector2 floorMiddle = W(new Vector2(0f, _faceZ + _floorDepth * 0.5f));
            Stand(SpawnRole.Raider, Around(floorMiddle, _floorDepth * 0.45f, r.Fork("floor"), 60), r.Int(4, 6), 4.5f, r.Fork("greycoats"));
            Stand(SpawnRole.Guard, Around(W(new Vector2(0f, _faceZ + _floorDepth + 3f)), 8f, r.Fork("mouth"), 30), 2, 4f, r.Fork("guards"));
            Stand(SpawnRole.Returning, Around(W(new Vector2(0f, _faceZ + 4f)), _width * 0.4f, r.Fork("face"), 30), r.Int(2, 3), 4f, r.Fork("quarrymen"));
            Chest(W(new Vector2(r.Range(-5f, 5f), _faceZ + _floorDepth + 4f)), Site.Radius * 0.5f);
        }
    }
}
