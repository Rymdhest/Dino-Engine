using Dino_Engine.Core;
using Dino_Engine.Modelling;
using Dino_Engine.Modelling.Model;
using Dino_Engine.Physics;
using Dino_Engine.Rendering;
using Dino_Engine.Util;
using OpenTK.Graphics.OpenGL;
using OpenTK.Mathematics;
using static OpenTK.Graphics.OpenGL.GL;

namespace Dino_Engine.Textures
{
    public class TextureStudio
    {

        private FrameBuffer framBuffer;
        private ShaderProgram _textureStudioShader = new ShaderProgram("textureStudioShader.vert", "textureStudioShader.frag");


        public TextureStudio()
        {
           
            FrameBufferSettings gBufferSettings = new FrameBufferSettings(TextureGenerator.TEXTURE_RESOLUTION);
            DrawBufferSettings gAlbedo = new DrawBufferSettings(FramebufferAttachment.ColorAttachment0);
            gAlbedo.formatInternal = PixelInternalFormat.Rgba8;
            gAlbedo.pixelType = PixelType.UnsignedByte;
            gBufferSettings.drawBuffers.Add(gAlbedo);

            DrawBufferSettings gNormal = new DrawBufferSettings(FramebufferAttachment.ColorAttachment1);
            gNormal.formatInternal = PixelInternalFormat.Rgba8;
            gNormal.pixelType = PixelType.UnsignedByte;
            gBufferSettings.drawBuffers.Add(gNormal);

            DrawBufferSettings gMaterials = new DrawBufferSettings(FramebufferAttachment.ColorAttachment2);
            gMaterials.formatInternal = PixelInternalFormat.Rgba8;
            gMaterials.pixelType = PixelType.UnsignedByte;
            gBufferSettings.drawBuffers.Add(gMaterials);

            DepthAttachmentSettings depthSettings = new DepthAttachmentSettings();
            depthSettings.isTexture = true;
            gBufferSettings.depthAttachmentSettings = depthSettings;
            framBuffer = new FrameBuffer(gBufferSettings);


            _textureStudioShader.bind();
            _textureStudioShader.loadUniformInt("albedoMapTextureArray", 0);
            _textureStudioShader.loadUniformInt("normalMapTextureArray", 1);
            _textureStudioShader.loadUniformInt("materialMapTextureArray", 2);

            _textureStudioShader.loadUniformInt("albedoMapModelTextureArray", 3);
            _textureStudioShader.loadUniformInt("normalMapModelTextureArray", 4);
            _textureStudioShader.loadUniformInt("materialMapModelTextureArray", 5);
            _textureStudioShader.unBind();
        }


