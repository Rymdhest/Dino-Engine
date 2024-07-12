using Dino_Engine.ECS;
using OpenTK.Windowing.Common;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Dino_Engine.Rendering.Renderers.Geometry
{
    internal class TerrainRenderer : Renderer
    {
        private ShaderProgram _terrainShader = new ShaderProgram("Model.vert", "Terrain.frag");


        public TerrainRenderer()
        {

        }

        internal override void Finish(ECSEngine eCSEngine, RenderEngine renderEngine)
        {
        }

        internal override void Prepare(ECSEngine eCSEngine, RenderEngine renderEngine)
        {
        }

        internal override void Render(ECSEngine eCSEngine, RenderEngine renderEngine)
        {
        }
        public override void CleanUp()
        {
            _terrainShader.cleanUp();
        }

        public override void OnResize(ResizeEventArgs eventArgs)
        {
        }

        public override void Update()
        {
        }
    }
}
