using Dino_Engine.Modelling.Model;
using Dino_Engine.Textures;
using Dino_Engine.Util;
using Dino_Engine.World;
using OpenTK.Mathematics;
using static Dino_Engine.Modelling.Procedural.Architecture.Places.PlaceMaterials;

namespace Dino_Engine.Modelling.Procedural.Architecture.Places
{
    /// <summary>What a place's ground is littered with, which is what says from 33 m up what's done where: chips round a sawpit, straw round a pen, bones round a nest.</summary>
    public enum LitterKind
    {
        Chips,      // Wood chips, bark curls and sawn offcuts.
        Straw,      // Loose straw and hay, in wisps and tufts.
        Bones,      // Bones and gnawed scraps, a small beast's skull among them.
        Spalls,     // Stone chips off a mason's chisel, of the stone an id carries.
        Rubble,     // Broken masonry, of the stone an id carries.
        Embers,     // Charcoal, ash and burnt ends, where a fire was.
        Shells,     // Shells, fish bones, cork floats and a bit of torn net.
        Feathers,   // Feathers and shards of eggshell.
        Papers,     // Papers blown about, a torn notice with the Reich's seal among them.
        Snow,       // Lumps of packed snow and shards of ice.
        Gear,       // What a fight leaves: arrows stuck in the ground, a broken spear, a dented helm, a split shield.
        Blood,      // Blood soaked into the ground where something was killed or butchered, dark, and the drops thrown off it: what shows on snow from anywhere.
    }

    /// <summary>
    /// What lies on the ground of a place with a story of its own (PLACES.md) and says what happened there: litter of its own kind spread over a patch, puddles standing in a yard's
    /// ruts, duckboards over the mud. All of it lies low, below <c>Sight.Above</c>, and starts where the ground is drawn (<see cref="PlaceMaterials.Ground"/>), as it's laid with
    /// <c>PlaceBuilder.Lay</c> and walked through. Headless.
    /// </summary>
    internal static class Underfoot
    {
        /// <summary>
        /// Litter of a kind over a disc: thickest at its middle and thinning to its edge, as anything worked at drops most where it's worked. Numbers: kind (<see cref="LitterKind"/>),
        /// radius, how many pieces, and a stone's colour (r, g, b per mille) for spalls and rubble.
        /// </summary>
        public static void Litter(MeshBuilder b, in PlaceSpec spec, WorldRandom random)
        {
            var kind = (LitterKind)Math.Clamp(spec.Int(0), 0, (int)LitterKind.Blood);
            float radius = Math.Clamp(spec.Metres(1), 0.6f, 9f);
            int count = Math.Clamp(spec.Int(2), 4, 220);
            Vector3 stone = spec.Count >= 6 ? StoneColour(spec.Int(3), spec.Int(4), spec.Int(5)) : new Vector3(0.5f, 0.48f, 0.45f);
            for (int i = 0; i < count; i++)
            {
                // Nearer the middle the more there is: the square root spreads them evenly, and the power gathers them in.
                float r = radius * MathF.Pow(random.Float(), 0.75f);
                float a = random.Float() * MathF.Tau;
                Vector3 at = new Vector3(MathF.Cos(a) * r, Ground, MathF.Sin(a) * r);
                float yaw = random.Float() * MathF.Tau;
                switch (kind)
                {
                    case LitterKind.Chips: Chip(b, at, yaw, random); break;
                    case LitterKind.Straw: Wisp(b, at, yaw, random); break;
                    case LitterKind.Bones: BoneBit(b, at, yaw, random, i == 0); break;
                    case LitterKind.Spalls: Spall(b, at, yaw, random, stone, 0.05f, 0.16f); break;
                    case LitterKind.Rubble: Spall(b, at, yaw, random, stone, 0.14f, 0.42f, dressed: true); break;
                    case LitterKind.Embers: Ember(b, at, yaw, random, i == 0, radius); break;
                    case LitterKind.Shells: ShellBit(b, at, yaw, random); break;
                    case LitterKind.Feathers: Feather(b, at, yaw, random); break;
                    case LitterKind.Papers: Paper(b, at, yaw, random); break;
                    case LitterKind.Snow: SnowBit(b, at, yaw, random); break;
                    case LitterKind.Gear: GearBit(b, at, yaw, random); break;
                    case LitterKind.Blood: BloodBit(b, at, yaw, random); break;
                }
            }
        }

