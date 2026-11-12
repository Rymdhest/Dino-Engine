using Dino_Engine.ECS.Components;
using Dino_Engine.Modelling.Model;
using OpenTK.Mathematics;

namespace Dino_Engine.Modelling.Procedural.Gear
{
    /// <summary>
    /// What's worn on the head, all on the head's bone and built round where the head is in <see cref="BodyForm"/>. A helm is one space of them, not a list: its skull round, pointed or combed;
    /// a brim or a tail or neither; open, with a nasal, with cheek plates, or a whole face of steel; a plume, a comb, horns or wings. Mail makes a coif, hardened leather a cap of panels, hide or
    /// cloth a hood open at the face, and fine cloth a tall hat or a circlet. Nothing wider than a neck comes below the jaw (<see cref="GearFit.HelmLow"/>), where a body's collar is, and anything
    /// of the head's that goes down the neck tucks inside it (<see cref="GearFit.NeckTuck"/>). Nothing ends in a spike on top, which reads as the old German soldiers' helm. See GEAR.md.
    /// </summary>
    public static class HeadGenerator
    {
        private const float Top = BodyForm.HeadTop;
        private const float Centre = BodyForm.HeadCentre;
        private const float HalfWidth = BodyForm.HeadHalfWidth;
        private const float HalfDepth = BodyForm.HeadHalfDepth;
        private const float Front = MathF.PI / 2f;

        public static void Build(GearMesh mesh, GearLook look)
        {
            var kit = new ArmourKit(look);
            Genes genes = look.Genes;
            GearForm form = look.Spec.Form;
            mesh.Bone = (int)BoneType.Head;

            switch (kit.Make)
            {
                case GearMake.Mail:
                    // High up the climb a coif is worn under a helm, its mail hanging from the helm's rim as an aventail, and the helm can carry a crest.
                    if (genes.Chance("helm.over", Math.Clamp((look.Power - 0.5f) * 2.4f, 0f, 0.9f))) mesh.Counted("helm", () => Helm(mesh, new ArmourKit(look, GearMake.Plate), genes.Part("helm"), coif: true));
                    else mesh.Counted("coif", () => Coif(mesh, kit, genes.Part("coif")));
                    break;
                case GearMake.Hardened:
                case GearMake.Studded:
                    mesh.Counted("cap", () => Cap(mesh, kit, genes.Part("cap")));
                    break;
                case GearMake.Hide:
                    mesh.Counted("hood", () => Hood(mesh, kit, genes.Part("hood")));
                    break;
                case GearMake.Cloth:
                case GearMake.Silk:
                case GearMake.Wraps:
                    if (form.Cover < 0.12f || kit.Make == GearMake.Silk && form.Cover < 0.3f && genes.Chance("circlet", 0.6f))
                    {
                        mesh.Counted("circlet", () => Circlet(mesh, kit, genes.Part("circlet")));
                        return;
                    }
                    if (form.Size > 0.4f && genes.Chance("hat", 0.3f + form.Size * 0.35f)) mesh.Counted("kasa", () => Kasa(mesh, kit, genes.Part("hat")));
                    else mesh.Counted("hood", () => Hood(mesh, kit, genes.Part("hood")));
                    break;
                case GearMake.Padded:
                    mesh.Counted("cap", () => Cap(mesh, kit, genes.Part("cap")));
                    break;
                default:
                    mesh.Counted("helm", () => Helm(mesh, kit, genes.Part("helm")));
                    break;
            }
            mesh.Covers |= BodyRegions.Hair;
        }

        private static float Smooth(float t)
        {
            t = Math.Clamp(t, 0f, 1f);
            return t * t * (3f - 2f * t);
        }

        // Below a shoulder's plate's top, nothing of the head's comes further across than GearFit.HeadSide, where a shoulder's plate starts: a plate laid there is drawn in at its sides, by
        // as much more as it stands out from where it's laid (its thickness and its rolled edge).
        private static Vector3 Beside(Vector3 p, float stands)
        {
            float most = GearFit.HeadSide - stands;
            return p.Y < GearFit.ShoulderTop ? new Vector3(Math.Clamp(p.X, -most, most), p.Y, p.Z) : p;
        }

        // The head's surface pushed out by gap, at a height and an angle round it, which every helm, coif and cap is laid over.
        private static Vector3 OnHead(float y, float angle, float gap, float lean = 0f)
        {
            float s = Math.Clamp((y - Centre) / BodyForm.HeadHalfHeight, -1f, 1f);
            float round = MathF.Sqrt(MathF.Max(1f - s * s, 0.0004f));
            return new Vector3(MathF.Cos(angle) * (HalfWidth + gap) * round, y, MathF.Sin(angle) * (HalfDepth + gap) * round - 0.015f + lean);
        }

        // A dome over the head from a height up, its crown shaped by a profile and run up to a point or flattened, as a loft round y.
        private static List<Ring> Dome(Genes genes, float from, float gap, float peak, float flat, int steps = 10)
        {
            float top = Top + gap + peak + genes.Range("crown.tall", -0.03f, 0.12f);
            Profile crown = new Profile((0f, 1f), (0.4f, genes.Range("crown.mid", 0.9f, 1.08f)), (0.75f, genes.Range("crown.high", 0.7f, 1f)), (1f, genes.Range("crown.top", 0.04f, 0.45f)));
            float power = 2.2f - MathF.Min(peak / 0.25f, 1f);
            var rings = new List<Ring>();
            for (int i = 0; i <= steps; i++)
            {
                float t = (float)i / steps;
                float y = from + (top - from) * MathF.Sin(t * MathF.PI * 0.5f);
                float s = (y - from) / (top - from);
                float along = MathF.Sqrt(MathF.Max(1f - MathF.Pow(s, power), 0f));
                along = MathF.Max((along * (1f - flat) + crown.At(s) * flat) * (0.9f + 0.1f * crown.At(s)), 0.02f);
                float headRound = MathF.Sqrt(MathF.Max(1f - MathF.Pow(Math.Clamp((MathF.Min(y, Centre) - Centre) / BodyForm.HeadHalfHeight, -1f, 0f), 2f), 0.3f));
                rings.Add(new Ring { Centre = new Vector3(0f, y, -0.015f), Side = Vector3.UnitX, Front = Vector3.UnitZ, HalfWidth = (HalfWidth + gap) * along * headRound, HalfDepth = (HalfDepth + gap) * along * headRound });
            }
            return rings;
        }

