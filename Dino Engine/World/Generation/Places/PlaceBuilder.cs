using Dino_Engine.Modelling.Procedural.Architecture.Places;
using Dino_Engine.Modelling.Procedural.Fence;
using Dino_Engine.Modelling.Procedural.Flora;
using Dino_Engine.World.Content;
using OpenTK.Mathematics;

namespace Dino_Engine.World.Generation.Places
{
    /// <summary>
    /// What every place with a story of its own is laid out with (PLACES.md): its frame (<see cref="PlacePlan"/>), the ground checked all over a footprint rather than round the circle that
    /// holds it, every piece put down with exactly the footprint it stands on and how tall it is, so that what looks solid is, and what stands low stops no blow (<c>Sight.Above</c>), the
    /// ground levelled where something wants it level, the people of the place stood where they could be reached, and a chest, as every held place has one. Each place's builder draws every
    /// decision from a fork of its own, so that changing one decision moves nothing else, and nothing is stored: every piece is an id grown again on any machine.
    /// </summary>
    internal abstract class PlaceBuilder
    {
        protected const float Tall = PlacementIndex.Tall;
        protected const float CharacterRoom = 1.2f;     // A character's reach from where it stands, and a little more.

        protected readonly SiteBuilder B;
        protected readonly WorldData World;
        protected readonly WorldSite Site;
        protected readonly WorldRandom Random;
        protected readonly PlacePlan Plan;
        protected readonly List<Vector2> Spots = new();     // Open ground the place's people could stand on.
        private uint _seed;

        protected PlaceBuilder(SiteBuilder b, string name)
        {
            B = b;
            World = b.World;
            Site = b.Site;
            Random = b.Random.Fork(name);
            Plan = Site.Place ??= new PlacePlan { Centre = Site.Position, Yaw = MathF.PI, Half = new Vector2(Site.Radius) };
            _seed = (uint)Random.Fork("pieces").NextULong();
        }

        /// <summary>A seed for the next piece: every piece of a place a model of its own, unless it asks for one it's already had.</summary>
        protected uint NextSeed()
        {
            _seed = _seed * 0x9E3779B1u + 0x7F4A7C15u;
            return _seed >> 4;
        }

        protected Vector2 W(float u, float v) => Plan.ToWorld(u, v);
        protected Vector2 W(Vector2 local) => Plan.ToWorld(local);
        protected Vector2 Dir(Vector2 local) => Plan.Dir(local);

        /// <summary>The yaw that turns a piece's front (+z) the way <paramref name="local"/> points in the place's frame.</summary>
        protected float YawOf(Vector2 local) => SiteBuilder.YawFacing(Plan.Dir(local));

        protected BiomeDefinition Biome => World.Plan.Biomes[Math.Clamp(Site.Biome, 0, World.Plan.Biomes.Count - 1)];

        protected bool Snowy => Biome.Ground == SurfaceMaterial.Snow;

        // ---------------------------------------------------------------- The ground

        /// <summary>
        /// Whether the land under a footprint can carry something, looked at all over it rather than round the circle that holds it, which for a long hull reaches far past its sides.
        /// <paramref name="wet"/> lets it stand in water as deep as that, as a wreck's stern does.
        /// </summary>
        protected bool Ground(in Footprint f, float maxSlope, float wet = -1f)
        {
            if (f.IsCircle) return wet < 0f ? B.GroundFits(f, maxSlope) : GroundAt(f.Centre, maxSlope, wet) && GroundAt(f.Centre + new Vector2(f.HalfExtents.X, 0f), maxSlope, wet) && GroundAt(f.Centre - new Vector2(f.HalfExtents.X, 0f), maxSlope, wet);
            int nx = Math.Max(1, (int)MathF.Ceiling(f.HalfExtents.X / 2.5f)), nz = Math.Max(1, (int)MathF.Ceiling(f.HalfExtents.Y / 2.5f));
            for (int i = -nx; i <= nx; i++)
            {
                for (int j = -nz; j <= nz; j++)
                {
                    if (!GroundAt(f.ToWorld(new Vector2(f.HalfExtents.X * i / nx, f.HalfExtents.Y * j / nz)), maxSlope, wet)) return false;
                }
            }
            return true;
        }

