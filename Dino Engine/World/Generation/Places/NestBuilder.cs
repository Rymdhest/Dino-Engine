using Dino_Engine.Modelling.Procedural.Architecture.Places;
using Dino_Engine.Modelling.Procedural.Flora;
using Dino_Engine.World.Content;
using OpenTK.Mathematics;

namespace Dino_Engine.World.Generation.Places
{
    /// <summary>
    /// Lays out a nesting ground (PLACES.md), deep in the woods where what got out of the pens found it could breed. Raptors heap a ring of mounds round a trampled clearing, each with its clutch,
    /// and drag back what they kill to the middle of it; a ravager scrapes one great nest and rings it with bones; waders heap reeds and mud at the water's edge; and the Institute's weavers string
    /// their webs between the trunks round their lair, hang their egg sacs in them and what they've caught. The ground is trampled bare, the shells of what's hatched lie about, and the trees close
    /// in round it. Whatever nests there guards it.
    /// </summary>
    internal sealed class NestBuilder : PlaceBuilder
    {
        private NestKind _kind;
        private readonly List<(Vector2 At, float Radius)> _nests = new();

        private NestBuilder(SiteBuilder b) : base(b, "nest") { }

        public static void Build(SiteBuilder b) => new NestBuilder(b).Lay();

        private void Lay()
        {
            _kind = PlaceStory.NestOf(World, Site);
            Plan.Yaw = Random.Float() * MathF.Tau;
            Plan.Half = new Vector2(Site.Radius * 0.8f);
            Paint(GroundPaint.Yard, Footprint.Circle(Site.Position, Site.Radius * 0.62f), 7f);
            switch (_kind)
            {
                case NestKind.Ravager: Scrape(Random.Fork("scrape")); break;
                case NestKind.Weaver: Lair(Random.Fork("lair")); break;
                default: Mounds(Random.Fork("mounds")); break;
            }
            Leavings(Random.Fork("leavings"));
            Wild(Random.Fork("wild"));
            Hold(Random.Fork("holders"));
        }

        // A ring of mounds round the clearing, each with its clutch, a wader's heaped of reeds.
        private void Mounds(WorldRandom r)
        {
            int count = r.Int(4, 6);
            float ring = Site.Radius * r.Range(0.32f, 0.42f);
            float start = r.Float() * MathF.Tau;
            for (int k = 0; k < count; k++)
            {
                for (int attempt = 0; attempt < 5; attempt++)
                {
                    float a = start + k * MathF.Tau / count + r.Range(-0.25f, 0.25f);
                    Vector2 at = Site.Position + SiteBuilder.Direction(a) * ring * r.Range(0.85f, 1.15f);
                    float radius = r.Range(1.7f, 2.3f);
                    int eggs = _kind == NestKind.Wader ? r.Int(4, 6) : r.Int(6, 10);
                    if (!PutRound(new PlaceSpec(PlacePiece.NestMound, NextSeed(), (int)_kind, eggs, PlaceSpec.Cm(radius)), at, r.Float() * MathF.Tau, radius + 0.3f, 0.8f, 0.6f, 0.4f)) continue;
                    _nests.Add((at, radius));
                    Plan.Marks.Add(new PlaceMark(PlaceMarkKind.Nest, at, 0f, new Vector2(radius)));
                    break;
                }
            }
            // A ring the ground broke up is made up anywhere in the clearing that'll take a mound, smaller, as a brood nests where it can.
            for (int attempt = 0; attempt < 24 && _nests.Count < 4; attempt++)
            {
                Vector2 at = Site.Position + SiteBuilder.Direction(r.Float() * MathF.Tau) * Site.Radius * r.Range(0.1f, 0.6f);
                float radius = r.Range(1.4f, 1.9f);
                if (_nests.Any(n => (n.At - at).Length < n.Radius + radius + 1.5f)) continue;
                if (!PutRound(new PlaceSpec(PlacePiece.NestMound, NextSeed(), (int)_kind, r.Int(5, 8), PlaceSpec.Cm(radius)), at, r.Float() * MathF.Tau, radius + 0.3f, 0.8f, 0.6f, 0.45f)) continue;
                _nests.Add((at, radius));
                Plan.Marks.Add(new PlaceMark(PlaceMarkKind.Nest, at, 0f, new Vector2(radius)));
            }
        }

