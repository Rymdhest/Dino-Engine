using Dino_Engine.Modelling.Procedural.Gear;
using Dino_Engine.World;
using OpenTK.Mathematics;

namespace Dino_Engine.Modelling.Procedural.Flora
{
    /// <summary>
    /// Fine wood, grown by competition: the self-organizing trees of Palubicki et al. (2009). Nothing here says where a branch goes. Buds find out what's around them from markers of
    /// free space (the space colonization of Runions et al. 2007) and a shadow grid, and a resource that the light earns is shared back out through the tree by the extended
    /// Borchert-Honda model, so branches in the open go on and those in the shade stall. Markers are what guarantee a crown: left to shade alone a seed can leave one side of a tree
    /// bare, whereas wood goes on growing until the markers it can reach are used up. It starts from a scaffold, which holds what a species is, and fills in around it.
    /// </summary>
    internal sealed class BudGrowth
    {
        private struct Bud
        {
            public int Node;
            public Vector3 Direction;
            public bool Terminal;
            public float Light;
            public float Resource;
            public Vector3 Space;   // Towards the free space it can sense.
            public int Sensed;      // How many markers of free space have it as their nearest bud.
        }

        private readonly PlantGraph _graph;
        private readonly BudForm _form;
        private readonly Envelope _envelope;
        private readonly WorldRandom _random;
        private readonly ShadowGrid _shadow;
        private readonly List<Bud> _buds = new();
        private readonly List<int> _born = new();
        private readonly int _scaffoldEnd;
        private readonly float _apical;
        private readonly float _vigour;
        private readonly float _seek;
        private readonly float _tropism;
        private readonly float _branchAngle;
        private readonly float _divergence;
        private readonly List<Vector3> _markers = new();
        private int _year;

        public BudGrowth(PlantGraph graph, BudForm form, Envelope envelope, Genes genes)
        {
            _graph = graph;
            _form = form;
            _envelope = envelope;
            _random = genes.Stream("buds");
            _scaffoldEnd = graph.Count;
            _apical = genes.Range("apical", form.Apical);
            _vigour = genes.Range("vigour", form.Vigour);
            _seek = genes.Range("seek", form.LightSeek);
            _tropism = genes.Range("tropism", form.Tropism);
            _branchAngle = genes.Range("branch angle", form.BranchAngle);
            _divergence = form.Divergence * (MathF.PI / 180f);

            float margin = envelope.Radius * 0.35f + 2f;
            Vector3 min = new Vector3(envelope.Offset.X - envelope.Radius - margin, 0f, envelope.Offset.Y - envelope.Radius - margin);
            Vector3 max = new Vector3(envelope.Offset.X + envelope.Radius + margin, envelope.Top + margin, envelope.Offset.Y + envelope.Radius + margin);
            _shadow = new ShadowGrid(min, max, MathF.Max(form.Internode * 1.5f, 0.45f), form.ShadeStrength, form.ShadeFalloff, form.ShadeDepth);

            for (int i = 0; i < graph.Count; i++) _born.Add(0);
            ScatterMarkers(genes.Stream("markers"), (uint)genes.Stream("clumps").NextULong());
        }

        // Free space, thicker towards the surface of the crown, where a real one has its leaves (Runions section 3). Noise takes holes out of it, so that what grows here comes in masses
        // with air between them, which is what a crown looks like from above.
        private void ScatterMarkers(WorldRandom random, uint seed)
        {
            float radius = _envelope.Radius * 1.15f;
            float height = _envelope.Top - _envelope.Bottom;
            float volume = MathF.PI * radius * radius * height;
            int tries = (int)(volume * _form.MarkerDensity);
            float clump = MathF.Max(_form.ClumpSize, 0.5f);

            for (int i = 0; i < tries; i++)
            {
                float angle = random.Float() * MathF.Tau;
                float distance = MathF.Sqrt(random.Float()) * radius;
                Vector3 at = new Vector3(_envelope.Offset.X + MathF.Cos(angle) * distance, _envelope.Bottom + random.Float() * height, _envelope.Offset.Y + MathF.Sin(angle) * distance);
                float inside = _envelope.Inside(at);
                if (inside <= 0f) continue;
                if (random.Float() > 1f - inside * _form.Hollow) continue;
                if (_form.Gaps > 0f && FloraNoise.Fbm(at / clump, seed, 2) < _form.Gaps) continue;
                _markers.Add(at);
            }
            ClearMarkers(0);
        }

