using Dino_Engine.Modelling.Model;
using Dino_Engine.World;
using Dino_Engine.World.Content;
using OpenTK.Mathematics;
using static Dino_Engine.Modelling.Procedural.Architecture.Places.PlaceMaterials;

namespace Dino_Engine.Modelling.Procedural.Architecture.Places
{
    /// <summary>
    /// A wreck (PLACES.md): a ship's two halves where the sea left them, its mast across the sand with the sail torn off its yard, what came out of its hold, its anchor, its boat, and the
    /// wreckers' sheerlegs. The Reich's ships are timber clad in riveted black iron, with an oxblood band at the waterline and brass along the rail, and its sails are oxblood: from across a
    /// beach a player should know whose it was by colour alone. The land's own are planked and painted and their sails are undyed canvas. Everything's at the world's scale, twice life.
    /// Headless.
    /// </summary>
    internal static class Ships
    {
        // ---------------------------------------------------------------- Her frame where she lies

        /// <summary>A ship's frame laid where it came to rest: turned over on its side, pitched along the beach, and sunk into the sand, about the middle of the part being built.</summary>
        private readonly struct Lying
        {
            private readonly Matrix3 _turn;
            private readonly Vector3 _pivot, _offset;

            public Lying(float heel, float pitch, Vector3 pivot, float sunk, ShipDesign design, float from, float to)
            {
                // Heeled to starboard is starboard down, which is a turn about her length the other way; her bow up is a turn about her beam the other way too.
                _turn = Matrix3.CreateRotationZ(-heel) * Matrix3.CreateRotationX(-pitch);
                _pivot = pivot;
                _offset = Vector3.Zero;
                // Lowered until the lowest of her skin is the sunk depth under the ground, wherever along the part that is.
                float lowest = float.MaxValue;
                for (int i = 0; i <= 6; i++)
                {
                    float x = MathHelper.Lerp(from, to, i / 6f);
                    for (int j = 0; j <= 6; j++)
                    {
                        foreach (float side in new[] { -1f, 1f })
                        {
                            Vector3 p = Rotate(design.Point(x, j / 6f, side) - pivot);
                            lowest = MathF.Min(lowest, p.Y);
                        }
                    }
                }
                _offset = new Vector3(0f, -lowest - sunk, 0f);
            }

            private Vector3 Rotate(Vector3 v) => v * _turn;
            public Vector3 P(Vector3 ship) => (ship - _pivot) * _turn + _offset;
            public Vector3 D(Vector3 ship) => ship * _turn;
        }

        // ---------------------------------------------------------------- Her hull

        private const int Rows = 9;                 // Strakes of plank or plate from her keel to her deck.
        private const float Column = 1.5f;          // Metres along her between the points her skin is laid through.
        private const float Shell = 0.17f;          // How thick her side is, plank and frame.

        /// <summary>One of a ship's two halves where it came ashore: its skin to the ragged break, the frames standing out past it, its deck and rails, its masts' stumps, and at the bow her stem,
        /// bowsprit and figure, at the stern her rudder, transom and stern cabin.</summary>
        public static void Hull(MeshBuilder b, in PlaceSpec spec, WorldRandom random)
        {
            int part = Math.Clamp(spec.Int(0), 0, 1);
            bool reich = spec.Int(4) == 1;
            var ship = ShipDesign.Of(spec.Seed, reich);
            float from = part == 0 ? ship.BowStart : 0f, to = part == 0 ? 1f : ship.SternEnd;
            float middle = (from + to) * 0.5f;
            var pose = new Lying(spec.Radians(1), spec.Radians(2), new Vector3(0f, 0f, (middle - 0.5f) * ship.Length), spec.Metres(3), ship, from, to);
            var r = random.Fork("hull");

            // Each strake ends at the break where it tore, the higher ones further back, as her side opened from the deck down.
            float[] ends = new float[Rows + 1];
            for (int j = 0; j <= Rows; j++)
            {
                float rag = (r.Range(0f, 2.4f) + j * r.Range(0.05f, 0.3f)) / ship.Length;
                ends[j] = part == 0 ? from + rag : to - rag;
            }

            Skin(b, ship, pose, part, from, to, ends, r.Fork("skin"));
            Frames(b, ship, pose, part, ends, r.Fork("frames"));
            Deck(b, ship, pose, part, from, to, ends, r.Fork("deck"));
            Rails(b, ship, pose, part, from, to, ends, r.Fork("rails"));
            for (int m = 0; m < ship.Masts.Length; m++)
            {
                float mast = ship.Masts[m];
                if (part == 0 ? mast < ends[Rows] + 0.02f : mast > ends[Rows] - 0.02f) continue;
                Stump(b, ship, pose, mast, reich, r.Fork(m));
            }
            if (part == 0) Bow(b, ship, pose, reich, r.Fork("bow"));
            else Stern(b, ship, pose, reich, r.Fork("stern"));
        }

        // Her side, plank by plank or plate by plate from her keel to her deck, both sides, and the dark of her hold within it; each strake's torn end closed, so her side has a thickness.
        private static void Skin(MeshBuilder b, ShipDesign ship, Lying pose, int part, float from, float to, float[] ends, WorldRandom random)
        {
            VertexMaterial inside = Timber(random, 0.32f);
            for (int j = 0; j < Rows; j++)
            {
                float y0 = j / (float)Rows, y1 = (j + 1f) / Rows;
                float start = part == 0 ? MathF.Max(ends[j], ends[j + 1]) : from;
                float end = part == 0 ? to : MathF.Min(ends[j], ends[j + 1]);
                if (end - start < 0.01f) continue;
                VertexMaterial outside = SkinOf(ship, j, random);
                int columns = Math.Max(1, (int)MathF.Ceiling((end - start) * ship.Length / Column));
                foreach (float side in new[] { -1f, 1f })
                {
                    for (int i = 0; i < columns; i++)
                    {
                        float xa = MathHelper.Lerp(start, end, i / (float)columns), xb = MathHelper.Lerp(start, end, (i + 1f) / columns);
                        Vector3 a0 = pose.P(ship.Point(xa, y0, side)), b0 = pose.P(ship.Point(xb, y0, side)), b1 = pose.P(ship.Point(xb, y1, side)), a1 = pose.P(ship.Point(xa, y1, side));
                        Vector3 outward = pose.D(new Vector3(side, 0.15f, 0f));
                        Along(b, a0, b0, b1, a1, outward, outside);
                        Vector3 c0 = pose.P(ship.Point(xa, y0, side, Shell)), d0 = pose.P(ship.Point(xb, y0, side, Shell)), d1 = pose.P(ship.Point(xb, y1, side, Shell)), c1 = pose.P(ship.Point(xa, y1, side, Shell));
                        Along(b, c0, d0, d1, c1, -outward, inside);
                    }
                    // The plank's torn end, between its face and the dark inside it.
                    float tear = part == 0 ? start : end;
                    Vector3 o0 = pose.P(ship.Point(tear, y0, side)), o1 = pose.P(ship.Point(tear, y1, side)), i0 = pose.P(ship.Point(tear, y0, side, Shell)), i1 = pose.P(ship.Point(tear, y1, side, Shell));
                    Kit.Face(b, o0, o1, i1, i0, pose.D(new Vector3(0f, 0f, part == 0 ? -1f : 1f)), Bleached(random, 0.8f));
                }
            }
            // Her keel, and its last length broken off and lying out past her planks.
            float keelFrom = part == 0 ? ends[0] - 0.05f : from, keelTo = part == 0 ? to : ends[0] + 0.05f;
            int steps = Math.Max(2, (int)MathF.Ceiling((keelTo - keelFrom) * ship.Length / 2.5f));
            VertexMaterial keel = ship.Reich ? Rusted(random) : Tarred(random);
            for (int i = 0; i < steps; i++)
            {
                float xa = MathHelper.Lerp(keelFrom, keelTo, i / (float)steps), xb = MathHelper.Lerp(keelFrom, keelTo, (i + 1f) / steps);
                Vector3 pa = new Vector3(0f, ship.KeelAt(xa) + 0.3f, (xa - 0.5f) * ship.Length), pb = new Vector3(0f, ship.KeelAt(xb) + 0.3f, (xb - 0.5f) * ship.Length);
                Kit.Plank(b, pose.P(pa), pose.P(pb), pose.D(Vector3.UnitY), ShipDesign.Keel * 2f, 0.62f, keel, 0.04f);
            }
            float out_ = random.Range(1.2f, 3f) / ship.Length;
            float tip = part == 0 ? ends[0] - out_ : ends[0] + out_;
            Vector3 stub0 = new Vector3(0f, ship.KeelAt(ends[0]) + 0.3f, (ends[0] - 0.5f) * ship.Length), stub1 = new Vector3(0.08f, ship.KeelAt(tip) + 0.22f, (tip - 0.5f) * ship.Length);
            Kit.Plank(b, pose.P(stub0), pose.P(stub1), pose.D(Vector3.UnitY), ShipDesign.Keel * 1.8f, 0.5f, Bleached(random, 0.9f), 0.03f);
        }

