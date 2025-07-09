using Dino_Engine.Core;
using Dino_Engine.Debug;
using Dino_Engine.ECS;
using Dino_Engine.ECS.ECS_Architecture;
using Dino_Engine.Rendering.Renderers;
using Dino_Engine.Rendering.Renderers.Geometry;
using Dino_Engine.Rendering.Renderers.Lighting;
using Dino_Engine.Rendering.Renderers.PosGeometry;
using Dino_Engine.Rendering.Renderers.PostProcessing;
using Dino_Engine.Textures;
using OpenTK.Graphics.OpenGL;
using OpenTK.Mathematics;
using OpenTK.Windowing.Common;
using OpenTK.Windowing.GraphicsLibraryFramework;
using System;
using System.Diagnostics.Tracing;

namespace Dino_Engine.Rendering
{
    public struct RenderContext
    {
        public Matrix4 viewMatrix;
        public Matrix4 invViewMatrix;
        public Matrix4 projectionMatrix;
        public Matrix4 invProjectionMatrix;
        public Vector3 viewPos;
    }

    public class RenderEngine
    {
        public RenderContext context = new RenderContext();

        private List<Renderer> _renderers = new List<Renderer>();
        
        private FrameBuffer _gBuffer;

        private DualBuffer _dualBufferFull;

        private ScreenQuadRenderer _screenQuadRenderer;
        public ModelRenderer _modelRenderer;
        public TerrainRenderer _terrainRenderer;
        private InstancedModelRenderer _instancedModelRenderer;
        public DirectionalLightRenderer _directionalLightRenderer;
        public PointLightRenderer _pointLightRenderer;
        public ShadowCascadeMapRenderer _shadowCascadeMapRenderer;
        private ToneMapRenderer _toneMapRenderer;
        private FXAARenderer _fXAARenderer;
        private SSAORenderer _sSAORenderer;
        private BloomRenderer _bloomRenderer;
        private SkyRenderer _skyRenderer;
        private FogRenderer _fogRenderer;
        private ScreenSpaceReflectionRenderer _screenSpaceReflectionRenderer;
        private GaussianBlurRenderer _gaussianBlurRenderer;
        public SpotLightRenderer _spotLightRenderer;
        public ParticleRenderer _particleRenderer;
        public GrassRenderer _grassRenderer;
        public SunRenderer _sunRenderer;
        private DepthOfFieldRenderer _depthOfFieldRenderer;
        public static DebugRenderer _debugRenderer = new DebugRenderer();
        public bool debugView = false;
        private ShaderProgram _simpleShader;

        public DualBuffer lastUsedBuffer;

        public TextureGenerator textureGenerator = new TextureGenerator();

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

        public void InitRenderers(Vector2i resolution)
        {
            _screenQuadRenderer = new ScreenQuadRenderer();

            FrameBufferSettings frameBufferSettings = new FrameBufferSettings(resolution);
            DrawBufferSettings drawBufferSettings = new DrawBufferSettings(FramebufferAttachment.ColorAttachment0);
            drawBufferSettings.pixelType = PixelType.Float;
            drawBufferSettings.formatInternal = PixelInternalFormat.Rgba16f;
            frameBufferSettings.drawBuffers.Add(drawBufferSettings);
            DepthAttachmentSettings depthSettings = new DepthAttachmentSettings();
            depthSettings.isTexture = true;
            frameBufferSettings.depthAttachmentSettings = depthSettings;
            _dualBufferFull = new DualBuffer(frameBufferSettings);
            lastUsedBuffer = _dualBufferFull;
            textureGenerator.GenerateAllTextures();

            _modelRenderer = new ModelRenderer();
            _terrainRenderer = new TerrainRenderer();
            _instancedModelRenderer = new InstancedModelRenderer();
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
            _grassRenderer = new GrassRenderer();
            _sunRenderer = new SunRenderer();

        }

