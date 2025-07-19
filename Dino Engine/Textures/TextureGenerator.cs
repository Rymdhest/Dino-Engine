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

        public static Mesh TEST_BRANCH_MESH;
        public static Mesh TEST_TREE_BRANCh_MESH;

        public int megaAlbedoModelTextureArray;
        public int megaNormalModelTextureArray;
        public int megaMaterialModelTextureArray;

        public int loadedModelTextures = 0;
        public int loadedMaterialTextures = 0;

        public static readonly Vector2i TEXTURE_RESOLUTION = new Vector2i(1024, 1024)/1;


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
        public int mirror;

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
            grass = createGrassTexture();
            flat = createFlatTexture();
            flatGlow = createFlatGlowTexture();
            sandDunes = createSandDunesTexture();
            cobble = createCobbleTexture();
            bark = createBark();
            metalFloor = createMetalFloorTexture();
            brick = brickTexture();
            crackedLava = createCrackedLAva();
            rock = createRock();
            mirror = createMirrorTexture();

            addAllPreparedTexturesToTexArray(true);
           
           
           preparedTextures.Add(textureStudio.GenerateTextureFromMesh(TreeGenerator.GenerateLeaf(), fullStretch: true));
           
          leaf = preparedTextures.Count - 1 + loadedMaterialTextures;
          addAllPreparedTexturesToTexArray(false);


            float leafSize = 10f;
        Mesh branchMesh = MeshGenerator.generatePlane(new Vector2(leafSize), new Vector2i(1, 1), new Material(new Colour(150, 161,87), leaf));
            branchMesh.rotate(new Vector3(MathF.PI / 2f, 0, 0f));
            branchMesh.rotate(new Vector3(0, 0f, MathF.PI/2f));
        branchMesh.translate(new Vector3(-leafSize / 2f, 0f, 0f));
        branchMesh += branchMesh.translated(new Vector3(leafSize, 0f, 0f));
        var controlPoints = new List<Vector3>();
        int n = 10;
        float[] sinFBM = FBMmisc.sinFBM(5, 0.6f, n);
        float[] sinFBM2 = FBMmisc.sinFBM(5, 0.9f, n);
        float r =0.8f;
        float h = 50f;
        for (int i = 0; i < n; i++)
        {
            float traversedRatio = i / (float)(n - 1);
            float angle = MathF.PI * i * 0.4f;
            float x = sinFBM[i] * r * traversedRatio;
            float z = sinFBM2[i] * r * traversedRatio * 0f;
            float y = traversedRatio * h;
            controlPoints.Add(new Vector3(x, y, z));
        }
        CardinalSpline3D spline = new CardinalSpline3D(controlPoints, 0.0f);

        Curve3D curve = spline.GenerateCurve(3);
        curve.LERPWidth(1f, 0.1f);
        Mesh mesh = MeshGenerator.generateCurvedTube(curve, 8, Material.BARK, textureRepeats: 1, flatStart: true);

        int leavesPerSide = 5;
        for (int i = 0; i< leavesPerSide; i++)
        {
            float t = 0.2f + 0.75f * (float)i / (leavesPerSide - 1);
            CurvePoint curvePoint = curve.getPointAt(t);
            var newBranch = branchMesh.scaled(new Vector3(1.1f - t * 0.56f));
            Vector3 col = MyMath.rng3D(0.1f);
            newBranch.setColour(new Colour(new Vector3(1f)-col));
            //newBranch.rotate(new Vector3(0.9f - t * 0.5f, 0f, 0f));
            //newBranch.translate(new Vector3(curvePoint.width / 2f, 0f, 0f));
            //newBranch.translate(new Vector3(0f, 0f, -curvePoint.width / 2f));
            //newBranch.rotate(new Vector3(0f, i * 1.14f, 0f));
            newBranch.rotate(curvePoint.rotation);
            newBranch.translate(curvePoint.pos);
            mesh += newBranch;
        }
            TEST_BRANCH_MESH = mesh;
        preparedTextures.Add(textureStudio.GenerateTextureFromMesh(mesh, fullStretch: false));
        leafBranch = preparedTextures.Count-1+ loadedMaterialTextures+loadedModelTextures;
        addAllPreparedTexturesToTexArray(false);


        Mesh treeBranchMesh = MeshGenerator.generatePlane(new Vector2(10f, 10f), new Vector2i(1, 1), new Material(new Colour(255, 255, 255), leafBranch));
            treeBranchMesh.rotate(new Vector3(MathF.PI / 2f, 0, 0f));
            treeBranchMesh.rotate(new Vector3(0, 0f, -MathF.PI / 2.0f));
            treeBranchMesh.translate(new Vector3(-10f / 2f, 0f, 0f));
        treeBranchMesh += treeBranchMesh.rotated(new Vector3(0, 0f, -MathF.PI / 1f));
        Mesh mesh2 = MeshGenerator.generateCurvedTube(curve, 8, Material.BARK, textureRepeats: 1, flatStart: true);

        int branchesPerSide = 13;
        for (int i = 0; i < branchesPerSide; i++)
        {
            float t = 0.15f + 0.83f * (float)i / (branchesPerSide - 1);
            CurvePoint curvePoint = curve.getPointAt(t);
            var newBranch = treeBranchMesh.scaled(new Vector3(1.5f - t * 1.06f));
            Vector3 col = MyMath.rng3D(0.1f);
            newBranch.setColour(new Colour(new Vector3(1f) - col));
            //newBranch.rotate(new Vector3(0,0,0.9f - t * 0.5f));
            //newBranch.translate(new Vector3(curvePoint.width / 2f, 0f, 0f));
            //newBranch.translate(new Vector3(0f, 0f, -curvePoint.width / 2f));
            //newBranch.rotate(new Vector3(i * 0.14f,0f, 0f));
            newBranch.rotate(curvePoint.rotation);
            newBranch.translate(curvePoint.pos);
            mesh2 += newBranch;
        }
        TEST_TREE_BRANCh_MESH = mesh2;
        preparedTextures.Add(textureStudio.GenerateTextureFromMesh(mesh2, fullStretch: false));
        treeBranch = preparedTextures.Count - 1 + loadedMaterialTextures + loadedModelTextures;
        addAllPreparedTexturesToTexArray(false);

        
        }

        private FrameBuffer generateNormalFrameBuffer(FrameBuffer materialBuffer, float normalFlatness)
        {
            DrawBufferSettings normalAttachment = new DrawBufferSettings(FramebufferAttachment.ColorAttachment0);
            normalAttachment.formatInternal = PixelInternalFormat.Rgba8;
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


        private int FinishTexture(MaterialLayer layer, float normalFlatness = 300.0f, float parallaxDepth = 0.06f)
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
            //if (type == 2) mips = 1;
            //if (type == 1) mips = 1;
            //if (type == 0) mips = 1;
            GL.TexStorage3D(TextureTarget3d.Texture2DArray, mips, SizedInternalFormat.Rgba8, TEXTURE_RESOLUTION.X, TEXTURE_RESOLUTION.Y, preparedTextures.Count+loadedTextures);

            if (loadedTextures > 0)
            {
                GL.CopyImageSubData(oldArray, ImageTarget.Texture2DArray, 0, 0, 0, 0, textureArray, ImageTarget.Texture2DArray, 0, 0, 0, 0, TEXTURE_RESOLUTION.X, TEXTURE_RESOLUTION.Y, loadedTextures);
                GL.DeleteTexture(oldArray);
            }
            
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
            return FinishTexture(procTextGen.CreateMaterial(new Colour(255, 255, 255), new Vector3(1f, 0.5f, 0f)));
        }
        private int createMirrorTexture()
        {
            MaterialLayer roughLayer = procTextGen.PerlinFBM(new Vector2(1f, 1f), octaves: 1, amplitudePerOctave: 0.8f);
            roughLayer.scaleHeight(0.0f);
            roughLayer.setMaterial(new Colour(255, 255, 255), new Vector3(0.4f, 0f, 0.6f));
            return FinishTexture(roughLayer);
        }

        private int createFlatTexture()
        {
            return FinishTexture(procTextGen.CreateMaterial(new Colour(255, 255, 255), new Vector3(0.5f, 0.0f, 0.0f)));
        }
        private int createCrackedDesert()
        {
            var voronoi = procTextGen.Voronoi(new Vector2(4f, 4f), jitter: 1.0f);
            return FinishTexture(voronoi);
        }

        private int createRock()
        {
            var levelsBig = procTextGen.Voronoi(new Vector2(117f, 117f), jitter: 1.0f, returnMode:ReturnMode.ID);
            levelsBig.setMaterial(new Colour(75, 55, 55), new Vector3(0.99f, 0f, 0f));
            var levelsSmall = procTextGen.Voronoi(new Vector2(24f, 24f), jitter: 1.0f, returnMode: ReturnMode.ID);
            var combined = MaterialLayersCombiner.combine(levelsBig, levelsSmall, FilterMode.Everywhere, materialOperation: Operation.Nothing, heightOperation:Operation.Add, weight:0.7f);

            var cellular = procTextGen.Cellular(new Vector2(117f, 117f), jitter:1.0f, metric:Metric.SquaredEuclidean).invertHeight();
            cellular.setMaterial(new Colour(55, 85, 25), new Vector3(0.4f, 0f, 0f));

            var combined2 = MaterialLayersCombiner.combine(combined, cellular, FilterMode.Everywhere, materialOperation: Operation.Mix, heightOperation: Operation.Add, weight: 0.2f);

            var cracks = procTextGen.VoronoiCracks(new Vector2(19f, 19f), jitter: 1.0f, width: 0.055f, smoothness: 0.5f).setMaterial(new Colour(120, 117, 116), new Vector3(0.45f, 0f, 0f));

            MaterialLayersCombiner.combine(combined2, cracks, FilterMode.Everywhere, materialOperation: Operation.Nothing, heightOperation: Operation.Scale, weight: 0.2f);

            var noise = procTextGen.PerlinFBM(new Vector2(44f, 44f), octaves: 10, amplitudePerOctave: 0.6f);

            MaterialLayersCombiner.combine(combined2, noise, FilterMode.Everywhere, materialOperation: Operation.Nothing, heightOperation: Operation.Add, weight: 0.7f);

            return FinishTexture(combined2, normalFlatness:250.0f);
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

        private int createCrackedLAva()
        {
            var lava = procTextGen.PerlinFBM(new Vector2(14f, 14f), octaves: 10, amplitudePerOctave: 0.9f);
            lava.setMaterial(new Colour(0, 0, 0), new Vector3(0.95f, 0f, 0f));
            lava.mix(procTextGen.CreateMaterial(new Colour(255, 35, 13), new Vector3(1f, 0.18f, 0f), height: 1.0f), FilterMode.Everywhere, Operation.Mix);

            var cracks = procTextGen.VoronoiCracks(new Vector2(14f, 14f), width: 0.06f, smoothness: 0.5f, jitter: 1f);

            var cracks2 = procTextGen.VoronoiCracks(new Vector2(58f, 58f), width: 0.08f, smoothness: 0.5f, jitter: 1f);
            cracks2.mix(procTextGen.PerlinFBM(new Vector2(6f, 6f), octaves: 1, amplitudePerOctave: 0.9f), FilterMode.Greater, Operation.Override, weight: 0.9f);
            cracks2.setMaterial(new Colour(10, 10, 10), new Vector3(0.55f, 0f, 0f));

            cracks.setMaterial(new Colour(60, 50, 50), new Vector3(0.55f, 0f, 0f));
            var noise = procTextGen.PerlinFBM(new Vector2(20f, 20f), octaves: 8, amplitudePerOctave: 0.6f, rigged:false);
            //noise.invertHeight();
            cracks.mix(cracks2, FilterMode.Everywhere, Operation.Scale,  weight:0.35f);

            MaterialLayersCombiner.combine(cracks, noise.scaleHeight(1.0f), FilterMode.Everywhere, heightOperation: Operation.Scale, materialOperation: Operation.Nothing, weight: 0.5f, smoothness: 0.8f);

            lava.mix(procTextGen.CreateMaterial(new Colour(220, 6, 2), new Vector3(1f, 0.4f, 0f), height: 1.0f), FilterMode.Everywhere, Operation.Mix);

            var crackedLava = MaterialLayersCombiner.combine(cracks, lava.scaleHeight(0.14f), FilterMode.Greater, heightOperation: Operation.Override, materialOperation: Operation.Override, weight: 0.5f, smoothness: 0.5f);

            return FinishTexture(crackedLava, normalFlatness: 100.0f);
        }

        private int createBark()
        {
            var bark = procTextGen.PerlinFBM(new Vector2(20f, 5f), octaves: 10, amplitudePerOctave: 0.6f);
            var noise = procTextGen.PerlinFBM(new Vector2(32f, 32f), octaves: 10, amplitudePerOctave: 0.6f);
            var barkCracks = procTextGen.VoronoiCracks(new Vector2(15f, 15f), width: 0.025f, smoothness: 0.02f, jitter: 1f);
            var wavy = procTextGen.PerlinFBM(new Vector2(1, 16), octaves: 1, amplitudePerOctave: 0.6f, rigged: true);
            bark.setMaterial(new Colour(140, 60, 25), new Vector3(0.95f, 0.0f, 0f));
            noise.setMaterial(new Colour(30, 20, 5), new Vector3(0.55f, 0f, 0f));
            barkCracks.setMaterial(new Colour(5, 5, 5), new Vector3(0.95f, 0f, 0f));
            wavy.setMaterial(new Colour(10, 7, 9), new Vector3(0.35f, 0f, 0f));

            MaterialLayersCombiner.combine(barkCracks, noise.scaleHeight(0.2f), FilterMode.Everywhere, heightOperation: Operation.Add, materialOperation: Operation.Override, weight: -0.1f, smoothness: 0.1f);

            MaterialLayersCombiner.combine(bark, barkCracks, FilterMode.Lesser, heightOperation: Operation.Override, materialOperation: Operation.Override, weight: 0.1f, smoothness: 0.9f);

            MaterialLayersCombiner.combine(bark, wavy.invertHeight(), FilterMode.Greater, heightOperation: Operation.Scale, materialOperation: Operation.Override, weight: 0.7f, smoothness: 0.9f);
            MaterialLayersCombiner.combine(bark, noise.scaleHeight(2.0f), FilterMode.Everywhere, heightOperation: Operation.Add, materialOperation: Operation.Smoothstep, weight: 0.4f, smoothness: 0.8f);

            bark.addHeight(-0.05f);
            bark.scaleHeight(4.0f);
            return FinishTexture(bark, normalFlatness: 100.0f);
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

            return FinishTexture(bricks, normalFlatness: 100);
        }

        private int createMetalFloorTexture()
        {
            MaterialLayer tileWweaves = procTextGen.TileWeave(new Vector2(16f, 16f), count: 3, smoothness: 0.9f, width: 0.5f);
            return FinishTexture(tileWweaves);
        }
        private int createGrassTexture()
        {
            MaterialLayer roughLayer = procTextGen.PerlinFBM(new Vector2(8f, 8f), octaves: 8, amplitudePerOctave: 0.8f);
            roughLayer.setMaterial(new Colour(50, 75, 10), new Vector3(0.65f, 0f, 0.0f));
            return FinishTexture(roughLayer);
        }
        private int createGrainTexture()
        {
            MaterialLayer roughLayer = procTextGen.PerlinFBM(new Vector2(8f, 8f), octaves: 8, amplitudePerOctave: 0.8f);
            roughLayer.setMaterial(new Colour(255, 255, 255), new Vector3(0.98f, 0f, 0.0f));
            return FinishTexture(roughLayer);
        }
        private int createSandDunesTexture()
        {
            var sandDunes = procTextGen.PerlinFBM(new Vector2(3f, 7f), octaves: 1, amplitudePerOctave: 0.17f, rigged: true);
            var noise = procTextGen.PerlinFBM(new Vector2(2f, 2f), octaves: 10, amplitudePerOctave: 0.8f);
            var noiseLarge = procTextGen.PerlinFBM(new Vector2(22f, 22f), octaves: 2, amplitudePerOctave: 0.5f);
            sandDunes.setMaterial(new Colour(200, 170, 100), new Vector3(0.35f, 0f, 0f));
            noiseLarge.setMaterial(new Colour(100, 75, 80), new Vector3(0.8f, 0f, 0f));
            noise.setMaterial(new Colour(220, 190, 120), new Vector3(0.98f, 0f, 0f));

            MaterialLayersCombiner.combine(sandDunes, noise, FilterMode.Everywhere, heightOperation: Operation.Add, materialOperation: Operation.Mix, weight: 0.2f, smoothness: 0.9f);

            MaterialLayersCombiner.combine(noise, noiseLarge.scaleHeight(1f), FilterMode.Everywhere, heightOperation: Operation.Scale, materialOperation: Operation.Mix, weight: 0.5f, smoothness: 0.9f);
            MaterialLayersCombiner.combine(sandDunes, noise, FilterMode.Greater, heightOperation: Operation.Override, materialOperation: Operation.Override, weight: 0.5f, smoothness: 0.9f);

            return FinishTexture(sandDunes, normalFlatness:80);
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
