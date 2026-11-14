using Dino_Engine.Modelling.Model;
using Dino_Engine.Textures;
using Dino_Engine.Util;
using Dino_Engine.World;
using Dino_Engine.World.Content;
using OpenTK.Mathematics;
using static Dino_Engine.Modelling.Procedural.Architecture.Places.PlaceMaterials;

namespace Dino_Engine.Modelling.Procedural.Architecture.Places
{
    /// <summary>
    /// An outstation of the Amber Institute (PLACES.md), where the blood-doctors grow beasts from what's in the amber: a house of sooty brick on a dark plinth, black iron at its corners and round
    /// its tall windows, which are lit amber from inside, its chimneys smoking; a glasshouse on an iron frame where the eggs are warmed in sand; vats of the growing broth, banded in brass and
    /// glowing amber through their windows; racks of jars with what's growing, or failed to, in them; the benches where amber is cut and drilled for the blood in it; and brass pipework between
    /// them. The Reich's colours are its alone: black iron, oxblood and brass, and the Institute's amber light. Model +z is a piece's front. Headless.
    /// </summary>
    internal static class Laboratory
    {
        private static VertexMaterial Brick(WorldRandom random, float shade = 1f) => new VertexMaterial(MaterialCatalog.Brick.Index, new Colour(new Vector3(0.36f, 0.3f, 0.28f) * shade * random.Range(0.94f, 1.05f)));
        private static Func<Vector3, VertexMaterial> Plinth(WorldRandom random) => n => Dressed(new Vector3(0.26f, 0.26f, 0.27f), random, n.Y > 0.5f ? 1.4f : 0.6f);

        /// <summary>How tall a house of a size stands to its eaves, and where its chimneys rise, which the builder and the smoke both need.</summary>
        public static float EavesOf(float width) => MathF.Max(9.5f, width * 0.55f);