        private static Vector3 Along(float yaw) => new Vector3(MathF.Cos(yaw), 0f, -MathF.Sin(yaw));

        private static Vector3 Across(float yaw) => new Vector3(MathF.Sin(yaw), 0f, MathF.Cos(yaw));

        // An irregular block lying on the ground: a box whose corners are each pushed about, so no two are alike, and which sits in the ground a little.
        private static void Lump(MeshBuilder b, Vector3 foot, float yaw, Vector3 half, float jitter, float chamfer, Func<Vector3, VertexMaterial> m, WorldRandom random, float tilt = 0.2f)
        {
            Vector3 ax = Along(yaw), az = Across(yaw);
            Vector3 up = Vector3.Normalize(Vector3.UnitY + ax * random.Range(-tilt, tilt) + az * random.Range(-tilt, tilt));
            ax = Vector3.Normalize(ax - up * Vector3.Dot(ax, up));
            az = Vector3.Cross(ax, up);
            Vector3 centre = foot + up * (half.Y * 0.7f);
            Vector3 J() => new Vector3(random.Range(-jitter, jitter), random.Range(-jitter, jitter), random.Range(-jitter, jitter));
            Vector3 C(float x, float y, float z) => centre + ax * (x * half.X) + up * (y * half.Y) + az * (z * half.Z) + J() * half.Length;
            Kit.Solid(b, new[] { C(-1, -1, 1), C(1, -1, 1), C(1, 1, 1), C(-1, 1, 1), C(-1, -1, -1), C(1, -1, -1), C(1, 1, -1), C(-1, 1, -1) }, chamfer, m);
        }

        private static void Chip(MeshBuilder b, Vector3 at, float yaw, WorldRandom random)
        {
            float roll = random.Float();
            if (roll < 0.62f)
            {
                // A chip off an axe: a flake of pale wood.
                Vector3 half = new Vector3(random.Range(0.05f, 0.13f), random.Range(0.008f, 0.02f), random.Range(0.03f, 0.07f));
                Kit.Block(b, at + Vector3.UnitY * half.Y, half, yaw, 0f, Fresh(random, random.Range(0.85f, 1.15f)));
            }
            else if (roll < 0.88f)
            {
                // A curl of bark.
                Vector3 half = new Vector3(random.Range(0.08f, 0.2f), 0.015f, random.Range(0.04f, 0.08f));
                Kit.Block(b, at + Vector3.UnitY * 0.02f, half, yaw, 0f, new VertexMaterial(MaterialCatalog.BarkFurrowed.Index, new Colour(new Vector3(0.36f, 0.3f, 0.25f) * random.Range(0.8f, 1.1f))));
            }
            else
            {
                // An offcut: a short round of the trunk, lying on its side.
                float r = random.Range(0.08f, 0.16f), l = random.Range(0.15f, 0.35f);
                Vector3 ax = Along(yaw);
                Kit.Lathe(b, at + Vector3.UnitY * r - ax * (l * 0.5f), ax, l, _ => r, 7, 1, Fresh(random, 0.9f));
            }
        }

