using Dino_Engine.Core;
using Dino_Engine.Rendering.Renderers.Lighting;
using OpenTK.Graphics.OpenGL;
using OpenTK.Mathematics;
using System;

namespace Dino_Engine.Rendering
{
    public class FrameBuffer
    {
        private int frameBufferID;
        private int depthAttachment = -1;
        private int[]? renderAttachments;
        private FrameBufferSettings settings;

        public FrameBuffer(FrameBufferSettings settings)
        {
            this.settings = settings;
            createFrameBuffer();
        }

        private void createFrameBuffer()
        {
            frameBufferID = GL.GenFramebuffer();
            GL.BindFramebuffer(FramebufferTarget.Framebuffer, frameBufferID);

            DrawBuffersEnum[] buffers = new DrawBuffersEnum[settings.drawBuffers.Count];
            renderAttachments = new int[settings.drawBuffers.Count];
            for (int i = 0; i < settings.drawBuffers.Count; i++)
            {
                renderAttachments[i] = createRenderAttachment(settings.drawBuffers[i], settings.resolution);
                buffers[i] = (DrawBuffersEnum)settings.drawBuffers[i].colorAttachment;
            }

            if (buffers.Length == 0)
            {
                GL.DrawBuffer(DrawBufferMode.None);
                GL.ReadBuffer(ReadBufferMode.None); // CRITICAL: Required for depth-only framebuffers
            }
            else
            {
                GL.DrawBuffers(buffers.Length, buffers);
            }

            if (settings.depthAttachmentSettings != null)
            {
                depthAttachment = createDepthAttachment(settings.depthAttachmentSettings, settings.resolution);
            }
        }

        public void resolveToScreen()
        {
            GL.BindFramebuffer(FramebufferTarget.DrawFramebuffer, 0);
            GL.BindFramebuffer(FramebufferTarget.ReadFramebuffer, frameBufferID);
            GL.DrawBuffer(DrawBufferMode.Back);
            GL.BlitFramebuffer(0, 0, settings.resolution.X, settings.resolution.Y, 0, 0, Engine.Resolution.X, Engine.Resolution.Y, ClearBufferMask.ColorBufferBit, BlitFramebufferFilter.Linear);
            unbind();
        }

        public void blitDepthBufferFrom(FrameBuffer other)
        {
            GL.BindFramebuffer(FramebufferTarget.ReadFramebuffer, other.frameBufferID);
            GL.BindFramebuffer(FramebufferTarget.DrawFramebuffer, frameBufferID);
            GL.BlitFramebuffer(0, 0, other.settings.resolution.X, other.settings.resolution.Y, 0, 0, this.settings.resolution.X, this.settings.resolution.Y, ClearBufferMask.DepthBufferBit, BlitFramebufferFilter.Nearest);
            unbind();
        }

        public void ClearDepth()
        {
            GL.DepthMask(true);
            bind();
            GL.Clear(ClearBufferMask.DepthBufferBit);
        }

        public void ClearColorDepth()
        {
            bind();
            GL.ClearColor(0f, 0f, 0f, 1f);
            GL.Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit);
        }

        public void bind()
        {
            GL.BindFramebuffer(FramebufferTarget.Framebuffer, frameBufferID);
            GL.Viewport(0, 0, settings.resolution.X, settings.resolution.Y);
        }

        // --- NEW: Added to support Point Light Shadow Passes ---
        public void bindFace(TextureTarget faceTarget)
        {
            GL.BindFramebuffer(FramebufferTarget.Framebuffer, frameBufferID);
            GL.FramebufferTexture2D(FramebufferTarget.Framebuffer, FramebufferAttachment.DepthAttachment, faceTarget, depthAttachment, 0);
            GL.Viewport(0, 0, settings.resolution.X, settings.resolution.Y);

        }

        public void bindRead()
        {
            GL.BindFramebuffer(FramebufferTarget.ReadFramebuffer, frameBufferID);
        }

        public void bindDraw()
        {
            GL.BindFramebuffer(FramebufferTarget.DrawFramebuffer, frameBufferID);
        }

        public int getDepthAttachment()
        {
            return depthAttachment;
        }

        public void unbind()
        {
            GL.BindFramebuffer(FramebufferTarget.Framebuffer, 0);
            Engine.WindowHandler.refreshViewport();
        }

