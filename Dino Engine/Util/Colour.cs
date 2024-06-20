

using OpenTK.Mathematics;

namespace Dino_Engine.Util
{
    public class Colour
    {
        private float _red;
        private float _green;
        private float _blue;
        private float _alpha= 1f;
        private float _intensity = 1f;
        public Colour(Vector3 colour) : this(colour.X, colour.Y, colour.Z) {}
        public Colour(float red, float green, float blue)
        {
            _red = red;
            _green = green;
            _blue = blue;
        }
        public Colour(float red, float green, float blue, float intensity)
        {
            _red = red;
            _green = green;
            _blue = blue;
            _intensity = intensity;
        }
        public Colour(float red, float green, float blue, float intensity, float alpha)
        {
            _red = red;
            _green = green;
            _blue = blue;
            _alpha = alpha;
            _intensity = intensity;
        }

        public float Red { get => _red; set => _red = value; }
        public float Green { get => _green; set => _green = value; }
        public float Blue { get => _blue; set => _blue = value; }
        public float Alpha { get => _alpha; set => _alpha = value; }
        public float Intensity { get => _intensity; set => _intensity = value; }

        public Vector3 ToVector3()
        {
            return new Vector3(Red, Green, Blue) * Intensity;
        }

        public Vector4 ToVector4()
        {
            return new Vector4(Red * Intensity, Green * Intensity, Blue * Intensity, Alpha);
        }
    }
}
