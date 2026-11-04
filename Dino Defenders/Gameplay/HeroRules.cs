using Dino_Defenders.Progression;
using Dino_Engine.Networking;
using Dino_Engine.World;
using OpenTK.Mathematics;

namespace Dino_Defenders.Gameplay
{
    /// <summary>What the player means to do this frame, from the keyboard and mouse or from whatever is driving the hero instead.</summary>
    public struct HeroIntent
    {
        public Vector2 Move;
        public Vector2? Aim;
        public bool Attack;
        public bool Dash;
        public bool Whirl;
        public bool Flask;
        public bool Interact;
        public bool Guide;
    }

    [Flags]
    public enum HeroButtons : byte
    {
        None = 0,
        Attack = 1,     // Held.
        Dash = 2,       // Pressed, like the rest.
        Whirl = 4,
        Flask = 8,
        Interact = 16,
        Frozen = 32,    // The game is paused, as someone playing alone can.

        /// <summary>The ones that happen once per press, and so must never be lost.</summary>
        Presses = Dash | Whirl | Flask | Interact,
    }

    /// <summary>
    /// What a hero does in one tick. It is the whole of what crosses the network from a player, in the coarse form it crosses in:
    /// the client turns its input into this first and simulates from that, so both ends start from exactly the same numbers.
    /// </summary>
    public struct HeroCommand
    {
        private const float AimStep = 1f / 32f;
        private const float MostAim = 1000f;

        public uint Tick;
        public sbyte MoveX;
        public sbyte MoveZ;
        public short AimX;
        public short AimZ;
        public HeroButtons Buttons;
        public byte ViewLag;    // How many ticks behind this one the player's picture of everyone else was.

        public readonly Vector2 Move => new Vector2(MoveX, MoveZ) * (1f / 127f);
        public readonly Vector2 Aim => new Vector2(AimX, AimZ) * AimStep;
        public readonly bool Has(HeroButtons button) => (Buttons & button) != 0;

        public static HeroCommand From(in HeroIntent intent, uint tick, byte viewLag)
        {
            var command = new HeroCommand { Tick = tick, ViewLag = viewLag };

            Vector2 move = intent.Move;
            if (move.LengthSquared > 1e-6f)
            {
                move.Normalize();
                command.MoveX = (sbyte)MathF.Round(move.X * 127f);
                command.MoveZ = (sbyte)MathF.Round(move.Y * 127f);
            }

            if (intent.Aim is Vector2 aim && aim.LengthSquared > 1e-4f)
            {
                float length = aim.Length;
                if (length > MostAim) aim *= MostAim / length;
                command.AimX = (short)MathF.Round(aim.X / AimStep);
                command.AimZ = (short)MathF.Round(aim.Y / AimStep);
            }

            if (intent.Attack) command.Buttons |= HeroButtons.Attack;
            if (intent.Dash) command.Buttons |= HeroButtons.Dash;
            if (intent.Whirl) command.Buttons |= HeroButtons.Whirl;
            if (intent.Flask) command.Buttons |= HeroButtons.Flask;
            if (intent.Interact) command.Buttons |= HeroButtons.Interact;
            return command;
        }

        public readonly void Write(BitWriter writer)
        {
            writer.WriteByte((byte)MoveX);
            writer.WriteByte((byte)MoveZ);
            writer.WriteBits((ushort)AimX, 16);
            writer.WriteBits((ushort)AimZ, 16);
            writer.WriteByte((byte)Buttons);
            writer.WriteByte(ViewLag);
        }

        public static HeroCommand Read(BitReader reader, uint tick) => new HeroCommand
        {
            Tick = tick,
            MoveX = (sbyte)reader.ReadByte(),
            MoveZ = (sbyte)reader.ReadByte(),
            AimX = (short)reader.ReadBits(16),
            AimZ = (short)reader.ReadBits(16),
            Buttons = (HeroButtons)reader.ReadByte(),
            ViewLag = reader.ReadByte(),
        };
    }

