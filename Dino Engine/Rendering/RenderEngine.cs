﻿using Dino_Engine.Core;
using Dino_Engine.ECS;
using Dino_Engine.Rendering.Renderers;
using OpenTK.Graphics.OpenGL;
using OpenTK.Windowing.Common;
using System;

namespace Dino_Engine.Rendering
{
    public class RenderEngine
    {
        private List<Renderer> _renderers = new List<Renderer>();
        
        private FrameBuffer _gBuffer;

        private ScreenQuadRenderer _screenQuadRenderer;
        private FlatGeogemetryRenderer _flatGeogemetryRenderer;

        private ShaderProgram _simpleShader;
        public RenderEngine()
        {
            InitGBuffer();

            _simpleShader = new ShaderProgram("Simple_Vertex", "Simple_Fragment");

            _simpleShader.bind();
            _simpleShader.loadUniformInt("blitTexture", 0);
            _simpleShader.unBind();
        }


        public void InitRenderers()
        {
            _screenQuadRenderer = new ScreenQuadRenderer();
            _flatGeogemetryRenderer = new FlatGeogemetryRenderer();
        }

        private void InitGBuffer()
        {
            FrameBufferSettings gBufferSettings = new FrameBufferSettings(Engine.WindowHandler.Size);
            DrawBufferSettings gAlbedo = new DrawBufferSettings(FramebufferAttachment.ColorAttachment0);
            gAlbedo.formatInternal = PixelInternalFormat.Rgba16f;
            gAlbedo.pixelType = PixelType.Float;
            gBufferSettings.drawBuffers.Add(gAlbedo);

            DrawBufferSettings gNormal = new DrawBufferSettings(FramebufferAttachment.ColorAttachment1);
            gNormal.formatInternal = PixelInternalFormat.Rgba16f;
            gNormal.pixelType = PixelType.Float;
            gBufferSettings.drawBuffers.Add(gNormal);

            DrawBufferSettings gPosition = new DrawBufferSettings(FramebufferAttachment.ColorAttachment2);
            gPosition.formatInternal = PixelInternalFormat.Rgba16f;
            gPosition.pixelType = PixelType.Float;
            gBufferSettings.drawBuffers.Add(gPosition);

            DrawBufferSettings gMaterials = new DrawBufferSettings(FramebufferAttachment.ColorAttachment3);
            gMaterials.formatInternal = PixelInternalFormat.Rgba16f;
            gMaterials.pixelType = PixelType.UnsignedByte;
            gBufferSettings.drawBuffers.Add(gMaterials);

            DepthAttachmentSettings depthSettings = new DepthAttachmentSettings();
            depthSettings.isTexture = true;
            gBufferSettings.depthAttachmentSettings = depthSettings;
            _gBuffer = new FrameBuffer(gBufferSettings);
        }

        public void Update()
        {
            foreach (Renderer renderer in _renderers)
            {
                renderer.Update();
            }
        }
        public void Render(ECSEngine eCSEngine)
        {
            PrepareFrame();

            GeometryPass(eCSEngine);
            LightingPass();
            PostProcessPass();

            _simpleShader.bind();
            _screenQuadRenderer.RenderTextureToScreen(_gBuffer.getRenderAttachment(0));

            FinishFrame();
        }
        private void GeometryPass(ECSEngine eCSEngine)
        {
            _gBuffer.bind();
            _flatGeogemetryRenderer.render(eCSEngine.getSystem<FlatModelSystem>(), eCSEngine.Camera);
        }
        private void LightingPass()
        {

        }

        private void PostProcessPass()
        {

        }

        private void PrepareFrame()
        {

        }

        private void FinishFrame()
        {

            Engine.WindowHandler.SwapBuffers();
        }


        public void OnResize(ResizeEventArgs eventArgs)
        {
            _gBuffer.resize(eventArgs.Size);
            foreach(Renderer renderer in _renderers)
            {
                renderer.OnResize(eventArgs);
            }
        }

        public List<Renderer> Renderers { get => _renderers; }
    }
}
