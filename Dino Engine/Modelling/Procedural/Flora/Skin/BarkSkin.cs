using Dino_Engine.Modelling.Procedural.Gear;
using OpenTK.Mathematics;

namespace Dino_Engine.Modelling.Procedural.Flora
{
    /// <summary>What covers the wood. A material is a kind of surface and its colour comes from here, so one bark can be every bark of that kind.</summary>
    public enum BarkKind
    {
        Furrowed,   // Deep ridges: oak, elm, old maple.
        Plated,     // Flaking plates: pine, spruce.
        Papery,     // Peeling, with dark marks: birch.
        Smooth,     // Tight, with lenticels: beech, cherry, young maple.
        Fibrous,    // Long soft strips: redwood, cedar, cypress.
        Ringed,     // Scarred in rings: palms.
        Cane,       // Smooth and jointed: bamboo, reeds.
        Bare,       // Silvered wood with no bark left.
        Charred,    // Burnt to scales.
        Stem,       // Green and soft: anything that isn't wood.
    }

    public sealed class BarkForm
    {
        public BarkKind Kind = BarkKind.Furrowed;
        public Vector3 Colour = new Vector3(0.8f, 0.72f, 0.62f);        // Multiplies the material, which is what makes it this tree's bark.
        public Vector3 YoungColour = new Vector3(0.62f, 0.52f, 0.42f);  // What it comes to on thin branches.
        public Vector3? UpperColour;                                    // What it turns to up the trunk, as a Scots pine goes orange.
        public float UpperFrom = 0.5f;
        public float Tile = 2.4f;           // Metres the texture covers.
        public float Bumps = 0.09f;         // How lumpy a stem is, as a share of its radius.
        public bool RoundTips;              // Stems end in a dome and not a point.
        public int Ribs;                    // Ridges running its whole length: a cactus.
        public float RibDepth = 0.12f;
        public float NodeSpacing;           // Metres between the joints of a cane, or 0.
        public float Moss;                  // 0 to 1, how much of it is under moss.
        public Vector3 MossColour = new Vector3(0.2f, 0.3f, 0.07f);
        public int MostSides = 16;
    }

    /// <summary>
    /// Turns axes into tubes, as generalized cylinders (Bloomenthal 1985) on parallel transport frames, so nothing twists along a bend. Rings are spaced by how thick a stem is, and
    /// have as many sides as that's worth: a detail smaller than a pixel from this camera is a fault, so the top of a tree costs a fraction of its foot.
    /// </summary>
    internal static class BarkSkin
    {
        private const float RingPitch = 2.2f;   // Diameters between rings, on a stem that's going straight.
        private const float LongestRing = 2.6f; // Metres, or a tall trunk is a cone between its foot and the first thing that changes it.

        private struct Ring
        {
            public Vector3 Centre;
            public Vector3 Along;
            public Vector3 Side;
            public float Radius;
            public float Travelled;
            public float Share;
            public float Light;     // What reaches it: wood inside a crown is much darker than a sun would make it, as a sun can't get in.
        }

        public static void Wrap(FloraMesh mesh, List<PlantAxis> axes, BarkForm bark, TreeForm form, Genes genes, float trunkRadius, float height, int lobes, float lobeFacing)
        {
            uint seed = (uint)genes.Stream("seed").NextULong();
            float lobeDepth = genes.Range("lobe depth", form.Trunk.LobeDepth);
            float flare = genes.Range("flare", form.Trunk.Flare);
            float flareHeight = form.Trunk.FlareHeight * height;
            float tint = genes.Range("tint", 0.88f, 1.12f);
            Vector3 mossAway = Vector3.Normalize(new Vector3(genes.Range("moss x", -1f, 1f), 0.45f, genes.Range("moss z", -1f, 1f)));

            foreach (PlantAxis axis in axes)
            {
                bool trunk = axis.Order == 0 && !axis.Root;
                var style = new SkinStyle
                {
                    Bark = bark,
                    Seed = seed + (uint)axis.Points.Count * 7919u,
                    Trunk = trunk,
                    Lobes = trunk ? lobes : 0,
                    LobeDepth = lobeDepth,
                    LobeFacing = lobeFacing,
                    Flare = trunk ? flare : 0f,
                    FlareHeight = flareHeight,
                    Tint = tint,
                    MossAway = mossAway,
                    TreeHeight = height,
                    ThickRadius = trunkRadius,
                };
                Skin(mesh, axis, style);
            }
        }

