using Dino_Defenders.Books;
using Dino_Defenders.Gameplay;
using Dino_Defenders.Progression;
using Dino_Defenders.Stats;
using Dino_Engine.ECS.Components;
using Dino_Engine.ECS.ECS_Architecture;
using Dino_Engine.Networking;
using Dino_Engine.World;
using Dino_Engine.World.Content;
using OpenTK.Mathematics;

namespace Dino_Defenders.Server
{
    public enum Team : byte
    {
        Heroes,
        Enemies,
    }

    /// <summary>A stack of venom: how much it does, for how long, and whose it is.</summary>
    public struct VenomStack
    {
        public float PerSecond;
        public float Left;
        public Actor? Source;
    }

    /// <summary>A Trigger Book, waiting.</summary>
    public sealed class BookWatch
    {
        public int Slot;
        public Item Book = null!;
        public BookWhen When;
        public float Amount;
        public float Progress;
    }

    /// <summary>What's been done to someone and hasn't worn off.</summary>
    public sealed class Afflictions
    {
        public float BurnPerSecond;
        public float BurnLeft;
        public Actor? BurnSource;
        public float BleedPerSecond;
        public float BleedLeft;
        public Actor? BleedSource;
        public List<VenomStack>? Venom;
        public float ChillEffect;       // Off action speed.
        public float ChillLeft;
        public Actor? ChillSource;
        public float FreezeLeft;
        public float ShockEffect;       // Onto damage taken.
        public float ShockLeft;
        public int HexStacks;
        public float HexEffect = 1f;    // What each stack is worth, as a multiple of normal.
        public float HexLeft;
        public float StunLeft;

        /// <summary>Damage held in the ice, to land when it breaks, and whose it is.</summary>
        public float Stored;
        public Actor? StoredBy;

        public Condition Current
        {
            get
            {
                Condition state = Condition.None;
                if (BurnLeft > 0f) state |= Condition.Burning;
                if (BleedLeft > 0f) state |= Condition.Bleeding;
                if (Venom is { Count: > 0 }) state |= Condition.Envenomed;
                if (ChillLeft > 0f) state |= Condition.Chilled;
                if (FreezeLeft > 0f) state |= Condition.Frozen;
                if (ShockLeft > 0f) state |= Condition.Shocked;
                if (HexLeft > 0f) state |= Condition.Hexed;
                if (StunLeft > 0f) state |= Condition.Stunned;
                return state;
            }
        }

        public void Clear()
        {
            BurnLeft = BleedLeft = ChillLeft = FreezeLeft = ShockLeft = HexLeft = StunLeft = 0f;
            BurnPerSecond = BleedPerSecond = ChillEffect = ShockEffect = Stored = 0f;
            HexStacks = 0;
            HexEffect = 1f;
            Venom?.Clear();
        }

        public float ResistanceLost => HexLeft > 0f ? HexStacks * AilmentRules.HexPerStack * HexEffect : 0f;
        public float TakenIncrease => ShockLeft > 0f ? ShockEffect : 0f;
        public bool Held => FreezeLeft > 0f || StunLeft > 0f;
        public float ActionRate => ChillLeft > 0f ? 1f - ChillEffect : 1f;
    }

    /// <summary>
    /// Someone in a fight. A hero, an enemy and a minion are each one of these, reading their numbers from a sheet and losing health the same way, and
    /// nothing in <see cref="Combat"/> knows which it has. What makes a hero a hero is only that a player is at the other end of it.
    /// </summary>
    public sealed class Actor
    {
        private const float LongAgo = 1000f;
        private const int TrailTicks = 32;

        public readonly Team Team;
        public readonly Afflictions Afflictions = new();
        public readonly Vector2[] Trail = new Vector2[TrailTicks];
        public readonly List<ActiveBuff> Buffs = new();

        /// <summary>What it has lingering on the ground, oldest first.</summary>
        public readonly List<ZoneState> Zones = new();

        /// <summary>What it is with nothing on it.</summary>
        public readonly Loadout Innate;

        public Loadout Loadout;
        public ActorSheet Sheet;
        public Vitals Vitals;
        public Entity Entity;
        public ServerPlayer? Player;
        public Actor? Owner;
        public SiteState? Site;
        public float Radius;

        /// <summary>The model it wears, for how a blow against it sounds.</summary>
        public string Body = "";

