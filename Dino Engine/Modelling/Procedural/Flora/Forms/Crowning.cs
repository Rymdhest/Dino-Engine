using Dino_Engine.Modelling.Model;
using Dino_Engine.Modelling.Procedural.Gear;
using Dino_Engine.World;
using OpenTK.Mathematics;

namespace Dino_Engine.Modelling.Procedural.Flora
{
    /// <summary>How a plant's cards sit.</summary>
    public enum CardPose
    {
        Spreading,  // Lying out from the twig and facing the sky: most trees.
        Hanging,    // Falling straight down from the twig: willows, wisteria.
        Upright,    // Reaching up: poplars, cedars.
    }

    /// <summary>What a tree wears.</summary>
    public sealed class FoliageForm
    {
        public CardForm Card = null!;
        public FloatRange CardSize = new FloatRange(2.6f, 3.6f);    // Metres.
        public float Aspect = 1f;               // How wide a card is for its length.
        public int MostCards = 260;
        public float Spacing = 0.42f;           // Between cards, as a share of their size.
        public CardPose Pose = CardPose.Spreading;
        public float Tilt = 0.45f;              // How far a card strays from its pose, in radians.
        public float Fold = 0.28f;              // A card is creased down its middle, as a flat one reads as flat from any side.
        public float Droop = 0.22f;             // How far its end falls, as a share of its length.
        public float Cross = 0.3f;              // The chance of a second card across the first, for what's seen from the side.
        public float Shell = 0.72f;             // How much a card's normals follow the mass it belongs to and not its own face.
        public float Shade = 0.25f;             // Below this much light a site grows nothing.
        public Palette Colours = new Palette();
    }

    /// <summary>
    /// Puts leaves on a tree. A tip of the wood is a place a card can go; those in the light are taken first and the rest thinned, so cards end up where a real crown has its leaves,
    /// on the outside. Then what sells it as a mass and not a pile of pictures: cards are gathered into clumps, and shaded as if each clump were one soft shape, with normals bent
    /// towards the clump's surface (what artists do by transferring normals from a sphere) and colour running from deep underneath to warm on top. That last is painted in, as from
    /// this camera most of a crown is seen from above and real shadow alone won't model it.
    /// </summary>
    internal static class Crowning
    {
        private const float LowestCard = 0.05f;

        private struct Site
        {
            public int Node;
            public Vector3 Tip;
            public Vector3 Foot;
            public float Light;
            public int Joint;
            public float Size;
        }

        private struct Clump
        {
            public Vector3 Centre;
            public float Radius;
            public float Tint;
        }

        public static void Dress(FloraMesh mesh, PlantGraph graph, List<PlantAxis> axes, int[] nodeJoint, Envelope envelope, FoliageForm form, Genes genes, bool walkable = false)
        {
            WorldRandom random = genes.Stream("placing");
            List<Site> sites = FindSites(graph, nodeJoint, form, random);
            if (sites.Count == 0) return;

            Clump[] clumps = Gather(sites, genes.Stream("clumps"), envelope);
            float drift = genes.Range("drift", -1f, 1f) * form.Colours.Drift;
            float warmth = genes.Range("warmth", -1f, 1f) * form.Colours.Drift;

            foreach (Site site in sites)
            {
                int clump = Nearest(clumps, site.Tip);
                AddCard(mesh, site, clumps[clump], envelope, form, random, drift, warmth, false, walkable);
                if (random.Float() < form.Cross) AddCard(mesh, site, clumps[clump], envelope, form, random, drift, warmth, true, walkable);
            }
        }

