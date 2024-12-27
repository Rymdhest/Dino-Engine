using Dino_Engine.Core;
using Dino_Engine.Modelling;
using Dino_Engine.Modelling.Model;
using Dino_Engine.Modelling.Procedural;
using Dino_Engine.Modelling.Procedural.Nature;
using Dino_Engine.Modelling.Procedural.Urban;
using Dino_Engine.Rendering;
using Dino_Engine.Rendering.Renderers;
using Dino_Engine.Util;
using OpenTK.Graphics.OpenGL;
using OpenTK.Mathematics;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Mail;
using System.Reflection.Emit;
using System.Text;
using System.Threading.Tasks;
using static Dino_Engine.Textures.MaterialLayersManipulator;
using static Dino_Engine.Textures.ProceduralTextureRenderer;

namespace Dino_Engine.Textures
{
    public class TextureGenerator
    {
        public int megaAlbedoTextureArray;
        public int megaNormalTextureArray;
        public int megaMaterialTextureArray;

        public int megaAlbedoModelTextureArray;
        public int megaNormalModelTextureArray;
        public int megaMaterialModelTextureArray;

        public int loadedModelTextures = 0;
        public int loadedMaterialTextures = 0;

        public static readonly Vector2i TEXTURE_RESOLUTION = new Vector2i(1024, 1024)*1;


        public int flat;
        public int test;
        public int grain;
        public int sand;
        public int mud;
        public int moss;
        public int clay;
        public int flatGlow;
        public int sandDunes;
        public int metalFloor;
        public int gravel;
        public int grass;
        public int rock;
        public int wood;
        public int bark;
        public int barkBirch;
        public int brick;
        public int leather;
        public int cobble;
        public int crackedDesert;
        public int crackedLava; 
        public int snow;
        public int ice;
        public int metal;
        public int gold;
        public int copper;
        public int crystal;
        public int rustyMetal;

        public int leaf;
        public int leafBranch;
        public int treeBranch;


        public static  ProceduralTextureRenderer procTextGen = new ProceduralTextureRenderer();

        private ShaderProgram _textureNormalShader = new ShaderProgram("Simple.vert", "procedural/Texture_Normal_Generate.frag");

        public List<MaterialMapsTextures> preparedTextures = new List<MaterialMapsTextures>();

        public static MaterialLayersManipulator MaterialLayersCombiner = new MaterialLayersManipulator();

        public static MaterialLayerHandler MaterialLayerHandler = new MaterialLayerHandler();

        private TextureStudio textureStudio;

        public TextureGenerator()
        {
            _textureNormalShader.bind();
            _textureNormalShader.loadUniformInt("heightMap", 0);
            _textureNormalShader.unBind();

            MaterialLayer.procTextGen = procTextGen;
            MaterialLayer.MaterialLayersCombiner = MaterialLayersCombiner;

            textureStudio = new TextureStudio();
        }

