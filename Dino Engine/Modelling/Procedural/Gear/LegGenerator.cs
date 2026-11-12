using Dino_Engine.ECS.Components;
using Dino_Engine.Modelling.Model;
using OpenTK.Mathematics;

namespace Dino_Engine.Modelling.Procedural.Gear
{
    /// <summary>
    /// What's worn on the legs: a seat round the hips under the body's piece, and down each leg its covering, close below the knee so that a boot goes over it (<see cref="GearFit.LegsUnder"/>). Over
    /// that, what its making lays on: plate on the thighs with a cop at the knee, rows of lamellar, a panel of scales or of a brigandine's rivets, hardened leather guards, studs, a quilted knee,
    /// laced hide, or a robe's wrapped shins. Everything on a leg is laid over its covering exactly as that's drawn (<see cref="Cover"/>), and bends at the knee, what's either side of it going with
    /// its own half. See GEAR.md.
    /// </summary>
    public static class LegGenerator
    {
        private const int Around = 16;
        private const float Front = MathF.PI / 2f;

        /// <summary>
        /// One leg's covering, which everything else on that leg is laid over: where its surface is at a height, at an angle round the leg (0 out to the body's left, a quarter turn its front)
        /// and a distance out from it. Its rings are drawn from this and at every height where its fit changes, so what lies on it lies on what's drawn.
        /// </summary>
        private sealed class Cover
        {
            public const float Top = BodyForm.Hip + 0.05f;
            public const float Gather = BodyForm.Knee - 0.15f;      // Where a loose leg has been gathered in to, below the knee.
            public const float Close = GearFit.BootTop + 0.04f;     // Below this it's no further out than a boot's inside allows.

            public readonly float Side;
            private readonly float _inner, _loose;

            public Cover(float side, float inner, float loose)
            {
                Side = side;
                _inner = inner;
                _loose = loose;
            }

            /// <summary>How far its surface is from the leg's middle at a height: loose or close at the thigh, gathered in just below the knee, always close where a boot goes over it.</summary>
            public float Radius(float y)
            {
                float leg = BodyForm.Leg(MathF.Min(y, BodyForm.Hip));
                float drape = _loose * MathF.Sin(Math.Clamp((Top - y) / (Top - BodyForm.Knee), 0f, 1f) * MathF.PI * 0.8f) * Math.Clamp((y - Gather) / (BodyForm.Knee - Gather), 0f, 1f);
                float r = leg + _inner + drape;
                return y < Close ? MathF.Min(r, leg + GearFit.LegsUnder) : r;
            }

            /// <summary>Where the leg's middle is across: straight down from the hip, as the body's legs are and a boot is.</summary>
            public float AxisX(float y) => Side * BodyForm.HipX;

            /// <summary>How much deeper than wide it is: a thigh is, a shin isn't.</summary>
            public static float Depth(float y) => 1f + 0.04f * Math.Clamp((y - BodyForm.Knee) / 0.15f, 0f, 1f);

            public Vector3 At(float y, float angle, float out_)
            {
                float r = Radius(y) + out_;
                return new Vector3(AxisX(y) + MathF.Cos(angle) * r, y, MathF.Sin(angle) * r * Depth(y));
            }

            /// <summary>Where something is <paramref name="fromLeg"/> off the bare leg, rather than off the covering.</summary>
            public Vector3 OffLeg(float y, float angle, float fromLeg)
            {
                float r = BodyForm.Leg(MathF.Min(y, BodyForm.Hip)) + fromLeg;
                return new Vector3(AxisX(y) + MathF.Cos(angle) * r, y, MathF.Sin(angle) * r * Depth(y));
            }

            public static Vector3 Out(float angle) => new Vector3(MathF.Cos(angle), 0f, MathF.Sin(angle));

            /// <summary>
            /// The angles from one side of something laid round the front of this leg to the other: as far round the outside as asked, and round the inside only as far as keeps it on this leg's
            /// side of the body's middle at <paramref name="radius"/> from the leg's middle, as the legs stand close and what's on one would otherwise go through what's on the other.
            /// </summary>
            public (float From, float To) Round(float outside, float inside, float radius)
            {
                inside = MathF.Min(inside, MathF.Asin(MathF.Min((BodyForm.HipX - 0.05f) / radius, 1f)));
                return Side > 0f ? (Front - outside, Front + inside) : (Front - inside, Front + outside);
            }

            /// <summary>
            /// Keeps a surface built since <paramref name="from"/> on this leg's side of the body's middle, by at least <paramref name="clear"/>, drawn in flat where it would cross (GearFit.SideCover).
            /// Only for a surface with no thickness, which flattening can't fold onto itself.
            /// </summary>
            public void KeepToSide(GearMesh mesh, int from, float clear) => mesh.Reshape(p => OnSide(p, clear), from);

            /// <summary>A point kept on this leg's side of the body's middle, by at least <paramref name="clear"/>.</summary>
            public Vector3 OnSide(Vector3 p, float clear) => p.X * Side < clear ? new Vector3(clear * Side, p.Y, p.Z) : p;

            /// <summary>What's either side of the knee goes with its own half, and across it with both.</summary>
            public (Vector2, Vector2) Skin(Vector3 at)
            {
                float blend = Math.Clamp((BodyForm.Knee + 0.14f - at.Y) / 0.28f, 0f, 1f);
                return (new Vector2((int)BodyForm.UpperLeg(Side), (int)BodyForm.LowerLeg(Side)), new Vector2(1f - blend, blend));
            }

