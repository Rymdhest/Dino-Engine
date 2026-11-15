using System.Collections.Generic;
using OpenTK.Graphics.OpenGL;
using OpenTK.Mathematics;
using Dino_Engine.Modelling.Model;
using Dino_Engine.Rendering.Renderers.Lighting;

namespace Dino_Engine.Rendering.Renderers.Geometry
{
    public struct CharacterInstanceData
    {
        public Matrix4 ModelMatrix;
        public Matrix4[] SkinningMatrices;

        /// <summary>What a blow has lit them up with: its colour, and how much of them.</summary>
        public Vector4 Flash;

        /// <summary>Whether it opens for the player behind it (x, 1 or 0), and how far it's thinned away (y).</summary>
        public Vector4 Sight;
    }
    public struct CharacterRenderCommand : IRenderCommand
    {
        public glModel model;
        public List<CharacterInstanceData> instances;

        public CharacterRenderCommand(glModel model, List<CharacterInstanceData> instances)
        {
            this.model = model;
            this.instances = instances;
        }
    }
    public class CharacterRenderer : GeometryCommandDrivenRenderer<CharacterRenderCommand>
    {
        private ShaderProgram _characterShader = new ShaderProgram("Model_Character.vert", "Model.frag", new[] { "CHARACTER" });
        private ShaderProgram _characterShadowShader = new ShaderProgram("Model_Character_Shadow.vert", "Shadow.frag");
        // Skin and hide alone, for how thick they are along the sun (FleshDepth, SUBSURFACE.md).
        private ShaderProgram _characterFleshShader = new ShaderProgram("Model_Character_Shadow.vert", "Shadow_Flesh.frag");
        private ShaderProgram _spectralShader = new ShaderProgram("Spectral.vert", "Spectral.frag");
        private readonly List<(glModel Model, CharacterInstanceData Instance)> _spectral = new();

        public CharacterRenderer() : base("Character")
        {
            _characterShader.bind();
            _characterShader.loadUniformInt("albedoMapTextureArray", 0);
            _characterShader.loadUniformInt("normalMapTextureArray", 1);
            _characterShader.loadUniformInt("materialMapTextureArray", 2);

            _characterShader.loadUniformInt("albedoMapModelTextureArray", 3);
            _characterShader.loadUniformInt("normalMapModelTextureArray", 4);
            _characterShader.loadUniformInt("materialMapModelTextureArray", 5);
            Simulation.WeatherRenderer.LinkSurfaceSamplers(_characterShader);
            PosGeometry.WaterRenderer.LinkShoreSamplers(_characterShader);
            _characterShader.unBind();

            _characterShadowShader.bind();
            _characterShadowShader.loadUniformInt("albedoMapTextureArray", 0);
            _characterShadowShader.loadUniformInt("albedoMapModelTextureArray", 3);
            _characterShadowShader.unBind();
        }

        public override void CleanUp()
        {
            _spectralShader.cleanUp();
            _characterShadowShader.cleanUp();
            _characterFleshShader.cleanUp();
            _characterShader.cleanUp();
        }

        internal override void PrepareGeometry(RenderEngine renderEngine)
        {
            GL.Enable(EnableCap.DepthTest);
            GL.Disable(EnableCap.CullFace);
            GL.CullFace(CullFaceMode.Back);
            GL.Disable(EnableCap.Blend);

            // So that what lies on the ground can go under them.
            GL.Enable(EnableCap.StencilTest);
            GL.StencilMask(AntiAliasing.MsaaRenderer.CharacterStencilBit);
            GL.StencilFunc(StencilFunction.Always, AntiAliasing.MsaaRenderer.CharacterStencilBit, AntiAliasing.MsaaRenderer.CharacterStencilBit);
            GL.StencilOp(StencilOp.Keep, StencilOp.Keep, StencilOp.Replace);

            _characterShader.bind();

            _characterShader.loadUniformFloat("reliefScale", renderEngine.ReliefScale);

            _characterShader.loadUniformInt("numberOfMaterials", renderEngine.textureGenerator.loadedMaterialTextures);

            GL.ActiveTexture(TextureUnit.Texture0);
            GL.BindTexture(TextureTarget.Texture2DArray, renderEngine.textureGenerator.megaAlbedoTextureArray);
            GL.ActiveTexture(TextureUnit.Texture1);
            GL.BindTexture(TextureTarget.Texture2DArray, renderEngine.textureGenerator.megaNormalTextureArray);
            GL.ActiveTexture(TextureUnit.Texture2);
            GL.BindTexture(TextureTarget.Texture2DArray, renderEngine.textureGenerator.megaMaterialTextureArray);

            GL.ActiveTexture(TextureUnit.Texture3);
            GL.BindTexture(TextureTarget.Texture2DArray, renderEngine.textureGenerator.megaAlbedoModelTextureArray);
            GL.ActiveTexture(TextureUnit.Texture4);
            GL.BindTexture(TextureTarget.Texture2DArray, renderEngine.textureGenerator.megaNormalModelTextureArray);
            GL.ActiveTexture(TextureUnit.Texture5);
            GL.BindTexture(TextureTarget.Texture2DArray, renderEngine.textureGenerator.megaMaterialModelTextureArray);
        }