        // Whatever wood has reached uses up the space it's in.
        private void ClearMarkers(int firstNode)
        {
            float cell = _form.Occupancy;
            float reach = cell * cell;
            var near = new Dictionary<(int, int, int), List<Vector3>>();
            for (int i = firstNode; i < _graph.Count; i++)
            {
                Vector3 at = _graph.Nodes[i].Position;
                Vector3 p = at / cell;
                var key = ((int)MathF.Floor(p.X), (int)MathF.Floor(p.Y), (int)MathF.Floor(p.Z));
                if (!near.TryGetValue(key, out List<Vector3>? list)) near[key] = list = new List<Vector3>();
                list.Add(at);
            }
            if (near.Count == 0) return;

            _markers.RemoveAll(marker =>
            {
                Vector3 p = marker / cell;
                int x = (int)MathF.Floor(p.X), y = (int)MathF.Floor(p.Y), z = (int)MathF.Floor(p.Z);
                for (int dz = -1; dz <= 1; dz++)
                {
                    for (int dy = -1; dy <= 1; dy++)
                    {
                        for (int dx = -1; dx <= 1; dx++)
                        {
                            if (!near.TryGetValue((x + dx, y + dy, z + dz), out List<Vector3>? list)) continue;
                            foreach (Vector3 wood in list)
                            {
                                if ((wood - marker).LengthSquared < reach) return true;
                            }
                        }
                    }
                }
                return false;
            });
        }

        /// <summary>The buds a scaffold starts with: one at every tip, and some along the way.</summary>
        public void Seed(List<List<int>> stems)
        {
            foreach (List<int> stem in stems)
            {
                for (int i = 1; i < stem.Count; i++)
                {
                    int node = stem[i];
                    Vector3 direction = (_graph.Nodes[node].Position - _graph.Nodes[stem[i - 1]].Position).Normalized();
                    if (i == stem.Count - 1) _buds.Add(new Bud { Node = node, Direction = direction, Terminal = true });
                    if (_envelope.Inside(_graph.Nodes[node].Position) < -_form.Room || _random.Float() > _form.ScaffoldBuds) continue;
                    AddLateral(_buds, node, direction, i);
                }
            }
        }

        // Where a bud sits around its stem goes by its rank along the shoot: the divergence angle of phyllotaxis, which is what spaces a stem's leaves out evenly.
        private void AddLateral(List<Bud> buds, int node, Vector3 direction, int rank)
        {
            float around = rank * _divergence + _random.Range(-0.2f, 0.2f);
            int pairs = _form.Opposite ? 2 : 1;
            for (int i = 0; i < pairs; i++)
            {
                Vector3 lateral = Scaffold.Away(direction, Vector3.UnitY, around + i * MathF.PI, _branchAngle + _random.Range(-0.15f, 0.15f));
                buds.Add(new Bud { Node = node, Direction = lateral });
            }
        }

        public void Run(int years)
        {
            for (_year = 1; _year <= years && _graph.Count < _form.MostNodes; _year++)
            {
                Perceive();
                Allocate(out int[] size);
                int before = _graph.Count;
                Shoot();
                ClearMarkers(before);
                Shed(size);
            }
        }

