using Dino_Engine.ECS.Components;
using Dino_Engine.Modelling.Model;
using OpenTK.Mathematics;

namespace Dino_Engine.Modelling.Procedural.Gear
{
    /// <summary>
    /// The outline of one body piece round a body, which everything on it is laid over: how far out it sits, how it swells at the chest and draws in at the waist, and how far its skirt flares, always
    /// clear of both legs in whatever they have on (<see cref="GearFit.HemAt"/>), and never narrower at the neck than a collar's inside (<see cref="GearFit.CollarInner"/>). An angle of 0 is the body's
    /// left side (+x) and a quarter turn is its front (+z).
    /// </summary>
    internal sealed class TorsoShape
    {
        public readonly float Base, Chest, Cinch, Waist, Hem, Flare;
        public bool Skirted => Hem < BodyForm.Hip - 0.1f;

        public TorsoShape(ArmourKit kit, Genes genes)
        {
            float cover = kit.Look.Spec.Form.Cover;
            Base = GearFit.BodyBase + kit.Blend("base", new Vector2(0.02f, 0.05f), new Vector2(0f, 0.025f), new Vector2(0.01f, 0.04f));
            Chest = kit.Blend("chest", new Vector2(0.04f, 0.13f), new Vector2(0f, 0.04f), new Vector2(0f, 0.03f));
            Cinch = kit.Blend("cinch", new Vector2(0f, 0.025f), new Vector2(0f, 0.05f), new Vector2(0.01f, 0.07f));
            Waist = genes.Range("waist", 2.5f, 2.78f);
            Hem = MathF.Max(kit.Blend("hem", new Vector2(1.72f, 2f), new Vector2(2.05f - cover * 0.85f, 1.9f - cover * 0.55f), new Vector2(1.7f - cover * 1.45f, 1.45f - cover * 1.05f)), 0.48f);
            Flare = kit.Blend("flare", new Vector2(0f, 0.08f), new Vector2(0.02f, 0.12f), new Vector2(0.06f, 0.3f));
        }

        /// <summary>Half its width and half its depth at a height, for a surface <paramref name="layer"/> metres out from the piece's own.</summary>
        public Vector2 Size(float y, float layer)
        {
            float belly = 1f - Math.Clamp(MathF.Abs(y - Waist) / 0.5f, 0f, 1f);
            float body = BodyForm.Torso(MathF.Max(y, 1.9f));
            float torso = body + Base - Cinch * belly + layer;
            float depth = body * BodyForm.Depth + Base - Cinch * belly * 0.6f + layer;
            // Drawn in at the waist, but never inside the legs' seat where that's under it (GearFit.LegsTop).
            float least = GearFit.BodyBase * Math.Clamp((GearFit.LegsTop + 0.12f - y) / 0.12f, 0f, 1f);
            torso = MathF.Max(torso, body + least + layer);
            depth = MathF.Max(depth, body * BodyForm.Depth + least + layer);
            float skirt = Smooth(BodyForm.Hip + 0.15f, BodyForm.Hip - 0.15f, y);
            if (skirt > 0f)
            {
                Vector2 clear = GearFit.HemAt(y) + new Vector2(layer);
                float below = Math.Clamp((BodyForm.Hip - y) / MathF.Max(BodyForm.Hip - Hem, 0.1f), 0f, 1f);
                float flare = Flare * below * below;
                torso += (MathF.Max(torso, clear.X) + flare - torso) * skirt;
                depth += (MathF.Max(depth, clear.Y) + flare * 0.8f - depth) * skirt;
            }
            // Over the shoulders it comes in towards the neck, but never inside the collar's opening, where a helm's neck has to fit.
            float neck = Math.Clamp((y - 3.8f) / 0.18f, 0f, 1f);
            float opening = GearFit.CollarInner + 0.03f + layer;
            float hold = Math.Clamp((y - 3.7f) / (GearFit.CollarLow - 3.72f), 0f, 1f);
            torso = MathF.Max(torso * (1f - 0.5f * neck), opening * hold);
            depth = MathF.Max(depth * (1f - 0.35f * neck), opening * hold);
            return new Vector2(torso, depth);
        }

        public float Bulge(float y) => Chest * (1f - MathF.Pow(Math.Clamp((y - 3.25f) / 0.8f, -1f, 1f), 2f));

        public Vector3 Point(float y, float angle, float layer)
        {
            Vector2 size = Size(y, layer);
            float bulge = Bulge(y);
            return new Vector3(MathF.Cos(angle) * size.X, y, bulge * 0.5f + MathF.Sin(angle) * (size.Y + bulge * 0.5f));
        }

        public Vector3 Normal(float y, float angle, float layer)
        {
            const float Step = 0.01f;
            Vector3 around = Point(y, angle + Step, layer) - Point(y, angle - Step, layer);
            Vector3 up = Point(y + Step, angle, layer) - Point(y - Step, angle, layer);
            // Out of the body: up crossed with the way round, as the angle goes from the left side towards the front.
            Vector3 normal = Vector3.Cross(up, around);
            return normal.LengthSquared > 1e-12f ? normal.Normalized() : new Vector3(MathF.Cos(angle), 0f, MathF.Sin(angle));
        }

        private static float Smooth(float edge0, float edge1, float x)
        {
            float t = Math.Clamp((x - edge0) / (edge1 - edge0), 0f, 1f);
            return t * t * (3f - 2f * t);
        }
    }

    /// <summary>
    /// What's worn on the body, which is most of what a hero looks like from up where the camera is. A garment from the hem to the collar, offset from <see cref="BodyForm"/>, then whatever its making
    /// lays over it: a cuirass of plate with its fauld and tassets, rows of lamellar laced in colour, a brigandine's rivets, plates of hardened leather, studs, or the layers and trims of a robe; then
    /// sleeves, shoulders, a collar, a belt, and whatever hangs from it all. Nothing is a model from a list: a plate harness and a silk robe are this same code with different makings and shares.
    /// Everything keeps to its bands in <see cref="GearFit"/>, so what's worn on the legs, hands, feet and head goes over or under it without meeting it. See GEAR.md.
    /// </summary>
    public static class TorsoGenerator
    {
        private const int Around = 24;
        private const float Front = MathF.PI / 2f;
        private const float Back = MathF.PI * 1.5f;

        public static void Build(GearMesh mesh, GearLook look)
        {
            var kit = new ArmourKit(look);
            Genes genes = look.Genes;
            var shape = new TorsoShape(kit, genes.Part("shape"));

            mesh.Bone = (int)BoneType.Chest;
            float outer = 0f;
            mesh.Counted("garment", () => outer = Garment(mesh, kit, shape, genes.Part("garment")));
            mesh.Counted(kit.Make.ToString().ToLowerInvariant(), () =>
            {
                switch (kit.Make)
                {
                    case GearMake.Plate:
                        outer = Cuirass(mesh, kit, shape, genes.Part("cuirass"));
                        break;
                    case GearMake.Lamellar:
                        outer = Lamellar(mesh, kit, shape, genes.Part("lamellar"));
                        break;
                    case GearMake.Brigandine:
                    case GearMake.Studded:
                        Studs(mesh, kit, shape, genes.Part("studs"), kit.Make == GearMake.Brigandine);
                        break;
                    case GearMake.Hardened:
                        outer = Boiled(mesh, kit, shape, genes.Part("boiled"));
                        break;
                    case GearMake.Cloth:
                    case GearMake.Silk:
                        outer = Layers(mesh, kit, shape, genes.Part("layers"));
                        break;
                    case GearMake.Hide:
                        Lacing(mesh, kit, shape, genes.Part("lacing"));
                        break;
                    case GearMake.Mail:
                        if (genes.Chance("surcoat", 0.3f + look.Ornament * 0.4f)) outer = Tabard(mesh, kit, shape, genes.Part("surcoat"), kit.C.Cloth, kit.C.Trim);
                        break;
                    case GearMake.Padded:
                        Closure(mesh, kit, shape, genes.Part("closure"));
                        break;
                    case GearMake.Scale:
                        Placket(mesh, kit, shape, genes.Part("placket"));
                        Mirrors(mesh, kit, shape, genes.Part("mirrors"));
                        break;
                }
            });

            mesh.Counted("sleeves", () => Sleeves(mesh, kit, shape, genes.Part("sleeves")));
            mesh.Counted("shoulders", () => Shoulders(mesh, kit, shape, genes.Part("shoulders")));
            mesh.Counted("collar", () => Collar(mesh, kit, shape, genes.Part("collar"), outer));
            mesh.Counted("belt", () => Belt(mesh, kit, shape, genes.Part("belt"), outer));
            mesh.Counted("hanging", () => Hanging(mesh, kit, shape, genes.Part("hanging"), outer));
            if (look.Legendary || look.Shines("chest.stone")) mesh.Counted("medallion", () => Medallion(mesh, kit, shape, genes.Part("medallion"), outer));
            mesh.Covers |= BodyRegions.Torso;
        }

