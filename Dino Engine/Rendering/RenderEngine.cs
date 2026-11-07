using Dino_Engine.Core;
using Dino_Engine.Debug;
using Dino_Engine.Rendering.Renderers;
using Dino_Engine.Rendering.Renderers.AntiAliasing;
using Dino_Engine.Rendering.Renderers.Geometry;
using Dino_Engine.Rendering.Renderers.Lighting;
using Dino_Engine.Rendering.Renderers.PosGeometry;
using Dino_Engine.Rendering.Renderers.PostProcessing;
using Dino_Engine.Rendering.Renderers.Simulation;
using Dino_Engine.Textures;
using Dino_Engine.Util;
using OpenTK.Graphics.OpenGL;
using OpenTK.Mathematics;
using OpenTK.Windowing.Common;
using OpenTK.Windowing.GraphicsLibraryFramework;
using System;
using System.Runtime.InteropServices;

namespace Dino_Engine.Rendering
{
    public struct ShaderGlobals
    {
        public Matrix4 viewMatrix;
        public Matrix4 invViewMatrix;
        public Matrix4 projectionMatrix;
        public Matrix4 invProjectionMatrix;
        public Vector3 viewPos;
        public float time;
        public Vector2 resolution;
        public Vector3 skyColour;
        public Vector4 focusPoint;  // xyz a point to keep in view, w the radius of the opening; see Shaders/FocusCutout.glsl.
        public float daylight;      // How bright the day is, 1 in full sun: for what's lit without going through the light passes.
        public Vector3 sunDirection;    // Unit vector towards the sun.
        public Vector3 sunColour;       // Its radiance by this hour, above whatever clouds there are.
        public Vector3 clearSkyColour;  // What the sky would be with no weather at all: the gaps between the clouds.
    }
    public struct ShaderGlobals2
    {
        public Matrix4 projectionMatrix;
        public Matrix4 viewMatrix;
        public Matrix4 invProjectionMatrix;
        public Matrix4 invViewMatrix;
        public Vector3 viewPosWorld;
        public float time;
        public Vector2 resolution;
        public float delta;
        public int worldSeed;
        public Vector3 skyColour;
        public float EMPTY;
        public Vector4 focusPoint;
    }

    public class RenderEngine
    {
        public ShaderGlobals context = new ShaderGlobals();

        /// <summary>The one graphics setting, which <see cref="GraphicsPresets"/> turns into all the others.</summary>
        public GraphicsQuality Quality = GraphicsQuality.High;
        private ShaderGlobals2 globals = new ShaderGlobals2();

        private List<BaseRenderer> _renderers = new List<BaseRenderer>();
        
        private FrameBuffer _gBuffer;

        private DualBuffer _dualBufferFull;

        private ScreenQuadRenderer _screenQuadRenderer;
        public ModelRenderer _modelRenderer;
        public TerrainRenderer _terrainRenderer;
        public InstancedModelRenderer _instancedModelRenderer;
        public CharacterRenderer _characterRenderer;
        public WildlifeRenderer _wildlifeRenderer;
        public ImposterRenderer _imposterRenderer;
        public AmbientLightRenderer _ambientLightRenderer;
        public DirectionalLightRenderer _directionalLightRenderer;
        public PointLightRenderer _pointLightRenderer;
        private ToneMapRenderer _toneMapRenderer;
        private FXAARenderer _fXAARenderer;
        private SSAORenderer _sSAORenderer;
        private BloomRenderer _bloomRenderer;
        private SkyRenderer _skyRenderer;
        public VolumetricFogRenderer _volumetricFogRenderer;
        public WindRenderer _windRenderer;
        public WaterRenderer _waterRenderer;
        public WeatherRenderer _weatherRenderer;
        private PrecipitationRenderer _precipitationRenderer;
        private LightningRenderer _lightningRenderer;
        private ScreenSpaceReflectionRenderer _screenSpaceReflectionRenderer;
        private GaussianBlurRenderer _gaussianBlurRenderer;
        private DepthBasedBlurRenderer _depthBasedBlurRenderer;
        public SpotLightRenderer _spotLightRenderer;
        public ParticleRenderer _particleRenderer;
        public BeamRenderer _beamRenderer;
        public GroundMarkRenderer _groundMarkRenderer;
        public OutlineRenderer _outlineRenderer;
        public TrailRenderer _trailRenderer;
        public GrassRenderer _grassRenderer;
        public CelestialBodyRenderer _sunRenderer;
        private DepthOfFieldRenderer _depthOfFieldRenderer;
        private MsaaRenderer _msaa;
        public static DebugRenderer _debugRenderer = new DebugRenderer();
        public bool debugView = false;
        private ShaderProgram _simpleShader;

