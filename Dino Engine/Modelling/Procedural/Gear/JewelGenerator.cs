using Dino_Engine.Modelling.Model;
using OpenTK.Mathematics;

namespace Dino_Engine.Modelling.Procedural.Gear
{
    /// <summary>
    /// Rings and amulets, life size as they're worn and drawn bigger than that on the ground and in a picture, where they're seen close. A ring is a band, which can be flat, rounded, wire, two
    /// wires twisted or a signet's broad shoulders, and what sits on it: a stone in a bezel or claws, a cluster of small ones, a beast's fang for those who take them, or nothing; a powerful one's
    /// runes run round it lit. An amulet is a chain of real links or a cord, and what hangs from it: a medallion with something raised on it, a stone in its setting, a fang bound in wire or a charm
    /// of carved bone. Legendary pieces are gilt, set with what glows, and scrolled about with filigree. See GEAR.md.
    /// </summary>
    public static class JewelGenerator
    {
        private const float FingerRadius = 0.065f;

        /// <summary>A ring lying flat around y, with its top out along +x.</summary>
        public static void Ring(GearMesh mesh, GearLook look)
        {
            Genes genes = look.Genes;
            GearColours c = look.Colours;
            VertexMaterial metal = Metal(look);

            float radius = FingerRadius * genes.Range("radius", 1f, 1.12f);
            int band = genes.Pick("band", look.Legendary ? 1f : 2.5f, 2.5f, look.Legendary ? 0f : 1.5f, 1f + look.Craft, 1f + look.Heft * 1.5f);
            float wide = FingerRadius * genes.Range("wide", 0.18f, 0.5f) * (0.8f + 0.5f * look.Heft);
            float thick = FingerRadius * genes.Range("thick", 0.1f, 0.2f);
            bool signet = band == 4;

            mesh.Counted("band", () =>
            {
                switch (band)
                {
                    case 2:
                        // Round wire, thin and plain.
                        Band(mesh, radius, Section.Round(8), thick * 0.9f, thick * 0.9f, 0f, metal);
                        break;
                    case 3:
                        // Two wires twisted round each other.
                        for (int k = 0; k < 2; k++)
                        {
                            int strand = k;
                            var spine = new List<Vector3>();
                            for (int i = 0; i <= 64; i++)
                            {
                                float a = i / 64f * MathF.Tau;
                                float twist = a * genes.Count("twists", 6, 10) + strand * MathF.PI;
                                Vector3 out_ = new Vector3(MathF.Cos(a), 0f, MathF.Sin(a));
                                spine.Add(out_ * (radius + MathF.Cos(twist) * thick * 0.7f) + Vector3.UnitY * MathF.Sin(twist) * thick * 0.7f);
                            }
                            mesh.Sweep(Section.Round(6), spine, _ => new Vector2(thick * 0.6f), metal, Vector3.UnitY, capStart: false, capEnd: false);
                        }
                        break;
                    case 4:
                        // A signet: shoulders swelling up to a broad flat top.
                        Band(mesh, radius, Section.Super(8, 3f), thick, wide * 0.6f, 1.6f, metal);
                        break;
                    case 1:
                        // A band rounded on its outside, as most are.
                        Band(mesh, radius, Section.Super(8, 2.6f), thick, wide * 0.55f, 0.25f, metal);
                        break;
                    default:
                        // Flat, its edges taken off.
                        Band(mesh, radius, Section.Slab(0.35f), thick * 0.8f, wide * 0.6f, 0.1f, metal);
                        break;
                }
            });

            // What sits on it, at its top along +x: the better a ring the likelier something does, and a legendary one always holds what glows.
            Vector3 top = new Vector3(radius + thick * (signet ? 2.2f : 1f), 0f, 0f);
            bool lit = look.Legendary || look.Theme != GearTheme.None && look.Shines("lit");
            int crown = signet ? 1 : genes.Pick("crown", look.Legendary ? 0f : look.Spec.Grade == 0 ? 3f : 0.6f, 2f + look.Craft, 1f + look.Craft * 2f, look.Motif == Motif.Fang ? 2.5f : 0.6f, look.Legendary ? 3f : 0f, 0.6f + look.Craft, 0.5f + look.Craft);
            mesh.Mark($"band {band}");
            mesh.Mark($"crown {crown}");
            mesh.Counted("crown", () =>
            {
                Matrix4 place = Matrix4.CreateRotationZ(-MathF.PI / 2f) * Matrix4.CreateTranslation(top);
                switch (crown)
                {
                    case 1:
                        // A signet's face, round, oval or cut, with a stone set in it or something raised on it: a lozenge, a ring, three roundels or a beast's fang.
                        mesh.At(place, () =>
                        {
                            float face = wide * genes.Range("face", 0.55f, 0.75f);
                            int shape = genes.Pick("face.shape", 2f, 1.5f, 1f + look.Angular * 2f);
                            float squash = shape == 1 ? genes.Range("face.oval", 1.25f, 1.5f) : 1f;
                            mesh.Lathe(shape == 2 ? Section.Polygon(genes.Count("face.sides", 6, 8)) : Section.Round(16), thick * 0.5f, 1, _ => face, metal, capStart: false, squash: squash);
                            int emblem = look.Legendary ? 0 : genes.Pick("emblem", 0.35f + look.Craft * 0.3f, 1f, 0.8f, 0.6f, look.Motif == Motif.Fang ? 1.5f : 0.5f);
                            mesh.Mark($"signet {shape} {emblem}");
                            VertexMaterial raised = look.Legendary ? c.Precious : metal;
                            Matrix4 on = Matrix4.CreateTranslation(0f, thick * 0.5f, 0f);
                            switch (emblem)
                            {
                                case 0:
                                    mesh.At(Matrix4.CreateTranslation(0f, thick * 0.3f, 0f), () => GearKit.Setting(mesh, genes.Part("stone"), face * 0.5f, metal, lit ? c.Glow : c.Gem));
                                    break;
                                case 1:
                                    mesh.At(on, () => mesh.Lathe(Section.Polygon(4), thick * 0.25f, 1, t => face * 0.55f * (1f - t * 0.3f), raised, capStart: false, squash: squash));
                                    break;
                                case 2:
                                    mesh.At(on, () => mesh.Hoop(Section.Round(5), face * 0.5f, face * 0.08f, face * 0.08f, 16, raised));
                                    break;
                                case 3:
                                    foreach ((float x, float z) in new[] { (-face * 0.3f, face * 0.22f), (face * 0.3f, face * 0.22f), (0f, -face * 0.28f) })
                                    {
                                        mesh.At(Matrix4.CreateTranslation(x, thick * 0.5f, z), () => mesh.Lathe(Section.Round(8), thick * 0.2f, 1, t => face * 0.17f * (1f - t * 0.4f), raised, capStart: false));
                                    }
                                    break;
                                default:
                                    mesh.At(Matrix4.CreateRotationX(-MathF.PI / 2f) * Matrix4.CreateTranslation(0f, thick * 0.55f, face * 0.62f), () => GearKit.Fang(mesh, face * 1.2f, face * 0.16f, genes.Sign("hook"), c.Bone));
                                    break;
                            }
                        });
                        break;
                    case 2:
                        // A stone in a bezel or claws.
                        mesh.At(place, () => GearKit.Setting(mesh, genes.Part("stone"), FingerRadius * genes.Range("stone", 0.32f, 0.6f) * (look.Legendary ? 1.2f : 1f), metal, lit ? c.Glow : c.Gem));
                        break;
                    case 3:
                        // A cluster: a stone and smaller ones round it.
                        mesh.At(place, () =>
                        {
                            float stone = FingerRadius * genes.Range("cluster", 0.26f, 0.36f);
                            GearKit.Setting(mesh, genes.Part("centre"), stone, metal, lit ? c.Glow : c.Gem);
                            int round = genes.Count("round", 5, 8);
                            for (int i = 0; i < round; i++)
                            {
                                float a = i / (float)round * MathF.Tau;
                                mesh.At(Matrix4.CreateTranslation(MathF.Cos(a) * stone * 1.6f, stone * 0.25f, MathF.Sin(a) * stone * 1.6f), () => mesh.Gem(6, stone * 0.34f, stone * 0.25f, stone * 0.15f, 0.5f, i % 2 == 0 ? c.Gem : look.Legendary ? c.Glow : c.Gem));
                            }
                        });
                        break;
                    case 4:
                        // A beast's fang, set in a cap on the band, point up.
                        mesh.At(place, () =>
                        {
                            mesh.Lathe(Section.Round(10), thick * 0.8f, 2, t => FingerRadius * 0.24f * (1f - t * 0.3f), metal, capStart: false, capEnd: false);
                            mesh.At(Matrix4.CreateTranslation(0f, thick * 0.6f, 0f), () => GearKit.Fang(mesh, FingerRadius * genes.Range("fang", 0.8f, 1.3f), FingerRadius * 0.2f, genes.Sign("hook"), c.Bone));
                        });
                        break;
                    case 6:
                    case 7:
                    {
                        // Two stones side by side along the band, or three in a row, the middle one largest.
                        int stones = crown == 6 ? 2 : 3;
                        float stone = FingerRadius * genes.Range("row", 0.2f, 0.3f);
                        for (int i = 0; i < stones; i++)
                        {
                            float along = (i - (stones - 1) * 0.5f) * stone * 2.1f;
                            float size = stones == 3 && i == 1 ? stone * 1.2f : stone;
                            int which = i;
                            mesh.At(Matrix4.CreateTranslation(0f, 0f, along) * place, () => GearKit.Setting(mesh, genes.Part($"stone{which}"), size, metal, lit && which == stones / 2 ? c.Glow : c.Gem));
                        }
                        break;
                    }
                    case 5:
                        // A crown of points round a glowing stone, as only a ring of legend has.
                        mesh.At(place, () =>
                        {
                            float stone = FingerRadius * 0.45f;
                            GearKit.Setting(mesh, genes.Part("stone"), stone, metal, c.Glow);
                            int points = genes.Count("points", 5, 8);
                            for (int i = 0; i < points; i++)
                            {
                                float a = i / (float)points * MathF.Tau;
                                mesh.At(Matrix4.CreateTranslation(MathF.Cos(a) * stone * 1.25f, 0f, MathF.Sin(a) * stone * 1.25f), () => mesh.Lathe(Section.Polygon(4), stone * 0.9f, 1, t => stone * 0.16f * (1f - t), metal, capEnd: false));
                            }
                        });
                        break;
                }
            });

            // Grooves cut round a plain band, either side of its middle.
            if (!look.Legendary && band is 0 or 1 && genes.Chance("grooves", 0.5f))
            {
                mesh.Mark(genes.Chance("groove.one", 0.4f) ? "ridge" : "grooves");
                mesh.Counted("grooves", () =>
                {
                    foreach (float edgeSide in new[] { -1f, 1f })
                    {
                        float y = edgeSide * wide * genes.Range("groove.at", 0.2f, 0.4f);
                        mesh.At(Matrix4.CreateTranslation(0f, y, 0f), () => mesh.Hoop(Section.Round(4), radius + thick * 0.62f, thick * 0.14f, thick * 0.1f, 32, c.FittingDark));
                    }
                });
            }

            // A legendary ring has a wire of the other precious metal twisted along each edge of its band.
            if (look.Legendary && band is 0 or 1 or 4)
            {
                mesh.Counted("wire", () =>
                {
                    VertexMaterial other = GearColours.Of(GearMaterials.Metal, GearSwatches.Find(GearSwatches.Metals, c.FittingName is "gold" or "rose gold" ? "silver" : "gold").Albedo);
                    foreach (float edgeSide in new[] { -1f, 1f })
                    {
                        float y = edgeSide * wide * 0.55f;
                        mesh.At(Matrix4.CreateTranslation(0f, y, 0f), () => GearKit.Twist(mesh, radius + thick * 0.55f, thick * 0.22f, 14, other));
                    }
                });
            }

            // A power's runes round the band, lit.
            if (lit && band is 0 or 1)
            {
                mesh.Counted("runes", () =>
                {
                    int runes = genes.Count("runes", 6, 10);
                    for (int i = 0; i < runes; i++)
                    {
                        float a = MathF.PI * (0.35f + 1.3f * i / (runes - 1f));
                        Vector3 out_ = new Vector3(MathF.Cos(a), 0f, MathF.Sin(a));
                        Vector3 at = out_ * (radius + thick * 0.95f);
                        mesh.Strip(new[] { at - Vector3.UnitY * wide * 0.3f, at + Vector3.UnitY * wide * 0.3f }, new[] { out_, out_ }, thick * 0.18f, thick * 0.12f, c.Glow);
                    }
                });
            }
        }