        /// <param name="coif">It's worn over a coif, whose mail hangs from its rim.</param>
        private static void Helm(GearMesh mesh, ArmourKit kit, Genes genes, bool coif = false)
        {
            GearLook look = kit.Look;
            GearForm form = look.Spec.Form;
            float gap = genes.Range("gap", 0.06f, 0.1f);
            float cover = form.Cover;
            float peak = genes.Range("peak", 0f, 0.26f) * (0.3f + look.Spikes);
            bool great = cover > 0.82f && genes.Chance("great", 0.85f);
            float brow = great ? GearFit.ShoulderTop + 0.035f : Centre + genes.Range("brow", -0.02f, 0.1f);
            Section section = look.Angular > 0.55f ? Section.Polygon(genes.Count("facets", 8, 12)) : Section.Round(18);

            if (great)
            {
                // A drum round the whole head, its top flat or a low dome, with a slit to see through and holes to breathe through.
                float slit = genes.Range("slit", 4.46f, 4.56f);
                float r = HalfWidth + gap + 0.04f, d = HalfDepth + gap + 0.04f;
                var drum = new List<Ring>();
                foreach (float y in new[] { brow, slit - 0.06f, slit - 0.025f, slit + 0.025f, slit + 0.06f, Top - 0.05f })
                {
                    drum.Add(new Ring { Centre = new Vector3(0f, y, 0.01f), Side = Vector3.UnitX, Front = Vector3.UnitZ, HalfWidth = r, HalfDepth = d, Hard = y == brow });
                }
                mesh.Bands(section, drum, (ring, _) => ring == 2 ? kit.C.MetalDark : kit.Shell, capStart: false, capEnd: false);
                var crown = Dome(genes, Top - 0.05f, gap + 0.04f, peak * 0.4f, genes.Range("flat", 0.4f, 1f), 6);
                mesh.Loft(section, crown, kit.Shell, capStart: false, capEnd: true);
                // A cross of iron over the face.
                mesh.Strip(new[] { new Vector3(0f, brow + 0.04f, d + 0.012f), new Vector3(0f, slit - 0.03f, d + 0.012f) }, new[] { Vector3.UnitZ, Vector3.UnitZ }, 0.03f, 0.012f, kit.Edge);
                int holes = genes.Count("holes", 3, 8);
                for (int i = 0; i < holes; i++)
                {
                    float y = brow + 0.07f + (i % 4) * 0.04f;
                    float x = (genes.Chance($"hole{i}.side", 0.5f) ? 1f : -1f) * (0.1f + (i / 4) * 0.05f);
                    mesh.Rivet(new Vector3(x, y, d * MathF.Sqrt(MathF.Max(1f - x * x / (r * r), 0.1f)) + 0.003f), Vector3.UnitZ, 0.014f, kit.C.MetalDark);
                }
            }
            else
            {
                List<Ring> dome = Dome(genes, brow, gap, peak, genes.Range("flat", 0f, 0.45f));
                bool banded = genes.Chance("banded", 0.35f + (look.Motif == Motif.Facet ? 0.2f : 0f));
                int segments = genes.Count("segments", 4, 6);
                mesh.Loft(section, dome, kit.Shell, capStart: false, capEnd: true);
                if (banded)
                {
                    mesh.Mark("bands");
                    // Bands up the dome from the brow to the crown, riveted, as a spangenhelm is made.
                    for (int i = 0; i < segments; i++)
                    {
                        float angle = Front + i * MathF.Tau / segments;
                        var path = new List<Vector3>();
                        var normals = new List<Vector3>();
                        foreach (Ring ring in dome)
                        {
                            Vector3 out_ = new Vector3(MathF.Cos(angle), 0f, MathF.Sin(angle));
                            path.Add(ring.Centre + new Vector3(out_.X * ring.HalfWidth, 0f, out_.Z * ring.HalfDepth));
                            normals.Add((out_ + Vector3.UnitY * ((ring.Centre.Y - brow) / (dome[^1].Centre.Y - brow + 0.01f))).Normalized());
                        }
                        mesh.Strip(path.Take(path.Count - 1).ToList(), normals.Take(normals.Count - 1).ToList(), 0.028f, 0.01f, kit.Fitting);
                        GearKit.Rivets(mesh, path.Take(path.Count - 2).ToList(), normals.Take(normals.Count - 2).ToList(), 0.09f, 0.012f, kit.Fitting);
                    }
                }
                // A comb from front to back over the crown.
                if (genes.Chance("comb", 0.2f + look.Angular * 0.25f))
                {
                    float tall = genes.Range("comb.tall", 0.05f, 0.14f);
                    var fin = new Ring[9];
                    float length = HalfDepth + gap;
                    float crownY = dome[^1].Centre.Y;
                    for (int i = 0; i <= 8; i++)
                    {
                        float t = i / 8f;
                        float s = t * 2f - 1f;
                        float y = brow + (crownY - brow) * MathF.Sqrt(MathF.Max(1f - s * s, 0f));
                        fin[i] = new Ring { Centre = new Vector3(0f, y + tall * 0.4f * (1f - s * s), s * length * 0.92f - 0.015f), Side = Vector3.UnitY, Front = Vector3.UnitX, HalfWidth = tall * (0.35f + 0.65f * (1f - s * s)), HalfDepth = 0.014f };
                    }
                    mesh.Loft(Section.Slab(0.3f), fin, kit.Shell);
                }
            }

            // A band round the brow, riveted.
            float browRadius = HalfWidth + gap + (great ? 0.045f : 0.006f);
            BrowBand(mesh, kit, genes, brow + (great ? 0f : 0.01f), browRadius, HalfDepth + gap + (great ? 0.045f : 0.006f), great ? 0.025f : 0.03f);

            if (!great)
            {
                float face = cover;
                if (face > 0.62f && genes.Chance("faceplate", 0.6f)) mesh.Counted("faceplate", () => Faceplate(mesh, kit, genes.Part("faceplate"), brow, gap));
                else if (kit.Likely("cheeks", 0.5f, 0.2f, 0f) && face > 0.3f) mesh.Counted("cheeks", () => Cheeks(mesh, kit, genes.Part("cheeks"), brow, gap));
                if (face > 0.25f && face < 0.7f && genes.Chance("nasal", 0.5f)) mesh.Counted("nasal", () => Nasal(mesh, kit, genes.Part("nasal"), brow, gap));
                if (genes.Chance("brim", 0.25f + look.Flare * 0.3f)) mesh.Counted("brim", () => Brim(mesh, kit, genes.Part("brim"), MathF.Max(brow, GearFit.BrimLow), gap));
                else if (genes.Chance("tail", 0.3f)) mesh.Counted("tail", () => Tail(mesh, kit, genes.Part("tail"), brow, gap));
            }
            if (genes.Chance("aventail", (look.Heavy * 0.55f + look.Leather * 0.1f) * (0.4f + look.Power * 0.8f)) || great || coif) mesh.Counted("aventail", () => Aventail(mesh, kit, genes.Part("aventail"), MathF.Min(brow, GearFit.HelmLow + 0.06f), gap));
            mesh.Counted("crest", () => Crest(mesh, kit, genes.Part("crest"), brow, gap, peak, great));
            if (look.Legendary || look.Shines("brow.stone")) BrowStone(mesh, kit, genes.Part("stone"), brow + 0.04f, HalfDepth + gap + 0.03f);
        }

