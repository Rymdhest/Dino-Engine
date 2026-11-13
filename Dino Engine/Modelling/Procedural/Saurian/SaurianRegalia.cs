using Dino_Engine.ECS.Components;
using Dino_Engine.Modelling.Procedural.Gear;
using OpenTK.Mathematics;

namespace Dino_Engine.Modelling.Procedural.Saurian
{
    /// <summary>
    /// What a commander puts on its own war-beast, the tyrant that holds a land: not a working harness but the Reich's pride in it. An iron helm over its skull with a crest of blades down the
    /// middle and guards over its cheeks, riveted in brass; an iron gorget at the root of its neck; a ridge of iron plates down its back, each with a blade standing off it, over an oxblood
    /// saddle-cloth; oxblood tabards hanging down its flanks, edged in brass, the middle one on each side carrying the Reich's mark; two standards on its shoulders; plates strapped to its
    /// thighs; iron rings round its tail; and chains from its helm to its gorget. Black iron, oxblood and brass, and nothing else wears them. Read LORE.md.
    /// </summary>
    public static class SaurianRegalia
    {
        private static Vector3 Iron => SaurianHarness.Iron;
        private static Vector3 Oxblood => SaurianHarness.Oxblood;
        private static Vector3 Brass => SaurianHarness.Brass;
        private static Vector3 Cloth => SaurianHarness.Strap;

        // The flesh the regalia is laid on, as a body is grown on one thread, for a cloth to follow the skin it's on rather than the line of the body, which the belly and the chest stand
        // well outside of.
        [ThreadStatic] private static SaurianField? _flesh;

        public static void Fit(SaurianMesh mesh, SaurianBody body)
        {
            _flesh = SaurianSculpt.Flesh(body);
            var dress = new SaurianDress(body, new SaurianPaint(body));
            Helm(mesh, body);
            Gorget(mesh, body, dress, out float collar);
            Back(mesh, body, dress);
            Tabards(mesh, body, dress);
            Standards(mesh, body, dress);
            Thighs(mesh, body);
            TailRings(mesh, body, dress);
            Chains(mesh, body, collar);
            _flesh = null;
        }

        // Where a ray out from the line of the body leaves its skin, and a little further, as far again as the share of its depth that's said: found by halving between a point inside and one
        // outside.
        private static Vector3 OnSkin(Vector3 centre, Vector3 way, float deep, float outset)
        {
            if (_flesh == null) return centre + way * (deep * outset);
            return centre + way * (_flesh.Exit(centre, way, deep) + deep * (outset - 1f));
        }

