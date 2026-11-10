using Dino_Engine.Core;
using Dino_Engine.Modelling.Model;
using OpenTK.Graphics.OpenGL;
using OpenTK.Mathematics;

namespace Dino_Engine.Rendering.Renderers.Geometry
{
    public struct WayRenderCommand
    {
        public glModel Mesh;
        public Matrix4 Model;
        public Vector4 Shape;       // The opening: half width, sill, springing, radius of its arcs (WaygateDesign).
        public Vector4 Heart;       // xy where its throat is centred, z the depth of its throat in metres, w 1 for the Haven's.
        public Vector3 Eye;         // The camera, in the gate's frame.
        public Vector3 Axis;        // The way its throat goes, in the gate's frame, into the face the camera sees.
        public float Age;           // Seconds since it opened.
        public float Strength;      // 1 when it's fully there.
        public float Breath;        // How bright its breathing makes it now, 1 on average (WayLook.Breathing), which its lights keep to.
        public Vector4[] Look;      // WayLook, as Way/Way.frag reads it.
    }

    /// <summary>
    /// Draws open ways (WAYGATES.md) in two passes. In the geometry pass each is an opaque surface that takes no light at all: black, wholly occluded, never in the sun, and depth for everything
    /// after it, so fog, depth of field and particles all know it's there and it hides what's behind it from either side. Its light comes after the light passes have added up, as radiance
    /// straight into the multisampled scene on the same depth, before the reflections, so wet ground shows it, and in floats, as the G-buffer's eight bits of emission band anything so smooth
    /// and so bright. Both passes run the same vertex shader, invariant, so the second lands on exactly the first's depth.
    /// </summary>
    public class WayRenderer : BaseRenderer
    {
        public const int LookLength = 12;

        private readonly ShaderProgram _geometry = new ShaderProgram("Way/Way.vert", "Way/Way_Geometry.frag");
        private readonly ShaderProgram _light = new ShaderProgram("Way/Way.vert", "Way/Way.frag");
        private readonly List<WayRenderCommand> _commands = new();

        public WayRenderer() : base("Ways")
        {
        }

        public int Count => _commands.Count;

        public void Clear() => _commands.Clear();

        public void Submit(in WayRenderCommand command) => _commands.Add(command);

        /// <summary>Into the multisampled G-buffer, which the geometry pass has bound.</summary>
        public void GeometryRenderPass(RenderEngine renderEngine)
        {
            if (_commands.Count == 0) return;
            Engine.PerformanceMonitor.startGPUTask("Ways Geometry");
            GL.Enable(EnableCap.DepthTest);
            GL.DepthFunc(DepthFunction.Less);
            GL.DepthMask(true);
            GL.Disable(EnableCap.Blend);
            GL.Disable(EnableCap.CullFace);
            _geometry.bind();
            foreach (WayRenderCommand command in _commands) Draw(_geometry, command, light: false);
            Finish();
            Engine.PerformanceMonitor.finishGPUTask("Ways Geometry");
        }

        /// <summary>Their light, added into the multisampled scene after the lights and before the reflections.</summary>
        public void LightPass(RenderEngine renderEngine)
        {
            if (_commands.Count == 0) return;
            Engine.PerformanceMonitor.startGPUTask("Ways Light");
            renderEngine.Msaa.SceneBuffer.bind();
            GL.Enable(EnableCap.DepthTest);
            GL.DepthFunc(DepthFunction.Lequal);
            GL.DepthMask(false);
            GL.Disable(EnableCap.StencilTest);
            GL.Enable(EnableCap.Blend);
            GL.BlendFunc(BlendingFactor.One, BlendingFactor.One);
            GL.BlendEquation(BlendEquationMode.FuncAdd);
            GL.Disable(EnableCap.CullFace);
            _light.bind();
            var lighting = renderEngine.Lighting;
            _light.loadUniformFloat("emissionScale", lighting.Emission ? lighting.EmissionScale : 0f);
            foreach (WayRenderCommand command in _commands) Draw(_light, command, light: true);
            Finish();
            GL.DepthFunc(DepthFunction.Less);
            GL.Disable(EnableCap.Blend);
            Engine.PerformanceMonitor.finishGPUTask("Ways Light");
        }

        // The geometry pass only needs to know where the way is and how far it's opened; its light needs everything.
        private static void Draw(ShaderProgram shader, in WayRenderCommand command, bool light)
        {
            shader.loadUniformMatrix4f("modelMatrix", command.Model);
            shader.loadUniformVector4f("wayShape", command.Shape);
            shader.loadUniformVector4f("wayHeart", command.Heart);
            shader.loadUniformVector4f("wayState", new Vector4(command.Age, command.Strength, command.Heart.W, command.Breath));
            if (light)
            {
                shader.loadUniformVector3f("wayEye", command.Eye);
                shader.loadUniformVector3f("wayAxis", command.Axis);
                shader.loadUniformVector4fArray("wayLook", command.Look);
            }

            GL.BindVertexArray(command.Mesh.getVAOID());
            GL.EnableVertexAttribArray(0);
            GL.EnableVertexAttribArray(1);
            GL.EnableVertexAttribArray(2);
            GL.EnableVertexAttribArray(4);
            GL.DrawElements(PrimitiveType.Triangles, command.Mesh.getVertexCount(), DrawElementsType.UnsignedInt, 0);
        }

        private static void Finish()
        {
            GL.DisableVertexAttribArray(0);
            GL.DisableVertexAttribArray(1);
            GL.DisableVertexAttribArray(2);
            GL.DisableVertexAttribArray(4);
            GL.BindVertexArray(0);
            GL.Enable(EnableCap.CullFace);
            GL.CullFace(CullFaceMode.Back);
        }

        public override void CleanUp()
        {
            _geometry.cleanUp();
            _light.cleanUp();
        }
    }
}
