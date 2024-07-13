using Dino_Engine.Core;
using Dino_Engine.ECS;
using Dino_Engine.ECS.Components;
using Dino_Engine.ECS.Systems;
using Dino_Engine.Modelling.Model;
using Dino_Engine.Modelling.Procedural.Nature;
using Dino_Engine.Modelling.Procedural.Urban;
using Dino_Engine.Modelling.Procedural;
using Dino_Engine.Util;
using OpenTK.Mathematics;
using Dino_Engine.Modelling;
using Dino_Engine.Debug;
using Dino_Engine.Modelling.Procedural.Terrain;
using Dino_Engine.Rendering;
using Dino_Engine.Util.Data_Structures.Grids;
using Dino_Engine.Util.Noise;
using Dino_Engine;
using Util.Noise;

namespace Dino_Defenders
{
    internal class Program
    {
        static void Main(string[] args)
        {
            Engine engine = new Engine(new EngineLaunchSettings("Dino Defenders"));
            DemoGame game = new DemoGame(engine);
            engine.Run();
        }
    }
}