        // The garment: one skin from the hem to the collar, banded at the hem. For plate, lamellar and boiled leather it's what's worn under them; for everything else it's the thing itself.
        // What comes back is how far out its outside is, for what goes over it.
        private static float Garment(GearMesh mesh, ArmourKit kit, TorsoShape shape, Genes genes)
        {
            bool under = kit.Make is GearMake.Plate or GearMake.Lamellar or GearMake.Hardened;
            VertexMaterial body = under ? kit.Under : kit.Shell;
            VertexMaterial hem = kit.Make is GearMake.Mail ? kit.Shell : kit.Woven ? kit.Trim : kit.Edge;
            // A yoke across the shoulders in a second hide or cloth, as a coat is often cut.
            bool yoked = !under && kit.Make != GearMake.Mail && genes.Chance("yoke", 0.3f);
            VertexMaterial yoke = kit.Hide ? kit.C.LeatherContrast : kit.Woven ? (genes.Chance("yoke.dark", 0.5f) ? kit.C.ClothDark : kit.C.Trim) : kit.ShellDark;
            float yokeAt = genes.Range("yoke.at", 3.42f, 3.6f);

            var heights = new List<float>();
            float step = MathF.Max((BodyForm.Hip + 0.2f - shape.Hem) / 9f, 0.07f);
            for (float y = shape.Hem; y < BodyForm.Hip + 0.2f - step * 0.5f; y += step) heights.Add(y);
            for (float y = BodyForm.Hip + 0.2f; y < 3.7f; y += 0.15f) heights.Add(y);
            heights.AddRange(new[] { 3.72f, 3.8f, 3.87f, 3.93f, 3.98f });

            int hemBands = genes.Count("hem.bands", 1, 1 + (int)(kit.Look.Ornament * 2.5f));
            float bandHeight = genes.Range("hem.band", 0.035f, 0.08f);
            var rings = new List<Ring>();
            var bands = new List<int>();
            void Add(float y, int band)
            {
                Vector2 size = shape.Size(y, 0f);
                float bulge = shape.Bulge(y);
                rings.Add(new Ring { Centre = new Vector3(0f, y, bulge * 0.5f), Side = Vector3.UnitX, Front = Vector3.UnitZ, HalfWidth = size.X, HalfDepth = size.Y + bulge * 0.5f });
                bands.Add(band);
            }
            // Hem bands first, each its own crisp band, then the body.
            for (int b = 0; b < hemBands; b++) Add(shape.Hem + b * bandHeight, b % 2 == 0 ? 1 : 2);
            if (yoked) heights.Add(yokeAt);
            heights.Sort();
            foreach (float y in heights)
            {
                if (y > shape.Hem + hemBands * bandHeight - 0.01f && (rings.Count == 0 || y - rings[^1].Centre.Y > 0.02f)) Add(y, yoked && y >= yokeAt - 0.001f ? 3 : 0);
            }

            int first = mesh.VertexCount;
            // What's worn under armour is mostly hidden by it, and needs fewer sides.
            mesh.Bands(Section.Round(under ? 20 : Around), rings, (ring, _) => bands[ring] switch { 1 => hem, 2 => kit.Under, 3 => yoke, _ => body }, capStart: false, capEnd: false);
            if (shape.Skirted) mesh.SkinBy(at => at.Y < BodyForm.Hip + 0.15f ? BodyForm.Skirt(at) : (new Vector2((int)BoneType.Chest, 0f), new Vector2(1f, 0f)), first);
            return 0.012f;
        }