        // One great scrape in the middle, ringed with what it's eaten, and the trees it snapped off pushing its way in, their tops lying beside them.
        private void Scrape(WorldRandom r)
        {
            float radius = r.Range(3.6f, 4.6f);
            Vector2 scrape = Site.Position;
            for (int attempt = 0; attempt < 12; attempt++)
            {
                Vector2 at = Site.Position + SiteBuilder.Direction(r.Float() * MathF.Tau) * attempt * 1.5f;
                if (!PutRound(new PlaceSpec(PlacePiece.NestMound, NextSeed(), (int)NestKind.Ravager, r.Int(9, 13), PlaceSpec.Cm(radius)), at, r.Float() * MathF.Tau, radius + 0.3f, 0.8f, 0.6f, 0.4f)) continue;
                _nests.Add((at, radius));
                Plan.Marks.Add(new PlaceMark(PlaceMarkKind.Nest, at, 0f, new Vector2(radius)));
                scrape = at;
                break;
            }
            // The ring of kills round it, picked to the ribs, the nearest still red.
            int kills = r.Int(3, 4);
            float start = r.Float() * MathF.Tau;
            for (int k = 0; k < kills; k++)
            {
                for (int attempt = 0; attempt < 6; attempt++)
                {
                    float a = start + k * MathF.Tau / kills + r.Range(-0.35f, 0.35f);
                    Vector2 at = scrape + SiteBuilder.Direction(a) * (radius + r.Range(3.5f, 7f));
                    float length = r.Range(5f, 8f);
                    // Lying round it like spokes, heads in.
                    float yaw = SiteBuilder.YawFacing(scrape - at) + r.Range(-0.5f, 0.5f);
                    if (PutBox(new PlaceSpec(PlacePiece.Carcass, NextSeed(), r.Chance(0.7f) ? 0 : 1, PlaceSpec.Cm(length)), at, yaw, new Vector2(length * 0.25f, length * 0.5f), 0.9f, 0.4f, 0.45f)) break;
                }
            }
            Snapped(r.Fork("snapped"), r.Int(2, 4));
        }

        // Trees snapped off head high where something great pushed through, their splintered stumps standing and their tops lying where they fell, pointing the way it went.
        private void Snapped(WorldRandom r, int count)
        {
            int[] wood = WoodOf();
            float way = r.Float() * MathF.Tau;
            for (int k = 0, tries = 0; k < count && tries < count * 6; tries++)
            {
                Vector2 at = Site.Position + SiteBuilder.Direction(way + r.Range(-0.8f, 0.8f)) * Site.Radius * r.Range(0.45f, 0.75f);
                float radius = r.Range(0.3f, 0.5f), height = r.Range(1.4f, 2.4f);
                if (!PutRound(new PlaceSpec(PlacePiece.Stump, NextSeed(), With(new[] { PlaceSpec.Cm(radius), PlaceSpec.Cm(height) }, wood)), at, r.Float() * MathF.Tau, radius + 0.3f, height, 0.3f, 0.5f)) continue;
                k++;
                // Its top, fallen away from the way the beast came.
                Vector2 fall = SiteBuilder.Direction(way + r.Range(-0.5f, 0.5f));
                float length = r.Range(8f, 13f);
                Vector2 middle = at + fall * (length * 0.5f + radius + 0.6f);
                PutBox(new PlaceSpec(PlacePiece.Log, NextSeed(), With(new[] { PlaceSpec.Cm(length), PlaceSpec.Cm(radius * 0.92f) }, wood)), middle, SiteBuilder.YawFacing(fall) - MathF.PI * 0.5f, new Vector2(length * 0.5f, radius + 0.2f), 0.9f, 0.2f, 0.45f);
            }
        }

