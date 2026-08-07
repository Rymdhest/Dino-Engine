using OpenTK.Mathematics;
using OpenTK.Graphics.OpenGL;
using OpenTK.Windowing.Common;
using Dino_Engine.Modelling.Model;
using System.Runtime.InteropServices;
using Dino_Engine.Rendering.Renderers.Lighting;
using Dino_Engine.Modelling;
using Dino_Engine.Core;

namespace Dino_Engine.Rendering.Renderers.Geometry
{
    // Pack=1 ensures there is no empty padding between variables
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct ImposterInstanceData
    {
        public float modelID;  // 4 bytes
        public Vector3 Position; // 12 bytes
        public Vector3 Scale;      // 12 bytes
        public float RotationY;   // 4 bytes
        public Vector3 BaseLength;      // 12 bytes
    }

    public struct ImposterRenderCommand : IRenderCommand
    {
        // Replaced Matrix4[] with our new optimized struct
        public ImposterInstanceData[] instances;
    }

    public class ImposterRenderer : GeometryCommandDrivenRenderer<ImposterRenderCommand>
    {
        private ShaderProgram _imposterShader = new ShaderProgram("Imposter.vert", "Imposter.frag");
        private ShaderProgram _imposterShadowShader = new ShaderProgram("Imposter_Shadow.vert", "Shadow.frag");
        private int _instanceVBO;
        private glModel imposterModel;

        public ImposterRenderer() : base("Imposter")
        {
            _imposterShader.bind();

            _imposterShader.loadUniformInt("albedoMapModelTextureArray", 0);
            _imposterShader.loadUniformInt("normalMapModelTextureArray", 1);
            _imposterShader.loadUniformInt("materialMapModelTextureArray", 2);
            _imposterShader.unBind();

            _imposterShadowShader.bind();
            _imposterShadowShader.loadUniformInt("albedoMapTextureArray", 0);
            _imposterShadowShader.unBind();

            _instanceVBO = GL.GenBuffer();


            float[] positions = {
                -0.5f, -0.5f, 0,
                0.5f, -0.5f, 0,
                -0.5f, 0.5f, 0,
                0.5f, 0.5f, 0
            };

            Vector3 n = new Vector3(0, 0, 1.0f);

            float[] normals = {
                n.X, n.Y, n.Z,
                n.X, n.Y, n.Z,
                n.X, n.Y, n.Z,
                n.X, n.Y, n.Z,
            };

            int[] indices = {
                0, 1, 2,
                1, 3, 2
            };

            imposterModel = glLoader.loadToVAO(positions, normals, indices);



        }

        public override void CleanUp()
        {
            _imposterShader.cleanUp();
            _imposterShadowShader.cleanUp();
            GL.DeleteBuffer(_instanceVBO);
        }

        internal override void PrepareGeometry(RenderEngine renderEngine)
        {
            GL.Enable(EnableCap.DepthTest);
            GL.Disable(EnableCap.CullFace);
            GL.CullFace(CullFaceMode.Back);
            GL.Disable(EnableCap.Blend);
            _imposterShader.bind();

            _imposterShader.loadUniformInt("sliceCount", Engine.RenderEngine.textureGenerator.anglesPerImposter);

            GL.ActiveTexture(TextureUnit.Texture0);
            GL.BindTexture(TextureTarget.Texture2DArray, renderEngine.textureGenerator.megaAlbedoImposterTextureArray);
            GL.ActiveTexture(TextureUnit.Texture1);
            GL.BindTexture(TextureTarget.Texture2DArray, renderEngine.textureGenerator.megaNormalImposterTextureArray);
            GL.ActiveTexture(TextureUnit.Texture2);
            GL.BindTexture(TextureTarget.Texture2DArray, renderEngine.textureGenerator.megaMaterialImposterTextureArray);
        }