        public MaterialMapsTextures GenerateTextureFromModel(glModel model, bool fullStretch = true, float rotY = 0f)
        {
            framBuffer.bind();
            _textureStudioShader.bind();
            GL.DepthMask(true);

            GL.ClearBuffer(OpenTK.Graphics.OpenGL.ClearBuffer.Color, 0, new float[] { 1f, 0f, 0f, 0f });  // Albedo - alpha
            GL.ClearBuffer(OpenTK.Graphics.OpenGL.ClearBuffer.Color, 1, new float[] { 0f, 0f, -1f, 0f });  // Normal - AO
            GL.ClearBuffer(OpenTK.Graphics.OpenGL.ClearBuffer.Color, 2, new float[] { 1f, 0f, 0f, 0f });  // Materials
            GL.Clear(ClearBufferMask.DepthBufferBit);

            GL.Enable(EnableCap.DepthTest);
            GL.DepthFunc(DepthFunction.Less);
            GL.Disable(EnableCap.CullFace);
            GL.Disable(EnableCap.Blend);

            GL.BindVertexArray(model.getVAOID());
            GL.EnableVertexAttribArray(0);
            GL.EnableVertexAttribArray(1);
            GL.EnableVertexAttribArray(2);
            GL.EnableVertexAttribArray(3);
            GL.EnableVertexAttribArray(4);
            GL.EnableVertexAttribArray(5);

            AABB box = model.box;
            Vector3 length = box.max - box.min;

            // Local center of the model
            Vector3 center = (box.max + box.min) / 2f;

            // Guard against flat planes (leaves)
            float safeZ = MathF.Max(length.Z, 0.01f);
            float maxXZ = MathF.Max(MathF.Sqrt((length.X * length.X) + (safeZ * safeZ)), 0.01f);

            // 1. Move center to (0,0,0), then rotate
            Matrix4 modelMatrix = Matrix4.CreateTranslation(-center) * Matrix4.CreateRotationY(rotY);

            // 2. Camera looking directly at origin
            Matrix4 viewMatrix = Matrix4.LookAt(
                new Vector3(0f, 0f, maxXZ),
                Vector3.Zero,
                Vector3.UnitY
            );

            // 3. Orthographic Projection
            Matrix4 projectionMatrix;
            if (fullStretch)
            {
                // TIGHT FIT: Fits exact X and Y bounds (e.g. static leaf textures)
                projectionMatrix = Matrix4.CreateOrthographic(length.X, length.Y, 0.0f, maxXZ * 2.0f);
            }
            else
            {
                // IMPOSTERS: Fits spinning XZ diagonal width, but maintains EXACT model Y height
                projectionMatrix = Matrix4.CreateOrthographic(maxXZ, length.Y, 0.0f, maxXZ * 2.0f);
            }

            Matrix4 modelViewMatrix = modelMatrix * viewMatrix;
            _textureStudioShader.loadUniformInt("numberOfMaterials", Engine.RenderEngine.textureGenerator.loadedMaterialTextures);
            _textureStudioShader.loadUniformMatrix4f("modelViewMatrix", modelViewMatrix);
            _textureStudioShader.loadUniformMatrix4f("modelViewProjectionMatrix", modelViewMatrix * projectionMatrix);
            _textureStudioShader.loadUniformMatrix4f("normalModelViewMatrix", Matrix4.Transpose(Matrix4.Invert(modelViewMatrix)));
            _textureStudioShader.loadUniformFloat("maxDepth", length.Z);

            GL.ActiveTexture(TextureUnit.Texture0);
            GL.BindTexture(TextureTarget.Texture2DArray, Engine.RenderEngine.textureGenerator.megaAlbedoTextureArray);
            GL.ActiveTexture(TextureUnit.Texture1);
            GL.BindTexture(TextureTarget.Texture2DArray, Engine.RenderEngine.textureGenerator.megaNormalTextureArray);
            GL.ActiveTexture(TextureUnit.Texture2);
            GL.BindTexture(TextureTarget.Texture2DArray, Engine.RenderEngine.textureGenerator.megaMaterialTextureArray);

            GL.ActiveTexture(TextureUnit.Texture3);
            GL.BindTexture(TextureTarget.Texture2DArray, Engine.RenderEngine.textureGenerator.megaAlbedoModelTextureArray);
            GL.ActiveTexture(TextureUnit.Texture4);
            GL.BindTexture(TextureTarget.Texture2DArray, Engine.RenderEngine.textureGenerator.megaNormalModelTextureArray);
            GL.ActiveTexture(TextureUnit.Texture5);
            GL.BindTexture(TextureTarget.Texture2DArray, Engine.RenderEngine.textureGenerator.megaMaterialModelTextureArray);

            GL.DrawElements(PrimitiveType.Triangles, model.getVertexCount(), DrawElementsType.UnsignedInt, 0);
            _textureStudioShader.unBind();
            framBuffer.unbind();
            GL.BindVertexArray(0);

            int albedo = framBuffer.exportAttachmentAsTexture(ReadBufferMode.ColorAttachment0);
            int normal = framBuffer.exportAttachmentAsTexture(ReadBufferMode.ColorAttachment1);
            int materials = framBuffer.exportAttachmentAsTexture(ReadBufferMode.ColorAttachment2);
            return new MaterialMapsTextures(albedo, normal, materials);
        }
        public MaterialMapsTextures GenerateTextureFromMesh(Mesh mesh, bool fullStretch = true)
        {
            glModel model = glLoader.loadToVAO(mesh);
            var textures = GenerateTextureFromModel(model, fullStretch);
            model.cleanUp();
            return textures;

        }

        public void CleanUp()
        {
            _textureStudioShader.cleanUp();
            framBuffer.cleanUp();
        }
    }
}