        // A weaver's lair: trees ringing a clearing open to the sky, their trunks wound in silk, webs strung between them with sheets slung under, egg sacs hung in the webs and what's been
        // caught hung from the boughs, shells about the middle.
        private void Lair(WorldRandom r)
        {
            PlantSpecies? tree = CanopyTree();
            var trunks = new List<(Vector2 At, float Radius)>();
            if (tree != null)
            {
                int count = r.Int(9, 12);
                for (int k = 0; k < count * 4 && trunks.Count < count; k++)
                {
                    Vector2 at = Site.Position + SiteBuilder.Direction(r.Float() * MathF.Tau) * Site.Radius * r.Range(0.62f, 0.88f);
                    if (trunks.Any(t => (t.At - at).Length < 5.5f)) continue;
                    float scale = tree.Scale.Lerp(r.Range(0.5f, 0.9f));
                    if (Plant(tree.Asset, at, scale, r.Float() * MathF.Tau)) trunks.Add((at, MathF.Max(tree.Asset.SolidRadius * scale, 0.18f)));
                }
            }
            // A web between each pair of near trunks, round the clearing, and silk wound round each trunk one is anchored to.
            var ordered = trunks.OrderBy(t => MathF.Atan2(t.At.Y - Site.Position.Y, t.At.X - Site.Position.X)).ToList();
            var wound = new HashSet<int>();
            for (int k = 0; k < ordered.Count; k++)
            {
                Vector2 a = ordered[k].At, c = ordered[(k + 1) % ordered.Count].At;
                float span = (c - a).Length;
                if (span < 4f || span > 16f) continue;
                Vector2 mid = (a + c) * 0.5f;
                Vector2 along = (c - a) / span;
                // Its own x along from one trunk to the other.
                float yaw = SiteBuilder.YawFacing(new Vector2(-along.Y, along.X)) - MathF.PI;
                // A web is threads: nothing walks round it, and a blow goes through.
                World.Pending.Add(new PendingObject(new PlaceSpec(PlacePiece.Web, NextSeed(), PlaceSpec.Cm(span - 1f), PlaceSpec.Cm(r.Range(4.5f, 7f)), r.Chance(0.65f) ? 1 : 0).Id, mid, yaw, Vector3.One, null));
                Plan.Marks.Add(new PlaceMark(PlaceMarkKind.Web, mid, yaw, new Vector2(span * 0.5f, 0.5f)));
                foreach (int end in new[] { k, (k + 1) % ordered.Count })
                {
                    if (!wound.Add(end)) continue;
                    World.Pending.Add(new PendingObject(new PlaceSpec(PlacePiece.Wrap, NextSeed(), PlaceSpec.Cm(ordered[end].Radius), PlaceSpec.Cm(r.Range(2.5f, 5f))).Id, ordered[end].At, r.Float() * MathF.Tau, Vector3.One, null));
                }
                Vector2 sac = mid + along * r.Range(-1f, 1f);
                if (r.Chance(0.6f)) World.Pending.Add(new PendingObject(new PlaceSpec(PlacePiece.EggSac, NextSeed(), PlaceSpec.Cm(r.Range(0.6f, 1.1f))).Id, sac, r.Float() * MathF.Tau, Vector3.One, null));
                if (r.Chance(0.45f)) World.Pending.Add(new PendingObject(new PlaceSpec(PlacePiece.Cocoon, NextSeed(), PlaceSpec.Cm(r.Range(1.8f, 3.2f))).Id, mid - along * r.Range(1f, 2f), r.Float() * MathF.Tau, Vector3.One, null));
            }
            // Its floor spun over the middle of the clearing, the dark mouth of its burrow in the middle of that, and smaller sheets spun at the roots of the trunks its webs come
            // down to: pale on the trodden earth, what says from up there what lives here before anything moves.
            // The floor is spun in pieces, each laid on its own bit of ground, so that all of it lies on the ground however the ground lies: the burrow's, and sheets round it.
            float floor = r.Range(4.2f, 5.6f);
            Vector2 burrow = Site.Position;
            for (int attempt = 0; attempt < 9; attempt++)
            {
                Vector2 at = attempt == 0 ? Site.Position : Site.Position + SiteBuilder.Direction(r.Float() * MathF.Tau) * r.Range(1.5f, 6f);
                if (!LayFlat(new PlaceSpec(PlacePiece.SilkFloor, NextSeed(), PlaceSpec.Cm(r.Range(2.6f, 3.2f)), 1), Plan.ToLocal(at), r.Float() * MathF.Tau, 3f, 0.2f)) continue;
                burrow = at;
                break;
            }
            int round = r.Int(4, 6);
            float turn = r.Float() * MathF.Tau;
            for (int k = 0; k < round; k++)
            {
                Vector2 at = burrow + SiteBuilder.Direction(turn + k * MathF.Tau / round + r.Range(-0.3f, 0.3f)) * r.Range(2.8f, floor - 0.6f);
                LayFlat(new PlaceSpec(PlacePiece.SilkFloor, NextSeed(), PlaceSpec.Cm(r.Range(1.9f, 2.7f)), 0), Plan.ToLocal(at), r.Float() * MathF.Tau, 2.3f, 0.3f);
            }
            for (int k = 0; k < ordered.Count; k += 2)
            {
                (Vector2 trunk, float girth) = ordered[k];
                Vector2 inward = (Site.Position - trunk).Normalized();
                float sheet = r.Range(1.8f, 2.8f);
                LayFlat(new PlaceSpec(PlacePiece.SilkFloor, NextSeed(), PlaceSpec.Cm(sheet), 0), Plan.ToLocal(trunk + inward * (girth + sheet * 0.6f)), r.Float() * MathF.Tau, sheet, 0.25f);
            }
            // The shells of what's hatched out of its sacs, swept off its floor.
            for (int k = 0; k < 3; k++)
            {
                Vector2 at = Site.Position + SiteBuilder.Direction(r.Float() * MathF.Tau) * r.Range(floor + 0.6f, floor + 4f);
                Lay(new PlaceSpec(PlacePiece.Shells, NextSeed()), at, r.Float() * MathF.Tau, 1.4f);
            }
            _nests.Add((Site.Position, 1.5f));
        }

