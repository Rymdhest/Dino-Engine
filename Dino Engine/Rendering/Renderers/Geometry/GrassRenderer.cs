using Dino_Engine.Core;
using Dino_Engine.ECS;
using Dino_Engine.ECS.Components;
using Dino_Engine.Modelling;
using Dino_Engine.Modelling.Model;
using Dino_Engine.Modelling.Procedural;
using Dino_Engine.Physics;
using Dino_Engine.Rendering.Renderers.Lighting;
using Dino_Engine.Textures;
using Dino_Engine.Util;
using Dino_Engine.Util.Data_Structures.Grids;
using OpenTK.Graphics.OpenGL;
using OpenTK.Mathematics;
using OpenTK.Windowing.Common;
using System.Drawing;
using static Dino_Engine.Textures.ProceduralTextureRenderer;

namespace Dino_Engine.Rendering.Renderers.Geometry
{

    public struct GrassChunkRenderData
    {
        public Vector2 chunkPos;
        public float size;
        public float arrayID;
    }

    public struct GrassRenderCommand : IRenderCommand
    {
        public GrassChunkRenderData[] chunks;
        public int LOD;

        public GrassRenderCommand(GrassChunkRenderData[] chunks, int lod)
        {
            this.chunks = chunks;
            this.LOD = lod;
        }
    }

    public class GrassRenderer : GeometryCommandDrivenRenderer<GrassRenderCommand>
    {

        private ShaderProgram _grassShader = new ShaderProgram("Grass.vert", "Grass.frag");
        private ShaderProgram _grassShadowShader = new ShaderProgram("Grass_Shadow.vert", "Grass_Shadow.frag");

        // Grass sways with the engine's wind (WindRenderer): bendMap is the vegetation state, windMap the wind field.
        private const int BendMapUnit = 8;
        private const int WindMapUnit = 9;

        private glModel grassBladeLOD0;
        private glModel grassBladeLOD1;
        private glModel grassBladeShadow;

        private int grassNoiseTexture;

        public static readonly int MAX_GRASS_CHUNKS = 1024;
        private int bladesPerAxis = 70;
        private float radiusBase = 0.02f;
        private float radiusTop = 0.01f;
        private float bladeHeight = 1.0f;

        private int bladesRenderedThisFrame = 0;

        private int chunkUBO;

        public GrassRenderer() : base("Grass")
        {
            _grassShader.bind();


            _grassShader.loadUniformInt("albedoMapTextureArray", 0);
            _grassShader.loadUniformInt("normalMapTextureArray", 1);
            _grassShader.loadUniformInt("materialMapTextureArray", 2);

            _grassShader.loadUniformInt("albedoMapModelTextureArray", 3);
            _grassShader.loadUniformInt("normalMapModelTextureArray", 4);
            _grassShader.loadUniformInt("materialMapModelTextureArray", 5);

            _grassShader.loadUniformInt("heightmaps", 6);
            _grassShader.loadUniformInt("heightmaps2", 7);
            _grassShader.loadUniformInt("bendMap", BendMapUnit);
            _grassShader.loadUniformInt("windMap", WindMapUnit);

            _grassShader.unBind();

            _grassShadowShader.bind();
            _grassShadowShader.loadUniformInt("heightmaps", 0);
            _grassShadowShader.loadUniformInt("heightmaps2", 1);
            _grassShadowShader.loadUniformInt("bendMap", BendMapUnit);
            _grassShadowShader.loadUniformInt("windMap", WindMapUnit);
            _grassShadowShader.unBind();

            grassBladeLOD0 = generateBladeModelLOD0();
            grassBladeLOD1 = generateBladeModelLOD1();
            generateBladeModelShadow();

            chunkUBO = GL.GenBuffer();
            int uboSize = MAX_GRASS_CHUNKS * (4 * 4); // vec4 = 16 bytes, no padding
            GL.BindBuffer(BufferTarget.UniformBuffer, chunkUBO);
            GL.BufferData(BufferTarget.UniformBuffer, uboSize, IntPtr.Zero, BufferUsageHint.DynamicDraw);
            GL.BindBufferBase(BufferRangeTarget.UniformBuffer, 5, chunkUBO);


            grassNoiseTexture = generateGrassNoiseTexture();
        }




