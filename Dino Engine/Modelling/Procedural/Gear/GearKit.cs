using Dino_Engine.Modelling.Model;
using OpenTK.Mathematics;

namespace Dino_Engine.Modelling.Procedural.Gear
{
    /// <summary>
    /// The details every weapon and piece of armour draws on, each worked in the piece's own <see cref="Motif"/> so that one hand made all of it: the turned end of anything, the end of a bar, a stone
    /// in its setting, a scroll of wire, a tassel, a trophy taken from a beast, rows of rivets. Nothing here knows what it's on.
    /// </summary>
    public static class GearKit
    {
        /// <summary>
        /// A turned end, standing up y from the origin: a pommel, a finial, the butt of a haft, the top of a sceptre. The motif decides what it is: a knob drawn through a few points, a knob with a
        /// twisted ring round its belly, a bud that comes to a point, a faceted knob, one that ends in a fang, or a fluted crown.
        /// </summary>
        public static void Terminal(GearMesh mesh, GearLook look, Genes genes, float height, float radius, VertexMaterial material)
        {
            float waist = genes.Range("waist", 0.2f, 0.7f);
            float belly = genes.Range("belly.at", 0.3f, 0.7f);
            Profile outline = new Profile(
                (0f, genes.Range("foot", 0.18f, 0.5f)),
                (belly * 0.5f, genes.Range("low", 0.6f, 0.95f)),
                (belly, 1f),
                (0.88f, genes.Range("shoulder", 0.35f, 0.8f)),
                (1f, waist * 0.6f));
            float squash = genes.Range("squash", 0.62f, 1f);

            switch (look.Motif)
            {
                case Motif.Facet:
                    int facets = genes.Count("facets", 5, 9);
                    mesh.Lathe(Section.Polygon(facets, genes.Unit("turn")), height, genes.Count("steps", 3, 4), t => radius * outline.At(t), material, squash: squash);
                    break;
                case Motif.Flute:
                    mesh.Lathe(Section.Fluted(24, genes.Count("flutes", 6, 12), genes.Range("depth", 0.12f, 0.22f)), height, 8, t => radius * outline.At(t), material, squash: squash);
                    mesh.At(Matrix4.CreateTranslation(0f, height, 0f), () => mesh.Lathe(Section.Round(10), radius * 0.3f, 4, t => radius * 0.22f * MathF.Sqrt(MathF.Max(1f - t * t, 0.04f)), material, capStart: false));
                    break;
                case Motif.Rope:
                    mesh.Lathe(Section.Round(14), height, 8, t => radius * outline.At(t), material, squash: squash);
                    float at = height * belly;
                    mesh.At(Matrix4.CreateTranslation(0f, at, 0f) * Matrix4.CreateScale(1f, 1f, squash), () => Twist(mesh, radius * 1.02f, radius * 0.16f, genes.Count("twists", 8, 14), material));
                    break;
                case Motif.Scroll:
                    // A bud, drawing in to a point.
                    Profile bud = new Profile((0f, genes.Range("bud.foot", 0.2f, 0.45f)), (belly * 0.8f, 1f), (0.8f, genes.Range("bud.neck", 0.35f, 0.6f)), (1f, 0.04f));
                    mesh.Lathe(Section.Round(14), height * genes.Range("bud.tall", 1.1f, 1.4f), 10, t => radius * bud.At(t), material, squash: squash);
                    break;
                case Motif.Fang:
                    mesh.Lathe(Section.Round(12), height * 0.75f, 7, t => radius * outline.At(t * 0.85f), material, squash: squash);
                    mesh.At(Matrix4.CreateTranslation(0f, height * 0.7f, 0f), () => Fang(mesh, radius * genes.Range("fang.size", 1.2f, 1.9f), radius * 0.38f, genes.Sign("fang.way"), look.Colours.Bone));
                    break;
                default:
                    mesh.Lathe(Section.Round(14), height, 8, t => radius * outline.At(t), material, squash: squash);
                    break;
            }
        }

