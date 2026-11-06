using Dino_Defenders.Progression;
using Dino_Defenders.Skills;
using Dino_Defenders.Stats;
using OpenTK.Mathematics;

namespace Dino_Defenders.Talents
{
    public enum TalentKind : byte
    {
        Root,
        Small,
        Notable,
        Keystone,
        Skill,
        Gate,
    }

    public sealed class TalentNode
    {
        public int Index;
        public string Id = "";
        public string Name = "";
        public TalentKind Kind;
        public int Sector = -1;
        public Vector2 Position;
        public Mod[] Mods = Array.Empty<Mod>();
        public string? Lore;
        public string? Title;       // What having it makes you, for a keystone with an opinion on the matter.
        public int Twin = -1;       // For a waygate: the one it opens onto.
        public readonly List<int> Links = new();
    }

    /// <summary>
    /// The one tree every hero grows through. There are no classes: where someone spends their points is what they are. It's a wheel of twelve
    /// sectors around a single root, each forking as it goes out, so the further a build reaches the more its choices narrow. Rings join each
    /// sector to its neighbours, with something at each join worth having for those who walk both. At the rim stand the keystones, and the
    /// waygates, which open onto a twin far across the wheel.
    ///
    /// Node ids are saved in heroes, and come from where a node stands in a sector's table, so a sector's shape can't change without care.
    /// Numbers, names and effects can change at any time. <see cref="Sanitize"/> looks after anything that goes missing.
    /// </summary>
    public static class TalentTree
    {
        private const float DegreesPerSector = 30f;
        private const float Fork = 7.5f;
        private const float SecondFork = 3.75f;
        private const float Step = 100f;
        private const float TrunkStart = 160f;
        private const int TrunkNodes = 6;
        private const int BranchNodes = 4;
        private const float TipReach = 130f;
        private const int TwigsPerSector = 4;

        private static readonly List<TalentNode> Built = new();
        private static readonly Dictionary<string, int> ById = new();

        public static readonly TalentNode[] Nodes;
        public static readonly TalentNode Root;

        /// <summary>How many points it takes to get from the root to each node, walking.</summary>
        public static readonly int[] DepthOf;

        static TalentTree()
        {
            Root = Add(new TalentNode { Id = "root", Name = "The Root", Kind = TalentKind.Root, Position = Vector2.Zero });

            var rim = new List<TalentNode>();
            var trunkEnds = new TalentNode[TalentContent.Sectors.Length];
            var leftEnds = new TalentNode[TalentContent.Sectors.Length];
            var rightEnds = new TalentNode[TalentContent.Sectors.Length];

            for (int i = 0; i < TalentContent.Sectors.Length; i++)
            {
                BuildSector(TalentContent.Sectors[i], i, out trunkEnds[i], out leftEnds[i], out rightEnds[i], rim);
            }

            for (int i = 0; i < TalentContent.Sectors.Length; i++)
            {
                int next = (i + 1) % TalentContent.Sectors.Length;
                BuildBridge(i, next, TalentContent.InnerBridges[i], trunkEnds[i], trunkEnds[next], "inner");
                BuildBridge(i, next, TalentContent.OuterBridges[i], rightEnds[i], leftEnds[next], "outer");
            }

            BuildRim(rim);

            foreach ((int first, int second) in TalentContent.GatePairs)
            {
                TalentNode a = Built[ById[$"{TalentContent.Sectors[first].Id}.gate"]];
                TalentNode b = Built[ById[$"{TalentContent.Sectors[second].Id}.gate"]];
                a.Twin = b.Index;
                b.Twin = a.Index;
            }

            Nodes = Built.ToArray();
            DepthOf = Depths();
        }

        private static TalentNode Add(TalentNode node)
        {
            // Levels give nothing but points, so it's the tree that makes a hero strong, and each sector does it in its own way.
            if (node.Sector >= 0 && node.Kind != TalentKind.Gate) node.Mods = node.Mods.Concat(TalentContent.AttributesOf(TalentContent.Sectors[node.Sector].Id)).Append(M.Flat(Stat.Health, Rulebook.NodeHealth)).ToArray();

            node.Index = Built.Count;
            Built.Add(node);
            ById.Add(node.Id, node.Index);
            return node;
        }