        private int generateGrassNoiseTexture()
        {


            var small = TextureGenerator.procTextGen.Voronoi(new Vector2(126f, 126f), jitter: 1.0f, phase: 1.0f, returnMode: ReturnMode.Height);
            var big = TextureGenerator.procTextGen.PerlinFBM(new Vector2(8f, 8f), octaves: 4);

            small.scaleHeight(0.6f);
            small.addHeight(0.4f);

            //big.scaleHeight(1.0f);
            //big.addHeight(0.0f);

            var final = TextureGenerator.MaterialLayersCombiner.combine(small, big, MaterialLayersManipulator.FilterMode.Everywhere, MaterialLayersManipulator.Operation.Scale);


            int texture = final.GetLastFrameBuffer().exportAttachmentAsTexture(ReadBufferMode.ColorAttachment2);
            TextureGenerator.MaterialLayerHandler.cleanUp();

            return texture;


        }


        private glModel generateBladeModelLOD0()
        {
            if (grassBladeLOD0 != null) grassBladeLOD0.cleanUp();
            /*
            VertexMaterial grassMaterial = new VertexMaterial(TextureGenerator.foliage_olive); //Throwaway
            List<Vector2> bladeLayers = new List<Vector2>() {
                new Vector2(radiusBase, 0),
                new Vector2(MyMath.lerp(radiusBase, radiusTop, 0.33f), bladeHeight*0.33f),
                new Vector2(MyMath.lerp(radiusBase, radiusTop, 0.66f), bladeHeight*0.66f),
                new Vector2(radiusTop, bladeHeight*0.95f),
                new Vector2(0.0001f, bladeHeight)};

            bladeLayers.Clear();
            int detailHeight = 5;
            for (int i = 0; i<detailHeight; i++)
            {
                float ratio = ((float)i) / (detailHeight-1.0f);
                bladeLayers.Add(new Vector2(MyMath.lerp(radiusBase, radiusTop, ratio), bladeHeight*0.95f * ratio));
            }
            bladeLayers.Add(new Vector2(0.0001f, bladeHeight));

            Mesh bladeMesh = MeshGenerator.generateCylinder(bladeLayers, 4, grassMaterial);
            bladeMesh.scale(new Vector3(1.0f, 1f, 0.25f));

            for (int i = 0; i < bladeMesh.meshVertices.Count; i++)
            {
                bladeMesh.meshVertices[i].position.Z += MathF.Abs( bladeMesh.meshVertices[i].position.X) * 0.5f;
            }

            //bladeMesh.makeFlat(true, false);
            return glLoader.loadToVAO(bladeMesh);
            */
            Mesh bladeMesh = MeshGenerator.generatePlane(new Vector2(2f, bladeHeight), new Vector2i(2, 4), new VertexMaterial(TextureGenerator.foliage_olive), centerX: true, centerY: false);
            bladeMesh.rotate(new Vector3(-MathF.PI / 2f, 0f, 0f));

            for (int i = 0; i < bladeMesh.meshVertices.Count; i++)
            {
                float factor = bladeMesh.meshVertices[i].position.Y / bladeHeight;
                factor = MathF.Pow(factor, 2.0f);
                bladeMesh.meshVertices[i].position.X *= MyMath.lerp(radiusBase, radiusTop, factor);
                bladeMesh.meshVertices[i].position.Z += MathF.Abs(bladeMesh.meshVertices[i].position.X) * 0.5f;
            }

            return glLoader.loadToVAO(bladeMesh);
        }
        