        // An iron helm over the top of the skull from the bridge of the snout back past the eyes, its brow plates over each eye, guards over the cheeks, and down the middle a crest of blades
        // that grow towards the back, all riveted in brass.
        private static void Helm(SaurianMesh mesh, SaurianBody body)
        {
            SaurianHead head = body.Head;
            float length = head.Length;
            float Top(float z) => head.Lip(z) + head.DepthAt(z) * 1.24f;

            var path = new List<Vector3>();
            var radii = new List<float>();
            for (int k = 0; k <= 8; k++)
            {
                float z = length * (0.12f + 0.6f * k / 8f);
                path.Add(head.ToBody(new Vector3(0f, Top(z) + length * 0.008f, z)));
                radii.Add(head.WidthAt(z) * (0.9f - 0.1f * k / 8f));
            }
            mesh.Tube(path, radii, 10, Iron, GearMaterials.Metal, BoneType.Head, head.Up, 0.2f);

            // The crest: blades standing up off the middle of the helm and leaning back, biggest over the eyes.
            for (int k = 0; k < 6; k++)
            {
                float t = k / 5f;
                float z = length * (0.18f + 0.48f * t);
                float size = length * (0.16f - 0.08f * t);
                Vector3 root = head.ToBody(new Vector3(0f, Top(z) + length * 0.01f, z));
                Vector3 up = head.ToBody(new Vector3(0f, Top(z) + 1f, z - 0.45f)) - root;
                mesh.Spike(root, up, size, size * 0.28f, -head.Forward, 0.25f, 4, Iron, GearMaterials.Metal, BoneType.Head, 0.25f, head.ToBody(Vector3.UnitX) - head.ToBody(Vector3.Zero));
            }

            // Rivets down both edges of the helm.
            for (int k = 0; k < 7; k++)
            {
                float z = length * (0.16f + 0.52f * k / 6f);
                for (int side = -1; side <= 1; side += 2)
                {
                    Vector3 rivet = head.ToBody(new Vector3(side * head.WidthAt(z) * 0.78f, Top(z) + length * 0.004f, z));
                    mesh.Ball(rivet, new Vector3(length * 0.017f), 1, Brass, GearMaterials.Metal, BoneType.Head);
                }
            }

            // Guards over the cheeks, behind and under the eyes, where what closes the jaws bulges.
            for (int side = -1; side <= 1; side += 2)
            {
                var cheek = new List<Vector3>();
                var cheekRadii = new List<float>();
                for (int k = 0; k <= 4; k++)
                {
                    float z = length * (0.04f + 0.26f * k / 4f);
                    float y = head.Lip(z) + head.DepthAt(z) * 0.5f;
                    cheek.Add(head.ToBody(new Vector3(side * head.WidthAt(z) * 1.0f, y, z)));
                    cheekRadii.Add(head.DepthAt(z) * 0.42f);
                }
                mesh.Tube(cheek, cheekRadii, 8, Iron, GearMaterials.Metal, BoneType.Head, head.ToBody(Vector3.UnitX * side) - head.ToBody(Vector3.Zero), 0.22f);
                float stud = length * 0.18f;
                Vector3 studAt = head.ToBody(new Vector3(side * head.WidthAt(stud) * 1.06f, head.Lip(stud) + head.DepthAt(stud) * 0.5f, stud));
                mesh.Ball(studAt, new Vector3(length * 0.03f), 1, Brass, GearMaterials.Metal, BoneType.Head);
            }
        }

        // A broad iron gorget round the root of the neck, two bands studded in brass.
        private static void Gorget(SaurianMesh mesh, SaurianBody body, SaurianDress dress, out float collar)
        {
            SaurianAxis axis = body.Axis;
            collar = body.NeckAt + (body.NeckTopAt - body.NeckAt) * 0.3f;
            float second = body.NeckAt + (body.NeckTopAt - body.NeckAt) * 0.62f;
            SaurianRot.Band(mesh, axis, collar, 1.14f, axis.DeepAt(collar) * 0.13f, Iron, GearMaterials.Metal, dress.Along(collar), bolts: 10, Brass);
            SaurianRot.Band(mesh, axis, second, 1.13f, axis.DeepAt(second) * 0.1f, Iron, GearMaterials.Metal, dress.Along(second), bolts: 8, Brass);
        }

        // Down the middle of the back from the gorget to the hips, an oxblood saddle-cloth draped over the body, edged in brass, and over it a ridge of iron plates, each with a blade
        // standing off it that leans back.
        private static void Back(SaurianMesh mesh, SaurianBody body, SaurianDress dress)
        {
            SaurianAxis axis = body.Axis;
            float from = body.HipAt - (body.HipAt - body.TailRootAt) * 0.5f;
            float to = body.NeckAt;
            Drape(mesh, body, dress, from, to, -0.95f, 0.95f, 1.035f, Cloth, GearMaterials.Cloth);
            Edge(mesh, body, dress, from, to, 0.95f, 1.045f);
            Edge(mesh, body, dress, from, to, -0.95f, 1.045f);

            const int Plates = 9;
            for (int k = 0; k < Plates; k++)
            {
                float t = k / (Plates - 1f);
                float along = from + (to - from) * t;
                Vector3 way = axis.Way(along);
                float deep = axis.DeepAt(along);
                float size = (to - from) / Plates;
                Hang hang = dress.Along(along);
                Drape(mesh, body, dress, along - size * 0.47f, along + size * 0.47f, -0.42f, 0.42f, 1.07f, Iron, GearMaterials.Metal, hang: hang);
                float blade = deep * (0.45f + 0.35f * MathF.Sin(MathF.PI * MathF.Min(t * 1.3f, 1f)));
                Vector3 root = axis.At(along) + Vector3.UnitY * (deep * 1.08f);
                mesh.Spike(root, Vector3.UnitY - way * 0.55f, blade, blade * 0.2f, -way, 0.3f, 4, Iron, GearMaterials.Metal, hang, 0.22f, way);
                mesh.Ball(root, new Vector3(deep * 0.06f), 1, Brass, GearMaterials.Metal, hang);
            }
        }