        private static void Wisp(MeshBuilder b, Vector3 at, float yaw, WorldRandom random)
        {
            // Straw from up here is strokes of pale gold, a few crossed in each tuft.
            int strokes = random.Int(2, 4);
            VertexMaterial straw = Straw(random, random.Range(0.85f, 1.2f));
            for (int k = 0; k < strokes; k++)
            {
                float y = yaw + random.Range(-0.6f, 0.6f);
                float l = random.Range(0.25f, 0.6f);
                Vector3 d = Along(y) * (l * 0.5f);
                Vector3 o = at + new Vector3(random.Range(-0.08f, 0.08f), 0.012f + k * 0.006f, random.Range(-0.08f, 0.08f));
                Kit.Plank(b, o - d, o + d + Vector3.UnitY * random.Range(0f, 0.04f), Vector3.UnitY, random.Range(0.025f, 0.05f), 0.008f, straw, 0f);
            }
        }

        private static void BoneBit(MeshBuilder b, Vector3 at, float yaw, WorldRandom random, bool skull)
        {
            VertexMaterial bone = Bone(random, random.Range(0.85f, 1.1f));
            if (skull)
            {
                // A small beast's skull: a long snout, its eyes dark.
                Vector3 ax = Along(yaw);
                Kit.Lathe(b, at + Vector3.UnitY * 0.1f - ax * 0.2f, ax, 0.5f, Kit.Profile((0f, 0.12f), (0.35f, 0.14f), (0.55f, 0.08f), (1f, 0.03f)), 7, 4, bone);
                Vector3 eye = at + Vector3.UnitY * 0.17f - ax * 0.05f;
                foreach (float s in new[] { -1f, 1f }) Kit.Block(b, eye + Across(yaw) * (s * 0.08f), new Vector3(0.035f), yaw, 0f, Dark);
                return;
            }
            if (random.Chance(0.75f))
            {
                // A long bone, a knob at each end.
                float l = random.Range(0.25f, 0.7f), r = random.Range(0.02f, 0.045f);
                Vector3 d = Along(yaw) * (l * 0.5f);
                Vector3 o = at + Vector3.UnitY * r;
                Kit.Pole(b, o - d, o + d, r, bone);
                Kit.Block(b, o - d, new Vector3(r * 1.7f, r * 1.4f, r * 1.9f), yaw, 0f, bone);
                Kit.Block(b, o + d, new Vector3(r * 1.5f, r * 1.3f, r * 1.7f), yaw, 0f, bone);
            }
            else
            {
                // A rib, curved, lying on its side.
                float l = random.Range(0.5f, 1f);
                var points = new List<Vector3>();
                for (int k = 0; k <= 4; k++)
                {
                    float t = k / 4f;
                    points.Add(at + Along(yaw) * (l * (t - 0.5f)) + Across(yaw) * (MathF.Sin(t * MathF.PI) * l * 0.25f) + Vector3.UnitY * 0.03f);
                }
                Kit.Bend(b, points, 0.03f, 0.015f, bone);
            }
        }

        private static void Spall(MeshBuilder b, Vector3 at, float yaw, WorldRandom random, Vector3 stone, float least, float most, bool dressed = false)
        {
            float s = random.Range(least, most);
            Vector3 half = new Vector3(s * random.Range(0.7f, 1.3f), s * random.Range(0.35f, 0.7f), s * random.Range(0.6f, 1.1f));
            Vector3 colour = stone * random.Range(0.86f, 1.1f);
            // A dressed block that broke keeps its faces: its top is pale with weather, its sides the stone.
            Func<Vector3, VertexMaterial> m = dressed
                ? n => n.Y > 0.6f ? Tint(Dressed(colour, random), new Vector3(0.95f, 1f, 0.9f)) : Dressed(colour, random, 0.6f)
                : _ => CutStone(colour);
            Lump(b, at, yaw, half, dressed ? 0.12f : 0.22f, dressed ? 0.02f : 0f, m, random, dressed ? 0.3f : 0.15f);
        }

