using System.Globalization;

namespace Dino_Engine.Rendering.Renderers.Lighting
{
    /// <summary>
    /// How the ambient occlusion is worked out (<see cref="RenderEngine.Occlusion"/>; live in the F3 Render window, and from a scripted run with
    /// --render ao=0 and the rest). Read OCCLUSION.md. While global illumination is on it has every occluder further than a contact, so this reaches
    /// only <see cref="GlobalIllumination.GlobalIlluminationSettings.ContactReach"/>; with it off, <see cref="Radius"/>.
    /// </summary>
    public class OcclusionSettings
    {
        public bool Enabled = true;

        /// <summary>Metres the occlusion reaches with global illumination off.</summary>
        public float Radius = 0.9f;

        /// <summary>How far in from the reach, as a share of it, a sample starts to count for less, so that nothing pops in at the edge.</summary>
        public float Fade = 0.6f;

        /// <summary>Metres deep anything solid is taken to be behind what's seen of it.</summary>
        public float Thickness = 0.5f;

        /// <summary>
        /// Metres deep a canopy is taken to be, leaves, blades, moss and fur, which let the sky in behind them: what it hides from anything solid. From
        /// another canopy it hides nothing, as a field darkens itself (OCCLUSION.md).
        /// </summary>
        public float ThinThickness = 0.08f;

        /// <summary>
        /// Slices through the hemisphere each pixel takes; its 4 x 4 tile takes sixteen times as many between them, which the denoise gathers, so one
        /// already comes to sixteen directions: a second was measured at a fifth of a millisecond more at 3440 x 1369 and couldn't be told from one.
        /// </summary>
        public int Slices = 1;

        /// <summary>Samples along each side of a slice.</summary>
        public int Steps = 3;

        /// <summary>Samples further out than this many pixels, as a power of two, read a coarser level of the depth.</summary>
        public float CoarseFrom = 3.3f;

        /// <summary>
        /// 0 the picture, 1 the occlusion alone on white, as the ambient light sees it (Ambient_Light.frag). Lamps and reflections still add on top, as
        /// they do over global illumination's pictures: look by day, or with --render lights=0.
        /// </summary>
        public int Debug = 0;

        /// <summary>Sets a value by name, as --render gives them. Returns whether there is one of that name.</summary>
        public bool Set(string name, string value)
        {
            bool on = value != "0" && value != "false" && value != "off";
            float number = float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out float parsed) ? parsed : 0f;
            switch (name)
            {
                case "ao": Enabled = on; break;
                case "aoradius": Radius = number; break;
                case "aofade": Fade = number; break;
                case "aothickness": Thickness = number; break;
                case "aothin": ThinThickness = number; break;
                case "aoslices": Slices = System.Math.Clamp((int)number, 1, 8); break;
                case "aosteps": Steps = System.Math.Clamp((int)number, 1, 12); break;
                case "aodebug": Debug = (int)number; break;
                default: return false;
            }
            return true;
        }
    }
}
