using Dino_Engine.Networking;
using Dino_Engine.World;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace Dino_Defenders.Server
{
    /// <summary>
    /// Keeps a world's generation from being done twice in one process: a host's server and its own client ask for the same world,
    /// and between them need only the one copy. Across a network each machine grows its own, from the same seed. The Haven is kept
    /// apart from whatever land was grown last, as everyone goes back to it between every two lands.
    /// </summary>
    public sealed class WorldCache
    {
        private readonly object _lock = new();
        private WorldRequest _request;
        private Task<WorldData>? _world;
        private Task<WorldData>? _haven;

        /// <summary>Told how far the most recent generation has got, which may have begun before this was set: the land behind the menu is begun before there's a window.</summary>
        public Action<float, string>? Progress;

        private int _latest;

        public Task<WorldData> Get(WorldRequest request)
        {
            lock (_lock)
            {
                if (Progression.Lands.IsHaven(request))
                {
                    if (_haven is { IsFaulted: false } kept) return kept;
                    Action<float, string> growing = Reporter();
                    _haven = Task.Run(() => new Dino_Engine.World.Generation.WorldGenerator().Generate(request, growing));
                    return _haven;
                }
                if (_world != null && Same(_request, request)) return _world;
                _request = request;
                Action<float, string> progress = Reporter();
                _world = Task.Run(() => new Dino_Engine.World.Generation.WorldGenerator().Generate(request, progress));
                return _world;
            }
        }

        // Passes a generation's progress on to whoever is listening now, for as long as nothing newer has been begun.
        private Action<float, string> Reporter()
        {
            int generation = ++_latest;
            return (progress, stage) =>
            {
                if (Volatile.Read(ref _latest) == generation) Progress?.Invoke(progress, stage);
            };
        }

        private static bool Same(in WorldRequest a, in WorldRequest b)
        {
            return a.Seed == b.Seed && a.Level == b.Level && a.ArchetypeId == b.ArchetypeId && a.MorePeople == b.MorePeople && a.MorePacks == b.MorePacks && a.Larger == b.Larger;
        }
    }

    /// <summary>
    /// Runs a server on a thread of its own, at a steady rate. That is what stops one player's slow frame from being everyone's
    /// problem: a host's graphics card can stall, and their friends' game goes on. Anything from outside gets to it through <see cref="Post"/>.
    /// </summary>
    public sealed class ServerHost : IDisposable
    {
        private const int MostCatchUp = 8;      // Ticks at once after a stall. Past that, the lost time stays lost.

        private readonly GameServer _server;
        private readonly Thread _thread;
        private readonly ConcurrentQueue<Action<GameServer>> _requests = new();
        private volatile bool _stopping;

        /// <summary>Set if the server died, and why.</summary>
        public Exception? Failure { get; private set; }

        public ServerHost(GameServer server)
        {
            _server = server;
            _thread = new Thread(Run) { IsBackground = true, Name = "Game Server" };
            _thread.Start();
        }

        /// <summary>Runs something on the server's own thread, between ticks.</summary>
        public void Post(Action<GameServer> request) => _requests.Enqueue(request);

        /// <summary>Asks something of the server and waits for the answer.</summary>
        public T Ask<T>(Func<GameServer, T> question, int timeoutMilliseconds = 2000)
        {
            var answer = new TaskCompletionSource<T>();
            Post(server =>
            {
                try
                {
                    answer.TrySetResult(question(server));
                }
                catch (Exception e)
                {
                    answer.TrySetException(e);
                }
            });
            return answer.Task.Wait(timeoutMilliseconds) ? answer.Task.Result : default!;
        }

        public void Dispose()
        {
            _stopping = true;
            _thread.Join(3000);
            _server.Dispose();
        }

        private void Run()
        {
            bool fineTimer = OperatingSystem.IsWindows() && TimeBeginPeriod(1) == 0;
            var clock = Stopwatch.StartNew();
            double next = 0.0;

            try
            {
                while (!_stopping)
                {
                    while (_requests.TryDequeue(out Action<GameServer>? request)) request(_server);

                    double now = clock.Elapsed.TotalSeconds;
                    int steps = 0;
                    while (now >= next && steps < MostCatchUp)
                    {
                        _server.Step();
                        next += NetTime.TickSecondsPrecise;
                        steps++;
                    }
                    if (now >= next) next = now;

                    double wait = next - clock.Elapsed.TotalSeconds;
                    if (wait > 0.002) Thread.Sleep((int)(wait * 1000.0));
                    else if (wait > 0.0) Thread.Yield();
                }
            }
            catch (Exception e)
            {
                // Whatever went wrong, nobody should be left standing in a world that has stopped.
                Failure = e;
                Console.WriteLine($"The server stopped: {e}");
                _server.Close("The server has stopped.");
            }
            finally
            {
                if (fineTimer) TimeEndPeriod(1);
            }
        }

        // By default Windows sleeps in steps of up to 15 ms, which is a whole tick. This is the documented way to ask for better.
        [DllImport("winmm.dll", EntryPoint = "timeBeginPeriod")]
        private static extern uint TimeBeginPeriod(uint milliseconds);

        [DllImport("winmm.dll", EntryPoint = "timeEndPeriod")]
        private static extern uint TimeEndPeriod(uint milliseconds);
    }
}