        public void GenerateAllTextures()
        {
            
            grain = createGrainTexture();
            flat = createFlatTexture();
            flatGlow = createFlatGlowTexture();
            sandDunes = createSandDunesTexture();
            test = createCobbleTexture();
            bark = createBark();
            metalFloor = createMetalFloorTexture();
            brick = brickTexture();
            
            addAllPreparedTexturesToTexArray(true);

            Material leafMaterial = new Material(new Colour(100, 200, 90), flatGlow);

            Mesh leafMesh = MeshGenerator.generatePlane(new Vector2(0.15f, 1f), new Vector2i(50, 50), leafMaterial);

            for (int i = 0; i<leafMesh.meshVertices.Count; i++)
            {
                leafMesh.meshVertices[i].position.X *= 1f + MathF.Sin((leafMesh.meshVertices[i].position.Y+0.25f) * MathF.Tau *1.0f) * 0.99f;
                leafMesh.meshVertices[i].position.X += MathF.Sin(leafMesh.meshVertices[i].position.Y * MathF.Tau * 10f) * 0.005f;


                //leafMesh.meshVertices[i].position.Z = MathF.Sin(leafMesh.meshVertices[i].position.X * MathF.Tau * 15f) * 0.000005f;
            }



            leafMesh.rotate(new Vector3(0, 0f, 0f));
            preparedTextures.Add(textureStudio.GenerateTextureFromMesh(leafMesh, fullStretch: false));
            leaf = preparedTextures.Count - 1 + loadedMaterialTextures;
            addAllPreparedTexturesToTexArray(false);

            Mesh branchMesh = MeshGenerator.generatePlane(new Vector2(15f, 15f), new Vector2i(1, 1), new Material(new Colour(150, 161,87), leaf));
            branchMesh.rotate(new Vector3(0, 0f, MathF.PI/2f));


            var controlPoints = new List<Vector3>();
            int n = 10;
            float[] sinFBM = FBMmisc.sinFBM(5, 0.6f, n);
            float[] sinFBM2 = FBMmisc.sinFBM(5, 0.9f, n);
            float r = 0.6f;
            float h = 50f;
            for (int i = 0; i < n; i++)
            {
                float traversedRatio = i / (float)(n - 1);
                float angle = MathF.PI * i * 0.2f;
                float x = sinFBM[i] * r * traversedRatio;
                float z = sinFBM2[i] * r * traversedRatio;
                float y = traversedRatio * h;
                controlPoints.Add(new Vector3(x, y, z));
            }
            CardinalSpline3D spline = new CardinalSpline3D(controlPoints, 0.0f);

            Curve3D curve = spline.GenerateCurve(3);
            curve.LERPWidth(2f, 0.1f);
            Mesh mesh = MeshGenerator.generateTube(curve, 8, Material.BARK, textureRepeats: 1, flatStart: true);
            for (int i = 0; i<10; i++)
            {
                mesh += branchMesh.translated(new Vector3(-7.5f, i*5.0f, 0f));
                mesh += branchMesh.translated(new Vector3(7.5f, i*5.0f, 0f));
            }
            preparedTextures.Add(textureStudio.GenerateTextureFromMesh(mesh, fullStretch: false));
            leafBranch = preparedTextures.Count-1+ loadedMaterialTextures+loadedModelTextures;
            addAllPreparedTexturesToTexArray(false);


            Mesh treeBranchMesh = MeshGenerator.generatePlane(new Vector2(7f, 7f), new Vector2i(1, 1), new Material(new Colour(255, 255, 255), leafBranch));
            treeBranchMesh.rotate(new Vector3(0, 0f, -MathF.PI / 2f));
            Mesh mesh2 = new Mesh();
            for (int i = 0; i < 10; i++)
            {
                treeBranchMesh.rotate(new Vector3(0, 0f, -MathF.PI / 1f));
                mesh2 += treeBranchMesh.translated(new Vector3(-3.5f, i * 5.0f, 0f));
                treeBranchMesh.rotate(new Vector3(0, 0f, -MathF.PI / 1f));
                mesh2 += treeBranchMesh.translated(new Vector3(3.5f, i * 5.0f, 0f));
            }
            preparedTextures.Add(textureStudio.GenerateTextureFromMesh(mesh2, fullStretch: false));
            treeBranch = preparedTextures.Count - 1 + loadedMaterialTextures + loadedModelTextures;
            addAllPreparedTexturesToTexArray(false);
        }

        private FrameBuffer generateNormalFrameBuffer(FrameBuffer materialBuffer, float normalFlatness)
        {
            DrawBufferSettings normalAttachment = new DrawBufferSettings(FramebufferAttachment.ColorAttachment0);
            normalAttachment.formatInternal = PixelInternalFormat.Rgba;
            normalAttachment.pixelType = PixelType.UnsignedByte;
            normalAttachment.wrapMode = TextureWrapMode.Repeat;     
            normalAttachment.formatExternal = PixelFormat.Rgba;
            FrameBufferSettings normalBufferSettings = new FrameBufferSettings(materialBuffer.getResolution());
            normalBufferSettings.drawBuffers.Add(normalAttachment);
            FrameBuffer normalBuffer = new FrameBuffer(normalBufferSettings);

            _textureNormalShader.bind();
            normalBuffer.bind(); 
            _textureNormalShader.loadUniformFloat("normalFlatness", normalFlatness * (1f/TEXTURE_RESOLUTION.X));
            _textureNormalShader.loadUniformVector2f("texelSize", new Vector2(1f)/TEXTURE_RESOLUTION);
            GL.ActiveTexture(TextureUnit.Texture0);
            GL.BindTexture(TextureTarget.Texture2D, materialBuffer.GetAttachment(1));
            Engine.RenderEngine.ScreenQuadRenderer.Render();

            return normalBuffer;
        }