        private static void BrowBand(GearMesh mesh, ArmourKit kit, Genes genes, float y, float radius, float depth, float height, VertexMaterial? material = null)
        {
            Ring[] rings =
            {
                new Ring { Centre = new Vector3(0f, y - height, -0.015f), Side = Vector3.UnitX, Front = Vector3.UnitZ, HalfWidth = radius, HalfDepth = depth, Hard = true },
                new Ring { Centre = new Vector3(0f, y - height * 0.6f, -0.015f), Side = Vector3.UnitX, Front = Vector3.UnitZ, HalfWidth = radius + 0.012f, HalfDepth = depth + 0.012f },
                new Ring { Centre = new Vector3(0f, y + height * 0.6f, -0.015f), Side = Vector3.UnitX, Front = Vector3.UnitZ, HalfWidth = radius + 0.012f, HalfDepth = depth + 0.012f },
                new Ring { Centre = new Vector3(0f, y + height, -0.015f), Side = Vector3.UnitX, Front = Vector3.UnitZ, HalfWidth = radius, HalfDepth = depth, Hard = true },
            };
            mesh.Loft(Section.Round(20), rings, material ?? kit.Edge, capStart: false, capEnd: false);
            int rivets = genes.Count("band.rivets", 0, 10);
            for (int i = 0; i < rivets; i++)
            {
                float angle = i * MathF.Tau / MathF.Max(rivets, 1);
                Vector3 out_ = new Vector3(MathF.Cos(angle), 0f, MathF.Sin(angle));
                mesh.Rivet(new Vector3(out_.X * (radius + 0.012f), y, out_.Z * (depth + 0.012f) - 0.015f), out_, 0.012f, kit.Fitting);
            }
        }

        // A bar down over the nose.
        private static void Nasal(GearMesh mesh, ArmourKit kit, Genes genes, float brow, float gap)
        {
            float length = MathF.Min(genes.Range("length", 0.18f, 0.32f), brow - GearFit.HelmLow - 0.02f);
            float wide = genes.Range("wide", 0.03f, 0.055f);
            float z = HalfDepth + gap + 0.01f;
            var path = new[] { new Vector3(0f, brow + 0.04f, z - 0.015f), new Vector3(0f, brow - length * 0.5f, z + 0.015f), new Vector3(0f, brow - length, z + 0.005f) };
            mesh.Sweep(Section.Slab(0.3f), Spine.Through(path, 5), t => new Vector2(wide * (1f - t * 0.35f), 0.012f), kit.Shell, Vector3.UnitZ);
        }

        // Plates hinged at the brow hanging down beside the face, no lower than the jaw.
        private static void Cheeks(GearMesh mesh, ArmourKit kit, Genes genes, float brow, float gap)
        {
            float low = GearFit.HelmLow + 0.03f;
            float wide = genes.Range("wide", 0.4f, 0.65f);
            foreach (float side in new[] { 1f, -1f })
            {
                float centre = side > 0f ? genes.Range("at", 0.55f, 0.85f) : MathF.PI - genes.Range("at", 0.55f, 0.85f);
                mesh.Plate((u, v) =>
                {
                    float angle = centre + (u - 0.5f) * wide;
                    float y = low + (brow - low) * v;
                    Vector3 p = OnHead(MathF.Max(y, Centre - 0.15f), angle, gap);
                    return (Beside(new Vector3(p.X, y, p.Z), 0.023f), new Vector3(MathF.Cos(angle), 0f, MathF.Sin(angle)));
                }, 5, 5, 0.016f, (_, _) => kit.Shell, PlateEdge.All, 0.012f, underside: kit.ShellDark, rollMaterial: kit.Gilt);
                Vector3 hinge = OnHead(brow, centre, gap + 0.012f);
                mesh.Rivet(hinge, new Vector3(MathF.Cos(centre), 0f, MathF.Sin(centre)), 0.018f, kit.Fitting);
            }
        }

        // A plate over the face from the brow down, with a slit to see through, as a barbute's or a sallet's is closed.
        private static void Faceplate(GearMesh mesh, ArmourKit kit, Genes genes, float brow, float gap)
        {
            float low = GearFit.HelmLow + 0.02f;
            float span = genes.Range("span", 1.6f, 2.4f);
            float slit = brow - genes.Range("slit", 0.06f, 0.1f);
            mesh.Plate((u, v) =>
            {
                float angle = Front + (u - 0.5f) * span;
                float y = low + (brow - low) * v;
                Vector3 p = OnHead(MathF.Max(y, Centre - 0.3f), angle, gap + 0.01f);
                float jut = 0.04f * MathF.Pow(MathF.Cos((u - 0.5f) * 3f), 2f) * (1f - v);
                return (Beside(new Vector3(p.X, y, p.Z + jut), 0.025f), new Vector3(MathF.Cos(angle), 0f, MathF.Sin(angle)));
            }, 10, 8, 0.018f, (u, v) => MathF.Abs(low + (brow - low) * v - slit) < 0.022f && MathF.Abs(u - 0.5f) < 0.32f ? kit.C.MetalDark : kit.Shell, PlateEdge.Bottom, 0.014f, underside: kit.ShellDark, rollMaterial: kit.Gilt);
        }

