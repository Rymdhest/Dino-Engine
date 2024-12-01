using Dino_Engine.Core;
using Dino_Engine.Rendering;
using Dino_Engine.Rendering.Renderers;
using OpenTK.Graphics.OpenGL;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static Dino_Engine.Textures.MaterialLayersManipulator;

namespace Dino_Engine.Textures
{
    public class MaterialLayersManipulator
    {
        private ShaderProgram _combineShader = new ShaderProgram("Simple.vert", "procedural/Texture_Combine.frag");
        public enum FilterMode
        {
            Everywhere,
            Greater,
            Lesser
        }
        public enum Operation
        {
            Add,
            Subtract,
            Scale,
            Smoothstep,
            Power,
            Override,
            Nothing,
            Hash,
            Mix,
            SameAsOther
        }


        public MaterialLayersManipulator()
        {
            _combineShader.bind();
            _combineShader.loadUniformInt("writeTextureAlbedo", 0);
            _combineShader.loadUniformInt("writeTextureMaterial", 1);

            _combineShader.loadUniformInt("readTextureAlbedo", 2);
            _combineShader.loadUniformInt("readTextureMaterial", 3);
        }


        public MaterialLayer combine(MaterialLayer writeLayer, MaterialLayer readLayer, FilterMode filterMode, Operation materialOperation = Operation.SameAsOther, Operation heightOperation = Operation.SameAsOther, float weight = 0.5f, float smoothness = 0f)
        {
            if (materialOperation == Operation.SameAsOther && heightOperation == Operation.SameAsOther)
            {
                throw new ArgumentException("Cant have both material and height operations be same as other.");
            } else if (materialOperation == Operation.SameAsOther)
            {
                materialOperation = heightOperation;
            } else if (heightOperation == Operation.SameAsOther)
            {
                heightOperation = materialOperation;
            }

            _combineShader.bind();

            _combineShader.loadUniformInt("filterMode", (int)filterMode);
            _combineShader.loadUniformInt("materialOperation", (int)materialOperation);
            _combineShader.loadUniformInt("heightOperation", (int)heightOperation);
            _combineShader.loadUniformFloat("weight", weight);
            _combineShader.loadUniformFloat("smoothness", smoothness);

            GL.ActiveTexture(TextureUnit.Texture0);
            GL.BindTexture(TextureTarget.Texture2D, writeLayer.GetLastFrameBuffer().GetAttachment(0));
            GL.ActiveTexture(TextureUnit.Texture1);
            GL.BindTexture(TextureTarget.Texture2D, writeLayer.GetLastFrameBuffer().GetAttachment(1));


            GL.ActiveTexture(TextureUnit.Texture2);
            GL.BindTexture(TextureTarget.Texture2D, readLayer.GetLastFrameBuffer().GetAttachment(0));
            GL.ActiveTexture(TextureUnit.Texture3);
            GL.BindTexture(TextureTarget.Texture2D, readLayer.GetLastFrameBuffer().GetAttachment(1));


            writeLayer.tap();

            _combineShader.unBind();

            return writeLayer;
        }
    }
}
