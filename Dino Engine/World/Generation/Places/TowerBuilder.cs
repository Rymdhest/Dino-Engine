using Dino_Engine.Modelling.Procedural.Architecture.Places;
using Dino_Engine.World.Content;
using OpenTK.Mathematics;

namespace Dino_Engine.World.Generation.Places
{
    /// <summary>
    /// Lays out a watchtower (LORE.md: from here you can see the next fire, when they are lit; the beacon has been cold since the ships came): one of the land's chain of beacon towers on
    /// its high ground, on the north side of its yard so the camera sees it standing over everything else, its beacon heaped and never lit; the wood for it stacked under its hoist and
    /// under the Reich's tarp; the keepers' cottage in the land's own way of building, their woodpile by it; the stone they sighted the next beacon by, turned to it, and the horn they'd
    /// blow. The Reich's guards hold it so that nobody lights the beacon: their tent, brazier and rack of spears, a barricade across the way up, and their banner on the tower.
    /// </summary>
    internal sealed class TowerBuilder : PlaceBuilder
    {
        private Vector2 _tower;
        private float _radius;
        private (int R, int G, int B) _stone;
        private Vector2 _door;
        private Vector2 _road;

        private TowerBuilder(SiteBuilder b) : base(b, "watchtower") { }

        public static void Build(SiteBuilder b) => new TowerBuilder(b).Lay();

        private void Lay()
        {
            Plan.Yaw = MathF.PI + Random.Range(-0.15f, 0.15f);
            Plan.Half = new Vector2(Site.Radius * 0.92f);
            (int r, int g, int bl) = LandStone;
            _stone = ((int)MathF.Min(r * 1.05f + 20f, 620f), (int)MathF.Min(g * 1.05f + 20f, 610f), (int)MathF.Min(bl * 1.05f + 18f, 600f));
            Vector2 road = Plan.ToLocal(Site.Position + Site.RoadDirection);
            _road = road.LengthSquared > 1e-4f ? road.Normalized() : Vector2.UnitY;
            Tower(Random.Fork("tower"));
            Keepers(Random.Fork("keepers"));
            Watch(Random.Fork("watch"));
            Held(Random.Fork("held"));
            Hold(Random.Fork("holders"));
            Litter(Random.Fork("litter"));
        }

        // The tower to the north of the yard, its door to the south; nearer the middle or to a side if the ground won't take it there.
        private void Tower(WorldRandom r)
        {
            bool square = r.Chance(0.4f);
            float high = r.Range(11f, 14.5f);
            // To the north of the yard if it'll stand there, then anywhere in it, nearest the north first; smaller if it won't stand at all.
            var wanted = new List<Vector2> { new Vector2(0f, -Site.Radius * 0.32f), new Vector2(-4f, -Site.Radius * 0.28f), new Vector2(4f, -Site.Radius * 0.28f) };
            for (float z = -Site.Radius * 0.4f; z <= Site.Radius * 0.3f; z += 3f)
            {
                for (float x = -Site.Radius * 0.45f; x <= Site.Radius * 0.45f; x += 3f) wanted.Add(new Vector2(x, z));
            }
            foreach (float size in new[] { 1f, 0.78f })
            {
                _radius = r.Range(2.4f, 3f) * size;
                var spec = new PlaceSpec(PlacePiece.BeaconTower, NextSeed(), PlaceSpec.Cm(high * (0.6f + 0.4f * size)), PlaceSpec.Cm(_radius), square ? 1 : 0, _stone.R, _stone.G, _stone.B);
                float reach = Beacon.FootReach(_radius);
                foreach (Vector2 spot in wanted)
                {
                    Vector2 at = W(spot);
                    float level = AverageGround(Footprint.Circle(at, reach));
                    if (!Put(spec, at, YawOf(Vector2.UnitY), Footprint.Circle(at, reach), Tall, PlacementLayer.Structure, 0.36f, 0.6f, floor: level)) continue;
                    _tower = spot;
                    _door = _tower + new Vector2(0f, reach + 1.8f);
                    Keep(Footprint.Box(W(_door), new Vector2(1.4f, 1.8f), Plan.Yaw));
                    // The yard round its foot trodden bare, and the way from the door down to the road.
                    Paint(GroundPaint.Yard, Footprint.Circle(at, reach + 3.5f), 2.5f);
                    Trail(_door, _tower + _road * Site.Radius, 2.4f, GroundPaint.Yard, 1.6f);
                    // The beacon's wood stacked under the hoist, its tarp roped down over it: its length runs north and south beside the tower, clear of the tower's foot.
                    Vector3 hoist = Beacon.HoistFoot(_radius);
                    Vector2 pyre = _tower + new Vector2(hoist.X + 0.4f, hoist.Z);
                    Tries(6, () =>
                    {
                        float length = r.Range(3.2f, 4.2f);
                        Vector2 stack = pyre + new Vector2(r.Range(0f, 0.6f), r.Range(-0.6f, 0.6f));
                        return PutBox(new PlaceSpec(PlacePiece.BeaconPyre, NextSeed(), PlaceSpec.Cm(length)), W(stack), YawOf(Vector2.UnitX), new Vector2(length * 0.5f + 1.2f, 1.35f), 1.8f, 0.1f, 0.4f);
                    });
                    return;
                }
            }
            // Nowhere for a tower: the keepers' yard is what's left, round the middle.
            _tower = new Vector2(0f, -Site.Radius * 0.2f);
            _door = Vector2.Zero;
        }