        private void InitGBuffer()
        {
            FrameBufferSettings gBufferSettings = new FrameBufferSettings(Engine.Resolution);
            DrawBufferSettings gAlbedo = new DrawBufferSettings(FramebufferAttachment.ColorAttachment0);
            gAlbedo.formatInternal = PixelInternalFormat.Rgba8;
            gAlbedo.pixelType = PixelType.UnsignedByte;
            gBufferSettings.drawBuffers.Add(gAlbedo);

            DrawBufferSettings gNormal = new DrawBufferSettings(FramebufferAttachment.ColorAttachment1);
            gNormal.formatInternal = PixelInternalFormat.Rgba16f;
            gNormal.pixelType = PixelType.Float;
            gBufferSettings.drawBuffers.Add(gNormal);

            DrawBufferSettings gMaterials = new DrawBufferSettings(FramebufferAttachment.ColorAttachment2);
            gMaterials.formatInternal = PixelInternalFormat.Rgba8;
            gMaterials.pixelType = PixelType.UnsignedByte;
            gBufferSettings.drawBuffers.Add(gMaterials);

            DepthAttachmentSettings depthSettings = new DepthAttachmentSettings();
            depthSettings.isTexture = true;
            gBufferSettings.depthAttachmentSettings = depthSettings;
            _gBuffer = new FrameBuffer(gBufferSettings);
        }

        public void Update()
        {
            if (Engine.WindowHandler.IsKeyPressed(Keys.T))
            {
            }
            //_grassRenderer.StepSimulation(ScreenQuadRenderer);
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
        public void Render()
        {

            PrepareFrame();

            if (debugView)
            {
                _debugRenderer.render(_dualBufferFull);
            } else
            {

                GeometryPass();
                LightPass();
                PostGeometryPass();
                PostProcessPass();


                //_debugRenderer.RenderNormals(eCSEngine, _dualBufferFull);

                _simpleShader.bind();
                lastUsedBuffer.GetLastFrameBuffer().resolveToScreen();

                //_screenQuadRenderer.RenderTextureToScreen(TextureGenerator.testTexture.textures[0]);

                //_dualBufferFull.RenderTextureToScreen(_gBuffer.GetAttachment(1));
                //_gBuffer.resolveToScreen();
                //_grassRenderer.GetLastFrameBuffer().resolveToScreen();
                //_screenQuadRenderer.RenderTextureToScreen(_grassRenderer.GetLastFrameBuffer().GetAttachment(0));

                //_screenQuadRenderer.RenderTextureToScreen(textureGenerator.testing.textures[0]);
                //_screenQuadRenderer.RenderTextureToScreen(textureGenerator._albedoBuffer.GetAttachment(0));
                //Console.WriteLine(textureGenerator.preparedTextures[0].textures[0]);
            }


            FinishFrame();
        }



        private void GeometryPass()
        {
            GBuffer.bind();
            GL.DepthMask(true);
            GL.ClearColor(0.0f, 0.0f, 0.0f, 1.0f);
            GL.Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit);

            _modelRenderer.RenderPass(this);
            _terrainRenderer.RenderPass(this);
            //_instancedModelRenderer.RenderPass(this);
            _grassRenderer.RenderPass(this);

            _dualBufferFull.blitBothDepthBufferFrom(_gBuffer);
        }
        private void LightPass()
        {
            _sSAORenderer.RenderPass(this);
            _shadowCascadeMapRenderer.RenderPass(this);

            _dualBufferFull.GetLastFrameBuffer().bind();
            GL.Clear(ClearBufferMask.ColorBufferBit);

            _directionalLightRenderer.RenderPass(this);
            _pointLightRenderer.RenderPass(this);
            _spotLightRenderer.RenderPass(this);
            _screenSpaceReflectionRenderer.RenderPass(this);

        }
        private void PostGeometryPass()
        {
            _skyRenderer.RenderPass(this);
            _particleRenderer.RenderPass(this);
        }
        private void PostProcessPass()
        {
            _fogRenderer.RenderPass(this);
            _sunRenderer.RenderPass(this);
            _bloomRenderer.RenderPass(this);
            //_fXAARenderer.RenderPass(eCSEngine, this);
            _depthOfFieldRenderer.RenderPass(this);
            _fXAARenderer.RenderPass(this); // before or after tone mapping????????????
            _toneMapRenderer.RenderPass(this);
        }

        private void PrepareFrame()
        {
            _dualBufferFull.clearBothBuffers();
        }


        private void FinishFrame()
        {

            Engine.WindowHandler.SwapBuffers();
        }


        public void OnResize(ResizeEventArgs eventArgs)
        {
            _dualBufferFull.OnResize(eventArgs);
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