        public DualBuffer lastUsedBuffer;

        private int globalsUBO;

        public TextureGenerator textureGenerator = new TextureGenerator();

        public GaussianBlurRenderer GaussianBlurRenderer { get => _gaussianBlurRenderer; }
        public DepthBasedBlurRenderer DepthBasedBlurRenderer { get => _depthBasedBlurRenderer; }
        /// <summary>
        /// The single-sample G-buffer the light and screen-space passes read, resolved from the multisampled one each frame (the
        /// average of a pixel's samples, or the farthest on edge pixels, which attachment <see cref="MsaaRenderer.EdgeAttachment"/> flags).
        /// Geometry renders into <see cref="MsaaRenderer.GeometryBuffer"/>.
        /// </summary>
        public FrameBuffer GBuffer { get => _gBuffer; }
        public ScreenQuadRenderer ScreenQuadRenderer { get => _screenQuadRenderer; }

        /// <summary>Takes pictures of models for the interface.</summary>
        public Renderers.Studio.ItemStudio Studio { get; private set; } = null!;
        /// <summary>Anti-aliasing settings, read each frame.</summary>
        public AntiAliasingSettings AntiAliasing { get; } = new AntiAliasingSettings();
        /// <summary>Lighting and exposure settings, read each frame.</summary>
        public LightingSettings Lighting { get; } = new LightingSettings();
        /// <summary>How reflections are traced, read each frame.</summary>
        public ReflectionSettings Reflections { get; } = new ReflectionSettings();
        /// <summary>The multisampled G-buffer and scene, and the passes that resolve them.</summary>
        public MsaaRenderer Msaa { get => _msaa; }
        /// <summary>This frame's screen-space ambient occlusion, for the light passes.</summary>
        public int AmbientOcclusionTexture { get => _sSAORenderer.OcclusionTexture; }
        /// <summary>This frame's lit scene as a soft mip chain, once the light passes have run.</summary>
        public int SceneBlurTexture { get => _screenSpaceReflectionRenderer.SceneBlurTexture; }

        public RenderEngine()
        {
            InitGBuffer();

            _simpleShader = new ShaderProgram("Simple.vert", "Simple.frag");

            _simpleShader.bind();
            _simpleShader.loadUniformInt("blitTexture", 0);
            _simpleShader.unBind();

            Console.WriteLine("MARShal. " + Marshal.SizeOf<ShaderGlobals2>());

            globalsUBO = GL.GenBuffer();
            GL.BindBuffer(BufferTarget.UniformBuffer, globalsUBO);
            GL.BufferData(BufferTarget.UniformBuffer, Marshal.SizeOf<ShaderGlobals2>() , IntPtr.Zero, BufferUsageHint.DynamicDraw);
            GL.BindBufferBase(BufferRangeTarget.UniformBuffer, 0, globalsUBO);
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

            _msaa = new MsaaRenderer(AntiAliasing);
            _modelRenderer = new ModelRenderer();
            _terrainRenderer = new TerrainRenderer();
            _instancedModelRenderer = new InstancedModelRenderer();
            _characterRenderer = new CharacterRenderer();
            _wildlifeRenderer = new WildlifeRenderer();
            _imposterRenderer = new ImposterRenderer();
            _ambientLightRenderer = new AmbientLightRenderer();
            _directionalLightRenderer = new DirectionalLightRenderer();
            _pointLightRenderer = new PointLightRenderer();
            _toneMapRenderer = new ToneMapRenderer();
            _fXAARenderer = new FXAARenderer();
            _sSAORenderer = new SSAORenderer();
            _bloomRenderer = new BloomRenderer();
            _skyRenderer = new SkyRenderer();
            _volumetricFogRenderer = new VolumetricFogRenderer();
            _windRenderer = new WindRenderer();
            _waterRenderer = new WaterRenderer();
            _weatherRenderer = new WeatherRenderer();
            _weatherRenderer.SetMaterialPorosity(textureGenerator.Porosity);
            Studio = new Renderers.Studio.ItemStudio();
            _precipitationRenderer = new PrecipitationRenderer();
            _lightningRenderer = new LightningRenderer();
            _screenSpaceReflectionRenderer = new ScreenSpaceReflectionRenderer();
            _gaussianBlurRenderer = new GaussianBlurRenderer();
            _depthBasedBlurRenderer = new DepthBasedBlurRenderer();
            _spotLightRenderer = new SpotLightRenderer();
            _particleRenderer = new ParticleRenderer();
            _beamRenderer = new BeamRenderer();
            _groundMarkRenderer = new GroundMarkRenderer();
            _outlineRenderer = new OutlineRenderer();
            _trailRenderer = new TrailRenderer();
            _depthOfFieldRenderer = new DepthOfFieldRenderer();
            _grassRenderer = new GrassRenderer();
            _sunRenderer = new CelestialBodyRenderer();

        }