        private static void Ember(MeshBuilder b, Vector3 at, float yaw, WorldRandom random, bool heap, float radius)
        {
            if (heap)
            {
                // The ash where the fire was, grey over black.
                Kit.Heap(b, new Vector3(0f, Ground - 0.04f, 0f), new Vector2(radius * 0.45f, radius * 0.38f), 0.12f, yaw, Flat(new Vector3(0.24f, 0.23f, 0.22f)), random, 3, 12);
                return;
            }
            if (random.Chance(0.7f))
            {
                float s = random.Range(0.04f, 0.1f);
                Lump(b, at, yaw, new Vector3(s, s * 0.7f, s * 0.9f), 0.2f, 0f, _ => Flat(new Vector3(0.05f, 0.045f, 0.04f) * random.Range(0.8f, 1.3f)), random);
            }
            else
            {
                // A burnt end of a branch.
                float l = random.Range(0.3f, 0.8f);
                Vector3 d = Along(yaw) * (l * 0.5f);
                Kit.Pole(b, at + Vector3.UnitY * 0.05f - d, at + Vector3.UnitY * 0.05f + d, random.Range(0.03f, 0.06f), new VertexMaterial(MaterialCatalog.BarkCharred.Index, new Colour(0.14f, 0.12f, 0.1f)));
            }
        }

        private static void ShellBit(MeshBuilder b, Vector3 at, float yaw, WorldRandom random)
        {
            float roll = random.Float();
            if (roll < 0.55f)
            {
                // A shell, a little cone on its side, pale.
                float r = random.Range(0.04f, 0.09f);
                Vector3 colour = Vector3.Lerp(new Vector3(0.78f, 0.74f, 0.66f), new Vector3(0.62f, 0.56f, 0.5f), random.Float());
                Kit.Lathe(b, at + Vector3.UnitY * (r * 0.4f), Along(yaw) + Vector3.UnitY * 0.3f, r * 1.6f, Kit.Profile((0f, r), (1f, 0f)), 7, 1, Flat(colour));
            }
            else if (roll < 0.8f)
            {
                // A fish's backbone, its ribs off it.
                float l = random.Range(0.25f, 0.45f);
                Vector3 ax = Along(yaw), az = Across(yaw);
                Vector3 o = at + Vector3.UnitY * 0.015f;
                VertexMaterial bone = Flat(new Vector3(0.72f, 0.7f, 0.64f));
                Kit.Plank(b, o - ax * (l * 0.5f), o + ax * (l * 0.5f), Vector3.UnitY, 0.012f, 0.01f, bone, 0f);
                for (int k = 1; k < 6; k++)
                {
                    Vector3 p = o + ax * (l * (k / 6f - 0.5f));
                    foreach (float s in new[] { -1f, 1f }) Kit.Plank(b, p, p + az * (s * 0.05f) - ax * 0.02f, Vector3.UnitY, 0.006f, 0.006f, bone, 0f);
                }
                // Its head.
                Kit.Block(b, o + ax * (l * 0.5f + 0.03f), new Vector3(0.04f, 0.015f, 0.025f), yaw, 0f, bone);
            }
            else
            {
                // A cork float, and a bit of net off it.
                Kit.Lathe(b, at + Vector3.UnitY * 0.04f - Along(yaw) * 0.06f, Along(yaw), 0.12f, _ => 0.045f, 7, 1, Flat(new Vector3(0.48f, 0.32f, 0.18f)));
                Vector3 o = at + Vector3.UnitY * 0.012f;
                Kit.Face(b, o - Along(yaw) * 0.3f + Across(yaw) * 0.25f, o + Along(yaw) * 0.25f + Across(yaw) * 0.3f, o + Along(yaw) * 0.3f - Across(yaw) * 0.2f, o - Along(yaw) * 0.2f - Across(yaw) * 0.28f, Vector3.UnitY, Net(random, 0.9f));
            }
        }

