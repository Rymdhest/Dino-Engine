using Dino_Engine.ECS.Components;
using Dino_Engine.Modelling.Model;
using OpenTK.Mathematics;

namespace Dino_Engine.Modelling.Procedural.Gear
{
    /// <summary>
    /// What's worn on the body, which is most of what a hero looks like from up where the camera is. One shell from hem to neck, offset from <see cref="BodyForm"/>, then whatever goes on it: sleeves,
    /// shoulders, a belt, straps, a collar, a mantle. There's no list of armours: a plate harness and a silk robe are this same code with different shares.
    /// </summary>
    public static class TorsoGenerator
    {
        private const int Around = 24;

        public static void Build(GearMesh mesh, GearStyle style)
        {
            var cut = new ArmourCut(style);
            Genes genes = style.Genes;
            GearForm form = style.Spec.Form;

            float gap = cut.Blend("gap", new Vector2(0.11f, 0.17f), new Vector2(0.05f, 0.09f), new Vector2(0.07f, 0.12f));
            float chest = cut.Blend("chest", new Vector2(0.07f, 0.2f), new Vector2(0f, 0.05f), new Vector2(0f, 0.03f));
            float cinch = cut.Blend("cinch", new Vector2(0f, 0.05f), new Vector2(0f, 0.09f), new Vector2(0.02f, 0.12f));
            float waist = genes.Range("waist.at", 2.5f, 2.85f);

            // How far down it goes. Robes reach the ankle, and how far a coat goes is what Cover says.
            float cover = form.Cover;
            float hem = cut.Blend("hem", new Vector2(1.72f, 1.98f), new Vector2(2.05f - cover * 0.85f, 1.9f - cover * 0.55f), new Vector2(1.7f - cover * 1.45f, 1.45f - cover * 1.05f));
            float flare = cut.Blend("flare", new Vector2(0.02f, 0.16f), new Vector2(0.03f, 0.2f), new Vector2(0.14f, 0.5f));
            bool skirted = hem < BodyForm.Hip - 0.1f;

            float Half(float y)
            {
                float belly = 1f - Math.Clamp(MathF.Abs(y - waist) / 0.5f, 0f, 1f);
                float below = y < BodyForm.Hip ? (BodyForm.Hip - y) / MathF.Max(BodyForm.Hip - hem, 0.1f) : 0f;
                return BodyForm.Torso(MathF.Max(y, 1.9f)) + gap - cinch * belly + flare * below * below;
            }

            // A body is flat front to back, but what hangs below the hips goes round the legs, and they aren't.
            float DepthAt(float y)
            {
                float below = Math.Clamp((BodyForm.Hip + 0.1f - y) / 0.7f, 0f, 1f);
                return BodyForm.Depth + (1f - BodyForm.Depth) * below;
            }

            // Plates overlap as they go down, so each is a step out from the one above.
            int lames = cut.Likely("lames", 0.85f, 0.1f, 0f) ? genes.Count("lames.count", 2, 6) : 0;
            float lamesTop = genes.Range("lames.top", 2.75f, 3.1f);
            float lamesStep = (lamesTop - MathF.Max(hem, BodyForm.Hip - 0.2f)) / MathF.Max(lames, 1);

            var heights = new List<(float y, bool hard)>();
            for (float y = hem; y < BodyForm.Hip - 0.02f; y += MathF.Max((BodyForm.Hip - hem) / 7f, 0.08f)) heights.Add((y, false));
            if (lames > 0)
            {
                for (int i = 0; i <= lames; i++) heights.Add((lamesTop - lamesStep * (lames - i), true));
                heights.Add((3.25f, false));
            }
            else
            {
                for (float y = BodyForm.Hip; y < 3.3f; y += 0.16f) heights.Add((y, false));
            }
            heights.Add((3.45f, false));
            heights.Add((3.62f, false));
            heights.Add((3.78f, false));
            heights.Add((3.9f, false));
            heights.Add((BodyForm.Neck - 0.06f, false));
            heights.Sort((a, b) => a.y.CompareTo(b.y));

            var rings = new List<Ring>(heights.Count);
            foreach ((float y, bool hard) in heights)
            {
                float shoulderIn = Math.Clamp((y - 3.82f) / (BodyForm.Neck - 3.82f), 0f, 1f);
                float r = Half(y) * (1f - shoulderIn * 0.62f);
                float bulge = chest * (1f - MathF.Pow(Math.Clamp((y - 3.25f) / 0.8f, -1f, 1f), 2f));
                float step = hard ? genes.Range("lame.step", 0.015f, 0.04f) : 0f;
                rings.Add(new Ring
                {
                    Centre = new Vector3(0f, y, bulge * 0.5f),
                    Side = Vector3.UnitX,
                    Front = Vector3.UnitZ,
                    HalfWidth = r + step,
                    HalfDepth = r * DepthAt(y) + bulge * 0.5f + step,
                    Hard = hard,
                });
            }

            // What it's divided into. A front panel, bands at the hem, a skirt of something else.
            float panel = cut.Likely("panel", 0.1f, 0.3f, 0.75f) ? genes.Range("panel.width", 0.5f, 1.3f) : 0f;
            int hemBands = genes.Count("hem.bands", 1, 2 + (int)(style.Ornate * 2f));
            bool mailSkirt = cut.Heavy > 0.5f;
            var cloth = new VertexMaterial[rings.Count];

            mesh.Bone = (int)BoneType.Chest;
            int first = mesh.VertexCount;
            mesh.Loft(Section.Round(Around), rings, (ring, point) =>
            {
                float y = rings[ring].Centre.Y;
                if (ring < hemBands) return cut.Second;
                if (y < BodyForm.Hip - 0.05f && mailSkirt) return cut.Under;
                if (y < lamesTop && lames > 0 && ring % 2 == 1) return cut.Second;

                float front = MathF.Abs(point * (MathF.Tau / Around) - MathF.PI / 2f);
                return panel > 0f && front < panel * 0.4f ? cut.Second : cut.Main;
            }, capStart: false, capEnd: false);
            if (skirted) mesh.SkinBy(BodyForm.Skirt, first);

            mesh.Covers |= BodyRegions.Torso;
            Vector3 Surface(float y, float angle, float extra = 0f)
            {
                float bulge = chest * (1f - MathF.Pow(Math.Clamp((y - 3.25f) / 0.8f, -1f, 1f), 2f));
                float r = Half(y);
                return new Vector3(MathF.Cos(angle) * (r + extra), y, bulge * 0.5f + MathF.Sin(angle) * (r * DepthAt(y) + bulge * 0.5f + extra));
            }

            Sleeves(mesh, cut, genes.Part("sleeves"), gap);
            Shoulders(mesh, cut, genes.Part("shoulders"), gap);
            Collar(mesh, cut, genes.Part("collar"), gap);
            Belt(mesh, cut, genes.Part("belt"), Half, waist, chest);
            Fixings(mesh, cut, genes.Part("fixings"), Surface, waist);
            Hanging(mesh, cut, genes.Part("hanging"), Half, gap, waist, hem);

            if (style.Shines("stone"))
            {
                float size = genes.Range("stone.size", 0.07f, 0.13f);
                Vector3 at = Surface(3.45f, MathF.PI / 2f, 0.03f);
                mesh.At(Matrix4.CreateRotationX(MathF.PI / 2f) * Matrix4.CreateTranslation(at), () => mesh.Gem(genes.Count("stone.sides", 5, 9), size, size * 0.7f, size * 0.3f, 0.5f, style.Palette.Glow));
            }
        }