        private bool GroundAt(Vector2 p, float maxSlope, float wet)
        {
            if (!World.InBounds(p)) return false;
            float height = World.Elevation.Sample(p);
            if (wet < 0f)
            {
                if (height < WorldPlan.SeaLevel + 0.4f || World.Reachable.Sample(p) < 0.5f) return false;
            }
            else if (height < WorldPlan.SeaLevel - wet) return false;
            return World.Elevation.SlopeAt(p) <= maxSlope;
        }

        /// <summary>Whether nothing of <paramref name="layers"/> is within <paramref name="padding"/> of a footprint.</summary>
        protected bool Clear(in Footprint f, PlacementLayer layers = PlacementLayer.Built | PlacementLayer.Road, float padding = 0.3f) => !World.Placement.Overlaps(f, layers, padding);

        /// <summary>The ground's height averaged over a footprint, which is what levelling it lays it to.</summary>
        protected float AverageGround(in Footprint f)
        {
            float total = World.Elevation.Sample(f.Centre);
            int samples = 1;
            float reach = f.BoundingRadius * 0.7f;
            for (int i = 0; i < 6; i++)
            {
                total += World.Elevation.Sample(f.Centre + SiteBuilder.Direction(i * (MathF.Tau / 6f)) * reach);
                samples++;
            }
            return total / samples;
        }

        // ---------------------------------------------------------------- Pieces

        /// <summary>
        /// Puts a piece down if the ground and what's already there let it: <paramref name="footprint"/> is what it stands on and is solid up to <paramref name="height"/> (0 for a post, which is
        /// walked round and stops nothing in the air), in <paramref name="layer"/>; <paramref name="floor"/>, if given, levels the ground under it to that height and stands it there.
        /// </summary>
        protected bool Put(PlaceSpec spec, Vector2 at, float yaw, in Footprint footprint, float height, PlacementLayer layer = PlacementLayer.Prop, float maxSlope = 0.45f,
            float padding = 0.3f, PlacementLayer blockers = PlacementLayer.Built | PlacementLayer.Road | PlacementLayer.Tree, float? floor = null, bool solid = true, float wet = -1f, float scale = 1f)
        {
            if (!Ground(footprint, maxSlope, wet) || !Clear(footprint, blockers, padding)) return false;
            LayOut(spec, at, yaw, footprint, layer, height, solid, scale);
            if (floor is float level) World.Pads.Add(new GroundPad(footprint.Grown(0.4f), level, 3f));
            World.Pending.Add(new PendingObject(spec.Id, at, yaw, new Vector3(scale), floor));
            return true;
        }

        private readonly List<HitPart> _parts = new();

        /// <summary>
        /// Keeps a piece's footprint for it, which nothing else is laid out on, and makes solid what's solid about it (<see cref="Hitboxes"/>): where its model stands in the way,
        /// and nowhere else, so that a tent's guys, a cart's pole or a barrel by a door don't shut anyone out of anywhere. What's put down as not solid isn't.
        /// </summary>
        protected void LayOut(in PlaceSpec spec, Vector2 at, float yaw, in Footprint footprint, PlacementLayer layer, float height, bool solid = true, float scale = 1f)
        {
            HitKind kind = solid ? Hitboxes.Of(spec, _parts) : HitKind.Open;
            Hitboxes.Put(World.Placement, kind, _parts, footprint, layer, height, at, yaw, scale);
        }

        /// <summary>Puts a piece down as a box of half its size about <paramref name="at"/>, turned by <paramref name="yaw"/>.</summary>
        protected bool PutBox(PlaceSpec spec, Vector2 at, float yaw, Vector2 half, float height, float padding = 0.3f, float maxSlope = 0.45f, PlacementLayer layer = PlacementLayer.Prop, float? floor = null, bool solid = true)
            => Put(spec, at, yaw, Footprint.Box(at, half, yaw), height, layer, maxSlope, padding, floor: floor, solid: solid);

        /// <summary>Puts a piece down as a circle about <paramref name="at"/>.</summary>
        protected bool PutRound(PlaceSpec spec, Vector2 at, float yaw, float radius, float height, float padding = 0.3f, float maxSlope = 0.45f, bool solid = true)
            => Put(spec, at, yaw, Footprint.Circle(at, radius), height, PlacementLayer.Prop, maxSlope, padding, solid: solid);