        internal override void FinishGeometry(RenderEngine renderEngine)
        {
            GL.StencilMask(0xFF);
            GL.Disable(EnableCap.StencilTest);
            
            GL.DisableVertexAttribArray(0);
            GL.DisableVertexAttribArray(1);
            GL.DisableVertexAttribArray(2);
            GL.DisableVertexAttribArray(3);
            GL.DisableVertexAttribArray(4);
            GL.DisableVertexAttribArray(5);
            GL.DisableVertexAttribArray(6); // Bone Indices
            GL.DisableVertexAttribArray(7); // Bone Weights
            
            GL.BindVertexArray(0);
        }

        internal override void PrepareShadow(RenderEngine renderEngine)
        {
            GL.DepthMask(true);
            GL.Enable(EnableCap.DepthTest);
            GL.Disable(EnableCap.CullFace);
            GL.Disable(EnableCap.Blend);
            GL.Enable(EnableCap.PolygonOffsetFill);

            GL.CullFace(CullFaceMode.Front);

            _characterFleshShader.bind();
            _characterFleshShader.loadUniformInt("numberOfMaterials", renderEngine.textureGenerator.loadedMaterialTextures);
            _characterShadowShader.bind();
            _characterShadowShader.loadUniformInt("numberOfMaterials", renderEngine.textureGenerator.loadedMaterialTextures);

            GL.ActiveTexture(TextureUnit.Texture0);
            GL.BindTexture(TextureTarget.Texture2DArray, renderEngine.textureGenerator.megaAlbedoTextureArray);

            GL.ActiveTexture(TextureUnit.Texture3);
            GL.BindTexture(TextureTarget.Texture2DArray, renderEngine.textureGenerator.megaAlbedoModelTextureArray);
        }

        internal override void FinishShadow(RenderEngine renderEngine)
        {
            GL.Disable(EnableCap.PolygonOffsetFill);
            GL.Enable(EnableCap.CullFace);
            GL.CullFace(CullFaceMode.Back);

            GL.DisableVertexAttribArray(0);
            GL.DisableVertexAttribArray(4);
            GL.DisableVertexAttribArray(5);
            GL.DisableVertexAttribArray(6); // Bone Indices
            GL.DisableVertexAttribArray(7); // Bone Weights
        }

        internal override void PerformGeometryCommand(CharacterRenderCommand command, RenderEngine renderEngine)
        {
            glModel glmodel = command.model;

            GL.BindVertexArray(glmodel.getVAOID());
            GL.EnableVertexAttribArray(0); // Position
            GL.EnableVertexAttribArray(1); // Normal
            GL.EnableVertexAttribArray(2); // UV
            GL.EnableVertexAttribArray(3); // Tangent
            GL.EnableVertexAttribArray(4); // Material Index
            GL.EnableVertexAttribArray(5); // Model Texture Index
            GL.EnableVertexAttribArray(6); // Bone Indices (ivec4)
            GL.EnableVertexAttribArray(7); // Bone Weights (vec4)

            for (int i = 0; i < command.instances.Count; i++)
            {
                CharacterInstanceData instance = command.instances[i];
                Matrix4 transformationMatrix = instance.ModelMatrix;
                Matrix4 modelViewMatrix = transformationMatrix * renderEngine.context.viewMatrix;

                _characterShader.loadUniformMatrix4f("modelMatrix", transformationMatrix);
                _characterShader.loadUniformMatrix4f("normalModelViewMatrix", Matrix4.Transpose(Matrix4.Invert(modelViewMatrix)));
                _characterShader.loadUniformVector4f("hitFlash", instance.Flash);
                _characterShader.loadUniformVector4f("characterSight", instance.Sight);

                // Every bone a rig has (Rig.BoneCount, 43 with the legs of what walks on eight), into uniform mat4 u_Bones[48], which leaves room for a few more.
                _characterShader.loadUniformMatrix4fArray("u_Bones", instance.SkinningMatrices);

                // Only what's solid: a ghost's light comes after the lights (SpectralPass), which keeps it, as this frame's commands are gone by then.
                GL.DrawElements(PrimitiveType.Triangles, glmodel.SolidIndexCount, DrawElementsType.UnsignedInt, 0);
                if (glmodel.SpectralIndexCount > 0) _spectral.Add((glmodel, instance));
            }
        }