        private static void Feather(MeshBuilder b, Vector3 at, float yaw, WorldRandom random)
        {
            Vector3 ax = Along(yaw), az = Across(yaw);
            Vector3 o = at + Vector3.UnitY * 0.012f;
            if (random.Chance(0.55f))
            {
                // A feather: a vane either side of its quill, mottled as a wild bird's or a feathered beast's are.
                float l = random.Range(0.14f, 0.32f), w = l * 0.22f;
                Vector3 colour = Vector3.Lerp(new Vector3(0.36f, 0.3f, 0.24f), new Vector3(0.62f, 0.58f, 0.5f), random.Float());
                Kit.Face(b, o - ax * (l * 0.5f), o + ax * (l * 0.5f), o + ax * (l * 0.1f) + az * w, o - ax * (l * 0.3f) + az * (w * 0.6f), Vector3.UnitY, Flat(colour));
                Kit.Face(b, o - ax * (l * 0.5f), o - ax * (l * 0.3f) - az * (w * 0.6f), o + ax * (l * 0.1f) - az * w, o + ax * (l * 0.5f), Vector3.UnitY, Flat(colour * 0.85f));
                return;
            }
            // A shard of eggshell, curved, pale and freckled.
            float s = random.Range(0.04f, 0.09f);
            Vector3 shell = Vector3.Lerp(new Vector3(0.82f, 0.78f, 0.68f), new Vector3(0.7f, 0.72f, 0.64f), random.Float());
            Kit.Face(b, o + ax * s, o + az * s * 0.8f + Vector3.UnitY * 0.02f, o - ax * s * 0.9f, Vector3.UnitY, Flat(shell));
            Kit.Face(b, o + ax * s, o - ax * s * 0.9f, o - az * s * 0.7f + Vector3.UnitY * 0.015f, Vector3.UnitY, Flat(shell * 0.92f));
        }

        private static void Paper(MeshBuilder b, Vector3 at, float yaw, WorldRandom random)
        {
            Vector3 ax = Along(yaw), az = Across(yaw);
            float w = random.Range(0.12f, 0.2f), h = random.Range(0.16f, 0.26f);
            Vector3 o = at + Vector3.UnitY * random.Range(0.01f, 0.03f);
            // A sheet lying curled: one corner lifted.
            Kit.Face(b, o - ax * w - az * h, o + ax * w - az * h, o + ax * w + az * h + Vector3.UnitY * random.Range(0f, 0.05f), o - ax * w + az * h, Vector3.UnitY, Plaster(random.Range(0.9f, 1.05f)));
            if (random.Chance(0.3f)) Kit.Face(b, o - ax * 0.04f + az * (h * 0.5f) + Vector3.UnitY * 0.004f, o + ax * 0.04f + az * (h * 0.5f) + Vector3.UnitY * 0.004f, o + ax * 0.04f + az * (h * 0.5f + 0.07f) + Vector3.UnitY * 0.004f, o - ax * 0.04f + az * (h * 0.5f + 0.07f) + Vector3.UnitY * 0.004f, Vector3.UnitY, OxbloodPaint());
        }

        private static void SnowBit(MeshBuilder b, Vector3 at, float yaw, WorldRandom random)
        {
            if (random.Chance(0.7f))
            {
                float r = random.Range(0.08f, 0.22f);
                Kit.Ball(b, at + Vector3.UnitY * (r * 0.5f), new Vector3(r, r * 0.75f, r * random.Range(0.8f, 1.1f)), 7, 4, PackedSnow(random.Range(0.95f, 1.03f)));
                return;
            }
            // A shard of ice, standing up out of the snow at a slant.
            float s = random.Range(0.06f, 0.14f);
            Lump(b, at, yaw, new Vector3(s * 0.5f, s * 2.2f, s * 0.4f), 0.15f, 0f, _ => Ice(random.Range(0.9f, 1.1f)), random, 0.5f);
        }