        private int FinishTexture(MaterialLayer layer, float normalFlatness = 100.0f, float parallaxDepth = 0.06f)
        {
            FrameBuffer normalBuffer = generateNormalFrameBuffer(layer.GetLastFrameBuffer(), normalFlatness);

            int albedoTexture = layer.GetLastFrameBuffer().exportAttachmentAsTexture(ReadBufferMode.ColorAttachment0);
            int materialTexture = layer.GetLastFrameBuffer().exportAttachmentAsTexture(ReadBufferMode.ColorAttachment1);
            int normalTexture = normalBuffer.exportAttachmentAsTexture(ReadBufferMode.ColorAttachment0);

            normalBuffer.cleanUp();

            preparedTextures.Add(new MaterialMapsTextures(albedoTexture, normalTexture, materialTexture));

            MaterialLayerHandler.cleanUp();

            return preparedTextures.Count-1;
        }

        private int loadTypeOfTextureToArray(int type, int oldArray, bool isMaterial)
        {
            int loadedTextures;
            if (isMaterial) loadedTextures = loadedMaterialTextures;
            else loadedTextures = loadedModelTextures;

            int textureArray = GL.GenTexture();
            GL.BindTexture(TextureTarget.Texture2DArray, textureArray);

            // Allocate storage for the texture array with the correct number of slices

            int maxDimension = Math.Max(TEXTURE_RESOLUTION.X, TEXTURE_RESOLUTION.Y);
            int mips = (int)Math.Floor(Math.Log(maxDimension, 2)) + 1;

            GL.TexStorage3D(TextureTarget3d.Texture2DArray, mips, SizedInternalFormat.Rgba8, TEXTURE_RESOLUTION.X, TEXTURE_RESOLUTION.Y, preparedTextures.Count+loadedTextures);


            if (loadedTextures > 0)
            {
                for (int i = 0; i < loadedTextures; i++)
                {
                    GL.CopyImageSubData(oldArray, ImageTarget.Texture2DArray, 0, 0, 0, i, textureArray, ImageTarget.Texture2DArray, 0, 0, 0, i, TEXTURE_RESOLUTION.X, TEXTURE_RESOLUTION.Y, 1);
                }
            }


            /*
            if (loadedTextures > 0)
            {
                GL.CopyImageSubData(oldArray, ImageTarget.Texture2DArray, 0, 0, 0, 0, textureArray, ImageTarget.Texture2DArray, 0, 0, 0, 0, TEXTURE_RESOLUTION.X, TEXTURE_RESOLUTION.Y, loadedTextures);
                GL.DeleteTexture(oldArray);
            }
            */
            Engine.CheckGLError("After TexStorage3D");

            for (int i = 0; i < preparedTextures.Count; i++)
            {
                //GL.BindTexture(TextureTarget.Texture2D, preparedTextures[i].textures[type]);
                Engine.CheckGLError("After BindTexture2D");
                //GL.CopyTexSubImage3D(TextureTarget.Texture2DArray, 0, 0, 0, i, 0, 0, textureResolution.X, textureResolution.Y);
                GL.CopyImageSubData(preparedTextures[i].textures[type], ImageTarget.Texture2D, 0, 0, 0, 0, textureArray, ImageTarget.Texture2DArray, 0, 0, 0, i+loadedTextures, TEXTURE_RESOLUTION.X, TEXTURE_RESOLUTION.Y, 1 );
                Engine.CheckGLError("After CopyTexSubImage3D " +i + " : " + loadedTextures);
            }
            GL.TexParameter(TextureTarget.Texture2DArray, TextureParameterName.TextureMinFilter, (int)TextureMinFilter.LinearMipmapLinear);
            GL.TexParameter(TextureTarget.Texture2DArray, TextureParameterName.TextureMagFilter, (int)TextureMagFilter.Linear);

            if (isMaterial)
            {
                GL.TexParameter(TextureTarget.Texture2DArray, TextureParameterName.TextureWrapS, (int)TextureWrapMode.Repeat);
                GL.TexParameter(TextureTarget.Texture2DArray, TextureParameterName.TextureWrapT, (int)TextureWrapMode.Repeat);
            } else
            {
                GL.TexParameter(TextureTarget.Texture2DArray, TextureParameterName.TextureWrapS, (int)TextureWrapMode.ClampToBorder);
                GL.TexParameter(TextureTarget.Texture2DArray, TextureParameterName.TextureWrapT, (int)TextureWrapMode.ClampToBorder);
            }


            Engine.CheckGLError("After TexParameter");
            GL.GenerateMipmap(GenerateMipmapTarget.Texture2DArray);


            // Unbind the texture array
            GL.BindTexture(TextureTarget.Texture2DArray, 0);

            return textureArray;
        }