        // What a bud knows of the world: the free space it's the nearest thing to, in a cone ahead of it, and how much light gets past everything that's grown already.
        private void Perceive()
        {
            _shadow.Clear();
            for (int i = _scaffoldEnd; i < _graph.Count; i++)
            {
                if (!_graph.Nodes[i].Dead && _year - _born[i] <= _form.LeafYears) _shadow.Cast(_graph.Nodes[i].Position);
            }

            float cell = _form.Perception;
            var grid = new Dictionary<(int, int, int), List<int>>();
            for (int i = 0; i < _buds.Count; i++)
            {
                Bud bud = _buds[i];
                bud.Space = Vector3.Zero;
                bud.Sensed = 0;
                _buds[i] = bud;

                Vector3 p = _graph.Nodes[bud.Node].Position / cell;
                var key = ((int)MathF.Floor(p.X), (int)MathF.Floor(p.Y), (int)MathF.Floor(p.Z));
                if (!grid.TryGetValue(key, out List<int>? list)) grid[key] = list = new List<int>();
                list.Add(i);
            }

            float reach = _form.Perception * _form.Perception;
            float cone = MathF.Cos(_form.PerceptionAngle);
            foreach (Vector3 marker in _markers)
            {
                Vector3 p = marker / cell;
                int x = (int)MathF.Floor(p.X), y = (int)MathF.Floor(p.Y), z = (int)MathF.Floor(p.Z);
                int nearest = -1;
                float best = reach;
                Vector3 towards = Vector3.Zero;
                for (int dz = -1; dz <= 1; dz++)
                {
                    for (int dy = -1; dy <= 1; dy++)
                    {
                        for (int dx = -1; dx <= 1; dx++)
                        {
                            if (!grid.TryGetValue((x + dx, y + dy, z + dz), out List<int>? list)) continue;
                            foreach (int i in list)
                            {
                                Bud bud = _buds[i];
                                Vector3 to = marker - _graph.Nodes[bud.Node].Position;
                                float distance = to.LengthSquared;
                                if (distance >= best || distance < 1e-6f) continue;
                                if (Vector3.Dot(to, bud.Direction) < cone * MathF.Sqrt(distance)) continue;
                                best = distance;
                                nearest = i;
                                towards = to / MathF.Sqrt(distance);
                            }
                        }
                    }
                }
                if (nearest < 0) continue;
                Bud chosen = _buds[nearest];
                chosen.Space += towards;
                chosen.Sensed++;
                _buds[nearest] = chosen;
            }

            for (int i = 0; i < _buds.Count; i++)
            {
                Bud bud = _buds[i];
                Vector3 at = _graph.Nodes[bud.Node].Position;
                bud.Light = bud.Sensed > 0 ? MathF.Max(_shadow.Light(at), 0.1f) : 0f;
                bud.Space = bud.Space.LengthSquared > 1e-6f ? bud.Space.Normalized() : Vector3.Zero;
                _buds[i] = bud;
            }
        }

        // Extended Borchert-Honda: light goes down the tree adding up, then the resource it earned comes back up, split at every fork by how much light each side gathered and
        // by lambda, which leans it to the main axis (excurrent) or to the laterals (decurrent).
        private void Allocate(out int[] size)
        {
            int count = _graph.Count;
            var light = new float[count];
            size = new int[count];
            var mainLight = new float[count];
            var sideLight = new float[count];

            foreach (Bud bud in _buds)
            {
                if (bud.Terminal) mainLight[bud.Node] += bud.Light;
                else sideLight[bud.Node] += bud.Light;
            }

            for (int i = count - 1; i >= 0; i--)
            {
                PlantGraph.Node node = _graph.Nodes[i];
                if (node.Dead) continue;
                light[i] = mainLight[i] + sideLight[i];
                size[i]++;
                if (node.Parent < 0) continue;
                size[node.Parent] += size[i];
                if (IsMain(i)) mainLight[node.Parent] += light[i];
                else sideLight[node.Parent] += light[i];
            }

            var resource = new float[count];
            for (int i = 0; i < count; i++)
            {
                PlantGraph.Node node = _graph.Nodes[i];
                if (node.Dead) continue;
                if (node.Parent < 0)
                {
                    resource[i] = _vigour * light[i];
                    continue;
                }
                resource[i] = Share(resource[node.Parent], mainLight[node.Parent], sideLight[node.Parent], light[i], IsMain(i));
            }

            for (int i = 0; i < _buds.Count; i++)
            {
                Bud bud = _buds[i];
                bud.Resource = Share(resource[bud.Node], mainLight[bud.Node], sideLight[bud.Node], bud.Light, bud.Terminal);
                _buds[i] = bud;
            }
        }

        private bool IsMain(int node) => _graph.Nodes[node].Order == _graph.Nodes[_graph.Nodes[node].Parent].Order;

