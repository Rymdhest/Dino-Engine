using Dino_Engine.Modelling.Model;
using Dino_Engine.Textures;
using Dino_Engine.Util;
using Dino_Engine.World;
using Dino_Engine.World.Content;
using OpenTK.Mathematics;
using static Dino_Engine.Modelling.Procedural.Architecture.Places.PlaceMaterials;

namespace Dino_Engine.Modelling.Procedural.Architecture.Places
{
    /// <summary>What a fossil is the bones of: how its skeleton is laid out.</summary>
    public enum FossilForm
    {
        Theropod,       // A tyrant or a ravager: a great skull, short arms, long legs, a long tail.
        Horned,         // A hornback: a skull with a frill and horns, four legs, a short tail.
        Plated,         // A spikeback: a small skull, plates down its back and spikes on its tail.
        Armoured,       // A clubtail: a broad body under plates, a club on its tail.
        Sauropod,       // One of the long-necks: a small skull on a neck as long as its tail.
    }

    /// <summary>
    /// The Ossuary's excavation (PLACES.md): one of the great beasts as it lies in the rock, in the death pose its kind died in, its neck thrown back over its spine and its tail arched, its
    /// ribs standing up out of the floor of the pit, partly freed and partly still in the rock; the strings of the grid it's measured by; boards shoring the pit's sides; the sieve the spoil
    /// is shaken through; bones jacketed in plaster for the journey and crates of them stencilled with the Reich's mark; the spoil heaped round; the table where what's found is drawn and
    /// numbered; and the tripod a bone is coming up on. The Ossuary's cold light is in the skull's eyes already. Headless.
    /// </summary>
    internal static class Excavation
    {
        public static FossilForm FormOf(string species) => species switch
        {
            "hornback" => FossilForm.Horned,
            "spikeback" => FossilForm.Plated,
            "clubtail" => FossilForm.Armoured,
            "giant" => FossilForm.Sauropod,
            _ => FossilForm.Theropod,
        };

        /// <summary>How long a fossil of a form is, at the world's scale.</summary>
        public static float LengthOf(FossilForm form, WorldRandom random) => form switch
        {
            FossilForm.Sauropod => random.Range(20f, 24f),
            FossilForm.Theropod => random.Range(13f, 16f),
            FossilForm.Armoured => random.Range(10f, 12f),
            _ => random.Range(11f, 13f),
        };

        // The share of its length each part of a form takes, tail to head: tail, body, neck, skull.
        private static (float Tail, float Body, float Neck, float Skull) Proportions(FossilForm form) => form switch
        {
            FossilForm.Sauropod => (0.4f, 0.17f, 0.39f, 0.04f),
            FossilForm.Horned => (0.28f, 0.34f, 0.07f, 0.31f),
            FossilForm.Plated => (0.4f, 0.36f, 0.14f, 0.1f),
            FossilForm.Armoured => (0.36f, 0.42f, 0.08f, 0.14f),
            _ => (0.46f, 0.22f, 0.15f, 0.17f),
        };