        // What a strake is clad in: the Reich's plates, rusted below her waterline, oxblood in a band at it and brass at her rail; or the land's planks, tarred below and painted above, with
        // a band of another colour along her.
        private static VertexMaterial SkinOf(ShipDesign ship, int row, WorldRandom random)
        {
            if (ship.Reich)
            {
                if (row <= 2) return RustedPlating(random);
                if (row == 3) return OxbloodPaint(0.95f);
                if (row == Rows - 1) return Brass(random, 0.9f);
                return Plating(random);
            }
            if (row <= 2) return Tarred(random, 1.3f);
            if (row == Rows - 2) return Painted(ship.Band);
            return Painted(ship.Paint * random.Range(0.92f, 1.05f));
        }

        // A face of her side laid through four points, from a0 to b0 along her and from a0 to a1 up her side (or across her deck), wound to face out, and mapped so that its texture runs
        // along her as a hull's strakes do: a material whose boards run along its v (planks, boards) has its v along her, and one whose courses run along its u (plates) its u.
        private static void Along(MeshBuilder b, Vector3 a0, Vector3 b0, Vector3 b1, Vector3 a1, Vector3 outward, VertexMaterial m)
        {
            bool boards = m.materialTextureIndex != Textures.MaterialCatalog.RivetedPlate.Index;
            // MeshBuilder runs a texture's u from the first corner to the second and its v towards the fourth.
            if (boards)
            {
                if (Vector3.Dot(Vector3.Cross(b1 - a0, b0 - a1), outward) >= 0f) b.Quad(a0, a1, b1, b0, m);
                else b.Quad(a1, a0, b0, b1, m);
            }
            else
            {
                if (Vector3.Dot(Vector3.Cross(b1 - a0, a1 - b0), outward) >= 0f) b.Quad(a0, b0, b1, a1, m);
                else b.Quad(b0, a0, a1, b1, m);
            }
        }

        // Her frames: where her side has torn away they stand out past her planks, bare, some broken short, as a wreck's ribs do; inside her they're seen through the break.
        private static void Frames(MeshBuilder b, ShipDesign ship, Lying pose, int part, float[] ends, WorldRandom random)
        {
            VertexMaterial timber = ship.Reich ? Timber(random, 0.8f) : Bleached(random, 0.85f);
            float spacing = random.Range(1.05f, 1.3f) / ship.Length;
            float edge = part == 0 ? ends.Min() : ends.Max();
            float reach = random.Range(1.8f, 3.4f) / ship.Length;
            int count = 0;
            for (float x = part == 0 ? edge - reach : edge + reach; part == 0 ? x < edge + 6f / ship.Length : x > edge - 6f / ship.Length; x += part == 0 ? spacing : -spacing)
            {
                if (++count > 8) break;
                bool past = part == 0 ? x < edge : x > edge;
                foreach (float side in new[] { -1f, 1f })
                {
                    // Out past her planks a frame's broken short more often than not.
                    float top = past ? random.Range(0.25f, 1f) : 1.04f;
                    if (past && random.Chance(0.25f)) continue;
                    var points = new List<Vector3>();
                    for (int j = 0; j <= 6; j++)
                    {
                        float y = top * j / 6f;
                        points.Add(pose.P(ship.Point(x, y, side, Shell * 0.6f)));
                    }
                    Kit.Bend(b, points, 0.15f, past ? 0.09f : 0.12f, timber);
                }
                // The beam across her at her deck, where it's still there.
                if (!past || random.Chance(0.35f))
                {
                    Vector3 port = ship.Point(x, 0.97f, -1f, Shell), starboard = ship.Point(x, 0.97f, 1f, Shell);
                    Kit.Plank(b, pose.P(port), pose.P(starboard), pose.D(Vector3.UnitY), 0.26f, 0.3f, timber, 0.03f);
                }
            }
        }