        /// <summary>
        /// Something lying low on the ground that nothing walks round and no blow stops, shells, a cloth, a coil of rope: drawn, and kept clear of only what's built or growing. It's still
        /// in the index, so that nothing else is put on top of it.
        /// </summary>
        protected bool Lay(PlaceSpec spec, Vector2 at, float yaw, float radius, float maxSlope = 0.55f)
        {
            Footprint f = Footprint.Circle(at, radius);
            if (!Ground(f, maxSlope) || World.Placement.Overlaps(f, PlacementLayer.Structure | PlacementLayer.Prop | PlacementLayer.Fence | PlacementLayer.Road | PlacementLayer.Tree, 0.05f)) return false;
            World.Placement.Add(f, PlacementLayer.Prop, solid: false, height: 0f);
            World.Pending.Add(new PendingObject(spec.Id, at, yaw, Vector3.One, null));
            return true;
        }

        /// <summary>A structure of the catalog, as any site puts one down, in the place's own style.</summary>
        protected bool Structure(StructureDefinition definition, Vector2 at, float yaw, float padding = 0.6f, float maxSlope = 0.42f) => B.Place(definition, at, yaw, padding: padding, maxSlope: maxSlope);

        /// <summary>A soft patch of painted ground.</summary>
        protected void Paint(GroundPaint paint, in Footprint shape, float softness) => B.Paint(paint, shape, softness);

        /// <summary>Ground kept clear of anything put down after, and of trees, as a yard or a way in is.</summary>
        protected void Keep(in Footprint f) => World.Placement.Add(f, PlacementLayer.Keepout);

        /// <summary>
        /// A way worn between two points of the place's frame, painted <paramref name="width"/> across and fraying at its edges, as feet wear it from a gate to a fire or a door: what
        /// says from 33 m up how a place is used, as a churchyard's paths do (CHURCHYARD.md).
        /// </summary>
        protected void Trail(Vector2 from, Vector2 to, float width, GroundPaint paint = GroundPaint.Yard, float softness = 1.6f)
        {
            Vector2 a = W(from), c = W(to);
            Vector2 d = c - a;
            float length = d.Length;
            if (length < 0.2f) return;
            Paint(paint, Footprint.Box((a + c) * 0.5f, new Vector2(width * 0.5f, length * 0.5f + width * 0.3f), SiteBuilder.YawFacing(d / length)), softness);
        }

        private WorldRandom? _strew;

        /// <summary>
        /// Litter of a kind strewn over a patch of the place's frame (<see cref="LitterKind"/>): straw round a pen, chips round a sawpit, bones round a nest. It lies below anything that
        /// stops a blow and is walked through, so it's laid last, under and round whatever's there, and kept off a road and off ground too steep for a patch of it to lie on.
        /// <paramref name="stone"/> is the colour of a stone's spalls or rubble.
        /// </summary>
        protected void Strew(LitterKind kind, Vector2 local, float radius, int count, (int R, int G, int B)? stone = null)
        {
            _strew ??= Random.Fork("strew");
            Vector2 at = W(local);
            radius = MathF.Min(radius, 4.5f);
            if (!World.InBounds(at) || World.Elevation.Sample(at) < WorldPlan.SeaLevel + 0.2f) return;
            // A patch lies flat in the model, so the steeper the ground the smaller it has to be, or its edge floats.
            float slope = World.Elevation.SlopeAt(at);
            if (slope > 0.4f) return;
            if (slope > 0.06f) radius = MathF.Min(radius, 0.18f / slope + 0.4f);
            if (World.Placement.Overlaps(Footprint.Circle(at, 0.5f), PlacementLayer.Road, 0f)) return;
            int[] values = stone is var (r, g, b) ? new[] { (int)kind, PlaceSpec.Cm(radius), count, r, g, b } : new[] { (int)kind, PlaceSpec.Cm(radius), count };
            World.Pending.Add(new PendingObject(new PlaceSpec(PlacePiece.Litter, NextSeed(), values).Id, at, _strew.Float() * MathF.Tau, Vector3.One, null));
        }

        /// <summary>Something that lies flat with nothing to stop and nothing to stand on it, a puddle, duckboards, a cloth: drawn where the ground will take it, and in nobody's way.</summary>
        protected bool LayFlat(PlaceSpec spec, Vector2 local, float yaw, float radius, float maxSlope = 0.12f)
        {
            Vector2 at = W(local);
            if (!World.InBounds(at) || World.Elevation.SlopeAt(at) > maxSlope || World.Elevation.Sample(at) < WorldPlan.SeaLevel + 0.3f) return false;
            if (World.Placement.Overlaps(Footprint.Circle(at, radius * 0.6f), PlacementLayer.Road | PlacementLayer.Structure, 0f)) return false;
            World.Pending.Add(new PendingObject(spec.Id, at, yaw, Vector3.One, null));
            return true;
        }

