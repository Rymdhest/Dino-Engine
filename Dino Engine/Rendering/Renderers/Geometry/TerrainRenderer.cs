using OpenTK.Mathematics;
using OpenTK.Graphics.OpenGL;
using OpenTK.Windowing.Common;
using Dino_Engine.Modelling.Model;
using Dino_Engine.Core;
using Dino_Engine.Util;
using Dino_Engine.Modelling;
using System.Drawing;
using Dino_Engine.Modelling.Procedural;
using Dino_Engine.Textures;
using Dino_Engine.Util.Data_Structures.Grids;
using System.Runtime.InteropServices;

namespace Dino_Engine.Rendering.Renderers.Geometry
{
    public struct TerrainChunkRenderCommand : IRenderCommand
    {
        public Vector3 chunkPos;
        public Vector3 size;
        public float arrayID;
    }


    public class TerrainRenderer : CommandDrivenRenderer<TerrainChunkRenderCommand>
    {
        private ShaderProgram _terrainShader = new ShaderProgram("Terrain.vert", "Terrain.frag");
        private glModel baseChunkModel;
        private int normalHeightTextureArray;
        private IDAllocator<ushort> normalHeightTextureArrayAllocator = new();
        private readonly int MAX_TERRAIN_CHUNKS = 512;
        public static readonly int CHUNK_RESOLUTION = 32;

        private int instanceVBO;

        public TerrainRenderer()
        {
            _terrainShader.bind();
            _terrainShader.loadUniformInt("albedoMapTextureArray", 0);
            _terrainShader.loadUniformInt("normalMapTextureArray", 1);
            _terrainShader.loadUniformInt("materialMapTextureArray", 2);

            _terrainShader.loadUniformInt("albedoMapModelTextureArray", 3);
            _terrainShader.loadUniformInt("normalMapModelTextureArray", 4);
            _terrainShader.loadUniformInt("materialMapModelTextureArray", 5);

            _terrainShader.loadUniformInt("normalHeightTextureArray", 6);

            _terrainShader.unBind();

            
            var mesh = MeshGenerator.generatePlane(size : new Vector2(1f, 1f), resolution : new Vector2i(CHUNK_RESOLUTION-1), material : Material.BARK, centerX : false, centerY : false);
            //var mesh = MeshGenerator.generateBox(Material.ROCK);
            vIndex[] vindices = mesh.getAllIndicesArray();
            int[] indices = new int[vindices.Length];
            for (int i = 0; i < vindices.Length; i++)
            {
                indices[i] = vindices[i].index;
            }
            
            baseChunkModel = glLoader.loadToVAO(mesh.getAllPositionsArray(), indices, 3);

            instanceVBO = GL.GenBuffer();
            GL.BindVertexArray(baseChunkModel.getVAOID());
            GL.BindBuffer(BufferTarget.ArrayBuffer, instanceVBO);

            int stride = Marshal.SizeOf<TerrainChunkRenderCommand>();

            // chunkPos at location 3
            GL.EnableVertexAttribArray(3);
            GL.VertexAttribPointer(3, 3, VertexAttribPointerType.Float, false, stride, 0);
            GL.VertexAttribDivisor(3, 1);

            // chunkSize at location 4
            GL.EnableVertexAttribArray(4);
            GL.VertexAttribPointer(4, 3, VertexAttribPointerType.Float, false, stride, Vector3.SizeInBytes);
            GL.VertexAttribDivisor(4, 1);

            // heightMapID at location 5
            GL.EnableVertexAttribArray(5);
            GL.VertexAttribPointer(5, 1, VertexAttribPointerType.Float, false, stride, 2 * Vector3.SizeInBytes);
            GL.VertexAttribDivisor(5, 1);

            GL.BindVertexArray(0);


            normalHeightTextureArray = GL.GenTexture();
            GL.BindTexture(TextureTarget.Texture2DArray, normalHeightTextureArray);
            GL.TexStorage3D(TextureTarget3d.Texture2DArray, 1, SizedInternalFormat.Rgba16f, CHUNK_RESOLUTION, CHUNK_RESOLUTION, MAX_TERRAIN_CHUNKS);

            GL.TexParameter(TextureTarget.Texture2DArray, TextureParameterName.TextureMinFilter, (int) TextureMinFilter.Linear);
            GL.TexParameter(TextureTarget.Texture2DArray, TextureParameterName.TextureMagFilter, (int) TextureMagFilter.Linear);

            GL.TexParameter(TextureTarget.Texture2DArray, TextureParameterName.TextureWrapS, (int) TextureWrapMode.ClampToEdge);
            GL.TexParameter(TextureTarget.Texture2DArray, TextureParameterName.TextureWrapT, (int) TextureWrapMode.ClampToEdge);
            GL.BindTexture(TextureTarget.Texture2DArray, 0);

        }   

        public int GetNormalHeightTextureArray()
        {
            return normalHeightTextureArray;
        }

        public void freeChunk(int chunk)
        {
            normalHeightTextureArrayAllocator.Release((ushort)chunk);
        }