        // Oxblood tabards hanging down each flank from the saddle-cloth, draped as the body is, pointed at their hems, and the middle one on each side carrying the Reich's mark: a tyrant's
        // skull, in brass, which is the Reich's and looks like nothing but what it is.
        private static void Tabards(SaurianMesh mesh, SaurianBody body, SaurianDress dress)
        {
            SaurianAxis axis = body.Axis;
            float from = body.HipAt + (body.ChestAt - body.HipAt) * 0.1f;
            float to = body.HipAt + (body.ChestAt - body.HipAt) * 0.94f;
            const int Panels = 3;
            float each = (to - from) / Panels;
            for (int k = 0; k < Panels; k++)
            {
                float a = from + each * (k + 0.06f), b = from + each * (k + 0.94f);
                float middle = (a + b) * 0.5f;
                for (int side = -1; side <= 1; side += 2)
                {
                    // From under the edge of the saddle-cloth down the flank, a point at the middle of its hem.
                    Drape(mesh, body, dress, a, b, side * 0.9f, side * 2.05f, 1.045f, Oxblood, GearMaterials.Cloth, t => 0.78f + 0.22f * (1f - MathF.Abs(t * 2f - 1f)));
                    if (k != Panels / 2) continue;

                    Vector3 centre = axis.At(middle);
                    Vector3 way = axis.Way(middle);
                    Vector3 up = (Vector3.UnitY - way * Vector3.Dot(Vector3.UnitY, way)).Normalized();
                    Vector3 across = Vector3.Cross(up, way);
                    float angle = side * 1.45f;
                    Vector3 at = OnSkin(centre, (up * MathF.Cos(angle) + across * MathF.Sin(angle)).Normalized(), axis.DeepAt(middle), 1.075f);
                    Vector3 normal = _flesh != null ? _flesh.Normal(at, axis.DeepAt(middle) * 0.05f) : (up * MathF.Cos(angle) + across * MathF.Sin(angle)).Normalized();
                    Vector3 markUp = Vector3.Cross(normal, way).Normalized();
                    if (Vector3.Dot(markUp, Vector3.UnitY) < 0f) markUp = -markUp;
                    Vector3 markRight = Vector3.Cross(markUp, normal).Normalized();
                    SaurianHarness.Mark(mesh, at, markRight, markUp, normal, each * 0.6f, dress.Along(middle));
                }
            }
        }