        /// <summary>The colour of the land's own stone, freshly cut, as the cliffs are drawn in (<see cref="PlaceMaterials.StoneOf"/>), per mille.</summary>
        protected (int R, int G, int B) LandStone => PlaceMaterials.StoneOf(World.Geology);

        // ---------------------------------------------------------------- Trees and what grows

        /// <summary>Plants one of the land's own plants somewhere if it fits, as the land would grow it, and keeps its trunk solid.</summary>
        protected bool Plant(WorldAsset asset, Vector2 at, float scale, float yaw)
        {
            float trunk = MathF.Max(asset.SolidRadius * scale, 0.35f);
            Footprint f = Footprint.Circle(at, trunk + 0.4f);
            if (!B.GroundFits(f, 0.45f) || World.Placement.Overlaps(f, PlacementLayer.Built | PlacementLayer.Road | PlacementLayer.Tree, 0.2f)) return false;
            World.Placement.Add(Footprint.Circle(at, trunk + 0.5f), PlacementLayer.Tree);
            if (asset.SolidRadius > 0f) World.Placement.Add(Footprint.Circle(at, trunk), PlacementLayer.None, solid: true);
            World.Pending.Add(new PendingObject(NatureCatalog.ModelAt(asset, World.Plan.Seed, at), at, yaw, new Vector3(scale), null));
            return true;
        }

        /// <summary>The tree the woods round the place are mostly of, by its biome: what a camp fells and a nest is built of.</summary>
        protected PlantSpecies? CanopyTree()
        {
            Species[] canopy = Biome.Canopy;
            float best = -1f;
            PlantSpecies? found = null;
            foreach (Species species in canopy)
            {
                if (species.Weight <= best || !FloraCatalog.TryGet(species.Asset.Id, out PlantSpecies plant) || plant.Kind != WorldAssetKind.Tree) continue;
                best = species.Weight;
                found = plant;
            }
            return found;
        }

        // ---------------------------------------------------------------- Walls and gates

        /// <summary>A side of a walled place, in its own frame: its back is -z, its front +z, its left -x.</summary>
        protected enum Side { Back, Front, Left, Right }

        protected static Vector2 Outward(Side side) => side switch
        {
            Side.Back => -Vector2.UnitY,
            Side.Front => Vector2.UnitY,
            Side.Left => -Vector2.UnitX,
            _ => Vector2.UnitX,
        };

        /// <summary>
        /// Where a walled place's gate goes, on its side of a box of half size <paramref name="half"/>: the side the road comes from, so whoever comes up the road comes straight in, and along it
        /// towards where the road meets it. Recorded on the plan.
        /// </summary>
        protected (Side Side, Vector2 Local) GateOnRoad(Vector2 half)
        {
            Vector2 road = Plan.ToLocal(Site.Position + Site.RoadDirection);
            road = road.LengthSquared > 1e-4f ? road.Normalized() : Vector2.UnitY;
            Side side = MathF.Abs(road.X) / half.X > MathF.Abs(road.Y) / half.Y ? (road.X > 0f ? Side.Right : Side.Left) : (road.Y > 0f ? Side.Front : Side.Back);
            float along = (side is Side.Front or Side.Back ? road.X * half.X : road.Y * half.Y) * 0.5f;
            Vector2 local = side switch
            {
                Side.Back => new Vector2(Math.Clamp(along, -half.X + 8f, half.X - 8f), -half.Y),
                Side.Front => new Vector2(Math.Clamp(along, -half.X + 8f, half.X - 8f), half.Y),
                Side.Left => new Vector2(-half.X, Math.Clamp(along, -half.Y + 8f, half.Y - 8f)),
                _ => new Vector2(half.X, Math.Clamp(along, -half.Y + 8f, half.Y - 8f)),
            };
            Plan.Gates.Add((W(local), Plan.Dir(Outward(side))));
            return (side, local);
        }