            public (Vector2, Vector2) Shin(Vector3 at) => (new Vector2((int)BodyForm.LowerLeg(Side), 0f), new Vector2(1f, 0f));
        }

        public static void Build(GearMesh mesh, GearLook look)
        {
            var kit = new ArmourKit(look);
            Genes genes = look.Genes;

            float inner = GearFit.LegsInner + genes.Range("inner", 0f, 0.012f);
            float loose = kit.Blend("loose", new Vector2(0f, 0.02f), new Vector2(0f, 0.035f), new Vector2(0.02f, 0.07f));
            // Under plate and lamellar the covering is close, as what's laid over it has to clear it and stay inside what a skirt clears.
            if (kit.Make is GearMake.Plate or GearMake.Lamellar) loose = MathF.Min(loose, 0.03f);
            // The hem stops above where a shoe's top comes, so that it hangs over a shoe and goes into a boot.
            float ankle = kit.Blend("ankle", new Vector2(0.3f, 0.36f), new Vector2(0.3f, 0.4f), new Vector2(0.3f, 0.38f));
            // A legendary leg's hem is a broad band of gold or silver.
            float hemHeight = look.Legendary ? genes.Range("hem", 0.07f, 0.11f) : kit.Woven ? genes.Range("hem", 0.04f, 0.08f) : genes.Range("hem", 0.025f, 0.045f);
            bool wrapped = kit.Woven && genes.Chance("wrapped", 0.5f) || kit.Make == GearMake.Hide && genes.Chance("laced", 0.3f);
            // Hide trousers are often cloth ones with leather sewn over the knees, as leather's dear and cloth wears through there first.
            bool reinforced = kit.Make == GearMake.Hide && genes.Chance("reinforced", 0.45f);
            VertexMaterial cover = kit.Make is GearMake.Plate ? kit.Under : kit.Make is GearMake.Hardened or GearMake.Studded ? kit.C.Leather : reinforced ? (genes.Chance("reinforced.plain", 0.5f) ? kit.C.Lining : kit.C.Cloth) : kit.Shell;
            // Which thigh a pouch is strapped to, if either.
            float pouchSide = genes.Chance("pouch", 0.25f) ? genes.Sign("pouch.side") : 0f;
            // Straps crossed up the shins over hide and cloth, as walkers bind their legs when they don't wrap them.
            bool garters = !wrapped && (kit.Make == GearMake.Hide && !reinforced || kit.Make is GearMake.Cloth or GearMake.Wraps) && genes.Chance("garters", 0.35f);
            // Rolled up at the ankle into a cuff, now and then.
            bool rolled = !look.Legendary && genes.Chance("rolled", 0.35f);
            VertexMaterial hem = kit.Woven ? kit.Trim : kit.Edge;

            // Its rings: evenly down the leg, and wherever the covering's fit changes, so that it bends where it should and what lies on it follows it. The last band is the hem.
            var heights = new List<float>();
            const int Steps = 12;
            for (int i = 0; i <= Steps; i++) heights.Add(Cover.Top + (ankle + hemHeight - Cover.Top) * i / Steps);
            heights.AddRange(new[] { BodyForm.Knee, Cover.Gather, Cover.Close });
            heights.Sort((a, b) => b.CompareTo(a));
            var ys = new List<float>();
            foreach (float y in heights)
            {
                if (y >= ankle + hemHeight - 0.001f && (ys.Count == 0 || ys[^1] - y > 0.03f)) ys.Add(y);
            }
            if (ys[^1] > ankle + hemHeight + 0.001f) ys.Add(ankle + hemHeight);
            ys.Add(ankle);

            foreach (float side in new[] { 1f, -1f })
            {
                var leg = new Cover(side, inner, loose);
                var rings = new List<Ring>(ys.Count);
                foreach (float y in ys)
                {
                    float r = leg.Radius(y);
                    rings.Add(new Ring { Centre = new Vector3(leg.AxisX(y), y, 0f), Side = Vector3.UnitX, Front = Vector3.UnitZ, HalfWidth = r, HalfDepth = r * Cover.Depth(y) });
                }
                int first = mesh.VertexCount;
                mesh.Counted("legs", () => mesh.Bands(Section.Round(Around), rings, (ring, _) => ring == rings.Count - 2 ? hem : cover, capStart: false, capEnd: false));
                if (rolled)
                {
                    mesh.Mark("cuff");
                    // The cuff: a band rolled over itself at the hem, standing out a little, inside a boot.
                    float low = ankle, high = ankle + hemHeight + genes.Range("cuff", 0.03f, 0.06f);
                    Ring[] cuff =
                    {
                        new Ring { Centre = new Vector3(leg.AxisX(low), low, 0f), Side = Vector3.UnitX, Front = Vector3.UnitZ, HalfWidth = leg.Radius(low) + 0.006f, HalfDepth = leg.Radius(low) + 0.006f, Hard = true },
                        new Ring { Centre = new Vector3(leg.AxisX(low), (low + high) * 0.5f, 0f), Side = Vector3.UnitX, Front = Vector3.UnitZ, HalfWidth = leg.Radius(low) + 0.016f, HalfDepth = leg.Radius(low) + 0.016f },
                        new Ring { Centre = new Vector3(leg.AxisX(low), high, 0f), Side = Vector3.UnitX, Front = Vector3.UnitZ, HalfWidth = leg.Radius(high) + 0.006f, HalfDepth = leg.Radius(high) + 0.006f, Hard = true },
                    };
                    mesh.Loft(Section.Round(Around), cuff, cover, capStart: false, capEnd: false);
                }
                mesh.SkinBy(leg.Skin, first);
                // Between the legs the two coverings would cross, so each is drawn in flat a little short of the middle (GearFit.SideCover).
                leg.KeepToSide(mesh, first, GearFit.SideCover);

                if (wrapped) mesh.Counted("wraps", () => Wraps(mesh, kit, genes.Part("wraps"), leg, ankle));
                else if (garters) mesh.Counted("garters", () => Garters(mesh, kit, genes.Part("garters"), leg, ankle));
                // What's on the thigh and what's on the knee, as a strap can only go where nothing else is.
                bool thigh = false, knee = false;
                mesh.Counted(kit.Make.ToString().ToLowerInvariant(), () =>
                {
                    switch (kit.Make)
                    {
                        case GearMake.Plate:
                            Cuisse(mesh, kit, genes.Part("cuisse"), leg);
                            Poleyn(mesh, kit, genes.Part("poleyn"), leg, kit.Shell, true);
                            thigh = knee = true;
                            break;
                        case GearMake.Mail:
                            knee = genes.Chance("poleyn", 0.55f);
                            if (knee) Poleyn(mesh, kit, genes.Part("poleyn"), leg, genes.Chance("leather.cop", 0.5f) ? kit.C.Leather : kit.C.Metal, false);
                            break;
                        case GearMake.Lamellar:
                            Rows(mesh, kit, genes.Part("rows"), leg);
                            thigh = true;
                            break;
                        case GearMake.Scale:
                        case GearMake.Brigandine:
                            Panel(mesh, kit, genes.Part("panel"), leg);
                            thigh = true;
                            break;
                        case GearMake.Hardened:
                            Guard(mesh, kit, genes.Part("guard"), leg);
                            // A cop of the same hide, or of the guard's boiled darker one.
                            bool darkCop = genes.Chance("dark.cop", 0.5f);
                            if (darkCop) mesh.Mark("dark cop");
                            Poleyn(mesh, kit, genes.Part("poleyn"), leg, darkCop ? kit.ShellDark : kit.Shell, false);
                            thigh = knee = true;
                            break;
                        case GearMake.Studded:
                            if (genes.Chance("studded.panel", 0.5f)) Panel(mesh, kit, genes.Part("panel"), leg, kit.C.LeatherContrast, true);
                            else StudRow(mesh, kit, genes.Part("studs"), leg);
                            thigh = true;
                            break;
                        case GearMake.Padded:
                            KneePad(mesh, kit, genes.Part("knee"), leg, true);
                            knee = true;
                            break;
                        case GearMake.Hide:
                            if (reinforced)
                            {
                                KneePad(mesh, kit, genes.Part("patch"), leg, false, kit.C.Leather);
                                knee = true;
                            }
                            else thigh = SideLacing(mesh, kit, genes.Part("lacing"), leg);
                            break;
                        case GearMake.Cloth:
                            knee = genes.Chance("patch", 0.12f + look.Wear * 0.5f);
                            if (knee) KneePad(mesh, kit, genes.Part("patch"), leg, false);
                            break;
                    }
                });
                // A pouch strapped to the outside of one thigh, where nothing else is: it's that thigh's strap.
                if (!thigh && side == pouchSide && kit.Make is not (GearMake.Plate or GearMake.Lamellar or GearMake.Scale or GearMake.Brigandine))
                {
                    int pouchFirst = mesh.VertexCount;
                    mesh.Counted("pouch", () => Pouch(mesh, kit, genes.Part("pouch"), leg));
                    mesh.SkinBy(leg.Skin, pouchFirst);
                    thigh = true;
                }

                // Piping down a free outside seam, a cord tied round the ankle over the hem, and a worn pair's patches.
                if (!thigh && kit.Make is not (GearMake.Plate or GearMake.Lamellar or GearMake.Scale or GearMake.Brigandine) && genes.Chance("piping", 0.3f))
                {
                    mesh.Mark("piping");
                    int pipeFirst = mesh.VertexCount;
                    float outside = side > 0f ? 0f : MathF.PI;
                    var path = new List<Vector3>();
                    var normals = new List<Vector3>();
                    for (int i = 0; i <= 10; i++)
                    {
                        float y = BodyForm.Hip - 0.04f + (ankle + hemHeight + 0.02f - BodyForm.Hip + 0.04f) * i / 10f;
                        path.Add(leg.At(y, outside, 0.002f));
                        normals.Add(Cover.Out(outside));
                    }
                    mesh.Strip(path, normals, 0.012f, 0.004f, kit.Woven ? kit.Trim : kit.C.LeatherContrast);
                    mesh.SkinBy(leg.Skin, pipeFirst);
                }
                if (!rolled && genes.Chance("ankle.tie", 0.25f))
                {
                    mesh.Mark("tie");
                    int tieFirst = mesh.VertexCount;
                    float y = ankle + hemHeight + genes.Range("tie.at", 0.04f, 0.1f);
                    mesh.At(Matrix4.CreateTranslation(leg.AxisX(y), y, 0f), () => GearKit.Twist(mesh, leg.Radius(y) + 0.008f, 0.007f, 9, kit.Strap));
                    leg.KeepToSide(mesh, tieFirst, GearFit.SideWraps);
                    mesh.SkinBy(leg.Shin, tieFirst);
                }
                if (look.Wear > 0.35f && !knee && genes.Chance("patched", 0.5f))
                {
                    mesh.Mark("patched");
                    KneePad(mesh, kit, genes.Part("worn"), leg, false, kit.Hide ? kit.C.LeatherContrast : kit.C.ClothDark);
                    knee = true;
                }

                // A gilt rosette at the front of a legendary knee that has nothing else over it.
                if (look.Legendary && !knee)
                {
                    int roseFirst = mesh.VertexCount;
                    Vector3 at = leg.At(BodyForm.Knee + 0.06f, Front, 0.005f);
                    mesh.At(GearMesh.FacingOut(at, Cover.Out(Front)), () => mesh.Lathe(Section.Round(12), 0.006f, 1, _ => 0.06f, kit.C.Precious, capStart: false));
                    mesh.SkinBy(leg.Skin, roseFirst);
                }

                // A strap round the thigh where nothing else is, laid along the covering's flattened inside and standing out from it towards the middle.
                if (!thigh && kit.Likely("strap", 0.45f, 0.55f, 0.1f))
                {
                    float y = genes.Range("strap.at", 1.3f, 1.75f);
                    int strapFirst = mesh.VertexCount;
                    mesh.Counted("strap", () => kit.Belt(mesh, genes.Part("strap"), a => leg.OnSide(leg.At(y, a, 0.006f), GearFit.SideCover - 0.004f), Cover.Out, 0.025f, 0.008f, side > 0f ? 0.2f : MathF.PI - 0.2f, buckled: genes.Chance("strap.buckle", 0.6f), steps: Around));
                    mesh.SkinBy(leg.Skin, strapFirst);
                }
            }

            // The seat, round the hips, under the body's piece (GearFit.WaistUnder).
            mesh.Counted("seat", () => Seat(mesh, kit, genes.Part("seat"), cover, inner));
            mesh.Covers |= BodyRegions.UpperLegs | BodyRegions.LowerLegs;
        }