        /// <summary>The Institute's house, its front towards +z: two storeys of brick in bays between iron pilasters, tall arched windows lit amber, a door of iron plate in an oxblood frame with the mark over it, a steep slate roof and its chimneys.</summary>
        public static void Hall(MeshBuilder b, in PlaceSpec spec, WorldRandom random, List<AssetLight> lights)
        {
            float w = Math.Clamp(spec.Metres(0), 10f, 26f), d = Math.Clamp(spec.Metres(1), 8f, 16f);
            float hx = w * 0.5f, hz = d * 0.5f;
            float eaves = EavesOf(w);
            VertexMaterial brick = Brick(random), iron = Iron(random), plate = Plating(random), brass = Brass(random);
            VertexMaterial slate = new VertexMaterial(MaterialCatalog.Slate.Index, new Colour(0.34f, 0.35f, 0.4f));
            // The plinth, a course of dark stone round its foot.
            Kit.Span(b, new Vector3(-hx - 0.3f, -0.6f, -hz - 0.3f), new Vector3(hx + 0.3f, 0.9f, hz + 0.3f), 0.06f, Plinth(random));
            // The walls, as four faces of brick over the plinth.
            Kit.Span(b, new Vector3(-hx, 0.9f, -hz), new Vector3(hx, eaves, hz), 0.02f, brick);
            // A cornice of iron along the eaves, and a string course between the storeys.
            Kit.Span(b, new Vector3(-hx - 0.35f, eaves - 0.1f, -hz - 0.35f), new Vector3(hx + 0.35f, eaves + 0.35f, hz + 0.35f), 0.03f, iron);
            float floor = eaves * 0.48f;
            Kit.Span(b, new Vector3(-hx - 0.12f, floor - 0.15f, -hz - 0.12f), new Vector3(hx + 0.12f, floor + 0.15f, hz + 0.12f), 0.02f, Plinth(random));
            // Bays: pilasters of riveted iron, a window in each storey of each bay, on the front and the sides.
            int bays = Math.Max(3, (int)MathF.Round(w / 4.2f));
            float bay = w / bays;
            for (int i = 0; i <= bays; i++)
            {
                float x = -hx + i * bay;
                Kit.Span(b, new Vector3(x - 0.32f, 0.9f, hz - 0.05f), new Vector3(x + 0.32f, eaves - 0.1f, hz + 0.22f), 0.02f, plate);
                Kit.Span(b, new Vector3(x - 0.32f, 0.9f, -hz - 0.22f), new Vector3(x + 0.32f, eaves - 0.1f, -hz + 0.05f), 0.02f, plate);
            }
            int door = bays / 2;
            int litWindows = 0;
            for (int i = 0; i < bays; i++)
            {
                float x = -hx + (i + 0.5f) * bay;
                for (int storey = 0; storey < 2; storey++)
                {
                    if (storey == 0 && i == door) continue;
                    float y0 = storey == 0 ? 1.6f : floor + 0.8f, y1 = storey == 0 ? floor - 0.9f : eaves - 1.1f;
                    bool lit = random.Chance(0.7f);
                    Window(b, new Vector3(x, 0f, hz + 0.02f), Vector3.UnitX, Vector3.UnitZ, bay * 0.52f, y0, y1, lit, iron, random);
                    if (lit && litWindows < 4 && storey == 0)
                    {
                        lights.Add(AssetLight.Window(new Vector3(x, (y0 + y1) * 0.5f, hz + 0.6f), Vector3.UnitZ) with { Colour = new Vector3(1f, 0.55f, 0.18f), Intensity = 44f, Radius = 11f });
                        litWindows++;
                    }
                }
            }
            // The side walls' windows, and the back's, fewer.
            int sideBays = Math.Max(2, (int)MathF.Round(d / 4.2f));
            for (int i = 0; i < sideBays; i++)
            {
                float z = -hz + (i + 0.5f) * d / sideBays;
                foreach (float s in new[] { -1f, 1f }) Window(b, new Vector3(s * (hx + 0.02f), 0f, z), new Vector3(0f, 0f, -s), new Vector3(s, 0f, 0f), d / sideBays * 0.5f, floor + 0.8f, eaves - 1.1f, random.Chance(0.6f), iron, random);
            }
            // The door: two leaves of iron plate in an oxblood frame, three steps up to it, and the mark over it in brass.
            float dx = -hx + (door + 0.5f) * bay;
            float dw = MathF.Min(bay * 0.72f, 4.4f), dh = MathF.Min(floor - 0.6f, 6.2f);
            Kit.Span(b, new Vector3(dx - dw * 0.5f - 0.45f, 0.9f, hz - 0.02f), new Vector3(dx + dw * 0.5f + 0.45f, 0.9f + dh + 0.5f, hz + 0.3f), 0.03f, OxbloodPaint());
            Kit.Span(b, new Vector3(dx - dw * 0.5f, 0.9f, hz + 0.25f), new Vector3(dx, 0.9f + dh, hz + 0.34f), 0.01f, plate);
            Kit.Span(b, new Vector3(dx, 0.9f, hz + 0.25f), new Vector3(dx + dw * 0.5f, 0.9f + dh, hz + 0.34f), 0.01f, plate);
            foreach (float s in new[] { -0.18f, 0.18f }) Kit.Box(b, new Vector3(dx + s, 0.9f + dh * 0.5f, hz + 0.38f), Vector3.UnitX, Vector3.UnitY, Vector3.UnitZ, new Vector3(0.05f, 0.22f, 0.05f), 0.01f, brass);
            for (int k = 0; k < 3; k++) Kit.Span(b, new Vector3(dx - dw * 0.5f - 0.8f, 0.3f * k, hz + 0.3f + (2 - k) * 0.55f), new Vector3(dx + dw * 0.5f + 0.8f, 0.3f * (k + 1), hz + 0.3f + (3 - k) * 0.55f), 0.03f, Plinth(random));
            Kit.Mark(b, new Vector3(dx, 0.9f + dh + 1.6f, hz + 0.32f), Vector3.UnitX, Vector3.UnitY, 1.9f, 0.05f, random);
            lights.Add(PlaceMaterials.Lantern(new Vector3(dx, 0.9f + dh + 0.3f, hz + 1.4f)) with { Colour = new Vector3(1f, 0.58f, 0.2f) });

            // The roof, steep, its ridge along it, slate over a lead ridge, the gables brick.
            float rise = MathF.Min(d * 0.55f, 7.5f);
            Vector3 eaveFront0 = new Vector3(-hx - 0.6f, eaves + 0.3f, hz + 0.7f), eaveFront1 = new Vector3(hx + 0.6f, eaves + 0.3f, hz + 0.7f);
            Vector3 ridge0 = new Vector3(-hx - 0.6f, eaves + 0.3f + rise, 0f), ridge1 = new Vector3(hx + 0.6f, eaves + 0.3f + rise, 0f);
            Vector3 eaveBack0 = new Vector3(-hx - 0.6f, eaves + 0.3f, -hz - 0.7f), eaveBack1 = new Vector3(hx + 0.6f, eaves + 0.3f, -hz - 0.7f);
            Kit.Face(b, eaveFront0, eaveFront1, ridge1, ridge0, new Vector3(0f, 1f, 1f), slate);
            Kit.Face(b, eaveBack1, eaveBack0, ridge0, ridge1, new Vector3(0f, 1f, -1f), slate);
            Kit.Face(b, eaveFront0, ridge0, eaveBack0, new Vector3(-1f, 0f, 0f), brick);
            Kit.Face(b, eaveFront1, eaveBack1, ridge1, new Vector3(1f, 0f, 0f), brick);
            Kit.Plank(b, ridge0 + Vector3.UnitY * 0.1f, ridge1 + Vector3.UnitY * 0.1f, Vector3.UnitY, 0.5f, 0.25f, iron, 0.03f);
            // The eaves' undersides, dark, so the roof has a thickness.
            Kit.Face(b, eaveFront0 - Vector3.UnitY * 0.25f, new Vector3(-hx - 0.6f, eaves + 0.05f, hz), new Vector3(hx + 0.6f, eaves + 0.05f, hz), eaveFront1 - Vector3.UnitY * 0.25f, -Vector3.UnitY, Dark);

            // Its chimneys: tall stacks of brick with iron caps, from which the furnaces under the vats smoke.
            foreach (Vector3 top in ChimneyTops(w, d))
            {
                Kit.Span(b, new Vector3(top.X - 0.8f, eaves, top.Z - 0.8f), new Vector3(top.X + 0.8f, top.Y - 0.6f, top.Z + 0.8f), 0.03f, brick);
                Kit.Span(b, new Vector3(top.X - 0.95f, top.Y - 0.6f, top.Z - 0.95f), new Vector3(top.X + 0.95f, top.Y - 0.3f, top.Z + 0.95f), 0.02f, iron);
                Kit.Pole(b, new Vector3(top.X, top.Y - 0.3f, top.Z), new Vector3(top.X, top.Y, top.Z), 0.45f, iron, 0.4f, capped: false);
                Kit.Lathe(b, new Vector3(top.X, top.Y - 0.25f, top.Z), Vector3.UnitY, 0.2f, Kit.Profile((0f, 0.43f), (1f, 0f)), 8, 1, Dark);
            }
            // A brass vent on the ridge, where the hatch rooms breathe.
            Kit.Lathe(b, ridge0 * 0.5f + ridge1 * 0.5f + Vector3.UnitY * 0.2f, Vector3.UnitY, 1.6f, Kit.Profile((0f, 0.9f), (0.5f, 0.85f), (0.7f, 1.1f), (1f, 0.1f)), 10, 4, brass);
        }