        // Her deck, planked fore and aft between her rails, each plank ending raggedly towards the break and some gone, so the dark of her hold shows; a hatch with its coaming.
        private static void Deck(MeshBuilder b, ShipDesign ship, Lying pose, int part, float from, float to, float[] ends, WorldRandom random)
        {
            // The Reich's decks are tarred dark, as everything it builds is near black; the land's are scrubbed and gone silver.
            VertexMaterial planks = ship.Reich ? Timber(random, 0.55f) : Weathered(random, 1.05f);
            float edge = part == 0 ? ends[Rows] + random.Range(0.5f, 2f) / ship.Length : ends[Rows] - random.Range(0.5f, 2f) / ship.Length;
            const float PlankWidth = 0.62f;
            float widest = ship.HalfBreadth(0.45f, 1f) - Shell;
            int strips = Math.Max(4, (int)MathF.Floor(widest * 2f / PlankWidth));
            for (int k = 0; k < strips; k++)
            {
                float across0 = -1f + 2f * k / strips, across1 = -1f + 2f * (k + 1) / strips;
                float rag = random.Range(0f, 3.2f) / ship.Length;
                float start = part == 0 ? edge + rag : from, end = part == 0 ? to : edge - rag;
                // A few planks torn out altogether, near the break.
                if (random.Chance(0.12f)) continue;
                int columns = Math.Max(1, (int)MathF.Ceiling((end - start) * ship.Length / Column));
                for (int i = 0; i < columns; i++)
                {
                    float xa = MathHelper.Lerp(start, end, i / (float)columns), xb = MathHelper.Lerp(start, end, (i + 1f) / columns);
                    Vector3 Corner(float x, float across)
                    {
                        float half = ship.HalfBreadth(x, 1f) - Shell;
                        float camber = (1f - across * across) * 0.12f;
                        return pose.P(new Vector3(across * half, ship.DeckAt(x) - 0.06f + camber, (x - 0.5f) * ship.Length));
                    }
                    Vector3 p00 = Corner(xa, across0), p10 = Corner(xb, across0), p11 = Corner(xb, across1), p01 = Corner(xa, across1);
                    Along(b, p00, p10, p11, p01, pose.D(Vector3.UnitY), planks);
                }
            }
            // The deck's thickness under its planks, dark, so what's seen through the gaps is the hold.
            float holdFrom = part == 0 ? edge : from, holdTo = part == 0 ? to : edge;
            int n = Math.Max(1, (int)MathF.Ceiling((holdTo - holdFrom) * ship.Length / 3f));
            for (int i = 0; i < n; i++)
            {
                float xa = MathHelper.Lerp(holdFrom, holdTo, i / (float)n), xb = MathHelper.Lerp(holdFrom, holdTo, (i + 1f) / n);
                Vector3 Below(float x, float s) => pose.P(new Vector3(s * (ship.HalfBreadth(x, 1f) - Shell), ship.DeckAt(x) - 0.4f, (x - 0.5f) * ship.Length));
                Kit.Face(b, Below(xa, -1f), Below(xb, -1f), Below(xb, 1f), Below(xa, 1f), pose.D(Vector3.UnitY), Dark);
            }
            // A hatch amidships of the part, its coaming standing up round the dark of the hold.
            float hatch = part == 0 ? MathHelper.Lerp(edge, to, 0.42f) : MathHelper.Lerp(from, edge, 0.62f);
            float hy = ship.DeckAt(hatch) + 0.06f, hz = (hatch - 0.5f) * ship.Length;
            float hw = MathF.Min(ship.HalfBreadth(hatch, 1f) * 0.36f, 2.2f), hl = random.Range(1.6f, 2.4f);
            VertexMaterial coaming = ship.Reich ? Iron(random) : Timber(random);
            foreach ((Vector3 a, Vector3 c) in new[] { (new Vector3(-hw, hy, hz - hl), new Vector3(hw, hy, hz - hl)), (new Vector3(hw, hy, hz - hl), new Vector3(hw, hy, hz + hl)), (new Vector3(hw, hy, hz + hl), new Vector3(-hw, hy, hz + hl)), (new Vector3(-hw, hy, hz + hl), new Vector3(-hw, hy, hz - hl)) })
            {
                Kit.Plank(b, pose.P(a + Vector3.UnitY * 0.28f), pose.P(c + Vector3.UnitY * 0.28f), pose.D(Vector3.UnitX), 0.56f, 0.2f, coaming, 0.02f);
            }
            Kit.Face(b, pose.P(new Vector3(-hw, hy + 0.12f, hz - hl)), pose.P(new Vector3(hw, hy + 0.12f, hz - hl)), pose.P(new Vector3(hw, hy + 0.12f, hz + hl)), pose.P(new Vector3(-hw, hy + 0.12f, hz + hl)), pose.D(Vector3.UnitY), Dark);
        }

        // Her rails: the bulwark along each side of her deck and the cap along its top, the Reich's of plate under brass, the land's of planks under a painted rail, broken at the break.
        private static void Rails(MeshBuilder b, ShipDesign ship, Lying pose, int part, float from, float to, float[] ends, WorldRandom random)
        {
            const float High = 1.15f;
            VertexMaterial wall = ship.Reich ? Plating(random, 0.95f) : Painted(ship.Paint * 0.95f);
            VertexMaterial cap = ship.Reich ? Brass(random) : Timber(random, 0.9f);
            foreach (float side in new[] { -1f, 1f })
            {
                float rag = random.Range(0.3f, 2.6f) / ship.Length;
                float start = part == 0 ? ends[Rows] + rag : from, end = part == 0 ? 0.985f : ends[Rows] - rag;
                int columns = Math.Max(1, (int)MathF.Ceiling((end - start) * ship.Length / Column));
                for (int i = 0; i < columns; i++)
                {
                    float xa = MathHelper.Lerp(start, end, i / (float)columns), xb = MathHelper.Lerp(start, end, (i + 1f) / columns);
                    Vector3 a0 = ship.Point(xa, 1f, side), b0 = ship.Point(xb, 1f, side);
                    Vector3 lean = new Vector3(side * 0.08f, High, 0f);
                    Vector3 pa0 = pose.P(a0), pb0 = pose.P(b0), pb1 = pose.P(b0 + lean), pa1 = pose.P(a0 + lean);
                    Vector3 outward = pose.D(new Vector3(side, 0f, 0f));
                    Along(b, pa0, pb0, pb1, pa1, outward, wall);
                    Along(b, pose.P(a0 + new Vector3(-side * 0.12f, 0f, 0f)), pose.P(b0 + new Vector3(-side * 0.12f, 0f, 0f)), pose.P(b0 + lean + new Vector3(-side * 0.12f, 0f, 0f)), pose.P(a0 + lean + new Vector3(-side * 0.12f, 0f, 0f)), -outward, ship.Reich ? Plating(random, 0.85f) : Timber(random, 0.5f));
                    Kit.Plank(b, pose.P(a0 + lean + new Vector3(-side * 0.04f, 0.06f, 0f)), pose.P(b0 + lean + new Vector3(-side * 0.04f, 0.06f, 0f)), pose.D(Vector3.UnitY), 0.26f, 0.12f, cap, 0.02f);
                }
            }
        }

        // A mast broken off a few metres above her deck, splintered at the top, with the collar it was wedged through at the deck.
        private static void Stump(MeshBuilder b, ShipDesign ship, Lying pose, float at, bool reich, WorldRandom random)
        {
            float z = (at - 0.5f) * ship.Length, deck = ship.DeckAt(at);
            float radius = random.Range(0.36f, 0.46f);
            float height = random.Range(1.4f, 4.2f);
            VertexMaterial wood = Timber(random, 0.85f);
            Kit.Pole(b, pose.P(new Vector3(0f, deck - 1.2f, z)), pose.P(new Vector3(0f, deck + height, z)), radius, wood, radius * 0.95f);
            // Splinters round the break, longer on one side as it went.
            for (int k = 0; k < 7; k++)
            {
                float a = k * MathF.Tau / 7f + random.Range(-0.2f, 0.2f);
                Vector3 foot = new Vector3(MathF.Cos(a) * radius * 0.7f, deck + height - 0.05f, z + MathF.Sin(a) * radius * 0.7f);
                Vector3 tip = foot + new Vector3(MathF.Cos(a) * 0.08f, random.Range(0.3f, 1.3f) * (k < 3 ? 1.4f : 0.7f), MathF.Sin(a) * 0.08f);
                Kit.Pole(b, pose.P(foot), pose.P(tip), 0.07f, Bleached(random), 0.01f);
            }
            Kit.Pole(b, pose.P(new Vector3(0f, deck - 0.05f, z)), pose.P(new Vector3(0f, deck + 0.35f, z)), radius + 0.22f, reich ? Iron(random) : Timber(random, 0.7f), radius + 0.18f);
        }

