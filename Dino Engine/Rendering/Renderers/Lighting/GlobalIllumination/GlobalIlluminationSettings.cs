using System.Globalization;

namespace Dino_Engine.Rendering.Renderers.Lighting.GlobalIllumination
{
    /// <summary>
    /// How light bounces (<see cref="RenderEngine.GlobalIllumination"/>; a toggle in the game's settings, live in the F3 Render window and
    /// from a scripted run with --render gi=1). Read GI.md.
    /// </summary>
    public class GlobalIlluminationSettings
    {
        /// <summary>Off, the ambient light is the flat sky and ground it always was, nothing of this runs, and its memory is given back.</summary>
        public bool Enabled = true;

        /// <summary>
        /// Shows one part in place of the picture: 1 the voxels' colours, 2 the light leaving the voxels, 3 the light GI gives every
        /// surface, on white, 4 where the probes reach and where they don't, 5 as 3 with each pixel taking only the probe that counts most,
        /// so that each probe shows as a patch of its own. While one is up, what GI holds is written to the console every 120 frames.
        /// </summary>
        public int Debug = 0;

        /// <summary>How much of the light that reaches a surface from a bounce bounces on again, 0 to 1.</summary>
        public float Bounce = 1f;

        /// <summary>What the light GI gathers is multiplied by: 1 is as it falls.</summary>
        public float Strength = 1f;

        /// <summary>
        /// Seconds what's gathered takes to settle, where the same spot of the world was last frame: whether a spot sees a probe is a yes
        /// or a no from one point, which flickers by a rail as the camera slides, and this makes it how much of the spot does. Long enough
        /// to steady that, short enough that a lamp coming on isn't seen to lag; 0 for none.
        /// </summary>
        public float Settle = 0.12f;

        /// <summary>Metres the screen-space occlusion reaches while GI is on: GI has everything further, so this is only the contact.</summary>
        public float ContactReach = 0.35f;

        /// <summary>
        /// A pixel of the picture, counted from its top left, whose probes are written to the console with the debug views' report
        /// (--render gidebug=3,giprobe=860:250); negative for none.
        /// </summary>
        public int ProbeX = -1, ProbeY = -1;

        /// <summary>Sets a value by name, as --render gives them. Returns whether there is one of that name.</summary>
        public bool Set(string name, string value)
        {
            bool on = value != "0" && value != "false" && value != "off";
            float number = float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out float parsed) ? parsed : 0f;
            switch (name)
            {
                case "gi": Enabled = on; break;
                case "gidebug": Debug = (int)number; break;
                case "gibounce": Bounce = number; break;
                case "gistrength": Strength = number; break;
                case "gicontact": ContactReach = number; break;
                case "gisettle": Settle = number; break;
                case "giprobe":
                    string[] xy = value.Split(':');
                    if (xy.Length != 2 || !int.TryParse(xy[0], out ProbeX) || !int.TryParse(xy[1], out ProbeY)) ProbeX = ProbeY = -1;
                    break;
                default: return false;
            }
            return true;
        }
    }
}