        public struct SkinStyle
        {
            public BarkForm Bark;
            public uint Seed;
            public bool Trunk;
            public int Lobes;
            public float LobeDepth;
            public float LobeFacing;    // Which way in the world the first ridge looks, in radians.
            public float Flare;
            public float FlareHeight;
            public float Tint;
            public Vector3 MossAway;
            public float TreeHeight;
            public float ThickRadius;
        }

        /// <summary>Wood with no tree to go with it: a log or a stump. It's given no joints, as nothing moves it.</summary>
        public static void WrapPlain(FloraMesh mesh, List<PlantAxis> axes, BarkForm bark, Genes genes, float radius, float height)
        {
            uint seed = (uint)genes.Stream("seed").NextULong();
            Vector3 mossAway = Vector3.Normalize(new Vector3(genes.Range("moss x", -0.3f, 0.3f), 1f, genes.Range("moss z", -0.3f, 0.3f)));
            float tint = genes.Range("tint", 0.88f, 1.12f);
            foreach (PlantAxis axis in axes)
            {
                axis.FirstLink = -1;
                var style = new SkinStyle { Bark = bark, Seed = seed + (uint)axis.Points.Count * 7919u, Tint = tint, MossAway = mossAway, TreeHeight = MathF.Max(height, 3f), ThickRadius = radius };
                Skin(mesh, axis, style);
            }
        }

        public static void Skin(FloraMesh mesh, PlantAxis axis, in SkinStyle style)
        {
            axis.NodeRings = style.Bark.NodeSpacing;
            List<Ring> rings = Rings(axis, style.Trunk ? style.FlareHeight : 0f);
            if (rings.Count < 2) return;
            if (style.Bark.RoundTips) Dome(rings);

            int material = FloraMaterials.Bark(style.Bark.Kind);
            float aroundTiles = MathF.Max(MathF.Round(MathF.Tau * rings[0].Radius / style.Bark.Tile), 1f);
            int previousStart = -1, previousSides = 0;

            for (int r = 0; r < rings.Count; r++)
            {
                Ring ring = rings[r];
                bool last = r == rings.Count - 1;
                int sides = Sides(ring.Radius * (1f + style.Flare * FlareAt(ring.Centre.Y, style)), style.Bark.MostSides);
                int joint = Rig.LinkAt(axis, ring.Share);
                Vector3 other = Vector3.Cross(ring.Along, ring.Side);
                int start = mesh.VertexCount;

                if (last && !axis.Cut)
                {
                    // A stem comes to a point, and that point needs no ring: one vertex closes it.
                    Vector3 tip = ring.Centre + ring.Along * ring.Radius;
                    mesh.Add(new FloraVertex
                    {
                        Position = tip,
                        Normal = ring.Along,
                        Tangent = ring.Side,
                        Colour = ColourAt(tip, ring.Along, ring.Radius, ring.Share, ring.Light, style),
                        UV = new Vector2(aroundTiles * 0.5f, ring.Travelled / style.Bark.Tile),
                        Material = material,
                        Joint = joint,
                    });
                    for (int i = 0; i < previousSides; i++) mesh.Triangle(previousStart + i, previousStart + i + 1, start);
                    break;
                }

                for (int i = 0; i <= sides; i++)
                {
                    float angle = i * (MathF.Tau / sides);
                    Vector3 at = Surface(ring, other, angle, style, out Vector3 normal, out Vector3 tangent);
                    mesh.Add(new FloraVertex
                    {
                        Position = at,
                        Normal = normal,
                        Tangent = tangent,
                        Colour = ColourAt(at, normal, ring.Radius, ring.Share, ring.Light, style),
                        UV = new Vector2(aroundTiles * i / sides, ring.Travelled / style.Bark.Tile),
                        Material = material,
                        Joint = joint,
                    });
                }

                if (previousStart >= 0) Stitch(mesh, previousStart, previousSides, start, sides);
                else if (axis.CutFoot) AddFace(mesh, ring, start, sides, joint, style, -1f);
                if (last) AddFace(mesh, ring, start, sides, joint, style, 1f);
                previousStart = start;
                previousSides = sides;
            }
        }