        /// <summary>A wall or fence of a use round a box of half size <paramref name="half"/> about the middle, broken where the gate is, and the gate's way kept clear inside and out.</summary>
        protected void Enclose(FenceUse use, Vector2 half, Side gateSide, Vector2 gateLocal, float gateHalf)
        {
            Vector2[] corners = { new(-half.X, -half.Y), new(half.X, -half.Y), new(half.X, half.Y), new(-half.X, half.Y) };
            Side[] sides = { Side.Back, Side.Right, Side.Front, Side.Left };
            for (int i = 0; i < 4; i++)
            {
                Vector2 a = corners[i], c = corners[(i + 1) % 4];
                if (sides[i] != gateSide)
                {
                    B.Wall(use, W(a), W(c), Plan.Centre);
                    continue;
                }
                Vector2 along = (c - a).Normalized();
                float t = Vector2.Dot(gateLocal - a, along);
                B.Wall(use, W(a), W(a + along * (t - gateHalf)), Plan.Centre);
                B.Wall(use, W(a + along * (t + gateHalf)), W(c), Plan.Centre);
            }
            Vector2 inward = -Outward(gateSide);
            Keep(Footprint.Box(W(gateLocal + inward * 5f), new Vector2(gateHalf, 6f), YawOf(inward)));
            Keep(Footprint.Box(W(gateLocal - inward * 3f), new Vector2(gateHalf, 3f), YawOf(inward)));
        }

        /// <summary>The Reich's banners either side of a gate, outside it, and its lanterns inside, as at every place it holds.</summary>
        protected void ReichGate(Side gateSide, Vector2 gateLocal, float gateHalf, WorldRandom r)
        {
            Vector2 outward = Plan.Dir(Outward(gateSide));
            Vector2 across = new Vector2(-outward.Y, outward.X);
            Vector2 inward = -Outward(gateSide);
            foreach (float s in new[] { -1f, 1f })
            {
                PutRound(new PlaceSpec(PlacePiece.Banner, NextSeed(), PlaceSpec.Cm(r.Range(6.4f, 7.4f)), 1), W(gateLocal) + outward * 2.6f + across * (s * (gateHalf + 1.6f)), SiteBuilder.YawFacing(outward), 0.5f, 0f, 0.2f, 0.5f);
                PutRound(new PlaceSpec(PlacePiece.Lantern, NextSeed(), PlaceSpec.Cm(4.2f), 1), W(gateLocal + inward * 2.2f) + across * (s * (gateHalf - 0.6f)), SiteBuilder.YawFacing(-outward), 0.4f, 0f, 0.2f, 0.5f);
            }
        }

        /// <summary>
        /// The tree worth felling that the woods round the place are mostly of, weighed over its own biome and its neighbours' out to a stone's throw by how much of each is woods worth
        /// felling, so that a camp at the edge of a bamboo thicket fells the pines beyond it. Null if nothing near is worth felling.
        /// </summary>
        protected PlantSpecies? TimberTree()
        {
            var score = new float[World.BiomeWeights.Length];
            for (int i = 0; i <= 16; i++)
            {
                Vector2 at = i == 0 ? Site.Position : Site.Position + SiteBuilder.Direction(i * MathF.Tau / 8f) * (i <= 8 ? 30f : 60f);
                if (!World.InBounds(at)) continue;
                for (int b = 0; b < score.Length; b++) score[b] += World.BiomeWeights[b].Sample(at);
            }
            PlantSpecies? found = null;
            float best = 0f;
            for (int b = 0; b < score.Length; b++)
            {
                BiomeDefinition biome = World.Plan.Biomes[b];
                float weight = score[b] * biome.ForestCover * PlaceStory.TimberShare(biome);
                if (weight <= best || PlaceStory.TimberOf(biome) is not PlantSpecies tree) continue;
                best = weight;
                found = tree;
            }
            return found;
        }

        /// <summary>
        /// The bark of the wood round the place, as the ids of what's cut from it carry it: its kind and its colour, per mille, from the tree worth felling its woods are mostly of
        /// (<see cref="TimberTree"/>), or an oak's if nothing near is.
        /// </summary>
        protected int[] WoodOf()
        {
            BarkKind kind = BarkKind.Furrowed;
            Vector3 colour = new Vector3(0.8f, 0.72f, 0.62f);
            if (TimberTree() is PlantSpecies tree && tree.Form is TreeForm form)
            {
                kind = form.Bark.Kind;
                colour = form.Bark.Colour;
            }
            return new[] { (int)kind, (int)MathF.Round(colour.X * 1000f), (int)MathF.Round(colour.Y * 1000f), (int)MathF.Round(colour.Z * 1000f) };
        }