        // A sheet laid over the body as it is, round it from one angle to another (0 along the top of the back, and further round each way down a flank) and along it between two places on its
        // line, a little outside its skin, for a cloth or a plate to follow the body rather than stand off it. A hem can bring its far edge up or down along it.
        private static void Drape(SaurianMesh mesh, SaurianBody body, SaurianDress dress, float from, float to, float startAngle, float endAngle, float outset, Vector3 colour, int material, Func<float, float>? hem = null, Hang? hang = null)
        {
            SaurianAxis axis = body.Axis;
            const int Along = 10, Around = 8;
            int first = mesh.VertexCount;
            for (int i = 0; i <= Along; i++)
            {
                float t = i / (float)Along;
                float a = from + (to - from) * t;
                Vector3 centre = axis.At(a), way = axis.Way(a);
                Vector3 up = (Vector3.UnitY - way * Vector3.Dot(Vector3.UnitY, way)).Normalized();
                Vector3 across = Vector3.Cross(up, way);
                float deep = axis.DeepAt(a) * outset, wide = axis.WideAt(a) * outset;
                float end = hem != null ? startAngle + (endAngle - startAngle) * hem(t) : endAngle;
                Hang onto = hang ?? dress.Along(a);
                for (int j = 0; j <= Around; j++)
                {
                    float angle = startAngle + (end - startAngle) * j / Around;
                    Vector3 ray = (up * MathF.Cos(angle) + across * MathF.Sin(angle)).Normalized();
                    Vector3 p = OnSkin(centre, ray, axis.DeepAt(a), outset);
                    Vector3 normal = _flesh != null ? _flesh.Normal(p, axis.DeepAt(a) * 0.05f) : (up * (MathF.Cos(angle) / deep) + across * (MathF.Sin(angle) / wide)).Normalized();
                    mesh.Vertex(p, normal, way, new Vector2(t, j / (float)Around), colour, material, onto);
                }
            }
            for (int i = 0; i < Along; i++)
            {
                for (int j = 0; j < Around; j++)
                {
                    int v = first + i * (Around + 1) + j;
                    int next = v + Around + 1;
                    mesh.Triangle(v, next, v + 1);
                    mesh.Triangle(v + 1, next, next + 1);
                }
            }
        }

        // A brass edge along a cloth, at an angle round the body, from one place on its line to another.
        private static void Edge(SaurianMesh mesh, SaurianBody body, SaurianDress dress, float from, float to, float angle, float outset)
        {
            SaurianAxis axis = body.Axis;
            const int Steps = 10;
            var path = new List<Vector3>();
            var radii = new List<float>();
            for (int i = 0; i <= Steps; i++)
            {
                float a = from + (to - from) * i / Steps;
                Vector3 way = axis.Way(a);
                Vector3 up = (Vector3.UnitY - way * Vector3.Dot(Vector3.UnitY, way)).Normalized();
                Vector3 across = Vector3.Cross(up, way);
                path.Add(OnSkin(axis.At(a), (up * MathF.Cos(angle) + across * MathF.Sin(angle)).Normalized(), axis.DeepAt(a), outset));
                radii.Add(axis.DeepAt(a) * 0.035f);
            }
            mesh.Tube(path, radii, 5, Brass, GearMaterials.Metal, dress.Along((from + to) * 0.5f));
        }

        // Two standards on its shoulders, left and right of the ridge, leaning back a little, each a pole with a crossbar and an oxblood banner hanging from it, the mark on both its faces.
        private static void Standards(SaurianMesh mesh, SaurianBody body, SaurianDress dress)
        {
            SaurianAxis axis = body.Axis;
            float at = body.HipAt + (body.ChestAt - body.HipAt) * 0.78f;
            Vector3 way = axis.Way(at);
            float deep = axis.DeepAt(at), wide = axis.WideAt(at);
            Hang hang = dress.Along(at);
            Vector3 across = Vector3.Cross(Vector3.UnitY, way).Normalized();
            for (int side = -1; side <= 1; side += 2)
            {
                Vector3 foot = axis.At(at) + Vector3.UnitY * (deep * 0.9f) + across * (side * wide * 0.5f);
                Vector3 pole = (Vector3.UnitY - way * 0.18f + across * (side * 0.08f)).Normalized();
                float tall = deep * 3.4f;
                Vector3 top = foot + pole * tall;
                mesh.Tube(new[] { foot, top }, new[] { deep * 0.055f, deep * 0.045f }, 6, Iron, GearMaterials.Metal, hang);
                mesh.Spike(top, pole, deep * 0.32f, deep * 0.08f, pole, 0f, 5, Brass, GearMaterials.Metal, hang);

                // A crossbar, and the banner hanging from it, swallow-tailed at the bottom.
                float width = deep * 0.95f, drop = deep * 1.7f;
                Vector3 bar = top - pole * (deep * 0.14f);
                Vector3 right = Vector3.Cross(pole, across).Normalized();
                mesh.Tube(new[] { bar - right * width * 0.55f, bar + right * width * 0.55f }, new[] { deep * 0.035f, deep * 0.035f }, 5, Iron, GearMaterials.Metal, hang);
                Vector3 down = -pole;
                var cloth = new List<Vector3>
                {
                    bar - right * width * 0.5f,
                    bar + right * width * 0.5f,
                    bar + right * width * 0.5f + down * drop,
                    bar + down * (drop * 0.78f),
                    bar - right * width * 0.5f + down * drop,
                };
                Vector3 middle = bar + down * (drop * 0.45f);
                mesh.Sheet(cloth, middle, across, deep * 0.02f, Oxblood, Oxblood * 0.8f, GearMaterials.Cloth, _ => hang);
                for (int face = -1; face <= 1; face += 2) SaurianHarness.Mark(mesh, middle + across * (face * deep * 0.016f), right, -down, across * face, width * 0.62f, hang);
            }
        }

