using Dino_Engine.Core;
using Dino_Engine.Rendering;
using Dino_Engine.Rendering.Renderers;
using Dino_Engine.Util;
using OpenTK.Graphics.OpenGL;
using OpenTK.Mathematics;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Mail;
using System.Text;
using System.Threading.Tasks;

namespace Dino_Engine.Textures
{
    public class TextureGenerator
    {

        private Vector2i textureResolution = new Vector2i(512, 512);

        public int megaAlbedoTextureArray;
        public int megaNormalTextureArray;
        public int megaMaterialTextureArray;

        public int grainIndex;
        public int sandIndex;
        public int flatIndex;
        public int flatGlowIndex;

        private ProceduralTextureRenderer proceduralTextureRenderer;

        public List<MaterialMapsTextures> preparedTextures = new List<MaterialMapsTextures>();

        public TextureGenerator()
        {
            proceduralTextureRenderer = new ProceduralTextureRenderer(textureResolution);
        }

        public void GenerateAllTextures()
        {
            grainIndex = createGrainTexture();
            flatIndex = createFlatTexture();
            flatGlowIndex = createFlatGlowTexture();
            loadAllTexturesToArray();
        }

        private int FinishTexture()
        {
            preparedTextures.Add(proceduralTextureRenderer.Export());
            return preparedTextures.Count-1;
        }

        private int loadTypeOfTextureToArray(int type)
        {
            int textureArray = GL.GenTexture();
            GL.BindTexture(TextureTarget.Texture2DArray, textureArray);
            Engine.CheckGLError("After BindTexture");

            // Allocate storage for the texture array with the correct number of slices
            GL.TexStorage3D(TextureTarget3d.Texture2DArray, 8, SizedInternalFormat.Rgba8, textureResolution.X, textureResolution.Y, preparedTextures.Count);
            Engine.CheckGLError("After TexStorage3D");

            for (int i = 0; i < preparedTextures.Count; i++)
            {
                //GL.BindTexture(TextureTarget.Texture2D, preparedTextures[i].textures[type]);
                Engine.CheckGLError("After BindTexture2D");
                //GL.CopyTexSubImage3D(TextureTarget.Texture2DArray, 0, 0, 0, i, 0, 0, textureResolution.X, textureResolution.Y);
                GL.CopyImageSubData(preparedTextures[i].textures[type], ImageTarget.Texture2D, 0, 0, 0, 0, textureArray, ImageTarget.Texture2DArray, 0, 0, 0, i,textureResolution.X, textureResolution.Y, 1 );
                Engine.CheckGLError("After CopyTexSubImage3D");
            }
            GL.TexParameter(TextureTarget.Texture2DArray, TextureParameterName.TextureMinFilter, (int)TextureMinFilter.LinearMipmapLinear);
            GL.TexParameter(TextureTarget.Texture2DArray, TextureParameterName.TextureMagFilter, (int)TextureMagFilter.Linear);
            GL.TexParameter(TextureTarget.Texture2DArray, TextureParameterName.TextureWrapS, (int)TextureWrapMode.Repeat);
            GL.TexParameter(TextureTarget.Texture2DArray, TextureParameterName.TextureWrapT, (int)TextureWrapMode.Repeat);

            Engine.CheckGLError("After TexParameter");
            GL.GenerateMipmap(GenerateMipmapTarget.Texture2DArray);


            // Unbind the texture array
            GL.BindTexture(TextureTarget.Texture2DArray, 0);

            return textureArray;
        }


        private void loadAllTexturesToArray()
        {
            megaAlbedoTextureArray = loadTypeOfTextureToArray(0);
            megaNormalTextureArray = loadTypeOfTextureToArray(1);
            megaMaterialTextureArray = loadTypeOfTextureToArray(2);

            foreach (MaterialMapsTextures materialTextures in preparedTextures)
            {
                materialTextures.CleanUp();
            }
        }