        private static void Sleeves(GearMesh mesh, ArmourCut cut, Genes genes, float gap)
        {
            float reach = cut.Blend("reach", new Vector2(0.45f, 1f), new Vector2(0.3f, 0.7f), new Vector2(0.8f, 1f));
            if (cut.Likely("none", 0.05f, 0.25f, 0f)) return;

            float sleeveGap = gap * genes.Range("gap", 0.45f, 0.75f);
            float bell = cut.Blend("bell", new Vector2(0f, 0.04f), new Vector2(0f, 0.08f), new Vector2(0.1f, 0.42f));
            VertexMaterial material = cut.Heavy > 0.5f ? cut.Under : cut.Main;
            VertexMaterial cuff = cut.Second;

            foreach (float side in new[] { 1f, -1f })
            {
                int steps = 10;
                var rings = new Ring[steps + 1];
                for (int i = 0; i <= steps; i++)
                {
                    float t = (float)i / steps * reach;
                    float r = BodyForm.Arm(t) + sleeveGap + bell * t * t;
                    rings[i] = new Ring { Centre = new Vector3(side * (BodyForm.ShoulderX - 0.12f + t * (BodyForm.ArmLength + 0.12f)), BodyForm.Shoulder, 0f), Side = Vector3.UnitZ, Front = Vector3.UnitY, HalfWidth = r, HalfDepth = r };
                }

                int first = mesh.VertexCount;
                mesh.Loft(Section.Round(10), rings, (ring, _) => ring >= steps - 1 ? cuff : material, capStart: false, capEnd: false);
                float s = side;
                mesh.SkinBy(at => Arm(at, s), first);
            }

            mesh.Covers |= BodyRegions.UpperArms;
            if (reach > 0.92f) mesh.Covers |= BodyRegions.LowerArms;
        }