        private void InitGBuffer()
        {
            FrameBufferSettings gBufferSettings = new FrameBufferSettings(Engine.Resolution);
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

            // 1 on the edge pixels, whose MSAA samples show different surfaces (MsaaRenderer.EdgeAttachment).
            DrawBufferSettings gEdges = new DrawBufferSettings(FramebufferAttachment.ColorAttachment3);
            gEdges.formatInternal = PixelInternalFormat.R8;
            gEdges.formatExternal = PixelFormat.Red;
            gEdges.pixelType = PixelType.UnsignedByte;
            gEdges.minFilterType = TextureMinFilter.Nearest;
            gEdges.magFilterType = TextureMagFilter.Nearest;
            gBufferSettings.drawBuffers.Add(gEdges);

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
            foreach (BaseRenderer renderer in _renderers)
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


            Studio.RenderPass();
            PrepareFrame();

            if (debugView)
            {
                _dualBufferFull.clearBothBuffers();
                _debugRenderer.render(_dualBufferFull);
            } else
            {

                // Weather and wind first: geometry, shadows, water and fog all read this frame's.
                _weatherRenderer.RenderPass(this);
                _windRenderer.RenderPass(this);
                _waterRenderer.SimulationPass(this);
                GeometryPass();
                ShadowPass();
                LightPass();
                // Water over the lit scene, before the sky and particles, which then sit behind and on it.
                _waterRenderer.RenderPass(this);
                PostGeometryPass();
                PostProcessPass();


                //_debugRenderer.RenderNormals(_dualBufferFull);

                if (!_toneMapRenderer.ToScreen)
                {
                    _simpleShader.bind();
                    lastUsedBuffer.GetLastFrameBuffer().resolveToScreen();
                }

                //_screenQuadRenderer.RenderTextureToScreen(TextureGenerator.testTexture.textures[0]);

                //_dualBufferFull.RenderTextureToScreen(_gBuffer.GetAttachment(0));
                //_dualBufferFull.RenderTextureToScreen(_gBuffer.GetAttachment(1));
                //_dualBufferFull.RenderTextureToScreen(_gBuffer.GetAttachment(2));
                //_gBuffer.resolveToScreen();
                //_grassRenderer.GetLastFrameBuffer().resolveToScreen();
                //_screenQuadRenderer.RenderTextureToScreen(_grassRenderer.GetLastFrameBuffer().GetAttachment(0));

                //_screenQuadRenderer.RenderTextureToScreen(textureGenerator.testing.textures[0]);
                //_screenQuadRenderer.RenderTextureToScreen(textureGenerator._albedoBuffer.GetAttachment(0));
                //Console.WriteLine(textureGenerator.preparedTextures[0].textures[0]);
            }

            RenderGui();

            FinishFrame();


        }

        private void RenderGui()
        {
            var gui = Engine.Instance?.GuiOrNull;
            if (gui == null) return;
            Engine.PerformanceMonitor.startGPUTask("GUI");
            Engine.PerformanceMonitor.startCPUTask("GUI RENDER");
            gui.Render();
            Engine.PerformanceMonitor.finishCPUTask("GUI RENDER");
            Engine.PerformanceMonitor.finishGPUTask("GUI");
        }

        private void ShadowPass()
        {
            _weatherRenderer.BeginRainShadow();

            _terrainRenderer.ShadowRenderPass(this);
            _grassRenderer.ShadowRenderPass(this);



            _modelRenderer.ShadowRenderPass(this);
            _characterRenderer.ShadowRenderPass(this);
            _wildlifeRenderer.ShadowRenderPass(this);
            _instancedModelRenderer.ShadowRenderPass(this);
            _imposterRenderer.ShadowRenderPass(this);

            _weatherRenderer.FinishRainShadow(this);
        }