        // Her bow: the stem up past her rail, the bowsprit out from it and snapped, and her figure under it: the Reich's mark in brass on each bow, or the land's carved and painted scroll.
        private static void Bow(MeshBuilder b, ShipDesign ship, Lying pose, bool reich, WorldRandom random)
        {
            VertexMaterial stem = reich ? Iron(random) : Timber(random, 0.8f);
            var points = new List<Vector3>();
            for (int j = 0; j <= 6; j++)
            {
                float y = j / 6f * 1.12f;
                float x = 0.985f + 0.015f * y;
                points.Add(pose.P(new Vector3(0f, ship.KeelAt(MathF.Min(x, 1f)) + (ship.DeckAt(1f) + 1.4f - ship.KeelAt(1f)) * (j / 6f), (MathF.Min(x, 1f) - 0.5f) * ship.Length + j * 0.1f)));
            }
            Kit.Bend(b, points, 0.32f, 0.26f, stem);
            Vector3 head = new Vector3(0f, ship.DeckAt(1f) + 1.2f, ship.Length * 0.5f + 0.5f);
            float sprit = random.Range(3f, 7f);
            Vector3 dir = new Vector3(0f, MathF.Sin(0.32f), MathF.Cos(0.32f));
            Kit.Pole(b, pose.P(head - dir * 2f), pose.P(head + dir * sprit), 0.26f, Timber(random, 0.8f), 0.2f);
            // Its snapped end.
            for (int k = 0; k < 4; k++)
            {
                float a = k * MathF.Tau / 4f;
                Vector3 foot = head + dir * sprit + new Vector3(MathF.Cos(a) * 0.12f, MathF.Sin(a) * 0.12f, 0f);
                Kit.Pole(b, pose.P(foot), pose.P(foot + dir * random.Range(0.3f, 0.9f)), 0.05f, Bleached(random), 0.01f);
            }
            float figure = 0.93f;
            foreach (float side in new[] { -1f, 1f })
            {
                Vector3 at = ship.Point(figure, 0.86f, side) + new Vector3(side * 0.03f, 0f, 0f);
                Vector3 along = (ship.Point(figure + 0.03f, 0.86f, side) - ship.Point(figure - 0.03f, 0.86f, side)).Normalized();
                if (reich)
                {
                    // The mark in brass on oxblood, a figure for a ship that's a weapon: facing out from each bow.
                    Vector3 right = side > 0f ? -along : along;
                    Kit.Mark(b, pose.P(at), pose.D(right), pose.D(Vector3.UnitY), 2.2f, 0.05f, random);
                }
                else
                {
                    // A scroll of carved and painted wood curling forward along her bow.
                    VertexMaterial gilt = Painted(new Vector3(0.52f, 0.38f, 0.12f));
                    var curl = new List<Vector3>();
                    for (int k = 0; k <= 8; k++)
                    {
                        float t = k / 8f;
                        float a = t * MathF.PI * 1.5f;
                        curl.Add(pose.P(at + along * (-1.6f + t * 2.2f) + Vector3.UnitY * (MathF.Sin(a) * 0.45f * (1f - t * 0.5f)) + new Vector3(side * 0.12f, 0f, 0f)));
                    }
                    Kit.Bend(b, curl, 0.16f, 0.07f, gilt);
                }
            }
        }

        // Her stern: the sternpost, the rudder hanging askew from it, the transom closing her, and the cabin over her quarterdeck with its windows, and her lantern.
        private static void Stern(MeshBuilder b, ShipDesign ship, Lying pose, bool reich, WorldRandom random)
        {
            float z0 = -ship.Length * 0.5f;
            VertexMaterial transom = reich ? Plating(random) : Painted(ship.Paint);
            // The transom: a fan of faces from the middle of her stern to the edge of her skin there.
            Vector3 centre = new Vector3(0f, (ship.KeelAt(0f) + ship.DeckAt(0f)) * 0.5f, z0);
            for (int j = 0; j < Rows; j++)
            {
                float y0 = j / (float)Rows, y1 = (j + 1f) / Rows;
                foreach (float side in new[] { -1f, 1f })
                {
                    Vector3 a = ship.Point(0f, y0, side), c = ship.Point(0f, y1, side);
                    Kit.Face(b, pose.P(centre), pose.P(a), pose.P(c), pose.D(-Vector3.UnitZ), transom);
                }
            }
            Vector3 portTop = ship.Point(0f, 1f, -1f), starTop = ship.Point(0f, 1f, 1f), portFoot = ship.Point(0f, 0f, -1f), starFoot = ship.Point(0f, 0f, 1f);
            Kit.Face(b, pose.P(centre), pose.P(starTop), pose.P(portTop), pose.D(-Vector3.UnitZ), transom);
            Kit.Face(b, pose.P(centre), pose.P(portFoot), pose.P(starFoot), pose.D(-Vector3.UnitZ), transom);

            // The rudder, its blade hanging off its post at a slant, as the sea left it.
            float swing = random.Range(0.25f, 0.7f) * (random.Chance(0.5f) ? 1f : -1f);
            Vector3 post = new Vector3(0f, ship.KeelAt(0f), z0 - 0.3f);
            Vector3 aft = new Vector3(MathF.Sin(swing), 0f, -MathF.Cos(swing));
            float tall = ship.DeckAt(0f) - ship.KeelAt(0f) - 0.4f;
            Kit.Box(b, pose.P(post + aft * 1.1f + Vector3.UnitY * (tall * 0.5f)), pose.D(new Vector3(MathF.Cos(swing), 0f, MathF.Sin(swing))), pose.D(Vector3.UnitY), pose.D(aft), new Vector3(0.22f, tall * 0.5f, 1.1f), 0.03f, reich ? Iron(random) : Tarred(random));
            Kit.Pole(b, pose.P(post), pose.P(post + Vector3.UnitY * (tall + 0.9f)), 0.2f, Timber(random, 0.7f));

            // The cabin on her quarterdeck, its windows dark, and a lantern on her taffrail.
            float cabinEnd = random.Range(0.13f, 0.19f);
            float deck0 = ship.DeckAt(0f), deck1 = ship.DeckAt(cabinEnd);
            float high = random.Range(2.2f, 2.9f);
            float half = ship.HalfBreadth(0.02f, 1f) - Shell - 0.2f;
            VertexMaterial wall = reich ? Plating(random, 0.9f) : Painted(ship.Paint * 1.08f);
            VertexMaterial trim = reich ? OxbloodPaint() : Painted(ship.Band);
            Vector3 cabinFoot = new Vector3(0f, MathF.Min(deck0, deck1), z0 + 0.3f);
            float cabinLength = cabinEnd * ship.Length - 0.6f;
            Kit.Box(b, pose.P(cabinFoot + new Vector3(0f, high * 0.5f, cabinLength * 0.5f)), pose.D(Vector3.UnitX), pose.D(Vector3.UnitY), pose.D(Vector3.UnitZ), new Vector3(half, high * 0.5f, cabinLength * 0.5f), 0.04f, wall);
            Kit.Box(b, pose.P(cabinFoot + new Vector3(0f, high + 0.08f, cabinLength * 0.5f)), pose.D(Vector3.UnitX), pose.D(Vector3.UnitY), pose.D(Vector3.UnitZ), new Vector3(half + 0.25f, 0.1f, cabinLength * 0.5f + 0.25f), 0.03f, trim);
            int windows = Math.Max(2, (int)(half * 2f / 1.5f));
            for (int k = 0; k < windows; k++)
            {
                float x = -half + (k + 0.5f) * (half * 2f / windows);
                Vector3 w = cabinFoot + new Vector3(x, high * 0.55f, -0.04f);
                Kit.Box(b, pose.P(w), pose.D(Vector3.UnitX), pose.D(Vector3.UnitY), pose.D(Vector3.UnitZ), new Vector3(0.42f, 0.55f, 0.06f), 0.01f, trim);
                Kit.Face(b, pose.P(w + new Vector3(-0.3f, -0.42f, -0.08f)), pose.P(w + new Vector3(0.3f, -0.42f, -0.08f)), pose.P(w + new Vector3(0.3f, 0.42f, -0.08f)), pose.P(w + new Vector3(-0.3f, 0.42f, -0.08f)), pose.D(-Vector3.UnitZ), Dark);
            }
            Vector3 lamp = new Vector3(0f, cabinFoot.Y + high + 0.2f, z0 - 0.2f);
            VertexMaterial metal = reich ? Brass(random) : LandIron(random);
            Kit.Pole(b, pose.P(lamp), pose.P(lamp + Vector3.UnitY * 1.4f), 0.06f, metal);
            Kit.Box(b, pose.P(lamp + new Vector3(0f, 1.9f, 0f)), pose.D(Vector3.UnitX), pose.D(Vector3.UnitY), pose.D(Vector3.UnitZ), new Vector3(0.34f, 0.5f, 0.34f), 0.02f, Glass(new Vector3(0.3f, 0.28f, 0.22f)));
            Kit.Box(b, pose.P(lamp + new Vector3(0f, 2.45f, 0f)), pose.D(Vector3.UnitX), pose.D(Vector3.UnitY), pose.D(Vector3.UnitZ), new Vector3(0.42f, 0.08f, 0.42f), 0.02f, metal);
        }

