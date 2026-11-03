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

            if (settings.sharedDepth != null)
            {
                FrameBuffer owner = settings.sharedDepth;
                GL.FramebufferTexture2D(FramebufferTarget.Framebuffer, owner.DepthAttachmentPoint, owner.DepthTextureTarget, owner.depthAttachment, 0);
            }
            else if (settings.depthAttachmentSettings != null)
            {
                depthAttachment = createDepthAttachment(settings.depthAttachmentSettings, settings.resolution);
            }

            FramebufferErrorCode status = GL.CheckFramebufferStatus(FramebufferTarget.Framebuffer);
            IsComplete = status == FramebufferErrorCode.FramebufferComplete;
            if (!IsComplete)
            {
                Console.WriteLine($"Warning: framebuffer {frameBufferID} is incomplete ({status}).");
            }
        }

        /// <summary>Whether the GPU accepted the attachments as a framebuffer it can render to.</summary>
        public bool IsComplete { get; private set; }

        /// <summary>Samples per pixel of the attachments; 0 when they are ordinary textures.</summary>
        public int Samples => settings.samples;

        private FramebufferAttachment DepthAttachmentPoint => settings.depthAttachmentSettings != null && settings.depthAttachmentSettings.stencil
            ? FramebufferAttachment.DepthStencilAttachment
            : FramebufferAttachment.DepthAttachment;

        private TextureTarget DepthTextureTarget => settings.samples > 0
            ? TextureTarget.Texture2DMultisample
            : settings.depthAttachmentSettings?.target ?? TextureTarget.Texture2D;

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
            return settings.sharedDepth != null ? settings.sharedDepth.getDepthAttachment() : depthAttachment;
        }

        public void unbind()
        {
            GL.BindFramebuffer(FramebufferTarget.Framebuffer, 0);
            Engine.WindowHandler.refreshViewport();
        }

        private int createRenderAttachment(DrawBufferSettings renderSettings, Vector2i resolution)
        {
            int attachment = GL.GenTexture();
            if (settings.samples > 0)
            {
                // Fixed sample locations: every attachment of a framebuffer must agree, and the shaders read the positions once.
                GL.BindTexture(TextureTarget.Texture2DMultisample, attachment);
                GL.TexImage2DMultisample(TextureTargetMultisample.Texture2DMultisample, settings.samples, renderSettings.formatInternal, resolution.X, resolution.Y, true);
                GL.FramebufferTexture2D(FramebufferTarget.Framebuffer, renderSettings.colorAttachment, TextureTarget.Texture2DMultisample, attachment, 0);
                GL.BindTexture(TextureTarget.Texture2DMultisample, 0);
                return attachment;
            }
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
            PixelInternalFormat depthFormat = depthSettings.stencil ? PixelInternalFormat.Depth32fStencil8 : depthSettings.precision;
            if (settings.samples > 0)
            {
                GL.BindTexture(TextureTarget.Texture2DMultisample, attachment);
                GL.TexImage2DMultisample(TextureTargetMultisample.Texture2DMultisample, settings.samples, depthFormat, resolution.X, resolution.Y, true);
                GL.FramebufferTexture2D(FramebufferTarget.Framebuffer, DepthAttachmentPoint, TextureTarget.Texture2DMultisample, attachment, 0);
                GL.BindTexture(TextureTarget.Texture2DMultisample, 0);
                return attachment;
            }
            if (depthSettings.stencil)
            {
                GL.BindTexture(TextureTarget.Texture2D, attachment);
                GL.TexImage2D(TextureTarget.Texture2D, 0, depthFormat, resolution.X, resolution.Y, 0, PixelFormat.DepthStencil, PixelType.Float32UnsignedInt248Rev, IntPtr.Zero);
                GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)TextureMagFilter.Nearest);
                GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)TextureMinFilter.Nearest);
                GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapS, (int)TextureWrapMode.ClampToEdge);
                GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapT, (int)TextureWrapMode.ClampToEdge);
                GL.FramebufferTexture2D(FramebufferTarget.Framebuffer, DepthAttachmentPoint, TextureTarget.Texture2D, attachment, 0);
                return attachment;
            }
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
            // A shared depth attachment belongs to the framebuffer it came from.
            DepthAttachmentSettings? ownDepth = settings.sharedDepth == null ? settings.depthAttachmentSettings : null;
            if (ownDepth != null && (ownDepth.isTexture || ownDepth.stencil || settings.samples > 0))
            {
                GL.DeleteTexture(depthAttachment);
            }
            else if (ownDepth != null)
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