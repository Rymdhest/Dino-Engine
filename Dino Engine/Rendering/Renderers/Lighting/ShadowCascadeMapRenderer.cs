using Dino_Engine.ECS;
using Dino_Engine.Util;
using OpenTK.Mathematics;
using OpenTK.Windowing.Common;
using OpenTK.Graphics.OpenGL;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Dino_Engine.ECS.Components;
using Dino_Engine.Modelling.Model;

namespace Dino_Engine.Rendering.Renderers.Lighting
{
    internal class ShadowCascadeMapRenderer : Renderer
    {

        private ShaderProgram _shadowShader = new ShaderProgram("Shadow.vert", "Shadow.frag");

        public const int CASCADETEXTURESINDEXSTART = 4;

        public ShadowCascadeMapRenderer()
        {

        }
        internal override void Prepare(ECSEngine eCSEngine, RenderEngine renderEngine)
        {
            GL.DepthMask(true);
            GL.Enable(EnableCap.DepthTest);
            GL.Disable(EnableCap.CullFace);
            GL.Disable(EnableCap.Blend);
            GL.Enable(EnableCap.PolygonOffsetFill);
            _shadowShader.bind();
        }

        internal override void Finish(ECSEngine eCSEngine, RenderEngine renderEngine)
        {

            GL.Disable(EnableCap.PolygonOffsetFill);
            GL.Enable(EnableCap.CullFace);
            GL.CullFace(CullFaceMode.Back);
        }

        internal override void Render(ECSEngine eCSEngine, RenderEngine renderEngine)
        {

            foreach (Entity directionalLight in eCSEngine.getSystem<DirectionalLightSystem>().MemberEntities)
            {
                if (directionalLight.TryGetComponent(out CascadingShadowComponent shadow))
                {
                    Vector3 lightDirection = directionalLight.getComponent<DirectionComponent>().Direction;
                    foreach (CascadingShadowComponent.ShadowCascade cascade in shadow.Cascades)
                    {
                        cascade.bindFrameBuffer();
                        GL.Clear(ClearBufferMask.DepthBufferBit);
                        GL.PolygonOffset(cascade.getPolygonOffset(), 1f);

                        foreach (KeyValuePair<glModel, List<Entity>> glmodels in eCSEngine.getSystem<ModelRenderSystem>().ModelsDictionary)
                        {
                            glModel glmodel = glmodels.Key;
                            GL.BindVertexArray(glmodel.getVAOID());
                            GL.EnableVertexAttribArray(0);
                            foreach (Entity entity in glmodels.Value)
                            {
                                Matrix4 transformationMatrix = MyMath.createTransformationMatrix(entity.getComponent<TransformationComponent>().Transformation);
                                _shadowShader.loadUniformMatrix4f("modelViewProjectionMatrix", transformationMatrix * shadow.LightViewMatrix * cascade.getProjectionMatrix());

                                GL.DrawElements(PrimitiveType.Triangles, glmodel.getVertexCount(), DrawElementsType.UnsignedInt, 0);
                            }
                        }
                    }
                }
            }
        }

        public override void OnResize(ResizeEventArgs eventArgs)
        {
        }

        public override void Update()
        {
        }
        public override void CleanUp()
        {
            _shadowShader.cleanUp();
        }


    }
}