        // What's dragged back to feed the young, the shells of what's hatched, and a trunk they've knocked down.
        private void Leavings(WorldRandom r)
        {
            int carcasses = _kind == NestKind.Ravager ? r.Int(0, 1) : r.Int(1, 2);
            for (int k = 0, tries = 0; k < carcasses && tries < 16; tries++)
            {
                Vector2 at = Site.Position + SiteBuilder.Direction(r.Float() * MathF.Tau) * r.Range(4f, Site.Radius * 0.5f);
                float length = r.Range(4.5f, 7f);
                if (!PutBox(new PlaceSpec(PlacePiece.Carcass, NextSeed(), r.Chance(0.6f) ? 0 : 1, PlaceSpec.Cm(length)), at, r.Float() * MathF.Tau, new Vector2(length * 0.25f, length * 0.5f), 0.9f, 0.4f, 0.45f)) continue;
                Strew(LitterKind.Blood, Plan.ToLocal(at), length * 0.45f, r.Int(8, 14));
                k++;
            }
            // Shells trodden into the ground just off each nest, where the young that hatched pushed them out.
            int shells = r.Int(3, 5);
            for (int k = 0, tries = 0; k < shells && tries < shells * 6; tries++)
            {
                (Vector2 near, float radius) = _nests.Count > 0 ? _nests[r.Int(_nests.Count)] : (Site.Position, 1f);
                Vector2 at = near + SiteBuilder.Direction(r.Float() * MathF.Tau) * (radius + 0.3f + 2f + r.Range(0.2f, 2.2f));
                if (Lay(new PlaceSpec(PlacePiece.Shells, NextSeed()), at, r.Float() * MathF.Tau, 1.9f)) k++;
            }
            if (!r.Chance(0.6f)) return;
            for (int attempt = 0; attempt < 8; attempt++)
            {
                Vector2 at = Site.Position + SiteBuilder.Direction(r.Float() * MathF.Tau) * Site.Radius * r.Range(0.5f, 0.75f);
                float length = r.Range(8f, 13f), radius = r.Range(0.35f, 0.55f);
                float yaw = r.Float() * MathF.Tau;
                if (PutBox(new PlaceSpec(PlacePiece.Log, NextSeed(), With(new[] { PlaceSpec.Cm(length), PlaceSpec.Cm(radius) }, WoodOf())), at, yaw, new Vector2(length * 0.5f, radius + 0.2f), 0.9f, 0.3f, 0.45f)) break;
            }
        }