        // The sawn or snapped end of a stem: a fan of bare wood, with the growth rings of the bark's own colour going out from the middle.
        private static void AddFace(FloraMesh mesh, in Ring ring, int ringStart, int sides, int joint, in SkinStyle style, float facing)
        {
            int material = FloraMaterials.Bark(BarkKind.Bare);
            Vector3 normal = ring.Along * facing;
            Vector3 heart = style.Bark.Colour * 0.2f + new Vector3(0.14f, 0.1f, 0.06f);
            int hub = mesh.Add(new FloraVertex { Position = ring.Centre + ring.Along * (ring.Radius * 0.12f * facing), Normal = normal, Tangent = ring.Side, Colour = heart * 0.6f, UV = new Vector2(0.5f), Material = material, Joint = joint });
            int first = mesh.VertexCount;
            for (int i = 0; i <= sides; i++)
            {
                FloraVertex rim = mesh.Vertices[ringStart + i];
                float jag = 0.35f * FloraNoise.Wave(i * 1.7f, style.Seed);
                rim.Position += ring.Along * (ring.Radius * jag * 0.3f);
                rim.Normal = normal;
                rim.Tangent = ring.Side;
                rim.Colour = heart;
                rim.UV = new Vector2(i / (float)sides, 0f);
                rim.Material = material;
                mesh.Add(rim);
            }
            for (int i = 0; i < sides; i++)
            {
                if (facing > 0f) mesh.Triangle(hub, first + i, first + i + 1);
                else mesh.Triangle(hub, first + i + 1, first + i);
            }
        }

        private static int Sides(float radius, int most)
        {
            int sides = radius < 0.05f ? 3 : radius < 0.09f ? 4 : radius < 0.16f ? 5 : radius < 0.26f ? 6 : radius < 0.42f ? 8 : radius < 0.7f ? 10 : radius < 1.1f ? 12 : 16;
            return Math.Min(sides, most);
        }

        // Joins two rings with different numbers of sides, by walking both around together.
        private static void Stitch(FloraMesh mesh, int a, int sidesA, int b, int sidesB)
        {
            int i = 0, j = 0;
            while (i < sidesA || j < sidesB)
            {
                float nextA = (i + 1f) / sidesA;
                float nextB = (j + 1f) / sidesB;
                if (j >= sidesB || (i < sidesA && nextA <= nextB))
                {
                    mesh.Triangle(a + i, a + i + 1, b + j);
                    i++;
                }
                else
                {
                    mesh.Triangle(a + i, b + j + 1, b + j);
                    j++;
                }
            }
        }

        private static float FlareAt(float y, in SkinStyle style)
        {
            if (style.Flare <= 0f) return 0f;
            float h = MathF.Max(y, 0f) / MathF.Max(style.FlareHeight, 0.01f);
            return MathF.Exp(-h * 2.4f);
        }

        // How far the surface is from the axis, at an angle round it. Flare and ridges are Weber and Penn's: exponential at the foot, and a sine round it, here sharpened so that
        // the ridges run up out of the roots like a buttress rather than a corrugation.
        private static float RadiusAt(in Ring ring, Vector3 outward, float angle, in SkinStyle style)
        {
            float radius = ring.Radius;
            if (style.Bark.Bumps > 0f)
            {
                Vector3 sample = (ring.Centre + outward * ring.Radius) * (1.4f / MathF.Max(ring.Radius * 1.5f + 0.25f, 0.3f));
                radius *= 1f + style.Bark.Bumps * (FloraNoise.Fbm(sample, style.Seed, 2) * 2f - 1f) * 1.6f;
            }
            if (style.Bark.Ribs > 0) radius *= 1f + style.Bark.RibDepth * (MathF.Pow(0.5f + 0.5f * MathF.Cos(style.Bark.Ribs * angle), 1.6f) - 0.5f) * 2f;
            if (style.Bark.NodeSpacing > 0f)
            {
                float from = ring.Travelled / style.Bark.NodeSpacing;
                radius *= 1f + 0.14f * Smooth(0.12f, 0f, MathF.Abs(from - MathF.Round(from)));
            }
            if (style.Trunk)
            {
                float flare = FlareAt(ring.Centre.Y, style);
                float facing = MathF.Atan2(outward.Z, outward.X) - style.LobeFacing;
                float ridge = style.Lobes > 0 ? MathF.Pow(0.5f + 0.5f * MathF.Cos(style.Lobes * facing), 2.5f) : 0f;
                radius *= 1f + style.Flare * flare + ridge * flare * style.Flare * 1.6f;
                radius *= 1f + style.LobeDepth * 0.3f * ridge * (1f - MathF.Min(ring.Share * 2.5f, 1f));
            }
            return radius;
        }

