using Dino_Engine.Modelling.Procedural.Flora;
using Dino_Engine.Modelling.Procedural.Gear;
using OpenTK.Mathematics;

namespace Dino_Engine.Modelling.Procedural.Saurian
{
    /// <summary>
    /// The colours of a hide, worked out at each point from where it is on the body rather than where it is in space, so that stripes run across the back and rings go round the tail whatever the mesh
    /// is like. Dark above and lighter below, as nearly every animal is and as Sinosauropteryx is known to have been, with a dark mask through the eye and a banded tail on those that have them.
    /// </summary>
    public sealed class SaurianPaint
    {
        private readonly SaurianBody _body;
        private readonly SaurianCoat _coat;
        private readonly uint _seed;

        public SaurianPaint(SaurianBody body)
        {
            _body = body;
            _coat = body.Coat;
            _seed = body.Coat.Seed;
        }

        public SaurianCoat Coat => _coat;

        private static float Smooth(float from, float to, float value)
        {
            float t = Math.Clamp((value - from) / (to - from), 0f, 1f);
            return t * t * (3f - 2f * t);
        }

        private float Noise(Vector3 p, float size, uint salt) => FloraNoise.Value(p / size, _seed + salt) * 2f - 1f;

        /// <summary>How far up the side of the body a point is, from -1 under the belly to 1 along the spine, and how far along the body.</summary>
        public float Height(Vector3 p, out float along)
        {
            along = _body.Axis.Nearest(p, out _);
            Vector3 centre = _body.Axis.At(along);
            float deep = MathF.Max(_body.Axis.DeepAt(along), 1e-3f);
            return Math.Clamp((p.Y - centre.Y) / deep, -1.5f, 1.5f);
        }

