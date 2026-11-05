using Dino_Defenders.Gameplay;
using Dino_Defenders.Net;
using Dino_Defenders.Progression;
using Dino_Defenders.Skills;
using Dino_Engine.ECS.ECS_Architecture;
using Dino_Engine.Networking;
using OpenTK.Mathematics;

namespace Dino_Defenders.Server
{
    /// <summary>
    /// Someone connected, and their hero as the server knows it. During a game the hero's truth is here; the client keeps a copy,
    /// which is what ends up in that player's own save.
    /// </summary>
    public sealed class ServerPlayer
    {
        private const int CommandRing = 256;
        private const int MostFilledTicks = 15;     // A connection this quiet has stopped, not slowed; the hero stops too.
        private const int SpareWindow = 30;         // Ticks over which the tightest arrival is reported.

        private readonly HeroCommand[] _commands = new HeroCommand[CommandRing];
        private HeroCommand _last;
        private HeroButtons _carried;
        private int _filledTicks;
        private int _worstSpare = int.MaxValue;
        private int _reportedSpare;
        private int _spareAge;

        public readonly int Id;
        public readonly INetConnection Connection;
        public GhostView View = new();
        public string Name = "Hero";
        public HeroState Hero = null!;
        public Actor Actor = null!;
        public HeroKit Kit = null!;
        public HeroSimState Sim;
        public SkillDefinition? Stolen;
        public float DownFor;
        public Vector2 Respawn;
        public Entity Entity;
        public uint GhostId;
        public byte SwingSerial;

        public bool Greeted;        // Has said hello, and been let in.
        public bool InWorld;        // Has a hero standing in the current world.
        public int ReadyEpoch = -1;
        public bool SummaryChanged;
        public bool GearChanged;
        public bool BuffsChanged;
        public uint LastClientTime;
        public long LastClientTimeArrived;
        public int SendInterval = 2;
        public int RoundTrip;
        public readonly Dictionary<int, List<Item>> Stocks = new();

        /// <summary>The newest command received, and commands that came too late to use.</summary>
        public uint NewestCommand { get; private set; }
        public int LateCommands { get; private set; }
        public int FilledCommands { get; private set; }

        public ServerPlayer(int id, INetConnection connection)
        {
            Id = id;
            Connection = connection;
        }

        /// <summary>Gear, talents or something else has changed, so everything is worked out again.</summary>
        public void RefreshLoadout()
        {
            Actor.Adopt(HeroBuild.Compile(Hero, Actor.Buffs, Stolen));
            Kit = new HeroKit(Hero, Actor.Sheet);
        }

        /// <summary>Takes up the last skill that hit them, for those who can.</summary>
        public void Mimic(SkillDefinition skill)
        {
            if (Stolen == skill) return;
            Stolen = skill;
            Hero.Bar[^1] = skill.Id;
            RefreshLoadout();
            SummaryChanged = true;
            BuffsChanged = true;
        }

        /// <summary>Takes commands off the wire. <paramref name="nextTick"/> is the next tick to run.</summary>
        public void Receive(in HeroCommand command, uint nextTick)
        {
            int spare = (int)(command.Tick - nextTick);
            if (command.Tick > NewestCommand) NewestCommand = command.Tick;

            // How tight it was for this one, the first time it arrives. A packet from a slow client holds several ticks, and what
            // matters to its clock is the oldest of them, not the newest.
            if (!Seen(command.Tick) && spare < _worstSpare) _worstSpare = spare;

            if (command.Tick < nextTick)
            {
                // Its tick has gone, but a press still counts: it happens late rather than not at all, or it would be lost
                // to a client that has already shown it happening.
                if (command.Tick + CommandRing > nextTick && !Seen(command.Tick))
                {
                    _carried |= command.Buttons & HeroButtons.Presses;
                    _commands[command.Tick % CommandRing] = command;
                    LateCommands++;
                }
                return;
            }

            // No further ahead than the ring reaches: that would overwrite commands still to be run.
            if (spare >= CommandRing) return;
            _commands[command.Tick % CommandRing] = command;
        }

        private bool Seen(uint tick) => _commands[tick % CommandRing].Tick == tick;

        /// <summary>The command for this tick. If it hasn't come, the last one again: most of the time a player is doing what they just did.</summary>
        public HeroCommand Take(uint tick)
        {
            HeroCommand command;
            if (Seen(tick))
            {
                command = _commands[tick % CommandRing];
                _filledTicks = 0;
                _last = command;
            }
            else
            {
                command = _last;
                command.Tick = tick;
                command.Buttons &= ~HeroButtons.Presses;
                FilledCommands++;
                if (++_filledTicks > MostFilledTicks)
                {
                    command.MoveX = 0;
                    command.MoveZ = 0;
                    command.Buttons &= ~HeroButtons.Slots;
                }
            }

            command.Buttons |= _carried;
            _carried = HeroButtons.None;

            if (++_spareAge >= SpareWindow)
            {
                _reportedSpare = _worstSpare == int.MaxValue ? _reportedSpare : _worstSpare;
                _worstSpare = int.MaxValue;
                _spareAge = 0;
            }
            return command;
        }

        /// <summary>How much slack commands have been arriving with, at worst, in ticks.</summary>
        public int Spare => Math.Min(_reportedSpare, _worstSpare);
    }
}