        // ---------------------------------------------------------------- Her rig

        /// <summary>A mast fallen across the sand, its foot splintered where it broke, its top and its yards lying over, the sail half off its yard and spread on the sand, and its rigging trailing.</summary>
        public static void Mast(MeshBuilder b, in PlaceSpec spec, WorldRandom random)
        {
            float length = Math.Clamp(spec.Metres(0), 8f, 30f);
            bool reich = spec.Int(1) == 1;
            float radius = random.Range(0.32f, 0.42f);
            float y = Ground + radius - 0.08f;
            VertexMaterial wood = Timber(random, 0.85f);
            Kit.Pole(b, new Vector3(0f, y, 0f), new Vector3(0f, y - radius * 0.4f, length), radius, wood, radius * 0.45f);
            for (int k = 0; k < 6; k++)
            {
                float a = k * MathF.Tau / 6f + random.Range(-0.3f, 0.3f);
                Vector3 foot = new Vector3(MathF.Cos(a) * radius * 0.6f, y + MathF.Sin(a) * radius * 0.6f, 0.05f);
                Kit.Pole(b, foot, foot + new Vector3(MathF.Cos(a) * 0.1f, MathF.Sin(a) * 0.1f, -random.Range(0.4f, 1.4f)), 0.07f, Bleached(random), 0.01f);
            }
            // Its top, a platform for the lookout, lying on its edge.
            float topAt = length * random.Range(0.62f, 0.72f);
            VertexMaterial top = reich ? Iron(random) : Timber(random, 0.75f);
            Kit.Box(b, new Vector3(0.9f, y + 0.4f, topAt), new Vector3(0.25f, 0.97f, 0f).Normalized(), new Vector3(-0.97f, 0.25f, 0f).Normalized(), Vector3.UnitZ, new Vector3(0.12f, 1.4f, 1.1f), 0.02f, top);

            // The yards, each across the mast and one end down on the sand, the other on the mast.
            VertexMaterial sail = reich ? Oxblood(0.9f) : Canvas(random);
            VertexMaterial sailBack = reich ? Oxblood(0.75f) : Canvas(random, 0.85f);
            int yards = random.Int(1, 2);
            for (int k = 0; k < yards; k++)
            {
                float along = length * (k == 0 ? random.Range(0.42f, 0.55f) : random.Range(0.74f, 0.86f));
                float span = length * random.Range(0.45f, 0.6f) * (k == 0 ? 1f : 0.75f);
                float turn = random.Range(-0.35f, 0.35f);
                Vector3 across = new Vector3(MathF.Cos(turn), 0f, MathF.Sin(turn));
                Vector3 lowEnd = new Vector3(0f, Ground + 0.18f, along) - across * (span * 0.5f);
                Vector3 highEnd = new Vector3(0f, y + radius + 0.15f, along) + across * (span * 0.5f);
                Kit.Pole(b, lowEnd, highEnd, 0.18f, wood, 0.12f);
                if (k > 0 && random.Chance(0.4f)) continue;
                // Its sail, still bent to half its length and spread over the sand from it, crumpled, its free edge torn.
                float share = random.Range(0.45f, 0.85f);
                Vector3 s0 = Vector3.Lerp(lowEnd, highEnd, 0.08f), s1 = Vector3.Lerp(lowEnd, highEnd, share);
                Vector3 spread = new Vector3(-MathF.Sin(turn), 0f, MathF.Cos(turn)) * (random.Chance(0.5f) ? 1f : -1f);
                float drop = span * random.Range(0.45f, 0.7f);
                Vector3 s3 = new Vector3(s0.X, Ground + 0.05f, s0.Z) + spread * drop, s2 = new Vector3(s1.X, Ground + 0.05f, s1.Z) + spread * drop * random.Range(0.6f, 1.1f);
                float seed = random.Range(0f, 10f);
                Kit.Cloth(b, s0, s1, s2, s3, -0.25f, 6, sail, sailBack, (u, v) => (MathF.Sin(u * 9f + seed) * 0.25f + MathF.Sin(v * 7f + seed * 1.7f) * 0.2f) * v);
                if (reich)
                {
                    // The mark on the sail, where it lies flattest.
                    Vector3 c = Vector3.Lerp(Vector3.Lerp(s0, s1, 0.5f), Vector3.Lerp(s3, s2, 0.5f), 0.55f) + Vector3.UnitY * 0.32f;
                    Vector3 right = (s1 - s0).Normalized();
                    Kit.Mark(b, c, right, Vector3.Cross(right, Vector3.UnitY).Normalized() * -1f, MathF.Min(drop * 0.4f, 3f), 0.02f, random);
                }
            }
            // Rigging trailing off the mast across the sand.
            VertexMaterial rope = Rope(random, 0.85f);
            for (int k = 0; k < 4; k++)
            {
                Vector3 from = new Vector3(0f, y, length * random.Range(0.3f, 0.95f));
                Vector3 to = from + new Vector3(random.Range(-6f, 6f), 0f, random.Range(-5f, 2f));
                to.Y = Ground + 0.06f;
                Kit.Rope(b, from, to, -0.1f, 0.05f, rope, 5);
            }
            Kit.Coil(b, new Vector3(random.Range(-3f, 3f), Ground, length * random.Range(0.2f, 0.5f)), 0.7f, rope, random);
        }

        // ---------------------------------------------------------------- What's strewn