        private void generateBladeModelShadow()
        {
            if (grassBladeShadow != null) grassBladeShadow.cleanUp();


            float[] positions = {
                -radiusBase, 0, 0,
                radiusBase, 0, 0,
                -radiusTop, bladeHeight, 0,
                radiusTop, bladeHeight, 0
            };

            float roundness = 0.5f;
            Vector3 NL = Vector3.Normalize(new Vector3(-roundness, 0, 1));
            Vector3 NR = Vector3.Normalize(new Vector3(roundness, 0, 1));

            float[] normals = {
                NL.X, NL.Y, NL.Z,
                NR.X, NR.Y, NR.Z,
                NL.X, NL.Y, NL.Z,
                NR.X, NR.Y, NR.Z,
            };

            int[] indices = {
                0, 1, 2,
                1, 3, 2
            };

            grassBladeShadow = glLoader.loadToVAO(positions, normals, indices);
        }


        private glModel generateBladeModelLOD1()
        {
            if (grassBladeLOD1 != null) grassBladeLOD1.cleanUp();


            float[] positions = {
                -radiusBase, 0, 0,
                0, 0, 0,
                radiusBase, 0, 0,
                -radiusTop, bladeHeight, 0,
                radiusTop, bladeHeight, 0
            };

            float roundness = 0.2f;
            float normY = -0.5f;
            Vector3 NL = Vector3.Normalize(new Vector3(-roundness, normY, 1));
            Vector3 NR = Vector3.Normalize(new Vector3(roundness, normY, 1));
            Vector3 N = Vector3.Normalize(new Vector3(0, normY, 1));

            float[] normals = {
                NL.X, NL.Y, NL.Z,
                N.X, N.Y, N.Z,
                NR.X, NR.Y, NR.Z,
                NL.X, NL.Y, NL.Z,
                NR.X, NR.Y, NR.Z,
            };

            int[] indices = {
                0, 1, 3,
                1, 4, 3,
                1, 2, 4
            };

            return glLoader.loadToVAO (positions, normals, indices);
        }

        public override void Update()
        {
            bladesPerAxis = 60;

            bladeHeight =1.4f;
            radiusBase = 0.003f;
            radiusTop = radiusBase * 0.2f;
        }

        public override void CleanUp()
        {
            GL.DeleteTexture(grassNoiseTexture);
            _grassShader.cleanUp();
            GL.DeleteBuffer(chunkUBO);
        }

        public override void OnResize(ResizeEventArgs eventArgs)
        {
        }

        internal override void PrepareGeometry(RenderEngine renderEngine)
        {
            GL.Enable(EnableCap.DepthTest);

            //GL.Enable(EnableCap.CullFace);
            GL.Disable(EnableCap.CullFace);
            GL.CullFace(CullFaceMode.Back);
            GL.Disable(EnableCap.Blend);


            generateBladeModelLOD0();
            generateBladeModelLOD1();
            generateBladeModelShadow();

            //StepSimulation(renderEngine.ScreenQuadRenderer);
            renderEngine.GBuffer.bind();

            _grassShader.bind();

            GL.ActiveTexture(TextureUnit.Texture6);
            GL.BindTexture(TextureTarget.Texture2DArray, Engine.RenderEngine._terrainRenderer.GetNormalRoadHeightTextureArray());

            GL.ActiveTexture(TextureUnit.Texture7);
            GL.BindTexture(TextureTarget.Texture2DArray, Engine.RenderEngine._terrainRenderer.GetGrassTextureArray());

            BindWindTextures(renderEngine);

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

            _grassShader.loadUniformInt("numberOfMaterials", renderEngine.textureGenerator.loadedMaterialTextures);
            Material grassMaterial = Material.FOLIAGE_PINE;
            Material grassMaterialDead = Material.GROUND_SOIL;
            _grassShader.loadUniformInt("textureIndex", TextureGenerator.foliage_pine);
            _grassShader.loadUniformFloat("groundNormalStrength", 0.1f);
            _grassShader.loadUniformFloat("groundNormalStrengthFlat", 0.2f);
            _grassShader.loadUniformFloat("colourError", 0.1f);
            _grassShader.loadUniformFloat("SSS", grassMaterial.subSurfaceTransparancy*0.6f);
            _grassShader.loadUniformFloat("fakeAmbientOcclusionStrength", 0.1f);
            _grassShader.loadUniformFloat("fakeColorAmbientOcclusionStrength", 0.6f);
            _grassShader.loadUniformVector4f("grassMaterial", new Vector4(grassMaterial.roughness, grassMaterial.emission, grassMaterial.metalic, 0f));
            _grassShader.loadUniformVector3f("baseColorAlive", grassMaterial.Colour.ToVector3());
            //_grassShader.loadUniformVector3f("baseColorAlive", new Colour(20, 50, 15).ToVector3());
            _grassShader.loadUniformVector3f("baseColorDead", grassMaterialDead.Colour.ToVector3());
            //_grassShader.loadUniformVector3f("baseColor", new Colour(30, 11, 8).ToVector3());

        }