        private static List<Site> FindSites(PlantGraph graph, int[] nodeJoint, FoliageForm form, WorldRandom random)
        {
            graph.Children(out int[] start, out int[] _);
            var candidates = new List<(float rank, int node)>();
            for (int i = 0; i < graph.Count; i++)
            {
                PlantGraph.Node node = graph.Nodes[i];
                if (node.Dead || start[i + 1] != start[i] || node.Light < form.Shade) continue;
                candidates.Add((node.Light + random.Float() * 0.5f, i));
            }
            candidates.Sort((a, b) => b.rank.CompareTo(a.rank));

            float size = (form.CardSize.Min + form.CardSize.Max) * 0.5f;
            float cell = size * form.Spacing;
            var taken = new HashSet<(int, int, int)>();
            var sites = new List<Site>();
            foreach ((float _, int index) in candidates)
            {
                PlantGraph.Node node = graph.Nodes[index];
                (int, int, int) key = ((int)MathF.Floor(node.Position.X / cell), (int)MathF.Floor(node.Position.Y / cell), (int)MathF.Floor(node.Position.Z / cell));
                if (!taken.Add(key)) continue;

                float cardSize = random.Range(form.CardSize);

                // A card stands on its own twig, about its length back from the tip: what's painted on it is that twig. What it hangs from is the nearest wood that has a joint.
                int walker = index;
                int foot = index;
                float walked = 0f;
                float wanted = cardSize * 0.7f;
                while (nodeJoint[walker] < 0 && graph.Nodes[walker].Parent >= 0)
                {
                    int parent = graph.Nodes[walker].Parent;
                    if (walked < wanted)
                    {
                        walked += (graph.Nodes[walker].Position - graph.Nodes[parent].Position).Length;
                        foot = parent;
                    }
                    walker = parent;
                }
                if (nodeJoint[walker] < 0) continue;
                if (walked < wanted) foot = walker;

                Vector3 footAt = graph.Nodes[foot].Position;
                Vector3 tip = node.Position;
                if ((tip - footAt).Length < cardSize * 0.2f)
                {
                    int back = graph.Nodes[foot].Parent;
                    Vector3 onward = back >= 0 ? (footAt - graph.Nodes[back].Position) : Vector3.UnitY;
                    tip = footAt + (onward.LengthSquared > 1e-5f ? onward.Normalized() : Vector3.UnitY) * cardSize * 0.5f;
                }

                sites.Add(new Site { Node = index, Tip = tip, Foot = footAt, Light = node.Light, Joint = nodeJoint[walker], Size = cardSize });
                if (sites.Count >= form.MostCards) break;
            }
            return sites;
        }

        // K-means, from points far apart: the masses a crown breaks into.
        private static Clump[] Gather(List<Site> sites, WorldRandom random, Envelope envelope)
        {
            int count = Math.Clamp(sites.Count / 16, 3, 18);
            var centres = new Vector3[count];
            centres[0] = sites[random.Int(sites.Count)].Tip;
            for (int c = 1; c < count; c++)
            {
                float farthest = -1f;
                foreach (Site site in sites)
                {
                    float nearest = float.MaxValue;
                    for (int k = 0; k < c; k++) nearest = MathF.Min(nearest, (site.Tip - centres[k]).LengthSquared);
                    if (nearest <= farthest) continue;
                    farthest = nearest;
                    centres[c] = site.Tip;
                }
            }

            var sums = new Vector3[count];
            var members = new int[count];
            for (int pass = 0; pass < 4; pass++)
            {
                Array.Clear(sums);
                Array.Clear(members);
                foreach (Site site in sites)
                {
                    int best = 0;
                    float distance = float.MaxValue;
                    for (int c = 0; c < count; c++)
                    {
                        float d = (site.Tip - centres[c]).LengthSquared;
                        if (d >= distance) continue;
                        distance = d;
                        best = c;
                    }
                    sums[best] += site.Tip;
                    members[best]++;
                }
                for (int c = 0; c < count; c++)
                {
                    if (members[c] > 0) centres[c] = sums[c] / members[c];
                }
            }

            var clumps = new Clump[count];
            for (int c = 0; c < count; c++) clumps[c] = new Clump { Centre = centres[c], Radius = 1f, Tint = random.Range(-1f, 1f) };
            var furthest = new float[count];
            foreach (Site site in sites)
            {
                int c = Nearest(clumps, site.Tip);
                furthest[c] = MathF.Max(furthest[c], (site.Tip - centres[c]).Length);
            }
            for (int c = 0; c < count; c++) clumps[c].Radius = MathF.Max(furthest[c], 1f);
            return clumps;
        }

        private static int Nearest(Clump[] clumps, Vector3 at)
        {
            int best = 0;
            float distance = float.MaxValue;
            for (int c = 0; c < clumps.Length; c++)
            {
                float d = (at - clumps[c].Centre).LengthSquared;
                if (d >= distance) continue;
                distance = d;
                best = c;
            }
            return best;
        }