        /// <summary>What the sea took apart and threw up the beach: an old frame's ribs standing out of the sand, a run of her side lying flat, or a heap of timbers.</summary>
        public static void Wreckage(MeshBuilder b, in PlaceSpec spec, WorldRandom random)
        {
            int form = spec.Int(0);
            float length = Math.Clamp(spec.Metres(1), 3f, 18f);
            bool reich = spec.Int(2) == 1;
            VertexMaterial bleached = Bleached(random);
            if (form == 0)
            {
                // A keel half sunk in the sand, its ribs curving up out of it in pairs, broken short more the further from the middle.
                Kit.Plank(b, new Vector3(0f, Ground, -length * 0.5f), new Vector3(0f, Ground + 0.05f, length * 0.5f), Vector3.UnitY, 0.5f, 0.45f, bleached, 0.04f);
                int ribs = Math.Max(4, (int)(length / 1.3f));
                float beam = random.Range(3.2f, 5f);
                for (int i = 0; i < ribs; i++)
                {
                    float z = -length * 0.5f + (i + 0.5f) * length / ribs;
                    float t = MathF.Abs(z) / (length * 0.5f);
                    foreach (float side in new[] { -1f, 1f })
                    {
                        if (random.Chance(0.15f + t * 0.3f)) continue;
                        float height = random.Range(0.35f, 1f) * (1f - t * 0.5f);
                        var points = new List<Vector3>();
                        for (int j = 0; j <= 5; j++)
                        {
                            float s = j / 5f * height;
                            points.Add(new Vector3(side * beam * 0.5f * MathF.Sin(s * MathF.PI * 0.5f) * (1f - t * 0.35f), Ground - 0.2f + s * 2.6f, z + random.Range(-0.05f, 0.05f)));
                        }
                        Kit.Bend(b, points, 0.16f, 0.1f, bleached);
                    }
                }
                return;
            }
            if (form == 1)
            {
                // A run of her side lying on the sand, planks across a few frames, its edges torn.
                float width = random.Range(2.4f, 4f);
                VertexMaterial skin = reich ? RustedPlating(random) : Weathered(random);
                int planks = (int)(width / 0.45f);
                for (int k = 0; k < planks; k++)
                {
                    float x = -width * 0.5f + (k + 0.5f) * width / planks;
                    float a = random.Range(0f, 1.6f), c = random.Range(0f, 1.6f);
                    if (random.Chance(0.12f)) continue;
                    Kit.Plank(b, new Vector3(x, Ground + 0.05f + k * 0.012f, -length * 0.5f + a), new Vector3(x, Ground + 0.05f + k * 0.012f, length * 0.5f - c), Vector3.UnitY, 0.43f, 0.1f, skin, 0.01f);
                }
                for (int i = 0; i < (int)(length / 1.3f); i++)
                {
                    float z = -length * 0.5f + 0.6f + i * 1.3f;
                    Kit.Plank(b, new Vector3(-width * 0.55f, Ground + 0.2f, z), new Vector3(width * 0.55f, Ground + 0.2f, z + random.Range(-0.1f, 0.1f)), Vector3.UnitY, 0.2f, 0.2f, bleached, 0.02f);
                }
                return;
            }
            // A heap of timbers, beams and broken spars crossed every way.
            int count = random.Int(6, 10);
            for (int k = 0; k < count; k++)
            {
                float turn = random.Float() * MathF.PI;
                float l = random.Range(length * 0.3f, length * 0.6f);
                Vector3 dir = new Vector3(MathF.Cos(turn), 0f, MathF.Sin(turn));
                Vector3 centre = new Vector3(random.Range(-1f, 1f) * length * 0.2f, Ground + 0.15f + k * 0.12f, random.Range(-1f, 1f) * length * 0.2f);
                Vector3 a = centre - dir * l * 0.5f, c = centre + dir * l * 0.5f;
                a.Y = Ground + 0.12f;
                if (random.Chance(0.5f)) Kit.Pole(b, a, c, random.Range(0.12f, 0.24f), random.Chance(0.6f) ? bleached : Timber(random, 0.7f));
                else Kit.Plank(b, a, c, Vector3.UnitY, random.Range(0.25f, 0.45f), random.Range(0.15f, 0.3f), random.Chance(0.6f) ? bleached : Timber(random, 0.7f));
            }
        }

        /// <summary>An anchor dragged up the beach: a stocked anchor lying on its side, one fluke dug in, its chain run back along +z.</summary>
        public static void Anchor(MeshBuilder b, in PlaceSpec spec, WorldRandom random)
        {
            float chain = Math.Clamp(spec.Metres(0), 0f, 20f);
            bool reich = spec.Int(1) == 1;
            VertexMaterial iron = reich ? Iron(random) : LandIron(random);
            float shank = random.Range(3.2f, 4.2f);
            float y = Ground + 0.2f;
            // The shank along z, the crown at its foot, an arm curving out each side to a palm.
            Kit.Pole(b, new Vector3(0f, y, -shank * 0.5f), new Vector3(0f, y, shank * 0.5f), 0.14f, iron, 0.11f);
            foreach (float side in new[] { -1f, 1f })
            {
                var arm = new List<Vector3>();
                for (int k = 0; k <= 5; k++)
                {
                    float a = k / 5f * 1.25f;
                    arm.Add(new Vector3(side * MathF.Sin(a) * 1.4f, y - (side > 0f ? 0.12f * k : 0f), -shank * 0.5f + (1f - MathF.Cos(a)) * 1.4f));
                }
                Kit.Bend(b, arm, 0.15f, 0.1f, iron);
                Vector3 palm = arm[^1];
                Kit.Box(b, palm + new Vector3(0f, side > 0f ? -0.25f : 0f, 0.25f), new Vector3(1f, 0f, 0f), Vector3.UnitY, Vector3.UnitZ, new Vector3(0.45f, 0.06f, 0.5f), 0.02f, iron);
            }
            // The stock across its head, timber bound in iron on the land's, iron on the Reich's, and the ring.
            Vector3 head = new Vector3(0f, y, shank * 0.5f - 0.35f);
            Kit.Box(b, head + Vector3.UnitY * 0.45f, Vector3.UnitY, Vector3.UnitX, Vector3.UnitZ, new Vector3(1.6f, 0.16f, 0.16f), 0.02f, reich ? iron : Timber(random, 0.7f));
            b.Tube(head + new Vector3(0f, 0f, 0.5f), head + new Vector3(0f, 0f, 0.62f), 0.32f, 0.32f, 10, iron, capped: false);
            if (chain < 0.5f) return;
            // The chain, snaking back across the sand.
            Vector3 from = head + new Vector3(0f, 0f, 0.8f);
            float bend = random.Range(-1f, 1f);
            Vector3 last = from;
            int stretches = Math.Max(1, (int)(chain / 3f));
            for (int k = 1; k <= stretches; k++)
            {
                float t = k / (float)stretches;
                Vector3 next = new Vector3(MathF.Sin(t * 2.4f + bend) * 1.2f, Ground + 0.06f, from.Z + chain * t);
                Kit.Chain(b, last, next, 0.02f, 0.36f, iron);
                last = next;
            }
        }