        /// <summary>Where a house's chimneys end, which is where its smoke comes from.</summary>
        public static Vector3[] ChimneyTops(float w, float d)
        {
            float eaves = EavesOf(w), rise = MathF.Min(d * 0.55f, 7.5f);
            float top = eaves + rise + 2.8f;
            return new[] { new Vector3(-w * 0.32f, top, -d * 0.18f), new Vector3(w * 0.3f, top + 0.8f, -d * 0.2f) };
        }

        // A tall window with an arched head in a frame of black iron, its glass in small panes behind iron glazing bars, lit amber from inside or dark.
        private static void Window(MeshBuilder b, Vector3 foot, Vector3 across, Vector3 outward, float width, float y0, float y1, bool lit, VertexMaterial iron, WorldRandom random)
        {
            float hw = width * 0.5f;
            Vector3 P(float x, float y, float out_) => foot + across * x + Vector3.UnitY * y + outward * out_;
            VertexMaterial glass = lit ? AmberGlow(random.Range(0.55f, 0.85f)) : Glass(new Vector3(0.12f, 0.13f, 0.14f));
            float spring = y1 - hw;
            // The pane: a rectangle and an arch over it, as a fan.
            Kit.Face(b, P(-hw, y0, 0.03f), P(hw, y0, 0.03f), P(hw, spring, 0.03f), P(-hw, spring, 0.03f), outward, glass);
            const int Arch = 6;
            for (int k = 0; k < Arch; k++)
            {
                float a0 = MathF.PI * k / Arch, a1 = MathF.PI * (k + 1) / Arch;
                Kit.Face(b, P(0f, spring, 0.03f), P(MathF.Cos(a0) * hw, spring + MathF.Sin(a0) * hw, 0.03f), P(MathF.Cos(a1) * hw, spring + MathF.Sin(a1) * hw, 0.03f), outward, glass);
            }
            // The frame round it, the sill, and its bars.
            Kit.Box(b, P(-hw - 0.12f, (y0 + spring) * 0.5f, 0.1f), across, Vector3.UnitY, outward, new Vector3(0.12f, (spring - y0) * 0.5f, 0.1f), 0.01f, iron);
            Kit.Box(b, P(hw + 0.12f, (y0 + spring) * 0.5f, 0.1f), across, Vector3.UnitY, outward, new Vector3(0.12f, (spring - y0) * 0.5f, 0.1f), 0.01f, iron);
            for (int k = 0; k < Arch; k++)
            {
                float a0 = MathF.PI * k / Arch, a1 = MathF.PI * (k + 1) / Arch;
                Kit.Plank(b, P(MathF.Cos(a0) * (hw + 0.12f), spring + MathF.Sin(a0) * (hw + 0.12f), 0.1f), P(MathF.Cos(a1) * (hw + 0.12f), spring + MathF.Sin(a1) * (hw + 0.12f), 0.1f), outward, 0.24f, 0.2f, iron, 0.01f);
            }
            Kit.Box(b, P(0f, y0 - 0.08f, 0.16f), across, Vector3.UnitY, outward, new Vector3(hw + 0.25f, 0.1f, 0.2f), 0.02f, iron);
            Kit.Box(b, P(0f, (y0 + spring) * 0.5f, 0.06f), across, Vector3.UnitY, outward, new Vector3(0.035f, (spring - y0) * 0.5f, 0.035f), 0f, iron);
            for (float y = y0 + 0.9f; y < spring; y += 0.9f) Kit.Box(b, P(0f, y, 0.06f), across, Vector3.UnitY, outward, new Vector3(hw, 0.03f, 0.035f), 0f, iron);
        }