        // A brim all round, as a kettle hat's, drooping at its edge, always above a shoulder's plate.
        private static void Brim(GearMesh mesh, ArmourKit kit, Genes genes, float y, float gap)
        {
            float reach = genes.Range("reach", 0.12f, 0.32f);
            float droop = genes.Range("droop", 0f, 0.1f);
            y = MathF.Max(y, GearFit.BrimLow + droop + 0.012f);
            float inner = HalfWidth + gap - 0.01f;
            mesh.Plate((u, v) =>
            {
                float angle = u * MathF.Tau;
                float r = inner + reach * v;
                return (new Vector3(MathF.Cos(angle) * r, y - droop * v * v, MathF.Sin(angle) * r * (HalfDepth / HalfWidth) - 0.015f), new Vector3(0f, 1f, 0f));
            }, 24, 3, 0.014f, (_, _) => kit.Shell, PlateEdge.Top, 0.014f, wrap: true, underside: kit.ShellDark, rollMaterial: kit.Gilt);
        }

        // Lames sweeping back from the dome over the nape, as a sallet's tail does, above the collar.
        private static void Tail(GearMesh mesh, ArmourKit kit, Genes genes, float brow, float gap)
        {
            float reach = genes.Range("reach", 0.12f, 0.28f);
            float span = genes.Range("span", 1.2f, 1.8f);
            float y = MathF.Max(brow - 0.06f, GearFit.HelmLow + 0.04f);
            int lames = genes.Count("lames", 1, 3);
            for (int i = 0; i < lames; i++)
            {
                float out_ = gap + 0.01f * i;
                float y0 = y - 0.07f * i;
                mesh.Plate((u, v) =>
                {
                    float angle = MathF.PI * 1.5f + (u - 0.5f) * span;
                    Vector3 p = OnHead(Centre - 0.1f, angle, out_);
                    Vector3 back = new Vector3(MathF.Cos(angle), 0f, MathF.Sin(angle));
                    return (Beside(new Vector3(p.X, MathF.Max(y0 - 0.08f * v, GearFit.HelmLow + 0.005f), p.Z) + back * reach * v, 0.023f), (Vector3.UnitY * 0.6f + back).Normalized());
                }, 10, 2, 0.016f, (_, _) => kit.Shell, PlateEdge.Top, 0.012f, underside: kit.ShellDark, rollMaterial: kit.Gilt);
            }
        }

        // Mail hung from a helm's rim round the neck, tucked in below the jaw to fit inside a collar.
        private static void Aventail(GearMesh mesh, ArmourKit kit, Genes genes, float rim, float gap)
        {
            float low = MathF.Max(GearFit.NeckLow + genes.Range("low", 0.01f, 0.06f), GearFit.NeckLow);
            float tuck = GearFit.NeckTuck - genes.Range("tuck", 0.005f, 0.03f);
            var rings = new List<Ring>
            {
                Ring.Flat(rim + 0.02f, rim + 0.02f < GearFit.ShoulderTop ? MathF.Min(HalfWidth + gap - 0.02f, GearFit.HeadSide - 0.005f) : HalfWidth + gap - 0.02f, HalfDepth + gap - 0.03f),
                Ring.Flat(GearFit.HelmLow + 0.005f, tuck, tuck * 0.98f),
                Ring.Flat((GearFit.HelmLow + low) * 0.5f, tuck, tuck * 0.98f),
                Ring.Flat(low, tuck * 0.97f, tuck * 0.95f),
            };
            // The rim round the head's middle, which sits a little back, and the rest round the neck's.
            for (int i = 0; i < rings.Count; i++)
            {
                Ring r = rings[i];
                r.Centre = new Vector3(0f, r.Centre.Y, i == 0 ? -0.015f : 0f);
                rings[i] = r;
            }
            mesh.Loft(Section.Round(18), rings, kit.Make == GearMake.Plate || kit.Make == GearMake.Mail ? kit.C.Mail : kit.Under, capStart: false, capEnd: false);
            mesh.SkinBy(at => BodyForm.Throat(at.Y));
        }

        // What makes a helm a shape from above: a plume, a fin, horns or wings, and on a legendary one a crown.
        private static void Crest(GearMesh mesh, ArmourKit kit, Genes genes, float brow, float gap, float peak, bool great)
        {
            GearLook look = kit.Look;
            float top = Top + gap + peak;
            // Nothing on top starting out; high up, a plume, horns or wings are the rule, as a crest is what a helm is told apart by from above.
            float low = (1f - look.Power) * (1f - look.Power);
            int kind = genes.Pick("kind", 0.3f + 2.6f * low, (1f + look.Ornament * 1.5f) * (0.25f + look.Power), (look.Spikes * 1.5f + (look.Motif == Motif.Fang ? 1f : 0f)) * (0.4f + look.Power * 0.6f), look.Flare * 1.2f * (0.2f + look.Power), look.Legendary ? 3f : 0f);
            if (kind > 0) mesh.Mark($"crest {kind}");
            switch (kind)
            {
                case 1:
                {
                    // A plume, falling back.
                    float reach = genes.Range("plume.reach", 0.55f, 1.1f);
                    float back = genes.Range("plume.back", 0.5f, 1f);
                    var keys = new[] { new Vector3(0f, top - 0.03f, 0f), new Vector3(0f, top + reach * 0.35f, -back * 0.25f), new Vector3(0f, top + reach * 0.4f, -back * 0.7f), new Vector3(0f, top + reach * 0.02f, -back) };
                    float wide = genes.Range("plume.wide", 0.07f, 0.14f);
                    mesh.Sweep(Section.Super(10, 2.2f), Spine.Through(keys, 12), t => new Vector2(wide * (0.3f + 1.1f * MathF.Sin(t * MathF.PI)) * (1f - t * 0.4f), wide * 0.6f * (0.3f + MathF.Sin(t * MathF.PI))), genes.Chance("plume.trim", 0.5f) ? kit.C.Trim : kit.C.Cloth, Vector3.UnitX);
                    mesh.At(Matrix4.CreateTranslation(0f, top - 0.05f, 0f), () => mesh.Lathe(Section.Round(8), 0.07f, 2, t => 0.04f * (1f - t * 0.3f), kit.Fitting));
                    break;
                }
                case 2:
                {
                    // Horns.
                    float span = genes.Range("horn.span", 0.35f, 0.8f);
                    float rise = genes.Range("horn.rise", 0.2f, 0.75f);
                    float thick = genes.Range("horn.thick", 0.05f, 0.09f);
                    VertexMaterial horn = genes.Chance("horn.dark", 0.5f) ? kit.C.Horn : kit.C.Bone;
                    foreach (float side in new[] { 1f, -1f })
                    {
                        float x = side * (HalfWidth + gap - 0.03f);
                        var path = new[] { new Vector3(x, Centre + 0.16f, 0f), new Vector3(x + side * span * 0.55f, Centre + 0.2f, -0.05f), new Vector3(x + side * span, Centre + 0.2f + rise * 0.6f, -0.02f), new Vector3(x + side * span * 0.86f, Centre + 0.2f + rise, 0.06f) };
                        mesh.Sweep(Section.Round(8), Spine.Through(path, 10), t => new Vector2(thick * (1f - t * 0.92f)), horn, Vector3.UnitZ);
                    }
                    break;
                }
                case 3:
                {
                    // Wings, of steel blades rising from either side.
                    int blades = genes.Count("wing.blades", 2, 4);
                    float length = genes.Range("wing.length", 0.35f, 0.65f);
                    foreach (float side in new[] { 1f, -1f })
                    {
                        for (int i = 0; i < blades; i++)
                        {
                            float tilt = 0.5f + (float)i / blades * 0.7f;
                            float x = side * (HalfWidth + gap - 0.03f);
                            var wing = new[] { new Vector3(x, Centre + 0.2f, -0.06f * i), new Vector3(x + side * MathF.Cos(tilt) * length * 0.5f, Centre + 0.2f + MathF.Sin(tilt) * length * 0.5f, -0.06f * i), new Vector3(x + side * MathF.Cos(tilt) * length, Centre + 0.2f + MathF.Sin(tilt) * length, -0.06f * i - 0.04f) };
                            mesh.Sweep(Section.Slab(0.3f), Spine.Through(wing, 5), t => new Vector2(0.025f, 0.05f * (1f - t * 0.9f)), look.Legendary ? kit.C.Precious : kit.Fitting, Vector3.UnitZ);
                        }
                    }
                    break;
                }
                case 4:
                {
                    // A crown of points round the brow, gilded.
                    int points = genes.Count("crown.points", 5, 9);
                    float y = brow + 0.04f;
                    for (int i = 0; i < points; i++)
                    {
                        float angle = Front + (i - (points - 1) * 0.5f) * genes.Range("crown.gap", 0.28f, 0.42f);
                        Vector3 at = OnHead(y, angle, gap + 0.015f);
                        float tall = genes.Range("crown.tall", 0.08f, 0.16f) * (i == points / 2 ? 1.5f : 1f);
                        mesh.At(Matrix4.CreateTranslation(at), () => mesh.Lathe(Section.Polygon(4), tall, 2, t => 0.026f * (1f - t), kit.C.Precious, capEnd: false));
                    }
                    break;
                }
            }
        }