        /// <summary>
        /// One of the great beasts in the rock, lying on its side on the floor of the pit with its spine along z: freed from the head back by the share of it that's freed, the rest still under the
        /// rock it lies in. Model y = 0 is the floor of the pit.
        /// </summary>
        public static void Fossil(MeshBuilder b, in PlaceSpec spec, WorldRandom random, List<AssetLight> lights)
        {
            var form = (FossilForm)Math.Clamp(spec.Int(0), 0, (int)FossilForm.Sauropod);
            float length = Math.Clamp(spec.Metres(1), 6f, 26f);
            float freed = Math.Clamp(spec.Share(2), 0.2f, 1f);
            (float tail, float body, float neck, float skull) = Proportions(form);
            VertexMaterial bone = PlaceMaterials.Fossil(random), dark = PlaceMaterials.Fossil(random, 0.78f);
            float thick = length * (form == FossilForm.Sauropod ? 0.022f : 0.028f);

            // The spine, as a line on the ground from the tail's tip to the back of the skull: the body along z, the tail arching away one way and the neck thrown back over the
            // spine the other, as a beast's is when it dies and dries.
            float arch = random.Range(0.5f, 1.1f), throwBack = form == FossilForm.Sauropod ? random.Range(1.6f, 2.4f) : form == FossilForm.Theropod ? random.Range(1.4f, 2.1f) : random.Range(0.4f, 0.9f);
            var spine = new List<Vector3>();
            var widths = new List<float>();
            float bodyLength = length * body, tailLength = length * tail, neckLength = length * neck;
            // Tail, from its tip at s = 0 to the hips.
            const int TailSteps = 14, NeckSteps = 9, BodySteps = 8;
            for (int i = 0; i <= TailSteps; i++)
            {
                float t = i / (float)TailSteps;
                float along = (1f - t) * tailLength;
                float bend = arch * (1f - t) * (1f - t);
                spine.Add(new Vector3(MathF.Sin(bend) * along * 0.45f, 0f, -along * MathF.Cos(bend * 0.6f)));
                widths.Add(thick * (0.25f + 0.75f * t));
            }
            for (int i = 1; i <= BodySteps; i++)
            {
                float t = i / (float)BodySteps;
                spine.Add(new Vector3(MathF.Sin(t * MathF.PI) * bodyLength * 0.04f, 0f, t * bodyLength));
                widths.Add(thick * (1f + 0.15f * MathF.Sin(t * MathF.PI)));
            }
            Vector3 shoulders = spine[^1];
            float heading = 0f;
            Vector3 at = shoulders;
            for (int i = 1; i <= NeckSteps; i++)
            {
                float t = i / (float)NeckSteps;
                heading = throwBack * t * t;
                Vector3 step = new Vector3(-MathF.Sin(heading), 0f, MathF.Cos(heading)) * (neckLength / NeckSteps);
                at += step;
                spine.Add(at);
                widths.Add(thick * (1f - 0.35f * t) * (form == FossilForm.Sauropod ? 0.8f : 1f));
            }
            int hips = TailSteps, neckStart = TailSteps + BodySteps;
            Vector3 skullDir = new Vector3(-MathF.Sin(heading), 0f, MathF.Cos(heading));

            // The vertebrae down it, each a short drum with its spine standing off it on the side its back was.
            for (int i = 0; i < spine.Count - 1; i++)
            {
                Vector3 a = spine[i], c = spine[i + 1];
                Vector3 dir = c - a;
                float l = dir.Length;
                if (l < 1e-3f) continue;
                dir /= l;
                Vector3 back = new Vector3(-dir.Z, 0f, dir.X);
                if (i < hips) back = -back;
                float w = widths[i];
                Vector3 mid = (a + c) * 0.5f + Vector3.UnitY * (w * 0.55f);
                Kit.Box(b, mid, back, Vector3.UnitY, dir, new Vector3(w * 0.55f, w * 0.5f, l * 0.42f), w * 0.15f, bone);
                float spineHeight = w * (i >= hips && i < neckStart ? (form is FossilForm.Sauropod or FossilForm.Theropod ? 2.4f : 1.8f) : 1.3f);
                Kit.Box(b, mid + back * (w * 0.5f + spineHeight * 0.5f), back, Vector3.UnitY, dir, new Vector3(spineHeight * 0.5f, w * 0.18f, l * 0.25f), 0.02f, dark);
            }

            // Ribs over the body, arching up out of the floor from the spine towards its belly, the further ones broken short, and the belly's ribs along under it on a tyrant.
            int ribs = form == FossilForm.Armoured ? 9 : 12;
            for (int k = 0; k < ribs; k++)
            {
                float t = (k + 0.5f) / ribs;
                int index = hips + (int)(t * BodySteps);
                Vector3 root = spine[Math.Min(index, spine.Count - 1)];
                Vector3 dir = (spine[Math.Min(index + 1, spine.Count - 1)] - spine[Math.Max(index - 1, 0)]).Normalized();
                Vector3 belly = new Vector3(dir.Z, 0f, -dir.X);
                float span = length * (form == FossilForm.Armoured ? 0.1f : form == FossilForm.Horned ? 0.09f : 0.075f) * MathF.Sin(MathF.PI * (0.2f + 0.8f * t));
                float rise = span * (form == FossilForm.Armoured ? 0.55f : 0.8f);
                if (random.Chance(0.12f)) continue;
                float reach = random.Chance(0.25f) ? random.Range(0.35f, 0.7f) : 1f;
                var arc = new List<Vector3>();
                for (int j = 0; j <= 6; j++)
                {
                    float s = j / 6f * reach;
                    float a = s * MathF.PI;
                    arc.Add(root + Vector3.UnitY * (thick * 0.9f + MathF.Sin(a) * rise) + belly * ((1f - MathF.Cos(a)) * 0.5f * span) - dir * (s * span * 0.25f));
                }
                Kit.Bend(b, arc, thick * 0.28f, thick * 0.15f, bone);
                if (form == FossilForm.Theropod)
                {
                    Vector3 g = root + belly * span * 1.05f - dir * span * 0.2f + Vector3.UnitY * 0.05f;
                    Kit.Pole(b, g - dir * span * 0.18f, g + dir * span * 0.18f + belly * 0.2f, thick * 0.1f, dark);
                }
            }

            // The hips: a blade of bone over the spine and the bones down from it to the belly; the shoulders: a blade over the first ribs.
            Vector3 hipDir = (spine[hips + 1] - spine[hips - 1]).Normalized();
            Vector3 hipBack = new Vector3(-hipDir.Z, 0f, hipDir.X);
            Kit.Box(b, spine[hips] + Vector3.UnitY * (thick * 0.8f) + hipBack * (thick * 0.4f), hipDir, Vector3.UnitY, -hipBack, new Vector3(length * 0.07f, thick * 0.22f, length * 0.035f), thick * 0.1f, bone);
            Kit.Pole(b, spine[hips] + Vector3.UnitY * thick * 0.6f, spine[hips] - hipBack * (length * 0.08f) - hipDir * (length * 0.02f) + Vector3.UnitY * 0.1f, thick * 0.3f, dark, thick * 0.2f);
            Kit.Pole(b, spine[hips] + Vector3.UnitY * thick * 0.6f, spine[hips] - hipBack * (length * 0.06f) - hipDir * (length * 0.07f) + Vector3.UnitY * 0.1f, thick * 0.28f, dark, thick * 0.18f);
            Vector3 shoulderDir = (spine[neckStart] - spine[neckStart - 2]).Normalized();
            Vector3 shoulderBelly = new Vector3(shoulderDir.Z, 0f, -shoulderDir.X);
            Kit.Box(b, shoulders + Vector3.UnitY * (thick * 1.4f) - shoulderDir * (length * 0.03f) + shoulderBelly * (length * 0.025f), shoulderDir, Vector3.UnitY, shoulderBelly, new Vector3(length * 0.05f, thick * 0.18f, length * 0.02f), thick * 0.08f, bone);

            // The legs, splayed as they fell: the upper one lying over the floor, the lower half sunk under the body.
            bool biped = form == FossilForm.Theropod;
            float legLength = length * (biped ? 0.26f : form == FossilForm.Sauropod ? 0.2f : 0.17f);
            Leg(b, spine[hips], hipDir, -hipBack, legLength, thick * (biped ? 0.55f : 0.65f), random.Range(-0.5f, 0.6f), biped, bone, dark, random);
            Leg(b, spine[hips] + Vector3.UnitY * -0.25f, hipDir, -hipBack, legLength * 0.95f, thick * 0.5f, random.Range(-0.2f, 0.9f), biped, dark, dark, random);
            float armLength = biped ? length * 0.07f : legLength * (form == FossilForm.Sauropod ? 1.05f : 0.82f);
            Leg(b, shoulders, shoulderDir, shoulderBelly, armLength, thick * (biped ? 0.25f : 0.55f), random.Range(-0.4f, 0.8f), false, bone, dark, random);

            // The skull, at the end of the neck, looking back the way the neck threw it.
            Skull(b, form, spine[^1], skullDir, length * skull, thick, bone, dark, random, lights);

            // What only some have: a spikeback's plates fallen in a row beside its spine and the spikes of its tail; a clubtail's club and the plates scattered off its back.
            if (form == FossilForm.Plated)
            {
                for (int i = hips - 6; i < neckStart + 2; i += 2)
                {
                    if (i < 0 || i >= spine.Count - 1) continue;
                    Vector3 dir = (spine[i + 1] - spine[i]).Normalized();
                    Vector3 back = new Vector3(-dir.Z, 0f, dir.X) * (i < hips ? -1f : 1f);
                    float size = thick * random.Range(2.4f, 3.6f) * (1f - MathF.Abs(i - (hips + 2f)) / 14f);
                    Vector3 c = spine[i] + back * (thick * 2.2f + size * 0.5f) + Vector3.UnitY * 0.12f;
                    Kit.Solid(b, new[] { c - dir * size * 0.5f, c + back * size * 0.5f, c + dir * size * 0.5f, c - back * size * 0.3f, c - dir * size * 0.5f + Vector3.UnitY * 0.12f, c + back * size * 0.5f + Vector3.UnitY * 0.12f, c + dir * size * 0.5f + Vector3.UnitY * 0.12f, c - back * size * 0.3f + Vector3.UnitY * 0.12f }, 0.02f, bone);
                }
                for (int k = 0; k < 4; k++)
                {
                    Vector3 tip = spine[1 + k];
                    Vector3 dir = (spine[2 + k] - spine[1 + k]).Normalized();
                    Vector3 side = new Vector3(-dir.Z, 0f, dir.X) * (k % 2 == 0 ? 1f : -1f);
                    Kit.Pole(b, tip + Vector3.UnitY * thick * 0.4f, tip + side * (thick * 4f) + dir * thick + Vector3.UnitY * thick * 0.6f, thick * 0.35f, bone, 0.02f);
                }
            }
            if (form == FossilForm.Armoured)
            {
                Kit.Lathe(b, spine[0] - Vector3.UnitZ * thick, new Vector3(0.3f, 0f, -1f), thick * 4f, Kit.Profile((0f, thick * 0.5f), (0.3f, thick * 2.2f), (0.7f, thick * 2f), (1f, 0f)), 10, 4, bone);
                for (int k = 0; k < 18; k++)
                {
                    Vector3 c = spine[hips + random.Int(BodySteps)] + new Vector3(random.Range(-1f, 1f), 0f, random.Range(-1f, 1f)) * length * 0.08f;
                    Kit.Block(b, c + Vector3.UnitY * 0.12f, new Vector3(thick * 0.8f, thick * 0.25f, thick * 0.7f), random.Float() * MathF.Tau, 0.04f, k % 3 == 0 ? dark : bone);
                }
            }

            // What isn't freed yet is still in the rock: a long low hump of it over the spine from the tail, and lumps round the bones near it.
            VertexMaterial rock = new VertexMaterial(MaterialCatalog.Soil.Index, new Colour(0.42f, 0.36f, 0.28f));
            int buried = (int)((1f - freed) * spine.Count);
            for (int i = 0; i < buried; i += 2)
            {
                Vector3 c = spine[i];
                float r = widths[i] * 5f + length * 0.04f;
                Kit.Heap(b, c + Vector3.UnitY * -0.05f, new Vector2(r, r * 1.3f), widths[i] * 2.6f + 0.35f, random.Float() * MathF.Tau, rock, random, 3, 10);
            }
        }