        private void addAllPreparedTexturesToTexArray(bool isMaterial)
        {
            if (isMaterial)
            {
                megaAlbedoTextureArray = loadTypeOfTextureToArray(0, megaAlbedoTextureArray, isMaterial);
                megaNormalTextureArray = loadTypeOfTextureToArray(1, megaNormalTextureArray, isMaterial);
                megaMaterialTextureArray = loadTypeOfTextureToArray(2, megaMaterialTextureArray, isMaterial);

                loadedMaterialTextures += preparedTextures.Count;
            } else
            {
                megaAlbedoModelTextureArray = loadTypeOfTextureToArray(0, megaAlbedoModelTextureArray, isMaterial);
                megaNormalModelTextureArray = loadTypeOfTextureToArray(1, megaNormalModelTextureArray, isMaterial);
                megaMaterialModelTextureArray = loadTypeOfTextureToArray(2, megaMaterialModelTextureArray, isMaterial);

                loadedModelTextures += preparedTextures.Count;
            }



            foreach (MaterialMapsTextures materialTextures in preparedTextures)
            {
                materialTextures.CleanUp();
            }
            preparedTextures.Clear();
            MaterialLayerHandler.cleanUp();
        }

        private int createFlatGlowTexture()
        {
            return createGrainTexture();
        }