        private static void BrowStone(GearMesh mesh, ArmourKit kit, Genes genes, float y, float z)
        {
            float size = genes.Range("size", 0.035f, 0.06f) * (kit.Look.Legendary ? 1.3f : 1f);
            mesh.At(Matrix4.CreateRotationX(MathF.PI / 2f) * Matrix4.CreateTranslation(0f, y, z), () => GearKit.Setting(mesh, genes, size, kit.C.Precious, kit.Look.Theme != GearTheme.None ? kit.C.Glow : kit.C.Gem));
        }

        // A hood of mail over the head and round the neck, open at the face, its neck tucked in below the jaw.
        private static void Coif(GearMesh mesh, ArmourKit kit, Genes genes)
        {
            float gap = genes.Range("gap", 0.04f, 0.07f);
            float open = genes.Range("open", 0.7f, 0.95f);
            float low = GearFit.NeckLow + genes.Range("low", 0.01f, 0.05f);
            float tuck = GearFit.NeckTuck - 0.02f - genes.Range("tuck", 0.005f, 0.02f);
            OpenHood(mesh, kit, genes, gap, open, low, tuck, kit.Shell, 0.012f, peak: 0f);
            // A band of leather round the brow, now and then.
            if (kit.Look.Legendary || genes.Chance("band", 0.5f)) BrowBand(mesh, kit, genes, Centre + 0.1f, HalfWidth + gap + 0.012f, HalfDepth + gap + 0.012f, 0.025f);
            if (kit.Look.Legendary) BrowStone(mesh, kit, genes.Part("stone"), Centre + 0.1f, HalfDepth + gap + 0.04f);
        }

        // A hood laid round the head with a face open in it, from the neck (tucked in below the jaw) over the crown, rolled at the face: what a coif, a hood and a cowl all are.
        private static void OpenHood(GearMesh mesh, ArmourKit kit, Genes genes, float gap, float open, float low, float tuck, VertexMaterial cloth, float thick, float peak, VertexMaterial? edge = null)
        {
            if (edge != null) mesh.Mark("edged");
            float top = Top + gap + peak;
            mesh.Plate((u, v) =>
            {
                // u runs round from one side of the face to the other, the long way; v from the neck up over the crown.
                float y = low + (top - low) * v;
                // Narrow at the throat, open round the face, and closing again over the brow, all smoothly, so its edge is a line and not a stair.
                float gapHere = 0.15f + (open - 0.15f) * Smooth((y - (GearFit.HelmLow - 0.1f)) / 0.12f);
                gapHere += (open * 0.4f - gapHere) * Smooth((y - (Centre + 0.1f)) / 0.2f);
                float angle = Front + gapHere + (MathF.Tau - gapHere * 2f) * u;
                Vector3 p;
                if (y < GearFit.HelmLow)
                {
                    float r = MathF.Min(tuck, HalfWidth + gap);
                    p = new Vector3(MathF.Cos(angle) * r, y, MathF.Sin(angle) * r * 0.98f);
                }
                else
                {
                    float s = Math.Clamp((y - Centre) / (top - Centre), -1f, 1f);
                    float round = y > Centre ? MathF.Sqrt(MathF.Max(1f - s * s, 0.0025f)) : 1f;
                    // Out from the tuck at the throat, across only as far as GearFit.HeadSide until it's above a shoulder's plate, and front to back as far as it likes.
                    float full = HalfWidth + gap, beside = MathF.Min(full, GearFit.HeadSide - thick * 2f);
                    float rw = y < GearFit.ShoulderTop
                        ? tuck + (beside - tuck) * Smooth((y - GearFit.HelmLow) / (GearFit.ShoulderTop - GearFit.HelmLow))
                        : beside + (full - beside) * Smooth((y - GearFit.ShoulderTop) / 0.12f);
                    float rd = tuck + (HalfDepth + gap - tuck) * Smooth((y - GearFit.HelmLow) / 0.18f);
                    p = new Vector3(MathF.Cos(angle) * rw * round, y, MathF.Sin(angle) * rd * round - 0.02f * Smooth((y - GearFit.HelmLow) / 0.18f));
                }
                Vector3 n = new Vector3(p.X, (y - Centre) * 0.5f, p.Z + 0.02f);
                return (p, n.LengthSquared > 1e-8f ? n.Normalized() : Vector3.UnitY);
            }, 22, 12, thick, (_, _) => cloth, PlateEdge.Start | PlateEdge.End, thick * 1.4f, underside: kit.Under, rollMaterial: edge);
            mesh.SkinBy(at => at.Y < GearFit.HelmLow + 0.05f ? BodyForm.Throat(at.Y) : (new Vector2((int)BoneType.Head, 0f), new Vector2(1f, 0f)));
        }

