

using OpenTK.Mathematics;

namespace Dino_Engine.Util
{
    public struct Colour
    {
        private float _red;
        private float _green;
        private float _blue;
        private float _alpha= 1f;
        private float _intensity = 1f;
        public Colour(Vector3 colour) : this(colour.X, colour.Y, colour.Z) {}

        public Colour(int red, int green, int blue)
        {
            _red = red/255f;
            _green = green/255f;
            _blue = blue/255f;
        }

        public Colour(int red, int green, int blue, float intensity)
        {
            _red = red / 255f;
            _green = green / 255f;
            _blue = blue / 255f;
            _intensity = intensity;
        }

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

        public override string ToString()
        {
            return $"Red: {_red} Green: {_green} Blue: {_blue} Alpha: {_alpha} Intesity: {_intensity}";
        }
        public static Colour mix(Colour left, Colour right, float mix)
        {
            float r = MyMath.lerp(left.Red, right.Red, mix);
            float g = MyMath.lerp(left.Green, right.Green, mix);
            float b = MyMath.lerp(left.Blue, right.Blue, mix);
            float i = MyMath.lerp(left.Intensity, right.Intensity, mix);
            float a = MyMath.lerp(left.Alpha, right.Alpha, mix);

            return new Colour(r, g, b, i, a);
        }
    }
}
