using Dino_Engine.Core;
using Dino_Engine.Rendering.Shells;
using OpenTK.Graphics.OpenGL;
using OpenTK.Mathematics;
using System.Globalization;

namespace Dino_Engine.Rendering.Renderers.Geometry
{
    /// <summary>How shells are drawn, read each frame (Graphics quality, <c>--render shells=0</c>, F3's Render window).</summary>
    public sealed class ShellSettings
    {
        /// <summary>Whether anything grows at all. Off, what grows is only what's painted on the surface under it.</summary>
        public bool Enabled = true;

        /// <summary>Layers to each pixel a growth's length covers on screen, over what a style draws (from its floor up). Fewer show as steps at a silhouette.</summary>
        public float LayersPerPixel = 1.5f;

        /// <summary>The most layers a style is drawn with, as a share of its own most.</summary>
        public float Most = 1f;

        /// <summary>Whether the ground's moss stands up too. It's most of the screen in a mossy wood and the least of what's seen from the camera, so it goes first.</summary>
        public bool Ground = true;

        /// <summary>Takes a setting by name, as <c>--render</c> gives them: <c>shells</c> 0 or 1, <c>shelllayers</c> per pixel, <c>shellmost</c> as a share, <c>shellground</c> 0 or 1.</summary>
        public bool Set(string name, string value)
        {
            switch (name)
            {
                case "shells":
                    Enabled = value != "0" && value != "false";
                    return true;
                case "shelllayers":
                    LayersPerPixel = Math.Max(float.Parse(value, CultureInfo.InvariantCulture), 0.25f);
                    return true;
                case "shellmost":
                    Most = Math.Clamp(float.Parse(value, CultureInfo.InvariantCulture), 0.1f, 4f);
                    return true;
                case "shellground":
                    Ground = value != "0" && value != "false";
                    return true;
            }
            return false;
        }
    }

    /// <summary>
    /// Shell texturing (SHELLS.md): moss and fur drawn as a stack of layers over the surface they grow on, each cut from one height field of clumps and the fine
    /// strands over them (Shells/Shell.glsl). This is what every renderer that draws something that grows shares: the programs, the styles on the GPU and how
    /// many layers a growth needs where it's seen. Each renderer draws its own after its geometry (<see cref="GeometryCommandDrivenRenderer{T}.AfterGeometry"/>),
    /// with what it already has bound, in the G-buffer like anything solid, so nothing after the geometry pass knows shells exist.
    /// </summary>
    public sealed class ShellRenderer
    {
        public const int UniformBinding = 13;
        private const int Rows = 7;

        // Texture units the shells read the wind from where their renderer has nothing of its own there: past the snow trail (SnowTrailRenderer.Unit).
        public const int BendUnit = 17;
        public const int WindUnit = 18;
        public const int TouchUnit = 19;

        public ShellSettings Settings { get; } = new ShellSettings();

        /// <summary>
        /// Held off by the game, whatever the settings say: the main menu's picture is settled (MENU.md), and nothing that wasn't in it when it was is drawn there.
        /// </summary>
        public bool Held { get; set; }

        // A program for each kind of thing that grows something, each its own pair of files declaring only its own uniforms (Shells/ShellVertex.glsl).
        public readonly ShaderProgram Instanced = new ShaderProgram("Shells/Shell_Instanced.vert", "Shells/Shell_Instanced.frag");
        public readonly ShaderProgram Character = new ShaderProgram("Shells/Shell_Character.vert", "Shells/Shell_Character.frag");
        public readonly ShaderProgram Wildlife = new ShaderProgram("Shells/Shell_Wildlife.vert", "Shells/Shell_Wildlife.frag");
        public readonly ShaderProgram Terrain = new ShaderProgram("Shells/Shell_Terrain.vert", "Shells/Shell_Terrain.frag");

        private readonly int _styleBuffer;

        public ShellRenderer()
        {
            _styleBuffer = GL.GenBuffer();
            UploadStyles();

            foreach (ShaderProgram program in new[] { Instanced, Character, Wildlife, Terrain })
            {
                program.bind();
                program.loadUniformInt("albedoMapTextureArray", 0);
                Simulation.WeatherRenderer.LinkSurfaceSamplers(program);
                program.unBind();
            }

            // A stone's or a tree's: what InstancedModelRenderer has bound for plants already.
            Instanced.bind();
            Instanced.loadUniformInt("bendMap", 6);
            Instanced.loadUniformInt("windMap", 7);
            Instanced.loadUniformInt("canopyMap", 8);
            Instanced.loadUniformInt("touchMap", 9);
            Instanced.loadUniformInt("plantJoints", 10);
            Instanced.unBind();

            foreach (ShaderProgram program in new[] { Character, Wildlife, Terrain })
            {
                program.bind();
                program.loadUniformInt("bendMap", BendUnit);
                program.loadUniformInt("windMap", WindUnit);
                program.unBind();
            }

            // The ground's: what TerrainRenderer has bound already, and what's walking through plants.
            Terrain.bind();
            Terrain.loadUniformInt("touchMap", TouchUnit);
            Terrain.loadUniformInt("normalHeightTextureArray", 6);
            Terrain.loadUniformInt("surfaceWeightsA", 9);
            Terrain.loadUniformInt("surfaceWeightsB", 10);
            Terrain.loadUniformInt("surfaceWeightsC", 11);
            Terrain.unBind();
        }

        public void CleanUp()
        {
            Instanced.cleanUp();
            Character.cleanUp();
            Wildlife.cleanUp();
            Terrain.cleanUp();
            GL.DeleteBuffer(_styleBuffer);
        }