        private static void Link(TalentNode a, TalentNode b)
        {
            a.Links.Add(b.Index);
            b.Links.Add(a.Index);
        }

        private static Vector2 At(float radius, float degrees)
        {
            float angle = MathHelper.DegreesToRadians(degrees);
            return new Vector2(MathF.Sin(angle), -MathF.Cos(angle)) * radius;
        }

        // The last of every twig is joined to the next, all the way around. So the far end of the tree is a place to walk along, and a waygate lands somewhere that leads somewhere.
        private static void BuildRim(List<TalentNode> rim)
        {
            for (int i = 0; i < rim.Count; i++)
            {
                TalentNode from = rim[i];
                TalentNode to = rim[(i + 1) % rim.Count];
                Sector sector = TalentContent.Sectors[from.Sector];
                Vector2 middle = (from.Position + to.Position).Normalized() * from.Position.Length;
                var link = Add(new TalentNode
                {
                    Id = $"{sector.Id}.rim{i % TwigsPerSector}", Name = sector.Name, Kind = TalentKind.Small, Sector = from.Sector,
                    Position = middle, Mods = sector.Small[1 + i % (sector.Small.Length - 1)],
                });
                Link(from, link);
                Link(link, to);
            }
        }

        private static void BuildSector(Sector sector, int index, out TalentNode trunkEnd, out TalentNode leftEnd, out TalentNode rightEnd, List<TalentNode> rim)
        {
            float centre = index * DegreesPerSector;
            int smallIndex = 0;

            TalentNode Trunk(string id, float radius, float degrees)
            {
                Mod[] mods = sector.Small[smallIndex++ % sector.Small.Length];
                return Add(new TalentNode { Id = $"{sector.Id}.{id}", Name = sector.Name, Kind = TalentKind.Small, Sector = index, Position = At(radius, degrees), Mods = mods });
            }

            // The trunk.
            var trunk = new TalentNode[TrunkNodes];
            for (int i = 0; i < TrunkNodes; i++)
            {
                trunk[i] = Trunk($"t{i}", TrunkStart + i * Step, centre);
                Link(i == 0 ? Root : trunk[i - 1], trunk[i]);
            }
            trunkEnd = trunk[^1];

            // It forks, and forks again.
            float firstForkAt = TrunkStart + TrunkNodes * Step;
            var branches = new TalentNode[2][];
            var twigs = new TalentNode[4][];
            for (int side = 0; side < 2; side++)
            {
                float degrees = centre + (side == 0 ? -Fork : Fork);
                branches[side] = new TalentNode[BranchNodes];
                for (int i = 0; i < BranchNodes; i++)
                {
                    branches[side][i] = Trunk($"b{side}.{i}", firstForkAt + i * Step, degrees);
                    Link(i == 0 ? trunkEnd : branches[side][i - 1], branches[side][i]);
                }

                float secondForkAt = firstForkAt + BranchNodes * Step;
                for (int fork = 0; fork < 2; fork++)
                {
                    float twigDegrees = degrees + (fork == 0 ? -SecondFork : SecondFork);
                    int twig = side * 2 + fork;
                    twigs[twig] = new TalentNode[BranchNodes];
                    for (int i = 0; i < BranchNodes; i++)
                    {
                        twigs[twig][i] = Trunk($"c{twig}.{i}", secondForkAt + i * Step, twigDegrees);
                        Link(i == 0 ? branches[side][^1] : twigs[twig][i - 1], twigs[twig][i]);
                    }

                    // The rim: three keystones and a waygate.
                    TalentNode tip = twig < sector.Keystones.Length
                        ? Keystone(sector, index, sector.Keystones[twig], At(secondForkAt + (BranchNodes - 1) * Step + TipReach, twigDegrees))
                        : Add(new TalentNode
                        {
                            Id = $"{sector.Id}.gate", Name = "Waygate", Kind = TalentKind.Gate, Sector = index,
                            Position = At(secondForkAt + (BranchNodes - 1) * Step + TipReach, twigDegrees),
                            Mods = new[] { M.Inc(Stat.MoveSpeed, 3f) },
                            Lore = "Light this, and its twin across the tree can be lit from here without walking there.",
                        });
                    Link(twigs[twig][^1], tip);
                    rim.Add(twigs[twig][^1]);
                }
            }
            leftEnd = branches[0][^1];
            rightEnd = branches[1][^1];

            // What hangs off all that.
            int pod = 0;
            void Pod(TalentNode from, TalentNode to, float radius, float degrees, float side)
            {
                if (pod >= sector.Pods.Length) return;
                BuildPod(sector, index, sector.Pods[pod++], from, to, radius, degrees, side);
            }

            Pod(trunk[0], trunk[2], TrunkStart, centre, 1f);
            Pod(trunk[2], trunk[4], TrunkStart + 2 * Step, centre, 1f);
            for (int side = 0; side < 2; side++)
            {
                float degrees = centre + (side == 0 ? -Fork : Fork);
                Pod(branches[side][0], branches[side][2], firstForkAt, degrees, side == 0 ? 1f : -1f);
                Pod(branches[side][0], branches[side][2], firstForkAt, degrees, side == 0 ? -1f : 1f);
            }
            for (int twig = 0; twig < 4; twig++)
            {
                float degrees = centre + (twig < 2 ? -Fork : Fork) + (twig % 2 == 0 ? -SecondFork : SecondFork);
                Pod(twigs[twig][0], twigs[twig][2], firstForkAt + BranchNodes * Step, degrees, 1f);
            }

            // An eleventh goes on the free side of the trunk, last of all, as what's already placed has to stay where it is.
            Pod(trunk[1], trunk[3], TrunkStart + Step, centre, -1f);
        }

