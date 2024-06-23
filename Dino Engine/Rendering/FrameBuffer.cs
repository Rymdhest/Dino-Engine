using Dino_Engine.Core;
using OpenTK.Graphics.OpenGL;
using OpenTK.Mathematics;

namespace Dino_Engine.Rendering
{
    internal class FrameBuffer
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
            } else
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
            int attachment;
            if (depthSettings.isTexture)
            {
                attachment = GL.GenTexture();
                GL.BindTexture(TextureTarget.Texture2D, attachment);
                GL.TexImage2D(TextureTarget.Texture2D, 0, PixelInternalFormat.DepthComponent32, resolution.X, resolution.Y, 0, PixelFormat.DepthComponent, PixelType.Float, IntPtr.Zero);
                GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (float)TextureMagFilter.Linear);
                GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (float)TextureMinFilter.Linear);
                GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapS, (float)TextureWrapMode.ClampToBorder);
                GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapT, (float)TextureWrapMode.ClampToBorder);

                if (depthSettings.isShadowDepthTexture)
                {
                    GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureCompareMode, (float)TextureCompareMode.CompareRefToTexture);
                    GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureCompareFunc, (float)DepthFunction.Lequal);
                } else
                {
                    GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureCompareMode, (float)TextureCompareMode.None);
                }

                
                GL.FramebufferTexture2D(FramebufferTarget.Framebuffer, FramebufferAttachment.DepthAttachment, TextureTarget.Texture2D, attachment, 0);

            } else
            {
                attachment = GL.GenRenderbuffer();
                GL.BindRenderbuffer(RenderbufferTarget.Renderbuffer, attachment);
                GL.RenderbufferStorage(RenderbufferTarget.Renderbuffer, RenderbufferStorage.DepthComponent32, resolution.X, resolution.Y);
                GL.FramebufferRenderbuffer(FramebufferTarget.Framebuffer, FramebufferAttachment.DepthAttachment, RenderbufferTarget.Renderbuffer, attachment);

            }
            return attachment;
        }
        public int getRenderAttachment(int attachmentNumber)
        {
            return renderAttachments[attachmentNumber];
        }
        public void cleanUp()
        {
            if (settings.depthAttachmentSettings != null)
            {
                if (settings.depthAttachmentSettings.isTexture)
                {
                    GL.DeleteTexture(depthAttachment);
                } else
                {
                    GL.DeleteRenderbuffer(depthAttachment);
                }
            }
            foreach (int attachment in renderAttachments)
            {
                GL.DeleteTexture(attachment);
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
