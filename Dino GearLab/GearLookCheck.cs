using Dino_Engine.Modelling.Model;
using Dino_Engine.Modelling.Procedural.Gear;
using OpenTK.Mathematics;

namespace Dino_GearLab
{
    /// <summary>
    /// What a look must and mustn't be, measured on what's made rather than on what was chosen. No piece may wear the Reich's colours, black iron, oxblood or brass (LORE.md), over more than a
    /// sliver of itself: a shadowed underside is a shade, a black plate is a uniform. And a legendary piece must never look like anyone's: what's precious, set or lit on it has to come to several
    /// times what it does on the plain ones of its kind, every kind. Exit code 1 if not. See GEAR.md.
    /// </summary>
    internal static class GearLookCheck
    {
        // The most of a piece's surface that may be in one of the Reich's colours.
        private const float MostReichs = 0.04f;
        // How many times the splendour of a plain piece a legendary one comes to, at the least, between their medians.
        private const float Splendour = 3f;

        public static int Run(Options options)
        {
            int count = options.Int("count", 200);
            int failures = 0;
            foreach (GearKind kind in Enum.GetValues<GearKind>())
            {
                if (kind is GearKind.Book or GearKind.OpenBook or GearKind.Page or GearKind.Sentence or GearKind.Orders) continue;
                if (options.Has("kind") && !string.Equals(options.Text("kind", ""), kind.ToString(), StringComparison.OrdinalIgnoreCase)) continue;
                failures += Check(kind, count);
            }
            Console.WriteLine(failures == 0 ? "All looks hold" : $"{failures} failed");
            return failures == 0 ? 0 : 1;
        }

        private static int Check(GearKind kind, int count)
        {
            float worstReichs = 0f;
            ulong worstSeed = 0;
            var plain = new List<float>();
            var legendary = new List<float>();
            for (int i = 0; i < count; i++)
            {
                GearSpec spec = GearCheck.Sample(kind, (ulong)i * 2654435761UL + 11, i);
                (float reichs, float shine) = Measure(GearForge.Make(spec));
                if (reichs > worstReichs)
                {
                    worstReichs = reichs;
                    worstSeed = spec.Seed;
                }
                (float plainReichs, float plainShine) = Measure(GearForge.Make(spec with { Grade = 0, Theme = GearTheme.None }));
                (float legendReichs, float legendShine) = Measure(GearForge.Make(spec with { Grade = 4 }));
                plain.Add(plainShine);
                legendary.Add(legendShine);
                if (MathF.Max(plainReichs, legendReichs) > worstReichs)
                {
                    worstReichs = MathF.Max(plainReichs, legendReichs);
                    worstSeed = spec.Seed;
                }
            }
            plain.Sort();
            legendary.Sort();
            float plainMedian = plain[plain.Count / 2], legendMedian = legendary[legendary.Count / 2];
            float legendLow = legendary[legendary.Count / 10];
            bool colours = worstReichs <= MostReichs;
            bool splendid = legendMedian >= MathF.Max(plainMedian * Splendour, 0.04f) && legendLow > plainMedian;
            Console.WriteLine($"  {(colours && splendid ? "ok  " : "FAIL")}  {kind,-11} the Reich's colours on {worstReichs * 100f:0.0}% at most (seed {worstSeed}); splendour {plainMedian * 100f:0.0}% plain, {legendMedian * 100f:0.0}% legendary, {legendLow * 100f:0.0}% at the legendary tenth");
            return colours && splendid ? 0 : 1;
        }

        // What share of a piece's surface is in one of the Reich's colours, and what share is precious: gold or silver, a stone, or what's lit.
        private static (float reichs, float shine) Measure(GearMesh made)
        {
            Mesh mesh = made.Build(false);
            float total = 0f, reichs = 0f, shine = 0f;
            foreach (Face face in mesh.faces)
            {
                float area = Vector3.Cross(face.B.position - face.A.position, face.C.position - face.A.position).Length * 0.5f;
                int material = face.A.materialTextureIndex;
                Vector3 albedo = face.A.colour.ToVector3() * GearMaterials.AlbedoOf(material);
                total += area;
                if (material == GearMaterials.Glow || material == GearMaterials.Gem)
                {
                    shine += area;
                    continue;
                }
                bool metal = GearMaterials.IsMetal(material);
                if (GearSwatches.IsReichs(albedo, metal)) reichs += area;
                (float hue, float saturation, float value) = GearSwatches.Hsv(albedo);
                if (metal && value > 0.8f && (saturation < 0.08f || hue is >= 15f and <= 50f)) shine += area;
            }
            return total > 0f ? (reichs / total, shine / total) : (0f, 0f);
        }
    }
}