        // A leg as it lies: thigh, shin, foot and toes splayed on the floor, from the hip at `root`, out on the belly's side and swung `swing` forward or back.
        private static void Leg(MeshBuilder b, Vector3 root, Vector3 along, Vector3 belly, float length, float thick, float swing, bool longFoot, VertexMaterial bone, VertexMaterial dark, WorldRandom random)
        {
            Vector3 way = (belly * MathF.Cos(swing) + along * MathF.Sin(swing)).Normalized();
            Vector3 knee = root + way * (length * 0.45f) + Vector3.UnitY * 0.12f;
            Vector3 kneeTurn = (way * MathF.Cos(-0.6f) + along * MathF.Sin(-0.6f) * MathF.Sign(swing + 0.01f)).Normalized();
            Vector3 ankle = knee + kneeTurn * (length * 0.38f);
            Vector3 footWay = (kneeTurn + belly * 0.3f).Normalized();
            Vector3 toes = ankle + footWay * (length * (longFoot ? 0.22f : 0.12f));
            LongBone(b, root + Vector3.UnitY * thick, knee + Vector3.UnitY * thick * 0.6f, thick, bone);
            LongBone(b, knee + Vector3.UnitY * thick * 0.6f, ankle + Vector3.UnitY * thick * 0.4f, thick * 0.8f, bone);
            LongBone(b, ankle + Vector3.UnitY * thick * 0.4f, toes + Vector3.UnitY * thick * 0.3f, thick * 0.5f, dark);
            for (int k = -1; k <= 1; k++)
            {
                Vector3 toeWay = (footWay + new Vector3(-footWay.Z, 0f, footWay.X) * (k * 0.35f)).Normalized();
                Kit.Pole(b, toes + Vector3.UnitY * thick * 0.3f, toes + toeWay * (length * 0.09f) + Vector3.UnitY * thick * 0.2f, thick * 0.28f, bone, thick * 0.08f);
            }
        }