        /// <summary>What the land was last found to say about where it stands, for those who ask.</summary>
        public Condition Ground;

        public Vector2 Position;
        public float Height;

        /// <summary>What it is, whatever it's doing: rare, boss.</summary>
        public Condition Nature;

        /// <summary>What's true of it, as of this tick.</summary>
        public Condition State;
        public Counters Counters;
        public bool Moving;

        public float SinceHit = LongAgo;
        public float SinceKill = LongAgo;
        public float SinceCrit = LongAgo;
        public float SinceBlock = LongAgo;
        public float SinceDodge = LongAgo;
        public float SinceUse = LongAgo;

        public float KillHeat;
        public float HealthSpent;
        public float Static;            // A charge built by moving.
        public float RebirthIn;
        public float TrailStep;
        public float CarrierClock;
        public bool Critical;           // The next hit is sure to crit.
        public bool Still;              // Stands and takes it: a training dummy.
        public int Alarm;               // Someone close has raised a cry: how much further it has to travel, counting this one.
        public bool Dead;
        public bool LoadoutChanged;

        /// <summary>What blows have done to where it's going, dying away. Metres a second.</summary>
        public Vector2 Push;

        /// <summary>How long it's still taking a blow for, in seconds.</summary>
        public float Reeling;

        /// <summary>How long until each of its triggers can go off again. Follows the loadout.</summary>
        public float[] TriggerReady;

        /// <summary>The Trigger Books it has, and how far each has got. Most have none.</summary>
        public BookWatch[] Watches = Array.Empty<BookWatch>();

        /// <summary>How long each of its books has left to cool. A hero's are in their step; everyone else's are here.</summary>
        public float[] Cooldowns;

        public Item[]? Gear;            // What it's wearing, and will drop.
        public float MinionLeft = float.PositiveInfinity;
        public string? MinionOf;        // What kind of minion it is, which is how many of a kind are counted.

        public bool IsHero => Player != null;

        public Actor(Team team, Loadout loadout, Vector2 position, float radius)
        {
            Team = team;
            Innate = loadout;
            Loadout = loadout;
            Sheet = loadout.SheetFor(Condition.None);
            Vitals = Vitals.Full(Sheet);
            Position = position;
            Radius = radius;
            Array.Fill(Trail, position);
            Cooldowns = new float[loadout.Books.Length];
            TriggerReady = new float[loadout.Triggers.Length];
        }

        public bool IsFoeOf(Actor other) => Team != other.Team;

        /// <summary>Everything that passes with time alone. Damage over time is <see cref="Combat"/>'s.</summary>
        public void Advance(float seconds, uint tick, Vector2 position)
        {
            Moving = (position - Position).LengthSquared > 1e-5f;
            if (Sheet.Has(Rule.StaticCharge)) Static = MathF.Min(Static + (position - Position).Length, Rulebook.MostStatic);
            Position = position;
            Trail[tick % TrailTicks] = position;

            SinceHit += seconds;
            SinceKill += seconds;
            SinceCrit += seconds;
            SinceBlock += seconds;
            SinceDodge += seconds;
            SinceUse += seconds;
            KillHeat = MathF.Max(KillHeat - Rulebook.RecentKillFade * seconds, 0f);
            HealthSpent = MathF.Max(HealthSpent - Rulebook.HealthSpentFade * seconds, 0f);
            RebirthIn = MathF.Max(RebirthIn - seconds, 0f);
            for (int i = 0; i < Cooldowns.Length; i++) Cooldowns[i] = MathF.Max(Cooldowns[i] - seconds, 0f);
            for (int i = 0; i < TriggerReady.Length; i++) TriggerReady[i] = MathF.Max(TriggerReady[i] - seconds, 0f);

            for (int i = Buffs.Count - 1; i >= 0; i--)
            {
                ActiveBuff buff = Buffs[i];
                buff.Left -= seconds;
                if (buff.Left > 0f)
                {
                    Buffs[i] = buff;
                    continue;
                }
                Buffs.RemoveAt(i);
                LoadoutChanged = true;
            }
        }