        // A band round y, swelling towards its top along +x by <swell> of itself.
        private static void Band(GearMesh mesh, float radius, Section section, float thick, float wide, float swell, VertexMaterial metal)
        {
            int steps = 40;
            var rings = new Ring[steps + 1];
            for (int i = 0; i <= steps; i++)
            {
                float a = (float)i / steps * MathF.Tau;
                float grow = 1f + swell * MathF.Pow(MathF.Max(MathF.Cos(a), 0f), 3f);
                Vector3 out_ = new Vector3(MathF.Cos(a), 0f, MathF.Sin(a));
                rings[i] = new Ring { Centre = out_ * (radius + thick * (grow - 1f) * 0.5f), Side = out_, Front = Vector3.UnitY, HalfWidth = thick * grow * 0.5f + thick * 0.5f, HalfDepth = wide * (1f + swell * 0.4f * (grow - 1f)) };
            }
            mesh.Loft(section, rings, metal, capStart: false, capEnd: false);
        }

        // Jewellery is worked in what's dear: silver or gold on the better pieces, bronze or copper or steel on the plain, gilt on a legendary one.
        private static VertexMaterial Metal(GearLook look) => look.Legendary || look.Craft > 0.45f ? look.Colours.Precious : look.Colours.Fitting;

        /// <summary>An amulet hanging from its chain, its front towards +z, the chain rising round a neck above it.</summary>
        /// <param name="strands">Where its chain goes when it's worn, from where the pendant hangs, laid over whatever's there (Outfit.Necklace); nothing for the loop it's shown on alone.</param>
        /// <summary>How far off what it lies on a chain's or a thong's middle is, at most: half a link's width, and its wire.</summary>
        public const float StrandLies = 0.042f * 1.25f * 0.5f + 0.042f * 1.25f * 0.22f;