        private static TalentNode Keystone(Sector sector, int index, KeystoneEntry entry, Vector2 position)
        {
            return Add(new TalentNode
            {
                Id = $"key.{entry.Id}", Name = entry.Name, Kind = TalentKind.Keystone, Sector = index, Position = position,
                Mods = entry.Mods, Lore = entry.Lore, Title = entry.Title,
            });
        }

        // A pod loops out from one node and back to another further on, so going through it never strands anyone in a dead end.
        private static void BuildPod(Sector sector, int index, PodEntry pod, TalentNode from, TalentNode to, float radius, float degrees, float side)
        {
            float Offset(float sideways, float at) => degrees + side * MathHelper.RadiansToDegrees(sideways / at);

            bool skill = pod.Skill != null;
            TalentNode previous = from;
            int smalls = skill ? 1 : 2;
            for (int i = 0; i < smalls; i++)
            {
                float at = radius + (skill ? 60f : 40f + i * 70f);
                var small = Add(new TalentNode
                {
                    Id = $"{sector.Id}.{pod.Id}.{i}", Name = pod.Name, Kind = TalentKind.Small, Sector = index,
                    Position = At(at, Offset(skill ? 62f : 56f + i * 14f, at)), Mods = new[] { pod.Small },
                });
                Link(previous, small);
                previous = small;
            }

            float end = radius + (skill ? 136f : 170f);
            var notable = Add(new TalentNode
            {
                Id = $"{sector.Id}.{pod.Id}", Name = pod.Name, Kind = skill ? TalentKind.Skill : TalentKind.Notable, Sector = index,
                Position = At(end, Offset(60f, end)),
                Mods = skill ? pod.Mods.Prepend(M.Skill(pod.Skill!)).ToArray() : pod.Mods,
                Lore = pod.Skill?.Text,
            });
            Link(previous, notable);
            Link(notable, to);
        }