        // The keepers' cottage to one side of the yard, in the land's own way of building, its woodpile by it and a barrel of water.
        private void Keepers(WorldRandom r)
        {
            float side = r.Chance(0.5f) ? 1f : -1f;
            if (Tries(10, () => Structure(r.Chance(0.6f) ? StructureCatalog.Hut : StructureCatalog.Cottage, W(new Vector2(side * r.Range(8f, 11f), r.Range(-3f, 4f))), YawOf(new Vector2(-side * 0.4f, 1f).Normalized()), 0.8f, 0.36f)))
            {
                Tries(8, () => PutBox(new PlaceSpec(PlacePiece.Firewood, NextSeed(), PlaceSpec.Cm(r.Range(2.4f, 3.6f)), PlaceSpec.Cm(r.Range(1.1f, 1.6f))), W(new Vector2(side * r.Range(6f, 9f), r.Range(5.5f, 8f))), YawOf(Vector2.UnitY) + r.Range(-0.2f, 0.2f), new Vector2(2.2f, 1.4f), 1.6f, 0.3f, 0.4f));
                Tries(6, () => PutBox(new PlaceSpec(PlacePiece.Barrels, NextSeed(), r.Int(1, 2)), W(new Vector2(side * r.Range(5f, 7f), r.Range(0f, 3f))), r.Float() * MathF.Tau, new Vector2(1.2f), 1.4f, 0.3f, 0.4f));
            }
        }

        // The sighting stone at the yard's edge, turned to the next beacon down the chain or, if there's none, to the keep the Reich holds it from; the horn beside it.
        private void Watch(WorldRandom r)
        {
            Vector2? target = null;
            float nearest = float.MaxValue;
            foreach (WorldSite other in World.Sites)
            {
                if (other == Site || other.Kind != SiteKind.Watchtower) continue;
                float d = (other.Position - Site.Position).Length;
                if (d < nearest) { nearest = d; target = other.Position; }
            }
            target ??= World.Sites.FirstOrDefault(s => s.Kind == SiteKind.Keep)?.Position;
            Vector2 toward = target is Vector2 t && (t - Site.Position).LengthSquared > 1f ? Plan.ToLocal(Site.Position + (t - Site.Position).Normalized()) : -Vector2.UnitY;
            toward = toward.LengthSquared > 1e-4f ? toward.Normalized() : -Vector2.UnitY;
            // At the edge of the yard on that side, or the nearest edge to it the ground allows.
            Vector2 stone = Vector2.Zero;
            for (int k = 0; k < 8; k++)
            {
                float a = MathF.Atan2(toward.Y, toward.X) + (k % 2 == 0 ? 1f : -1f) * (k / 2) * 0.35f;
                Vector2 dir = new Vector2(MathF.Cos(a), MathF.Sin(a));
                Vector2 tried = _tower * 0.4f + dir * Site.Radius * 0.62f;
                if (!PutRound(new PlaceSpec(PlacePiece.SightStone, NextSeed(), _stone.R, _stone.G, _stone.B), W(tried), YawOf(toward), 0.7f, 1.4f, 0.3f, 0.45f)) continue;
                stone = tried;
                break;
            }
            Vector2 aside = new Vector2(-toward.Y, toward.X);
            Tries(8, () => PutRound(new PlaceSpec(PlacePiece.Horn, NextSeed()), W(stone + aside * ((r.Chance(0.5f) ? 1f : -1f) * r.Range(2.2f, 3f)) - toward * r.Range(0.5f, 1.5f)), YawOf(toward), 1f, 1.8f, 0.3f, 0.45f));
            Trail(_tower * 0.4f, stone, 1.4f, GroundPaint.Yard, 1.4f);
        }