    [Flags]
    public enum HeroStepEvents
    {
        None = 0,
        Swung = 1,      // A swing has started.
        Struck = 2,     // Its blow lands now.
        Whirled = 4,
        Dashed = 8,
    }

    /// <summary>
    /// Everything about a hero that a tick can change. The client predicts this and the server has the last word on it, so it is
    /// sent whole and unrounded: a client that lands on the same numbers knows it guessed right, and otherwise has what it needs to
    /// correct itself.
    /// </summary>
    public struct HeroSimState
    {
        public Vector2 Position;
        public float Yaw;
        public Vector2 Aim;
        public Vector2 DashDirection;
        public float DashLeft;
        public float SwingImpactIn;     // Below 0 when no blow is on its way.
        public float SwingCooldown;
        public float DashCooldown;
        public float WhirlCooldown;
        public float Safe;              // Nothing can touch them for this long: a dodge's window of safety.
        public bool Down;
        public byte Moves;              // Goes up each time the server puts them somewhere else, so it isn't smoothed over.

        public static HeroSimState At(Vector2 position) => new HeroSimState
        {
            Position = position,
            Aim = Vector2.UnitY,
            SwingImpactIn = -1f,
        };

        public readonly void Write(BitWriter writer)
        {
            writer.WriteFloat(Position.X);
            writer.WriteFloat(Position.Y);
            writer.WriteFloat(Yaw);
            writer.WriteFloat(Aim.X);
            writer.WriteFloat(Aim.Y);
            writer.WriteFloat(DashDirection.X);
            writer.WriteFloat(DashDirection.Y);
            writer.WriteFloat(DashLeft);
            writer.WriteFloat(SwingImpactIn);
            writer.WriteFloat(SwingCooldown);
            writer.WriteFloat(DashCooldown);
            writer.WriteFloat(WhirlCooldown);
            writer.WriteFloat(Safe);
            writer.WriteBool(Down);
            writer.WriteByte(Moves);
        }

        public static HeroSimState Read(BitReader reader) => new HeroSimState
        {
            Position = new Vector2(reader.ReadFloat(), reader.ReadFloat()),
            Yaw = reader.ReadFloat(),
            Aim = new Vector2(reader.ReadFloat(), reader.ReadFloat()),
            DashDirection = new Vector2(reader.ReadFloat(), reader.ReadFloat()),
            DashLeft = reader.ReadFloat(),
            SwingImpactIn = reader.ReadFloat(),
            SwingCooldown = reader.ReadFloat(),
            DashCooldown = reader.ReadFloat(),
            WhirlCooldown = reader.ReadFloat(),
            Safe = reader.ReadFloat(),
            Down = reader.ReadBool(),
            Moves = reader.ReadByte(),
        };

        /// <summary>Whether a prediction landed on the same place as what the server says. How they face doesn't affect anything, so it isn't counted.</summary>
        public readonly bool Matches(in HeroSimState other)
        {
            return (Position - other.Position).LengthSquared < 1e-6f
                && Down == other.Down && Moves == other.Moves
                && MathF.Abs(DashLeft - other.DashLeft) < 1e-3f
                && MathF.Abs(SwingImpactIn - other.SwingImpactIn) < 1e-3f
                && MathF.Abs(SwingCooldown - other.SwingCooldown) < 1e-3f
                && MathF.Abs(DashCooldown - other.DashCooldown) < 1e-3f
                && MathF.Abs(WhirlCooldown - other.WhirlCooldown) < 1e-3f;
        }
    }

