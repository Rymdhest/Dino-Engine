using Dino_Engine.Core;
using Dino_Engine.Modelling;
using Dino_Engine.Modelling.Model;
using Dino_Engine.Physics;
using Dino_Engine.Rendering;
using Dino_Engine.Util;
using OpenTK.Graphics.OpenGL;
using OpenTK.Mathematics;
using System;
using System.Collections.Generic;
using static OpenTK.Graphics.OpenGL.GL;

namespace Dino_Engine.Textures
{
    public class TextureStudio
    {
        private DualBuffer _studioFrameBuffer;
        private ShaderProgram _textureStudioShader = new ShaderProgram("textureStudioShader.vert", "textureStudioShader.frag");
        private ShaderProgram _texturePaddingShader = new ShaderProgram("Simple.vert", "texturePaddingShader.frag");

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
            _studioFrameBuffer = new DualBuffer(gBufferSettings);

            _textureStudioShader.bind();
            _textureStudioShader.loadUniformInt("albedoMapTextureArray", 0);
            _textureStudioShader.loadUniformInt("normalMapTextureArray", 1);
            _textureStudioShader.loadUniformInt("materialMapTextureArray", 2);
            _textureStudioShader.loadUniformInt("albedoMapModelTextureArray", 3);
            _textureStudioShader.loadUniformInt("normalMapModelTextureArray", 4);
            _textureStudioShader.loadUniformInt("materialMapModelTextureArray", 5);
            _textureStudioShader.unBind();

            _texturePaddingShader.bind();
            _texturePaddingShader.loadUniformInt("AlbedoIn", 0);
            _texturePaddingShader.loadUniformInt("NormalIn", 1);
            _texturePaddingShader.loadUniformInt("MaterialIn", 2);
            _texturePaddingShader.unBind();
        }