        // A breastplate and a backplate over the garment, a keel down the breastplate's middle, rolled at the neck and the armholes; lames of a fauld below it round the belly, and tassets
        // hanging from the fauld over the thighs. Rivets where the lames are held.
        private static float Cuirass(GearMesh mesh, ArmourKit kit, TorsoShape shape, Genes genes)
        {
            GearLook look = kit.Look;
            float layer = 0.025f;
            float thick = genes.Range("thick", 0.018f, 0.028f);
            float roll = thick * genes.Range("roll", 1.1f, 1.6f);
            float low = shape.Waist - genes.Range("low", 0.04f, 0.14f);
            float top = genes.Range("top", 3.6f, 3.72f);
            float keel = genes.Range("keel", 0.01f, 0.05f) * (0.6f + look.Angular);
            float span = genes.Range("span", 1.15f, 1.4f);
            float dip = genes.Range("dip", 0.08f, 0.2f);
            float armhole = genes.Range("armhole", 0.12f, 0.26f);

            (Vector3, Vector3) Surface(float u, float v, float centre, float depth)
            {
                float angle = centre - span + span * 2f * u;
                float side = MathF.Abs(u * 2f - 1f);
                float edge = top - dip * (1f - side * side) * (centre == Front ? 1f : 0.4f) - armhole * MathF.Pow(side, 3f);
                float y = low + (edge - low) * v;
                Vector3 n = shape.Normal(y, angle, layer);
                Vector3 p = shape.Point(y, angle, layer) + n * (keel * depth * MathF.Pow(MathF.Max(1f - side * 1.6f, 0f), 2f));
                return (p, n);
            }

            VertexMaterial plate = kit.Shell;
            // A breastplate is some forty pixels across from where the camera is, so a dozen faces round it are already more than the eye can count.
            mesh.Plate((u, v) => Surface(u, v, Front, 1f), 12, 6, thick, (_, _) => plate, PlateEdge.Top | PlateEdge.Start | PlateEdge.End, roll, underside: kit.ShellDark, rollMaterial: kit.Gilt);
            mesh.Plate((u, v) => Surface(u, v, Back, 0.3f), 12, 5, thick, (_, _) => plate, PlateEdge.Top, roll, underside: kit.ShellDark, rollMaterial: kit.Gilt);

            // On a legendary harness, gilt scrolls up the breastplate either side of its keel.
            if (look.Legendary)
            {
                foreach (float side in new[] { -1f, 1f })
                {
                    foreach (float at in new[] { 0.62f })
                    {
                        float y = low + (top - low) * at;
                        float angle = Front + side * 0.3f;
                        Vector3 start = shape.Point(y, angle, layer + thick + 0.006f) + shape.Normal(y, angle, layer) * (keel * 0.2f);
                        Vector3 around = new Vector3(-MathF.Sin(angle), 0f, MathF.Cos(angle));
                        GearKit.Scroll(mesh, start, -around * side, Vector3.UnitY * (at > 0.5f ? 1f : -1f), 0.09f, 1.5f, 0.012f, kit.C.Precious);
                    }
                }
            }

            // Straps over the shoulders and at the sides, joining the two plates.
            foreach (float side in new[] { -1f, 1f })
            {
                float angle = side > 0f ? 0f : MathF.PI;
                for (int k = 0; k < 1; k++)
                {
                    float y = low + (top - low) * 0.5f;
                    Vector3 at = shape.Point(y, angle, layer + thick);
                    Vector3 n = shape.Normal(y, angle, layer);
                    mesh.Strip(new[] { at + Vector3.UnitZ * 0.12f, at, at - Vector3.UnitZ * 0.12f }, new[] { n, n, n }, 0.032f, 0.01f, kit.Strap);
                    mesh.At(GearMesh.Basis(at + n * 0.012f, Vector3.UnitY, Vector3.UnitZ, n), () => mesh.Buckle(0.06f, 0.05f, 0.006f, kit.Fitting));
                }
            }

            // The fauld: lames round the belly below the breastplate, each lapping the one below it.
            int lames = genes.Count("fauld", 2, 3);
            float lameHeight = genes.Range("lame", 0.09f, 0.13f);
            float y0 = low;
            for (int i = 0; i < lames; i++)
            {
                float lameTop = y0 + 0.025f, lameLow = y0 - lameHeight;
                float out_ = layer + 0.006f * (i + 1);
                mesh.Plate((u, v) =>
                {
                    float angle = Front - 1.55f + 3.1f * u;
                    float y = lameLow + (lameTop - lameLow) * v;
                    return (shape.Point(y, angle, out_), shape.Normal(y, angle, out_));
                }, 10, 1, thick * 0.9f, (_, _) => plate, PlateEdge.Bottom, roll * 0.8f, underside: kit.ShellDark, rollMaterial: kit.Gilt);
                foreach (float end in new[] { Front - 1.45f, Front + 1.45f })
                {
                    float y = (lameTop + lameLow) * 0.5f;
                    mesh.Rivet(shape.Point(y, end, out_ + thick), shape.Normal(y, end, out_), 0.016f, kit.Fitting);
                }
                y0 = lameLow;
            }

            // Tassets over the thighs, from the last lame, buckled on.
            if (kit.Likely("tassets", 0.9f, 0f, 0f))
            {
                int plates = genes.Count("tasset.lames", 2, 3);
                float tasset = genes.Range("tasset.lame", 0.1f, 0.15f);
                float width = genes.Range("tasset.width", 0.38f, 0.55f);
                foreach (float side in new[] { -1f, 1f })
                {
                    float centre = Front - side * genes.Range("tasset.at", 0.45f, 0.7f);
                    float y = y0 + 0.02f;
                    int first = mesh.VertexCount;
                    for (int i = 0; i < plates; i++)
                    {
                        float lameTop = y + 0.02f, lameLow = y - tasset;
                        float out_ = layer + 0.012f + 0.006f * i;
                        float flare = 1f + 0.08f * i;
                        mesh.Plate((u, v) =>
                        {
                            float angle = centre + (u - 0.5f) * width * flare;
                            float yy = lameLow + (lameTop - lameLow) * v;
                            return (shape.Point(yy, angle, out_), shape.Normal(yy, angle, out_));
                        }, 5, 1, thick * 0.9f, (_, _) => plate, PlateEdge.Bottom, roll * 0.7f, underside: kit.ShellDark, rollMaterial: kit.Gilt);
                        y = lameLow;
                    }
                    mesh.SkinBy(BodyForm.Skirt, first);
                    Vector3 hook = shape.Point(y0, centre, layer + 0.04f);
                    mesh.Strip(new[] { hook + Vector3.UnitY * 0.08f, hook, hook - Vector3.UnitY * 0.06f }, new[] { Vector3.UnitZ, Vector3.UnitZ, Vector3.UnitZ }, 0.025f, 0.008f, kit.Strap);
                }
            }
            return layer + thick;
        }

        // Rows of small plates laced together round the body, each row lapping the one below, with the laces running between: a long skirt of it too, in panels so the legs can move.
        // Each row is one ring of plates, each plate a flat face that laps the next, so a row's outline steps once a plate; there are never more than fourteen rows, as each costs a ring.
        private static float Lamellar(GearMesh mesh, ArmourKit kit, TorsoShape shape, Genes genes)
        {
            float layer = 0.02f;
            float top = genes.Range("top", 3.55f, 3.7f);
            float low = MathF.Max(shape.Hem + 0.05f, 1.55f);
            float row = MathF.Max(genes.Range("row", 0.11f, 0.16f), (top - low) / 14f);
            int rows = Math.Max((int)((top - low) / row), 3);
            int plates = genes.Count("plates", 18, 26);
            VertexMaterial lace = kit.Trim;

            // Two points to a plate: where it starts, standing out by its lap over the plate before, and where it ends, flush. The short step back out between them is the next plate's edge.
            var outline = new Vector2[plates * 2];
            var hard = new bool[plates * 2];
            for (int p = 0; p < plates; p++)
            {
                float start = p * MathF.Tau / plates, end = (p + 0.9f) * MathF.Tau / plates;
                outline[p * 2] = new Vector2(MathF.Cos(start), MathF.Sin(start)) * 1.035f;
                outline[p * 2 + 1] = new Vector2(MathF.Cos(end), MathF.Sin(end));
            }
            Array.Fill(hard, true);
            var section = new Section(outline, hard);

            for (int r = 0; r < rows; r++)
            {
                float rowTop = top - r * row, rowLow = rowTop - row * 1.12f;
                float out_ = layer + 0.004f * (rows - r) * 0.5f;
                int first = mesh.VertexCount;
                var rings = new Ring[2];
                for (int k = 0; k < 2; k++)
                {
                    float y = k == 0 ? rowLow : rowTop;
                    Vector2 size = shape.Size(y, out_);
                    float bulge = shape.Bulge(y);
                    rings[k] = new Ring { Centre = new Vector3(0f, y, bulge * 0.5f), Side = Vector3.UnitX, Front = Vector3.UnitZ, HalfWidth = size.X, HalfDepth = size.Y + bulge * 0.5f };
                }
                mesh.Loft(section, rings, kit.Shell, capStart: false, capEnd: false);
                if (rowLow < BodyForm.Hip + 0.15f) mesh.SkinBy(BodyForm.Skirt, first);

                // The lacing between this row and the next, over the plates' lap, so that it runs on round the body unbroken.
                float laceY = rowLow + row * 0.08f;
                Vector2 laced = shape.Size(laceY, out_) * 1.035f + new Vector2(0.004f);
                first = mesh.VertexCount;
                float bulgeAt = shape.Bulge(laceY);
                Ring[] band =
                {
                    new Ring { Centre = new Vector3(0f, laceY - 0.012f, bulgeAt * 0.5f), Side = Vector3.UnitX, Front = Vector3.UnitZ, HalfWidth = laced.X, HalfDepth = laced.Y + bulgeAt * 0.5f },
                    new Ring { Centre = new Vector3(0f, laceY, bulgeAt * 0.5f), Side = Vector3.UnitX, Front = Vector3.UnitZ, HalfWidth = laced.X + 0.005f, HalfDepth = laced.Y + bulgeAt * 0.5f + 0.005f },
                    new Ring { Centre = new Vector3(0f, laceY + 0.012f, bulgeAt * 0.5f), Side = Vector3.UnitX, Front = Vector3.UnitZ, HalfWidth = laced.X, HalfDepth = laced.Y + bulgeAt * 0.5f },
                };
                mesh.Loft(Section.Round(20), band, lace, capStart: false, capEnd: false);
                if (laceY < BodyForm.Hip + 0.15f) mesh.SkinBy(BodyForm.Skirt, first);
            }
            return layer + 0.03f;
        }