        // Round the hips from the top of the thighs to the waist, inside what the body's piece is allowed to sit over, its top a waistband.
        private static void Seat(GearMesh mesh, ArmourKit kit, Genes genes, VertexMaterial cover, float inner)
        {
            float top = GearFit.LegsTop - genes.Range("below", 0f, 0.08f);
            var rings = new List<Ring>();
            foreach (float y in new[] { 1.78f, 1.92f, 2.05f, 2.2f, top - 0.04f, top })
            {
                float r = MathF.Min(BodyForm.Torso(MathF.Max(y, 1.9f)) + inner + 0.02f, BodyForm.Torso(MathF.Max(y, 1.9f)) + GearFit.WaistUnder);
                float hips = Math.Clamp((2f - y) / 0.22f, 0f, 1f);
                float wide = MathF.Max(r, (BodyForm.HipX + BodyForm.Leg(BodyForm.Hip) + inner) * hips + r * (1f - hips));
                rings.Add(new Ring { Centre = new Vector3(0f, y, 0f), Side = Vector3.UnitX, Front = Vector3.UnitZ, HalfWidth = wide, HalfDepth = MathF.Max(r * BodyForm.Depth, (BodyForm.Leg(BodyForm.Hip) + inner) * hips + r * BodyForm.Depth * (1f - hips)) });
            }
            mesh.Bone = (int)BoneType.Pelvis;
            mesh.Bands(Section.Round(20), rings, (ring, _) => ring >= rings.Count - 2 ? kit.Strap : cover, capStart: false, capEnd: false);
            mesh.Bone = (int)BoneType.Chest;
        }