        // A hood: hide or cloth over the head, open at the face, its point falling back, with a band at its edge.
        private static void Hood(GearMesh mesh, ArmourKit kit, Genes genes)
        {
            GearLook look = kit.Look;
            float gap = genes.Range("gap", 0.07f, 0.12f);
            float peak = genes.Range("peak", 0f, 0.24f) * (0.4f + look.Spec.Form.Curve);
            float open = genes.Range("open", 0.75f, 1f);
            float low = GearFit.NeckLow + genes.Range("low", 0.01f, 0.06f);
            float tuck = GearFit.NeckTuck - 0.022f - genes.Range("tuck", 0.005f, 0.02f);
            OpenHood(mesh, kit, genes, gap, open, low, tuck, kit.Shell, 0.014f, peak, genes.Chance("edge", 0.1f + kit.Look.Power * 0.6f) ? (kit.Hide ? kit.C.LeatherContrast : kit.Trim) : null);
            if (look.Legendary)
            {
                // A gilt circlet worn over the hood's brow, and a stone at its front.
                float y = Centre + 0.14f;
                float s = Math.Clamp((y - Centre) / (Top + gap + peak - Centre), -1f, 1f);
                float round = MathF.Sqrt(MathF.Max(1f - s * s, 0.05f));
                BrowBand(mesh, kit, genes, y, (HalfWidth + gap) * round + 0.016f, (HalfDepth + gap) * round + 0.012f, 0.022f);
                BrowStone(mesh, kit, genes.Part("stone"), y, (HalfDepth + gap) * round + 0.03f);
            }
            int front = genes.Pick("front", 2f, 1f, 0.8f);
            if (front == 1)
            {
                // Two ties hanging from the throat.
                mesh.Mark("ties");
                foreach (float side in new[] { -1f, 1f })
                {
                    Vector3 from = new Vector3(side * tuck * 0.55f, GearFit.HelmLow + 0.02f, tuck * 0.82f);
                    // Short enough that their ends stay above where nothing of the head's may go (GearFit.NeckLow).
                    var cord = new[] { from, from + new Vector3(side * 0.01f, -0.06f, 0.015f), from + new Vector3(side * 0.005f, -0.12f, 0.01f) };
                    mesh.Sweep(Section.Round(4), Spine.Through(cord, 4), t => new Vector2(0.007f), kit.Strap, Vector3.UnitZ);
                    GearKit.Tassel(mesh, genes.Part($"tie{side}"), cord[^1], 0.02f, 0.011f, kit.Strap, kit.Strap);
                }
            }
            else if (front == 2)
            {
                // The hood's brow stiffened into a short peak.
                mesh.Mark("hood peak");
                float y = Centre + 0.2f;
                float s = Math.Clamp((y - Centre) / (Top + gap + peak - Centre), -1f, 1f);
                float round = MathF.Sqrt(MathF.Max(1f - s * s, 0.05f));
                mesh.Plate((u, v) =>
                {
                    float angle = Front + (u - 0.5f) * open * 1.6f;
                    float r = (HalfDepth + gap) * round + 0.01f + 0.1f * v;
                    return (new Vector3(MathF.Cos(angle) * ((HalfWidth + gap) * round + 0.01f + 0.1f * v), y - 0.03f * v, MathF.Sin(angle) * r - 0.02f), Vector3.UnitY);
                }, 6, 2, 0.012f, (_, _) => kit.Shell, PlateEdge.Top, 0.01f, underside: kit.Under);
            }
            if (genes.Chance("tail", 0.45f))
            {
                mesh.Mark("tail");
                // A short point falling back from the crown, staying above the collar.
                float length = genes.Range("tail.length", 0.25f, 0.6f);
                float wide = genes.Range("tail.wide", 0.08f, 0.14f);
                float top = Top + gap + peak;
                var keys = new[] { new Vector3(0f, top - 0.12f, -0.12f), new Vector3(0f, top - 0.05f, -0.12f - length * 0.4f), new Vector3(0f, MathF.Max(top - length * 0.45f, GearFit.HelmLow + 0.1f), -0.12f - length * 0.7f) };
                mesh.Sweep(Section.Round(8), Spine.Through(keys, 8), t => new Vector2(wide * (1f - t * 0.9f)), kit.Shell, Vector3.UnitX);
            }
            if (kit.Make == GearMake.Hide && look.Motif == Motif.Fang && genes.Chance("quills", 0.6f) || look.Legendary)
            {
                // Quills or a crest of feathers from a beast brought down, standing up from the crown.
                int quills = genes.Count("quills.count", 3, 6);
                for (int i = 0; i < quills; i++)
                {
                    float z = -0.05f - i * 0.07f;
                    Vector3 at = new Vector3(0f, Top + gap * 0.9f + peak * 0.5f - i * 0.03f, z);
                    var keys = new[] { at, at + new Vector3(0f, 0.18f, -0.08f), at + new Vector3(0f, 0.3f, -0.22f) };
                    mesh.Sweep(Section.Slab(0.2f), Spine.Through(keys, 5), t => new Vector2(0.004f, 0.04f * MathF.Sin(MathF.Max(t, 0.12f) * MathF.PI)), look.Legendary ? kit.C.Trim : kit.C.Bone, Vector3.UnitX);
                }
            }
        }