        // A band round the body at a height, for lacing, trims and belts.
        private static void ArmourBand(GearMesh mesh, Vector3 centre, float halfWidth, float halfDepth, float height, float thick, VertexMaterial material)
        {
            Ring[] rings =
            {
                new Ring { Centre = centre - Vector3.UnitY * height, Side = Vector3.UnitX, Front = Vector3.UnitZ, HalfWidth = halfWidth, HalfDepth = halfDepth, Hard = true },
                new Ring { Centre = centre - Vector3.UnitY * height * 0.6f, Side = Vector3.UnitX, Front = Vector3.UnitZ, HalfWidth = halfWidth + thick, HalfDepth = halfDepth + thick },
                new Ring { Centre = centre + Vector3.UnitY * height * 0.6f, Side = Vector3.UnitX, Front = Vector3.UnitZ, HalfWidth = halfWidth + thick, HalfDepth = halfDepth + thick },
                new Ring { Centre = centre + Vector3.UnitY * height, Side = Vector3.UnitX, Front = Vector3.UnitZ, HalfWidth = halfWidth, HalfDepth = halfDepth, Hard = true },
            };
            mesh.Loft(Section.Round(Around), rings, material, capStart: false, capEnd: false);
        }

        // A brigandine's rivets, in rows, holding the plates inside its cover, or a studded coat's studs: front and back, finer and closer on the better made.
        private static void Studs(GearMesh mesh, ArmourKit kit, TorsoShape shape, Genes genes, bool rivets)
        {
            float size = rivets ? genes.Range("rivet", 0.016f, 0.022f) : genes.Range("stud", 0.022f, 0.032f);
            int rows = genes.Count("rows", 4, 6);
            int each = genes.Count("each", rivets ? 6 : 5, rivets ? 9 : 8);
            float top = genes.Range("top", 3.5f, 3.65f), low = shape.Waist - 0.1f;
            foreach (float centre in new[] { Front, Back })
            {
                for (int r = 0; r < rows; r++)
                {
                    float y = top - (top - low) * r / MathF.Max(rows - 1, 1);
                    for (int i = 0; i < each; i++)
                    {
                        float angle = centre - 1.2f + 2.4f * (i + (r % 2) * 0.5f) / each;
                        mesh.Rivet(shape.Point(y, angle, 0.012f), shape.Normal(y, angle, 0f), size, kit.Fitting);
                    }
                }
            }
            if (rivets) Placket(mesh, kit, shape, genes);
        }

        // Where a coat closes down the front, a band of something else over the join.
        private static void Placket(GearMesh mesh, ArmourKit kit, TorsoShape shape, Genes genes)
        {
            float low = MathF.Max(shape.Hem, BodyForm.Hip - 0.1f), top = 3.9f;
            var path = new List<Vector3>();
            var normals = new List<Vector3>();
            for (int i = 0; i <= 10; i++)
            {
                float y = low + (top - low) * i / 10f;
                path.Add(shape.Point(y, Front, 0.012f));
                normals.Add(shape.Normal(y, Front, 0f));
            }
            // In the coat's own shadow, or leather on scales: never the trim, which with a sash of the trim across it makes a cross on the chest.
            mesh.Strip(path, normals, genes.Range("placket", 0.03f, 0.055f), 0.008f, kit.Make == GearMake.Scale ? kit.Edge : kit.ShellDark);
        }

        // Plates of leather boiled hard over a form, front and back, their edges welted in darker leather, laced together at the sides.
        private static float Boiled(GearMesh mesh, ArmourKit kit, TorsoShape shape, Genes genes)
        {
            float layer = 0.02f;
            float thick = genes.Range("thick", 0.022f, 0.032f);
            float top = genes.Range("top", 3.55f, 3.7f), low = shape.Waist - genes.Range("low", 0.1f, 0.25f);
            float span = genes.Range("span", 1.05f, 1.3f);
            // Moulded over a form: drawn in towards the waist, and with a ridge down the middle of the front, so it reads as a breastplate of leather and not an apron.
            float waist = genes.Range("taper", 0.68f, 0.85f);
            float keel = genes.Range("keel", 0.012f, 0.035f);
            foreach (float centre in new[] { Front, Back })
            {
                mesh.Plate((u, v) =>
                {
                    float across = span * (waist + (1f - waist) * v);
                    float angle = centre - across + across * 2f * u;
                    float y = low + (top - low - 0.1f * (1f - MathF.Pow(u * 2f - 1f, 2f)) * (centre == Front ? 1f : 0f)) * v;
                    Vector3 n = shape.Normal(y, angle, layer);
                    float side = MathF.Abs(u * 2f - 1f);
                    return (shape.Point(y, angle, layer) + n * (centre == Front ? keel * MathF.Pow(MathF.Max(1f - side * 1.8f, 0f), 2f) : 0f), n);
                }, 10, 5, thick, (_, _) => kit.Shell, PlateEdge.All, thick * 0.9f, underside: kit.ShellDark, rollMaterial: kit.Gilt);
            }
            // Straps over the shoulders, from the breastplate's top to the backplate's, buckled at the front.
            foreach (float side in new[] { -1f, 1f })
            {
                float y = top - 0.04f;
                float front = Front - side * span * 0.55f, back = Back + side * span * 0.55f;
                Vector3 f = shape.Point(y, front, layer + thick + 0.004f), b = shape.Point(y, back, layer + thick + 0.004f);
                var over = new Vector3(side * (GearFit.PauldronIn + 0.05f), 4.0f, -0.02f);
                var path = new List<Vector3> { f, Vector3.Lerp(f, over, 0.55f) + Vector3.UnitY * 0.06f, over, Vector3.Lerp(b, over, 0.55f) + Vector3.UnitY * 0.06f, b };
                var normals = new List<Vector3> { shape.Normal(y, front, layer), (shape.Normal(y, front, layer) + Vector3.UnitY).Normalized(), Vector3.UnitY, (shape.Normal(y, back, layer) + Vector3.UnitY).Normalized(), shape.Normal(y, back, layer) };
                mesh.Strip(Spine.Through(path, 10), Spine.Through(normals, 10), 0.032f, 0.009f, kit.Strap);
                Vector3 n0 = shape.Normal(y, front, layer);
                mesh.At(GearMesh.Basis(f + n0 * 0.012f, Vector3.Cross(Vector3.UnitY, n0).Normalized(), Vector3.UnitY, n0), () => mesh.Buckle(0.06f, 0.05f, 0.005f, kit.Fitting));
            }
            // The welt round each plate's edge is its rolled edge, in the darker leather.
            foreach (float side in new[] { 0f, MathF.PI })
            {
                for (int k = 0; k < 4; k++)
                {
                    float y = low + 0.08f + (top - low - 0.2f) * k / 3f;
                    Vector3 a = shape.Point(y + 0.05f, side - 0.18f, layer + thick), b = shape.Point(y - 0.05f, side + 0.18f, layer + thick);
                    Vector3 c = shape.Point(y + 0.05f, side + 0.18f, layer + thick), d = shape.Point(y - 0.05f, side - 0.18f, layer + thick);
                    mesh.Sweep(Section.Round(4), new[] { a, b }, _ => new Vector2(0.008f), kit.Strap, shape.Normal(y, side, layer));
                    mesh.Sweep(Section.Round(4), new[] { c, d }, _ => new Vector2(0.008f), kit.Strap, shape.Normal(y, side, layer));
                }
            }
            return layer + thick;
        }

        // Fitted leather laced up the front.
        private static void Lacing(GearMesh mesh, ArmourKit kit, TorsoShape shape, Genes genes)
        {
            if (!genes.Chance("laced", 0.6f)) return;
            int crosses = genes.Count("count", 4, 7);
            float spread = genes.Range("spread", 0.1f, 0.18f);
            float top = 3.8f, step = genes.Range("step", 0.1f, 0.14f);
            for (int i = 0; i < crosses; i++)
            {
                float y = top - i * step;
                foreach (float way in new[] { -1f, 1f })
                {
                    Vector3 a = shape.Point(y, Front + way * spread, 0.014f), b = shape.Point(y - step, Front - way * spread, 0.014f);
                    mesh.Sweep(Section.Round(4), new[] { a, b }, _ => new Vector2(0.009f), kit.Strap, shape.Normal(y, Front, 0f));
                }
            }
        }