        // A thong of leather, or links each a small oval turned a quarter from the one before, along a spine. Up is what starts the thong's frame, square to where it first goes.
        private static void Strand(GearMesh mesh, IReadOnlyList<Vector3> spine, bool cord, float link, GearColours c, VertexMaterial metal, Vector3 up)
        {
            if (cord)
            {
                mesh.Sweep(Section.Round(5), spine, _ => new Vector2(0.011f), c.Strap, up, false, false);
                return;
            }
            float wire = link * 0.22f;
            float run = 0f, next = 0f;
            int index = 0;
            for (int i = 1; i < spine.Count; i++)
            {
                float step = (spine[i] - spine[i - 1]).Length;
                while (next <= run + step && index < 60)
                {
                    float t = step > 1e-6f ? (next - run) / step : 0f;
                    Vector3 at = Vector3.Lerp(spine[i - 1], spine[i], t);
                    Vector3 along = (spine[i] - spine[i - 1]).Normalized();
                    Vector3 across = Vector3.Cross(along, index % 2 == 0 ? Vector3.UnitZ : Vector3.UnitX);
                    if (across.LengthSquared < 1e-6f) across = Vector3.Cross(along, Vector3.UnitY);
                    across.Normalize();
                    Vector3 face = Vector3.Cross(along, across);
                    mesh.At(GearMesh.Basis(at, across, face, along), () => mesh.Hoop(Section.Round(4), link * 0.5f, wire, wire, 8, metal));
                    next += link * 0.82f;
                    index++;
                }
                run += step;
            }
        }