        /// <summary>Works out what's true of it now, and takes the sheet that goes with that.</summary>
        public void ReadState(Condition land)
        {
            Condition state = Nature | land | Afflictions.Current | Hits.PoolState(Sheet, Vitals);
            state |= Moving ? Condition.Moving : Condition.Stationary;
            if (SinceHit < Rulebook.Recently) state |= Condition.RecentlyHit;
            if (SinceKill < Rulebook.Recently) state |= Condition.RecentlyKilled;
            if (SinceCrit < Rulebook.Recently) state |= Condition.RecentlyCrit;
            if (SinceBlock < Rulebook.Recently) state |= Condition.RecentlyBlocked;
            if (SinceDodge < Rulebook.Recently) state |= Condition.RecentlyDodged;
            if (SinceUse < Rulebook.Recently) state |= Condition.RecentlyUsedBook;
            if (Sheet.Has(Rule.AlwaysShocked)) state |= Condition.Shocked;
            State = state | Loadout.Granted;

            Counters.RecentKills = (byte)Math.Min((int)KillHeat, 255);
            Counters.HealthSpent = (byte)Math.Min((int)HealthSpent, 255);

            float health = Vitals.Health / Sheet.MaxHealth;
            ActorSheet sheet = Loadout.SheetFor(State, Counters);
            if (ReferenceEquals(sheet, Sheet)) return;

            // A pool's size can change with what's going on. What's in it keeps its share, or a build could be killed by standing up.
            Sheet = sheet;
            Vitals.Health = MathF.Min(health * sheet.MaxHealth, sheet.MaxHealth);
            Vitals.Mana = MathF.Min(Vitals.Mana, sheet.MaxMana);
        }

        /// <summary>A new set of things it can do: new gear, a new tree, a buff. Pools hold their share.</summary>
        public void Adopt(Loadout loadout)
        {
            float health = Vitals.Health / MathF.Max(Sheet.MaxHealth, 1f);
            Loadout = loadout;
            Sheet = loadout.SheetFor(State, Counters);
            Vitals.Health = MathF.Min(Sheet.MaxHealth * health, Sheet.MaxHealth);
            Vitals.Mana = MathF.Min(Vitals.Mana, Sheet.MaxMana);

            // These go by place in the loadout, and a new one can hold more or less.
            if (Cooldowns.Length != loadout.Books.Length) Cooldowns = new float[loadout.Books.Length];
            if (TriggerReady.Length != loadout.Triggers.Length) TriggerReady = new float[loadout.Triggers.Length];
        }

        /// <summary>What ailments and rules add to everything it takes.</summary>
        public float TakenIncrease
        {
            get
            {
                float taken = Afflictions.TakenIncrease;
                return Sheet.Has(Rule.AlwaysShocked) ? MathF.Max(taken, Rulebook.AlwaysShockedTaken) : taken;
            }
        }

        public Vector2 PositionAgo(int ticks, uint tick) => Trail[(int)((tick + TrailTicks - (uint)ticks) % TrailTicks)];
    }

    /// <summary>What an entity has that makes it someone in a fight.</summary>
    public struct ActorComponent : IComponent
    {
        public Actor Actor;

        public ActorComponent(Actor actor)
        {
            Actor = actor;
        }
    }

    /// <summary>
    /// Where everyone is, by the patch of ground they're on. It's what makes an area of effect cost what's near it rather than what's in the world.
    /// It holds no references between ticks, is filled again each one, and allocates nothing while it's doing either.
    /// </summary>
    public sealed class ActorGrid
    {
        private const float CellSize = 12f;
        private const int Empty = -1;

        private readonly int _columns;
        private readonly int[] _heads;
        private readonly uint[] _stamps;
        private Actor[] _actors = new Actor[256];
        private int[] _next = new int[256];
        private int _count;
        private uint _stamp;

        public ActorGrid(float worldSize)
        {
            _columns = (int)(worldSize / CellSize) + 2;
            _heads = new int[_columns * _columns];
            _stamps = new uint[_columns * _columns];
        }

        public void Begin()
        {
            _stamp++;
            _count = 0;
        }

        private int CellOf(int x, int z) => Math.Clamp(z, 0, _columns - 1) * _columns + Math.Clamp(x, 0, _columns - 1);

        public void Add(Actor actor)
        {
            if (_count == _actors.Length)
            {
                Array.Resize(ref _actors, _count * 2);
                Array.Resize(ref _next, _count * 2);
            }

            int cell = CellOf((int)(actor.Position.X / CellSize), (int)(actor.Position.Y / CellSize));
            if (_stamps[cell] != _stamp)
            {
                _stamps[cell] = _stamp;
                _heads[cell] = Empty;
            }

            _actors[_count] = actor;
            _next[_count] = _heads[cell];
            _heads[cell] = _count;
            _count++;
        }