        // A long bone: a shaft with a knuckle at each end.
        private static void LongBone(MeshBuilder b, Vector3 from, Vector3 to, float thick, VertexMaterial bone)
        {
            Kit.Pole(b, from, to, thick * 0.42f, bone, thick * 0.36f);
            Kit.Lathe(b, from - (to - from).Normalized() * thick * 0.35f, to - from, thick * 0.8f, Kit.Profile((0f, 0f), (0.35f, thick * 0.62f), (0.7f, thick * 0.55f), (1f, 0f)), 7, 3, bone);
            Kit.Lathe(b, to - (to - from).Normalized() * thick * 0.4f, to - from, thick * 0.8f, Kit.Profile((0f, 0f), (0.35f, thick * 0.6f), (0.7f, thick * 0.5f), (1f, 0f)), 7, 3, bone);
        }

        // The skull: a tyrant's great one with its jaw dropped open and its teeth, a hornback's under its frill with its horns, the small ones of the rest. Its eye socket dark, with the
        // Ossuary's cold light come into it.
        private static void Skull(MeshBuilder b, FossilForm form, Vector3 back, Vector3 forward, float length, float thick, VertexMaterial bone, VertexMaterial dark, WorldRandom random, List<AssetLight> lights)
        {
            Vector3 side = new Vector3(-forward.Z, 0f, forward.X);
            float high = form switch { FossilForm.Theropod => length * 0.42f, FossilForm.Horned => length * 0.38f, FossilForm.Armoured => length * 0.32f, _ => length * 0.4f };
            float wide = form == FossilForm.Armoured ? length * 0.7f : length * 0.36f;
            Vector3 c = back + forward * (length * 0.5f) + Vector3.UnitY * (high * 0.42f);
            // Lying on its side: its height along the ground, its width up.
            Vector3 up = side, across = Vector3.UnitY;
            Kit.Solid(b, new[]
            {
                c - forward * length * 0.5f - up * high * 0.5f + across * wide * 0.5f, c + forward * length * 0.5f - up * high * 0.2f + across * wide * 0.3f, c + forward * length * 0.5f + up * high * 0.15f + across * wide * 0.3f, c - forward * length * 0.5f + up * high * 0.5f + across * wide * 0.5f,
                c - forward * length * 0.5f - up * high * 0.5f - across * wide * 0.5f, c + forward * length * 0.5f - up * high * 0.2f - across * wide * 0.3f, c + forward * length * 0.5f + up * high * 0.15f - across * wide * 0.3f, c - forward * length * 0.5f + up * high * 0.5f - across * wide * 0.5f,
            }, length * 0.04f, bone);
            // The eye socket and the openings in front of it, dark, the cold light in the eye.
            Vector3 eye = c - forward * length * 0.18f + up * high * 0.18f + across * (wide * 0.5f + 0.02f);
            Kit.Lathe(b, eye, Vector3.UnitY, 0.05f, Kit.Profile((0f, length * 0.09f), (1f, length * 0.09f)), 8, 1, Dark);
            Kit.Lathe(b, eye + Vector3.UnitY * 0.04f, Vector3.UnitY, 0.06f, Kit.Profile((0f, length * 0.045f), (1f, 0f)), 6, 1, OssuaryGlow(2.4f));
            Kit.Lathe(b, c + forward * length * 0.15f + across * (wide * 0.45f + 0.02f), Vector3.UnitY, 0.05f, Kit.Profile((0f, length * 0.07f), (1f, length * 0.07f)), 7, 1, Dark);
            lights.Add(Cold(eye + Vector3.UnitY * 0.8f, 26f, 7f));
            if (form is FossilForm.Theropod)
            {
                // The jaw, fallen open, and the teeth along both.
                float gape = random.Range(0.35f, 0.7f);
                Vector3 hinge = c - forward * length * 0.42f - up * high * 0.42f;
                Vector3 jawWay = (forward * MathF.Cos(gape) - up * MathF.Sin(gape)).Normalized();
                Kit.Box(b, hinge + jawWay * length * 0.45f, -up, across, jawWay, new Vector3(high * 0.12f, wide * 0.32f, length * 0.46f), length * 0.02f, dark);
                for (int k = 0; k < 9; k++)
                {
                    float t = 0.2f + k * 0.08f;
                    Vector3 upperRoot = c - forward * length * 0.5f + forward * length * t - up * high * (0.5f - t * 0.3f) + across * (wide * 0.25f);
                    Kit.Pole(b, upperRoot, upperRoot - up * length * 0.07f, length * 0.014f, bone, 0.005f);
                    Vector3 lowerRoot = hinge + jawWay * length * (t * 0.95f) + up * high * 0.05f + across * (wide * 0.25f);
                    Kit.Pole(b, lowerRoot, lowerRoot + (up * MathF.Cos(gape) + forward * MathF.Sin(gape)) * length * 0.055f, length * 0.012f, bone, 0.005f);
                }
            }
            else if (form is FossilForm.Horned)
            {
                // Its frill, a great shield of bone behind the skull, scalloped round its edge; its two brow horns and the one on its nose.
                Vector3 frill = c - forward * length * 0.42f;
                const int Edge = 10;
                float fr = length * 0.52f;
                for (int k = 0; k < Edge; k++)
                {
                    float a0 = -MathF.PI * 0.5f + MathF.PI * k / Edge, a1 = -MathF.PI * 0.5f + MathF.PI * (k + 1) / Edge;
                    Vector3 p0 = frill + (-forward * MathF.Cos(a0) * 0.8f + up * MathF.Sin(a0)) * fr, p1 = frill + (-forward * MathF.Cos(a1) * 0.8f + up * MathF.Sin(a1)) * fr;
                    Kit.Face(b, frill + across * 0.15f, p0 + across * 0.12f, p1 + across * 0.12f, across, bone);
                    Kit.Lathe(b, (p0 + p1) * 0.5f, -forward + up * MathF.Sin((a0 + a1) * 0.5f), length * 0.05f, Kit.Profile((0f, length * 0.035f), (1f, 0f)), 5, 1, bone);
                }
                foreach (float s in new[] { 0.2f, -0.2f })
                {
                    Vector3 root = c - forward * length * 0.1f + up * high * s + across * (wide * 0.35f);
                    Kit.Pole(b, root, root + forward * length * 0.45f + up * high * s * 0.8f + across * 0.2f, length * 0.05f, bone, 0.01f);
                }
                Vector3 nose = c + forward * length * 0.4f + across * 0.2f;
                Kit.Pole(b, nose, nose + forward * length * 0.15f - up * high * 0.1f + across * 0.15f, length * 0.035f, bone, 0.01f);
            }
            else if (form is FossilForm.Armoured)
            {
                foreach (float s in new[] { 1f, -1f })
                {
                    Vector3 corner = c - forward * length * 0.4f + up * high * 0.45f * s + across * 0.1f;
                    Kit.Pole(b, corner, corner + (up * s - forward) * length * 0.18f, length * 0.05f, bone, 0.01f);
                }
            }
        }

