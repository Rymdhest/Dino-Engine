using Dino_Engine.Modelling.Procedural.Flora;
using Dino_Engine.Modelling.Procedural.Gear;
using Dino_Engine.Modelling.Procedural.Saurian;
using OpenTK.Mathematics;

namespace Dino_Engine.Modelling.Procedural.Character
{
    /// <summary>
    /// What colour a wolf's skin is and where its coat grows. How the skin hangs on its bones and how its texture lies are every animal on four legs' (<see cref="QuadrupedDress"/>).
    /// </summary>
    public sealed class WolfDress : QuadrupedDress
    {
        private readonly WolfBody _body;
        private readonly uint _seed;

        public WolfDress(WolfBody body) : base(body.Frame())
        {
            _body = body;
            _seed = (uint)(body.Seed ^ (body.Seed >> 32));
        }

        private float Noise(Vector3 p, float size, uint salt) => FloraNoise.Value(p / size, _seed + salt);

        // ---- Colour.

        // The trunk: pale underneath and up the throat and chest, grizzled on the flanks, and a darker saddle over the back from the shoulders to the tail, as most wolves have.
        private Vector3 Coat(Vector3 p)
        {
            WolfHide hide = _body.Hide;
            BoarStation station = _body.Station(MathF.Min(p.Z, _body.Trunk[^1].Z));
            float height = (p.Y - station.Middle) / MathF.Max(station.Deep, 1e-3f);
            Vector3 colour = Vector3.Lerp(hide.Belly, hide.Flank, Smooth(-0.6f, 0.05f, height));

            float saddle = Smooth(_body.HindZ - _body.W(0.12f), _body.HindZ + _body.W(0.05f), p.Z) * (1f - Smooth(_body.ForeZ + _body.W(0.05f), _body.ChestFront + _body.W(0.12f), p.Z));
            colour = Vector3.Lerp(colour, hide.Back, Smooth(0.3f, 0.85f, height) * MathF.Max(saddle, 0.35f));

            // A pale throat and chest, under the ruff.
            float front = Smooth(_body.ForeZ - _body.W(0.04f), _body.ChestFront, p.Z) * Smooth(0.35f, -0.25f, height);
            colour = Vector3.Lerp(colour, hide.Belly, front);

            float grizzle = 0.9f + 0.2f * Noise(p, _body.W(0.18f), 11u) + 0.12f * (Noise(p, _body.W(0.045f), 12u) - 0.5f);
            return colour * grizzle;
        }

        public override Vector3 Colour(Vector3 p, Vector3 normal, SaurianPart part, out int material, out float glow)
        {
            glow = 1f;
            material = GearMaterials.Hide;
            WolfHide hide = _body.Hide;
            if (IsLeg(part))
            {
                // Black pads under the paws, the paws and lower legs the colour of its legs, paler on the inside, running up into the coat.
                if (p.Y < _body.W(0.014f) && normal.Y < -0.3f) return hide.Nose;
                float dark = 1f - Smooth(_body.W(0.26f), _body.Brisket + _body.W(0.05f), p.Y);
                Vector3 colour = Vector3.Lerp(Coat(p), hide.Legs, dark);
                float inside = Smooth(0.1f, 0.6f, -MathF.Sign(p.X) * normal.X);
                colour = Vector3.Lerp(colour, hide.Belly, inside * 0.5f);
                if (p.Y < _body.AnkleHeight + _body.W(0.01f)) colour *= 0.85f;
                return colour;
            }
            if (part == SaurianPart.Head) return Face(p, normal);
            return Coat(p);
        }

        // The mask a wolf's face is known by: pale cheeks, chin and sides of the muzzle, the bridge of the nose and the brow darker, the back of the head the colour of its neck, and a black nose
        // and lips.
        private Vector3 Face(Vector3 p, Vector3 normal)
        {
            WolfHide hide = _body.Hide;
            Vector3 local = ToHead(p) / _body.HeadLength;
            float forward = local.Z, up = local.Y, across = MathF.Abs(local.X);

            if (forward > 0.92f && up > -0.1f) return hide.Nose;
            float lip = MathHelper.Lerp(-0.13f, -0.1f, Smooth(0.45f, 0.92f, forward));
            if (forward > 0.42f && MathF.Abs(up - lip) < 0.014f && across > 0.02f) return hide.Nose;

            Vector3 colour = hide.Muzzle;
            float lower = Smooth(0.06f, -0.04f, up);
            float cheeks = Smooth(0.09f, 0.17f, across) * (1f - Smooth(0.25f, 0.45f, forward));
            colour = Vector3.Lerp(colour, hide.Face, MathF.Max(lower, cheeks) * 0.9f);

            // The back of the head and its ears' roots go into the neck's colour.
            colour = Vector3.Lerp(colour, hide.Flank, Smooth(0.22f, 0.04f, forward) * Smooth(-0.05f, 0.1f, up));

            float grizzle = 0.92f + 0.16f * Noise(p, _body.W(0.1f), 31u);
            return colour * grizzle;
        }

        // ---- Coat.

        /// <summary>Where its coat grows (SHELLS.md): over all of it, shorter down the legs and the muzzle, and none on the pads, the nose or the lips.</summary>
        public override float Shell(Vector3 p, Vector3 normal, SaurianPart part)
        {
            if (IsLeg(part))
            {
                if (p.Y < _body.W(0.014f) && normal.Y < -0.3f) return 0f;
                if (p.Y < _body.AnkleHeight + _body.W(0.02f)) return 0.6f;
                return MathHelper.Lerp(0.8f, 1f, Smooth(_body.W(0.3f), _body.Brisket, p.Y));
            }
            if (part != SaurianPart.Head) return 1f;

            Vector3 local = ToHead(p) / _body.HeadLength;
            if (local.Z > 0.9f && local.Y > -0.1f) return 0f;
            float lip = MathHelper.Lerp(-0.13f, -0.1f, Smooth(0.45f, 0.92f, local.Z));
            if (local.Z > 0.42f && MathF.Abs(local.Y - lip) < 0.02f) return 0.2f;
            return 1f - 0.3f * Smooth(0.45f, 0.85f, local.Z);
        }
    }
}