        /// <summary>Adds everyone alive whose body reaches within <paramref name="radius"/> of a point to <paramref name="found"/>, on the other side from <paramref name="team"/>.</summary>
        public void Foes(Team team, Vector2 centre, float radius, List<Actor> found)
        {
            float reach = radius + 3f;
            int left = (int)((centre.X - reach) / CellSize);
            int right = (int)((centre.X + reach) / CellSize);
            int top = (int)((centre.Y - reach) / CellSize);
            int bottom = (int)((centre.Y + reach) / CellSize);

            for (int z = top; z <= bottom; z++)
            {
                if (z < 0 || z >= _columns) continue;
                for (int x = left; x <= right; x++)
                {
                    if (x < 0 || x >= _columns) continue;
                    int cell = z * _columns + x;
                    if (_stamps[cell] != _stamp) continue;

                    for (int i = _heads[cell]; i != Empty; i = _next[i])
                    {
                        Actor actor = _actors[i];
                        if (actor.Team == team || actor.Dead) continue;
                        float within = radius + actor.Radius;
                        if ((actor.Position - centre).LengthSquared <= within * within) found.Add(actor);
                    }
                }
            }
        }
    }

    /// <summary>
    /// What the land says about somewhere, in the terms a build can ask about. All of it comes from the same fields a world is made of, each an
    /// array lookup, and it asks them only for those whose modifiers care.
    /// </summary>
    public sealed class LandReader
    {
        private readonly WorldData _world;
        private readonly bool _endlessNight;
        private readonly Condition[] _biomes;
        private readonly List<WorldSite> _graveyards = new();
        private readonly List<(Vector2 position, float radius, float until)> _pools = new();

        /// <param name="endlessNight">Whether all of it is dark, as a land can be made to be.</param>
        public LandReader(WorldData world, bool endlessNight = false)
        {
            _world = world;
            _endlessNight = endlessNight;
            _biomes = new Condition[world.Plan.Biomes.Count];
            for (int i = 0; i < _biomes.Length; i++)
            {
                BiomeDefinition biome = world.Plan.Biomes[i];
                Condition land = Condition.None;
                if (biome.Temperature >= Rulebook.HotFrom && biome.Moisture < Rulebook.HotDrierThan) land |= Condition.HotLand;
                if (biome.Temperature <= Rulebook.ColdFrom) land |= Condition.ColdLand;
                if (biome.Moisture >= Rulebook.WetFrom) land |= Condition.WetLand;
                if (biome.Altitude >= Rulebook.HighFrom) land |= Condition.HighLand;
                _biomes[i] = land;
            }

            foreach (WorldSite site in world.Sites)
            {
                if (site.Kind == SiteKind.Graveyard) _graveyards.Add(site);
            }
        }

        /// <summary>Water someone has made. It counts for everyone, until it dries up.</summary>
        public void AddPool(Vector2 position, float radius, float until) => _pools.Add((position, radius, until));

        public void Forget(float now) => _pools.RemoveAll(pool => pool.until <= now);

        public bool InWater(Vector2 position)
        {
            if (_world.WaterDepthAt(position) > Rulebook.WaterDepth) return true;
            foreach ((Vector2 at, float radius, _) in _pools)
            {
                if ((at - position).LengthSquared <= radius * radius) return true;
            }
            return false;
        }

        public Condition At(Vector2 position)
        {
            if (!_world.InBounds(position)) return Condition.None;

            Condition land = _biomes[_world.DominantBiome(position)];
            if (InWater(position)) land |= Condition.InWater;

            float x = position.X, z = position.Y;
            if (_world.Canopy.Sample(x, z) > Rulebook.CanopyCover) land |= Condition.UnderTrees;
            if (_world.PavedRoad.Sample(x, z) > Rulebook.RoadCover || _world.TrailRoad.Sample(x, z) > Rulebook.RoadCover) land |= Condition.OnRoad;
            if (_endlessNight) return land | Condition.InDarkness;

            foreach (WorldSite site in _graveyards)
            {
                float reach = site.Radius + Rulebook.DarknessReach;
                if ((site.Position - position).LengthSquared > reach * reach) continue;
                land |= Condition.InDarkness;
                break;
            }
            return land;
        }
    }
}