        /// <summary>The strings of a dig's grid over a box centred on the origin, on pegs at its edges, a metre and a bit apart, and a measuring rod laid across it.</summary>
        public static void DigGrid(MeshBuilder b, in PlaceSpec spec, WorldRandom random)
        {
            float w = Math.Clamp(spec.Metres(0), 3f, 30f), d = Math.Clamp(spec.Metres(1), 3f, 30f);
            VertexMaterial peg = Timber(random, 1.1f), cord = new VertexMaterial(MaterialCatalog.Cloth.Index, new Colour(0.62f, 0.58f, 0.46f));
            const float Step = 2.4f, High = 0.5f;
            int nx = Math.Max(1, (int)MathF.Round(w / Step)), nz = Math.Max(1, (int)MathF.Round(d / Step));
            for (int i = 0; i <= nx; i++)
            {
                float x = -w * 0.5f + w * i / nx;
                Kit.Pole(b, new Vector3(x, -0.2f, -d * 0.5f), new Vector3(x, High + 0.1f, -d * 0.5f), 0.05f, peg);
                Kit.Pole(b, new Vector3(x, -0.2f, d * 0.5f), new Vector3(x, High + 0.1f, d * 0.5f), 0.05f, peg);
                Kit.Rope(b, new Vector3(x, High, -d * 0.5f), new Vector3(x, High, d * 0.5f), 0.05f, 0.018f, cord, 3);
            }
            for (int j = 0; j <= nz; j++)
            {
                float z = -d * 0.5f + d * j / nz;
                Kit.Pole(b, new Vector3(-w * 0.5f, -0.2f, z), new Vector3(-w * 0.5f, High + 0.1f, z), 0.05f, peg);
                Kit.Pole(b, new Vector3(w * 0.5f, -0.2f, z), new Vector3(w * 0.5f, High + 0.1f, z), 0.05f, peg);
                Kit.Rope(b, new Vector3(-w * 0.5f, High, z), new Vector3(w * 0.5f, High, z), 0.05f, 0.018f, cord, 3);
            }
            // A rod marked off in bands, and a little flag at each corner.
            for (int k = 0; k < 8; k++) Kit.Box(b, new Vector3(-w * 0.3f + k * 0.4f, Ground + 0.04f, d * 0.5f + 0.6f), Vector3.UnitX, Vector3.UnitY, Vector3.UnitZ, new Vector3(0.2f, 0.03f, 0.04f), 0f, k % 2 == 0 ? Painted(new Vector3(0.62f, 0.6f, 0.55f)) : OxbloodPaint());
        }