        private static void AddCard(FloraMesh mesh, in Site site, in Clump clump, Envelope envelope, FoliageForm form, WorldRandom random, float drift, float warmth, bool across, bool walkable)
        {
            Vector3 heading = site.Tip - site.Foot;
            heading = heading.LengthSquared > 1e-6f ? heading.Normalized() : Vector3.UnitY;
            Vector3 outward = envelope.Outward(site.Tip);

            Vector3 axis;
            Vector3 facing;
            switch (form.Pose)
            {
                case CardPose.Hanging:
                    axis = (-Vector3.UnitY + heading * 0.18f + Jitter(random, form.Tilt * 0.3f)).Normalized();
                    facing = new Vector3(outward.X, 0f, outward.Z);
                    break;
                case CardPose.Upright:
                    axis = (Vector3.UnitY * 0.8f + heading + Jitter(random, form.Tilt * 0.5f)).Normalized();
                    facing = new Vector3(outward.X, 0.3f, outward.Z);
                    break;
                default:
                    axis = (heading + outward * 0.35f + Jitter(random, form.Tilt * 0.4f)).Normalized();
                    facing = Vector3.UnitY * 0.85f + outward * 0.5f + Jitter(random, form.Tilt);
                    break;
            }
            if (facing.LengthSquared < 1e-4f) facing = Vector3.UnitY;
            Vector3 side = Vector3.Cross(axis, facing);
            if (side.LengthSquared < 1e-5f) side = Scaffold.Perpendicular(axis);
            side.Normalize();
            if (across) side = (side * 0.45f + Vector3.Cross(axis, side) * 0.9f).Normalized();
            Vector3 normal = Vector3.Cross(side, axis).Normalized();

            float length = site.Size * random.Range(0.85f, 1.15f);
            float width = length * form.Aspect;
            Vector3 foot = site.Foot;

            // The card's own joint: it bends from where it leaves the wood, carried by what that does.
            int joint = mesh.AddJoint((walkable ? PlantJoint.Leaf : PlantJoint.Spray).Between(foot, foot + axis * length, site.Joint));
            int variation = random.Int(CardLibrary.CellsPerLayer);
            CardLibrary.Cell(variation, out Vector2 uvMin, out Vector2 uvMax);
            bool mirror = random.Float() < 0.5f;
            int material = CardLibrary.Material(form.Card);

            const int Across = 2, Along = 3;
            int first = mesh.VertexCount;
            for (int j = 0; j <= Along; j++)
            {
                float v = (float)j / Along;
                for (int i = 0; i <= Across; i++)
                {
                    float u = (float)i / Across;
                    float x = u * 2f - 1f;
                    Vector3 at = foot
                        + axis * (length * v)
                        + side * (x * width * 0.5f)
                        + normal * (MathF.Abs(x) * width * form.Fold * 0.5f - form.Droop * length * v * v);
                    at.Y = MathF.Max(at.Y, LowestCard);     // What droops onto the ground lies on it. Under it, it's only paying to be drawn.

                    // Soft shape: the clump's sphere, a little of the crown's, and a lean to the sky.
                    Vector3 fromClump = (at - clump.Centre) / clump.Radius;
                    Vector3 shell = (fromClump + envelope.Outward(at) * 0.5f + Vector3.UnitY * 0.35f).Normalized();
                    Vector3 face = (normal - side * (x * form.Fold)).Normalized();
                    Vector3 blended = Vector3.Lerp(face, shell, form.Shell).Normalized();
                    Vector3 tangent = (side - blended * Vector3.Dot(side, blended)).Normalized();

                    float top = Math.Clamp(fromClump.Y * 0.5f + 0.5f, 0f, 1f);
                    float rim = Math.Clamp(fromClump.Length, 0f, 1f);
                    float tone = Math.Clamp(0.08f + 0.5f * site.Light * site.Light + 0.32f * top + 0.14f * rim - 0.02f, 0f, 1f);
                    Vector3 colour = form.Colours.At(tone);
                    colour *= 1f + clump.Tint * form.Colours.Patches + drift;
                    colour.X *= 1f + warmth * 0.8f + clump.Tint * form.Colours.Patches * 0.5f;
                    colour.Z *= 1f - warmth * 0.6f;

                    Vector2 uv = new Vector2(MathHelper.Lerp(uvMin.X, uvMax.X, mirror ? 1f - u : u), MathHelper.Lerp(uvMin.Y, uvMax.Y, v));
                    mesh.Add(new FloraVertex { Position = at, Normal = blended, Tangent = mirror ? -tangent : tangent, Colour = colour, UV = uv, Material = material, Joint = joint });
                }
            }

            const int Row = Across + 1;
            for (int j = 0; j < Along; j++)
            {
                for (int i = 0; i < Across; i++)
                {
                    int a = first + j * Row + i;
                    mesh.Quad(a, a + 1, a + Row + 1, a + Row);
                }
            }
        }

        private static Vector3 Jitter(WorldRandom random, float amount) => new Vector3(random.Range(-1f, 1f), random.Range(-1f, 1f), random.Range(-1f, 1f)) * amount;
    }
}