        // A sleeve bends at the elbow, and what's either side of it goes with its own half.
        private static (Vector2, Vector2) Arm(Vector3 at, float side)
        {
            float blend = Math.Clamp((MathF.Abs(at.X) - BodyForm.ElbowX + 0.14f) / 0.28f, 0f, 1f);
            return (new Vector2((int)BodyForm.UpperArm(side), (int)BodyForm.LowerArm(side)), new Vector2(1f - blend, blend));
        }

        private static void Shoulders(GearMesh mesh, ArmourCut cut, Genes genes, float gap)
        {
            if (!cut.Likely("any", 0.92f, 0.34f, 0.14f)) return;

            GearStyle style = cut.Style;
            float size = cut.Blend("size", new Vector2(0.3f, 0.52f), new Vector2(0.24f, 0.34f), new Vector2(0.24f, 0.32f)) * (0.85f + 0.4f * style.Flare);
            float length = size * genes.Range("length", 1f, 1.8f);
            int lames = cut.Heavy > 0.4f ? genes.Count("lames", 1, 4) : 1;
            float rise = size * genes.Range("rise", 0.05f, 0.4f);
            bool spiked = genes.Chance("spiked", style.Spikes * cut.Heavy);
            VertexMaterial shell = cut.Heavy > 0.3f ? cut.Plate : cut.Second;
            VertexMaterial trim = cut.Fitting;
            Section section = style.Angular > 0.55f ? Section.Polygon(genes.Count("facets", 7, 11)) : Section.Round(12);

            foreach (float side in new[] { 1f, -1f })
            {
                mesh.Bone = (int)BodyForm.UpperArm(side);
                float x = side * (BodyForm.ShoulderX + 0.02f);

                for (int lame = 0; lame < lames; lame++)
                {
                    float start = (float)lame / lames;
                    float end = (lame + 1f) / lames;
                    int steps = 6;
                    var rings = new Ring[steps + 1];
                    for (int i = 0; i <= steps; i++)
                    {
                        float t = start + (end - start) * i / steps;

                        // A dome over the point of the shoulder, opening out along the arm.
                        float dome = MathF.Sqrt(MathF.Max(1f - MathF.Pow(1f - MathF.Min(t * 2.2f, 1f), 2f), 0.02f));
                        float r = size * dome * (1f + 0.1f * (end - t) * lames);
                        rings[i] = new Ring { Centre = new Vector3(x + side * (t - 0.12f) * length, BodyForm.Shoulder + rise * (1f - t), 0f), Side = Vector3.UnitZ, Front = Vector3.UnitY, HalfWidth = r, HalfDepth = r * 0.92f, Hard = i == steps };
                    }
                    mesh.Loft(section, rings, (ring, _) => ring == steps ? trim : shell, capStart: lame == 0, capEnd: false);
                }

                if (spiked)
                {
                    int spikes = genes.Count("spikes", 1, 4);
                    float reach = size * genes.Range("spike.reach", 0.5f, 1.2f);
                    for (int i = 0; i < spikes; i++)
                    {
                        float at = (i + 0.5f) / spikes * 0.7f;
                        Vector3 normal = new Vector3(side * 0.3f, 1f, genes.Range("spike.lean", -0.3f, 0.3f)).Normalized();
                        Vector3 place = new Vector3(x + side * (at - 0.12f) * length, BodyForm.Shoulder + rise * (1f - at) + size * 0.86f, 0f);
                        mesh.At(ArmourCut.FacingOut(place, normal), () => mesh.Lathe(Section.Polygon(genes.Count("spike.sides", 3, 6)), reach, 2, t => size * 0.2f * (1f - t), cut.Fitting, capEnd: false));
                    }
                }
            }
            mesh.Bone = (int)BoneType.Chest;
        }

