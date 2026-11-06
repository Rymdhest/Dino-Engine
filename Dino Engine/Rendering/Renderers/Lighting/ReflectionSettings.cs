using System.Globalization;

namespace Dino_Engine.Rendering.Renderers.Lighting
{
    /// <summary>How reflections are traced (<see cref="RenderEngine.Reflections"/>; live in the F3 Render window).</summary>
    public class ReflectionSettings
    {
        /// <summary>Rays traced on screen. Off, reflections are the sky and the world's shape alone.</summary>
        public bool ScreenRays = true;

        /// <summary>How many steps a ray takes through the depth pyramid before giving up.</summary>
        public int MaxSteps = 56;

        /// <summary>Levels of the depth pyramid a ray may climb to. Past a few, a step skips more than rays ever need.</summary>
        public int PyramidLevels = 7;

        /// <summary>Metres behind a surface that a ray still counts as having struck it, plus this share of the distance from the eye.</summary>
        public float ThicknessBase = 0.5f;
        public float ThicknessPerMetre = 0.02f;

        /// <summary>How much of the last frame a reflection keeps, 0 to 0.98, on the roughest surfaces. Mirrors take less.</summary>
        public float History = 0.9f;

        /// <summary>
        /// Shows one part of the reflection in place of the picture: 1 where the rays found something, 2 how much sky a ray can see, 3 all that's reflected, before any material has had its say, 4 the sky map.
        /// </summary>
        public int Debug = 0;

        /// <summary>Sets a value by name, as --render gives them. Returns whether there is one of that name.</summary>
        public bool Set(string name, string value)
        {
            bool on = value != "0" && value != "false" && value != "off";
            float number = float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out float parsed) ? parsed : 0f;
            switch (name)
            {
                case "reflectionrays": ScreenRays = on; break;
                case "reflectionsteps": MaxSteps = (int)number; break;
                case "reflectionhistory": History = number; break;
                case "reflectiondebug": Debug = (int)number; break;
                default: return false;
            }
            return true;
        }
    }
}