        public Vector3 Hide(Vector3 p, Vector3 normal, SaurianPart part, out int material)
        {
            material = GearMaterials.Leather;
            SaurianCoat c = _coat;
            float scale = _body.Length;

            float along;
            float height;
            if (part == SaurianPart.Head)
            {
                along = _body.Axis.Nearest(p, out _);
                Vector3 local = _body.Head.ToHead(p);
                float lip = _body.Head.Lip(local.Z);
                float above = (local.Y - lip) / MathF.Max(_body.Head.DepthAt(local.Z) * 1.24f, 1e-3f);
                height = above * 2f - 0.8f;

                // The roof of the mouth, which shows when it's open.
                if (local.Y - lip < _body.Head.Length * 0.012f && Vector3.Dot(normal, _body.Head.Up) < -0.5f) return c.Mouth;
            }
            else if (part == SaurianPart.Body) height = Height(p, out along);
            else
            {
                // A limb is dark on the outside and light within, and darker towards the ground.
                along = _body.HipAt;
                height = Vector3.Dot(normal, Vector3.UnitY) * 0.6f + Math.Sign(p.X) * normal.X * 0.6f - 0.2f;
            }

            float wobble = Noise(p, scale * 0.06f, 11u) * 0.22f + Noise(p, scale * 0.015f, 12u) * 0.08f;
            float h = height + wobble;
            Vector3 colour = Vector3.Lerp(c.Belly, c.Flank, Smooth(-0.6f, -0.05f, h));
            colour = Vector3.Lerp(colour, c.Back, Smooth(0.12f, 0.72f, h));

            bool onTail = part == SaurianPart.Body && along < _body.HipAt;
            bool onTrunk = part == SaurianPart.Body && along >= _body.TailRootAt && along <= _body.NeckAt;
            bool onNeck = part == SaurianPart.Body && along > _body.NeckAt;

            bool striped = onTrunk || onNeck || (onTail && along > _body.HipAt * 0.45f);
            float contrast = c.Contrast;
            if ((c.Patterns & HidePattern.Stripes) != 0 && striped)
            {
                // Bands across the back that fade down the flanks, ragged at their edges.
                float phase = along / c.StripeEvery + c.StripePhase + Noise(p, scale * 0.05f, 21u) * 0.35f;
                float wave = MathF.Sin(phase * MathF.Tau);
                float band = Smooth(0.35f, 0.75f, wave) * Smooth(-0.45f, 0.35f, h);
                colour = Vector3.Lerp(colour, c.Pattern, band * 0.82f * contrast);
            }

            if ((c.Patterns & HidePattern.Bands) != 0 && onTail && along < _body.HipAt * 0.7f)
            {
                // Rings all the way round the end of the tail.
                float rings = MathF.Sin((along / (c.StripeEvery * 1.15f) + c.StripePhase * 0.5f) * MathF.Tau);
                float strength = Smooth(_body.HipAt * 0.7f, _body.HipAt * 0.45f, along);
                colour = Vector3.Lerp(colour, c.Pattern, Smooth(0.2f, 0.6f, rings) * strength * 0.9f * contrast);
            }

            if ((c.Patterns & HidePattern.Spots) != 0 && h > -0.5f)
            {
                float spots = FloraNoise.Value(p / c.SpotSize, _seed + 31u);
                float spot = Smooth(0.68f, 0.78f, spots) * Smooth(-0.5f, 0.1f, h);
                colour = Vector3.Lerp(colour, c.Pattern, spot * 0.85f * contrast);
            }

            if ((c.Patterns & HidePattern.Saddles) != 0 && part == SaurianPart.Body && h > 0f)
            {
                float saddle = FloraNoise.Value(new Vector3(along / (scale * 0.09f), 0.5f, 0.5f), _seed + 41u);
                colour = Vector3.Lerp(colour, c.Pattern, Smooth(0.55f, 0.7f, saddle) * Smooth(0f, 0.55f, h) * 0.8f * contrast);
            }

            if ((c.Patterns & HidePattern.Mottle) != 0)
            {
                float mottle = FloraNoise.Value(p / (scale * 0.006f), _seed + 51u);
                colour *= 1f - 0.12f * contrast + 0.24f * contrast * mottle;
            }

            if ((c.Patterns & HidePattern.Mask) != 0 && part == SaurianPart.Head)
            {
                // A dark stripe through the eye, from the snout to behind the jaw.
                Vector3 local = _body.Head.ToHead(p);
                float eyeZ = _body.Head.EyeAt * _body.Head.Length;
                float eyeY = _body.Head.Lip(eyeZ) + _body.Head.DepthAt(eyeZ) * 1.24f * 0.62f;
                float across = MathF.Abs(local.Y - (eyeY + (local.Z - eyeZ) * -0.12f)) / (_body.Head.Length * 0.07f);
                float mask = (1f - Smooth(0.5f, 1f, across)) * Smooth(_body.Head.Length * 0.85f, eyeZ, local.Z) * Smooth(0.2f, 0.6f, MathF.Abs(normal.X));
                colour = Vector3.Lerp(colour, c.Pattern, mask * 0.85f * contrast);
            }

            if (part == SaurianPart.Head && _body.Head.Sauropod)
            {
                SaurianHead head = _body.Head;
                Vector3 local = head.ToHead(p);

                // A sauropod's dome is the one thing about it that's shown off, a little brighter than the rest, as a cassowary's casque is.
                if (head.Arch > 0f)
                {
                    float roof = head.Lip(local.Z) + head.DepthAt(local.Z) * 1.24f;
                    float near = MathF.Exp(-MathF.Pow((local.Z - head.ArchAt * head.Length) / (head.Length * 0.2f), 2f));
                    float dome = near * Smooth(roof - head.Arch * 0.3f, roof + head.Arch * 0.4f, local.Y);
                    colour = Vector3.Lerp(colour, c.Display, dome * 0.55f);
                }

                // A soft dark ring round each eye, which is what lets an eye be found at all from far off.
                float eyeZ = head.EyeAt * head.Length;
                Vector3 eye = new Vector3(MathF.Sign(local.X) * head.WidthAt(eyeZ) * 0.98f, SaurianSculpt.EyeHeight(head), eyeZ);
                float ring = 1f - Smooth(0.55f, 1f, (local - eye).Length / (head.EyeSize * 1.6f));
                colour *= 1f - 0.4f * ring;
            }

            // A throat that's shown off.
            if (onNeck && _body.Head.Throat > 0f && h < -0.2f && along > _body.NeckTopAt - (_body.HeadAt - _body.NeckTopAt) * 0.4f)
            {
                colour = Vector3.Lerp(colour, c.Display, Smooth(-0.2f, -0.6f, h) * 0.9f);
            }

            // Feet are darker, and so is everything near the ground.
            if (part is not SaurianPart.Body and not SaurianPart.Head)
            {
                float low = Smooth(_body.HipJoint * 0.55f, 0f, p.Y);
                colour = Vector3.Lerp(colour, colour * 0.55f + c.Pattern * 0.15f, low);
            }

            // The Reich marks what's theirs in oxblood: a band over the snout and a ring round the tail, which is how one pack is told from another across a field.
            if (_body.Harnessed)
            {
                float paint = 0f;
                if (part == SaurianPart.Head)
                {
                    Vector3 local = _body.Head.ToHead(p);
                    float band = 1f - Smooth(_body.Head.Length * 0.05f, _body.Head.Length * 0.075f, MathF.Abs(local.Z - _body.Head.Length * 0.6f));
                    paint = band * Smooth(_body.Head.Lip(local.Z) + _body.Head.DepthAt(local.Z) * 0.4f, _body.Head.Lip(local.Z) + _body.Head.DepthAt(local.Z) * 0.7f, local.Y);
                }
                else if (onTail) paint = 1f - Smooth(c.StripeEvery * 0.35f, c.StripeEvery * 0.55f, MathF.Abs(along - _body.HipAt * 0.68f));
                float ragged = 0.75f + 0.25f * FloraNoise.Value(p / (scale * 0.01f), _seed + 61u);
                colour = Vector3.Lerp(colour, SaurianHarness.Oxblood, Math.Clamp(paint * ragged, 0f, 1f));
            }

            return colour;
        }
    }
}