        /// <summary>A ship's boat lying upturned on the sand, its keel up and its planks lapped, painted as she was; its oars beside it.</summary>
        public static void ShipBoat(MeshBuilder b, in PlaceSpec spec, WorldRandom random)
        {
            float length = Math.Clamp(spec.Metres(0), 4f, 9f);
            bool reich = spec.Int(1) == 1;
            float beam = length * random.Range(0.3f, 0.36f), depth = beam * random.Range(0.42f, 0.5f);
            VertexMaterial paint = reich ? Plating(random, 1.1f) : Painted(BoatPaints[random.Int(BoatPaints.Length)]);
            VertexMaterial inside = Timber(random, 0.45f);
            const int Lengthwise = 8, Up = 4;
            Vector3 P(float t, float s, float side)
            {
                // Upside down: its gunwale on the sand, its keel up.
                float plan = MathF.Pow(MathF.Max(0f, 1f - MathF.Pow(MathF.Abs(t - 0.5f) * 2f, 2.2f)), 0.55f);
                float half = beam * 0.5f * plan * MathF.Pow(1f - s, 0.6f) + 0.03f;
                return new Vector3(side * half, Ground + 0.04f + depth * s * (0.6f + 0.4f * plan), (t - 0.5f) * length);
            }
            for (int i = 0; i < Lengthwise; i++)
            {
                for (int j = 0; j < Up; j++)
                {
                    float t0 = i / (float)Lengthwise, t1 = (i + 1f) / Lengthwise, s0 = j / (float)Up, s1 = (j + 1f) / Up;
                    foreach (float side in new[] { -1f, 1f })
                    {
                        Vector3 a = P(t0, s0, side), c = P(t1, s0, side), d = P(t1, s1, side), e = P(t0, s1, side);
                        Along(b, a, c, d, e, new Vector3(side, 0.6f, 0f), j == 0 ? Painted(PlaceMaterials.BoatWhite) : paint);
                    }
                }
            }
            Kit.Plank(b, P(0.02f, 1f, 0f) + Vector3.UnitY * 0.05f, P(0.98f, 1f, 0f) + Vector3.UnitY * 0.05f, Vector3.UnitY, 0.16f, 0.14f, inside, 0.02f);
            foreach (float side in new[] { -1f, 1f })
            {
                Vector3 oar = new Vector3(side * (beam * 0.5f + random.Range(0.6f, 1.2f)), Ground + 0.06f, random.Range(-0.6f, 0.6f));
                float turn = random.Range(-0.3f, 0.3f);
                Vector3 along = new Vector3(MathF.Sin(turn), 0f, MathF.Cos(turn));
                Kit.Pole(b, oar - along * length * 0.35f, oar + along * length * 0.25f, 0.06f, Timber(random));
                Kit.Box(b, oar + along * length * 0.32f, Vector3.Cross(Vector3.UnitY, along), Vector3.UnitY, along, new Vector3(0.2f, 0.03f, 0.55f), 0.01f, Timber(random));
            }
        }

        /// <summary>One of the Reich's cages from a ship's hold, lying where it was washed up, burst open from inside: its bars bowed out round a hole, its door torn off beside it, straw and a chain spilling.</summary>
        public static void BurstCage(MeshBuilder b, in PlaceSpec spec, WorldRandom random)
        {
            float width = Math.Clamp(spec.Metres(0), 2.5f, 5f);
            float depth = width * 0.7f, high = width * 0.62f;
            VertexMaterial iron = Iron(random), rust = Rusted(random);
            // It lies on its back, so what was its top faces +z and its floor -z.
            Vector3 min = new Vector3(-width * 0.5f, Ground, -depth * 0.5f), max = new Vector3(width * 0.5f, Ground + high, depth * 0.5f);
            foreach (float x in new[] { min.X, max.X })
            {
                foreach (float z in new[] { min.Z, max.Z }) Kit.Pole(b, new Vector3(x, min.Y, z), new Vector3(x, max.Y, z), 0.11f, iron);
                foreach (float y in new[] { min.Y + 0.08f, max.Y }) Kit.Pole(b, new Vector3(x, y, min.Z), new Vector3(x, y, max.Z), 0.1f, iron);
            }
            foreach (float y in new[] { min.Y + 0.08f, max.Y })
            {
                foreach (float z in new[] { min.Z, max.Z }) Kit.Pole(b, new Vector3(min.X, y, z), new Vector3(max.X, y, z), 0.1f, iron);
            }
            // Bars on its sides; on the face up, which was its top, bowed out round where it broke.
            int bars = (int)(width / 0.38f);
            for (int i = 1; i < bars; i++)
            {
                float x = min.X + width * i / bars;
                float t = MathF.Abs(x) / (width * 0.5f);
                bool burst = t < 0.42f;
                Kit.Pole(b, new Vector3(x, min.Y + 0.08f, min.Z), new Vector3(x, max.Y, min.Z), 0.05f, i % 3 == 0 ? rust : iron);
                if (burst)
                {
                    float out_ = (1f - t / 0.42f) * random.Range(0.8f, 1.5f);
                    var bent = new List<Vector3> { new Vector3(x, min.Y + 0.08f, max.Z), new Vector3(x * (1f + out_ * 0.6f), min.Y + high * 0.3f, max.Z + out_), new Vector3(x * (1f + out_ * 0.3f), max.Y - high * 0.2f, max.Z + out_ * 0.6f), new Vector3(x, max.Y, max.Z) };
                    if (random.Chance(0.35f)) bent.RemoveAt(bent.Count - 1);
                    Kit.Bend(b, bent, 0.05f, 0.05f, iron);
                }
                else Kit.Pole(b, new Vector3(x, min.Y + 0.08f, max.Z), new Vector3(x, max.Y, max.Z), 0.05f, iron);
                Kit.Pole(b, new Vector3(x, max.Y, min.Z), new Vector3(x, max.Y, max.Z), 0.05f, iron);
            }
            Kit.Mark(b, new Vector3(0f, max.Y - 0.35f, min.Z - 0.04f), -Vector3.UnitX, Vector3.UnitY, 0.6f, 0.03f, random);
            // Its door, torn off and thrown down beside it.
            Vector3 door = new Vector3(max.X + 1.2f, Ground + 0.06f, random.Range(-0.5f, 0.5f));
            float dw = depth * 0.8f, dh = high * 0.8f;
            for (int k = 0; k <= 4; k++) Kit.Pole(b, door + new Vector3(-dw * 0.5f + dw * k / 4f, 0f, -dh * 0.5f), door + new Vector3(-dw * 0.5f + dw * k / 4f, 0f, dh * 0.5f), 0.05f, iron);
            Kit.Pole(b, door + new Vector3(-dw * 0.5f, 0f, -dh * 0.5f), door + new Vector3(dw * 0.5f, 0f, -dh * 0.5f), 0.07f, iron);
            Kit.Pole(b, door + new Vector3(-dw * 0.5f, 0f, dh * 0.5f), door + new Vector3(dw * 0.5f, 0f, dh * 0.5f), 0.07f, iron);
            // Straw spilling out of the hole, and the chain.
            Kit.Heap(b, new Vector3(0f, Ground, max.Z + 0.6f), new Vector2(width * 0.35f, 0.9f), 0.25f, 0f, Straw(random, 0.85f), random, 3, 10);
            Kit.Chain(b, new Vector3(min.X + 0.3f, Ground + 0.1f, 0f), new Vector3(random.Range(-1f, 1f), Ground + 0.06f, max.Z + 2.2f), 0.02f, 0.3f, iron);
        }