        private int createRenderAttachment(DrawBufferSettings renderSettings, Vector2i resolution)
        {
            int attachment = GL.GenTexture();
            GL.BindTexture(TextureTarget.Texture2D, attachment);
            GL.TexImage2D(TextureTarget.Texture2D, 0, renderSettings.formatInternal, resolution.X, resolution.Y, 0, renderSettings.formatExternal, renderSettings.pixelType, IntPtr.Zero);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (float)renderSettings.magFilterType);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (float)renderSettings.minFilterType);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapS, (float)renderSettings.wrapMode);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapT, (float)renderSettings.wrapMode);
            GL.FramebufferTexture2D(FramebufferTarget.Framebuffer, renderSettings.colorAttachment, TextureTarget.Texture2D, attachment, 0);
            return attachment;
        }

        private int createDepthAttachment(DepthAttachmentSettings depthSettings, Vector2i resolution)
        {
            int attachment = GL.GenTexture();
            GL.BindTexture(depthSettings.target, attachment);

            if (depthSettings.isTexture)
            {
                if (depthSettings.target == TextureTarget.TextureCubeMap)
                {
                    for (int i = 0; i < 6; i++)
                    {
                        GL.TexImage2D(TextureTarget.TextureCubeMapPositiveX + i, 0, depthSettings.precision, resolution.X, resolution.Y, 0, PixelFormat.DepthComponent, PixelType.Float, IntPtr.Zero);
                    }
                    GL.TexParameter(TextureTarget.TextureCubeMap, TextureParameterName.TextureMagFilter, (int)TextureMagFilter.Linear);
                    GL.TexParameter(TextureTarget.TextureCubeMap, TextureParameterName.TextureMinFilter, (int)TextureMinFilter.Linear);
                    GL.TexParameter(TextureTarget.TextureCubeMap, TextureParameterName.TextureWrapS, (int)TextureWrapMode.ClampToEdge);
                    GL.TexParameter(TextureTarget.TextureCubeMap, TextureParameterName.TextureWrapT, (int)TextureWrapMode.ClampToEdge);
                    GL.TexParameter(TextureTarget.TextureCubeMap, TextureParameterName.TextureWrapR, (int)TextureWrapMode.ClampToEdge);
                }
                else
                {
                    GL.TexImage2D(TextureTarget.Texture2D, 0, depthSettings.precision, resolution.X, resolution.Y, 0, PixelFormat.DepthComponent, PixelType.Float, IntPtr.Zero);
                    GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)TextureMagFilter.Linear);
                    GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)TextureMinFilter.Linear);
                    GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapS, (int)TextureWrapMode.ClampToEdge);
                    GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapT, (int)TextureWrapMode.ClampToEdge);
                }

                if (depthSettings.isShadowDepthTexture)
                {
                    GL.TexParameter(depthSettings.target, TextureParameterName.TextureCompareMode, (int)TextureCompareMode.CompareRefToTexture);
                    GL.TexParameter(depthSettings.target, TextureParameterName.TextureCompareFunc, (int)DepthFunction.Lequal);
                }

                if (depthSettings.target == TextureTarget.Texture2D)
                {
                    GL.FramebufferTexture2D(FramebufferTarget.Framebuffer, FramebufferAttachment.DepthAttachment, TextureTarget.Texture2D, attachment, 0);
                }
                else if (depthSettings.target == TextureTarget.TextureCubeMap)
                {
                    GL.FramebufferTexture2D(FramebufferTarget.Framebuffer, FramebufferAttachment.DepthAttachment, TextureTarget.TextureCubeMapPositiveX, attachment, 0);
                }
            }
            else
            {
                attachment = GL.GenRenderbuffer();
                GL.BindRenderbuffer(RenderbufferTarget.Renderbuffer, attachment);
                GL.RenderbufferStorage(RenderbufferTarget.Renderbuffer, RenderbufferStorage.DepthComponent16, resolution.X, resolution.Y);
                GL.FramebufferRenderbuffer(FramebufferTarget.Framebuffer, FramebufferAttachment.DepthAttachment, RenderbufferTarget.Renderbuffer, attachment);
            }
            return attachment;
        }

        public int exportAttachmentAsTexture(ReadBufferMode attachment)
        {
            int texture = GL.GenTexture();
            Engine.CheckGLError("Framebuffer generating");
            GL.BindTexture(TextureTarget.Texture2D, texture);
            Engine.CheckGLError("Framebuffer binding texture");
            GL.TexImage2D(TextureTarget.Texture2D, 0, PixelInternalFormat.Rgba8, settings.resolution.X, settings.resolution.Y, 0, PixelFormat.Rgba, PixelType.UnsignedByte, IntPtr.Zero);
            Engine.CheckGLError("Framebuffer export attachment TexImage2D");
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)TextureMinFilter.Linear);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)TextureMagFilter.Linear);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapS, (int)TextureWrapMode.Repeat);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapT, (int)TextureWrapMode.Repeat);

            GL.BindFramebuffer(FramebufferTarget.ReadFramebuffer, frameBufferID);
            GL.ReadBuffer(attachment);
            Engine.CheckGLError("Framebuffer export attachment ReadBuffer");
            GL.CopyTexImage2D(TextureTarget.Texture2D, 0, InternalFormat.Rgba8, 0, 0, settings.resolution.X, settings.resolution.Y, 0);
            Engine.CheckGLError("Framebuffer export attachment CopyTextureSubImage2D");
            GL.BindFramebuffer(FramebufferTarget.ReadFramebuffer, 0);

            return texture;
        }

        public int GetAttachment(int attachmentNumber)
        {
            return renderAttachments[attachmentNumber];
        }

        public void cleanUp()
        {
            if (settings.depthAttachmentSettings != null && settings.depthAttachmentSettings.isTexture)
            {
                GL.DeleteTexture(depthAttachment);
            }
            else if (settings.depthAttachmentSettings != null)
            {
                GL.DeleteRenderbuffer(depthAttachment);
            }

            if (renderAttachments != null)
            {
                foreach (int attachment in renderAttachments) GL.DeleteTexture(attachment);
            }
            GL.DeleteFramebuffer(frameBufferID);
        }

        public void resize(Vector2i newResolution)
        {
            settings.resolution = newResolution;
            cleanUp();
            createFrameBuffer();
        }

        public Vector2i getResolution()
        {
            return settings.resolution;
        }

        public override string ToString()
        {
            return $"Framebuffer ID: {frameBufferID}";
        }
    }
}