        /// <summary>A glasshouse along x, its front towards +z: a low wall of brick, a frame of black iron ribs over it carrying glass, panes left open along the ridge, and inside, trays of sand with the eggs in rows, warmed by braziers.</summary>
        public static void Hatchery(MeshBuilder b, in PlaceSpec spec, WorldRandom random, List<AssetLight> lights)
        {
            float l = Math.Clamp(spec.Metres(0), 6f, 22f), w = Math.Clamp(spec.Metres(1), 4f, 10f);
            float hl = l * 0.5f, hw = w * 0.5f;
            VertexMaterial brick = Brick(random, 0.92f), iron = Iron(random), brass = Brass(random);
            // Glass is dark, as what's seen of it is the sky it gives back: a pale glass reads white from up here.
            VertexMaterial glass = Glass(new Vector3(0.11f, 0.14f, 0.13f));
            VertexMaterial warm = AmberGlow(0.32f);
            const float Wall = 1.1f;
            float eaves = 3.4f, ridge = eaves + w * 0.42f;
            // Its low wall all round, a gap at the front for its door.
            Kit.Span(b, new Vector3(-hl, -0.4f, -hw), new Vector3(hl, Wall, -hw + 0.3f), 0.03f, brick);
            Kit.Span(b, new Vector3(-hl, -0.4f, -hw + 0.3f), new Vector3(-hl + 0.3f, Wall, hw), 0.03f, brick);
            Kit.Span(b, new Vector3(hl - 0.3f, -0.4f, -hw + 0.3f), new Vector3(hl, Wall, hw), 0.03f, brick);
            Kit.Span(b, new Vector3(-hl + 0.3f, -0.4f, hw - 0.3f), new Vector3(-1.4f, Wall, hw), 0.03f, brick);
            Kit.Span(b, new Vector3(1.4f, -0.4f, hw - 0.3f), new Vector3(hl - 0.3f, Wall, hw), 0.03f, brick);
            // Ribs of iron every metre and a half, up the walls and over the ridge.
            int ribs = Math.Max(3, (int)MathF.Round(l / 1.5f));
            for (int i = 0; i <= ribs; i++)
            {
                float x = -hl + l * i / ribs;
                Kit.Plank(b, new Vector3(x, Wall, hw), new Vector3(x, eaves, hw), Vector3.UnitZ, 0.08f, 0.12f, iron, 0.005f);
                Kit.Plank(b, new Vector3(x, Wall, -hw), new Vector3(x, eaves, -hw), Vector3.UnitZ, 0.08f, 0.12f, iron, 0.005f);
                Kit.Plank(b, new Vector3(x, eaves, hw), new Vector3(x, ridge, 0f), new Vector3(0f, 1f, 1f), 0.08f, 0.12f, iron, 0.005f);
                Kit.Plank(b, new Vector3(x, eaves, -hw), new Vector3(x, ridge, 0f), new Vector3(0f, 1f, -1f), 0.08f, 0.12f, iron, 0.005f);
            }
            Kit.Plank(b, new Vector3(-hl, ridge + 0.05f, 0f), new Vector3(hl, ridge + 0.05f, 0f), Vector3.UnitY, 0.18f, 0.14f, iron, 0.01f);
            foreach (float s in new[] { -1f, 1f }) Kit.Plank(b, new Vector3(-hl, eaves, s * hw), new Vector3(hl, eaves, s * hw), Vector3.UnitY, 0.14f, 0.16f, iron, 0.01f);
            // Its glass: the sides warm with what's inside, the roof grey-green, and along the ridge some panes propped open, through which the eggs show.
            for (int i = 0; i < ribs; i++)
            {
                float x0 = -hl + l * i / ribs + 0.04f, x1 = -hl + l * (i + 1) / ribs - 0.04f;
                foreach (float s in new[] { -1f, 1f })
                {
                    Kit.Face(b, new Vector3(x0, Wall, s * hw), new Vector3(x1, Wall, s * hw), new Vector3(x1, eaves, s * hw), new Vector3(x0, eaves, s * hw), new Vector3(0f, 0f, s), warm);
                    bool open = i % 3 == 1;
                    Vector3 a = new Vector3(x0, eaves, s * hw), c = new Vector3(x1, eaves, s * hw);
                    Vector3 top0 = new Vector3(x0, ridge, 0f), top1 = new Vector3(x1, ridge, 0f);
                    Vector3 mid0 = Vector3.Lerp(a, top0, 0.55f), mid1 = Vector3.Lerp(c, top1, 0.55f);
                    Kit.Face(b, a, c, mid1, mid0, new Vector3(0f, 1f, s), glass);
                    if (open)
                    {
                        // Propped open on a stay, so it stands up off the roof.
                        Vector3 lift = new Vector3(0f, 0.55f, s * 0.2f);
                        Kit.Sheet(b, mid0, mid1, top1 + lift, top0 + lift, glass, glass);
                        Kit.Pole(b, mid0 * 0.5f + mid1 * 0.5f, (top0 + top1) * 0.5f + lift * 0.6f, 0.025f, iron);
                    }
                    else Kit.Face(b, mid0, mid1, top1, top0, new Vector3(0f, 1f, s), glass);
                }
            }
            // The gable ends, glazed.
            foreach (float s in new[] { -1f, 1f })
            {
                Kit.Face(b, new Vector3(s * hl, Wall, -hw), new Vector3(s * hl, Wall, hw), new Vector3(s * hl, eaves, hw), new Vector3(s * hl, eaves, -hw), new Vector3(s, 0f, 0f), warm);
                Kit.Face(b, new Vector3(s * hl, eaves, -hw), new Vector3(s * hl, eaves, hw), new Vector3(s * hl, ridge, 0f), new Vector3(s, 0f, 0f), glass);
            }
            // Inside: trays of sand on trestles down both sides, the eggs in rows in them, and braziers between.
            VertexMaterial sand = Sand(0.95f);
            foreach (float s in new[] { -1f, 1f })
            {
                float z = s * (hw * 0.5f);
                Kit.Span(b, new Vector3(-hl + 0.8f, 0.9f, z - hw * 0.32f), new Vector3(hl - 0.8f, 1.25f, z + hw * 0.32f), 0.03f, Timber(random, 0.7f));
                Kit.Span(b, new Vector3(-hl + 0.9f, 1.25f, z - hw * 0.29f), new Vector3(hl - 0.9f, 1.3f, z + hw * 0.29f), 0f, sand);
                int across = Math.Max(2, (int)(hw * 0.58f / 0.55f)), along = Math.Max(4, (int)((l - 2f) / 0.6f));
                for (int i = 0; i < along; i++)
                {
                    for (int j = 0; j < across; j++)
                    {
                        if (random.Chance(0.12f)) continue;
                        float ex = -hl + 1.2f + i * (l - 2.4f) / Math.Max(1, along - 1);
                        float ez = z - hw * 0.24f + j * (hw * 0.48f) / Math.Max(1, across - 1);
                        Egg(b, new Vector3(ex, 1.3f, ez), random.Range(0.22f, 0.28f), random);
                    }
                }
            }
            int braziers = Math.Max(1, (int)(l / 9f));
            for (int k = 0; k < braziers; k++)
            {
                float x = -hl + l * (k + 0.5f) / braziers;
                Kit.Brazier(b, new Vector3(x, 0f, 0f), 0.95f, random, k == 0 ? lights : new List<AssetLight>(), iron);
            }
            lights.Add(Amber(new Vector3(0f, eaves, 0f), 50f, hl + 4f));
            // A brass thermometer by the door, as the blood-doctors watch the heat.
            Kit.Pole(b, new Vector3(1.7f, Wall, hw + 0.05f), new Vector3(1.7f, Wall + 1.6f, hw + 0.05f), 0.05f, brass);
        }