        private float Share(float resource, float main, float side, float own, bool isMain)
        {
            float divisor = _apical * main + (1f - _apical) * side;
            if (divisor <= 1e-6f) return 0f;
            return resource * (isMain ? _apical : 1f - _apical) * own / divisor;
        }

        private void Shoot()
        {
            var next = new List<Bud>(_buds.Count + 64);
            foreach (Bud bud in _buds)
            {
                int metamers = Math.Min((int)bud.Resource, _form.MostPerYear);
                if (metamers < 1)
                {
                    // A bud can sleep for years and still go when a gap opens, but not for ever, or a crown fills with buds that will never do anything.
                    if (_random.Float() > _form.BudDeath) next.Add(bud);
                    continue;
                }

                float length = _form.Internode * MathF.Min(bud.Resource / metamers, 2f);
                int parent = bud.Node;
                int order = bud.Terminal ? _graph.Nodes[parent].Order : _graph.Nodes[parent].Order + 1;
                Vector3 direction = bud.Direction;
                Vector3 at = _graph.Nodes[parent].Position;
                int rank = _random.Int(64);

                for (int i = 0; i < metamers && _graph.Count < _form.MostNodes; i++)
                {
                    direction = Orient(direction, bud.Space, at, order);
                    at += direction * length;
                    int node = _graph.Add(at, parent, order);
                    _born.Add(_year);
                    parent = node;
                    AddLateral(next, node, direction, rank + i);
                }
                next.Add(new Bud { Node = parent, Direction = direction, Terminal = true });
            }
            _buds.Clear();
            _buds.AddRange(next);
        }

        // A weighted sum of where it was going, the way to free space and the light, and which way up it wants to be (section 4.3), and a little of its own mind.
        private Vector3 Orient(Vector3 direction, Vector3 space, Vector3 at, int order)
        {
            float tropism = _tropism + _form.TropismByOrder * order;
            Vector3 result = direction + space * _seek + _shadow.TowardsLight(at) * (_seek * 0.25f) + Vector3.UnitY * tropism;
            result += new Vector3(_random.Range(-1f, 1f), _random.Range(-1f, 1f), _random.Range(-1f, 1f)) * _form.Wobble;

            // Near the edge of the crown it turns back in, so that a shape holds without being clipped.
            float inside = _envelope.Inside(at);
            if (inside < 0.15f) result -= _envelope.Outward(at) * (_form.Containment * (0.15f - inside));

            return result.LengthSquared > 1e-6f ? result.Normalized() : direction;
        }

        // A branch that gathers too little light for its size costs more than it earns, and goes (Takenaka 1994, by way of section 4.4). This reads the shade and not the markers, as
        // a yes or no would drop a branch the moment it stopped growing. What it gave the limb it stood on stays.
        private void Shed(int[] size)
        {
            if (_form.Shed <= 0f) return;
            int measured = size.Length;
            var gathered = new float[measured];
            for (int i = measured - 1; i >= 0; i--)
            {
                PlantGraph.Node node = _graph.Nodes[i];
                if (node.Dead) continue;
                gathered[i] += _shadow.Light(node.Position);
                if (node.Parent >= 0) gathered[node.Parent] += gathered[i];
            }

            var dying = new bool[_graph.Count];
            bool any = false;
            for (int i = _scaffoldEnd; i < measured; i++)
            {
                PlantGraph.Node node = _graph.Nodes[i];
                if (node.Dead || node.Parent < 0 || IsMain(i) || _year - _born[i] < 3 || size[i] < 3) continue;
                if (gathered[i] / size[i] >= _form.Shed) continue;
                dying[i] = true;
                any = true;

                PlantGraph.Node parent = _graph.Nodes[node.Parent];
                parent.Scars += size[i] * 0.35f;
                _graph.Nodes[node.Parent] = parent;
            }
            if (!any) return;

            for (int i = _scaffoldEnd; i < _graph.Count; i++)
            {
                PlantGraph.Node node = _graph.Nodes[i];
                if (node.Dead) continue;
                if (dying[i] || (node.Parent >= 0 && _graph.Nodes[node.Parent].Dead))
                {
                    node.Dead = true;
                    _graph.Nodes[i] = node;
                }
            }
            _buds.RemoveAll(bud => _graph.Nodes[bud.Node].Dead);
        }
    }
}
