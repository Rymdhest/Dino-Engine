using System.Globalization;

namespace Dino_Engine.Rendering.Renderers.Lighting
{
    /// <summary>
    /// How soft the sun's shadows are (<see cref="RenderEngine.SunShadows"/>; live in the F3 Render window, and from a scripted run with --render
    /// sunsize=1 and the rest). A shadow is as soft as the sun's disc is wide where it's cast from: hard where something meets the ground, softer the
    /// further off what casts it, faint where something small stands high (Lighting/SunShadow.glsl, SHADOWS.md).
    /// </summary>
    public class SunShadowSettings
    {
        /// <summary>Off, every shadow is as soft as the sharpest, as they all were before.</summary>
        public bool Soft = true;

        /// <summary>
        /// Degrees across the sun's disc as shadows see it: the real sun's 0.53. Wider softens every shadow cast from any height, and costs as much
        /// more to work out as the square of how much wider each gets.
        /// </summary>
        public float SunSize = 0.53f;

        /// <summary>
        /// The radius, in shadow map texels, of the filter on even the hardest shadow: under about 2.5 its edge steps, and a field in the wind flickered
        /// a tenth more at 2.75 than at 4, the fixed filter's radius before shadows softened, as its blades' shadows were that much sharper. The contact
        /// shadows draw what's sharper than this.
        /// </summary>
        public float Sharpest = 4f;

        /// <summary>
        /// The radius, in shadow map texels, that no shadow is softened beyond, which bounds what one pixel can cost: at the sun's own size, what
        /// stands over about 40 metres above what it shades softens no further.
        /// </summary>
        public float Softest = 8f;

        /// <summary>0 the picture, 1 how soft each shadow is (black sharpest, white softest) and 2 the shadow alone on white (Ambient_Light.frag).</summary>
        public int Debug = 0;

        /// <summary>Sets a value by name, as --render gives them. Returns whether there is one of that name.</summary>
        public bool Set(string name, string value)
        {
            bool on = value != "0" && value != "false" && value != "off";
            float number = float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out float parsed) ? parsed : 0f;
            switch (name)
            {
                case "softshadows": Soft = on; break;
                case "sunsize": SunSize = number; break;
                case "shadowsharpest": Sharpest = number; break;
                case "shadowsoftest": Softest = number; break;
                case "shadowdebug": Debug = (int)number; break;
                default: return false;
            }
            return true;
        }
    }
}
