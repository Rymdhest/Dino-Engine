using System.Globalization;

namespace Dino_Engine.Rendering.Renderers.Lighting
{
    /// <summary>
    /// The sun's contact shadows, traced in screen space where the shadow map can't see (<see cref="RenderEngine.ContactShadows"/>; live in the F3
    /// Render window, and from a scripted run with --render contact=0 and the rest). Read SHADOWS.md.
    /// </summary>
    public class ContactShadowSettings
    {
        public bool Enabled = true;

        /// <summary>
        /// How thick anything is taken to be behind what's seen of it, as a share of the distance left to the far plane in the depth buffer's own
        /// terms, which comes to about half a percent of how far it is from the camera at 0.005 (Bend Studio's starting value).
        /// </summary>
        public float Thickness = 0.005f;

        /// <summary>How far apart two neighbours' depths may be, in the same terms, and still be read as one surface between them.</summary>
        public float EdgeThreshold = 0.02f;

        /// <summary>How sharply a sample goes from lighting to shading: 1 and up.</summary>
        public float Contrast = 4f;

        /// <summary>
        /// Metres something has to stand in front of the ray, along the view, to cast anything at all: less, and it's a step in the depth or a bump of
        /// the ground's relief, whose shadow the relief draws itself.
        /// </summary>
        public float MinHeight = 0.03f;

        /// <summary>
        /// Pixels along the ray towards the sun past which a sample starts to count for less, and where it counts for nothing (at most the shader's
        /// SAMPLE_COUNT): the shadow map has everything further, softened by how far it is (SHADOWS.md).
        /// </summary>
        public float FadeFrom = 10f;
        public float FadeTo = 32f;

        /// <summary>0 the picture, 1 the contact shadows alone on white (Ambient_Light.frag).</summary>
        public int Debug = 0;

        /// <summary>Sets a value by name, as --render gives them. Returns whether there is one of that name.</summary>
        public bool Set(string name, string value)
        {
            bool on = value != "0" && value != "false" && value != "off";
            float number = float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out float parsed) ? parsed : 0f;
            switch (name)
            {
                case "contact": Enabled = on; break;
                case "contactthickness": Thickness = number; break;
                case "contactedge": EdgeThreshold = number; break;
                case "contactcontrast": Contrast = number; break;
                case "contactmin": MinHeight = number; break;
                case "contactfadefrom": FadeFrom = number; break;
                case "contactfadeto": FadeTo = number; break;
                case "contactdebug": Debug = (int)number; break;
                default: return false;
            }
            return true;
        }
    }
}
