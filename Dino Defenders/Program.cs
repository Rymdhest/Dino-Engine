using Dino_Engine.Core;

namespace Dino_Defenders
{
    internal class Program
    {
        static void Main(string[] args)
        {
            Engine engine = new Engine(new EngineLaunchSettings("Dino Defenders"));
            ArpgGame game = new ArpgGame(engine, GameOptions.Parse(args));
            engine.Run();
        }
    }
}