        internal override void FinishGeometry(RenderEngine renderEngine)
        {
            GL.DisableVertexAttribArray(0);
            GL.DisableVertexAttribArray(1);
            GL.DisableVertexAttribArray(2);
            GL.DisableVertexAttribArray(3);
            GL.DisableVertexAttribArray(4);
            GL.DisableVertexAttribArray(5);
            GL.DisableVertexAttribArray(6);
            GL.DisableVertexAttribArray(7);
            GL.DisableVertexAttribArray(8);
            GL.DisableVertexAttribArray(9);
            GL.BindVertexArray(0);

            GL.ActiveTexture(TextureUnit.Texture0);
            GL.BindTexture(TextureTarget.Texture2DArray, renderEngine.textureGenerator.megaAlbedoImposterTextureArray);

            GL.ActiveTexture(TextureUnit.Texture1);
            GL.BindTexture(TextureTarget.Texture2DArray, renderEngine.textureGenerator.megaNormalImposterTextureArray);


            GL.ActiveTexture(TextureUnit.Texture2);
            GL.BindTexture(TextureTarget.Texture2DArray, renderEngine.textureGenerator.megaMaterialImposterTextureArray);
        }

        internal override void PrepareShadow(RenderEngine renderEngine)
        {
            
            GL.DepthMask(true);
            GL.Enable(EnableCap.DepthTest);
            GL.Disable(EnableCap.CullFace);
            GL.Disable(EnableCap.Blend);
            GL.Enable(EnableCap.PolygonOffsetFill);
            GL.CullFace(CullFaceMode.Back);

            _imposterShadowShader.bind();
            _imposterShadowShader.loadUniformInt("numberOfMaterials", renderEngine.textureGenerator.loadedImposterTextures);
            _imposterShadowShader.loadUniformInt("sliceCount", Engine.RenderEngine.textureGenerator.anglesPerImposter);

            GL.ActiveTexture(TextureUnit.Texture0);
            GL.BindTexture(TextureTarget.Texture2DArray, renderEngine.textureGenerator.megaAlbedoImposterTextureArray);
            GL.TexParameter(TextureTarget.Texture2DArray, TextureParameterName.TextureMinFilter, (int)TextureMinFilter.Nearest);
            GL.TexParameter(TextureTarget.Texture2DArray, TextureParameterName.TextureMagFilter, (int)TextureMagFilter.Nearest);

        }

        internal override void FinishShadow(RenderEngine renderEngine)
        {
            

            GL.Disable(EnableCap.PolygonOffsetFill);
            GL.Enable(EnableCap.CullFace);
            GL.CullFace(CullFaceMode.Back);

            GL.DisableVertexAttribArray(0);
            GL.DisableVertexAttribArray(4);
            GL.DisableVertexAttribArray(5);
            GL.DisableVertexAttribArray(6);
            GL.DisableVertexAttribArray(7);
            GL.DisableVertexAttribArray(8);
            GL.DisableVertexAttribArray(9);
            GL.BindVertexArray(0);
            
        }

        internal override void PerformGeometryCommand(ImposterRenderCommand command, RenderEngine renderEngine)
        {
            int instanceCount = command.instances.Length;
            if (instanceCount == 0) return;

            // 1. Upload Data
            int stride = Marshal.SizeOf<ImposterInstanceData>(); // Will be 44 bytes
            int sizeInBytes = instanceCount * stride;

            GL.BindBuffer(BufferTarget.ArrayBuffer, _instanceVBO);
            GL.BufferData(BufferTarget.ArrayBuffer, sizeInBytes, command.instances, BufferUsageHint.DynamicDraw);

            // 2. Bind Mesh VAO
            GL.BindVertexArray(imposterModel.getVAOID());

            // Static Mesh Attributes (Locations 0 and 1)
            GL.EnableVertexAttribArray(0); // Mesh Position
            GL.EnableVertexAttribArray(1); // Mesh UV

            // 3. Setup Instanced Attributes
            GL.BindBuffer(BufferTarget.ArrayBuffer, _instanceVBO);

            // Location 5: Instance model ID (float)
            GL.EnableVertexAttribArray(5);
            GL.VertexAttribPointer(5, 1, VertexAttribPointerType.Float, false, stride, 0);
            GL.VertexAttribDivisor(5, 1);

            // Location 6: Instance Position (vec3)
            GL.EnableVertexAttribArray(6);
            GL.VertexAttribPointer(6, 3, VertexAttribPointerType.Float, false, stride, 4);
            GL.VertexAttribDivisor(6, 1);

            // Location 7: Instance Scale (vec3)
            GL.EnableVertexAttribArray(7);
            GL.VertexAttribPointer(7, 3, VertexAttribPointerType.Float, false, stride, 16);
            GL.VertexAttribDivisor(7, 1);

            // Location 8: Instance Rotation Y (float)
            GL.EnableVertexAttribArray(8);
            GL.VertexAttribPointer(8, 1, VertexAttribPointerType.Float, false, stride, 28);
            GL.VertexAttribDivisor(8, 1);

            // Location 9: Base Length (vec3)
            GL.EnableVertexAttribArray(9);
            GL.VertexAttribPointer(9, 3, VertexAttribPointerType.Float, false, stride, 32);  
            GL.VertexAttribDivisor(9, 1);

            GL.BindBuffer(BufferTarget.ArrayBuffer, 0);

            // 4. Draw
            GL.DrawElementsInstanced(
                PrimitiveType.Triangles,
                imposterModel.getVertexCount(),
                DrawElementsType.UnsignedInt,
                IntPtr.Zero,
                instanceCount);
        }