        /// <summary>Boards shoring a pit's side along x, standing from the floor of the pit (y = -depth) to just over the edge, held by stakes, the side behind them earth.</summary>
        public static void Revetment(MeshBuilder b, in PlaceSpec spec, WorldRandom random)
        {
            float length = Math.Clamp(spec.Metres(0), 1f, 30f), depth = Math.Clamp(spec.Metres(1), 0.5f, 4f);
            VertexMaterial boards = Timber(random, 0.95f), stake = Timber(random, 0.7f);
            int planks = Math.Max(2, (int)MathF.Ceiling((depth + 0.3f) / 0.38f));
            for (int k = 0; k < planks; k++)
            {
                float y = -depth + 0.19f + k * 0.38f;
                Kit.Plank(b, new Vector3(-length * 0.5f, y, 0f), new Vector3(length * 0.5f, y, 0.02f * k), Vector3.UnitZ, 0.36f, 0.08f, boards, 0.01f);
            }
            for (float x = -length * 0.5f + 0.4f; x < length * 0.5f; x += 1.8f)
            {
                Kit.Plank(b, new Vector3(x, -depth - 0.2f, 0.12f), new Vector3(x, 0.45f, 0.12f), Vector3.UnitZ, 0.14f, 0.12f, stake, 0.01f);
            }
            // The earth it holds back, so nothing shows behind it but the side of the pit.
            Kit.Face(b, new Vector3(-length * 0.5f, -depth, -0.05f), new Vector3(length * 0.5f, -depth, -0.05f), new Vector3(length * 0.5f, 0.1f, -0.05f), new Vector3(-length * 0.5f, 0.1f, -0.05f), Vector3.UnitZ, Soil(0.8f));
        }

        /// <summary>A sieve on legs at the pit's edge, a heap of what's been shaken through under it and a basket of what hasn't.</summary>
        public static void Sieve(MeshBuilder b, in PlaceSpec spec, WorldRandom random)
        {
            VertexMaterial wood = Timber(random), mesh = Net(random, 0.8f);
            Vector3 c = new Vector3(0f, 1.1f, 0f);
            foreach (float x in new[] { -0.9f, 0.9f })
            {
                foreach (float z in new[] { -0.6f, 0.6f }) Kit.Pole(b, new Vector3(x * 1.1f, 0f, z * 1.1f), c + new Vector3(x, 0f, z), 0.05f, wood);
            }
            Kit.Span(b, c + new Vector3(-1f, -0.06f, -0.7f), c + new Vector3(1f, 0.12f, 0.7f), 0.01f, wood);
            Kit.Face(b, c + new Vector3(-0.92f, 0.02f, 0.62f), c + new Vector3(0.92f, 0.02f, 0.62f), c + new Vector3(0.92f, 0.02f, -0.62f), c + new Vector3(-0.92f, 0.02f, -0.62f), Vector3.UnitY, mesh);
            Kit.Heap(b, new Vector3(0f, Ground - 0.05f, 0f), new Vector2(1.1f, 0.8f), 0.4f, 0f, Soil(1.05f), random, 3, 10);
            Kit.Lathe(b, new Vector3(1.6f, 0f, 0.4f), Vector3.UnitY, 0.7f, Kit.Profile((0f, 0.35f), (1f, 0.45f)), 9, 2, Straw(random, 0.75f));
        }