        // A robe's layers: an over-robe open down the front over the garment, a tabard hung front and back, or a stole down the chest, each edged in its trim.
        private static float Layers(GearMesh mesh, ArmourKit kit, TorsoShape shape, Genes genes)
        {
            GearLook look = kit.Look;
            int kind = genes.Pick("kind", 1.5f, 2f + look.Craft * 2f, 1.5f, 1f + look.Craft * 2f);
            VertexMaterial cloth = genes.Chance("second.dye", 0.5f) ? kit.C.Trim : kit.C.ClothDark;
            VertexMaterial edge = kit.Trim;
            switch (kind)
            {
                case 1:
                {
                    // An over-robe, open down the front, from the shoulders to the hem or above it.
                    float layer = 0.03f;
                    float open = genes.Range("open", 0.28f, 0.55f);
                    float hem = MathF.Max(shape.Hem + genes.Range("over.hem", 0f, 0.6f), 0.6f);
                    float edgeWidth = genes.Range("edge", 0.05f, 0.09f);
                    int first = mesh.VertexCount;
                    mesh.Plate((u, v) =>
                    {
                        float angle = Front + open + (MathF.Tau - open * 2f) * u;
                        float y = hem + (3.94f - hem) * v;
                        return (shape.Point(y, angle, layer), shape.Normal(y, angle, layer));
                    }, 24, 12, 0.012f, (u, v) => u < edgeWidth || u > 1f - edgeWidth || v < 0.04f ? edge : cloth, PlateEdge.None, underside: kit.C.Lining);
                    mesh.SkinBy(at => at.Y < BodyForm.Hip + 0.15f ? BodyForm.Skirt(at) : (new Vector2((int)BoneType.Chest, 0f), new Vector2(1f, 0f)), first);
                    return layer + 0.012f;
                }
                case 2:
                    return Tabard(mesh, kit, shape, genes, cloth, edge);
                case 3:
                {
                    // A stole: two bands down the chest from the neck, their ends fringed.
                    float half = genes.Range("stole.half", 0.06f, 0.1f);
                    float end = MathF.Max(shape.Hem + 0.25f, genes.Range("stole.end", 1.2f, 2.2f));
                    foreach (float side in new[] { -1f, 1f })
                    {
                        var path = new List<Vector3>();
                        var normals = new List<Vector3>();
                        float angle = Front + side * genes.Range("stole.at", 0.28f, 0.42f);
                        for (int i = 0; i <= 12; i++)
                        {
                            float y = 3.95f - (3.95f - end) * i / 12f;
                            path.Add(shape.Point(y, angle, 0.014f));
                            normals.Add(shape.Normal(y, angle, 0f));
                        }
                        int first = mesh.VertexCount;
                        mesh.Strip(path, normals, half, 0.01f, edge);
                        mesh.SkinBy(at => at.Y < BodyForm.Hip + 0.15f ? BodyForm.Skirt(at) : (new Vector2((int)BoneType.Chest, 0f), new Vector2(1f, 0f)), first);
                    }
                    return 0.025f;
                }
                default:
                    return 0.012f;
            }
        }

        // A tabard or a surcoat, front and back, to below the belt, edged; over mail it's what the mail's worn under, so it sits out past the mail's own links.
        private static float Tabard(GearMesh mesh, ArmourKit kit, TorsoShape shape, Genes genes, VertexMaterial cloth, VertexMaterial edge)
        {
            float layer = 0.02f;
            float hem = MathF.Max(MathF.Min(shape.Hem + genes.Range("tabard.below", -0.1f, 0.4f), BodyForm.Hip - 0.25f), 0.7f);
            float half = genes.Range("tabard.half", 0.32f, 0.55f);
            foreach (float centre in new[] { Front, Back })
            {
                int first = mesh.VertexCount;
                mesh.Plate((u, v) =>
                {
                    float y = hem + (3.9f - hem) * v;
                    float angle = centre + (u - 0.5f) * 2f * half;
                    return (shape.Point(y, angle, layer), shape.Normal(y, angle, layer));
                }, 8, 12, 0.01f, (u, v) => u < 0.1f || u > 0.9f || v < 0.05f ? edge : cloth, PlateEdge.None, underside: kit.C.Lining);
                mesh.SkinBy(at => at.Y < BodyForm.Hip + 0.15f ? BodyForm.Skirt(at) : (new Vector2((int)BoneType.Chest, 0f), new Vector2(1f, 0f)), first);
            }
            return layer + 0.01f;
        }

        // A quilted coat closes down the front: a placket over the join, and toggles of horn or buttons of metal down it.
        private static void Closure(GearMesh mesh, ArmourKit kit, TorsoShape shape, Genes genes)
        {
            Placket(mesh, kit, shape, genes);
            int count = genes.Count("toggles", 6, 10);
            float low = MathF.Max(shape.Hem, BodyForm.Hip - 0.1f) + 0.1f, top = 3.8f;
            VertexMaterial toggle = genes.Chance("toggles.horn", 0.5f) ? kit.C.Horn : kit.Fitting;
            for (int i = 0; i < count; i++)
            {
                float y = low + (top - low) * i / (count - 1f);
                mesh.Rivet(shape.Point(y, Front, 0.02f), shape.Normal(y, Front, 0f), 0.026f, toggle);
            }
        }

        // Round plates on the breast and the back of a coat of scales, each a dish with a rim and a boss, as the steppe's riders wore them.
        private static void Mirrors(GearMesh mesh, ArmourKit kit, TorsoShape shape, Genes genes)
        {
            if (!genes.Chance("any", 0.45f)) return;
            float size = genes.Range("size", 0.12f, 0.18f);
            float y = genes.Range("at", 3.2f, 3.4f);
            bool back = genes.Chance("back", 0.6f);
            foreach (float centre in back ? new[] { Front, Back } : new[] { Front })
            {
                Vector3 at = shape.Point(y, centre, 0.016f);
                Vector3 n = shape.Normal(y, centre, 0.016f);
                mesh.At(GearMesh.FacingOut(at, n), () =>
                {
                    mesh.Lathe(Section.Round(18), 0.03f, 3, t => size * (t < 0.67f ? 1f : 0.88f), kit.C.Metal, capStart: false);
                    mesh.Lathe(Section.Round(10), 0.05f, 1, t => size * 0.22f * (1f - t * 0.6f), kit.Fitting, capStart: false);
                });
            }
        }