        // Plates over the front of the thigh, a lame or two at their top, strapped round behind. Off the bare leg by a gap and always clear of the covering, and inside what a skirt clears.
        private static void Cuisse(GearMesh mesh, ArmourKit kit, Genes genes, Cover leg)
        {
            mesh.Mark("cuisse");
            float top = genes.Range("top", 1.78f, 1.88f), low = GearFit.KneeHigh - 0.04f;
            float span = genes.Range("span", 1.2f, 1.6f);
            float thick = 0.02f;
            float gap = genes.Range("gap", 0.07f, 0.095f);
            float Off(float y) => MathF.Max(gap, leg.Radius(y) - BodyForm.Leg(MathF.Min(y, BodyForm.Hip)) + 0.015f);
            (float a0, float a1) = leg.Round(span, span, BodyForm.Leg(BodyForm.Hip) + 0.15f);
            int first = mesh.VertexCount;
            mesh.Plate((u, v) =>
            {
                float angle = a0 + (a1 - a0) * u;
                float y = low + (top - low) * v;
                return (leg.OffLeg(y, angle, Off(y)), Cover.Out(angle));
            }, 8, 5, thick, (_, _) => kit.Shell, PlateEdge.Top | PlateEdge.Start | PlateEdge.End, thick * 1.2f, underside: kit.ShellDark, rollMaterial: kit.Gilt);
            // The lames lap upwards to just below the hip, where the body's piece takes over.
            int lames = genes.Count("lames", 1, 2);
            for (int i = 0; i < lames; i++)
            {
                float y0 = top - 0.02f + i * 0.055f, y1 = y0 + 0.07f;
                mesh.Plate((u, v) =>
                {
                    float angle = Front + (a0 - Front) * 0.9f + (a1 - a0) * 0.9f * u;
                    float y = y0 + (y1 - y0) * v;
                    return (leg.OffLeg(y, angle, Off(y) + 0.008f * (i + 1)), Cover.Out(angle));
                }, 8, 1, thick, (_, _) => kit.Shell, PlateEdge.Top, thick, underside: kit.ShellDark, rollMaterial: kit.Gilt);
            }
            // Over the plate in front, and down onto the covering behind it, a little past the plate's sides.
            float strapY = (top + low) * 0.5f;
            float over = Off(strapY) + thick + 0.004f;
            float Radius(float a)
            {
                float at = MathF.IEEERemainder(a - Front, MathF.Tau);
                float past = MathF.Max(MathF.Max(a0 - Front - at, at - (a1 - Front)), 0f);
                float onPlate = Math.Clamp(1f - past / 0.3f, 0f, 1f);
                onPlate = onPlate * onPlate * (3f - 2f * onPlate);
                float behind = leg.Radius(strapY) - BodyForm.Leg(strapY) + 0.006f;
                return behind + (over - behind) * onPlate;
            }
            kit.Belt(mesh, genes.Part("strap"), a => leg.OnSide(leg.OffLeg(strapY, a, Radius(a)), GearFit.SideCover - 0.004f), Cover.Out, 0.024f, 0.008f, leg.Side > 0f ? 0.3f : MathF.PI - 0.3f, steps: Around);
            mesh.SkinBy(leg.Skin, first);
        }