        private static void Collar(GearMesh mesh, ArmourCut cut, Genes genes, float gap)
        {
            if (!cut.Likely("any", 0.8f, 0.4f, 0.65f)) return;

            float height = cut.Blend("height", new Vector2(0.14f, 0.32f), new Vector2(0.07f, 0.18f), new Vector2(0.1f, 0.42f));
            float width = BodyForm.Torso(BodyForm.Neck) + genes.Range("width", 0.06f, 0.13f);
            float open = genes.Range("open", 0f, 0.26f);
            VertexMaterial material = cut.Heavy > 0.5f ? cut.Plate : cut.Second;
            float y = BodyForm.Neck - 0.1f;

            Ring[] rings =
            {
                Ring.Flat(y, width + 0.1f, (width + 0.1f) * 0.92f),
                Ring.Flat(y + height * 0.5f, width, width * 0.92f),
                Ring.Flat(y + height, width + open, (width + open) * 0.92f, true),
            };
            mesh.Bone = (int)BoneType.Chest;
            mesh.Loft(Section.Round(16), rings, material, capStart: false, capEnd: false);
        }

        private static void Belt(GearMesh mesh, ArmourCut cut, Genes genes, Func<float, float> half, float waist, float chest)
        {
            if (!cut.Likely("any", 0.55f, 0.9f, 0.7f)) return;

            float at = waist - genes.Range("drop", 0.12f, 0.42f);
            float r = half(at);
            float height = cut.Blend("height", new Vector2(0.07f, 0.11f), new Vector2(0.05f, 0.1f), new Vector2(0.08f, 0.16f));
            VertexMaterial material = cut.Cloth > 0.5f ? cut.Second : cut.Strap;
            Vector3 centre = new Vector3(0f, at, 0f);
            mesh.Bone = (int)BoneType.Chest;
            ArmourCut.Band(mesh, centre, r, r * BodyForm.Depth, height, 0.018f, material);

            if (cut.Cloth < 0.7f) cut.Buckle(mesh, genes, new Vector3(0f, at, r * BodyForm.Depth + 0.012f), height * genes.Range("buckle", 0.9f, 1.5f));
            if (!cut.Likely("tail", 0.1f, 0.25f, 0.8f)) return;

            // The end of a sash, hanging down.
            float x = r * genes.Range("tail.at", -0.5f, 0.5f);
            float length = genes.Range("tail.length", 0.5f, 1.3f);
            float wide = genes.Range("tail.wide", 0.05f, 0.11f);
            var keys = new[] { new Vector3(x, at, r * BodyForm.Depth + 0.02f), new Vector3(x * 1.05f, at - length * 0.5f, r * BodyForm.Depth + 0.06f), new Vector3(x * 1.1f, at - length, r * BodyForm.Depth + 0.08f) };
            int first = mesh.VertexCount;
            mesh.Sweep(Section.Slab(0.3f), Spine.Through(keys, 6), t => new Vector2(wide, 0.012f), material, Vector3.UnitZ);
            mesh.SkinBy(BodyForm.Skirt, first);
        }