        private int createFlatTexture()
        {
            return createMetalFloorTexture();
        }
        private int createCrackedDesert()
        {
            var voronoi = procTextGen.VoronoiCracks(new Vector2(4f, 4f), jitter: 1.0f, warpSmudge: false, warp: 0.2f, warpScale: 0.2f, width: 0.04f, smudgePhase: 1.0f, smoothness: 0.1f);
            return FinishTexture(voronoi);
        }
        private int createCobbleTexture()
        {
            float stoneSize = 7;
            var stones = procTextGen.VoronoiCracks(new Vector2(stoneSize, stoneSize), jitter: 1.0f, width:0.55f, smoothness:0.5f).setMaterial(new Colour(204, 173, 106), new Vector3(0.45f, 0f, 0f));
            var stonesID = procTextGen.VoronoiCracks(new Vector2(stoneSize, stoneSize), jitter: 1.0f, width: 0.45f, smoothness: 0.5f, returnMode:ReturnMode.ID).setMaterial(new Colour(104, 133, 86), new Vector3(0.45f, 0f, 0f));

            var background = procTextGen.PerlinFBM(new Vector2(32f, 32f), octaves: 10, amplitudePerOctave: 0.5f, rigged: false).setMaterial(new Colour(40, 73, 30), new Vector3(0.75f, 0f, 0f));
            var roughLayer = procTextGen.PerlinFBM(new Vector2(32f, 32f), octaves: 10, amplitudePerOctave: 0.5f, rigged: false).setMaterial(new Colour(100, 103, 56), new Vector3(0.95f, 0f, 0f));
            var stoneTop = procTextGen.PerlinFBM(new Vector2(16f, 16f), octaves:1, rigged:true).setMaterial(new Colour(114, 103, 76), new Vector3(0.95f, 0f, 0f));

            var scratch = procTextGen.PerlinFBM(new Vector2(8f, 8f), octaves: 1, amplitudePerOctave: 0.5f, rigged: true).setMaterial(new Colour(120, 173, 110), new Vector3(0.75f, 0f, 0f)).invertHeight();

            var dirtMask = procTextGen.PerlinFBM(new Vector2(20f, 20f), octaves: 1, rigged: true).setMaterial(new Colour(120, 95, 55), new Vector3(0.85f, 0f, 0f)).addHeight(0.5f);
            MaterialLayersCombiner.combine(dirtMask, background, FilterMode.Greater, heightOperation: Operation.Nothing, materialOperation: Operation.Override, weight: -0.5f, smoothness: 0.1f);


            MaterialLayersCombiner.combine(stones, procTextGen.CreateFlatHeight(.25f), FilterMode.Everywhere, heightOperation: Operation.Power, materialOperation: Operation.Nothing, weight: 0.5f, smoothness: 1.0f);

            MaterialLayersCombiner.combine(stones, roughLayer, FilterMode.Everywhere, heightOperation: Operation.Add, materialOperation: Operation.Smoothstep, weight: 0.3f, smoothness: 0.5f);

            //MaterialLayersCombiner.combine(stonesID, stonesID, FilterMode.Everywhere, heightOperation: Operation.Hash, materialOperation: Operation.Nothing, weight: -0.2f, smoothness: 0.1f);

            MaterialLayersCombiner.combine(stones, stonesID, FilterMode.Everywhere, heightOperation: Operation.Nothing, materialOperation: Operation.Smoothstep, weight: 0.5f, smoothness: 1.0f);
            MaterialLayersCombiner.combine(stones, scratch, FilterMode.Everywhere, heightOperation: Operation.Subtract, materialOperation: Operation.Nothing, weight: 0.25f, smoothness: 1.0f);


            MaterialLayersCombiner.combine(stones, background.scaleHeight(0.4f), FilterMode.Greater, heightOperation: Operation.Override, materialOperation: Operation.Override, weight: -0.2f, smoothness: 0.1f);

            return FinishTexture(stones, normalFlatness:50.0f);
            MaterialLayersCombiner.combine(stones, dirtMask, FilterMode.Greater, heightOperation: Operation.Add, materialOperation: Operation.Smoothstep, weight: 0.5f, smoothness: 0.9f);

            MaterialLayersCombiner.combine(stones, stoneTop, FilterMode.Everywhere, heightOperation: Operation.Scale, materialOperation: Operation.Scale, weight: 0.15f, smoothness: 0.1f);

            background.setMaterial(new Colour(163, 152, 138), new Vector3(0.98f, 0f, 0f));
            MaterialLayersCombiner.combine(background, procTextGen.CreateFlatHeight(0.3f), FilterMode.Everywhere, heightOperation: Operation.Add, materialOperation: Operation.Nothing, weight: -0.2f);
            MaterialLayersCombiner.combine(stones, background, FilterMode.Greater, heightOperation: Operation.Smoothstep, materialOperation: Operation.Override, weight: 0.5f, smoothness: 0.1f);
            return FinishTexture(stones);
        }
        private int createBark()
        {
            var bark = procTextGen.PerlinFBM(new Vector2(20f, 5f), octaves: 10, amplitudePerOctave:0.6f);
            var noise = procTextGen.PerlinFBM(new Vector2(32f, 32f), octaves: 10, amplitudePerOctave: 0.6f);
            var barkCracks = procTextGen.VoronoiCracks(new Vector2(40f, 35f), width:0.015f, smoothness:0.02f, jitter:1f);
            var wavy = procTextGen.PerlinFBM(new Vector2(16, 2), octaves: 1, amplitudePerOctave: 0.6f, rigged:true);
            bark.setMaterial(new Colour(90, 73, 60), new Vector3(0.95f, 0f, 0f));
            noise.setMaterial(new Colour(32, 33, 23), new Vector3(0.95f, 0f, 0f));
            barkCracks.setMaterial(new Colour(255, 255, 255), new Vector3(0.95f, 0f, 0f));
            wavy.setMaterial(new Colour(50, 60, 45), new Vector3(0.35f, 0f, 0f));

            MaterialLayersCombiner.combine(barkCracks, noise.scaleHeight(0.2f), FilterMode.Everywhere, heightOperation: Operation.Add, materialOperation: Operation.Override, weight: -0.1f, smoothness: 0.1f);

            MaterialLayersCombiner.combine(bark, barkCracks, FilterMode.Lesser, heightOperation: Operation.Override, materialOperation: Operation.Override, weight: 0.1f, smoothness: 0.9f);

            MaterialLayersCombiner.combine(bark, wavy.invertHeight(), FilterMode.Everywhere, heightOperation: Operation.Scale, materialOperation: Operation.Smoothstep, weight: 0.7f, smoothness: 0.9f);
            MaterialLayersCombiner.combine(bark, noise.scaleHeight(2.0f), FilterMode.Everywhere, heightOperation: Operation.Add, materialOperation: Operation.Smoothstep, weight: 0.4f, smoothness: 0.8f);

            bark.addHeight(-0.05f);
            bark.scaleHeight(4.0f);
            return FinishTexture(bark, normalFlatness:100.0f);
        }