        /// <summary>
        /// The end of a bar, at <paramref name="at"/>, going on along <paramref name="way"/> with <paramref name="up"/> for which way is up: a quillon's tip, a rim's end, a crest's. A ball, a ring, a
        /// curl rolled back on itself, a cut block, a hook, or a flared trumpet.
        /// </summary>
        public static void Tip(GearMesh mesh, GearLook look, Genes genes, Vector3 at, Vector3 way, Vector3 up, float size, VertexMaterial material)
        {
            way.Normalize();
            Vector3 side = Vector3.Cross(way, up).Normalized();
            up = Vector3.Cross(side, way).Normalized();
            Matrix4 place = GearMesh.Basis(at, side, way, up);
            switch (look.Motif)
            {
                case Motif.Rope:
                    mesh.At(Matrix4.CreateRotationX(MathF.PI / 2f) * Matrix4.CreateTranslation(0f, size * 0.9f, 0f) * place, () => mesh.Hoop(Section.Round(6), size * 0.75f, size * 0.24f, size * 0.24f, 14, material));
                    break;
                case Motif.Scroll:
                    mesh.At(place, () => Scroll(mesh, new Vector3(0f, size * 0.2f, size * 0.55f), Vector3.UnitY, Vector3.UnitZ, size * 0.9f, genes.Range("scroll.turns", 1.1f, 1.6f), size * 0.22f, material));
                    break;
                case Motif.Facet:
                    mesh.At(Matrix4.CreateTranslation(0f, -size * 0.2f, 0f) * place, () => mesh.Block(size * 0.62f, size * 1.25f, size * 0.62f, 0.35f, material));
                    break;
                case Motif.Fang:
                    mesh.At(place, () => Fang(mesh, size * 2.2f, size * 0.42f, genes.Sign("hook"), material));
                    break;
                case Motif.Flute:
                    mesh.At(place, () => mesh.Lathe(Section.Fluted(16, 8, 0.18f), size * 1.3f, 5, t => size * (0.45f + 0.55f * t * t), material));
                    break;
                default:
                    mesh.At(place, () => mesh.Lathe(Section.Round(10), size * 1.6f, 6, t => size * (0.35f + 0.65f * MathF.Sin(MathF.Min(t * 1.25f, 1f) * MathF.PI)), material));
                    break;
            }
        }

        /// <summary>A curved point, up y from the origin, hooked over towards +x or -x: a tooth, a claw, a quill's end, a spike that's been made to look like one.</summary>
        public static void Fang(GearMesh mesh, float length, float radius, float hook, VertexMaterial material)
        {
            var keys = new[] { Vector3.Zero, new Vector3(hook * length * 0.06f, length * 0.4f, 0f), new Vector3(hook * length * 0.22f, length * 0.78f, 0f), new Vector3(hook * length * 0.42f, length, 0f) };
            mesh.Sweep(Section.Super(8, 2.4f), Spine.Through(keys, 8), t => new Vector2(radius * (1f - t * 0.92f), radius * 0.8f * (1f - t * 0.92f)), material, Vector3.UnitZ, capStart: true, capEnd: false);
        }

        /// <summary>A ring of twisted wire or cord lying round y, for a band on anything turned.</summary>
        public static void Twist(GearMesh mesh, float radius, float thick, int twists, VertexMaterial material)
        {
            int steps = twists * 4;
            var rings = new Ring[steps + 1];
            for (int i = 0; i <= steps; i++)
            {
                float t = (float)i / steps;
                float angle = t * MathF.Tau;
                Vector3 outwards = new Vector3(MathF.Cos(angle), 0f, MathF.Sin(angle));
                float r = thick * (1f + 0.35f * MathF.Abs(MathF.Sin(t * twists * MathF.PI)));
                rings[i] = new Ring { Centre = outwards * radius, Side = outwards, Front = Vector3.UnitY, HalfWidth = r, HalfDepth = r };
            }
            mesh.Loft(Section.Round(6), rings, material, false, false);
        }

        /// <summary>
        /// A scroll of wire or a curl of metal, lying in the plane of <paramref name="right"/> and <paramref name="up"/> from <paramref name="start"/>, rolling inwards by <paramref name="turns"/> as it goes:
        /// the end of a volute, a filigree's curl, the curl at a quillon's tip.
        /// </summary>
        public static void Scroll(GearMesh mesh, Vector3 start, Vector3 right, Vector3 up, float radius, float turns, float wire, VertexMaterial material)
        {
            right.Normalize();
            up.Normalize();
            int steps = Math.Clamp((int)(turns * 8f), 6, 24);
            var spine = new List<Vector3>(steps + 1);
            for (int i = 0; i <= steps; i++)
            {
                float t = (float)i / steps;
                float angle = t * turns * MathF.Tau;
                float r = radius * (1f - 0.78f * t);
                spine.Add(start + right * (MathF.Sin(angle) * r) + up * (radius - MathF.Cos(angle) * r));
            }
            mesh.Sweep(Section.Round(5), spine, t => new Vector2(wire * (1f - 0.55f * t)), material, Vector3.Cross(right, up), capStart: true, capEnd: true);
        }