        // What else lies about a nest (PLACES.md): the boughs torn down to build the nests heaped round them, bones and feathers everywhere the young have fed, and, at the clearing's
        // edge, what's left of the Reich's last try at taking back what got out of its pens: its wagon broken, its tent torn down, its wranglers' arms and bones in the grass.
        private void Wild(WorldRandom r)
        {
            int[] wood = WoodOf();
            foreach ((Vector2 nest, float radius) in _nests.Take(4))
            {
                // A weaver builds with nothing but its silk, and what it feeds on it sucks dry and drops: the bones, not the feathers.
                if (_kind != NestKind.Weaver) Tries(4, () => PutRound(new PlaceSpec(PlacePiece.Slash, NextSeed(), PlaceSpec.Cm(r.Range(1.4f, 2.2f)), wood[0], wood[1], wood[2], wood[3]), nest + SiteBuilder.Direction(r.Float() * MathF.Tau) * (radius + r.Range(2.5f, 4f)), r.Float() * MathF.Tau, 1.4f, 0.8f, 0.2f, 0.5f));
                Strew(_kind == NestKind.Weaver ? LitterKind.Bones : LitterKind.Feathers, Plan.ToLocal(nest) + new Vector2(r.Range(-2f, 2f), r.Range(-2f, 2f)), radius + 2f, r.Int(10, 16));
            }
            for (int k = 0; k < 4; k++) Strew(LitterKind.Bones, Plan.ToLocal(Site.Position + SiteBuilder.Direction(r.Float() * MathF.Tau) * r.Range(2f, Site.Radius * 0.6f)), r.Range(2f, 3.2f), r.Int(10, 18));
            if (_kind == NestKind.Wader) for (int k = 0; k < 3; k++) LayFlat(new PlaceSpec(PlacePiece.Puddle, NextSeed(), PlaceSpec.Cm(r.Range(1f, 2f))), Plan.ToLocal(Site.Position + SiteBuilder.Direction(r.Float() * MathF.Tau) * r.Range(4f, Site.Radius * 0.6f)), r.Float() * MathF.Tau, 1.8f, 0.15f);
            // The Reich's hunting party, at the edge of the clearing, which came for what got out and didn't leave.
            if (!r.Chance(0.75f)) return;
            for (int attempt = 0; attempt < 10; attempt++)
            {
                Vector2 at = Site.Position + SiteBuilder.Direction(r.Float() * MathF.Tau) * Site.Radius * r.Range(0.55f, 0.75f);
                if (!PutBox(new PlaceSpec(PlacePiece.Wagon, NextSeed(), 1, 1, PlaceSpec.Cm(r.Range(3.8f, 4.4f))), at, r.Float() * MathF.Tau, new Vector2(2.4f, 3.4f), 2.4f, 0.4f, 0.45f)) continue;
                Vector2 local = Plan.ToLocal(at);
                Strew(LitterKind.Gear, local + new Vector2(r.Range(-3f, 3f), r.Range(-3f, 3f)), 3.6f, r.Int(10, 16));
                Strew(LitterKind.Bones, local + new Vector2(r.Range(-3f, 3f), r.Range(-3f, 3f)), 2.6f, r.Int(10, 14));
                Strew(LitterKind.Papers, local, 2f, r.Int(4, 7));
                Tries(6, () => PutBox(new PlaceSpec(PlacePiece.Crates, NextSeed(), r.Int(1, 3), 1), at + SiteBuilder.Direction(r.Float() * MathF.Tau) * r.Range(3f, 5f), r.Float() * MathF.Tau, new Vector2(1.6f, 1f), 1.4f, 0.3f, 0.45f));
                break;
            }
        }

        // Whatever nests there, round its nests, and the chest: a traveller's strongbox the hunters dragged back with the rest, among the bones.
        private void Hold(WorldRandom r)
        {
            var spots = new List<Vector2>();
            foreach ((Vector2 nest, float radius) in _nests) spots.AddRange(Around(nest, radius + 6f, r.Fork(spots.Count), 20));
            spots.AddRange(Around(Site.Position, Site.Radius * 0.6f, r.Fork("about"), 40));
            Stand(SpawnRole.Beast, spots, r.Int(5, 7), 4f, r.Fork("stand"));
            Chest(Site.Position + SiteBuilder.Direction(r.Float() * MathF.Tau) * r.Range(3f, 7f), Site.Radius * 0.6f);
        }
    }
}