        private static Vector3 Surface(Ring ring, Vector3 other, float angle, SkinStyle style, out Vector3 normal, out Vector3 tangent)
        {
            const float Nudge = 0.08f;
            Vector3 Out(float a) => ring.Side * MathF.Cos(a) + other * MathF.Sin(a);
            Vector3 Point(float a)
            {
                Vector3 outward = Out(a);
                return ring.Centre + outward * RadiusAt(ring, outward, a, style);
            }

            Vector3 at = Point(angle);
            tangent = (Point(angle + Nudge) - Point(angle - Nudge)).Normalized();

            // The surface leans in as a stem tapers or a flare dies away.
            Vector3 outwardHere = Out(angle);
            float slope = 0f;
            if (style.Trunk && style.Flare > 0f)
            {
                float flare = FlareAt(ring.Centre.Y, style);
                slope = style.Flare * flare * 2.4f * ring.Radius / MathF.Max(style.FlareHeight, 0.01f);
            }
            Vector3 along = (ring.Along - outwardHere * slope).Normalized();
            normal = Vector3.Cross(tangent, along);
            if (Vector3.Dot(normal, outwardHere) < 0f) normal = -normal;
            normal = normal.LengthSquared > 1e-6f ? normal.Normalized() : outwardHere;
            tangent = (tangent - normal * Vector3.Dot(tangent, normal)).Normalized();
            return at;
        }

        private static Vector3 ColourAt(Vector3 at, Vector3 normal, float radius, float share, float light, in SkinStyle style)
        {
            BarkForm bark = style.Bark;
            float young = 1f - MathF.Min(radius / MathF.Max(style.ThickRadius * 0.28f, 0.05f), 1f);
            Vector3 colour = Vector3.Lerp(bark.Colour, bark.YoungColour, young * young);
            if (bark.UpperColour is Vector3 upper)
            {
                float climbed = at.Y / MathF.Max(style.TreeHeight, 1f);
                colour = Vector3.Lerp(colour, upper, Smooth(bark.UpperFrom - 0.12f, bark.UpperFrom + 0.12f, climbed));
            }

            // Bark is never one colour: slow patches of damp and lichen, in a colour that stays this bark's.
            float patch = FloraNoise.Fbm(at * 0.55f, style.Seed + 3u, 3);
            colour *= style.Tint * (0.72f + 0.56f * patch);

            if (bark.Moss > 0f)
            {
                // Moss likes the damp foot of a tree, the upper side of anything leaning, and one side more than the other.
                float wet = MathF.Exp(-MathF.Max(at.Y, 0f) / MathF.Max(style.TreeHeight * 0.16f, 1f));
                float faces = Math.Clamp(Vector3.Dot(normal, style.MossAway) * 0.6f + 0.45f, 0f, 1f);
                float grows = FloraNoise.Fbm(at * 0.9f + new Vector3(9f), style.Seed + 5u, 3);
                float moss = Smooth(0.42f, 0.62f, grows + (wet * 0.5f + faces * 0.32f - 0.42f) * bark.Moss * 1.8f) * MathF.Min(bark.Moss * 1.6f, 1f);
                colour = Vector3.Lerp(colour, bark.MossColour * (0.7f + 0.6f * grows), moss);
            }

            // A little darker where it meets the ground, and in the crook of what grew from it.
            colour *= 0.7f + 0.3f * Smooth(-0.2f, 1.4f, at.Y);
            if (bark.NodeSpacing > 0f)
            {
                float from = at.Y / bark.NodeSpacing;
                colour *= 1f - 0.55f * Smooth(0.05f, 0f, MathF.Abs(from - MathF.Round(from)));
            }
            return colour * light;
        }

        private static float Smooth(float from, float to, float value)
        {
            float t = Math.Clamp((value - from) / (to - from), 0f, 1f);
            return t * t * (3f - 2f * t);
        }

        // Replaces the last ring with a few that close in on a circle.
        private static void Dome(List<Ring> rings)
        {
            Ring last = rings[^1];
            float radius = rings[^2].Radius;
            rings.RemoveAt(rings.Count - 1);
            const int Steps = 4;
            for (int i = 0; i <= Steps; i++)
            {
                float angle = (i + 1f) / (Steps + 1f) * MathF.PI * 0.5f;
                Ring ring = last;
                ring.Centre = last.Centre + last.Along * (MathF.Sin(angle) * radius);
                ring.Radius = MathF.Cos(angle) * radius;
                ring.Travelled = last.Travelled + MathF.Sin(angle) * radius;
                rings.Add(ring);
            }
        }