        // A cop over the knee, lapping down over a boot's top. Its inside is just clear of the covering; below a boot's top it flares out past where any boot comes (GearFit.BootTopOuter), and its
        // cop comes out no further than a long skirt clears (GearFit.LegsShell). On a harness a fan guards the outside of the knee.
        private static void Poleyn(GearMesh mesh, ArmourKit kit, Genes genes, Cover leg, VertexMaterial material, bool harness)
        {
            mesh.Mark(harness ? "poleyn" : "cop");
            GearLook look = kit.Look;
            float size = genes.Range("size", 0.2f, 0.28f) * (0.9f + 0.2f * look.Heft);
            float thick = 0.016f;
            float low = GearFit.KneeLow + thick * 1.5f, high = GearFit.KneeHigh;
            float Base(float y)
            {
                float over = leg.Radius(y) - BodyForm.Leg(y) + 0.012f;
                float flare = Math.Clamp((GearFit.BootTop + 0.06f - y) / 0.06f, 0f, 1f);
                flare = flare * flare * (3f - 2f * flare);
                return over + (MathF.Max(over, GearFit.BootTopOuter + 0.006f + thick * 0.55f) - over) * flare;
            }
            (float a0, float a1) = leg.Round(1.15f, 1.15f, BodyForm.Leg(GearFit.KneeHigh) + GearFit.LegsShell);
            int first = mesh.VertexCount;
            mesh.Plate((u, v) =>
            {
                float angle = a0 + (a1 - a0) * u;
                float y = low + (high - low) * v;
                float room = MathF.Max(GearFit.LegsShell - thick * 1.5f - Base(y), 0f);
                float cop = MathF.Sin(v * MathF.PI) * MathF.Min(size * 0.35f, room) * MathF.Pow(MathF.Cos((u - 0.5f) * 2.2f), 2f);
                return (leg.OffLeg(y, angle, Base(y) + cop), Cover.Out(angle));
            }, 8, 5, thick, (_, _) => material, PlateEdge.Top | PlateEdge.Bottom, thick, underside: kit.ShellDark, rollMaterial: kit.Gilt);
            if (harness && genes.Chance("fan", 0.7f))
            {
                float knee = BodyForm.Knee + 0.06f;
                float outside = leg.Side > 0f ? 0f : MathF.PI;
                float fan = MathF.Min(size * genes.Range("fan.size", 0.45f, 0.65f), (knee - GearFit.BootTop - 0.02f) / 1.2f);
                Vector3 at = leg.OffLeg(knee, outside, Base(knee) + thick) + Vector3.UnitZ * 0.02f;
                mesh.At(GearMesh.Basis(at, Vector3.UnitZ, Vector3.UnitX * leg.Side, Vector3.UnitY), () => mesh.Lathe(look.Angular > 0.5f ? Section.Polygon(8) : Section.Round(14), 0.018f, 1, _ => fan, kit.Gilt ?? material, squash: 1.2f));
            }
            if (look.Legendary)
            {
                // A gilt rosette on the cop's brow, flat, inside what a skirt clears.
                float y = low + (high - low) * 0.82f;
                float room = MathF.Max(GearFit.LegsShell - thick * 1.5f - Base(y), 0f);
                float cop = MathF.Sin(0.82f * MathF.PI) * MathF.Min(size * 0.35f, room);
                Vector3 at = leg.OffLeg(y, Front, Base(y) + cop + thick);
                mesh.At(GearMesh.FacingOut(at, Cover.Out(Front)), () => mesh.Lathe(Section.Round(12), 0.006f, 1, _ => size * 0.22f, kit.C.Precious, capStart: false));
            }
            mesh.SkinBy(leg.Shin, first);
        }