        /// <summary>An egg, upright and a little sunk in the sand: pale, mottled, the size of a head at this scale.</summary>
        public static void Egg(MeshBuilder b, Vector3 foot, float size, WorldRandom random, VertexMaterial? shell = null)
        {
            Vector3 tint = Vector3.Lerp(new Vector3(0.66f, 0.62f, 0.52f), new Vector3(0.56f, 0.58f, 0.5f), random.Float()) * random.Range(0.9f, 1.05f);
            VertexMaterial m = shell ?? new VertexMaterial(MaterialCatalog.Bone.Index, new Colour(tint));
            float lean = random.Range(-0.25f, 0.25f);
            Vector3 axis = new Vector3(MathF.Sin(lean), 1f, MathF.Cos(lean) * 0.2f).Normalized();
            Kit.Lathe(b, foot - axis * (size * 0.3f), axis, size * 2.3f, Kit.Profile((0f, 0f), (0.12f, size * 0.62f), (0.38f, size * 0.92f), (0.62f, size * 0.86f), (0.85f, size * 0.55f), (1f, 0f)), 8, 5, m);
        }

        /// <summary>A vat of the growing broth: staves banded in brass on a stone base, a glass window in its side glowing amber, a ladder up to its lid and its hatch, and pipes off its top.</summary>
        public static void Vat(MeshBuilder b, in PlaceSpec spec, WorldRandom random, List<AssetLight> lights)
        {
            float h = Math.Clamp(spec.Metres(0), 3f, 9f);
            float r = h * random.Range(0.28f, 0.36f);
            VertexMaterial staves = Timber(random, 0.55f), brass = Brass(random), iron = Iron(random);
            Kit.Lathe(b, new Vector3(0f, -0.4f, 0f), Vector3.UnitY, 1.2f, Kit.Profile((0f, r + 0.6f), (1f, r + 0.5f)), 10, 1, Dressed(new Vector3(0.28f, 0.28f, 0.29f), random));
            Kit.Lathe(b, new Vector3(0f, 0.8f, 0f), Vector3.UnitY, h, Kit.Profile((0f, r), (0.5f, r * 1.04f), (1f, r * 0.97f)), 14, 4, staves);
            for (float t = 0.1f; t < 1f; t += 0.22f) b.Tube(new Vector3(0f, 0.8f + h * t - 0.08f, 0f), new Vector3(0f, 0.8f + h * t + 0.08f, 0f), r * 1.05f + 0.04f, r * 1.05f + 0.04f, 14, brass, capped: false);
            // The lid, domed, its hatch, and the pipes off it.
            Kit.Lathe(b, new Vector3(0f, 0.8f + h, 0f), Vector3.UnitY, r * 0.55f, Kit.Profile((0f, r * 1.02f), (0.6f, r * 0.7f), (1f, 0.2f)), 14, 3, iron);
            Kit.Span(b, new Vector3(-0.5f, 0.8f + h + r * 0.35f, r * 0.2f), new Vector3(0.5f, 0.8f + h + r * 0.45f, r * 0.7f), 0.02f, brass);
            Kit.Bend(b, new[] { new Vector3(0f, 0.8f + h + r * 0.5f, 0f), new Vector3(0f, 0.8f + h + r * 0.5f + 1.2f, 0f), new Vector3(0f, 0.8f + h + r * 0.5f + 1.6f, -0.6f), new Vector3(0f, 0.8f + h + r * 0.5f + 1.6f, -r - 1.5f) }, 0.16f, 0.16f, brass);
            // Its window, round, glowing amber with what's growing in it, a dark shape curled in the light.
            float wy = 0.8f + h * 0.45f;
            Vector3 face = new Vector3(0f, wy, r * 1.04f + 0.02f);
            const int Sides = 12;
            float wr = MathF.Min(r * 0.55f, h * 0.2f);
            for (int k = 0; k < Sides; k++)
            {
                float a0 = k * MathF.Tau / Sides, a1 = (k + 1) * MathF.Tau / Sides;
                Kit.Face(b, face, face + new Vector3(MathF.Cos(a0) * wr, MathF.Sin(a0) * wr, 0f), face + new Vector3(MathF.Cos(a1) * wr, MathF.Sin(a1) * wr, 0f), Vector3.UnitZ, AmberGlow(1.1f));
                b.Tube(face + new Vector3(MathF.Cos(a0) * (wr + 0.08f), MathF.Sin(a0) * (wr + 0.08f), 0.02f), face + new Vector3(MathF.Cos(a1) * (wr + 0.08f), MathF.Sin(a1) * (wr + 0.08f), 0.02f), 0.09f, 0.09f, 5, brass, capped: false);
            }
            Kit.Lathe(b, face + new Vector3(-wr * 0.2f, -wr * 0.1f, 0.01f), new Vector3(1f, 0.4f, 0f), wr * 0.8f, Kit.Profile((0f, 0f), (0.3f, wr * 0.3f), (0.8f, wr * 0.22f), (1f, 0f)), 6, 3, Flat(new Vector3(0.12f, 0.06f, 0.03f)));
            lights.Add(Amber(face + Vector3.UnitZ * 0.8f, 36f, 9f));
            // A ladder up its side.
            Vector3 foot = new Vector3(r * 0.75f, 0.8f, r * 0.75f), top = new Vector3(r * 0.75f, 0.8f + h, r * 0.75f);
            Vector3 side = new Vector3(0.7f, 0f, -0.7f) * 0.32f;
            Kit.Pole(b, foot + side, top + side, 0.04f, iron);
            Kit.Pole(b, foot - side, top - side, 0.04f, iron);
            for (float y = 0.4f; y < h; y += 0.45f) Kit.Pole(b, foot + side + Vector3.UnitY * y, foot - side + Vector3.UnitY * y, 0.025f, iron);
            // A furnace door at its foot, the fire under it.
            Kit.Span(b, new Vector3(-0.6f, 0.15f, r + 0.4f), new Vector3(0.6f, 0.95f, r + 0.62f), 0.02f, iron);
            Kit.Face(b, new Vector3(-0.42f, 0.25f, r + 0.63f), new Vector3(0.42f, 0.25f, r + 0.63f), new Vector3(0.42f, 0.8f, r + 0.63f), new Vector3(-0.42f, 0.8f, r + 0.63f), Vector3.UnitZ, Coals);
        }

