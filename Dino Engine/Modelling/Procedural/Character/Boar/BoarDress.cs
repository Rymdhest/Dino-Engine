using Dino_Engine.Modelling.Procedural.Flora;
using Dino_Engine.Modelling.Procedural.Gear;
using Dino_Engine.Modelling.Procedural.Saurian;
using OpenTK.Mathematics;

namespace Dino_Engine.Modelling.Procedural.Character
{
    /// <summary>
    /// What colour a boar's skin is and where its bristles grow. How the skin hangs on its bones and how its texture lies are every animal on four legs' (<see cref="QuadrupedDress"/>).
    /// </summary>
    public sealed class BoarDress : QuadrupedDress
    {
        private readonly BoarBody _body;
        private readonly uint _seed;

        public BoarDress(BoarBody body) : base(body.Frame())
        {
            _body = body;
            _seed = (uint)(body.Seed ^ (body.Seed >> 32));
        }

        // ---- Colour.

        private float Noise(Vector3 p, float size, uint salt) => FloraNoise.Value(p / size, _seed + salt);

        // The trunk: dark along the top and the mane, the flanks a little lighter, the belly no paler than a wild boar's is, all of it grizzled in patches.
        private Vector3 Coat(Vector3 p)
        {
            BoarHide hide = _body.Hide;
            BoarStation station = _body.Station(MathF.Min(p.Z, _body.Trunk[^1].Z));
            float height = (p.Y - station.Middle) / MathF.Max(station.Deep, 1e-3f);
            Vector3 colour = Vector3.Lerp(hide.Belly, hide.Flank, Smooth(-0.75f, -0.1f, height));
            colour = Vector3.Lerp(colour, hide.Back, Smooth(0.3f, 0.9f, height));

            // The mane's longer bristles are darker, from behind the ears to the middle of the back.
            float maneFrom = _body.HindZ + (_body.ForeZ - _body.HindZ) * 0.3f;
            float mane = Smooth(0.82f, 1.05f, height) * Smooth(maneFrom, maneFrom + _body.W(0.2f), p.Z);
            colour = Vector3.Lerp(colour, hide.Back * 0.7f, mane);

            // Darker over the front of the shoulders and the throat, as most wild boars are.
            float front = Smooth(_body.ForeZ, _body.ChestFront + _body.W(0.1f), p.Z) * (1f - Smooth(0.2f, 0.8f, height));
            colour = Vector3.Lerp(colour, hide.Face, front * 0.55f);

            float grizzle = 0.88f + 0.24f * Noise(p, _body.W(0.2f), 11u) + 0.1f * (Noise(p, _body.W(0.05f), 12u) - 0.5f);
            return colour * grizzle;
        }

        public override Vector3 Colour(Vector3 p, Vector3 normal, SaurianPart part, out int material, out float glow)
        {
            glow = 1f;
            material = GearMaterials.Leather;
            BoarHide hide = _body.Hide;
            if (IsLeg(part))
            {
                if (p.Y < _body.AnkleHeight + _body.W(0.003f)) return hide.Hoof * (0.9f + 0.2f * Noise(p, _body.W(0.03f), 21u));
                float dark = 1f - Smooth(_body.W(0.22f), _body.Brisket + _body.W(0.06f), p.Y);
                return Vector3.Lerp(Coat(p), hide.Legs, dark);
            }
            if (part == SaurianPart.Head) return Face(p, normal);
            return Coat(p);
        }

        // The head: the top of it the colour of the back, the face and snout darker, a pale grizzled band along the cheek under the eye, darker lips, and a bare pink-grey disc with its nostrils.
        private Vector3 Face(Vector3 p, Vector3 normal)
        {
            BoarHide hide = _body.Hide;
            Vector3 local = ToHead(p) / _body.HeadLength;
            float forward = local.Z, up = local.Y, across = MathF.Abs(local.X);

            if (forward > 0.93f && Vector3.Dot(normal, _body.HeadForward) > 0.45f)
            {
                float nostril = MathF.Min(new Vector2(across - 0.035f, up + 0.015f).Length, 1f);
                return Vector3.Lerp(hide.Disc * 0.25f, hide.Disc, Smooth(0.012f, 0.026f, nostril));
            }

            Vector3 colour = Vector3.Lerp(hide.Back, hide.Face, Smooth(0.18f, 0.5f, forward));
            float cheek = Smooth(0.06f, 0.18f, forward) * (1f - Smooth(0.48f, 0.66f, forward)) * Smooth(0.08f, -0.06f, up) * Smooth(0.05f, 0.15f, across);
            colour = Vector3.Lerp(colour, hide.Cheek, cheek * 0.85f);

            // The line of the mouth, back from the disc to the corner of the lips under the eye.
            float lip = MathHelper.Lerp(-0.075f, -0.06f, Smooth(0.45f, 0.95f, forward));
            float mouth = (1f - Smooth(0.008f, 0.02f, MathF.Abs(up - lip))) * Smooth(0.42f, 0.5f, forward) * Smooth(0.03f, 0.07f, across);
            colour = Vector3.Lerp(colour, hide.Face * 0.45f, mouth);

            float grizzle = 0.9f + 0.2f * Noise(p, _body.W(0.12f), 31u);
            return colour * grizzle;
        }

        // ---- Bristles.

        /// <summary>
        /// Where its coat grows (SHELLS.md): over all of the body, thinner down the legs to the fetlocks and none on the hooves, thinner down the snout and none on its disc or the lips.
        /// </summary>
        public override float Shell(Vector3 p, Vector3 normal, SaurianPart part)
        {
            if (IsLeg(part))
            {
                if (p.Y < _body.AnkleHeight + _body.W(0.006f)) return 0f;
                return MathHelper.Lerp(0.6f, 1f, Smooth(_body.W(0.12f), _body.Brisket, p.Y));
            }
            if (part != SaurianPart.Head) return 1f;

            Vector3 local = ToHead(p) / _body.HeadLength;
            if (local.Z > 0.92f && Vector3.Dot(normal, _body.HeadForward) > 0.35f) return 0f;
            float lips = Smooth(-0.04f, -0.1f, local.Y) * Smooth(0.5f, 0.7f, local.Z);
            return (1f - 0.45f * Smooth(0.45f, 0.85f, local.Z)) * (1f - 0.6f * lips);
        }
    }
}
