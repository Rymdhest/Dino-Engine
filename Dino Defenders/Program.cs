using Dino_Defenders.Net;
using Dino_Defenders.Server;
using Dino_Engine.Core;
using Dino_Engine.ECS.ECS_Architecture;
using Dino_Engine.Networking;
using Dino_Engine.World;
using OpenTK.Graphics.OpenGL;

namespace Dino_Defenders
{
    internal class Program
    {
        static int Main(string[] args)
        {
            if (args.Length == 3 && args[0] == HangWatch.DumpArgument) return HangWatch.WriteDump(int.Parse(args[1]), args[2]);

            GameLog.Start();
            GameOptions options = GameOptions.Parse(args);
            if (options.Dedicated)
            {
                RunDedicated(options);
                return 0;
            }

            Engine engine = new Engine(new EngineLaunchSettings(BuildInfo.Title) { _hidden = options.Hidden });
            Console.WriteLine($"{GL.GetString(StringName.Renderer)}, OpenGL {GL.GetString(StringName.Version)}");
            ArpgGame game = new ArpgGame(engine, options);
            Engine.WindowHandler.UpdateFrame += _ => HangWatch.Beat();
            HangWatch.Start();
            engine.Run();
            return 0;
        }

        // A server with no window and no hero of its own, for anyone with a machine to leave running.
        private static void RunDedicated(GameOptions options)
        {
            ComponentTypeRegistry.AutoRegisterAllComponents();
            NetSchema.Build();

            uint secret = options.Secret != null ? NetSchema.Fnv(options.Secret) : InviteCode.NewSecret();
            var request = new WorldRequest(options.Seed ?? Environment.TickCount, options.Archetype, options.Level);
            var server = new GameServer(new ServerSettings { Request = request, Offering = options.Offering, StartSite = options.Site }, new WorldCache());
            server.AddTransport(UdpTransport.Host(options.Port, InviteCode.KeyFor(secret)));
            using var host = new ServerHost(server);

            Console.WriteLine($"Serving seed {request.Seed} on port {options.Port}.");
            Console.WriteLine($"Invite: {new InviteCode { Local = InviteCode.LocalAddresses().FirstOrDefault(), Port = options.Port, Secret = secret }.Text}");
            while (host.Failure == null) Thread.Sleep(1000);
        }
    }
}
