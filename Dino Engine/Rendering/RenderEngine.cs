using Dino_Engine.Core;
using Dino_Engine.Debug;
using Dino_Engine.ECS;
using Dino_Engine.Rendering.Renderers;
using Dino_Engine.Rendering.Renderers.Geometry;
using Dino_Engine.Rendering.Renderers.Lighting;
using Dino_Engine.Rendering.Renderers.PosGeometry;
using Dino_Engine.Rendering.Renderers.PostProcessing;
using OpenTK.Graphics.OpenGL;
using OpenTK.Windowing.Common;
using OpenTK.Windowing.GraphicsLibraryFramework;
using System;
using System.Diagnostics.Tracing;

namespace Dino_Engine.Rendering
{
    public class RenderEngine
    {
        private List<Renderer> _renderers = new List<Renderer>();
        
        private FrameBuffer _gBuffer;

        private ScreenQuadRenderer _screenQuadRenderer;
        private GeogemetryRenderer _flatGeogemetryRenderer;
        private DirectionalLightRenderer _directionalLightRenderer;
        private PointLightRenderer _pointLightRenderer;
        private ShadowCascadeMapRenderer _shadowCascadeMapRenderer;
        private ToneMapRenderer _toneMapRenderer;
        private FXAARenderer _fXAARenderer;
        private SSAORenderer _sSAORenderer;
        private BloomRenderer _bloomRenderer;
        private SkyRenderer _skyRenderer;
        private FogRenderer _fogRenderer;
        private ScreenSpaceReflectionRenderer _screenSpaceReflectionRenderer;
        private GaussianBlurRenderer _gaussianBlurRenderer;
        private SpotLightRenderer _spotLightRenderer;
        private ParticleRenderer _particleRenderer;
        private DepthOfFieldRenderer _depthOfFieldRenderer;
        public static DebugRenderer _debugRenderer = new DebugRenderer();
        public bool debugView = false;
        private ShaderProgram _simpleShader;


        public GaussianBlurRenderer GaussianBlurRenderer { get => _gaussianBlurRenderer;  }
        public FrameBuffer GBuffer { get => _gBuffer; }
        public ScreenQuadRenderer ScreenQuadRenderer { get => _screenQuadRenderer; }

        public RenderEngine()
        {
            InitGBuffer();

            _simpleShader = new ShaderProgram("Simple.vert", "Simple.frag");

            _simpleShader.bind();
            _simpleShader.loadUniformInt("blitTexture", 0);
            _simpleShader.unBind();
        }

        public void InitRenderers()
        {
            _screenQuadRenderer = new ScreenQuadRenderer();
            _flatGeogemetryRenderer = new GeogemetryRenderer();
            _directionalLightRenderer = new DirectionalLightRenderer();
            _pointLightRenderer = new PointLightRenderer();
            _shadowCascadeMapRenderer = new ShadowCascadeMapRenderer();
            _toneMapRenderer = new ToneMapRenderer();
            _fXAARenderer = new FXAARenderer();
            _sSAORenderer = new SSAORenderer();
            _bloomRenderer = new BloomRenderer();
            _skyRenderer = new SkyRenderer();
            _fogRenderer = new FogRenderer();
            _screenSpaceReflectionRenderer = new ScreenSpaceReflectionRenderer();
            _gaussianBlurRenderer = new GaussianBlurRenderer();
            _spotLightRenderer = new SpotLightRenderer();
            _particleRenderer = new ParticleRenderer();
            _depthOfFieldRenderer = new DepthOfFieldRenderer();
        }

        private void InitGBuffer()
        {
            FrameBufferSettings gBufferSettings = new FrameBufferSettings(Engine.Resolution);
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

            if (Engine.WindowHandler.IsKeyPressed(Keys.F8))
            {
                if (debugView) debugView = false;
                else debugView = true;
            }
        }
        public void Render(ECSEngine eCSEngine)
        {

            PrepareFrame();

            if (debugView)
            {
                _debugRenderer.render(_screenQuadRenderer);
            } else
            {

                GeometryPass(eCSEngine);
                LightPass(eCSEngine);
                PostGeometryPass(eCSEngine);
                PostProcessPass(eCSEngine);

                _simpleShader.bind();

                _screenQuadRenderer.GetLastFrameBuffer().resolveToScreen();

                //_screenQuadRenderer.RenderTextureToScreen(_screenQuadRenderer.GetLastOutputTexture());

                //_screenQuadRenderer.RenderTextureToScreen(_gBuffer.GetAttachment(1));
            }


            FinishFrame();
        }

        private void LightPass(ECSEngine eCSEngine)
        {
            _sSAORenderer.RenderPass(eCSEngine, this);
            _shadowCascadeMapRenderer.RenderPass(eCSEngine, this);

            ScreenQuadRenderer.GetLastFrameBuffer().bind();
            GL.Clear(ClearBufferMask.ColorBufferBit);

            _directionalLightRenderer.RenderPass(eCSEngine, this);
            _pointLightRenderer.RenderPass(eCSEngine, this);
            _spotLightRenderer.RenderPass(eCSEngine, this);
        }
        private void PostGeometryPass(ECSEngine eCSEngine)
        {
            _screenSpaceReflectionRenderer.RenderPass(eCSEngine, this);
            _skyRenderer.RenderPass(eCSEngine, this);
            _particleRenderer.RenderPass(eCSEngine, this);
        }
        private void GeometryPass(ECSEngine eCSEngine)
        {
            _flatGeogemetryRenderer.RenderPass(eCSEngine, this);

            _screenQuadRenderer.GetNextFrameBuffer().blitDepthBufferFrom(_gBuffer);
            _screenQuadRenderer.GetLastFrameBuffer().blitDepthBufferFrom(_gBuffer);
        }

        private void PostProcessPass(ECSEngine eCSEngine)
        {
            _bloomRenderer.RenderPass(eCSEngine, this);
            _fogRenderer.RenderPass(eCSEngine, this);
            _toneMapRenderer.RenderPass(eCSEngine, this);
            //_depthOfFieldRenderer.RenderPass(eCSEngine, this);
            _fXAARenderer.RenderPass(eCSEngine, this);
        }

        private void PrepareFrame()
        {
            _screenQuadRenderer.clearBothBuffers();
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

        public void CleanUp()
        {
            foreach (Renderer renderer in _renderers)
            {
                renderer.CleanUp();
            }
            _gBuffer.cleanUp();
            _simpleShader.cleanUp();
        }

        public List<Renderer> Renderers { get => _renderers; }
    }
}