        internal override void PerformShadowCommand(ImposterRenderCommand command, Shadow shadow, RenderEngine renderEngine)
        {
            if (shadow.isCubeMap && shadow.cubemapFaceIndex >= 0)
            {
                shadow.shadowFrameBuffer.bindFace(TextureTarget.TextureCubeMapPositiveX + shadow.cubemapFaceIndex);
            }
            else
            {
                shadow.shadowFrameBuffer.bind();
            }

            GL.PolygonOffset(shadow.polygonOffsetModel, shadow.polygonOffsetModel * 10.1f);


            _imposterShadowShader.loadUniformMatrix4f("viewpPojectionMatrix", shadow.lightViewMatrix * shadow.shadowProjectionMatrix);
            _imposterShadowShader.loadUniformMatrix4f("lightViewpMatrix", shadow.lightViewMatrix);

            int instanceCount = command.instances.Length;
            if (instanceCount == 0) return;

            // 1. Upload Data
            int stride = Marshal.SizeOf<ImposterInstanceData>(); // Will be 44 bytes
            int sizeInBytes = instanceCount * stride;

            GL.BindBuffer(BufferTarget.ArrayBuffer, _instanceVBO);
            GL.BufferData(BufferTarget.ArrayBuffer, sizeInBytes, command.instances, BufferUsageHint.DynamicDraw);

            // 2. Bind Mesh VAO
            GL.BindVertexArray(imposterModel.getVAOID());

            // Static Mesh Attributes (Locations 0 and 1)
            GL.EnableVertexAttribArray(0); // Mesh Position
            GL.EnableVertexAttribArray(1); // Mesh UV

            // 3. Setup Instanced Attributes
            GL.BindBuffer(BufferTarget.ArrayBuffer, _instanceVBO);

            // Location 5: Instance model ID (float)
            GL.EnableVertexAttribArray(5);
            GL.VertexAttribPointer(5, 1, VertexAttribPointerType.Float, false, stride, 0);
            GL.VertexAttribDivisor(5, 1);

            // Location 6: Instance Position (vec3)
            GL.EnableVertexAttribArray(6);
            GL.VertexAttribPointer(6, 3, VertexAttribPointerType.Float, false, stride, 4);
            GL.VertexAttribDivisor(6, 1);

            // Location 7: Instance Scale (vec3)
            GL.EnableVertexAttribArray(7);
            GL.VertexAttribPointer(7, 3, VertexAttribPointerType.Float, false, stride, 16);
            GL.VertexAttribDivisor(7, 1);

            // Location 8: Instance Rotation Y (float)
            GL.EnableVertexAttribArray(8);
            GL.VertexAttribPointer(8, 1, VertexAttribPointerType.Float, false, stride, 28);
            GL.VertexAttribDivisor(8, 1);

            // Location 9: Base Length (vec3)
            GL.EnableVertexAttribArray(9);
            GL.VertexAttribPointer(9, 3, VertexAttribPointerType.Float, false, stride, 32);
            GL.VertexAttribDivisor(9, 1);

            GL.BindBuffer(BufferTarget.ArrayBuffer, 0);

            // 4. Draw
            GL.DrawElementsInstanced(
                PrimitiveType.Triangles,
                imposterModel.getVertexCount(),
                DrawElementsType.UnsignedInt,
                IntPtr.Zero,
                instanceCount);
        }
    }
}