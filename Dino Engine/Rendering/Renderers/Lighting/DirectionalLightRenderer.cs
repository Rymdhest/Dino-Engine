﻿using OpenTK.Mathematics;
using OpenTK.Graphics.OpenGL;
using OpenTK.Windowing.Common;
using Dino_Engine.ECS;
using Dino_Engine.Util;
using Dino_Engine.Core;
using static OpenTK.Graphics.OpenGL.GL;
using Dino_Engine.ECS.Components;
using static Dino_Engine.ECS.Components.CascadingShadowComponent;


namespace Dino_Engine.Rendering.Renderers.Lighting
{
    internal class DirectionalLightRenderer : Renderer
    {
        private ShaderProgram _directionalLightShader = new ShaderProgram("Simple.vert", "Directional_Light.frag");

        public DirectionalLightRenderer()
        {
            _directionalLightShader.bind();
            _directionalLightShader.loadUniformInt("gAlbedo", 0);
            _directionalLightShader.loadUniformInt("gNormal", 1);
            _directionalLightShader.loadUniformInt("gMaterials", 2);
            _directionalLightShader.loadUniformInt("gDepth", 3);
            _directionalLightShader.unBind();
        }
        internal override void Prepare(ECSEngine eCSEngine, RenderEngine renderEngine)
        {
            FrameBuffer gBuffer = renderEngine.GBuffer;

            _directionalLightShader.bind();

            Enable(EnableCap.Blend);
            BlendFunc(BlendingFactor.One, BlendingFactor.One);
            BlendEquation(BlendEquationMode.FuncAdd);

            ActiveTexture(TextureUnit.Texture0);
            BindTexture(TextureTarget.Texture2D, gBuffer.GetAttachment(0));

            ActiveTexture(TextureUnit.Texture1);
            BindTexture(TextureTarget.Texture2D, gBuffer.GetAttachment(1));

            ActiveTexture(TextureUnit.Texture2);
            BindTexture(TextureTarget.Texture2D, gBuffer.GetAttachment(2));

            ActiveTexture(TextureUnit.Texture3);
            BindTexture(TextureTarget.Texture2D, gBuffer.getDepthAttachment());
        }
        internal override void Finish(ECSEngine eCSEngine, RenderEngine renderEngine)
        {

        }

        internal override void Render(ECSEngine eCSEngine, RenderEngine renderEngine)
        {
            Matrix4 viewMatrix = MyMath.createViewMatrix(eCSEngine.Camera.getComponent<TransformationComponent>().Transformation);
            Matrix4 projectionMatrix = eCSEngine.Camera.getComponent<ProjectionComponent>().ProjectionMatrix;

            foreach (Entity entity in eCSEngine.getSystem<DirectionalLightSystem>().MemberEntities)
            {
                Vector3 lightDirection = entity.getComponent<DirectionComponent>().Direction;
                Vector3 lightColour = entity.getComponent<ColourComponent>().Colour.ToVector3();
                float ambientFactor = AmbientLightComponent.DEFAULT;

                if (entity.TryGetComponent(out AmbientLightComponent? ambientLightComponent))
                {
                    ambientFactor = ambientLightComponent.AmbientFactor;
                }
                if (entity.TryGetComponent(out CascadingShadowComponent shadow))
                {
                    for (int i = 0; i < shadow.Cascades.Count; i++)
                    {

                        ShadowCascade cascade = shadow.Cascades[i];
                        _directionalLightShader.loadUniformInt("shadowMaps[" + i + "]", ShadowCascadeMapRenderer.CASCADETEXTURESINDEXSTART + i);

                        _directionalLightShader.loadUniformFloat("cascadeProjectionSizes[" + i + "]", cascade.getProjectionSize());
                        ActiveTexture(TextureUnit.Texture4 + i);
                        BindTexture(TextureTarget.Texture2D, cascade.getDepthTexture());
                        //_directionalLightShader.loadUniformVector2f("shadowMapResolutions[" + i + "]", cascade.getResolution());


                        Matrix4 shadowMatrix = Matrix4.Invert(viewMatrix) * shadow.LightViewMatrix * cascade.getProjectionMatrix();
                        _directionalLightShader.loadUniformMatrix4f("sunSpaceMatrices[" + i + "]", shadowMatrix);
                        _directionalLightShader.loadUniformInt("numberOfCascades", shadow.Cascades.Count);
                    }
                }
                else
                {
                    _directionalLightShader.loadUniformInt("numberOfCascades", 0);
                }



                Vector4 lightDirectionViewSpace = new Vector4(lightDirection, 1.0f) * Matrix4.Transpose(Matrix4.Invert(viewMatrix));
                _directionalLightShader.loadUniformVector3f("LightDirectionViewSpace", lightDirectionViewSpace.Xyz);

                _directionalLightShader.loadUniformVector3f("lightColour", lightColour);
                _directionalLightShader.loadUniformFloat("ambientFactor", ambientFactor);
                _directionalLightShader.loadUniformVector2f("resolution", Engine.Resolution);
                _directionalLightShader.loadUniformMatrix4f("invProjection", Matrix4.Invert(projectionMatrix));

                renderEngine.ScreenQuadRenderer.Render(clearColor: false, blend: true);
            }
        }
        public override void Update()
        {
        }
        public override void OnResize(ResizeEventArgs eventArgs)
        {
        }


        public override void CleanUp()
        {
            _directionalLightShader.cleanUp();
        }
    }
}