        // Hardened leather down the outside of the thigh, its edges welted.
        private static void Guard(GearMesh mesh, ArmourKit kit, Genes genes, Cover leg)
        {
            mesh.Mark("guard");
            // Boiling darkens leather, so a guard is a darker hide than the trousers it's on, or the same one gone dark.
            VertexMaterial boiled = genes.Chance("contrast", 0.6f) ? kit.C.LeatherContrast : kit.ShellDark;
            float top = genes.Range("top", 1.8f, 1.95f), low = genes.Range("low", 1.2f, 1.35f);
            // Down the outside of the thigh, or over its front as a cuisse is, kept to its own side of the body's middle.
            bool front = genes.Chance("front", 0.35f);
            if (front) mesh.Mark("front guard");
            float centre = front ? Front + leg.Side * -genes.Range("front.at", 0.1f, 0.35f) : leg.Side > 0f ? genes.Range("at", 0.1f, 0.6f) : MathF.PI - genes.Range("at", 0.1f, 0.6f);
            float span = genes.Range("span", 0.7f, 1.1f);
            // Its edges welted in a strap's hide, or rolled in its own.
            bool welted = genes.Chance("welted", 0.5f);
            if (welted) mesh.Mark("welted");
            VertexMaterial? edge = kit.Gilt ?? (welted ? kit.Strap : null);
            int first = mesh.VertexCount;
            if (genes.Chance("splints", 0.4f))
            {
                // Splints: narrow strips side by side, each riveted top and bottom.
                int splints = genes.Count("count", 3, 5);
                mesh.Mark($"splints {splints}");
                float each = span * 2f / splints;
                for (int k = 0; k < splints; k++)
                {
                    float a0 = centre - span + each * k + each * 0.08f, a1 = a0 + each * 0.84f;
                    mesh.Plate((u, v) =>
                    {
                        float angle = a0 + (a1 - a0) * u;
                        return (leg.At(low + (top - low) * v, angle, 0.012f), Cover.Out(angle));
                    }, 2, 5, 0.02f, (_, _) => boiled, PlateEdge.Top | PlateEdge.Bottom, 0.012f, underside: kit.ShellDark, rollMaterial: edge);
                    foreach (float y in new[] { low + 0.05f, top - 0.05f }) mesh.Rivet(leg.At(y, (a0 + a1) * 0.5f, 0.032f), Cover.Out((a0 + a1) * 0.5f), 0.014f, kit.Fitting);
                }
            }
            else
            {
                mesh.Plate((u, v) =>
                {
                    float angle = centre - span + span * 2f * u;
                    return (leg.At(low + (top - low) * v, angle, 0.012f), Cover.Out(angle));
                }, 6, 5, 0.024f, (_, _) => boiled, PlateEdge.All, 0.02f, underside: kit.ShellDark, rollMaterial: edge);
            }
            mesh.SkinBy(leg.Skin, first);
        }

        // Studs in a row down the outside of the leg.
        private static void StudRow(GearMesh mesh, ArmourKit kit, Genes genes, Cover leg)
        {
            mesh.Mark("studs");
            int count = genes.Count("count", 5, 9);
            int rows = genes.Count("rows", 1, 2);
            mesh.Mark($"stud rows {rows} of {count}");
            int first = mesh.VertexCount;
            for (int r = 0; r < rows; r++)
            {
                float angle = leg.Side > 0f ? 0.15f + r * 0.4f : MathF.PI - 0.15f - r * 0.4f;
                for (int i = 0; i < count; i++)
                {
                    float y = 1.85f - (1.85f - 1.15f) * (i + r * 0.5f) / (count - 0.5f);
                    mesh.Rivet(leg.At(y, angle, 0.002f), Cover.Out(angle), 0.022f, kit.Fitting);
                }
            }
            mesh.SkinBy(leg.Skin, first);
        }

        // Rows of small plates down the front of the thigh, each lapping the one below, laced at its top in the lacing's colour, as a lamellar coat's skirt carries on down a leg.
        private static void Rows(GearMesh mesh, ArmourKit kit, Genes genes, Cover leg)
        {
            mesh.Mark("rows");
            int rows = genes.Count("rows", 3, 5);
            float top = genes.Range("top", 1.85f, 1.95f);
            float row = (top - GearFit.KneeHigh) / rows;
            float span = genes.Range("span", 0.9f, 1.25f);
            (float a0, float a1) = leg.Round(span, span, BodyForm.Leg(BodyForm.Hip) + 0.1f);
            int first = mesh.VertexCount;
            for (int r = 0; r < rows; r++)
            {
                float rowTop = top - r * row, rowLow = rowTop - row * 1.15f;
                float out_ = 0.012f + 0.004f * (rows - r);
                mesh.Plate((u, v) =>
                {
                    float angle = a0 + (a1 - a0) * u;
                    return (leg.At(rowLow + (rowTop - rowLow) * v, angle, out_), Cover.Out(angle));
                }, 6, 1, 0.012f, (_, v) => v > 0.82f ? kit.Trim : kit.Shell, PlateEdge.None, underside: kit.ShellDark, rollMaterial: kit.Gilt);
            }
            mesh.SkinBy(leg.Skin, first);
        }