        /// <summary>What came out of a hold: crates, barrels, the Institute's amber in a split chest, or bales, tumbled where the sea left them.</summary>
        public static void Cargo(MeshBuilder b, in PlaceSpec spec, WorldRandom random, List<AssetLight> lights)
        {
            int form = spec.Int(0);
            int count = Math.Clamp(spec.Int(1), 1, 7);
            bool reich = spec.Int(2) == 1;
            VertexMaterial boards = reich ? Timber(random, 0.8f) : Timber(random, 1.05f), battens = reich ? Iron(random) : Timber(random, 0.7f);
            switch (form)
            {
                case 0:
                    for (int k = 0; k < count; k++)
                    {
                        float size = random.Range(0.55f, 0.85f);
                        Vector3 at = new Vector3(random.Range(-1.6f, 1.6f), Ground, random.Range(-1.6f, 1.6f));
                        float stack = k > 0 && random.Chance(0.3f) ? size * 2f : 0f;
                        Kit.Crate(b, at + Vector3.UnitY * (size + stack), random.Float() * MathF.PI, new Vector3(size, size, size * random.Range(0.8f, 1.2f)), boards, battens, random, reich && random.Chance(0.6f));
                    }
                    break;
                case 1:
                    for (int k = 0; k < count; k++)
                    {
                        Vector3 at = new Vector3(random.Range(-1.8f, 1.8f), Ground - 0.05f, random.Range(-1.8f, 1.8f));
                        Kit.Barrel(b, at, random.Float() * MathF.Tau, random.Chance(0.55f), random.Range(0.42f, 0.5f), random.Range(1.1f, 1.35f), Timber(random, 0.9f), reich ? Iron(random) : LandIron(random));
                    }
                    break;
                case 2:
                    {
                        // A chest of the Institute's amber, burst, the amber spilled over the sand catching what light there is.
                        Kit.Crate(b, new Vector3(0f, Ground + 0.55f, 0f), random.Range(-0.4f, 0.4f), new Vector3(1.1f, 0.55f, 0.7f), boards, Iron(random), random, marked: true);
                        VertexMaterial amber = Glass(new Vector3(0.62f, 0.32f, 0.05f));
                        for (int k = 0; k < 14; k++)
                        {
                            float a = random.Float() * MathF.Tau, r = random.Range(1.1f, 2.6f);
                            float s = random.Range(0.1f, 0.24f);
                            Kit.Block(b, new Vector3(MathF.Cos(a) * r, Ground + s * 0.6f, MathF.Sin(a) * r * 0.7f + 0.9f), new Vector3(s, s * 0.7f, s * 0.85f), random.Float() * MathF.Tau, s * 0.35f, k % 4 == 0 ? AmberGlow(0.6f) : amber);
                        }
                        lights.Add(Amber(new Vector3(0f, 0.8f, 1.2f), 14f, 5f));
                        break;
                    }
                default:
                    for (int k = 0; k < count; k++)
                    {
                        Vector3 at = new Vector3(random.Range(-1.6f, 1.6f), Ground + 0.5f, random.Range(-1.6f, 1.6f));
                        float turn = random.Float() * MathF.PI;
                        VertexMaterial wrap = Canvas(random, random.Range(0.75f, 1f));
                        Kit.Block(b, at, new Vector3(0.75f, 0.5f, 0.55f), turn, 0.2f, wrap);
                        Vector3 across = new Vector3(MathF.Cos(turn), 0f, -MathF.Sin(turn));
                        foreach (float s in new[] { -0.4f, 0.4f }) b.Tube(at + across * s - Vector3.UnitY * 0.52f, at + across * s + Vector3.UnitY * 0.52f, 0.03f, 0.03f, 4, Rope(random), capped: false);
                    }
                    break;
            }
        }

        /// <summary>A log the sea bleached and left at the tide line, its roots at one end.</summary>
        public static void Driftwood(MeshBuilder b, in PlaceSpec spec, WorldRandom random)
        {
            float length = Math.Clamp(spec.Metres(0), 2f, 12f);
            VertexMaterial wood = Bleached(random, 1.05f);
            float radius = random.Range(0.18f, 0.32f);
            var trunk = new List<Vector3>();
            for (int k = 0; k <= 4; k++) trunk.Add(new Vector3(MathF.Sin(k * 0.9f) * 0.25f, Ground + radius * 0.6f, -length * 0.5f + length * k / 4f));
            Kit.Bend(b, trunk, radius, radius * 0.55f, wood);
            for (int k = 0; k < 6; k++)
            {
                float a = k * MathF.Tau / 6f + random.Range(-0.3f, 0.3f);
                Vector3 foot = trunk[0];
                Kit.Pole(b, foot, foot + new Vector3(MathF.Cos(a) * random.Range(0.8f, 1.6f), MathF.Sin(a) * random.Range(0.5f, 1.2f) + 0.2f, -random.Range(0.1f, 0.5f)), radius * 0.35f, wood, 0.03f);
            }
        }

        /// <summary>The wreckers' sheerlegs over the break, two spars lashed at their head with a tackle hanging from it and a crate in its sling, a stay back to a stake, and the rope coiled.</summary>
        public static void Salvage(MeshBuilder b, in PlaceSpec spec, WorldRandom random)
        {
            float height = Math.Clamp(spec.Metres(0), 5f, 12f);
            VertexMaterial wood = Weathered(random), rope = Rope(random);
            Vector3 head = new Vector3(0f, height, 0.9f);
            foreach (float side in new[] { -1f, 1f }) Kit.Pole(b, new Vector3(side * 2.2f, -0.2f, -0.6f), head, 0.17f, wood, 0.13f);
            b.Tube(head - new Vector3(0.3f, 0f, 0f), head + new Vector3(0.3f, 0f, 0f), 0.24f, 0.24f, 6, rope, capped: true);
            // The tackle: a block under the head, the fall down to the crate, and the hauling part off to where they pull.
            Vector3 block = head - new Vector3(0f, 0.7f, 0f);
            Kit.Box(b, block, Vector3.UnitX, Vector3.UnitY, Vector3.UnitZ, new Vector3(0.14f, 0.32f, 0.22f), 0.03f, wood);
            float hang = random.Range(0.8f, 2.4f);
            Vector3 load = new Vector3(0f, Ground + hang + 0.7f, 0.9f);
            Kit.Rope(b, block - Vector3.UnitY * 0.3f, load + Vector3.UnitY * 0.85f, 0f, 0.05f, rope, 1);
            Kit.Crate(b, load, random.Range(-0.3f, 0.3f), new Vector3(0.75f, 0.65f, 0.65f), Timber(random), Timber(random, 0.7f), random);
            Kit.Rope(b, block, new Vector3(random.Range(-1f, 1f), Ground + 0.9f, -6.5f), 0.3f, 0.05f, rope, 6);
            Kit.Rope(b, head, new Vector3(0f, Ground + 0.5f, -8.5f), 0.4f, 0.05f, rope, 6);
            Kit.Pole(b, new Vector3(0f, -0.3f, -8.5f), new Vector3(0f, Ground + 0.65f, -8.5f), 0.1f, wood);
            Kit.Coil(b, new Vector3(1.6f, Ground, -4.5f), 0.65f, rope, random);
        }
    }
}
