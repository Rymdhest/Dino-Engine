using Dino_Engine.ECS.Components;
using Dino_Engine.Modelling.Procedural.Flora;
using Dino_Engine.Modelling.Procedural.Gear;
using Dino_Engine.Modelling.Procedural.Saurian;
using OpenTK.Mathematics;

namespace Dino_Engine.Modelling.Procedural.Character
{
    /// <summary>
    /// What colour a bear's skin is, where its coat grows, and how its feet hang, as it stands on its soles. How the rest of the skin hangs on its bones and how its texture lies are every animal on
    /// four legs' (<see cref="QuadrupedDress"/>).
    /// </summary>
    public sealed class BearDress : QuadrupedDress
    {
        private readonly BearBody _body;
        private readonly uint _seed;

        public BearDress(BearBody body) : base(body.Frame())
        {
            _body = body;
            _seed = (uint)(body.Seed ^ (body.Seed >> 32));
        }

        private float Noise(Vector3 p, float size, uint salt) => FloraNoise.Value(p / size, _seed + salt);

        /// <summary>
        /// A bear stands on its soles: the toes ahead of the ball of the foot go with the foot's bone, which turns there, and the sole and the heel behind it go with the ankle, shared half and half
        /// right at the ball. A hoof's rule, everything below the ball, put the heel on the toes.
        /// </summary>
        protected override Hang? FootHang(Vector3 p, Vector3 pivot, BoneType lower, BoneType foot)
        {
            if (p.Z < pivot.Z || p.Y > pivot.Y + W(0.07f)) return null;
            return new Hang(foot, lower, 0.5f + 0.5f * Smooth(pivot.Z, pivot.Z + W(0.03f), p.Z));
        }

        // ---- Colour.

        // The trunk: the tips of the hair catching the light over the hump and the back, the flanks darker under them and the belly darker still.
        private Vector3 Coat(Vector3 p)
        {
            BearHide hide = _body.Hide;
            BoarStation station = _body.Station(MathF.Min(p.Z, _body.Trunk[^1].Z));
            float height = (p.Y - station.Middle) / MathF.Max(station.Deep, 1e-3f);
            Vector3 colour = Vector3.Lerp(hide.Belly, hide.Flank, Smooth(-0.6f, 0.1f, height));
            colour = Vector3.Lerp(colour, hide.Back, Smooth(0.35f, 0.9f, height));

            // The hump's hair is the longest and palest at its tips.
            float hump = Smooth(0.6f, 1.05f, height) * (1f - Smooth(_body.W(0.15f), _body.W(0.45f), MathF.Abs(p.Z - _body.ForeZ)));
            colour = Vector3.Lerp(colour, hide.Back * 1.12f, hump * 0.6f);

            float grizzle = 0.88f + 0.24f * Noise(p, _body.W(0.16f), 11u) + 0.12f * (Noise(p, _body.W(0.04f), 12u) - 0.5f);
            return colour * grizzle;
        }

        public override Vector3 Colour(Vector3 p, Vector3 normal, SaurianPart part, out int material, out float glow)
        {
            glow = 1f;
            material = GearMaterials.Hide;
            BearHide hide = _body.Hide;
            if (IsLeg(part))
            {
                // Black pads under the paws, and legs darker the further down they go.
                if (p.Y < _body.W(0.014f) && normal.Y < -0.3f) return hide.Nose;
                float dark = 1f - Smooth(_body.W(0.3f), _body.Brisket + _body.W(0.05f), p.Y);
                return Vector3.Lerp(Coat(p), hide.Legs, dark);
            }
            if (part == SaurianPart.Head) return Face(p, normal);
            return Coat(p);
        }

        // The head: the skull the colour of the coat, the muzzle paler, the eyes in darker hair, and a black nose and lips.
        private Vector3 Face(Vector3 p, Vector3 normal)
        {
            BearHide hide = _body.Hide;
            Vector3 local = ToHead(p) / _body.HeadLength;
            float forward = local.Z, up = local.Y, across = MathF.Abs(local.X);

            if (forward > 0.93f && up > -0.16f) return hide.Nose;
            float lip = MathHelper.Lerp(-0.2f, -0.17f, Smooth(0.5f, 0.92f, forward));
            if (forward > 0.5f && MathF.Abs(up - lip) < 0.016f && across > 0.03f) return hide.Nose;

            Vector3 colour = Vector3.Lerp(hide.Face, hide.Muzzle, Smooth(0.48f, 0.66f, forward));
            float eyes = 1f - Smooth(0.04f, 0.1f, new Vector2(across - 0.16f, forward - 0.5f).Length);
            colour = Vector3.Lerp(colour, hide.Legs, eyes * 0.5f);
            float grizzle = 0.92f + 0.16f * Noise(p, _body.W(0.1f), 31u);
            return colour * grizzle;
        }

        // ---- Coat.

        /// <summary>Where its coat grows (SHELLS.md): over all of it, shorter on the muzzle, and none on its pads, nose or lips.</summary>
        public override float Shell(Vector3 p, Vector3 normal, SaurianPart part)
        {
            if (IsLeg(part))
            {
                if (p.Y < _body.W(0.014f) && normal.Y < -0.3f) return 0f;
                return p.Y < _body.AnkleHeight + _body.W(0.03f) ? 0.8f : 1f;
            }
            if (part != SaurianPart.Head) return 1f;

            Vector3 local = ToHead(p) / _body.HeadLength;
            if (local.Z > 0.93f && local.Y > -0.16f) return 0f;
            float lip = MathHelper.Lerp(-0.2f, -0.17f, Smooth(0.5f, 0.92f, local.Z));
            if (local.Z > 0.5f && MathF.Abs(local.Y - lip) < 0.022f) return 0.2f;
            return 1f - 0.3f * Smooth(0.5f, 0.85f, local.Z);
        }
    }
}