        private int createFlatGlowTexture()
        {
            proceduralTextureRenderer.heightFactor = 0.06f;
            proceduralTextureRenderer.colour = new Colour(255, 255, 255, 1f, 1f);
            proceduralTextureRenderer.rougness = 0.7f;
            proceduralTextureRenderer.emission = 1.0f;
            proceduralTextureRenderer.startFrequenzy = new Vector2(10.0f, 10.0f);
            proceduralTextureRenderer.Tap();
            return FinishTexture();
        }

        private int createFlatTexture()
        {
            proceduralTextureRenderer.heightFactor = 1.0f;
            proceduralTextureRenderer.colour = new Colour(255, 255, 255, 1f, 1f);
            proceduralTextureRenderer.rougness = 0.6f;
            proceduralTextureRenderer.metlaic = 0.0f;
            proceduralTextureRenderer.startFrequenzy = new Vector2(10.0f, 10.0f);
            proceduralTextureRenderer.Tap();
            return FinishTexture();
        }

        private int createGrainTexture()
        {

            proceduralTextureRenderer.octaves = 14;
            proceduralTextureRenderer.seed = 1.6f;
            proceduralTextureRenderer.exponent = 1.2f;
            proceduralTextureRenderer.amplitudePerOctave = 0.5f;
            proceduralTextureRenderer.heightFactor = 0.4f;
            proceduralTextureRenderer.colour = new Colour(155, 120, 100, 1f, 1f);
            proceduralTextureRenderer.rougness = 0.7f;
            proceduralTextureRenderer.rigged = true;
            proceduralTextureRenderer.invert = false;
            proceduralTextureRenderer.startFrequenzy = new Vector2(50.0f, 50.0f);
            proceduralTextureRenderer.Tap();

            proceduralTextureRenderer.startFrequenzy = new Vector2(25.0f, 25.0f);
            proceduralTextureRenderer.colour = new Colour(70, 70, 70, 1f, 1f);
            proceduralTextureRenderer.depthCheck = true;
            proceduralTextureRenderer.blendMode = ProceduralTextureRenderer.BlendMode.overriding;
            proceduralTextureRenderer.seed = 7.6f;
            proceduralTextureRenderer.Tap();

            proceduralTextureRenderer.octaves = 5;
            proceduralTextureRenderer.seed = 3.1f;
            proceduralTextureRenderer.exponent = 1.2f;
            proceduralTextureRenderer.amplitudePerOctave = 0.35f;
            proceduralTextureRenderer.heightFactor = 1.0f;
            proceduralTextureRenderer.colour = new Colour(155, 155, 155, 1f, 1f);
            proceduralTextureRenderer.rougness = 0.32f;
            proceduralTextureRenderer.metlaic = 0.0f;
            proceduralTextureRenderer.rigged = true;
            proceduralTextureRenderer.invert = false;
            proceduralTextureRenderer.depthCheck = true;
            proceduralTextureRenderer.startFrequenzy = new Vector2(10.0f, 10.0f);
            proceduralTextureRenderer.Tap();

            proceduralTextureRenderer.octaves = 15;
            proceduralTextureRenderer.seed = 13.1f;
            proceduralTextureRenderer.exponent = 1.0f;
            proceduralTextureRenderer.amplitudePerOctave = 0.55f;
            proceduralTextureRenderer.heightFactor = 1.0f;
            proceduralTextureRenderer.colour = new Colour(255, 0, 0, 1f, 1f);
            proceduralTextureRenderer.rougness = 0.7f;
            proceduralTextureRenderer.metlaic = 0.0f;
            proceduralTextureRenderer.rigged = false;
            proceduralTextureRenderer.invert = false;
            proceduralTextureRenderer.depthCheck = true;
            proceduralTextureRenderer.scaleOutputToHeight = true;
            proceduralTextureRenderer.writeToHeight = true;
            proceduralTextureRenderer.startFrequenzy = new Vector2(15.0f, 15.0f);
            //proceduralTextureRenderer.Tap();


            proceduralTextureRenderer.normalFlatness = 0.15f;
            return FinishTexture();
        }
        public void CleanUp()
        {
            GL.DeleteTexture(megaAlbedoTextureArray);
            GL.DeleteTexture(megaMaterialTextureArray);
            GL.DeleteTexture(megaNormalTextureArray);
        }
    }
}