        // Sleeves: from the shoulder, as far as the piece's family takes them. On the forearm they're fitted, so that a glove's cuff goes over them, or belled out wide over it.
        private static void Sleeves(GearMesh mesh, ArmourKit kit, TorsoShape shape, Genes genes)
        {
            GearLook look = kit.Look;
            float reach = kit.Blend("reach", new Vector2(0.75f, 1f), new Vector2(0.28f, 0.75f), new Vector2(0.85f, 1f));
            if (kit.Likely("none", 0.02f, 0.2f, 0f)) return;
            bool bell = kit.Woven && look.Cloth > 0.5f && genes.Chance("bell", 0.55f);
            // Under hide, the sleeves are as often a shirt's, of cloth, as the jerkin's own.
            VertexMaterial material = kit.Make is GearMake.Plate or GearMake.Lamellar or GearMake.Hardened ? kit.Under
                : kit.Hide && genes.Chance("shirt", 0.4f) ? (genes.Chance("shirt.plain", 0.5f) ? kit.C.Lining : kit.C.Cloth)
                : kit.Make == GearMake.Studded || kit.Make == GearMake.Brigandine ? kit.C.Leather : kit.Shell;
            VertexMaterial cuff = kit.Make is GearMake.Mail or GearMake.Plate ? material : kit.Woven ? kit.Trim : kit.Edge;
            float gap = genes.Range("gap", 0.04f, 0.06f);
            foreach (float side in new[] { 1f, -1f })
            {
                int steps = 10;
                var rings = new List<Ring>();
                var cuffs = new List<bool>();
                var along = new float[steps + 1];
                for (int i = 0; i <= steps; i++) along[i] = (float)i / steps * reach;
                if (bell)
                {
                    foreach (float at in new[] { 0.5f, 0.6f })
                    {
                        if (at > reach - 0.03f) continue;
                        int nearest = 0;
                        for (int i = 1; i <= steps; i++) if (MathF.Abs(along[i] - at) < MathF.Abs(along[nearest] - at)) nearest = i;
                        along[nearest] = at;
                    }
                }
                for (int i = 0; i <= steps; i++)
                {
                    float t = along[i];
                    float r = BodyForm.Arm(t) + gap;
                    // Belled, it's all the way out before a glove's cuff can begin (GearFit.CuffReach); fitted, it stays inside one.
                    if (t > 0.5f) r = bell ? MathF.Max(r, BodyForm.Arm(t) + GearFit.SleeveBell * Math.Clamp((t - 0.5f) / 0.1f, 0f, 1f) + gap * (1f - Math.Clamp((t - 0.5f) / 0.1f, 0f, 1f))) : MathF.Min(r, BodyForm.Arm(t) + GearFit.SleeveFitted);
                    // From just outside the collar's space (GearFit.PauldronIn) out to the wrist.
                    float start = GearFit.PauldronIn + 0.02f;
                    rings.Add(new Ring { Centre = new Vector3(side * (start + t * (BodyForm.WristX - start)), BodyForm.Shoulder, 0f), Side = Vector3.UnitZ, Front = Vector3.UnitY, HalfWidth = r, HalfDepth = r });
                    cuffs.Add(i >= steps - 1);
                }
                int first = mesh.VertexCount;
                // Under a harness a sleeve's mostly hidden, and needs fewer sides.
                mesh.Bands(Section.Round(kit.Make == GearMake.Plate ? 10 : 12), rings, (ring, _) => cuffs[ring] ? cuff : material, capStart: true, capEnd: false);
                float s = side;
                mesh.SkinBy(at => Arm(at, s), first);
            }
            mesh.Covers |= BodyRegions.UpperArms;
            if (reach > 0.92f) mesh.Covers |= BodyRegions.LowerArms;

            // Plate at the elbow on a harness: a cop with a fan on its outside.
            if (kit.Make == GearMake.Plate && kit.Likely("couters", 0.85f, 0f, 0f))
            {
                float size = genes.Range("couter", 0.2f, 0.28f);
                foreach (float side in new[] { 1f, -1f })
                {
                    Vector3 elbow = new Vector3(side * BodyForm.ElbowX, BodyForm.Shoulder, 0f);
                    int first = mesh.VertexCount;
                    // Centred on the elbow, so it ends before a glove's cuff can begin.
                    mesh.At(Matrix4.CreateRotationZ(-side * MathF.PI / 2f) * Matrix4.CreateTranslation(elbow - Vector3.UnitX * (side * size * 0.45f)), () =>
                    {
                        mesh.Lathe(look.Angular > 0.5f ? Section.Polygon(10) : Section.Round(14), size * 0.9f, 4, t => (BodyForm.Arm(0.5f) + 0.07f) * (1f + 0.35f * MathF.Sin(t * MathF.PI)), kit.Shell, capStart: false, capEnd: false);
                    });
                    Vector3 fanAt = elbow + new Vector3(0f, 0f, -BodyForm.Arm(0.5f) - 0.09f);
                    mesh.At(GearMesh.Basis(fanAt, Vector3.UnitX * side, -Vector3.UnitZ, Vector3.UnitY), () => mesh.Lathe(Section.Round(12), 0.02f, 1, _ => MathF.Min(size * 0.6f, 0.12f), kit.Shell, squash: 0.8f));
                    float s = side;
                    mesh.SkinBy(at => Arm(at, s), first);
                }
            }
        }

        // A sleeve bends at the elbow, and what's either side of it goes with its own half.
        private static (Vector2, Vector2) Arm(Vector3 at, float side)
        {
            float blend = Math.Clamp((MathF.Abs(at.X) - BodyForm.ElbowX + 0.14f) / 0.28f, 0f, 1f);
            return (new Vector2((int)BodyForm.UpperArm(side), (int)BodyForm.LowerArm(side)), new Vector2(1f - blend, blend));
        }

        // What's on the shoulders: lames of plate, a guard of lamellar or scale hanging from them, layered leather, a mail cape, or a cloth mantle. Plates are skinned partly to the chest and
        // partly to the arm by where they are (GearFit.Shoulder), and none rises above a brim.
        private static void Shoulders(GearMesh mesh, ArmourKit kit, TorsoShape shape, Genes genes)
        {
            GearLook look = kit.Look;
            bool any = kit.Make switch
            {
                GearMake.Plate => true,
                GearMake.Lamellar or GearMake.Scale => genes.Chance("any", 0.8f),
                GearMake.Hardened => genes.Chance("any", 0.8f),
                GearMake.Mail or GearMake.Brigandine => genes.Chance("any", 0.5f),
                GearMake.Silk or GearMake.Cloth => genes.Chance("any", 0.45f),
                _ => genes.Chance("any", 0.35f),
            };
            if (!any) return;

            if (kit.Woven || kit.Make == GearMake.Hide && genes.Chance("mantle", 0.5f))
            {
                Mantle(mesh, kit, shape, genes.Part("mantle"));
                return;
            }

            float size = kit.Make == GearMake.Plate ? genes.Range("size", 0.3f, 0.46f) * (0.85f + 0.35f * look.Flare) * (look.Legendary ? 1.15f : 1f) : genes.Range("size", 0.26f, 0.36f);
            float length = size * genes.Range("length", 1.1f, 1.7f);
            int lames = kit.Make == GearMake.Plate ? genes.Count("lames", 2, 4) : kit.Make == GearMake.Hardened ? genes.Count("lames", 2, 3) : 1;
            float gap = genes.Range("gap", 0.08f, 0.12f);
            float rise = size * genes.Range("rise", 0f, 0.18f);
            // However big a shoulder's plate, its top stays below a brim (GearFit.ShoulderTop): what it can't have in height it has out to the sides.
            float height = MathF.Max(MathF.Min(size * 0.55f, GearFit.ShoulderTop - BodyForm.Shoulder - rise - BodyForm.Arm(0f) - gap - 0.03f), 0.04f);
            VertexMaterial shell = kit.Make == GearMake.Hardened ? kit.Shell : kit.Make is GearMake.Mail or GearMake.Brigandine ? kit.C.Metal : kit.Shell;
            VertexMaterial rim = kit.Make == GearMake.Hardened ? kit.Edge : kit.Edge;
            float thick = 0.02f;
            // A narrow lame needs fewer columns along the arm than a wide one: what a pauldron costs stays the same however many lames it's in.
            int across = Math.Max(2, 8 / lames);
            foreach (float side in new[] { 1f, -1f })
            {
                int first = mesh.VertexCount;
                float x0 = side * GearFit.PauldronIn;
                for (int lame = 0; lame < lames; lame++)
                {
                    // Each lame a band of the dome, out along the arm, the outer ones lapping over the inner.
                    float from = (float)lame / lames, to = (lame + 1.15f) / lames;
                    float out_ = gap + 0.008f * lame;
                    mesh.Plate((u, v) =>
                    {
                        float t = from + (to - from) * u;
                        float angle = MathF.PI * (0.06f + 0.88f * v);
                        float dome = MathF.Sqrt(MathF.Max(1f - MathF.Pow(1f - MathF.Min(t * 1.9f + 0.15f, 1f), 2f), 0.04f));
                        float arm = BodyForm.Arm(MathF.Min(t * 0.4f, 0.5f)) + out_;
                        float up = arm + height * dome, wide = arm + size * 0.6f * dome;
                        float x = x0 + side * t * length;
                        var p = new Vector3(x, BodyForm.Shoulder + rise * (1f - t) + MathF.Sin(angle) * up, MathF.Cos(angle) * wide);
                        var n = new Vector3(0f, MathF.Sin(angle) / up, MathF.Cos(angle) / wide).Normalized();
                        return (p, n);
                    }, across, 7, thick, (_, _) => shell, lame == lames - 1 ? PlateEdge.End : PlateEdge.None, thick * 1.3f, underside: kit.ShellDark, rollMaterial: kit.Gilt);
                }
                // A rivet at each end of each lame, where it's held to the next.
                for (int lame = 0; lame < lames && kit.Make == GearMake.Plate; lame++)
                {
                    float t = (lame + 0.6f) / lames;
                    float arm = BodyForm.Arm(MathF.Min(t * 0.4f, 0.5f)) + gap + 0.008f * lame + thick;
                    float up = arm + height, wide = arm + size * 0.6f;
                    foreach (float angle in new[] { MathF.PI * 0.12f, MathF.PI * 0.88f })
                    {
                        Vector3 at = new Vector3(x0 + side * t * length, BodyForm.Shoulder + rise * (1f - t) + MathF.Sin(angle) * up, MathF.Cos(angle) * wide);
                        mesh.Rivet(at, new Vector3(0f, MathF.Sin(angle), MathF.Cos(angle)), 0.015f, kit.Fitting);
                    }
                }
                // A stone set in the front of a legendary pauldron, low enough to stay under a brim.
                if (look.Legendary)
                {
                    float t = 0.4f, angle = MathF.PI * 0.25f;
                    float dome = MathF.Sqrt(MathF.Max(1f - MathF.Pow(1f - MathF.Min(t * 1.9f + 0.15f, 1f), 2f), 0.04f));
                    float arm = BodyForm.Arm(MathF.Min(t * 0.4f, 0.5f)) + gap + thick;
                    float up = arm + height * dome, wide = arm + size * 0.6f * dome;
                    var at = new Vector3(x0 + side * t * length, BodyForm.Shoulder + rise * (1f - t) + MathF.Sin(angle) * up, MathF.Cos(angle) * wide);
                    var n = new Vector3(0f, MathF.Sin(angle) / up, MathF.Cos(angle) / wide).Normalized();
                    mesh.At(GearMesh.FacingOut(at, n), () => GearKit.Setting(mesh, genes.Part("stone"), 0.035f, kit.C.Precious, look.Theme != GearTheme.None ? kit.C.Glow : kit.C.Gem));
                }
                float s = side;
                mesh.SkinBy(at => GearFit.Shoulder(at, s), first);
                if (kit.Make == GearMake.Plate && look.Spikes > 0.55f && genes.Chance("spike", look.Spikes - 0.4f))
                {
                    float x = x0 + side * length * 0.45f;
                    int spikeFirst = mesh.VertexCount;
                    float foot = BodyForm.Shoulder + rise * 0.5f + BodyForm.Arm(0.2f) + gap + height * 0.9f;
                    float reach = GearFit.ShoulderTop - foot;
                    if (reach > 0.05f) mesh.At(GearMesh.FacingOut(new Vector3(x, foot, 0f), new Vector3(side * 0.5f, 1f, 0f)), () => mesh.Lathe(Section.Polygon(5), reach * 0.9f, 2, t => size * 0.14f * (1f - t), kit.Fitting, capEnd: false));
                    mesh.SkinBy(at => GearFit.Shoulder(at, s), spikeFirst);
                }
            }
        }