        private static void BuildBridge(int left, int right, BridgeEntry bridge, TalentNode from, TalentNode to, string ring)
        {
            float a = MathF.Atan2(from.Position.X, -from.Position.Y);
            float b = MathF.Atan2(to.Position.X, -to.Position.Y);
            float gap = MathF.IEEERemainder(b - a, MathF.Tau);
            float radius = from.Position.Length;
            string id = $"bridge.{TalentContent.Sectors[left].Id}.{ring}";

            Vector2 Along(float share)
            {
                float angle = a + gap * share;
                return new Vector2(MathF.Sin(angle), -MathF.Cos(angle)) * radius;
            }

            var first = Add(new TalentNode { Id = $"{id}.0", Name = bridge.Name, Kind = TalentKind.Small, Sector = left, Position = Along(0.25f), Mods = TalentContent.Sectors[left].Small[0] });
            var middle = Add(new TalentNode { Id = id, Name = bridge.Name, Kind = TalentKind.Notable, Sector = left, Position = Along(0.5f), Mods = bridge.Mods });
            var second = Add(new TalentNode { Id = $"{id}.1", Name = bridge.Name, Kind = TalentKind.Small, Sector = right, Position = Along(0.75f), Mods = TalentContent.Sectors[right].Small[0] });
            Link(from, first);
            Link(first, middle);
            Link(middle, second);
            Link(second, to);
        }

        private static int[] Depths()
        {
            var depth = new int[Nodes.Length];
            Array.Fill(depth, -1);
            depth[Root.Index] = 0;
            var frontier = new Queue<int>();
            frontier.Enqueue(Root.Index);
            while (frontier.TryDequeue(out int index))
            {
                foreach (int link in Nodes[index].Links)
                {
                    if (depth[link] >= 0) continue;
                    depth[link] = depth[index] + 1;
                    frontier.Enqueue(link);
                }
            }
            return depth;
        }

        public static TalentNode? Find(string id) => ById.TryGetValue(id, out int index) ? Nodes[index] : null;

        /// <summary>The nodes a hero has, as places in <see cref="Nodes"/>, the root among them. Sorted, so everything adds up in the same order everywhere.</summary>
        public static List<int> Allocated(HeroState hero)
        {
            var allocated = new List<int> { Root.Index };
            foreach (string id in hero.Talents)
            {
                if (ById.TryGetValue(id, out int index) && index != Root.Index) allocated.Add(index);
            }
            allocated.Sort();
            return allocated;
        }

        private static bool[] Marks(IEnumerable<int> allocated)
        {
            var marks = new bool[Nodes.Length];
            foreach (int index in allocated) marks[index] = true;
            return marks;
        }

        private static bool Reaches(bool[] allocated, TalentNode node)
        {
            foreach (int link in node.Links)
            {
                if (allocated[link]) return true;
            }
            return node.Twin >= 0 && allocated[node.Twin];
        }

        /// <summary>Whether a node can be taken next: it has to touch what's already there, or be the far side of a waygate that's lit.</summary>
        public static bool CanAllocate(HeroState hero, int index)
        {
            if (index < 0 || index >= Nodes.Length || hero.UnspentPoints <= 0) return false;
            bool[] allocated = Marks(Allocated(hero));
            return !allocated[index] && Reaches(allocated, Nodes[index]);
        }

        /// <summary>Whether a node can be given back: only if everything else stays joined to the root without it.</summary>
        public static bool CanRefund(HeroState hero, int index)
        {
            if (index <= 0 || index >= Nodes.Length) return false;
            bool[] allocated = Marks(Allocated(hero));
            if (!allocated[index]) return false;

            allocated[index] = false;
            bool[] joined = Joined(allocated);
            for (int i = 0; i < Nodes.Length; i++)
            {
                if (allocated[i] && !joined[i]) return false;
            }
            return true;
        }

        // Everything that can be got to from the root by what's allocated, walking or stepping through a lit gate.
        private static bool[] Joined(bool[] allocated)
        {
            var joined = new bool[Nodes.Length];
            joined[Root.Index] = true;
            var frontier = new Stack<int>();
            frontier.Push(Root.Index);
            while (frontier.TryPop(out int index))
            {
                TalentNode node = Nodes[index];
                foreach (int link in node.Links)
                {
                    if (!allocated[link] || joined[link]) continue;
                    joined[link] = true;
                    frontier.Push(link);
                }
                if (node.Twin >= 0 && allocated[node.Twin] && !joined[node.Twin])
                {
                    joined[node.Twin] = true;
                    frontier.Push(node.Twin);
                }
            }
            return joined;
        }