        // An iron plate strapped to the outside of each thigh.
        private static void Thighs(SaurianMesh mesh, SaurianBody body)
        {
            foreach (SaurianLeg leg in body.Legs)
            {
                if (leg.Front) continue;
                BoneType thigh = leg.Side > 0f ? BoneType.UpperLegL : BoneType.UpperLegR;
                Vector3 femur = leg.Knee - leg.Hip;
                Vector3 out_ = new Vector3(leg.Side, 0f, 0f);
                var path = new List<Vector3>();
                var radii = new List<float>();
                for (int k = 0; k <= 3; k++)
                {
                    float t = 0.22f + 0.5f * k / 3f;
                    path.Add(leg.Hip + femur * t + out_ * (leg.HipRadius * 0.95f));
                    radii.Add(leg.HipRadius * (0.62f - 0.12f * k / 3f));
                }
                mesh.Tube(path, radii, 8, Iron, GearMaterials.Metal, thigh, out_, 0.22f);
                mesh.Ball(leg.Hip + femur * 0.47f + out_ * (leg.HipRadius * 1.1f), new Vector3(leg.HipRadius * 0.1f), 1, Brass, GearMaterials.Metal, thigh);
            }
        }

        // Iron rings round the tail, studded in brass, as many as there's tail for.
        private static void TailRings(SaurianMesh mesh, SaurianBody body, SaurianDress dress)
        {
            SaurianAxis axis = body.Axis;
            foreach (float share in new[] { 0.8f, 0.6f, 0.42f })
            {
                float at = body.HipAt * share;
                SaurianRot.Band(mesh, axis, at, 1.14f, axis.DeepAt(at) * 0.14f, Iron, GearMaterials.Metal, dress.Along(at), bolts: 6, Brass);
            }
        }

        // A chain from each cheek guard back to the gorget, sagging a little, that goes with the head at one end and the neck at the other.
        private static void Chains(SaurianMesh mesh, SaurianBody body, float collar)
        {
            SaurianHead head = body.Head;
            SaurianAxis axis = body.Axis;
            for (int side = -1; side <= 1; side += 2)
            {
                float z = head.Length * 0.12f;
                Vector3 from = head.ToBody(new Vector3(side * head.WidthAt(z) * 1.05f, head.Lip(z) + head.DepthAt(z) * 0.3f, z));
                Vector3 to = axis.At(collar) + Vector3.Cross(Vector3.UnitY, axis.Way(collar)).Normalized() * (side * axis.WideAt(collar) * 1.02f);
                const int Links = 8;
                float thick = head.Length * 0.018f;
                for (int k = 0; k < Links; k++)
                {
                    float t = (k + 0.5f) / Links;
                    Vector3 at = Vector3.Lerp(from, to, t) - Vector3.UnitY * (MathF.Sin(MathF.PI * t) * head.Length * 0.12f);
                    var hang = new Hang(BoneType.Head, BoneType.Neck, 1f - t);
                    mesh.Ball(at, new Vector3(thick * 1.6f, thick, thick * 1.6f), 1, Iron, GearMaterials.Metal, hang);
                }
            }
        }
    }
}
