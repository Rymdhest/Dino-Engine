using Dino_Engine.Core;
using Dino_Engine.Rendering;
using Dino_Engine.Rendering.Renderers;
using OpenTK.Graphics.OpenGL;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Dino_Engine.Textures
{
    internal class MaterialLayersCombiner
    {
        private ShaderProgram _combineShader = new ShaderProgram("Simple.vert", "procedural/Texture_Combine.frag");

        public MaterialLayersCombiner()
        {
            _combineShader.bind();
            _combineShader.loadUniformInt("albedoTexture1", 0);
            _combineShader.loadUniformInt("materialTexture1", 1);

            _combineShader.loadUniformInt("albedoTexture2", 2);
            _combineShader.loadUniformInt("materialTexture2", 3);
            _combineShader.unBind();
        }

        public MaterialLayer combine(MaterialLayer layer1, MaterialLayer layer2)
        {

            MaterialLayer combinedLayer = new MaterialLayer(layer1.Resolution);
            combinedLayer.GetNextFrameBuffer().bind();

            GL.ActiveTexture(TextureUnit.Texture0);
            GL.BindTexture(TextureTarget.Texture2D, layer1.GetLastFrameBuffer().GetAttachment(0));
            GL.ActiveTexture(TextureUnit.Texture1);
            GL.BindTexture(TextureTarget.Texture2D, layer1.GetLastFrameBuffer().GetAttachment(1));


            GL.ActiveTexture(TextureUnit.Texture2);
            GL.BindTexture(TextureTarget.Texture2D, layer2.GetLastFrameBuffer().GetAttachment(0));
            GL.ActiveTexture(TextureUnit.Texture3);
            GL.BindTexture(TextureTarget.Texture2D, layer2.GetLastFrameBuffer().GetAttachment(1));
            _combineShader.bind();


            Engine.RenderEngine.ScreenQuadRenderer.Render();
            combinedLayer.StepToggle();
            _combineShader.unBind();

            return combinedLayer;
        }
    }
}
