using System.Globalization;
using Dino_Engine.Textures;
using OpenTK.Graphics.OpenGL;
using OpenTK.Mathematics;

namespace Dino_Engine.Rendering.Renderers.Lighting
{
    /// <summary>
    /// How one kind of subsurface scattering behaves (SUBSURFACE.md, Shaders/Lighting/lightingCalc.glsl). Every number is a multiple of what a surface says of itself, which is how much
    /// it scatters, texel by texel, from its recipe or its card or its blade.
    /// </summary>
    public sealed class SubsurfaceProfile
    {
        /// <summary>Flesh and frost: metres each colour goes in it before it's down to a third (1/e), at an amount of 1. Red goes furthest through blood, blue through ice.</summary>
        public Vector3 Reach;

        /// <summary>Thin things: how far what's let through wraps round towards the side facing the light, as light that's crossed something is spread: 0 for none.</summary>
        public float BackWrap;

        /// <summary>The colour light comes out in. Of a thin thing a multiple of its own (deepened by <see cref="Saturation"/>); of flesh or frost its own, blood's or ice's.</summary>
        public Vector3 Colour = Vector3.One;

        /// <summary>Flesh and frost: how much of the surface's own hue is in the light that comes out, 0 to 1, as what comes out passes the pigment on its way.</summary>
        public float Hue;

        /// <summary>How much of a light gets through, for each unit of it reaching the surface.</summary>
        public float Direct = 1f;

        /// <summary>Thin things: how much of the light from all round gets through, from the half of the world behind them.</summary>
        public float Ambient;

        /// <summary>A canopy's: the light leaves pass on among themselves before it comes out, the same every way, as a share of what comes straight through.</summary>
        public float Fill;

        /// <summary>Thin things: how much deeper the colour of what crosses them is than of what bounces off them, 1 for the same.</summary>
        public float Saturation = 1f;

        /// <summary>Flesh and frost: how far each colour of their own diffuse light goes on past where the light stops, at an amount of 1, as light that's got in comes out further on.</summary>
        public Vector3 Wrap;
    }

    /// <summary>
    /// Subsurface scattering, live in the F3 Render window and from a scripted run with --render (<see cref="Set"/>). A profile for each <see cref="SubsurfaceKind"/>, in its order,
    /// which <see cref="SubsurfaceProfiles"/> sends to the shaders each frame. Read SUBSURFACE.md.
    /// </summary>
    public sealed class SubsurfaceSettings
    {
        /// <summary>All of it: off, every surface is lit as if light stopped at it.</summary>
        public bool On = true;

        /// <summary>Whether flesh lets the sun through where it's thin, measured in a map of flesh alone from the sun (<see cref="FleshDepth"/>). Off, that map isn't drawn, and flesh only has its light wrapped past the terminator.</summary>
        public bool Thickness = true;

        /// <summary>A picture of it in place of the lit scene: 1 what every surface says of light under it, by kind and amount; 2 what of the sun gets through flesh. 0 for the scene.</summary>
        public int Debug;

        /// <summary>Cloth, paper, petals: thin, with faces of their own.</summary>
        public readonly SubsurfaceProfile Sheet = new SubsurfaceProfile
        {
            BackWrap = 0.3f,
            Colour = Vector3.One,
            Direct = 1f,
            Ambient = 0.8f,
            Saturation = 1.25f,
        };

        /// <summary>Leaves, blades, moss and fur. What comes through them is a warmer green than what bounces off, as a leaf lets yellow through better than blue.</summary>
        public readonly SubsurfaceProfile Canopy = new SubsurfaceProfile
        {
            BackWrap = 0.4f,
            Colour = new Vector3(1.06f, 1f, 0.78f),
            Direct = 1.8f,
            Ambient = 0.9f,
            Fill = 0.15f,
            Saturation = 1.35f,
        };

        /// <summary>
        /// Skin and hide. Real skin lets red go about 3.7 mm, green 1.4 mm and blue 0.7 mm (Christensen and Burley 2015), and the world is twice a real one's size; these reach
        /// ten times further, as from 33 m up a crest, a frill or a tail is a few pixels across, and what glows red through it has to be seen there. A body stays solid: a
        /// limb a span thick lets through a few hundredths of the light.
        /// </summary>
        public readonly SubsurfaceProfile Flesh = new SubsurfaceProfile
        {
            Reach = new Vector3(0.07f, 0.021f, 0.012f),
            Colour = new Vector3(1f, 0.42f, 0.28f),
            Hue = 0.3f,
            Direct = 1f,
            Wrap = new Vector3(0.16f, 0.06f, 0.04f),
        };

        /// <summary>
        /// Snow and ice: what gets into them goes on mostly the way it was going and comes back out a little cooler (<see cref="SubsurfaceProfile.Direct"/> is how much of that
        /// there is), and their diffuse light goes on past the terminator, blue furthest, as ice takes a little red with every centimetre.
        /// </summary>
        public readonly SubsurfaceProfile Frost = new SubsurfaceProfile
        {
            Reach = new Vector3(0.05f, 0.07f, 0.09f),
            Colour = new Vector3(0.92f, 0.97f, 1f),
            Hue = 0.3f,
            Direct = 3.3f,
            Saturation = 1f,
            Wrap = new Vector3(0.1f, 0.14f, 0.2f),
        };