        /// <summary>A rack of jars on shelves: glass cylinders of amber broth, each with something curled in it, some dark and empty, one fallen and broken on the ground.</summary>
        public static void Specimens(MeshBuilder b, in PlaceSpec spec, WorldRandom random, List<AssetLight> lights)
        {
            VertexMaterial wood = Timber(random, 0.6f), brass = Brass(random);
            const float Long = 3.6f, Deep = 0.9f;
            float[] shelves = { 0.3f, 1.35f, 2.4f };
            foreach (float x in new[] { -Long * 0.5f, Long * 0.5f })
            {
                foreach (float z in new[] { -Deep * 0.5f, Deep * 0.5f }) Kit.Span(b, new Vector3(x - 0.06f, 0f, z - 0.06f), new Vector3(x + 0.06f, 3.3f, z + 0.06f), 0.01f, wood);
            }
            foreach (float y in shelves) Kit.Span(b, new Vector3(-Long * 0.5f - 0.05f, y, -Deep * 0.5f - 0.05f), new Vector3(Long * 0.5f + 0.05f, y + 0.07f, Deep * 0.5f + 0.05f), 0.01f, wood);
            for (int s = 0; s < shelves.Length; s++)
            {
                int jars = random.Int(3, 4);
                for (int k = 0; k < jars; k++)
                {
                    float x = -Long * 0.5f + 0.5f + k * (Long - 1f) / Math.Max(1, jars - 1);
                    float jh = random.Range(0.55f, 0.85f), jr = random.Range(0.18f, 0.26f);
                    Vector3 foot = new Vector3(x, shelves[s] + 0.07f, random.Range(-0.1f, 0.1f));
                    bool full = random.Chance(0.75f);
                    Kit.Lathe(b, foot, Vector3.UnitY, jh, Kit.Profile((0f, jr), (0.9f, jr), (1f, jr * 0.7f)), 8, 1, full ? AmberGlow(random.Range(0.35f, 0.6f)) : Glass(new Vector3(0.2f, 0.22f, 0.2f)));
                    Kit.Lathe(b, foot + Vector3.UnitY * jh, Vector3.UnitY, 0.08f, Kit.Profile((0f, jr * 0.75f), (1f, jr * 0.75f)), 8, 1, brass);
                }
            }
            // One fallen off and broken, its broth spilled dark across the ground and what was in it lying there.
            Vector3 spill = new Vector3(random.Range(-1f, 1f), Ground + 0.01f, Deep * 0.5f + 0.9f);
            Kit.Heap(b, spill, new Vector2(0.8f, 0.55f), 0.02f, random.Float(), Flat(new Vector3(0.18f, 0.09f, 0.02f)), random, 2, 10);
            Kit.Lathe(b, spill + new Vector3(0.1f, 0.12f, 0f), new Vector3(1f, 0.1f, 0.3f), 0.45f, Kit.Profile((0f, 0f), (0.3f, 0.12f), (0.7f, 0.1f), (1f, 0f)), 6, 3, Flesh(0.8f));
            lights.Add(Amber(new Vector3(0f, 1.8f, Deep * 0.5f + 0.5f), 12f, 5f));
        }

