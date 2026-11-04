using System.Globalization;

namespace Dino_Engine.Rendering.Renderers.Lighting
{
    /// <summary>
    /// How the scene is lit and exposed (<see cref="RenderEngine.Lighting"/>; live in the F3 Render window, and from a
    /// scripted run with --render). The switches take one source of light out at a time, to see what the others do alone.
    /// </summary>
    public class LightingSettings
    {
        public bool Sun = true;
        public bool Sky = true;
        public bool LocalLights = true;
        public bool Emission = true;
        public bool Fog = true;
        public bool Bloom = true;

        /// <summary>At least this much night everywhere, 0 to 1, whatever the game has made of it (NightfallSingleton).</summary>
        public float Night = 0f;

        /// <summary>How much of the sky's colour reaches a surface facing straight up. Water uses the same (WATER_SKY_AMBIENT).</summary>
        public float SkyFill = 1.1f;

        /// <summary>
        /// How much of the sun the air scatters into the shade, from all around. This is what keeps the hero readable in the shadow
        /// of a house under a low sun, when the sky alone is dim; it takes its colour and strength from the sun.
        /// </summary>
        public float SunFill = 0.04f;

        /// <summary>How much light the ground throws back up, onto whatever faces it.</summary>
        public float GroundAlbedo = 0.25f;

        public float LocalLightScale = 1f;
        public float EmissionScale = 1f;

        /// <summary>Applied before tone mapping. The MSAA resolve weighs samples by how bright this makes them look.</summary>
        public float Exposure = 0.42f;

        /// <summary>How bright light must be, in scene radiance, to bloom. Sunlit surfaces stay under it, or the picture hazes over.</summary>
        public float BloomThreshold = 4.5f;
        public float BloomKnee = 1.5f;
        public float BloomStrength = 0.5f;

        /// <summary>How many point lights have a shadow map at once: the nearest to the hero that can be seen.</summary>
        public int ShadowedPointLights = 4;

        /// <summary>Metres from the hero past which a point light gives up its shadow map.</summary>
        public float ShadowDistance = 60f;

        /// <summary>Sets a value by name, as --render gives them. Returns whether there is one of that name.</summary>
        public bool Set(string name, string value)
        {
            bool on = value != "0" && value != "false" && value != "off";
            float number = float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out float parsed) ? parsed : 0f;
            switch (name)
            {
                case "sun": Sun = on; break;
                case "sky": Sky = on; break;
                case "lights": LocalLights = on; break;
                case "emission": Emission = on; break;
                case "fog": Fog = on; break;
                case "bloom": Bloom = on; break;
                case "night": Night = number; break;
                case "skyfill": SkyFill = number; break;
                case "sunfill": SunFill = number; break;
                case "ground": GroundAlbedo = number; break;
                case "lightscale": LocalLightScale = number; break;
                case "emissionscale": EmissionScale = number; break;
                case "exposure": Exposure = number; break;
                case "bloomthreshold": BloomThreshold = number; break;
                case "bloomstrength": BloomStrength = number; break;
                case "shadows": ShadowedPointLights = (int)number; break;
                default: return false;
            }
            return true;
        }
    }
}