        /// <summary>The cheapest way from what's already taken to a node, ending with it. Empty if it's taken already.</summary>
        public static List<int> PathTo(HeroState hero, int target)
        {
            var path = new List<int>();
            if (target < 0 || target >= Nodes.Length) return path;

            bool[] allocated = Marks(Allocated(hero));
            if (allocated[target]) return path;

            var from = new int[Nodes.Length];
            Array.Fill(from, -1);
            var frontier = new Queue<int>();
            for (int i = 0; i < Nodes.Length; i++)
            {
                if (!allocated[i]) continue;
                from[i] = i;
                frontier.Enqueue(i);
            }

            while (frontier.TryDequeue(out int index))
            {
                if (index == target) break;
                TalentNode node = Nodes[index];
                foreach (int link in node.Links)
                {
                    if (from[link] >= 0) continue;
                    from[link] = index;
                    frontier.Enqueue(link);
                }

                // A gate's twin is one step off, as a path takes its nodes in order and so lights the gate on its way through.
                if (node.Twin >= 0 && from[node.Twin] < 0)
                {
                    from[node.Twin] = index;
                    frontier.Enqueue(node.Twin);
                }
            }

            if (from[target] < 0) return path;
            for (int at = target; !allocated[at]; at = from[at]) path.Add(at);
            path.Reverse();
            return path;
        }

        /// <summary>
        /// Puts a hero right, whatever a save or a stranger says it has. What's unknown goes, what's cut off from the root goes, and what can't
        /// be paid for goes. So a change to the tree costs someone a few points back in their pocket, never their hero.
        /// </summary>
        public static void Sanitize(HeroState hero)
        {
            bool[] allocated = Marks(Allocated(hero));
            bool[] joined = Joined(allocated);

            var kept = new List<string>();
            var seen = new HashSet<int>();
            foreach (string id in hero.Talents)
            {
                if (!ById.TryGetValue(id, out int index) || index == Root.Index || !joined[index] || !seen.Add(index)) continue;
                kept.Add(id);
            }

            // Too many for their level: the last taken go first, which can't cut off anything taken before them.
            int budget = hero.TalentPoints;
            if (kept.Count > budget) kept.RemoveRange(budget, kept.Count - budget);
            hero.Talents = kept;

            bool[] trimmed = Marks(Allocated(hero));
            bool[] stillJoined = Joined(trimmed);
            hero.Talents.RemoveAll(id => !stillJoined[ById[id]]);
        }

        /// <summary>Everything a hero's talents add up to.</summary>
        public static IEnumerable<Mod> Mods(HeroState hero)
        {
            foreach (int index in Allocated(hero))
            {
                foreach (Mod mod in Nodes[index].Mods) yield return mod;
            }
        }

        /// <summary>
        /// What someone is, by where they've spent their points: the sector they've given most to names them, and the next one colours it. A
        /// keystone can have the last word.
        /// </summary>
        public static string TitleOf(HeroState hero)
        {
            var points = new int[TalentContent.Sectors.Length];
            string? named = null;
            int spent = 0;
            foreach (int index in Allocated(hero))
            {
                TalentNode node = Nodes[index];
                if (node.Sector < 0) continue;
                points[node.Sector]++;
                spent++;
                if (node.Title != null) named = node.Title;
            }

            if (named != null) return named;
            if (spent < 6) return "Wanderer";

            int first = 0;
            for (int i = 1; i < points.Length; i++)
            {
                if (points[i] > points[first]) first = i;
            }

            int second = -1;
            for (int i = 0; i < points.Length; i++)
            {
                if (i != first && points[i] > 0 && (second < 0 || points[i] > points[second])) second = i;
            }

            Sector main = TalentContent.Sectors[first];
            if (second < 0 || points[second] * 3 < points[first]) return $"{main.Adjective} {main.Noun}";
            return $"{TalentContent.Sectors[second].Adjective} {main.Noun}";
        }
    }
}
