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

            if (options.MeshSeed is int meshSeed) Dino_Engine.Util.MyMath.rand = new Random(meshSeed);
            Client.SettingsStore.Use(options.Settings, options.Scripted);
            Client.Bindings.Load();
            UI.VideoSettings video = UI.VideoSettings.Load();

            var launch = new EngineLaunchSettings(BuildInfo.Title) { _hidden = options.Hidden };
            bool ownSettings = !options.Scripted || options.Settings != null;
            if (ownSettings) video.Launching(launch);
            if (options.Size is OpenTK.Mathematics.Vector2i size) launch._resolution = size;
            if (options.FrameRateLimit is int limit) launch._frameRateLimit = limit;
            Engine engine = new Engine(launch);
            Console.WriteLine($"{GL.GetString(StringName.Renderer)}, OpenGL {GL.GetString(StringName.Version)}");
            if (ownSettings) video.Apply();
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