        /// <summary>A stone in a setting, standing up y from the origin: a cup that holds it and claws over its girdle, or a bezel round it, with the stone itself cut in facets.</summary>
        public static void Setting(GearMesh mesh, Genes genes, float stone, VertexMaterial metal, VertexMaterial gem)
        {
            int sides = genes.Count("sides", 6, 10);
            float seat = stone * genes.Range("seat", 0.35f, 0.6f);
            bool bezel = genes.Chance("bezel", 0.45f);
            mesh.Lathe(Section.Round(12), seat, 2, t => stone * (0.7f + 0.42f * t), metal, capEnd: !bezel);
            if (bezel)
            {
                mesh.At(Matrix4.CreateTranslation(0f, seat, 0f), () => mesh.Lathe(Section.Round(14), stone * 0.32f, 1, t => stone * (1.12f - 0.06f * t), metal, capStart: false, capEnd: false));
            }
            else
            {
                int claws = genes.Count("claws", 4, 6);
                for (int i = 0; i < claws; i++)
                {
                    float angle = i * (MathF.Tau / claws) + MathF.PI / claws;
                    Vector3 out_ = new Vector3(MathF.Cos(angle), 0f, MathF.Sin(angle));
                    var keys = new[] { out_ * stone * 1.05f + Vector3.UnitY * seat * 0.4f, out_ * stone * 1.14f + Vector3.UnitY * (seat + stone * 0.25f), out_ * stone * 0.82f + Vector3.UnitY * (seat + stone * 0.55f) };
                    mesh.Sweep(Section.Round(4), Spine.Through(keys, 4), t => new Vector2(stone * 0.12f * (1f - t * 0.5f)), metal, Vector3.UnitZ);
                }
            }
            mesh.At(Matrix4.CreateTranslation(0f, seat + stone * 0.08f, 0f), () => mesh.Gem(sides, stone, stone * genes.Range("crown", 0.42f, 0.7f), stone * genes.Range("pavilion", 0.25f, 0.45f), genes.Range("table", 0.42f, 0.66f), gem));
        }

        /// <summary>A tassel hanging from <paramref name="at"/>: a knot and the strands spreading out under it, which is what a cord ends in.</summary>
        public static void Tassel(GearMesh mesh, Genes genes, Vector3 at, float length, float radius, VertexMaterial cord, VertexMaterial knot)
        {
            mesh.At(Matrix4.CreateTranslation(at - Vector3.UnitY * radius * 1.6f), () => mesh.Lathe(Section.Round(8), radius * 1.6f, 4, t => radius * (0.55f + 0.45f * MathF.Sin(t * MathF.PI)), knot));
            int strands = genes.Count("strands", 6, 10);
            float spread = genes.Range("spread", 0.55f, 0.9f);
            for (int i = 0; i < strands; i++)
            {
                float angle = i * (MathF.Tau / strands) + genes.Range($"s{i}", -0.2f, 0.2f);
                Vector3 out_ = new Vector3(MathF.Cos(angle), 0f, MathF.Sin(angle));
                float reach = length * genes.Range($"l{i}", 0.85f, 1.05f);
                var keys = new[] { at - Vector3.UnitY * radius * 1.4f + out_ * radius * 0.4f, at - Vector3.UnitY * (radius * 1.4f + reach * 0.5f) + out_ * radius * spread, at - Vector3.UnitY * (radius * 1.4f + reach) + out_ * radius * spread * 1.15f };
                mesh.Sweep(Section.Round(4), Spine.Through(keys, 4), t => new Vector2(radius * 0.26f * (1f - t * 0.6f)), cord, Vector3.UnitZ);
            }
        }

        /// <summary>Rivets along a run of points on a surface, one every <paramref name="spacing"/> metres or so, each standing out along the surface's normal there.</summary>
        public static void Rivets(GearMesh mesh, IReadOnlyList<Vector3> points, IReadOnlyList<Vector3> normals, float spacing, float radius, VertexMaterial material)
        {
            if (points.Count == 0) return;
            float run = 0f, next = spacing * 0.5f;
            for (int i = 1; i < points.Count; i++)
            {
                float step = (points[i] - points[i - 1]).Length;
                while (next <= run + step)
                {
                    float t = step > 1e-6f ? (next - run) / step : 0f;
                    mesh.Rivet(Vector3.Lerp(points[i - 1], points[i], t), Vector3.Lerp(normals[i - 1], normals[i], t), radius, material);
                    next += spacing;
                }
                run += step;
            }
        }

        /// <summary>A tooth or a claw on a cord, hung from <paramref name="at"/> by its root: what a Defender takes from a beast they've brought down.</summary>
        public static void Trophy(GearMesh mesh, Genes genes, Vector3 at, float size, VertexMaterial tooth, VertexMaterial cord)
        {
            float hook = genes.Sign("hook");
            mesh.At(Matrix4.CreateRotationZ(MathF.PI) * Matrix4.CreateRotationY(genes.Range("turn", -0.6f, 0.6f)) * Matrix4.CreateTranslation(at), () =>
            {
                mesh.Lathe(Section.Round(6), size * 0.18f, 1, _ => size * 0.16f, cord);
                mesh.At(Matrix4.CreateTranslation(0f, size * 0.14f, 0f), () => Fang(mesh, size, size * genes.Range("girth", 0.13f, 0.2f), hook, tooth));
            });
        }
    }
}