        // A splash of blood soaked into the ground, ragged at its edge, and the drops thrown off beyond it: low domes rather than flat discs, as the ground drawn under a piece is
        // never as flat as the piece, and snow least of all, and a disc a few centimetres up is under it as often as on it.
        private static void BloodBit(MeshBuilder b, Vector3 at, float yaw, WorldRandom random)
        {
            // Red rather than a butcher's brown, as it's seen in snow glare or in a forest's gloom.
            VertexMaterial blood = new VertexMaterial(MaterialCatalog.Leather.Index, new Colour(new Vector3(0.42f, 0.05f, 0.04f) * random.Range(0.6f, 0.9f)));
            float size = random.Range(0.14f, 0.4f);
            Vector3 along = new Vector3(MathF.Cos(yaw), 0f, MathF.Sin(yaw));
            Kit.Ball(b, at + Vector3.UnitY * 0.04f, new Vector3(size, 0.08f, size * random.Range(0.55f, 0.9f)), 9, 3, blood);
            // A second lobe where it ran, for a ragged edge.
            Kit.Ball(b, at + along * size * random.Range(0.5f, 0.8f) + Vector3.UnitY * 0.04f, new Vector3(size * 0.55f, 0.07f, size * 0.45f), 7, 2, blood);
            int drops = random.Int(1, 3);
            for (int d = 0; d < drops; d++)
            {
                float a = random.Float() * MathF.Tau, r = size * random.Range(1.3f, 2.2f), s = random.Range(0.04f, 0.08f);
                Kit.Ball(b, at + new Vector3(MathF.Cos(a) * r, 0.04f, MathF.Sin(a) * r), new Vector3(s, 0.06f, s), 5, 2, blood);
            }
        }

        private static void GearBit(MeshBuilder b, Vector3 at, float yaw, WorldRandom random)
        {
            Vector3 ax = Along(yaw), az = Across(yaw);
            float roll = random.Float();
            if (roll < 0.55f)
            {
                // An arrow stuck in the ground at a slant, its fletching pale.
                Vector3 tilt = Vector3.Normalize(Vector3.UnitY * random.Range(0.6f, 1f) + ax * random.Range(0.2f, 0.7f));
                Vector3 foot = at + Vector3.UnitY * 0.02f;
                Vector3 top = foot + tilt * random.Range(0.45f, 0.7f);
                Kit.Pole(b, foot - tilt * 0.12f, top, 0.012f, Timber(random, 1.1f));
                Kit.Face(b, top - tilt * 0.12f, top - tilt * 0.12f + az * 0.035f, top + az * 0.02f, top, az, Flat(new Vector3(0.66f, 0.64f, 0.58f)));
                return;
            }
            if (roll < 0.75f)
            {
                // A spear snapped in two, its head still on one half.
                float l = random.Range(0.8f, 1.3f);
                Vector3 o = at + Vector3.UnitY * 0.03f;
                Kit.Pole(b, o - ax * l, o - ax * 0.05f, 0.025f, Timber(random));
                Vector3 bent = Vector3.Normalize(ax + az * random.Range(-0.6f, 0.6f));
                Kit.Pole(b, o + bent * 0.05f, o + bent * l * 0.8f, 0.025f, Timber(random));
                Kit.Lathe(b, o + bent * l * 0.8f, bent, 0.22f, Kit.Profile((0f, 0.035f), (0.3f, 0.045f), (1f, 0f)), 4, 2, LandIron(random));
                return;
            }
            if (roll < 0.9f)
            {
                // A shield on its back, split, its boss rusting.
                float r = random.Range(0.35f, 0.45f);
                Vector3 o = at + Vector3.UnitY * 0.05f;
                Vector3 colour = BoatPaints[random.Int(BoatPaints.Length)] * 1.2f;
                Kit.Lathe(b, o, Vector3.UnitY, 0.04f, _ => r, 12, 1, Painted(colour));
                Kit.Lathe(b, o + Vector3.UnitY * 0.04f, Vector3.UnitY, 0.06f, Kit.Profile((0f, 0.09f), (1f, 0.04f)), 8, 1, LandIron(random));
                Kit.Plank(b, o + Vector3.UnitY * 0.042f - ax * r * 0.9f, o + Vector3.UnitY * 0.042f + ax * r * 0.9f, Vector3.UnitY, 0.02f, 0.004f, Dark, 0f);
                return;
            }
            // A helm, dented, on its side.
            Kit.Lathe(b, at + Vector3.UnitY * 0.14f - ax * 0.1f, ax, 0.26f, Kit.Profile((0f, 0.14f), (0.6f, 0.13f), (1f, 0.02f)), 9, 3, LandIron(random, 1.2f));
        }