        /// <param name="strands">Where its chain goes when it's worn, from where the pendant hangs, laid over whatever's there (Outfit.Necklace); nothing for the loop it's shown on alone.</param>
        public static void Amulet(GearMesh mesh, GearLook look, IReadOnlyList<IReadOnlyList<Vector3>>? strands = null)
        {
            Genes genes = look.Genes;
            GearForm form = look.Spec.Form;
            GearColours c = look.Colours;
            VertexMaterial metal = Metal(look);

            float drop = genes.Range("drop", 0.34f, 0.6f);
            float neckWide = genes.Range("neck.wide", 0.26f, 0.34f);
            // A thong for the hide-wearers and the poor, a chain for everyone else and always for a legendary piece.
            bool cord = !look.Legendary && (look.Leather > 0.5f && genes.Chance("cord.hide", 0.7f) || genes.Chance("cord", 0.15f + (1f - look.Craft) * 0.3f));
            var path = new List<Vector3>();
            for (int i = 0; i <= 16; i++)
            {
                float angle = i / 16f * MathF.Tau;
                float front = 0.5f + 0.5f * MathF.Cos(angle);
                path.Add(new Vector3(MathF.Sin(angle) * neckWide, drop * (1f - MathF.Pow(front, 2.4f)), MathF.Cos(angle) * neckWide * 0.72f - neckWide * 0.72f));
            }
            float link = genes.Range("link", 0.028f, 0.042f) * (0.85f + 0.4f * look.Heft);

            mesh.Counted("chain", () =>
            {
                if (strands == null) Strand(mesh, Spine.Through(path, 64), cord, link, c, metal, Vector3.UnitY);
                else foreach (IReadOnlyList<Vector3> strand in strands) Strand(mesh, strand, cord, link, c, metal, Vector3.UnitZ);
            });

            float size = genes.Around("size", 0.08f + form.Size * 0.08f, 0.015f) * (0.9f + 0.4f * look.Heft) * (look.Legendary ? 1.2f : 1f);
            Vector3 hang = new Vector3(0f, -size * 0.2f, 0f);
            bool lit = look.Legendary || look.Theme != GearTheme.None && look.Shines("lit");
            int kind = genes.Pick("kind", 2f + look.Craft, 2f + look.Craft * 1.5f, (look.Motif == Motif.Fang ? 2.5f : 0.6f) + look.Leather * 2f, 0.8f + look.Leather);

            // The bail it hangs from.
            mesh.Counted("bail", () => mesh.At(Matrix4.CreateRotationZ(MathF.PI / 2f) * Matrix4.CreateTranslation(0f, -size * 0.05f, 0f), () => mesh.Hoop(Section.Round(5), size * 0.22f, size * 0.05f, size * 0.05f, 10, cord ? c.Strap : metal)));

            mesh.Counted("pendant", () =>
            {
                switch (kind)
                {
                    case 0:
                    {
                        // A medallion: a disc with a rim and something raised on it.
                        Vector3 centre = hang - Vector3.UnitY * size;
                        mesh.At(Matrix4.CreateRotationX(MathF.PI / 2f) * Matrix4.CreateTranslation(centre), () =>
                        {
                            Section disc = look.Angular > 0.5f ? Section.Polygon(genes.Count("sides", 6, 8), MathF.PI / 2f) : Section.Round(20);
                            mesh.Lathe(disc, size * 0.14f, 2, t => size * (1f - 0.08f * t), metal);
                            mesh.At(Matrix4.CreateTranslation(0f, size * 0.14f, 0f), () => mesh.Hoop(Section.Round(5), size * 0.92f, size * 0.05f, size * 0.04f, 24, metal));
                            if (genes.Chance("stone", 0.45f + look.Craft * 0.3f) || look.Legendary)
                            {
                                mesh.At(Matrix4.CreateTranslation(0f, size * 0.12f, 0f), () => GearKit.Setting(mesh, genes.Part("stone"), size * 0.4f, metal, lit ? c.Glow : c.Gem));
                            }
                            else
                            {
                                // A fang raised on its face, as a Defender's token.
                                mesh.At(Matrix4.CreateRotationX(-MathF.PI / 2f) * Matrix4.CreateTranslation(0f, size * 0.16f, size * 0.6f), () => GearKit.Fang(mesh, size * 1.1f, size * 0.16f, genes.Sign("hook"), look.Legendary ? c.Precious : c.Bone));
                            }
                        });
                        break;
                    }
                    case 1:
                    {
                        // A stone, long and cut, in a frame of claws, hanging point down.
                        Vector3 centre = hang - Vector3.UnitY * size * 1.1f;
                        mesh.At(Matrix4.CreateRotationX(MathF.PI) * Matrix4.CreateTranslation(centre + Vector3.UnitY * size * 0.6f), () => mesh.Lathe(Section.Round(8), size * 0.2f, 1, t => size * 0.22f * (1f - t * 0.3f), metal, capStart: false));
                        mesh.At(Matrix4.CreateTranslation(centre), () => mesh.Gem(genes.Count("facets", 6, 10), size * 0.42f, size * 0.55f, size * 0.95f, 0.45f, lit ? c.Glow : c.Gem));
                        break;
                    }
                    case 2:
                    {
                        // A beast's fang bound in wire, point down, as those who bring them down wear them.
                        Vector3 root = hang - Vector3.UnitY * size * 0.15f;
                        mesh.At(Matrix4.CreateRotationZ(MathF.PI) * Matrix4.CreateTranslation(root), () =>
                        {
                            mesh.Lathe(Section.Round(8), size * 0.3f, 2, t => size * 0.2f * (1f + 0.15f * MathF.Sin(t * MathF.PI)), cord ? c.Strap : metal);
                            mesh.At(Matrix4.CreateTranslation(0f, size * 0.25f, 0f), () => GearKit.Fang(mesh, size * genes.Range("fang", 1.6f, 2.4f), size * 0.24f, genes.Sign("hook"), c.Bone));
                        });
                        break;
                    }
                    default:
                    {
                        // A charm of carved bone or wood: a pierced disc with a groove round it.
                        Vector3 centre = hang - Vector3.UnitY * size * 0.9f;
                        VertexMaterial carved = genes.Chance("bone", 0.6f) ? c.Bone : c.Wood;
                        mesh.At(Matrix4.CreateRotationX(MathF.PI / 2f) * Matrix4.CreateTranslation(centre), () =>
                        {
                            // Flat, as a pierced disc is, with a groove cut round its face.
                            mesh.Hoop(Section.Slab(0.45f), size * 0.62f, size * 0.36f, size * 0.07f, 24, carved);
                            mesh.At(Matrix4.CreateTranslation(0f, size * 0.072f, 0f), () => mesh.Hoop(Section.Round(4), size * 0.62f, size * 0.02f, size * 0.012f, 24, look.Legendary ? c.Precious : c.WoodDark));
                        });
                        break;
                    }
                }
            });

            if (look.Legendary)
            {
                // Filigree scrolls either side of what hangs, and a halo of its power behind it.
                mesh.Counted("filigree", () =>
                {
                    Vector3 centre = hang - Vector3.UnitY * size;
                    foreach (float side in new[] { -1f, 1f })
                    {
                        GearKit.Scroll(mesh, centre + new Vector3(side * size * 0.95f, size * 0.3f, 0.01f), new Vector3(side, 0f, 0f), Vector3.UnitY, size * 0.35f, 1.6f, size * 0.05f, c.Precious);
                        GearKit.Scroll(mesh, centre + new Vector3(side * size * 0.95f, -size * 0.3f, 0.01f), new Vector3(side, 0f, 0f), -Vector3.UnitY, size * 0.35f, 1.6f, size * 0.05f, c.Precious);
                    }
                    mesh.At(Matrix4.CreateRotationX(MathF.PI / 2f) * Matrix4.CreateTranslation(centre - Vector3.UnitZ * size * 0.15f), () => mesh.Hoop(Section.Round(5), size * 1.45f, size * 0.03f, size * 0.03f, 28, c.Glow));
                });
            }
        }
    }
}