        /// <summary>The profile of a kind.</summary>
        public SubsurfaceProfile Of(SubsurfaceKind kind) => kind switch
        {
            SubsurfaceKind.Canopy => Canopy,
            SubsurfaceKind.Flesh => Flesh,
            SubsurfaceKind.Frost => Frost,
            _ => Sheet,
        };

        /// <summary>
        /// Sets a value by name, as --render gives them: sss=0 turns it all off, sssthick=0 the light through flesh, and sheet., canopy., flesh. or frost. followed by direct, ambient,
        /// fill, saturation, backwrap, hue, reach (in metres, red's, with green's and blue's kept in proportion), wrap (red's, likewise) or colour (r:g:b) sets one profile's number.
        /// Returns whether there is one of that name.
        /// </summary>
        public bool Set(string name, string value)
        {
            bool on = value != "0" && value != "false" && value != "off";
            float number = float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out float parsed) ? parsed : 0f;
            if (name == "sss") { On = on; return true; }
            if (name == "sssthick") { Thickness = on; return true; }
            if (name == "sssdebug") { Debug = (int)number; return true; }

            int dot = name.IndexOf('.');
            if (dot < 0) return false;
            SubsurfaceProfile? profile = name.Substring(0, dot) switch
            {
                "sheet" => Sheet,
                "canopy" => Canopy,
                "flesh" => Flesh,
                "frost" => Frost,
                _ => null,
            };
            if (profile == null) return false;
            switch (name.Substring(dot + 1))
            {
                case "direct": profile.Direct = number; break;
                case "ambient": profile.Ambient = number; break;
                case "fill": profile.Fill = number; break;
                case "saturation": profile.Saturation = number; break;
                case "backwrap": profile.BackWrap = number; break;
                case "hue": profile.Hue = number; break;
                case "reach": profile.Reach = profile.Reach.X > 0f ? profile.Reach * (number / profile.Reach.X) : new Vector3(number); break;
                case "wrap": profile.Wrap = profile.Wrap.X > 0f ? profile.Wrap * (number / profile.Wrap.X) : new Vector3(number); break;
                case "colour":
                    string[] parts = value.Split(':');
                    if (parts.Length != 3) return false;
                    profile.Colour = new Vector3(Parse(parts[0]), Parse(parts[1]), Parse(parts[2]));
                    break;
                default: return false;
            }
            return true;
        }

        private static float Parse(string text) => float.Parse(text, NumberStyles.Float, CultureInfo.InvariantCulture);
    }

    /// <summary>
    /// Sends <see cref="SubsurfaceSettings"/> to the shaders, as the SubsurfaceData block (Shaders/Lighting/lightingCalc.glsl): four rows a kind, in <see cref="SubsurfaceKind"/>'s order, and the
    /// switches. Written once a frame before the lights, and bound for good, as nothing else uses its binding.
    /// </summary>
    public static class SubsurfaceProfiles
    {
        public const int Binding = 14;
        private const int Kinds = 4;
        private const int Floats = (Kinds * 4 + 1) * 4;

        private static int _buffer;
        private static readonly float[] _data = new float[Floats];

        public static void Upload(SubsurfaceSettings settings)
        {
            for (int kind = 0; kind < Kinds; kind++)
            {
                SubsurfaceProfile p = settings.Of((SubsurfaceKind)kind);
                int at = kind * 16;
                Row(at, p.Reach, p.BackWrap);
                Row(at + 4, p.Colour, p.Hue);
                Row(at + 8, new Vector3(p.Direct, p.Ambient, p.Fill), p.Saturation);
                Row(at + 12, p.Wrap, 0f);
            }
            Row(Kinds * 16, new Vector3(settings.On ? 1f : 0f, settings.Thickness ? 1f : 0f, settings.Debug), 0f);

            if (_buffer == 0)
            {
                _buffer = GL.GenBuffer();
                GL.BindBuffer(BufferTarget.UniformBuffer, _buffer);
                GL.BufferData(BufferTarget.UniformBuffer, Floats * sizeof(float), IntPtr.Zero, BufferUsageHint.DynamicDraw);
            }
            GL.BindBuffer(BufferTarget.UniformBuffer, _buffer);
            GL.BufferSubData(BufferTarget.UniformBuffer, IntPtr.Zero, Floats * sizeof(float), _data);
            GL.BindBuffer(BufferTarget.UniformBuffer, 0);
            GL.BindBufferBase(BufferRangeTarget.UniformBuffer, Binding, _buffer);
        }

        public static void CleanUp()
        {
            if (_buffer != 0) GL.DeleteBuffer(_buffer);
            _buffer = 0;
        }

        private static void Row(int at, Vector3 xyz, float w)
        {
            _data[at] = xyz.X;
            _data[at + 1] = xyz.Y;
            _data[at + 2] = xyz.Z;
            _data[at + 3] = w;
        }
    }
}