        private int brickTexture()
        {
            var bricks = procTextGen.Createbricks(new Vector2(7f, 16f), smoothness: 0.04f, spacing: 0.016f, returnMode: ReturnMode.Height);
            var bricksID = procTextGen.Createbricks(new Vector2(7f, 16f), smoothness: 0.04f, spacing: 0.016f, returnMode: ReturnMode.ID);
            bricks.setMaterial(new Colour(189, 110, 81), new Vector3(0.95f, 0f, 0f));
            var background = procTextGen.PerlinFBM(new Vector2(16f, 16f), octaves: 10, amplitudePerOctave: 0.8f, rigged: false);
            background.setMaterial(new Colour(163, 152, 138), new Vector3(0.98f, 0f, 0f));
            MaterialLayersCombiner.combine(background, procTextGen.CreateFlatHeight(0.0f), FilterMode.Everywhere, heightOperation: Operation.Add, materialOperation: Operation.Nothing, weight: -0.2f);

            MaterialLayersCombiner.combine(bricks, bricksID, FilterMode.Everywhere, heightOperation: Operation.Scale, materialOperation: Operation.Nothing, weight: 0.2f, smoothness: 0.5f);
            MaterialLayersCombiner.combine(bricks, background, FilterMode.Greater, heightOperation: Operation.Smoothstep, materialOperation: Operation.Override, weight: 0.5f, smoothness: 0.1f);

            MaterialLayersCombiner.combine(bricks, background, FilterMode.Everywhere, heightOperation: Operation.Add, materialOperation: Operation.Smoothstep, weight: 0.3f, smoothness: 0.9f);

            return FinishTexture(bricks);
        }

        private int createMetalFloorTexture()
        {
            MaterialLayer tileWweaves = procTextGen.TileWeave(new Vector2(16f, 16f), count: 3, smoothness: 0.9f, width: 0.5f);
            return FinishTexture(tileWweaves);
        }

        private int createGrainTexture()
        {
            MaterialLayer roughLayer = procTextGen.PerlinFBM(new Vector2(14f, 14f), octaves: 10, amplitudePerOctave: 0.8f);
            return FinishTexture(roughLayer);
        }
        private int createSandDunesTexture()
        {
            var sandDunes = procTextGen.PerlinFBM(new Vector2(8f, 28f), octaves: 1, amplitudePerOctave: 0.47f, rigged: true);
            return FinishTexture(sandDunes);
        }

        public void CleanUp()
        {
            GL.DeleteTexture(megaAlbedoTextureArray);
            GL.DeleteTexture(megaMaterialTextureArray);
            GL.DeleteTexture(megaNormalTextureArray);

            GL.DeleteTexture(megaAlbedoModelTextureArray);
            GL.DeleteTexture(megaNormalModelTextureArray);
            GL.DeleteTexture(megaMaterialModelTextureArray);

            loadedModelTextures = 0;
            loadedMaterialTextures = 0;
            _textureNormalShader.cleanUp();
            textureStudio.CleanUp();
        }
    }
}