        private void GeometryPass()
        {
            _msaa.BeginGeometry();
            _weatherRenderer.BindSurfaceMaps();

            _modelRenderer.GeometryRenderPass(this);
            _characterRenderer.GeometryRenderPass(this);
            _wildlifeRenderer.GeometryRenderPass(this);
            _instancedModelRenderer.GeometryRenderPass(this);
            _imposterRenderer.GeometryRenderPass(this);

            // Grass before terrain: blades hide much of the ground, and depth testing then skips the terrain's costly
            // parallax shading behind them. Grass also uploads the settings the terrain's ground tint reads.
            _grassRenderer.GeometryRenderPass(this);
            _terrainRenderer.GeometryRenderPass(this);

            // The single-sample G-buffer for the screen-space passes, and the edge pixels marked for per-sample shading.
            _msaa.ResolveGeometry(this);
        }
        private void LightPass()
        {
            _sSAORenderer.RenderPass(this);

            // Lighting adds up in the multisampled scene, which shares the G-buffer's depth and edge stencil.
            _msaa.SceneBuffer.bind();
            GL.ClearColor(0f, 0f, 0f, 1f);
            GL.Clear(ClearBufferMask.ColorBufferBit);

            _ambientLightRenderer.RenderPass(this);
            _directionalLightRenderer.RenderPass(this);
            _pointLightRenderer.RenderPass(this);
            _spotLightRenderer.RenderPass(this);
            _screenSpaceReflectionRenderer.RenderPass(this);

        }
        private void PostGeometryPass()
        {
            _skyRenderer.RenderPass(this);
            _particleRenderer.RenderPass(this);
            _beamRenderer.RenderPass(this);
            _trailRenderer.RenderPass(this);
        }
        private void PostProcessPass()
        {
            // The fog's composite is the last pass over the multisampled scene, which is then resolved into the dual buffer.
            _volumetricFogRenderer.RenderPass(this);
            // What's about to hit someone comes over the fog: it isn't atmosphere, and nothing is allowed to hide it.
            _groundMarkRenderer.RenderPass(this);
            // And so is who's doing it.
            _outlineRenderer.RenderPass(this);
            // What falls comes in front of the fog, and is lit by what it falls through.
            _precipitationRenderer.RenderPass(this);
            _lightningRenderer.RenderPass(this);
            _msaa.ResolveScene(this);
            // The sun's depth test reads the dual buffer's depth.
            if (_sunRenderer.Shows(this)) _dualBufferFull.blitBothDepthBufferFrom(_gBuffer);

            _depthOfFieldRenderer.RenderPass(this);
            _sunRenderer.RenderPass(this);
            _bloomRenderer.RenderPass(this);
            // Straight onto the screen, unless something is still to come or the F3 tools might want to look at it.
            _toneMapRenderer.ToScreen = !AntiAliasing.Fxaa && !AntiAliasing.ShowEdgePixels && !Engine.DebugOverlay.Visible;
            _toneMapRenderer.RenderPass(this);
            // FXAA works on the image as it will be seen: tone mapped, gamma encoded.
            if (AntiAliasing.Fxaa) _fXAARenderer.RenderPass(this);
            if (AntiAliasing.ShowEdgePixels && _msaa.Samples > 1) _msaa.DrawEdgeOverlay(this);
        }

        private void PrepareFrame()
        {
            _msaa.BeginFrame(AntiAliasing);

            /*
            Vector3 viewPos = new Vector3(500, 500, 500);
            var viewMatrix = MyMath.createViewMatrix(viewPos, new Vector3(MathF.PI/2f, 0, 0));
            context.viewMatrix = viewMatrix;
            context.invViewMatrix = Matrix4.Invert(viewMatrix);
            context.viewPos = viewPos;
            */

            globals.projectionMatrix = context.projectionMatrix;
            globals.viewMatrix = context.viewMatrix;
            globals.invProjectionMatrix = context.invProjectionMatrix;
            globals.invViewMatrix = context.invViewMatrix;
            globals.viewPosWorld = context.viewPos;
            globals.time = Engine.Time;
            globals.resolution =Engine.Resolution;
            globals.delta = Engine.Delta;
            globals.worldSeed = 1;
            globals.skyColour = context.skyColour;
            globals.focusPoint = context.focusPoint;





            GL.BindBuffer(BufferTarget.UniformBuffer, globalsUBO);
            GL.BufferSubData(BufferTarget.UniformBuffer, IntPtr.Zero, Marshal.SizeOf<ShaderGlobals2>() ,ref globals);
        }


        private void FinishFrame()
        {

            Engine.Instance?.CaptureFrame();
            Engine.PerformanceMonitor.startCPUTask("Waiting for the GPU");
            Engine.WindowHandler.SwapBuffers();
            Engine.PerformanceMonitor.finishCPUTask("Waiting for the GPU");
        }


        public void OnResize(ResizeEventArgs eventArgs)
        {
            _dualBufferFull.OnResize(eventArgs);
            _gBuffer.resize(eventArgs.Size);
            foreach(BaseRenderer renderer in _renderers)
            {
                renderer.OnResize(eventArgs);
            }
        }

        public void CleanUp()
        {
            foreach (BaseRenderer renderer in _renderers)
            {
                renderer.CleanUp();
            }
            _gBuffer.cleanUp();
            _simpleShader.cleanUp();

            GL.DeleteBuffer(globalsUBO);
        }

        public List<BaseRenderer> Renderers { get => _renderers; }
    }
}