        /// <summary>Water standing in a hollow of a yard after rain, dark and still, with a rim of churned mud round it. Numbers: radius.</summary>
        public static void Puddle(MeshBuilder b, in PlaceSpec spec, WorldRandom random)
        {
            float radius = Math.Clamp(spec.Metres(0), 0.5f, 5f);
            const int Around = 14;
            var edge = new float[Around];
            for (int k = 0; k < Around; k++) edge[k] = random.Range(0.65f, 1.15f);
            Vector3 At(int k, float scale, float y)
            {
                float a = k * MathF.Tau / Around;
                // Long, as a rut is: wider one way than the other.
                return new Vector3(MathF.Cos(a) * radius * 1.3f, y, MathF.Sin(a) * radius * 0.75f) * new Vector3(edge[k % Around] * scale, 1f, edge[k % Around] * scale);
            }
            VertexMaterial water = new VertexMaterial(MaterialCatalog.Mirror.Index, new Colour(0.035f, 0.04f, 0.042f));
            VertexMaterial mud = new VertexMaterial(MaterialCatalog.Mud.Index, new Colour(new Vector3(0.22f, 0.17f, 0.12f) * random.Range(0.9f, 1.1f)));
            Vector3 middle = new Vector3(0f, Ground + 0.012f, 0f);
            for (int k = 0; k < Around; k++)
            {
                Kit.Face(b, middle, At(k, 1f, Ground + 0.012f), At(k + 1, 1f, Ground + 0.012f), Vector3.UnitY, water);
                // The rim, a little proud of the water and falling to the ground.
                Kit.Face(b, At(k, 1f, Ground + 0.012f), At(k, 1.35f, Ground + 0.005f), At(k + 1, 1.35f, Ground + 0.005f), At(k + 1, 1f, Ground + 0.012f), Vector3.UnitY, mud);
            }
        }

        /// <summary>Boards laid over the mud on two runners to keep feet out of it, some split or gone. Numbers: length, width.</summary>
        public static void Duckboards(MeshBuilder b, in PlaceSpec spec, WorldRandom random)
        {
            float length = Math.Clamp(spec.Metres(0), 1.5f, 14f), width = Math.Clamp(spec.Metres(1), 0.6f, 2.2f);
            VertexMaterial runner = Weathered(random, 0.75f);
            foreach (float s in new[] { -1f, 1f }) Kit.Plank(b, new Vector3(s * width * 0.36f, Ground + 0.03f, -length * 0.5f), new Vector3(s * width * 0.36f, Ground + 0.03f, length * 0.5f), Vector3.UnitY, 0.12f, 0.08f, runner);
            for (float z = -length * 0.5f + 0.15f; z < length * 0.5f - 0.1f; z += random.Range(0.26f, 0.34f))
            {
                if (random.Chance(0.06f)) continue;
                float shade = random.Range(0.75f, 1.05f);
                bool split = random.Chance(0.1f);
                float half = width * 0.5f * (split ? random.Range(0.4f, 0.7f) : 1f);
                float off = split ? random.Range(-0.2f, 0.2f) : random.Range(-0.04f, 0.04f);
                Kit.Plank(b, new Vector3(-half + off, Ground + 0.085f, z), new Vector3(half + off, Ground + 0.085f + random.Range(-0.01f, 0.01f), z + random.Range(-0.03f, 0.03f)), Vector3.UnitY, 0.22f, 0.035f, Weathered(random, shade));
            }
        }
    }
}
