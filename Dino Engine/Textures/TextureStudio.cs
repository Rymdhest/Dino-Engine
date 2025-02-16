using Dino_Engine.Core;
using Dino_Engine.ECS;
using Dino_Engine.Modelling;
using Dino_Engine.Modelling.Model;
using Dino_Engine.Modelling.Procedural.Indoor;
using Dino_Engine.Modelling.Procedural.Urban;
using Dino_Engine.Physics;
using Dino_Engine.Rendering;
using Dino_Engine.Util;
using OpenTK.Graphics.OpenGL;
using OpenTK.Mathematics;

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
            depthSettings.isTexture = false;
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

        public MaterialMapsTextures GenerateTextureFromMesh(Mesh mesh, bool fullStretch = true)
        {

            framBuffer.bind();
            _textureStudioShader.bind();
            GL.ClearColor(0f, 0f, 0f, 0f);
            GL.DepthMask(true);
            GL.Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit);

            GL.Enable(EnableCap.DepthTest);
            GL.DepthFunc(DepthFunction.Less);
            GL.Enable(EnableCap.CullFace);
            GL.CullFace(CullFaceMode.Back);
            GL.Disable(EnableCap.Blend);


            glModel model = glLoader.loadToVAO(mesh);

            GL.BindVertexArray(model.getVAOID());
            GL.EnableVertexAttribArray(0);
            GL.EnableVertexAttribArray(1);
            GL.EnableVertexAttribArray(2);
            GL.EnableVertexAttribArray(3);
            GL.EnableVertexAttribArray(4);
            GL.EnableVertexAttribArray(5);

            AABB box = mesh.createAABB();


            Vector3 length = box._max - box._min;
            Matrix4 projectionMatrix;

            if (fullStretch)
            {
                projectionMatrix = Matrix4.CreateOrthographic(length.X, length.Y, 1f, -length.Z - 1f);
            }
            else
            {
                float max = MathF.Max(length.Y, length.X);
                projectionMatrix = Matrix4.CreateOrthographic(max, max, 1f, -length.Z - 1f);
            }
            Matrix4 viewMatrix = MyMath.createViewMatrix(new Vector3(0f, 0f, 0f), new Vector3(0f, 0f, 0f));
            Matrix4 transformationMatrix = MyMath.createTransformationMatrix(new Transformation(new Vector3(-length.X / 2f - box._min.X, -length.Y / 2f - box._min.Y, -box._min.Z), new Vector3(0f), new Vector3(1f)));
            //transformationMatrix = MyMath.createTransformationMatrix(new Transformation(new Vector3(0, 0, -11), new Vector3(0f), new Vector3(1f)));

            Matrix4 modelViewMatrix = transformationMatrix * viewMatrix;
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
            model.cleanUp();

            int albedo = framBuffer.exportAttachmentAsTexture(ReadBufferMode.ColorAttachment0);
            int normal = framBuffer.exportAttachmentAsTexture(ReadBufferMode.ColorAttachment1);
            int materials = framBuffer.exportAttachmentAsTexture(ReadBufferMode.ColorAttachment2);
            return new MaterialMapsTextures(albedo, normal, materials);
        }

        public void CleanUp()
        {
            _textureStudioShader.cleanUp();
            framBuffer.cleanUp();
        }
    }
}