        // A panel down the front of the thigh, of scales or of a brigandine's riveted cloth, edged in leather.
        private static void Panel(GearMesh mesh, ArmourKit kit, Genes genes, Cover leg, VertexMaterial? face = null, bool studded = false)
        {
            // Edged in leather, or not.
            bool bordered = !studded || genes.Chance("border", 0.5f);
            if (bordered) mesh.Mark("border");
            mesh.Mark(studded ? "studded panel" : "panel");
            float top = genes.Range("top", 1.82f, 1.95f), low = GearFit.KneeHigh + genes.Range("low", -0.02f, 0.08f);
            float span = genes.Range("span", 0.85f, 1.2f);
            (float a0, float a1) = leg.Round(span, span, BodyForm.Leg(BodyForm.Hip) + 0.1f);
            int first = mesh.VertexCount;
            mesh.Plate((u, v) =>
            {
                float angle = a0 + (a1 - a0) * u;
                return (leg.At(low + (top - low) * v, angle, 0.012f), Cover.Out(angle));
            }, 6, 4, 0.014f, (u, v) => bordered && (v < 0.08f || u < 0.06f || u > 0.94f) ? kit.Edge : face ?? kit.Shell, PlateEdge.None, underside: kit.ShellDark, rollMaterial: kit.Gilt);
            if (kit.Make == GearMake.Brigandine || studded)
            {
                int across = genes.Count("across", 3, 4), down = genes.Count("down", 3, 5);
                mesh.Mark($"grid {across}x{down}");
                for (int i = 0; i < across; i++)
                {
                    for (int j = 0; j < down; j++)
                    {
                        float angle = a0 + (a1 - a0) * (0.12f + 0.76f * i / (across - 1f));
                        float y = low + 0.06f + (top - low - 0.12f) * j / (down - 1f);
                        mesh.Rivet(leg.At(y, angle, 0.026f), Cover.Out(angle), studded ? 0.022f : 0.017f, kit.Fitting);
                    }
                }
            }
            mesh.SkinBy(leg.Skin, first);
        }

        // A pad over the knee: quilted and thick on a padded leg, a patch sewn on over cloth, worn or put there to stop it wearing.
        private static void KneePad(GearMesh mesh, ArmourKit kit, Genes genes, Cover leg, bool padded, VertexMaterial? patch = null)
        {
            // A patch is square, round, or long down onto the shin; a quilted pad is square.
            int shape = padded ? 0 : genes.Pick("shape", 1.5f, 1f, 1f);
            mesh.Mark(padded ? "knee pad" : $"patch {shape}");
            float half = genes.Range("half", 0.12f, 0.18f), span = genes.Range("span", 0.7f, 1f);
            float thick = padded ? genes.Range("thick", 0.025f, 0.04f) : 0.006f;
            VertexMaterial material = padded ? kit.Shell : patch ?? (genes.Chance("leather", 0.5f) ? kit.C.Leather : kit.C.ClothDark);
            float low = padded ? MathF.Max(BodyForm.Knee - half, GearFit.BootTop + 0.02f) : BodyForm.Knee - half * (shape == 2 ? 2.4f : 1f), high = BodyForm.Knee + half;
            (float a0, float a1) = leg.Round(span, span, BodyForm.Leg(high) + 0.1f);
            int first = mesh.VertexCount;
            mesh.Plate((u, v) =>
            {
                float across = shape == 1 ? MathF.Sqrt(MathF.Max(1f - (2f * v - 1f) * (2f * v - 1f), 0.08f)) : 1f;
                float angle = (a0 + a1) * 0.5f + (a1 - a0) * (u - 0.5f) * across;
                return (leg.At(low + (high - low) * v, angle, 0.004f), Cover.Out(angle));
            }, 6, shape == 1 ? 6 : 4, thick, (_, _) => material, PlateEdge.None, underside: kit.ShellDark, rollMaterial: kit.Gilt);
            mesh.SkinBy(leg.Skin, first);
        }

        // Hide laced up the outside of each thigh: a welt down the seam and thongs crossing over it. Whether there was any.
        private static bool SideLacing(GearMesh mesh, ArmourKit kit, Genes genes, Cover leg)
        {
            if (!genes.Chance("any", 0.6f)) return false;
            mesh.Mark("lacing");
            float top = 1.92f, low = genes.Range("low", 1.2f, 1.5f);
            float angle = leg.Side > 0f ? 0f : MathF.PI;
            int first = mesh.VertexCount;
            var path = new List<Vector3>();
            var normals = new List<Vector3>();
            for (int i = 0; i <= 6; i++)
            {
                path.Add(leg.At(top + (low - top) * i / 6f, angle, 0.002f));
                normals.Add(Cover.Out(angle));
            }
            mesh.Strip(path, normals, 0.02f, 0.006f, kit.Edge);
            int crosses = genes.Count("crosses", 3, 6);
            mesh.Mark($"crosses {crosses}");
            // Thongs of the strap's hide or of the second one.
            bool pale = genes.Chance("thongs", 0.5f);
            if (pale) mesh.Mark("pale thongs");
            VertexMaterial thong = pale ? kit.C.LeatherContrast : kit.Strap;
            float step = (top - low - 0.06f) / crosses;
            for (int i = 0; i < crosses; i++)
            {
                float y = top - 0.03f - i * step;
                foreach (float way in new[] { -1f, 1f })
                {
                    Vector3 a = leg.At(y, angle + way * 0.09f, 0.012f), b = leg.At(y - step, angle - way * 0.09f, 0.012f);
                    mesh.Sweep(Section.Round(4), new[] { a, b }, _ => new Vector2(0.007f), thong, Cover.Out(angle));
                }
            }
            mesh.SkinBy(leg.Skin, first);
            return true;
        }