        /// <summary>A piece's numbers followed by the wood's bark, for what's cut from the wood round the place.</summary>
        protected static int[] With(int[] numbers, int[] wood) => numbers.Concat(wood).ToArray();

        // ---------------------------------------------------------------- People

        /// <summary>Whether someone could stand here: ground they can walk, and nothing solid within a body's reach.</summary>
        protected bool CanStand(Vector2 at)
        {
            Footprint standing = Footprint.Circle(at, CharacterRoom);
            return B.GroundFits(standing, 0.55f) && !World.Placement.Overlaps(standing, PlacementLayer.Structure | PlacementLayer.Prop | PlacementLayer.Fence);
        }

        /// <summary>Marks some of the place's people of <paramref name="role"/> on spots of its own, at least <paramref name="apart"/> from each other and from anyone already standing.</summary>
        protected int Stand(SpawnRole role, IEnumerable<Vector2> spots, int count, float apart, WorldRandom random)
        {
            int placed = 0;
            List<Vector2> taken = Site.Spawns.Select(s => s.Position).ToList();
            foreach (Vector2 spot in spots.OrderBy(_ => random.Float()))
            {
                if (placed >= count) break;
                if (taken.Any(t => (t - spot).Length < apart) || !CanStand(spot)) continue;
                B.Spawn(role, spot);
                taken.Add(spot);
                placed++;
            }
            return placed;
        }

        /// <summary>Open spots round a point, out to <paramref name="reach"/>, on a jittered ring, for those who stand about a place.</summary>
        protected IEnumerable<Vector2> Around(Vector2 centre, float reach, WorldRandom random, int count = 40)
        {
            for (int i = 0; i < count; i++)
            {
                float a = random.Float() * MathF.Tau;
                yield return centre + SiteBuilder.Direction(a) * (reach * MathF.Sqrt(random.Range(0.05f, 1f)));
            }
        }

        /// <summary>Someone a place can't do without, a trader or a keeper, wherever there's ground for them, nearest where they're wanted: false only if there's none within reach.</summary>
        protected bool StandNear(SpawnRole role, Vector2 wanted, float reach)
        {
            for (float r = 0f; r <= reach; r += 1f)
            {
                int directions = r <= 0f ? 1 : Math.Max(8, (int)(r * 2f));
                for (int i = 0; i < directions; i++)
                {
                    Vector2 at = wanted + SiteBuilder.Direction(i * MathF.Tau / directions) * r;
                    if (!CanStand(at) || Site.Spawns.Any(s => (s.Position - at).Length < 2f)) continue;
                    B.Spawn(role, at);
                    return true;
                }
            }
            return false;
        }

        /// <summary>Every place a land asks to be freed has a chest of what its holders took, so there has to be one, wherever there's ground for it, nearest where it's wanted.</summary>
        protected void Chest(Vector2 wanted, float reach)
        {
            for (float r = 0f; r <= reach; r += 1.5f)
            {
                int directions = r <= 0f ? 1 : Math.Max(8, (int)(r * 1.6f));
                for (int i = 0; i < directions; i++)
                {
                    Vector2 at = wanted + SiteBuilder.Direction(i * MathF.Tau / directions) * r;
                    if (!CanStand(at)) continue;
                    B.Spawn(SpawnRole.Chest, at);
                    return;
                }
            }
            B.Spawn(SpawnRole.Chest, wanted);
        }

        /// <summary>Tries something up to <paramref name="attempts"/> times, each drawing a place of its own, until it fits.</summary>
        protected static bool Tries(int attempts, Func<bool> attempt)
        {
            for (int i = 0; i < attempts; i++)
            {
                if (attempt()) return true;
            }
            return false;
        }

        /// <summary>A pick by weight from a few things.</summary>
        protected static T Pick<T>(WorldRandom random, params (T Item, float Weight)[] items)
        {
            float total = items.Sum(i => i.Weight), roll = random.Float() * total;
            foreach ((T item, float weight) in items)
            {
                roll -= weight;
                if (roll <= 0f) return item;
            }
            return items[^1].Item;
        }
    }
}