    /// <summary>
    /// How a hero moves and acts, for one tick. The server runs this for every hero and each client runs it for its own, the same code on
    /// the same numbers. It depends on the command, the hero's stats and the fixed shape of the world, and on nothing that other
    /// characters are doing: that is what lets a client know where its hero will be without waiting to be told, and it is the
    /// rule that keeps this game free of the desync other ARPGs are known for. Anything that needs to push a hero around
    /// (knockback, stuns, slows) has to come in through what the server sends back, never by reading other entities here.
    /// </summary>
    public static class HeroRules
    {
        public const float SwingDuration = 0.6f;
        public const float SwingImpact = 0.15f;
        public const float SwingReach = 6.4f;
        public const float SwingArc = 2.4f;
        public const float DashDuration = 0.2f;
        public const float DashSpeed = 34f;
        public const float DashCooldown = 2.4f;
        public const float WhirlReach = 8.5f;
        public const float WhirlCooldown = 8f;
        public const float WhirlDamage = 1.9f;
        public const float FlaskHeal = 0.45f;
        public const float DeathDelay = 2.4f;
        public const float Radius = WorldData.CharacterRadius;

        private const float TurnSpeed = 14f;

        public static HeroStepEvents Step(ref HeroSimState hero, in HeroCommand command, in HeroStats stats, CharacterMover mover)
        {
            const float deltaTime = NetTime.TickSeconds;
            if (command.Has(HeroButtons.Frozen)) return HeroStepEvents.None;

            hero.SwingCooldown = MathF.Max(hero.SwingCooldown - deltaTime, 0f);
            hero.DashCooldown = MathF.Max(hero.DashCooldown - deltaTime, 0f);
            hero.WhirlCooldown = MathF.Max(hero.WhirlCooldown - deltaTime, 0f);
            hero.Safe = MathF.Max(hero.Safe - deltaTime, 0f);
            if (hero.Down) return HeroStepEvents.None;

            var events = HeroStepEvents.None;
            Vector2 aim = command.Aim;
            if (aim.LengthSquared > 1e-4f) hero.Aim = aim.Normalized();

            Vector2 move = command.Move;
            if (move.LengthSquared > 0f) move.Normalize();

            float speed = stats.MoveSpeed;
            if (hero.SwingImpactIn >= 0f || hero.SwingCooldown > 0f) speed *= 0.55f;

            if (command.Has(HeroButtons.Dash) && hero.DashCooldown <= 0f)
            {
                hero.DashDirection = move.LengthSquared > 0f ? move : hero.Aim;
                hero.DashLeft = DashDuration;
                hero.DashCooldown = DashCooldown;
                hero.Safe = DashDuration + 0.1f;
                events |= HeroStepEvents.Dashed;
            }

            Vector2 velocity = move * speed;
            if (hero.DashLeft > 0f)
            {
                hero.DashLeft -= deltaTime;
                velocity = hero.DashDirection * DashSpeed;
            }
            hero.Position = mover.Move(hero.Position, velocity, Radius, deltaTime);

            bool attacking = hero.SwingImpactIn >= 0f || hero.SwingCooldown > 0f;
            Turn(ref hero, attacking ? hero.Aim : move, deltaTime);

            if (command.Has(HeroButtons.Attack) && hero.SwingCooldown <= 0f)
            {
                float rate = stats.AttackSpeed;
                hero.SwingCooldown = SwingDuration / rate;
                hero.SwingImpactIn = SwingImpact / rate;
                events |= HeroStepEvents.Swung;
            }

            if (command.Has(HeroButtons.Whirl) && hero.WhirlCooldown <= 0f)
            {
                hero.WhirlCooldown = WhirlCooldown;
                events |= HeroStepEvents.Whirled;
            }

            if (hero.SwingImpactIn >= 0f)
            {
                hero.SwingImpactIn -= deltaTime;
                if (hero.SwingImpactIn < 0f) events |= HeroStepEvents.Struck;
            }
            return events;
        }

        private static void Turn(ref HeroSimState hero, Vector2 direction, float deltaTime)
        {
            if (direction.LengthSquared < 1e-5f) return;
            float target = MathF.Atan2(direction.X, direction.Y);
            float difference = MathF.IEEERemainder(target - hero.Yaw, MathF.Tau);
            hero.Yaw += difference * Math.Clamp(TurnSpeed * deltaTime, 0f, 1f);
        }
    }
}