        /// <summary>
        /// What's light and not flesh, a ghost's skin round its bones (glModel.SpectralIndexCount): over the lit scene, before the reflections, so wet ground shows it, and under the fog. It
        /// adds and takes nothing away, so nothing it's in front of needs sorting, and it's tested against the depth of everything solid but writes none, so a wall hides it and its own bones
        /// are seen through it. Brightest where it's seen edge on and thinning to a wisp at its feet (Spectral.frag), and black with emission off, as anything that gives off light is.
        /// </summary>
        public void SpectralPass(RenderEngine renderEngine)
        {
            if (_spectral.Count == 0) return;

            Core.Engine.PerformanceMonitor.startGPUTask("Spectral");
            renderEngine.Msaa.SceneBuffer.bind();
            GL.Enable(EnableCap.DepthTest);
            GL.DepthFunc(DepthFunction.Lequal);
            GL.DepthMask(false);
            GL.Disable(EnableCap.StencilTest);
            GL.Enable(EnableCap.Blend);
            GL.BlendFunc(BlendingFactor.One, BlendingFactor.One);
            GL.BlendEquation(BlendEquationMode.FuncAdd);
            GL.Enable(EnableCap.CullFace);
            GL.CullFace(CullFaceMode.Back);

            _spectralShader.bind();
            var lighting = renderEngine.Lighting;
            _spectralShader.loadUniformFloat("emissionScale", lighting.Emission ? lighting.EmissionScale : 0f);
            foreach ((glModel model, CharacterInstanceData instance) in _spectral)
            {
                GL.BindVertexArray(model.getVAOID());
                GL.EnableVertexAttribArray(0);
                GL.EnableVertexAttribArray(1);
                GL.EnableVertexAttribArray(2);
                GL.EnableVertexAttribArray(6);
                GL.EnableVertexAttribArray(7);
                _spectralShader.loadUniformMatrix4f("modelMatrix", instance.ModelMatrix);
                _spectralShader.loadUniformMatrix4fArray("u_Bones", instance.SkinningMatrices);
                _spectralShader.loadUniformVector4f("hitFlash", instance.Flash);
                _spectralShader.loadUniformVector4f("characterSight", instance.Sight);
                GL.DrawElements(PrimitiveType.Triangles, model.SpectralIndexCount, DrawElementsType.UnsignedInt, (IntPtr)(model.SolidIndexCount * sizeof(int)));
            }
            _spectral.Clear();

            for (int i = 0; i < 8; i++) GL.DisableVertexAttribArray(i);
            GL.BindVertexArray(0);
            GL.Disable(EnableCap.CullFace);
            GL.Disable(EnableCap.Blend);
            GL.DepthMask(true);
            GL.DepthFunc(DepthFunction.Less);
            Core.Engine.PerformanceMonitor.finishGPUTask("Spectral");
        }

        internal override bool WantsShells(RenderEngine renderEngine)
        {
            foreach (CharacterRenderCommand command in geometryCommands)
            {
                if (renderEngine.Shells.Draws(command.model)) return true;
            }
            return false;
        }

