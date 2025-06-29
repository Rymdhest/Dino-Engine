﻿using Dino_Engine.ECS;
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
        private ShaderProgram _InstancedShadowShader = new ShaderProgram("Shadow_Instanced.vert", "Shadow.frag");

        public const int CASCADETEXTURESINDEXSTART = 4;

        public ShadowCascadeMapRenderer()
        {
            _shadowShader.bind();
            _shadowShader.loadUniformInt("albedoMapTextureArray", 0);

            _shadowShader.loadUniformInt("albedoMapModelTextureArray", 3);
            _shadowShader.unBind();

            _InstancedShadowShader.bind();
            _InstancedShadowShader.loadUniformInt("albedoMapTextureArray", 0);

            _InstancedShadowShader.loadUniformInt("albedoMapModelTextureArray", 3);
            _InstancedShadowShader.unBind();
        }
        internal override void Prepare(ECSEngine eCSEngine, RenderEngine renderEngine)
        {
            GL.DepthMask(true);
            GL.Enable(EnableCap.DepthTest);
            GL.Disable(EnableCap.CullFace);
            GL.Disable(EnableCap.Blend);
            GL.Enable(EnableCap.PolygonOffsetFill);
            GL.CullFace(CullFaceMode.Front);
        }

        internal override void Finish(ECSEngine eCSEngine, RenderEngine renderEngine)
        {

            GL.Disable(EnableCap.PolygonOffsetFill);
            GL.Enable(EnableCap.CullFace);
            GL.CullFace(CullFaceMode.Back);

            GL.DisableVertexAttribArray(0);
            GL.DisableVertexAttribArray(4);
            GL.DisableVertexAttribArray(5);
            GL.DisableVertexAttribArray(6);
            GL.DisableVertexAttribArray(7);
        }

        internal override void Render(ECSEngine eCSEngine, RenderEngine renderEngine)
        {

            _shadowShader.bind();

            _shadowShader.loadUniformInt("numberOfMaterials", renderEngine.textureGenerator.loadedMaterialTextures);

            GL.ActiveTexture(TextureUnit.Texture0);
            GL.BindTexture(TextureTarget.Texture2DArray, renderEngine.textureGenerator.megaAlbedoTextureArray);

            GL.ActiveTexture(TextureUnit.Texture3);
            GL.BindTexture(TextureTarget.Texture2DArray, renderEngine.textureGenerator.megaAlbedoModelTextureArray);

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
                            GL.EnableVertexAttribArray(4);
                            GL.EnableVertexAttribArray(5);
                            foreach (Entity entity in glmodels.Value)
                            {
                                Matrix4 transformationMatrix = MyMath.createTransformationMatrix(entity.getComponent<TransformationComponent>().Transformation);
                                _shadowShader.loadUniformMatrix4f("modelViewProjectionMatrix", transformationMatrix * shadow.LightViewMatrix * cascade.getProjectionMatrix());

                                GL.DrawElements(PrimitiveType.Triangles, glmodel.getVertexCount(), DrawElementsType.UnsignedInt, 0);
                            }
                        }

                        foreach (KeyValuePair<glModel, List<Entity>> glmodels in eCSEngine.getSystem<TerrainRenderSystem>().ModelsDictionary)
                        {
                            glModel glmodel = glmodels.Key;
                            GL.BindVertexArray(glmodel.getVAOID());
                            GL.EnableVertexAttribArray(0);
                            GL.EnableVertexAttribArray(4);
                            GL.EnableVertexAttribArray(5);
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
            
            _InstancedShadowShader.bind();

            _InstancedShadowShader.loadUniformInt("numberOfMaterials", renderEngine.textureGenerator.loadedMaterialTextures);

            GL.ActiveTexture(TextureUnit.Texture0);
            GL.BindTexture(TextureTarget.Texture2DArray, renderEngine.textureGenerator.megaAlbedoTextureArray);

            GL.ActiveTexture(TextureUnit.Texture3);
            GL.BindTexture(TextureTarget.Texture2DArray, renderEngine.textureGenerator.megaAlbedoModelTextureArray);

            foreach (Entity directionalLight in eCSEngine.getSystem<DirectionalLightSystem>().MemberEntities)
            {
                if (directionalLight.TryGetComponent(out CascadingShadowComponent shadow))
                {
                    _InstancedShadowShader.loadUniformMatrix4f("viewMatrix", shadow.LightViewMatrix);
                    Vector3 lightDirection = directionalLight.getComponent<DirectionComponent>().Direction;
                    foreach (CascadingShadowComponent.ShadowCascade cascade in shadow.Cascades)
                    {
                        _InstancedShadowShader.loadUniformMatrix4f("projectionMatrix", cascade.getProjectionMatrix());

                        cascade.bindFrameBuffer();
                        //GL.Clear(ClearBufferMask.DepthBufferBit);
                        GL.PolygonOffset(cascade.getPolygonOffset(), 1f);

                        foreach (KeyValuePair<glModel, List<Entity>> glmodels in eCSEngine.getSystem<InstancedModelSystem>().ModelsDictionary)
                        {
                            glModel glmodel = glmodels.Key;
                            GL.BindVertexArray(glmodel.getVAOID());
                            GL.EnableVertexAttribArray(0);
                            GL.EnableVertexAttribArray(4);
                            GL.EnableVertexAttribArray(5);
                            GL.EnableVertexAttribArray(6);
                            GL.EnableVertexAttribArray(7);



                            GL.DrawElementsInstanced(PrimitiveType.Triangles, glmodel.getVertexCount(), DrawElementsType.UnsignedInt, 0, glmodels.Value.Count);
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