        private static void BindWindTextures(RenderEngine renderEngine)
        {
            GL.ActiveTexture(TextureUnit.Texture0 + BendMapUnit);
            GL.BindTexture(TextureTarget.Texture2D, renderEngine._windRenderer.VegetationBendTexture);
            GL.ActiveTexture(TextureUnit.Texture0 + WindMapUnit);
            GL.BindTexture(TextureTarget.Texture2D, renderEngine._windRenderer.WindMapTexture);
        }

        internal override void FinishGeometry(RenderEngine renderEngine)
        {
            //Console.WriteLine("Grass blades rendered this frame: "+bladesRenderedThisFrame);
            bladesRenderedThisFrame = 0;
        }

        internal override void PrepareShadow(RenderEngine renderEngine)
        {
            GL.Enable(EnableCap.DepthTest);

            GL.Enable(EnableCap.PolygonOffsetFill);
            GL.Enable(EnableCap.CullFace);
            GL.CullFace(CullFaceMode.Back);
            GL.Disable(EnableCap.Blend);



            GL.ActiveTexture(TextureUnit.Texture0);
            GL.BindTexture(TextureTarget.Texture2DArray, Engine.RenderEngine._terrainRenderer.GetNormalRoadHeightTextureArray());

            GL.ActiveTexture(TextureUnit.Texture1);
            GL.BindTexture(TextureTarget.Texture2DArray, Engine.RenderEngine._terrainRenderer.GetGrassTextureArray());

            BindWindTextures(renderEngine);

            _grassShadowShader.bind();
            ShaderProgram shader = _grassShadowShader;
            for (int i = 0; i <2; i++)
            {
                shader.loadUniformFloat("bladeHeight", bladeHeight);
                shader.loadUniformFloat("bendyness", 0.08f);
                shader.loadUniformFloat("heightError", 0.35f);
                shader.loadUniformFloat("radiusError", 0.35f);
                shader.loadUniformFloat("cutOffThreshold", 0.1f);
                shader.loadUniformFloat("cutOffRange", 0.7f);
                shader.loadUniformFloat("steepnessCutoffStrength", 4.0f);
                shader.loadUniformFloat("textureMapOffset", 1f / TerrainRenderer.CHUNK_RESOLUTION);

                shader = _grassShader;
                shader.bind();
            }

            _grassShadowShader.bind();
        }

        internal override void FinishShadow(RenderEngine renderEngine)
        {

            GL.Disable(EnableCap.PolygonOffsetFill);
        }