        /// <summary>Bones jacketed in plaster and sacking for the journey, on a pallet of planks, each numbered in oxblood.</summary>
        public static void Jackets(MeshBuilder b, in PlaceSpec spec, WorldRandom random)
        {
            int count = Math.Clamp(spec.Int(0), 1, 5);
            VertexMaterial wood = Timber(random), plaster = Plaster(0.95f);
            Kit.Span(b, new Vector3(-1.6f, 0f, -1f), new Vector3(1.6f, 0.25f, 1f), 0.02f, wood);
            for (int k = 0; k < count; k++)
            {
                float z = -0.6f + k * 1.2f / Math.Max(1, count - 1);
                float l = random.Range(1.4f, 2.6f), r = random.Range(0.28f, 0.42f);
                Kit.Lathe(b, new Vector3(-l * 0.5f, 0.25f + r * 0.9f, z), Vector3.UnitX, l, Kit.Profile((0f, 0f), (0.12f, r), (0.5f, r * 1.12f), (0.88f, r), (1f, 0f)), 8, 4, plaster);
                Kit.Box(b, new Vector3(random.Range(-0.3f, 0.3f), 0.25f + r * 1.9f, z), Vector3.UnitX, Vector3.UnitY, Vector3.UnitZ, new Vector3(0.18f, 0.01f, 0.12f), 0f, OxbloodPaint());
            }
        }

        /// <summary>Crates of bones packed in straw, a lid off one to show them, the Reich's mark stencilled on them.</summary>
        public static void BoneCrates(MeshBuilder b, in PlaceSpec spec, WorldRandom random)
        {
            VertexMaterial boards = Timber(random, 0.85f), iron = Iron(random);
            for (int k = 0; k < 3; k++)
            {
                Vector3 at = new Vector3(-1.4f + k * 1.4f, 0.6f, random.Range(-0.2f, 0.2f));
                Kit.Crate(b, at, random.Range(-0.15f, 0.15f), new Vector3(0.65f, 0.55f, 0.6f), boards, iron, random, marked: k != 1);
            }
            // One open: the straw in it and bones standing out of it.
            Vector3 open = new Vector3(0f, 1.15f, 0f);
            Kit.Span(b, open + new Vector3(-0.55f, -0.02f, -0.5f), open + new Vector3(0.55f, 0.02f, 0.5f), 0f, Straw(random));
            for (int k = 0; k < 4; k++) Kit.Pole(b, open + new Vector3(random.Range(-0.4f, 0.4f), 0f, random.Range(-0.3f, 0.3f)), open + new Vector3(random.Range(-0.4f, 0.4f), random.Range(0.3f, 0.6f), random.Range(-0.4f, 0.4f)), 0.06f, PlaceMaterials.Fossil(random));
        }

        /// <summary>A heap of what's been dug out, long and low along x, raked off at its foot.</summary>
        public static void Spoil(MeshBuilder b, in PlaceSpec spec, WorldRandom random)
        {
            float length = Math.Clamp(spec.Metres(0), 2f, 20f);
            Kit.Heap(b, new Vector3(0f, Ground - 0.25f, 0f), new Vector2(length * 0.5f, length * 0.22f + 0.8f), length * 0.12f + 0.6f, 0f, Soil(1.1f), random, 5, 16);
            // A spade left stuck in it.
            Vector3 spade = new Vector3(random.Range(-length * 0.3f, length * 0.3f), 0.6f, 0.4f);
            Kit.Pole(b, spade, spade + new Vector3(0.2f, 1.6f, -0.1f), 0.04f, Timber(random));
            Kit.Box(b, spade - new Vector3(0f, 0.2f, 0f), Vector3.UnitX, Vector3.UnitY, Vector3.UnitZ, new Vector3(0.18f, 0.24f, 0.02f), 0f, LandIron(random));
        }