        private static void Fixings(GearMesh mesh, ArmourCut cut, Genes genes, Func<float, float, float, Vector3> surface, float waist)
        {
            GearStyle style = cut.Style;
            mesh.Bone = (int)BoneType.Chest;

            // Rows of rivets or studs round the chest.
            if (cut.Likely("studs", 0.6f, 0.65f, 0.05f))
            {
                int rows = genes.Count("studs.rows", 1, 2 + (int)(style.Ornate * 3f));
                int each = genes.Count("studs.each", 6, 13);
                float size = genes.Range("studs.size", 0.022f, 0.044f);
                float top = genes.Range("studs.top", 3.3f, 3.6f);
                for (int row = 0; row < rows; row++)
                {
                    float y = top - row * genes.Range("studs.gap", 0.14f, 0.26f);
                    for (int i = 0; i < each; i++)
                    {
                        float angle = MathF.PI * ((i + 0.5f) / each);
                        Vector3 at = surface(y, angle, 0f);
                        cut.Stud(mesh, at, new Vector3(MathF.Cos(angle), 0f, MathF.Sin(angle)), size, cut.Fitting);
                    }
                }
            }

            // Straps across the body, or crossing over it.
            int straps = cut.Likely("straps", 0.2f, 0.75f, 0.1f) ? genes.Count("straps.count", 1, 2) : 0;
            float strapWide = genes.Range("straps.wide", 0.04f, 0.075f);
            for (int s = 0; s < straps; s++)
            {
                float way = s == 0 ? genes.Sign("straps.way") : -genes.Sign("straps.way");
                var path = new List<Vector3>();
                for (int i = 0; i <= 10; i++)
                {
                    float t = i / 10f;
                    float y = 3.86f - t * 1.5f;
                    float angle = MathF.PI / 2f + way * (0.95f - 1.9f * t);
                    path.Add(surface(y, angle, 0.022f));
                }
                mesh.Sweep(Section.Slab(0.3f), path, _ => new Vector2(strapWide, 0.012f), cut.Strap, Vector3.UnitZ);
            }

            // Lacing up the front.
            if (cut.Likely("laces", 0.02f, 0.35f, 0.22f))
            {
                int crosses = genes.Count("laces.count", 3, 7);
                float spread = genes.Range("laces.spread", 0.1f, 0.2f);
                float top = 3.72f;
                float step = genes.Range("laces.step", 0.09f, 0.15f);
                for (int i = 0; i < crosses; i++)
                {
                    float y = top - i * step;
                    foreach (float way in new[] { -1f, 1f })
                    {
                        var path = new[] { surface(y, MathF.PI / 2f + way * spread, 0.016f), surface(y - step, MathF.PI / 2f - way * spread, 0.016f) };
                        mesh.Sweep(Section.Round(4), path, _ => new Vector2(0.011f), cut.Second, Vector3.UnitZ);
                    }
                }
            }
        }