        public int insertDataToTextureArray(FloatGrid heightGrid, Vector3Grid normalGrid)
        {
            int dimensions = 4;
            int id = (int)normalHeightTextureArrayAllocator.Allocate();
            var resolution = heightGrid.Resolution;
            var pixels = new float[dimensions * resolution.X * resolution.Y];
            for (int y = 0; y < resolution.Y; y++)
            {
                for (int x = 0; x < resolution.X; x++)
                {
                    int i = y * resolution.X + x;
                    pixels[i * dimensions + 0] = normalGrid.Values[x, y].X;
                    pixels[i * dimensions + 1] = normalGrid.Values[x, y].Y;
                    pixels[i * dimensions + 2] = normalGrid.Values[x, y].Z;
                    pixels[i * dimensions + 3] = heightGrid.Values[x, y];
                }
            }
            int newTexture = GL.GenTexture();
            GL.BindTexture(TextureTarget.Texture2D, newTexture);
            GL.TexImage2D(TextureTarget.Texture2D, 0, PixelInternalFormat.Rgba16f, resolution.X, resolution.Y, 0, PixelFormat.Rgba, PixelType.Float, pixels);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)TextureMagFilter.Linear);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)TextureMinFilter.Linear);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapS, (int)TextureWrapMode.ClampToEdge);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapT, (int)TextureWrapMode.ClampToEdge);
            GL.BindTexture(TextureTarget.Texture2D, 0);


            GL.BindTexture(TextureTarget.Texture2DArray, normalHeightTextureArray);
            GL.CopyImageSubData(newTexture, ImageTarget.Texture2D, 0, 0, 0, 0, normalHeightTextureArray, ImageTarget.Texture2DArray, 0, 0, 0, id, resolution.X, resolution.Y, 1);

            //GL.GenerateMipmap(GenerateMipmapTarget.Texture2DArray);
            GL.BindTexture(TextureTarget.Texture2DArray, 0);

            GL.DeleteTexture(newTexture);


            return id;
        }

        internal override void Prepare(RenderEngine renderEngine)   
        {
            GL.Enable(EnableCap.DepthTest);
            GL.Enable(EnableCap.CullFace);
            GL.CullFace(CullFaceMode.Back);
            GL.Disable(EnableCap.Blend);
            _terrainShader.bind();
            
            _terrainShader.loadUniformFloat("parallaxDepth", 0.05f);
            _terrainShader.loadUniformFloat("parallaxLayers", 64);
            _terrainShader.loadUniformFloat("textureTileSize", 5.0f);
            
            _terrainShader.loadUniformBool("DEBUG_VIEW", false);
            _terrainShader.loadUniformFloat("textureMapOffset", (1.0f/(CHUNK_RESOLUTION)));
            _terrainShader.loadUniformInt("numberOfMaterials", renderEngine.textureGenerator.loadedMaterialTextures);

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

            GL.ActiveTexture(TextureUnit.Texture6);
            GL.BindTexture(TextureTarget.Texture2DArray, normalHeightTextureArray);

            _terrainShader.loadUniformVector3f("viewPos", renderEngine.context.viewPos);

            GL.BindBuffer(BufferTarget.ArrayBuffer, instanceVBO);
            GL.BufferData(BufferTarget.ArrayBuffer,
                commands.Count * Marshal.SizeOf<TerrainChunkRenderCommand>(),
                commands.ToArray(),
                BufferUsageHint.DynamicDraw);


            _terrainShader.loadUniformFloat("groundID", Engine.RenderEngine.textureGenerator.grass);
            _terrainShader.loadUniformFloat("rockID", Engine.RenderEngine.textureGenerator.rock);

            GL.BindVertexArray(baseChunkModel.getVAOID());
            GL.DisableVertexAttribArray(1);
            GL.DisableVertexAttribArray(2);
            GL.DisableVertexAttribArray(5);

            GL.EnableVertexAttribArray(0);
            GL.EnableVertexAttribArray(3);
            GL.EnableVertexAttribArray(4);
            GL.EnableVertexAttribArray(5);

            Matrix4 projectionViewMatrix = renderEngine.context.viewMatrix * renderEngine.context.projectionMatrix;
            _terrainShader.loadUniformMatrix4f("invViewMatrix", renderEngine.context.invViewMatrix);
            _terrainShader.loadUniformMatrix4f("projectionViewMatrix", projectionViewMatrix);
            //_terrainShader.loadUniformMatrix4f("normalModelViewMatrix", Matrix4.Transpose(Matrix4.Invert(MyMath.createTransformationMatrix(command.chunkPos, Quaternion.Identity, new Vector3(command.size.X)) * viewMatrix)));


            GL.BindVertexArray(baseChunkModel.getVAOID());
            GL.DrawElementsInstanced(
                PrimitiveType.Triangles,
                baseChunkModel.getVertexCount(),
                DrawElementsType.UnsignedInt,
                IntPtr.Zero,
                commands.Count
            );

        }
        internal override void Finish(RenderEngine renderEngine)
        {
            GL.DisableVertexAttribArray(0);
            GL.DisableVertexAttribArray(1);
            GL.DisableVertexAttribArray(2);
            GL.DisableVertexAttribArray(3);
            GL.DisableVertexAttribArray(4);
            GL.DisableVertexAttribArray(5);
            GL.BindVertexArray(0);
        }
        public override void CleanUp()
        {
            _terrainShader.cleanUp();
        }

        public override void PerformCommand(TerrainChunkRenderCommand command, RenderEngine renderEngine)
        {

            
        }
    }
}