        public MaterialMapsTextures GenerateTextureFromModel(glModel model, bool fullStretch = true, float rotY = 0f)
        {
            // ==========================================
            // MATRIX & BOUNDS CALCULATIONS
            // ==========================================
            AABB box = model.box;
            Vector3 length = box.max - box.min;
            Vector3 center = (box.max + box.min) / 2f; 

            float safeZ = MathF.Max(length.Z, 0.01f);
            float maxXZ = MathF.Max(MathF.Sqrt((length.X * length.X) + (safeZ * safeZ)), 0.01f);

            Matrix4 modelMatrix = Matrix4.CreateTranslation(-center) * Matrix4.CreateRotationY(rotY);
            Matrix4 viewMatrix = Matrix4.LookAt(new Vector3(0f, 0f, maxXZ), Vector3.Zero, Vector3.UnitY);

            Matrix4 projectionMatrix = fullStretch
                //? Matrix4.CreateOrthographic(length.X, length.Y, 0.0f, maxXZ * 2.0f)
                ? Matrix4.CreateOrthographic(maxXZ, length.Y, 0.0f, maxXZ * 2.0f)
                //: Matrix4.CreateOrthographic(maxXZ, length.Y, 0.0f, maxXZ * 2.0f);
                : Matrix4.CreateOrthographic(MathF.Max(length.X, length.Y), MathF.Max(length.X, length.Y), 0.0f, maxXZ * 2.0f);

            Matrix4 modelViewMatrix = modelMatrix * viewMatrix;
            Matrix4 modelViewProjectionMatrix = modelViewMatrix * projectionMatrix;
            Matrix4 normalModelViewMatrix = Matrix4.Transpose(Matrix4.Invert(modelViewMatrix));

            // ==========================================
            // PASS 0: OVERDRAW / THICKNESS MAP
            // ==========================================
            _studioFrameBuffer.GetNextFrameBuffer().bind();
            _textureStudioShader.bind();

            GL.DepthMask(false); // Disable depth writes so all geometry processes
            GL.Disable(EnableCap.DepthTest); // Disable depth test so back-faces/occluded geometry count
            GL.Disable(EnableCap.CullFace);

            GL.ClearBuffer(OpenTK.Graphics.OpenGL.ClearBuffer.Color, 0, new float[] { 0f, 0f, 0f, 0f });
            GL.ClearBuffer(OpenTK.Graphics.OpenGL.ClearBuffer.Color, 1, new float[] { 0f, 0f, 0f, 0f });
            GL.ClearBuffer(OpenTK.Graphics.OpenGL.ClearBuffer.Color, 2, new float[] { 0f, 0f, 0f, 0f });
            GL.Clear(ClearBufferMask.DepthBufferBit);

            // Enable Additive Blending to accumulate overdraw counts in the red channel
            GL.Enable(EnableCap.Blend);
            GL.BlendEquation(BlendEquationMode.FuncAdd);
            GL.BlendFunc(BlendingFactor.One, BlendingFactor.One);

            GL.BindVertexArray(model.getVAOID());
            for (int i = 0; i <= 5; i++) GL.EnableVertexAttribArray(i);

            _textureStudioShader.loadUniformInt("isOverdrawPass", 1);
            _textureStudioShader.loadUniformMatrix4f("modelViewMatrix", modelViewMatrix);
            _textureStudioShader.loadUniformMatrix4f("modelViewProjectionMatrix", modelViewProjectionMatrix);

            // Bind mega textures (Units 0 - 5)
            GL.ActiveTexture(TextureUnit.Texture0); GL.BindTexture(TextureTarget.Texture2DArray, Engine.RenderEngine.textureGenerator.megaAlbedoTextureArray);
            GL.ActiveTexture(TextureUnit.Texture1); GL.BindTexture(TextureTarget.Texture2DArray, Engine.RenderEngine.textureGenerator.megaNormalTextureArray);
            GL.ActiveTexture(TextureUnit.Texture2); GL.BindTexture(TextureTarget.Texture2DArray, Engine.RenderEngine.textureGenerator.megaMaterialTextureArray);
            GL.ActiveTexture(TextureUnit.Texture3); GL.BindTexture(TextureTarget.Texture2DArray, Engine.RenderEngine.textureGenerator.megaAlbedoModelTextureArray);
            GL.ActiveTexture(TextureUnit.Texture4); GL.BindTexture(TextureTarget.Texture2DArray, Engine.RenderEngine.textureGenerator.megaNormalModelTextureArray);
            GL.ActiveTexture(TextureUnit.Texture5); GL.BindTexture(TextureTarget.Texture2DArray, Engine.RenderEngine.textureGenerator.megaMaterialModelTextureArray);

            GL.DrawElements(PrimitiveType.Triangles, model.getVertexCount(), DrawElementsType.UnsignedInt, 0);

            // Swap buffers so Pass 0 result is now in GetLastFrameBuffer()
            _studioFrameBuffer.StepToggle();

            // ==========================================
            // PASS 1: RENDER MODEL TO FBO
            // ==========================================
            _studioFrameBuffer.GetNextFrameBuffer().bind();
            _textureStudioShader.bind();

            GL.DepthMask(true);
            GL.Enable(EnableCap.DepthTest);
            GL.DepthFunc(DepthFunction.Less);
            GL.Disable(EnableCap.CullFace);
            GL.Disable(EnableCap.Blend); // Turn off additive blending for regular render

            GL.ClearBuffer(OpenTK.Graphics.OpenGL.ClearBuffer.Color, 0, new float[] { 0f, 0f, 0f, 0f });
            GL.ClearBuffer(OpenTK.Graphics.OpenGL.ClearBuffer.Color, 1, new float[] { 0f, 0f, 0f, 0f });
            GL.ClearBuffer(OpenTK.Graphics.OpenGL.ClearBuffer.Color, 2, new float[] { 0f, 0f, 0f, 0f });
            GL.Clear(ClearBufferMask.DepthBufferBit);

            // Pass configuration for SSS Occlusion lookup
            _textureStudioShader.loadUniformInt("isOverdrawPass", 0);
            _textureStudioShader.loadUniformVector2f("resolution", TextureGenerator.TEXTURE_RESOLUTION);

            // Bind Pass 0 output (Overdraw result) to Texture Unit 6
            GL.ActiveTexture(TextureUnit.Texture6);
            GL.BindTexture(TextureTarget.Texture2D, _studioFrameBuffer.GetLastFrameBuffer().GetAttachment(0));

            _textureStudioShader.loadUniformInt("overdrawTexture", 6);

            _textureStudioShader.loadUniformInt("numberOfMaterials", Engine.RenderEngine.textureGenerator.loadedMaterialTextures);
            _textureStudioShader.loadUniformMatrix4f("modelViewMatrix", modelViewMatrix);
            _textureStudioShader.loadUniformMatrix4f("modelViewProjectionMatrix", modelViewProjectionMatrix);
            _textureStudioShader.loadUniformMatrix4f("normalModelViewMatrix", normalModelViewMatrix);
            _textureStudioShader.loadUniformFloat("maxDepth", length.Z);

            // Re-bind mega textures 0 - 5
            GL.ActiveTexture(TextureUnit.Texture0); GL.BindTexture(TextureTarget.Texture2DArray, Engine.RenderEngine.textureGenerator.megaAlbedoTextureArray);
            GL.ActiveTexture(TextureUnit.Texture1); GL.BindTexture(TextureTarget.Texture2DArray, Engine.RenderEngine.textureGenerator.megaNormalTextureArray);
            GL.ActiveTexture(TextureUnit.Texture2); GL.BindTexture(TextureTarget.Texture2DArray, Engine.RenderEngine.textureGenerator.megaMaterialTextureArray);
            GL.ActiveTexture(TextureUnit.Texture3); GL.BindTexture(TextureTarget.Texture2DArray, Engine.RenderEngine.textureGenerator.megaAlbedoModelTextureArray);
            GL.ActiveTexture(TextureUnit.Texture4); GL.BindTexture(TextureTarget.Texture2DArray, Engine.RenderEngine.textureGenerator.megaNormalModelTextureArray);
            GL.ActiveTexture(TextureUnit.Texture5); GL.BindTexture(TextureTarget.Texture2DArray, Engine.RenderEngine.textureGenerator.megaMaterialModelTextureArray);

            GL.DrawElements(PrimitiveType.Triangles, model.getVertexCount(), DrawElementsType.UnsignedInt, 0);

            // Unbind Texture Unit 6
            GL.ActiveTexture(TextureUnit.Texture6);
            GL.BindTexture(TextureTarget.Texture2D, 0);

            // Swap buffers so Pass 1 result is in GetLastFrameBuffer()
            _studioFrameBuffer.StepToggle();

            // ==========================================
            // PASS 2: FULL-CANVAS BOUNDARY DILATION
            // ==========================================
            _texturePaddingShader.bind();

            var previousLastBuffer = Engine.RenderEngine.lastUsedBuffer;
            GL.Disable(EnableCap.DepthTest);
            GL.DepthMask(false);

            // 1.5x Resolution guarantees padding reaches absolute corners seamlessly
            int totalPasses = (int)(TextureGenerator.TEXTURE_RESOLUTION.X * 1.5f);

            for (int i = 0; i < totalPasses; i++)
            {
                GL.ActiveTexture(TextureUnit.Texture0);
                GL.BindTexture(TextureTarget.Texture2D, _studioFrameBuffer.GetLastFrameBuffer().GetAttachment(0));
                GL.ActiveTexture(TextureUnit.Texture1);
                GL.BindTexture(TextureTarget.Texture2D, _studioFrameBuffer.GetLastFrameBuffer().GetAttachment(1));
                GL.ActiveTexture(TextureUnit.Texture2);
                GL.BindTexture(TextureTarget.Texture2D, _studioFrameBuffer.GetLastFrameBuffer().GetAttachment(2));

                _studioFrameBuffer.RenderToNextFrameBuffer();
            }

            // Restore engine state and clean up bindings
            Engine.RenderEngine.lastUsedBuffer = previousLastBuffer;
            GL.DepthMask(true);
            _texturePaddingShader.unBind();
            GL.BindVertexArray(0);

            int albedo = _studioFrameBuffer.GetLastFrameBuffer().exportAttachmentAsTexture(ReadBufferMode.ColorAttachment0);
            int normal = _studioFrameBuffer.GetLastFrameBuffer().exportAttachmentAsTexture(ReadBufferMode.ColorAttachment1);
            int materials = _studioFrameBuffer.GetLastFrameBuffer().exportAttachmentAsTexture(ReadBufferMode.ColorAttachment2);

            GL.ActiveTexture(TextureUnit.Texture2); GL.BindTexture(TextureTarget.Texture2D, 0);
            GL.ActiveTexture(TextureUnit.Texture1); GL.BindTexture(TextureTarget.Texture2D, 0);
            GL.ActiveTexture(TextureUnit.Texture0); GL.BindTexture(TextureTarget.Texture2D, 0);

            _studioFrameBuffer.UnBind();

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
            _studioFrameBuffer.CleanUp();
            _texturePaddingShader.cleanUp();
        }
    }
}