        // A cap of panels of hardened leather or quilted cloth, its seams standing up, a band round its brow, maybe a peak at the front and flaps over the ears.
        private static void Cap(GearMesh mesh, ArmourKit kit, Genes genes)
        {
            GearLook look = kit.Look;
            float gap = genes.Range("gap", 0.04f, 0.07f);
            float brow = Centre + genes.Range("brow", 0.02f, 0.1f);
            List<Ring> dome = Dome(genes, brow, gap, genes.Range("peak", 0f, 0.1f), genes.Range("flat", 0f, 0.3f));
            mesh.Loft(Section.Round(18), dome, kit.Shell, capStart: false, capEnd: true);
            int panels = genes.Count("panels", 4, 6);
            mesh.Mark($"panels {panels}");
            float crownY = dome[^1].Centre.Y;
            float plain = (1f - kit.Look.Power) * (1f - kit.Look.Power);
            int extra = genes.Pick("extra", 0.3f + 2.4f * plain, 0.5f + kit.Look.Power, 0.5f + kit.Look.Power, 0.5f + kit.Look.Power);
            if (extra == 1)
            {
                // A ridge of doubled leather over the crown, front to back.
                mesh.Mark("ridge");
                var path = new List<Vector3>();
                var normals = new List<Vector3>();
                foreach (Ring ring in dome)
                {
                    path.Add(ring.Centre + new Vector3(0f, 0f, ring.HalfDepth));
                    normals.Add((Vector3.UnitZ + Vector3.UnitY * ((ring.Centre.Y - brow) / (crownY - brow + 0.01f))).Normalized());
                }
                // Over the top, through the middle of the crown's flat, and down the back.
                path.Add(dome[^1].Centre);
                normals.Add(Vector3.UnitY);
                for (int k = dome.Count - 1; k >= 0; k--)
                {
                    path.Add(dome[k].Centre - new Vector3(0f, 0f, dome[k].HalfDepth));
                    normals.Add((-Vector3.UnitZ + Vector3.UnitY * ((dome[k].Centre.Y - brow) / (crownY - brow + 0.01f))).Normalized());
                }
                mesh.Strip(path, normals, 0.026f, 0.016f, kit.ShellDark);
            }
            else if (extra == 2)
            {
                // A plate of harder leather riveted over the brow.
                mesh.Mark("brow plate");
                mesh.Plate((u, v) =>
                {
                    float angle = Front + (u - 0.5f) * 1.5f;
                    Vector3 p = OnHead(brow + 0.03f + 0.11f * v, angle, gap + 0.008f);
                    return (p, new Vector3(MathF.Cos(angle), 0.25f, MathF.Sin(angle)).Normalized());
                }, 6, 2, 0.012f, (_, _) => kit.C.LeatherContrast, PlateEdge.None, underside: kit.ShellDark);
                foreach (float a in new[] { Front - 0.6f, Front, Front + 0.6f }) mesh.Rivet(OnHead(brow + 0.09f, a, gap + 0.022f), new Vector3(MathF.Cos(a), 0.25f, MathF.Sin(a)).Normalized(), 0.014f, kit.Fitting);
            }
            else if (extra == 3)
            {
                // A knot of leather on top, where the panels meet.
                mesh.Mark("knot");
                mesh.At(Matrix4.CreateTranslation(0f, crownY - 0.02f, -0.015f), () => mesh.Lathe(Section.Round(8), 0.07f, 3, t => 0.04f * (0.6f + 0.6f * MathF.Sin(MathF.Max(t, 0.1f) * MathF.PI)), kit.Strap));
            }
            for (int i = 0; i < panels; i++)
            {
                float angle = Front + i * MathF.Tau / panels + MathF.PI / panels;
                var path = new List<Vector3>();
                var normals = new List<Vector3>();
                foreach (Ring ring in dome.Take(dome.Count - 1))
                {
                    Vector3 out_ = new Vector3(MathF.Cos(angle), 0f, MathF.Sin(angle));
                    path.Add(ring.Centre + new Vector3(out_.X * ring.HalfWidth, 0f, out_.Z * ring.HalfDepth));
                    normals.Add((out_ + Vector3.UnitY * ((ring.Centre.Y - brow) / (dome[^1].Centre.Y - brow + 0.01f))).Normalized());
                }
                mesh.Strip(path, normals, 0.012f, 0.008f, kit.ShellDark);
            }
            // Its brow banded in leather, or in fur on a hide cap now and then; a hardened cap sometimes has a nasal.
            bool fur = kit.Hide && !look.Legendary && genes.Chance("fur", 0.25f);
            BrowBand(mesh, kit, genes, brow + 0.01f, HalfWidth + gap + (fur ? 0.012f : 0.005f), HalfDepth + gap + (fur ? 0.012f : 0.005f), fur ? 0.04f : 0.028f, fur ? kit.C.Fur : null);
            if (kit.Make == GearMake.Hardened && genes.Chance("nasal", 0.25f)) Nasal(mesh, kit, genes.Part("nasal"), brow, gap);
            if (fur) mesh.Mark("fur");
            if (genes.Chance("peak.front", 0.35f))
            {
                mesh.Mark("peak");
                mesh.Plate((u, v) =>
                {
                    float angle = Front + (u - 0.5f) * 1.6f;
                    Vector3 p = OnHead(brow, angle, gap + 0.01f);
                    Vector3 out_ = new Vector3(MathF.Cos(angle), 0f, MathF.Sin(angle));
                    return (p + out_ * 0.12f * v - Vector3.UnitY * 0.03f * v, Vector3.UnitY);
                }, 8, 2, 0.012f, (_, _) => kit.ShellDark, PlateEdge.Top, 0.01f, underside: kit.ShellDark, rollMaterial: kit.Gilt);
            }
            if (genes.Chance("flaps", 0.3f))
            {
                mesh.Mark("flaps");
                foreach (float side in new[] { 1f, -1f })
                {
                    float centre = side > 0f ? 0.05f : MathF.PI - 0.05f;
                    mesh.Plate((u, v) =>
                    {
                        float angle = centre + (u - 0.5f) * 0.7f;
                        float y = GearFit.HelmLow + 0.04f + (brow - GearFit.HelmLow - 0.04f) * v;
                        Vector3 p = OnHead(MathF.Max(y, Centre - 0.2f), angle, gap);
                        return (new Vector3(p.X, y, p.Z), new Vector3(MathF.Cos(angle), 0f, MathF.Sin(angle)));
                    }, 4, 4, 0.012f, (_, _) => kit.Shell, PlateEdge.Bottom, 0.012f, underside: kit.Under);
                }
            }
            if (kit.Make == GearMake.Studded)
            {
                mesh.Mark("studs");
                for (int i = 0; i < 8; i++)
                {
                    float angle = Front + i * MathF.Tau / 8f;
                    Vector3 at = OnHead(brow + 0.08f, angle, gap + 0.01f);
                    mesh.Rivet(at, new Vector3(MathF.Cos(angle), 0.3f, MathF.Sin(angle)).Normalized(), 0.018f, kit.Fitting);
                }
            }
        }