        private static void Hanging(GearMesh mesh, ArmourCut cut, Genes genes, Func<float, float> half, float gap, float waist, float hem)
        {
            GearStyle style = cut.Style;

            // Plates hanging from the waist over the thighs.
            if (cut.Likely("tassets", 0.75f, 0.16f, 0f))
            {
                int plates = genes.Count("tassets.count", 2, 6);
                float length = genes.Range("tassets.length", 0.42f, 0.85f);
                float top = BodyForm.Hip + 0.2f;
                for (int i = 0; i < plates; i++)
                {
                    float angle = MathF.PI * ((i + 0.5f) / plates) * genes.Range("tassets.spread", 0.9f, 1.1f);
                    Vector3 outwards = new Vector3(MathF.Cos(angle), 0f, MathF.Sin(angle) * BodyForm.Depth);
                    float r = half(top);
                    float wide = MathF.PI * r / plates * 0.58f;
                    Vector3 side = Vector3.Cross(Vector3.UnitY, outwards).Normalized();
                    Ring[] rings =
                    {
                        new Ring { Centre = outwards * r + Vector3.UnitY * top, Side = side, Front = outwards.Normalized(), HalfWidth = wide, HalfDepth = 0.02f, Hard = true },
                        new Ring { Centre = outwards * (r + 0.1f) + Vector3.UnitY * (top - length * 0.55f), Side = side, Front = outwards.Normalized(), HalfWidth = wide * 1.1f, HalfDepth = 0.02f },
                        new Ring { Centre = outwards * (r + 0.16f) + Vector3.UnitY * (top - length), Side = side, Front = outwards.Normalized(), HalfWidth = wide * genes.Range("tassets.tip", 0.5f, 1.05f), HalfDepth = 0.02f, Hard = true },
                    };
                    int first = mesh.VertexCount;
                    mesh.Loft(Section.Slab(0.4f), rings, cut.Plate);
                    mesh.SkinBy(BodyForm.Skirt, first);
                }
            }

            // A panel of cloth down the front.
            if (cut.Likely("tabard", 0.2f + style.Ornate * 0.3f, 0.08f, 0.24f))
            {
                float bottom = MathF.Max(hem - genes.Range("tabard.below", 0.1f, 0.5f), 0.45f);
                float wide = genes.Range("tabard.wide", 0.22f, 0.44f);
                float top = waist + genes.Range("tabard.top", 0.3f, 0.8f);
                float r = half(top) * BodyForm.Depth + gap * 0.3f;
                int first = mesh.VertexCount;
                var rings = new Ring[7];
                for (int i = 0; i <= 6; i++)
                {
                    float t = i / 6f;
                    float y = top + (bottom - top) * t;
                    rings[i] = new Ring { Centre = new Vector3(0f, y, r + 0.03f + t * 0.1f), Side = Vector3.UnitX, Front = Vector3.UnitZ, HalfWidth = wide * (1f + 0.15f * t), HalfDepth = 0.012f };
                }
                mesh.Loft(Section.Slab(0.3f), rings, (ring, _) => ring >= 5 ? style.Palette.Trim : style.Palette.Cloth);
                mesh.SkinBy(BodyForm.Skirt, first);
            }

            // A cape, from the shoulders.
            if (cut.Likely("cape", 0.14f + style.Ornate * 0.2f, 0.1f, 0.25f))
            {
                float bottom = genes.Range("cape.bottom", 0.7f, 2f);
                float top = BodyForm.Neck - 0.14f;
                float wide = genes.Range("cape.wide", 0.55f, 0.82f);
                int steps = 9;
                var rings = new Ring[steps + 1];
                for (int i = 0; i <= steps; i++)
                {
                    float t = (float)i / steps;
                    float y = top + (bottom - top) * t;
                    float back = half(MathF.Max(y, 2f)) * BodyForm.Depth + 0.06f + t * t * 0.3f;
                    rings[i] = new Ring { Centre = new Vector3(0f, y, -back), Side = Vector3.UnitX, Front = Vector3.UnitZ, HalfWidth = wide * (0.72f + 0.34f * t), HalfDepth = 0.012f };
                }
                int first = mesh.VertexCount;
                mesh.Loft(Section.Slab(0.4f), rings, (ring, _) => ring == steps ? style.Palette.Trim : style.Palette.Cloth);
                mesh.SkinBy(BodyForm.Skirt, first);
            }
        }
    }
}