        internal override void PerformGeometryCommand(GrassRenderCommand command, RenderEngine renderEngine)
        {
            int bladesPerChunk = (int)Math.Pow(bladesPerAxis, 2);
            _grassShader.loadUniformInt("bladesPerChunk", bladesPerChunk);
            _grassShader.loadUniformInt("bladesPerAxis", bladesPerAxis);

            Vector4[] chunkData = new Vector4[MAX_GRASS_CHUNKS];
            for (int i = 0; i < command.chunks.Length; i++)
            {
                var chunkCommand = command.chunks[i];
                chunkData[i] = new Vector4(chunkCommand.chunkPos.X, chunkCommand.chunkPos.Y, chunkCommand.size, chunkCommand.arrayID);
            }
            GL.BindBuffer(BufferTarget.UniformBuffer, chunkUBO);
            GL.BufferSubData(BufferTarget.UniformBuffer, IntPtr.Zero, Vector4.SizeInBytes * MAX_GRASS_CHUNKS, chunkData);

            glModel grassBlade = grassBladeLOD0;
            if (command.LOD == 1)
            {
                _grassShader.loadUniformBool("isBillboard", true);
                GL.Enable(EnableCap.CullFace);
                grassBlade = grassBladeLOD1;
            } else
            {
                _grassShader.loadUniformBool("isBillboard", false);
                GL.Disable(EnableCap.CullFace);
            }

            GL.BindVertexArray(grassBlade.getVAOID());
            GL.EnableVertexAttribArray(0);
            GL.EnableVertexAttribArray(1);
            GL.EnableVertexAttribArray(2);
            GL.EnableVertexAttribArray(3);
            GL.EnableVertexAttribArray(4);
            GL.EnableVertexAttribArray(5);

            GL.DrawElementsInstanced(PrimitiveType.Triangles, grassBlade.getVertexCount(), DrawElementsType.UnsignedInt, IntPtr.Zero, bladesPerChunk * command.chunks.Length);

            bladesRenderedThisFrame += bladesPerChunk * command.chunks.Length;

        }


        internal override void PerformShadowCommand(GrassRenderCommand command, Shadow shadow, RenderEngine renderEngine)
        {
            
            GL.PolygonOffset(shadow.polygonOffsetModel, shadow.polygonOffsetModel * 10.1f);
            GL.PolygonOffset(0.01f, 0.01f);
            
            if (shadow.isCubeMap && shadow.cubemapFaceIndex >= 0)
            {
                shadow.shadowFrameBuffer.bindFace(TextureTarget.TextureCubeMapPositiveX + shadow.cubemapFaceIndex);

            }
            else
            {
                shadow.shadowFrameBuffer.bind();
            }

            int bladesPerChunk = (int)Math.Pow(bladesPerAxis, 2);
            _grassShadowShader.loadUniformInt("bladesPerChunk", bladesPerChunk);
            _grassShadowShader.loadUniformInt("bladesPerAxis", bladesPerAxis);
            _grassShadowShader.loadUniformMatrix4f("shadowViewProjectionMatrix", shadow.lightViewMatrix*shadow.shadowProjectionMatrix);

            //  KINDA BAD TO INVERT HE VIEW MATRIX JUST TO GET THE POSITION....
            _grassShadowShader.loadUniformVector3f("lightPosWorld", shadow.lightViewMatrix.Inverted().ExtractTranslation());
            Vector4[] chunkData = new Vector4[MAX_GRASS_CHUNKS];
            for (int i = 0; i < command.chunks.Length; i++)
            {
                if (i >= MAX_GRASS_CHUNKS - 1) {
                    Console.WriteLine("Warning: More chunks in command than MAX_GRASS_CHUNKS. Some chunks will not be rendered.");
                    continue;
                }
                var chunkCommand = command.chunks[i];
                chunkData[i] = new Vector4(chunkCommand.chunkPos.X, chunkCommand.chunkPos.Y, chunkCommand.size, chunkCommand.arrayID);
            }

            GL.BindBuffer(BufferTarget.UniformBuffer, chunkUBO);
            GL.BufferSubData(BufferTarget.UniformBuffer, IntPtr.Zero, Vector4.SizeInBytes * MAX_GRASS_CHUNKS, chunkData);

            glModel grassBlade = grassBladeShadow;

            GL.BindVertexArray(grassBlade.getVAOID());
            GL.EnableVertexAttribArray(0);
            GL.DisableVertexAttribArray(1);
            GL.DisableVertexAttribArray(3);
            GL.DisableVertexAttribArray(4);
            GL.DisableVertexAttribArray(5);

            GL.DrawElementsInstanced(PrimitiveType.Triangles, grassBlade.getVertexCount(), DrawElementsType.UnsignedInt, IntPtr.Zero, bladesPerChunk * command.chunks.Length);
            
        }
    }
}