        /// <summary>A bench where amber is cut and drilled for the blood in it: lumps of it on the board, a drill on its stand over one, a lamp lit beside it, a lens on an arm, and a crate of raw amber under it.</summary>
        public static void AmberBench(MeshBuilder b, in PlaceSpec spec, WorldRandom random, List<AssetLight> lights)
        {
            VertexMaterial wood = Timber(random, 0.65f), brass = Brass(random), iron = Iron(random);
            VertexMaterial amber = Glass(new Vector3(0.62f, 0.32f, 0.05f));
            const float Long = 3.2f, Deep = 1.2f, High = 1.25f;
            foreach (float x in new[] { -Long * 0.45f, Long * 0.45f })
            {
                foreach (float z in new[] { -Deep * 0.4f, Deep * 0.4f }) Kit.Span(b, new Vector3(x - 0.07f, 0f, z - 0.07f), new Vector3(x + 0.07f, High, z + 0.07f), 0.01f, wood);
            }
            Kit.Span(b, new Vector3(-Long * 0.5f, High, -Deep * 0.5f), new Vector3(Long * 0.5f, High + 0.1f, Deep * 0.5f), 0.02f, wood);
            for (int k = 0; k < 7; k++)
            {
                float s = random.Range(0.08f, 0.2f);
                Kit.Block(b, new Vector3(random.Range(-Long * 0.4f, Long * 0.1f), High + 0.1f + s * 0.6f, random.Range(-Deep * 0.35f, Deep * 0.35f)), new Vector3(s, s * 0.7f, s * 0.85f), random.Float() * MathF.Tau, s * 0.3f, k == 0 ? AmberGlow(0.7f) : amber);
            }
            // The drill: a brass column on a foot, an arm off it, the bit down onto a lump.
            Vector3 drill = new Vector3(Long * 0.28f, High + 0.1f, -Deep * 0.2f);
            Kit.Span(b, drill + new Vector3(-0.2f, 0f, -0.2f), drill + new Vector3(0.2f, 0.08f, 0.2f), 0.01f, iron);
            Kit.Pole(b, drill, drill + Vector3.UnitY * 1.1f, 0.05f, brass);
            Kit.Pole(b, drill + Vector3.UnitY * 1f, drill + new Vector3(0f, 1f, 0.45f), 0.04f, brass);
            Kit.Pole(b, drill + new Vector3(0f, 1f, 0.45f), drill + new Vector3(0f, 0.25f, 0.45f), 0.025f, iron, 0.01f);
            Kit.Block(b, drill + new Vector3(0f, 0.18f, 0.45f), new Vector3(0.15f, 0.1f, 0.13f), 0.4f, 0.04f, amber);
            // The lamp, lit, and the lens on its arm.
            Vector3 lamp = new Vector3(-Long * 0.42f, High + 0.1f, Deep * 0.25f);
            Kit.Lathe(b, lamp, Vector3.UnitY, 0.6f, Kit.Profile((0f, 0.14f), (0.15f, 0.06f), (0.6f, 0.11f), (0.8f, 0.13f), (1f, 0.02f)), 8, 4, brass);
            Kit.Lathe(b, lamp + Vector3.UnitY * 0.25f, Vector3.UnitY, 0.22f, Kit.Profile((0f, 0.09f), (1f, 0.07f)), 8, 1, LampGlow(1.6f));
            lights.Add(PlaceMaterials.Lantern(lamp + Vector3.UnitY * 0.6f) with { Intensity = 30f, Radius = 7f });
            Kit.Pole(b, lamp + new Vector3(0.4f, 0f, -0.3f), lamp + new Vector3(0.6f, 0.7f, -0.4f), 0.025f, brass);
            Kit.Lathe(b, lamp + new Vector3(0.6f, 0.7f, -0.45f), new Vector3(0f, 0f, 1f), 0.04f, Kit.Profile((0f, 0.14f), (1f, 0.14f)), 10, 1, Glass(new Vector3(0.5f, 0.55f, 0.5f)));
            // A crate of raw amber under it, the mark on it.
            Kit.Crate(b, new Vector3(0f, 0.45f, 0f), 0.1f, new Vector3(0.6f, 0.42f, 0.42f), Timber(random, 0.8f), iron, random, marked: true);
        }