        // The Reich's: its sentries' tent, their brazier and spears, a barricade across the way up, and a lantern by the tower's door.
        private void Held(WorldRandom r)
        {
            float side = r.Chance(0.5f) ? 1f : -1f;
            Vector2 camp = _door + new Vector2(side * r.Range(5f, 7f), r.Range(2f, 4f));
            Tries(10, () => PutBox(new PlaceSpec(PlacePiece.Tent, NextSeed(), PlaceSpec.Cm(r.Range(4f, 4.8f)), 1), W(camp + new Vector2(side * r.Range(1f, 3f), r.Range(-1f, 1f))), YawOf(new Vector2(-side, 0.4f).Normalized()), new Vector2(3.6f, 3.4f), 3.4f, 0.4f, 0.4f));
            Tries(10, () => PutRound(new PlaceSpec(PlacePiece.Brazier, NextSeed(), PlaceSpec.Cm(r.Range(1f, 1.2f))), W(camp + new Vector2(-side * r.Range(2f, 3f), r.Range(1f, 2.5f))), 0f, 0.8f, 1.4f, 0.3f, 0.4f));
            Paint(GroundPaint.Yard, Footprint.Circle(W(camp), 4f), 2f);
            Tries(8, () => PutBox(new PlaceSpec(PlacePiece.WeaponRack, NextSeed(), PlaceSpec.Cm(r.Range(2f, 2.6f)), 1), W(camp + new Vector2(r.Range(-2f, 2f), r.Range(-3f, -2f))), YawOf(Vector2.UnitY), new Vector2(1.5f, 0.6f), 2f, 0.3f, 0.4f));
            Tries(8, () => PutBox(new PlaceSpec(PlacePiece.Crates, NextSeed(), r.Int(2, 4), 1), W(camp + new Vector2(side * r.Range(-1f, 3f), r.Range(3f, 4.5f))), r.Float() * MathF.Tau, new Vector2(1.8f, 1.1f), 1.6f, 0.3f, 0.4f));
            // The keepers' cart that brought the beacon's wood up, stopped on the way and gone through, a wheel off it.
            Tries(8, () => PutBox(new PlaceSpec(PlacePiece.Wagon, NextSeed(), 1, 0, PlaceSpec.Cm(r.Range(3.4f, 4.2f))), W(_tower + _road * (Site.Radius * r.Range(0.45f, 0.6f)) + new Vector2(-_road.Y, _road.X) * ((r.Chance(0.5f) ? 1f : -1f) * r.Range(3f, 4.5f))), YawOf(_road) + r.Range(-0.4f, 0.4f), new Vector2(2.4f, 3.4f), 2.4f, 0.3f, 0.4f));
            // The barricade across the way up from the road, a gap left at one end to pass.
            Vector2 way = _tower + _road * (Site.Radius * 0.72f);
            Vector2 across = new Vector2(-_road.Y, _road.X);
            Tries(6, () => PutBox(new PlaceSpec(PlacePiece.Barricade, NextSeed(), PlaceSpec.Cm(r.Range(3.4f, 4.4f)), 1), W(way + across * ((r.Chance(0.5f) ? 1f : -1f) * r.Range(2.4f, 3.2f))), YawOf(_road), new Vector2(2.2f, 1.1f), 1.5f, 0.2f, 0.45f));
            PutRound(new PlaceSpec(PlacePiece.Lantern, NextSeed(), PlaceSpec.Cm(3.6f), 1), W(_door + new Vector2(1.8f, -0.6f)), YawOf(-Vector2.UnitY), 0.4f, 0f, 0.2f, 0.45f);
        }

        // Guards at the door and by their fire, and the chest of what they took by the keepers' house.
        private void Hold(WorldRandom r)
        {
            Stand(SpawnRole.Guard, Around(W(_door + new Vector2(0f, 2f)), 5f, r.Fork("door"), 30), r.Int(1, 2), 3.5f, r.Fork("door guards"));
            Stand(SpawnRole.Guard, Around(W(Vector2.Zero), Site.Radius * 0.55f, r.Fork("yard"), 40), r.Int(1, 2), 3.5f, r.Fork("yard guards"));
            Chest(W(_door + new Vector2(r.Range(-3f, 3f), r.Range(3f, 5f))), Site.Radius * 0.5f);
        }

        // What lies about: what the keepers' last fight left at the door, ash by the brazier, chips by the woodpile.
        private void Litter(WorldRandom r)
        {
            Strew(LitterKind.Gear, _door + new Vector2(r.Range(-2f, 2f), r.Range(1f, 3f)), 2.8f, r.Int(7, 11));
            Strew(LitterKind.Chips, _tower + new Vector2(Beacon.HoistFoot(_radius).X + 1f, 2f), 1.6f, r.Int(14, 22));
            Strew(LitterKind.Papers, Vector2.Zero, 2f, r.Int(3, 6));
        }
    }
}