        // A short cape over the shoulders, edged and often furred. Where it lies over the top of an arm it goes with the arm, as a shoulder's plate does, so that a hanging arm takes it down.
        private static void Mantle(GearMesh mesh, ArmourKit kit, TorsoShape shape, Genes genes)
        {
            float bottom = genes.Range("bottom", 3.55f, 3.72f);
            float layer = 0.045f;
            VertexMaterial cloth = kit.Woven ? kit.Shell : kit.C.Cloth;
            VertexMaterial edge = kit.Make == GearMake.Hide || genes.Chance("fur", 0.25f) ? kit.C.Fur : kit.Trim;
            int first = mesh.VertexCount;
            mesh.Plate((u, v) =>
            {
                float angle = u * MathF.Tau;
                float y = bottom + (3.96f - bottom) * v;
                Vector3 n = shape.Normal(y, angle, layer);
                return (shape.Point(y, angle, layer) + n * 0.03f * (1f - v), n);
            }, 28, 6, 0.012f, (_, v) => v < 0.18f ? edge : cloth, PlateEdge.None, wrap: true, underside: kit.C.Lining);
            mesh.SkinBy(at => MathF.Abs(at.X) > BodyForm.ShoulderX - 0.1f ? GearFit.Shoulder(at, MathF.Sign(at.X)) : (new Vector2((int)BoneType.Chest, 0f), new Vector2(1f, 0f)), first);
        }

        // Round the neck, inside its band (GearFit.CollarLow to CollarTop) and never closer to the neck than CollarInner, so a helm's neck fits inside it: a gorget's lames, a standing
        // collar, or a roll of fur.
        private static void Collar(GearMesh mesh, ArmourKit kit, TorsoShape shape, Genes genes, float outer)
        {
            if (!kit.Likely("any", 0.85f, 0.55f, 0.6f)) return;
            GearLook look = kit.Look;
            float inner = GearFit.CollarInner + 0.01f;
            float height = genes.Range("height", 0.12f, GearFit.CollarTop - GearFit.CollarLow - 0.02f);
            float low = GearFit.CollarTop - height - 0.01f;
            if (kit.Make == GearMake.Plate)
            {
                int lames = genes.Count("lames", 2, 3);
                float lame = height / lames;
                for (int i = 0; i < lames; i++)
                {
                    float y0 = low + lame * i, y1 = MathF.Min(y0 + lame * 1.2f, GearFit.CollarTop - 0.014f);
                    float r0 = inner + 0.06f + 0.05f * (lames - i), r1 = inner + 0.05f + 0.05f * (lames - i - 1);
                    mesh.Plate((u, v) =>
                    {
                        float angle = u * MathF.Tau;
                        float y = y0 + (y1 - y0) * v;
                        float r = MathF.Min(r0 + (r1 - r0) * v, GearFit.CollarOuter - 0.02f);
                        return (new Vector3(MathF.Cos(angle) * r, y, MathF.Sin(angle) * r * 0.95f), new Vector3(MathF.Cos(angle), 0.35f, MathF.Sin(angle)).Normalized());
                    }, 20, 1, 0.016f, (_, _) => kit.Shell, i == lames - 1 ? PlateEdge.Top : PlateEdge.None, 0.018f, wrap: true, underside: kit.ShellDark, rollMaterial: kit.Gilt);
                }
                return;
            }
            VertexMaterial material = kit.Hide && genes.Chance("fur", 0.35f) ? kit.C.Fur : kit.Make is GearMake.Mail or GearMake.Scale ? kit.C.Mail : kit.Woven ? kit.Shell : kit.Edge;
            bool fur = GearMesh.Same(material, kit.C.Fur);
            float swell = fur ? genes.Range("fur.swell", 0.06f, 0.1f) : 0.015f;
            var rings = new Ring[5];
            for (int i = 0; i < 5; i++)
            {
                float t = i / 4f;
                float r = inner + 0.04f + swell * MathF.Sin(t * MathF.PI) + (fur ? 0.03f : 0.02f) * (1f - t);
                rings[i] = Ring.Flat(low + height * t, MathF.Min(r, GearFit.CollarOuter), MathF.Min(r, GearFit.CollarOuter) * 0.95f, i == 4);
            }
            mesh.Loft(Section.Round(Around), rings, material, capStart: false, capEnd: false);
        }