        private static List<Ring> Rings(PlantAxis axis, float footHeight)
        {
            var rings = new List<Ring>();
            List<Vector3> points = axis.Points;
            int count = points.Count;
            float length = axis.Length;
            if (length < 1e-3f) return rings;

            Vector3 Sample(float share, out float radius)
            {
                float wanted = share * (count - 1);
                int i = Math.Min((int)wanted, count - 2);
                float t = wanted - i;
                radius = MathHelper.Lerp(axis.Radii[i], axis.Radii[i + 1], t);
                Vector3 p0 = points[Math.Max(i - 1, 0)], p1 = points[i], p2 = points[i + 1], p3 = points[Math.Min(i + 2, count - 1)];
                float t2 = t * t, t3 = t2 * t;
                return 0.5f * (2f * p1 + (p2 - p0) * t + (2f * p0 - 5f * p1 + 4f * p2 - p3) * t2 + (3f * p1 - p0 - 3f * p2 + p3) * t3);
            }

            // Walks along the curve in steps set by how thick it is there, and closer together where it's bending.
            float nodes = axis.NodeRings;
            var centres = new List<(Vector3 at, float radius, float travelled)>();
            float share = 0f;
            Vector3 point = Sample(0f, out float r0);
            centres.Add((point, r0, 0f));
            float travelled = 0f;
            float per = 1f / MathF.Max(count - 1, 1);
            Vector3 lastDirection = (points[1] - points[0]).Normalized();
            while (share < 1f)
            {
                Vector3 here = Sample(share, out float radius);
                float wanted = Math.Clamp(radius * 2f * RingPitch, 0.22f, LongestRing);
                if (nodes > 0f) wanted = MathF.Min(wanted, nodes * 0.5f);
                if (here.Y < footHeight * 1.6f) wanted = MathF.Min(wanted, MathF.Max(footHeight * 0.28f, 0.2f));
                float step = wanted / MathF.Max(length, 1e-3f);
                float next = MathF.Min(share + step, 1f);
                Vector3 there = Sample(next, out float radiusThere);
                Vector3 direction = (there - here).LengthSquared > 1e-8f ? (there - here).Normalized() : lastDirection;
                if (Vector3.Dot(direction, lastDirection) < 0.94f && step > per * 0.5f)
                {
                    next = MathF.Min(share + step * 0.5f, 1f);
                    there = Sample(next, out radiusThere);
                }
                travelled += (there - here).Length;
                share = next;
                if (share >= 0.999f || 1f - share < step * 0.35f)
                {
                    there = Sample(1f, out radiusThere);
                    share = 1f;
                }
                centres.Add((there, radiusThere, travelled));
                lastDirection = direction;
            }

            float total = MathF.Max(travelled, 1e-3f);
            Vector3 along = (centres[1].at - centres[0].at).Normalized();
            Vector3 side = Scaffold.Perpendicular(along);
            for (int i = 0; i < centres.Count; i++)
            {
                Vector3 next = i + 1 < centres.Count ? centres[i + 1].at - centres[i].at : centres[i].at - centres[i - 1].at;
                if (next.LengthSquared > 1e-8f)
                {
                    Vector3 nextAlong = next.Normalized();
                    // Parallel transport: the side turns by what the axis turned by, and no more.
                    Vector3 axisOfTurn = Vector3.Cross(along, nextAlong);
                    float sine = axisOfTurn.Length;
                    if (sine > 1e-5f)
                    {
                        axisOfTurn /= sine;
                        float angle = MathF.Atan2(sine, Vector3.Dot(along, nextAlong));
                        side = Vector3.Transform(side, Quaternion.FromAxisAngle(axisOfTurn, angle));
                    }
                    along = nextAlong;
                    side = (side - along * Vector3.Dot(side, along)).Normalized();
                }
                float share2 = centres[i].travelled / total;
                float light = axis.Lights.Count > 0 ? axis.Lights[Math.Min((int)(share2 * (axis.Lights.Count - 1)), axis.Lights.Count - 1)] : 1f;
                rings.Add(new Ring { Centre = centres[i].at, Along = along, Side = side, Radius = centres[i].radius, Travelled = centres[i].travelled, Share = share2, Light = light });
            }
            return rings;
        }
    }
}