        // Shells/ShellStyle.glsl's ShellData: change them together.
        private void UploadStyles()
        {
            float[] data = new float[ShellStyles.Most * Rows * 4];
            for (int i = 1; i < ShellStyles.All.Length && i < ShellStyles.Most; i++)
            {
                ShellStyle? s = ShellStyles.All[i];
                if (s == null) continue;
                int at = i * Rows * 4;
                void Row(int row, float x, float y, float z, float w)
                {
                    data[at + row * 4] = x;
                    data[at + row * 4 + 1] = y;
                    data[at + row * 4 + 2] = z;
                    data[at + row * 4 + 3] = w;
                }
                Row(0, s.Length, s.ClumpSize, MathF.Min(s.ClumpWidth, 0.5f), s.ClumpFill);
                Row(1, s.Carpet, s.HeightVariety, s.Shape, MathF.Max(s.Stretch, 1f));
                Row(2, s.Fringe, s.StrandSize, s.Variety, s.Growth == ShellGrowth.Fur ? 1f : 0f);
                Row(3, s.RootShade, s.RootOcclusion, s.Translucency, s.Roughness);
                Row(4, s.TipTint.X, s.TipTint.Y, s.TipTint.Z, s.Porosity);
                Row(5, s.Lean, s.Droop, s.Sway, (float)s.Mask);
                Row(6, s.Crease, s.Lattices, s.Floor, 0f);
            }
            GL.BindBuffer(BufferTarget.UniformBuffer, _styleBuffer);
            GL.BufferData(BufferTarget.UniformBuffer, data.Length * sizeof(float), data, BufferUsageHint.StaticDraw);
            GL.BindBuffer(BufferTarget.UniformBuffer, 0);
            GL.BindBufferBase(BufferRangeTarget.UniformBuffer, UniformBinding, _styleBuffer);
        }

        /// <summary>Whether a model has anything to grow that should be drawn now.</summary>
        public bool Draws(Modelling.Model.glModel model) => Drawing && model.Shell != 0 && model.HasShellData && ShellStyles.Of(model.Shell) != null;

        /// <summary>Whether anything grows this frame.</summary>
        public bool Drawing => Settings.Enabled && !Held;

        /// <summary>
        /// How many layers a growth needs seen from <paramref name="distance"/> metres: as many as its length covers pixels, times <see cref="ShellSettings.LayersPerPixel"/>,
        /// within the style's fewest and most. From the game's camera that's four to eight.
        /// </summary>
        public int LayersAt(ShellStyle style, float distance, RenderEngine engine)
        {
            float pixelsPerMetre = engine.context.projectionMatrix.M22 * Engine.RenderResolution.Y / (2f * MathF.Max(distance, 0.5f));
            int most = Math.Max(style.LeastShells, (int)MathF.Round(style.MostShells * Settings.Most));
            int wanted = (int)MathF.Ceiling(style.Length * (1f - style.Floor) * pixelsPerMetre * Settings.LayersPerPixel);
            return Math.Clamp(wanted, style.LeastShells, most);
        }


        /// <summary>
        /// Readies a program to draw a style's layers: depth tested and written as anything solid is, nothing blended, both faces, as an instance may be mirrored.
        /// The renderer's own textures stay where it bound them.
        /// </summary>
        public void Begin(ShaderProgram program, RenderEngine engine)
        {
            GL.Enable(EnableCap.DepthTest);
            GL.DepthFunc(DepthFunction.Less);
            GL.DepthMask(true);
            GL.ColorMask(true, true, true, true);
            GL.Disable(EnableCap.Blend);
            program.bind();
            program.loadUniformInt("numberOfMaterials", engine.textureGenerator.loadedMaterialTextures);
        }

        /// <summary>One style's layers, about to be drawn.</summary>
        public void Use(ShaderProgram program, byte style, int layers)
        {
            ShellStyle s = ShellStyles.Of(style)!;
            program.loadUniformInt("shellStyle", style);
            program.loadUniformInt("shellCount", layers);
            program.loadUniformVector2f("shellFadeRange", new Vector2(s.FadeFrom, s.FadeTo));
        }

        /// <summary>Binds the wind where the shells of what has nothing of its own there read it.</summary>
        public static void BindWind(RenderEngine engine, bool touch)
        {
            GL.ActiveTexture(TextureUnit.Texture0 + BendUnit);
            GL.BindTexture(TextureTarget.Texture2D, engine._windRenderer.VegetationBendTexture);
            GL.ActiveTexture(TextureUnit.Texture0 + WindUnit);
            GL.BindTexture(TextureTarget.Texture2D, engine._windRenderer.WindMapTexture);
            if (touch)
            {
                GL.ActiveTexture(TextureUnit.Texture0 + TouchUnit);
                GL.BindTexture(TextureTarget.Texture2D, engine._windRenderer.VegetationTouchTexture);
            }
            GL.ActiveTexture(TextureUnit.Texture0);
        }

        /// <summary>Counts what was drawn, for the performance report.</summary>
        public static void Count(byte style, int draws, long instances, long triangles)
        {
            Engine.PerformanceMonitor.Count("Shell draws", draws);
            Engine.PerformanceMonitor.Count("Shell layers", instances);
            Engine.PerformanceMonitor.Count("Shell triangles", triangles);
            if (style < TriangleCounters.Length) Engine.PerformanceMonitor.Count(TriangleCounters[style], triangles);
        }

        // Each style's own, named once, as a name made each frame would be garbage.
        private static readonly string[] TriangleCounters = Array.ConvertAll(ShellStyles.All, s => s == null ? "Shell triangles of nothing" : $"Shell triangles: {s.Name}");
    }
}