        // A belt round the waist over whatever's outermost there, buckled at the front, with pouches on the better kept; or a sash for a robe, its ends hanging.
        private static void Belt(GearMesh mesh, ArmourKit kit, TorsoShape shape, Genes genes, float outer)
        {
            if (!kit.Likely("any", 0.75f, 0.92f, 0.8f)) return;
            GearLook look = kit.Look;
            float y = shape.Waist - genes.Range("drop", 0.12f, 0.36f);
            float layer = outer + 0.006f;
            if (kit.Woven && genes.Chance("sash", 0.6f))
            {
                float half = genes.Range("sash.half", 0.08f, 0.14f);
                Vector2 size = shape.Size(y, layer);
                ArmourBand(mesh, new Vector3(0f, y, shape.Bulge(y) * 0.5f), size.X, size.Y + shape.Bulge(y) * 0.5f, half, 0.012f, kit.C.Trim);
                float tail = genes.Range("sash.tail", 0.5f, 1.1f);
                float angle = Front + genes.Range("sash.at", -0.6f, 0.6f);
                var path = new List<Vector3>();
                var normals = new List<Vector3>();
                for (int i = 0; i <= 8; i++)
                {
                    float yy = y - tail * i / 8f;
                    path.Add(shape.Point(yy, angle, layer + 0.018f));
                    normals.Add(shape.Normal(yy, angle, layer));
                }
                int first = mesh.VertexCount;
                mesh.Strip(path, normals, half * 0.7f, 0.01f, kit.C.Trim);
                mesh.SkinBy(BodyForm.Skirt, first);
                return;
            }
            float halfWidth = genes.Range("width", 0.035f, 0.06f) * (kit.Metal ? 1.2f : 1f);
            kit.Belt(mesh, genes, angle => shape.Point(y, angle, layer), angle => shape.Normal(y, angle, layer), halfWidth, 0.012f, Front);

            int pouches = genes.Count("pouches", 0, kit.Hide ? 3 : 2);
            for (int i = 0; i < pouches; i++)
            {
                float angle = Front + (i % 2 == 0 ? -1f : 1f) * genes.Range($"pouch{i}", 0.7f, 1.3f);
                float w = genes.Range($"pouch{i}.w", 0.1f, 0.16f), h = genes.Range($"pouch{i}.h", 0.12f, 0.18f);
                Vector3 at = shape.Point(y - halfWidth, angle, layer + 0.012f + w * 0.45f);
                Vector3 n = shape.Normal(y, angle, layer);
                Vector3 side = Vector3.Cross(Vector3.UnitY, n).Normalized();
                int first = mesh.VertexCount;
                mesh.At(GearMesh.Basis(at - Vector3.UnitY * h, side, Vector3.UnitY, n), () =>
                {
                    mesh.Block(w, h, w * 0.45f, 0.4f, kit.C.Leather);
                    mesh.At(Matrix4.CreateTranslation(0f, h * 0.55f, w * 0.42f), () => mesh.Block(w * 1.04f, h * 0.48f, 0.012f, 0.5f, kit.C.LeatherDark));
                });
                mesh.SkinBy(at2 => (new Vector2((int)BoneType.Pelvis, 0f), new Vector2(1f, 0f)), first);
            }
        }

        // What hangs from the body: a cape from behind the collar, a baldric across, a hood lying down the back, or a beast's teeth on a cord, for the trophy-takers.
        private static void Hanging(GearMesh mesh, ArmourKit kit, TorsoShape shape, Genes genes, float outer)
        {
            GearLook look = kit.Look;
            if (look.Legendary && genes.Chance("legend.cape", 0.6f) || kit.Likely("cape", 0.12f + look.Ornament * 0.25f, 0.06f, 0.2f))
            {
                float bottom = MathF.Max(genes.Range("cape.bottom", 1.35f, 2.2f), 1.3f);
                float wide = genes.Range("cape.wide", 0.55f, 0.8f);
                int first = mesh.VertexCount;
                mesh.Plate((u, v) =>
                {
                    float y = bottom + (3.92f - bottom) * v;
                    float angle = Back + (u - 0.5f) * 2f * wide * (0.75f + 0.25f * (1f - v));
                    Vector3 n = shape.Normal(y, angle, outer);
                    float fall = 0.06f + 0.24f * (1f - v) * (1f - v);
                    return (shape.Point(y, angle, outer + 0.03f) + n * fall, n);
                }, 10, 12, 0.012f, (u, v) => v < 0.05f || u < 0.04f || u > 0.96f ? kit.C.Trim : kit.C.Cloth, PlateEdge.None, underside: kit.C.Lining);
                mesh.SkinBy(at => at.Y < BodyForm.Hip + 0.15f ? BodyForm.Skirt(at) : (new Vector2((int)BoneType.Chest, 0f), new Vector2(1f, 0f)), first);
            }
            else if (kit.Likely("hood", 0.05f, 0.3f, 0.3f))
            {
                // A hood thrown back, lying on the shoulders behind the neck.
                Vector3 at = shape.Point(3.7f, Back, outer + 0.04f);
                mesh.At(GearMesh.Basis(at, Vector3.UnitX, Vector3.UnitY, -Vector3.UnitZ), () =>
                {
                    mesh.Lathe(Section.Round(14), 0.36f, 5, t => 0.26f * MathF.Sin(MathF.Max(t, 0.06f) * MathF.PI * 0.85f + 0.2f), kit.Woven ? kit.Shell : kit.C.Cloth, squash: 0.45f);
                });
            }

            if (kit.Likely("baldric", 0.15f, 0.35f, 0.08f))
            {
                float way = genes.Sign("baldric.way");
                var path = new List<Vector3>();
                var normals = new List<Vector3>();
                for (int i = 0; i <= 14; i++)
                {
                    float t = i / 14f;
                    float y = 3.85f - t * 1.45f;
                    float angle = Front + way * (1.0f - 2f * t);
                    path.Add(shape.Point(y, angle, outer + 0.012f));
                    normals.Add(shape.Normal(y, angle, outer));
                }
                mesh.Strip(path, normals, genes.Range("baldric.half", 0.035f, 0.055f), 0.01f, kit.Strap);
            }

            if (look.Motif == Motif.Fang && kit.Hide && genes.Chance("teeth", 0.6f) || look.Legendary && kit.Hide)
            {
                int teeth = genes.Count("teeth.count", 3, 7);
                for (int i = 0; i < teeth; i++)
                {
                    float angle = Front + (i - (teeth - 1) * 0.5f) * 0.16f;
                    float y = 3.62f - MathF.Abs(i - (teeth - 1) * 0.5f) * 0.04f;
                    GearKit.Trophy(mesh, genes.Part($"tooth{i}"), shape.Point(y, angle, outer + 0.02f), genes.Range($"t{i}", 0.12f, 0.2f), kit.C.Bone, kit.Strap);
                }
            }
        }

        // A boss at the middle of the chest, set with a stone, gilded and scrolled on a legendary piece.
        private static void Medallion(GearMesh mesh, ArmourKit kit, TorsoShape shape, Genes genes, float outer)
        {
            GearLook look = kit.Look;
            float y = genes.Range("at", 3.3f, 3.5f);
            Vector3 at = shape.Point(y, Front, outer);
            Vector3 n = shape.Normal(y, Front, outer);
            float size = genes.Range("size", 0.07f, 0.11f) * (look.Legendary ? 1.25f : 1f);
            mesh.At(GearMesh.FacingOut(at, n), () =>
            {
                mesh.Lathe(look.Angular > 0.5f ? Section.Polygon(genes.Count("sides", 6, 8)) : Section.Round(16), size * 0.3f, 2, t => size * (1.3f - 0.2f * t), look.Legendary ? kit.C.Precious : kit.Fitting);
                mesh.At(Matrix4.CreateTranslation(0f, size * 0.25f, 0f), () => GearKit.Setting(mesh, genes.Part("stone"), size * 0.6f, kit.C.Precious, look.Theme != GearTheme.None || look.Legendary ? kit.C.Glow : kit.C.Gem));
            });
            if (!look.Legendary) return;
            foreach (float side in new[] { -1f, 1f })
            {
                Vector3 from = shape.Point(y, Front + side * 0.12f, outer + 0.004f);
                GearKit.Scroll(mesh, from, new Vector3(side, 0f, 0f), Vector3.UnitY, size * 1.1f, 1.5f, 0.01f, kit.C.Precious);
            }
        }
    }
}