        // A pouch on the outside of the thigh, its flap buckled shut, strapped round the thigh.
        private static void Pouch(GearMesh mesh, ArmourKit kit, Genes genes, Cover leg)
        {
            float y = genes.Range("at", 1.45f, 1.65f);
            float outside = leg.Side > 0f ? 0f : MathF.PI;
            float w = genes.Range("w", 0.09f, 0.13f), h = genes.Range("h", 0.12f, 0.17f), d = genes.Range("d", 0.035f, 0.05f);
            // As deep as it can be and stay inside what a skirt clears, flap and buckle and all.
            d = MathF.Min(d, MathF.Max((GearFit.LegsShell - (leg.Radius(y) - BodyForm.Leg(y)) - 0.03f) * 0.5f, 0.012f));
            VertexMaterial hide = genes.Chance("contrast", 0.5f) ? kit.C.LeatherContrast : kit.C.Leather;
            Vector3 out_ = Cover.Out(outside);
            Vector3 at = leg.At(y - h * 0.5f, outside, 0.004f + d);
            mesh.At(GearMesh.Basis(at, Vector3.UnitZ * -leg.Side, Vector3.UnitY, out_), () =>
            {
                mesh.Block(w, h, d, 0.45f, hide);
                mesh.At(Matrix4.CreateTranslation(0f, h * 0.55f, d * 0.95f), () => mesh.Block(w * 1.05f, h * 0.47f, 0.01f, 0.5f, kit.C.LeatherDark));
                mesh.At(GearMesh.Basis(new Vector3(0f, h * 0.62f, d * 1.1f), Vector3.UnitX, Vector3.UnitY, Vector3.UnitZ), () => mesh.Buckle(w * 0.5f, w * 0.45f, 0.004f, kit.Fitting));
            });
            float strapY = y + h * 0.3f;
            kit.Belt(mesh, genes.Part("strap"), a => leg.OnSide(leg.At(strapY, a, 0.006f), GearFit.SideCover - 0.004f), Cover.Out, 0.016f, 0.006f, leg.Side > 0f ? 0.5f : MathF.PI - 0.5f, buckled: false, steps: Around);
        }

        // Two straps crossed round and round the shin from the ankle to below the knee, one going each way, the one over the other a hair proud of it.
        private static void Garters(GearMesh mesh, ArmourKit kit, Genes genes, Cover leg, float ankle)
        {
            float top = MathF.Min(genes.Range("top", 0.62f, 0.82f), Cover.Gather);
            float turns = genes.Range("turns", 1.6f, 2.6f);
            float start = genes.Range("start", 0f, MathF.Tau);
            VertexMaterial strap = genes.Chance("cloth", 0.4f) ? kit.C.Lining : kit.Strap;
            int first = mesh.VertexCount;
            foreach (float way in new[] { 1f, -1f })
            {
                var path = new List<Vector3>();
                var normals = new List<Vector3>();
                for (int i = 0; i <= 28; i++)
                {
                    float t = i / 28f;
                    float y = ankle + 0.02f + (top - ankle - 0.02f) * t;
                    float a = start + way * turns * MathF.Tau * t;
                    path.Add(leg.At(y, a, way > 0f ? 0.006f : 0.011f));
                    normals.Add(Cover.Out(a));
                }
                mesh.Strip(path, normals, 0.012f, 0.004f, strap);
            }
            mesh.SkinBy(leg.Shin, first);
        }

        // Strips wound round the shin from the ankle to below the knee, as walkers and robed folk bind their legs: the wraps stand out a little and lap as they go, and stay inside a boot.
        private static void Wraps(GearMesh mesh, ArmourKit kit, Genes genes, Cover leg, float ankle)
        {
            float top = MathF.Min(genes.Range("top", 0.72f, 0.9f), Cover.Gather);
            VertexMaterial wrap = kit.Hide ? kit.Strap : genes.Chance("pale", 0.5f) ? kit.C.Lining : kit.C.ClothDark;
            float centre = leg.AxisX((ankle + top) * 0.5f);
            int first = mesh.VertexCount;
            mesh.At(Matrix4.CreateTranslation(centre, 0f, 0f), () =>
                mesh.Wrap(ankle, top, t =>
                {
                    float y = ankle + (top - ankle) * t;
                    return leg.Radius(y) + 0.006f + MathF.Abs(leg.AxisX(y) - centre);
                }, genes.Range("pitch", 0.08f, 0.12f), 0.06f, wrap, sides: 10));
            leg.KeepToSide(mesh, first, GearFit.SideWraps);
            mesh.SkinBy(leg.Shin, first);
        }
    }
}