        // A wide hat, a shallow cone of stiffened cloth, lacquered wood or hardened leather over a frame, that shades the shoulders and is tied under the chin: what walkers, wandering scholars
        // and those who study what the Reich would rather they didn't wear, and from above what a caster is known by. It rests on the crown on a headband inside it, and its rim stays well
        // above a shoulder's plate (GearFit.BrimLow).
        private static void Kasa(GearMesh mesh, ArmourKit kit, Genes genes)
        {
            GearLook look = kit.Look;
            float reach = genes.Range("reach", 0.5f, 0.78f) * (0.85f + look.Spec.Form.Size * 0.3f);
            float rise = genes.Range("rise", 0.16f, 0.34f);
            float apex = Top + genes.Range("apex", 0.12f, 0.18f);
            float power = genes.Range("profile", 0.8f, 1.35f);
            int stuff = genes.Pick("stuff", 5.5f, 3f + look.Craft * 2f, 1.5f);
            VertexMaterial body = stuff switch
            {
                0 => genes.Chance("plain", 0.6f) ? kit.C.Lining : kit.C.Cloth,
                1 => look.Legendary && genes.Chance("vermilion", 0.5f) ? GearColours.Of(GearMaterials.Timber, GearSwatches.Find(GearSwatches.Woods, "vermilion lacquer").Albedo) : kit.C.WoodDark,
                _ => kit.C.Leather,
            };
            VertexMaterial binding = look.Legendary ? kit.C.Precious : stuff == 1 ? kit.Fitting : kit.C.LeatherDark;
            float Height(float r) => apex - rise * MathF.Pow(Math.Clamp(r / reach, 0f, 1f), power);
            const float From = 0.04f;
            mesh.Plate((u, v) =>
            {
                float angle = u * MathF.Tau;
                float r = From + (reach - From) * v;
                float slope = rise * power * MathF.Pow(MathF.Max(r / reach, 0.01f), power - 1f) / reach;
                var out_ = new Vector3(MathF.Cos(angle) * slope, 1f, MathF.Sin(angle) * slope);
                return (new Vector3(MathF.Cos(angle) * r, Height(r), MathF.Sin(angle) * r), out_);
            }, 28, 6, 0.012f, (_, v) => v > 0.93f ? binding : body, PlateEdge.Top, 0.012f, wrap: true, underside: kit.ShellDark, rollMaterial: kit.Gilt);
            // A knob where the cone comes to its point.
            mesh.At(Matrix4.CreateTranslation(0f, Height(From) - 0.006f, 0f), () => mesh.Lathe(Section.Round(10), 0.05f, 2, t => From * 1.3f * (1f - t * 0.5f), binding));
            // The headband inside it, where it rests on the crown, and the cord tied under the chin.
            float band = HalfWidth * 0.9f;
            mesh.At(Matrix4.CreateTranslation(0f, Centre + 0.22f, -0.015f), () => mesh.Lathe(Section.Round(16), 0.04f, 1, _ => band, kit.C.Lining, capStart: false, capEnd: false, squash: HalfDepth / HalfWidth));
            foreach (float side in new[] { 1f, -1f })
            {
                // Down the cheeks on the head's surface, from beside the band to just short of the chin's middle.
                float Round(float t) => side > 0f ? t : MathF.PI - t;
                var cord = new[] { new Vector3(side * band, Centre + 0.22f, -0.015f), OnHead(Centre - 0.05f, Round(0.25f), 0.014f), OnHead(GearFit.HelmLow + 0.1f, Round(0.8f), 0.014f), OnHead(GearFit.HelmLow + 0.02f, Round(1.35f), 0.014f) };
                mesh.Sweep(Section.Round(4), Spine.Through(cord, 6), _ => new Vector2(0.008f), kit.Strap, Vector3.UnitZ);
            }
            if (look.Ornament > 0.45f && genes.Chance("ring", 0.6f))
            {
                // A ring of colour round the crown.
                float r0 = reach * 0.18f, r1 = reach * 0.26f;
                mesh.Plate((u, v) =>
                {
                    float angle = u * MathF.Tau;
                    float r = r0 + (r1 - r0) * v;
                    return (new Vector3(MathF.Cos(angle) * r, Height(r) + 0.012f, MathF.Sin(angle) * r), Vector3.UnitY);
                }, 20, 1, 0.004f, (_, _) => kit.C.Trim, PlateEdge.None, wrap: true);
            }
            if (look.Legendary || look.Shines("hat.stone"))
            {
                // A stone set in the front of the cone, facing the way it slopes.
                float r = reach * 0.45f;
                float slope = rise * power * MathF.Pow(r / reach, power - 1f) / reach;
                float size = genes.Range("stone.size", 0.035f, 0.055f) * (look.Legendary ? 1.3f : 1f);
                mesh.At(GearMesh.FacingOut(new Vector3(0f, Height(r) + 0.012f, r), new Vector3(0f, 1f, slope)), () => GearKit.Setting(mesh, genes.Part("stone"), size, kit.C.Precious, look.Theme != GearTheme.None ? kit.C.Glow : kit.C.Gem));
            }
        }

        // A thin band round the brow and nothing else, so that hair still shows: points rising from it and a stone at its front.
        private static void Circlet(GearMesh mesh, ArmourKit kit, Genes genes)
        {
            GearLook look = kit.Look;
            float y = Centre + genes.Range("at", 0.1f, 0.2f);
            float s = Math.Clamp((y - Centre) / BodyForm.HeadHalfHeight, -1f, 1f);
            float round = MathF.Sqrt(MathF.Max(1f - s * s, 0.05f));
            float rw = HalfWidth * round + 0.02f, rd = HalfDepth * round + 0.02f;
            float wide = genes.Range("wide", 0.018f, 0.045f);
            BrowBand(mesh, kit, genes, y, rw, rd, wide);
            int points = genes.Count("points", 0, 7);
            float reach = genes.Range("point.reach", 0.05f, 0.16f);
            for (int i = 0; i < points; i++)
            {
                float angle = Front + (i - (points - 1) * 0.5f) * genes.Range("point.gap", 0.3f, 0.55f);
                Vector3 at = new Vector3(MathF.Cos(angle) * rw, y + wide, MathF.Sin(angle) * rd - 0.015f);
                mesh.At(Matrix4.CreateTranslation(at), () => mesh.Lathe(Section.Polygon(4), reach * (i == points / 2 ? 1.5f : 1f), 2, t => 0.022f * (1f - t), look.Craft > 0.5f ? kit.C.Precious : kit.Fitting, capEnd: false));
            }
            BrowStone(mesh, kit, genes.Part("stone"), y, rd + 0.004f);
        }
    }
}