        /// <summary>A trestle table under an oxblood awning where what's found is drawn and numbered: papers weighed down with stones, a lamp, a skull being drawn, a stool.</summary>
        public static void DrawingTable(MeshBuilder b, in PlaceSpec spec, WorldRandom random, List<AssetLight> lights)
        {
            VertexMaterial wood = Timber(random), iron = Iron(random);
            const float Long = 3f, Deep = 1.4f, High = 1.25f;
            foreach (float x in new[] { -Long * 0.4f, Long * 0.4f })
            {
                Kit.Pole(b, new Vector3(x, 0f, -Deep * 0.4f), new Vector3(x, High, 0f), 0.05f, wood);
                Kit.Pole(b, new Vector3(x, 0f, Deep * 0.4f), new Vector3(x, High, 0f), 0.05f, wood);
            }
            Kit.Span(b, new Vector3(-Long * 0.5f, High, -Deep * 0.5f), new Vector3(Long * 0.5f, High + 0.08f, Deep * 0.5f), 0.01f, wood);
            for (int k = 0; k < 4; k++)
            {
                Vector3 sheet = new Vector3(random.Range(-1.1f, 0.6f), High + 0.085f + k * 0.003f, random.Range(-0.4f, 0.4f));
                float turn = random.Range(-0.4f, 0.4f);
                Kit.Box(b, sheet, new Vector3(MathF.Cos(turn), 0f, -MathF.Sin(turn)), Vector3.UnitY, new Vector3(MathF.Sin(turn), 0f, MathF.Cos(turn)), new Vector3(0.3f, 0.002f, 0.42f), 0f, Plaster(1.1f));
            }
            Kit.Lathe(b, new Vector3(Long * 0.35f, High + 0.08f, 0.2f), Vector3.UnitY, 0.45f, Kit.Profile((0f, 0.12f), (0.6f, 0.1f), (0.8f, 0.12f), (1f, 0.02f)), 8, 3, Brass(random));
            Kit.Lathe(b, new Vector3(Long * 0.35f, High + 0.25f, 0.2f), Vector3.UnitY, 0.16f, Kit.Profile((0f, 0.08f), (1f, 0.06f)), 8, 1, LampGlow(1.4f));
            lights.Add(PlaceMaterials.Lantern(new Vector3(Long * 0.35f, High + 0.6f, 0.2f)) with { Intensity = 28f, Radius = 7f });
            Kit.Block(b, new Vector3(-0.2f, High + 0.3f, -0.1f), new Vector3(0.4f, 0.22f, 0.25f), 0.4f, 0.08f, PlaceMaterials.Fossil(random));
            Kit.Lathe(b, new Vector3(0f, 0f, Deep * 0.75f), Vector3.UnitY, 0.75f, Kit.Profile((0f, 0.25f), (1f, 0.3f)), 8, 1, wood);
            // The awning: four poles and an oxblood cloth over them.
            foreach (float x in new[] { -Long * 0.65f, Long * 0.65f })
            {
                foreach (float z in new[] { -Deep, Deep * 0.9f }) Kit.Pole(b, new Vector3(x, -0.2f, z), new Vector3(x, z < 0f ? 3.6f : 3.2f, z), 0.06f, wood);
            }
            Kit.Cloth(b, new Vector3(-Long * 0.7f, 3.6f, -Deep * 1.05f), new Vector3(Long * 0.7f, 3.6f, -Deep * 1.05f), new Vector3(Long * 0.7f, 3.2f, Deep * 1f), new Vector3(-Long * 0.7f, 3.2f, Deep * 1f), 0.12f, 3, Oxblood(), Oxblood(0.6f));
        }

        /// <summary>A tripod of poles over the pit, a chain down from its head, and a jacketed bone coming up on it.</summary>
        public static void Hoist(MeshBuilder b, in PlaceSpec spec, WorldRandom random)
        {
            float height = Math.Clamp(spec.Metres(0), 4f, 10f);
            VertexMaterial wood = Weathered(random), iron = Iron(random);
            Vector3 head = new Vector3(0f, height, 0f);
            for (int k = 0; k < 3; k++)
            {
                float a = k * MathF.Tau / 3f + 0.4f;
                Kit.Pole(b, new Vector3(MathF.Cos(a) * height * 0.45f, -0.3f, MathF.Sin(a) * height * 0.45f), head + new Vector3(MathF.Cos(a) * 0.12f, 0.2f, MathF.Sin(a) * 0.12f), 0.11f, wood, 0.08f);
            }
            b.Tube(head - new Vector3(0f, 0.15f, 0f), head + new Vector3(0f, 0.25f, 0f), 0.2f, 0.2f, 6, Rope(random));
            float hang = random.Range(1.2f, height - 1.5f);
            Kit.Chain(b, head, head - Vector3.UnitY * (height - hang), 0f, 0.2f, iron);
            Vector3 load = new Vector3(0f, hang - 0.5f, 0f);
            Kit.Lathe(b, load - new Vector3(1.1f, 0f, 0f), Vector3.UnitX, 2.2f, Kit.Profile((0f, 0f), (0.1f, 0.35f), (0.5f, 0.42f), (0.9f, 0.35f), (1f, 0f)), 8, 4, Plaster(0.95f));
            Kit.Rope(b, head - Vector3.UnitY * (height - hang), load + new Vector3(-0.7f, 0.3f, 0f), 0f, 0.03f, Rope(random), 1);
            Kit.Rope(b, head - Vector3.UnitY * (height - hang), load + new Vector3(0.7f, 0.3f, 0f), 0f, 0.03f, Rope(random), 1);
            // The hauling line off to a winch at one leg.
            float a2 = 0.4f;
            Vector3 winch = new Vector3(MathF.Cos(a2) * height * 0.35f, 0.8f, MathF.Sin(a2) * height * 0.35f);
            Kit.Pole(b, winch - Vector3.UnitZ * 0.5f, winch + Vector3.UnitZ * 0.5f, 0.18f, wood);
            Kit.Rope(b, head, winch, 0f, 0.03f, Rope(random), 1);
        }
    }
}