        // Fur (SHELLS.md): every layer of a body in one call, posed as the body is. It's part of them as far as anything on the ground goes, which goes under them.
        internal override void ShellPass(RenderEngine renderEngine)
        {
            ShellRenderer shells = renderEngine.Shells;
            ShaderProgram program = shells.Character;
            shells.Begin(program, renderEngine);
            GL.ActiveTexture(TextureUnit.Texture0);
            GL.BindTexture(TextureTarget.Texture2DArray, renderEngine.textureGenerator.megaAlbedoTextureArray);
            ShellRenderer.BindWind(renderEngine, touch: false);

            GL.Enable(EnableCap.StencilTest);
            GL.StencilMask(AntiAliasing.MsaaRenderer.CharacterStencilBit);
            GL.StencilFunc(StencilFunction.Always, AntiAliasing.MsaaRenderer.CharacterStencilBit, AntiAliasing.MsaaRenderer.CharacterStencilBit);
            GL.StencilOp(StencilOp.Keep, StencilOp.Keep, StencilOp.Replace);

            Vector3 camera = renderEngine.context.viewPos;
            foreach (CharacterRenderCommand command in geometryCommands)
            {
                glModel model = command.model;
                if (!shells.Draws(model)) continue;
                Rendering.Shells.ShellStyle style = Rendering.Shells.ShellStyles.Of(model.Shell)!;

                GL.BindVertexArray(model.getVAOID());
                GL.EnableVertexAttribArray(0);
                GL.EnableVertexAttribArray(1);
                GL.EnableVertexAttribArray(2);
                GL.EnableVertexAttribArray(5);
                GL.EnableVertexAttribArray(6);
                GL.EnableVertexAttribArray(7);

                for (int i = 0; i < command.instances.Count; i++)
                {
                    CharacterInstanceData instance = command.instances[i];
                    float distance = (instance.ModelMatrix.ExtractTranslation() - camera).Length;
                    if (distance >= style.FadeTo) continue;
                    int layers = shells.LayersAt(style, distance, renderEngine);
                    shells.Use(program, model.Shell, layers);
                    program.loadUniformMatrix4f("modelMatrix", instance.ModelMatrix);
                    program.loadUniformMatrix4fArray("u_Bones", instance.SkinningMatrices);
                    program.loadUniformVector4f("hitFlash", instance.Flash);
                    program.loadUniformVector4f("characterSight", instance.Sight);
                    GL.DrawElementsInstanced(PrimitiveType.Triangles, model.ShellIndexCount, DrawElementsType.UnsignedInt, IntPtr.Zero, layers);
                    ShellRenderer.Count(model.Shell, 1, layers, (long)layers * model.ShellIndexCount / 3);
                }
            }

            GL.StencilMask(0xFF);
            GL.Disable(EnableCap.StencilTest);
            for (int i = 0; i < 8; i++) GL.DisableVertexAttribArray(i);
            GL.BindVertexArray(0);
        }

        internal override void PerformShadowCommand(CharacterRenderCommand command, Shadow shadow, RenderEngine renderEngine)
        {
            if (shadow.isCubeMap && shadow.cubemapFaceIndex >= 0)
            {
                shadow.shadowFrameBuffer.bindFace(TextureTarget.TextureCubeMapPositiveX + shadow.cubemapFaceIndex);
            }
            else
            {
                shadow.shadowFrameBuffer.bind();
            }
            ShaderProgram program = shadow.fleshOnly ? _characterFleshShader : _characterShadowShader;
            program.bind();
            for (int i = 0; i < command.instances.Count; i++)
            {
                GL.PolygonOffset(shadow.polygonOffsetModel, shadow.polygonOffsetModel * 10.1f);

                glModel glmodel = command.model;
                GL.BindVertexArray(glmodel.getVAOID());


                GL.EnableVertexAttribArray(0); // Position
                GL.EnableVertexAttribArray(4); // Material Index
                GL.EnableVertexAttribArray(5); // Model Texture Index
                GL.EnableVertexAttribArray(6); // Bone Indices
                GL.EnableVertexAttribArray(7); // Bone Weights


                CharacterInstanceData instance = command.instances[i];
                Matrix4 transformationMatrix = instance.ModelMatrix;

                program.loadUniformMatrix4f("modelViewProjectionMatrix", transformationMatrix * shadow.lightViewMatrix * shadow.shadowProjectionMatrix);
                program.loadUniformMatrix4fArray("u_Bones", instance.SkinningMatrices);

                // Light casts no shadow: only a ghost's bones do.
                GL.DrawElements(PrimitiveType.Triangles, glmodel.SolidIndexCount, DrawElementsType.UnsignedInt, 0);
            }
        }
    }
}