        /// <summary>A run of brass pipe on iron stands along x from its start to its length, low enough to step over, with a valve wheel half way.</summary>
        public static void Pipes(MeshBuilder b, in PlaceSpec spec, WorldRandom random)
        {
            float length = Math.Clamp(spec.Metres(0), 2f, 30f);
            VertexMaterial brass = Brass(random), iron = Iron(random);
            float y = 0.9f;
            foreach (float z in new[] { -0.22f, 0.22f }) Kit.Pole(b, new Vector3(0f, y + (z > 0f ? 0.25f : 0f), z), new Vector3(length, y + (z > 0f ? 0.25f : 0f), z), 0.12f, brass);
            for (float x = 0.6f; x < length; x += 2.4f)
            {
                Kit.Pole(b, new Vector3(x, -0.2f, 0f), new Vector3(x, y + 0.35f, 0f), 0.06f, iron);
                Kit.Span(b, new Vector3(x - 0.06f, y + 0.35f, -0.4f), new Vector3(x + 0.06f, y + 0.45f, 0.4f), 0.01f, iron);
            }
            Vector3 valve = new Vector3(length * 0.5f, y + 0.6f, 0.22f);
            Kit.Pole(b, valve - Vector3.UnitY * 0.35f, valve, 0.05f, brass);
            for (int k = 0; k < 8; k++)
            {
                float a0 = k * MathF.Tau / 8f, a1 = (k + 1) * MathF.Tau / 8f;
                b.Tube(valve + new Vector3(MathF.Cos(a0) * 0.32f, 0.05f, MathF.Sin(a0) * 0.32f), valve + new Vector3(MathF.Cos(a1) * 0.32f, 0.05f, MathF.Sin(a1) * 0.32f), 0.04f, 0.04f, 4, Oxblood(), capped: false);
            }
        }
    }
}
