using Dino_Defenders.Books;
using Dino_Defenders.Progression;
using Dino_Defenders.Stats;
using Dino_Engine.Networking;
using Dino_Engine.World;
using OpenTK.Mathematics;
using System.Runtime.CompilerServices;

namespace Dino_Defenders.Gameplay
{
    /// <summary>What the player means to do this frame, from the keyboard and mouse or from whatever is driving the hero instead.</summary>
    public struct HeroIntent
    {
        public Vector2 Move;
        public Vector2? Aim;
        public int Slots;       // A bit for each place on the bar being asked for.
        public bool Dash;
        public bool Flask;
        public bool Interact;
        public bool Guide;

        public void Use(int slot) => Slots |= 1 << slot;
    }

    [Flags]
    public enum HeroButtons : ushort
    {
        None = 0,
        Slot0 = 1,      // Held, like Slot1: the mouse buttons.
        Slot1 = 2,
        Slot2 = 4,      // Pressed, like the rest.
        Slot3 = 8,
        Slot4 = 16,
        Slot5 = 32,
        Dash = 128,
        Flask = 256,
        Interact = 512,
        Frozen = 1024,  // The game is paused, as someone playing alone can.

        Slots = Slot0 | Slot1 | Slot2 | Slot3 | Slot4 | Slot5,

        /// <summary>The ones that happen once per press, and so must never be lost.</summary>
        Presses = Slot2 | Slot3 | Slot4 | Slot5 | Dash | Flask | Interact,
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

            command.Buttons |= (HeroButtons)(intent.Slots & (int)HeroButtons.Slots);
            if (intent.Dash) command.Buttons |= HeroButtons.Dash;
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
            writer.WriteBits((ushort)Buttons, 16);
            writer.WriteByte(ViewLag);
        }

        public static HeroCommand Read(BitReader reader, uint tick) => new HeroCommand
        {
            Tick = tick,
            MoveX = (sbyte)reader.ReadByte(),
            MoveZ = (sbyte)reader.ReadByte(),
            AimX = (short)reader.ReadBits(16),
            AimZ = (short)reader.ReadBits(16),
            Buttons = (HeroButtons)reader.ReadBits(16),
            ViewLag = reader.ReadByte(),
        };
    }

    [Flags]
    public enum HeroStepEvents
    {
        None = 0,
        Used = 1,       // A book has started. Which one is in HeroSimState.Using.
        Landed = 2,     // Its blow lands now.
        Dashed = 4,
    }

    /// <summary>A number for each place on the bar. It's a value, so a hero's state can be copied into the history a client keeps of it.</summary>
    [InlineArray(HeroState.BarSlots)]
    public struct SlotTimes
    {
        private float _first;
    }

    /// <summary>
    /// Everything about a hero that a tick can change. The client predicts this and the server has the last word on it, so it is
    /// sent whole and unrounded: a client that lands on the same numbers knows it guessed right, and otherwise has what it needs to
    /// correct itself. The last group the server alone sets, and the client takes as it finds them: that's what lets a chill or a freeze
    /// slow a hero without either end having to guess.
    /// </summary>
    public struct HeroSimState
    {
        public const sbyte NoSlot = -1;

        public Vector2 Position;
        public float Yaw;
        public Vector2 Aim;
        public float AimReach;
        public Vector2 DashDirection;
        public float DashSpeed;
        public float DashLeft;
        public sbyte Using;             // The slot being used, while it's being used.
        public float ImpactIn;          // Below 0 when no blow is on its way.
        public float BusyFor;
        public SlotTimes Cooldowns;
        public byte Toggled;            // A bit for each place on the bar whose Toggle Book is on.
        public float DodgeCooldown;
        public float DodgeQueuedFor;    // A dodge pressed a moment too soon, which goes the moment it can.
        public float Safe;              // Nothing can touch them for this long: a dodge's window of safety.
        public sbyte Queued;            // A press made too early, held on to for a moment.
        public float QueuedFor;
        public HeroButtons Pressing;    // What was held last tick, to tell a click from a button still held.
        public float Mana;
        public bool Down;
        public byte Moves;              // Goes up each time the server puts them somewhere else, so it isn't smoothed over.

        public float Health;
        public float Held;              // Stunned or frozen for this long.
        public float MoveRate;
        public float ActionRate;
        public Condition State;
        public Counters Counters;
        public SlotTimes Charged;       // How far each Trigger Book has got, from 0 to 1. Only the server sees what they're waiting for.

        public static HeroSimState At(Vector2 position, float mana) => new HeroSimState
        {
            Position = position,
            Aim = Vector2.UnitY,
            Using = NoSlot,
            Queued = NoSlot,
            ImpactIn = -1f,
            Mana = mana,
            MoveRate = 1f,
            ActionRate = 1f,
        };

        public readonly void Write(BitWriter writer)
        {
            writer.WriteFloat(Position.X);
            writer.WriteFloat(Position.Y);
            writer.WriteFloat(Yaw);
            writer.WriteFloat(Aim.X);
            writer.WriteFloat(Aim.Y);
            writer.WriteFloat(AimReach);
            writer.WriteFloat(DashDirection.X);
            writer.WriteFloat(DashDirection.Y);
            writer.WriteFloat(DashSpeed);
            writer.WriteFloat(DashLeft);
            writer.WriteByte((byte)Using);
            writer.WriteFloat(ImpactIn);
            writer.WriteFloat(BusyFor);

            // Most of these are nothing, most of the time.
            uint running = 0;
            for (int i = 0; i < HeroState.BarSlots; i++)
            {
                if (Cooldowns[i] > 0f) running |= 1u << i;
            }
            writer.WriteBits(running, HeroState.BarSlots);
            for (int i = 0; i < HeroState.BarSlots; i++)
            {
                if ((running & (1u << i)) != 0) writer.WriteFloat(Cooldowns[i]);
            }
            writer.WriteByte(Toggled);

            uint charging = 0;
            for (int i = 0; i < HeroState.BarSlots; i++)
            {
                if (Charged[i] > 0f) charging |= 1u << i;
            }
            writer.WriteBits(charging, HeroState.BarSlots);
            for (int i = 0; i < HeroState.BarSlots; i++)
            {
                if ((charging & (1u << i)) != 0) writer.WriteFloat(Charged[i]);
            }

            writer.WriteFloat(DodgeCooldown);
            writer.WriteFloat(DodgeQueuedFor);
            writer.WriteFloat(Safe);
            writer.WriteByte((byte)Queued);
            writer.WriteFloat(QueuedFor);
            writer.WriteBits((ushort)Pressing, 16);
            writer.WriteFloat(Mana);
            writer.WriteBool(Down);
            writer.WriteByte(Moves);

            writer.WriteFloat(Health);
            writer.WriteFloat(Held);
            writer.WriteFloat(MoveRate);
            writer.WriteFloat(ActionRate);
            writer.WriteVarULong((ulong)State);
            Counters.Write(writer);
        }

        public static HeroSimState Read(BitReader reader)
        {
            var state = new HeroSimState
            {
                Position = new Vector2(reader.ReadFloat(), reader.ReadFloat()),
                Yaw = reader.ReadFloat(),
                Aim = new Vector2(reader.ReadFloat(), reader.ReadFloat()),
                AimReach = reader.ReadFloat(),
                DashDirection = new Vector2(reader.ReadFloat(), reader.ReadFloat()),
                DashSpeed = reader.ReadFloat(),
                DashLeft = reader.ReadFloat(),
                Using = (sbyte)reader.ReadByte(),
                ImpactIn = reader.ReadFloat(),
                BusyFor = reader.ReadFloat(),
            };

            uint running = reader.ReadBits(HeroState.BarSlots);
            for (int i = 0; i < HeroState.BarSlots; i++)
            {
                if ((running & (1u << i)) != 0) state.Cooldowns[i] = reader.ReadFloat();
            }
            state.Toggled = reader.ReadByte();

            uint charging = reader.ReadBits(HeroState.BarSlots);
            for (int i = 0; i < HeroState.BarSlots; i++)
            {
                if ((charging & (1u << i)) != 0) state.Charged[i] = reader.ReadFloat();
            }

            state.DodgeCooldown = reader.ReadFloat();
            state.DodgeQueuedFor = reader.ReadFloat();
            state.Safe = reader.ReadFloat();
            state.Queued = (sbyte)reader.ReadByte();
            state.QueuedFor = reader.ReadFloat();
            state.Pressing = (HeroButtons)reader.ReadBits(16);
            state.Mana = reader.ReadFloat();
            state.Down = reader.ReadBool();
            state.Moves = reader.ReadByte();

            state.Health = reader.ReadFloat();
            state.Held = reader.ReadFloat();
            state.MoveRate = reader.ReadFloat();
            state.ActionRate = reader.ReadFloat();
            state.State = (Condition)reader.ReadVarULong();
            state.Counters = Counters.Read(reader);
            return state;
        }

        /// <summary>Whether a prediction landed on the same place as what the server says. How they face doesn't affect anything, so it isn't counted.</summary>
        public readonly bool Matches(in HeroSimState other)
        {
            if ((Position - other.Position).LengthSquared >= 1e-6f || Down != other.Down || Moves != other.Moves || Using != other.Using || Toggled != other.Toggled) return false;
            if (MathF.Abs(DashLeft - other.DashLeft) >= 1e-3f || MathF.Abs(ImpactIn - other.ImpactIn) >= 1e-3f || MathF.Abs(BusyFor - other.BusyFor) >= 1e-3f) return false;
            if (MathF.Abs(DodgeCooldown - other.DodgeCooldown) >= 1e-3f || MathF.Abs(Mana - other.Mana) >= 0.05f) return false;
            for (int i = 0; i < HeroState.BarSlots; i++)
            {
                if (MathF.Abs(Cooldowns[i] - other.Cooldowns[i]) >= 1e-3f) return false;
            }
            return true;
        }

        /// <summary>Takes on what only the server decides, which is never a wrong guess.</summary>
        public void TakeOrders(in HeroSimState truth)
        {
            Health = truth.Health;
            Held = truth.Held;
            MoveRate = truth.MoveRate;
            ActionRate = truth.ActionRate;
            State = truth.State;
            Counters = truth.Counters;
            Charged = truth.Charged;
        }
    }

    /// <summary>
    /// What a hero has to work with, as of now: their sheet, and what's in each slot. Made when the sheet changes rather than looked up in a tick.
    /// </summary>
    public sealed class HeroKit
    {
        public readonly ActorSheet Sheet;
        public readonly BookProfile?[] Slots = new BookProfile?[HeroState.BarSlots];

        public HeroKit(HeroState hero, ActorSheet sheet)
        {
            Sheet = sheet;
            for (int slot = 0; slot < HeroState.BarSlots; slot++)
            {
                int index = HeroBuild.SlotIndex(hero, slot);
                if (index >= 0 && index < sheet.Loadout.Books.Length) Slots[slot] = sheet.Profile(index);
            }
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
        public const float DashDuration = 0.2f;
        public const float FlaskHeal = 0.45f;
        public const float DeathDelay = 2.4f;
        public const float Radius = WorldData.CharacterRadius;
        public const float QueueTime = 0.3f;
        public const float DodgeQueueTime = 0.18f;

        /// <summary>How many places on the bar go on for as long as they're held, rather than once for each press.</summary>
        public const int HeldSlots = 2;

        private const float TurnSpeed = 14f;

        public static HeroButtons SlotButton(int slot) => (HeroButtons)(1 << slot);

        public static HeroStepEvents Step(ref HeroSimState hero, in HeroCommand command, HeroKit kit, CharacterMover mover)
        {
            const float deltaTime = NetTime.TickSeconds;
            if (command.Has(HeroButtons.Frozen)) return HeroStepEvents.None;

            ActorSheet sheet = kit.Sheet;
            for (int i = 0; i < HeroState.BarSlots; i++) hero.Cooldowns[i] = MathF.Max(hero.Cooldowns[i] - deltaTime, 0f);
            hero.DodgeCooldown = MathF.Max(hero.DodgeCooldown - deltaTime, 0f);
            hero.DodgeQueuedFor = MathF.Max(hero.DodgeQueuedFor - deltaTime, 0f);
            hero.Safe = MathF.Max(hero.Safe - deltaTime, 0f);
            hero.QueuedFor = MathF.Max(hero.QueuedFor - deltaTime, 0f);
            hero.Held = MathF.Max(hero.Held - deltaTime, 0f);
            Settle(ref hero, kit);
            hero.Mana = MathF.Min(hero.Mana + (sheet.ManaRegen - Reserved(hero, kit)) * deltaTime, sheet.MaxMana);
            if (hero.Down) return HeroStepEvents.None;

            // Pressed even with no way of acting on it: someone stunned who hits dodge wants to dodge the moment they can.
            if (command.Has(HeroButtons.Dash)) hero.DodgeQueuedFor = DodgeQueueTime;
            HeroButtons pressed = command.Buttons & ~hero.Pressing;
            hero.Pressing = command.Buttons;
            Flip(ref hero, kit, pressed);

            var events = HeroStepEvents.None;
            Vector2 aim = command.Aim;
            if (aim.LengthSquared > 1e-4f)
            {
                hero.AimReach = aim.Length;
                hero.Aim = aim / hero.AimReach;
            }

            // Stunned or frozen, nothing happens, and whatever was starting goes with it.
            if (hero.Held > 0f)
            {
                hero.Using = HeroSimState.NoSlot;
                hero.ImpactIn = -1f;
                hero.BusyFor = 0f;
                hero.DashLeft = 0f;
                return events;
            }

            Vector2 move = command.Move;
            if (move.LengthSquared > 0f) move.Normalize();

            float speed = sheet.MoveSpeed * hero.MoveRate;
            if (hero.Using >= 0 && kit.Slots[hero.Using] is BookProfile inHand) speed *= Slowed(hero, inHand);

            // Nothing gets in the way of a dodge. Whatever they were doing is over, and if the blow hadn't landed it doesn't: getting out of there was worth more.
            if (hero.DodgeQueuedFor > 0f && hero.DodgeCooldown <= 0f && PayForDodge(ref hero, sheet))
            {
                hero.DashDirection = move.LengthSquared > 0f ? move : hero.Aim;
                hero.DashSpeed = sheet.DodgeSpeed;
                hero.DashLeft = DashDuration;
                hero.DodgeCooldown = sheet.DodgeCooldown;
                hero.DodgeQueuedFor = 0f;
                hero.Safe = sheet.Has(Rule.NoSafeDodge) ? 0f : DashDuration + Rulebook.DodgeSafeExtra;
                hero.Using = HeroSimState.NoSlot;
                hero.ImpactIn = -1f;
                hero.BusyFor = 0f;
                events |= HeroStepEvents.Dashed;
            }

            Vector2 velocity = move * speed;
            if (hero.DashLeft > 0f)
            {
                hero.DashLeft -= deltaTime;
                velocity = hero.DashDirection * hero.DashSpeed;
            }
            hero.Position = mover.Move(hero.Position, velocity, Radius, deltaTime);

            Turn(ref hero, hero.Using >= 0 ? hero.Aim : move, deltaTime);

            float action = deltaTime * hero.ActionRate;
            hero.BusyFor = MathF.Max(hero.BusyFor - action, 0f);
            if (hero.ImpactIn >= 0f)
            {
                hero.ImpactIn -= action;
                if (hero.ImpactIn < 0f) events |= HeroStepEvents.Landed;
            }

            // What's landed stays in hand for this tick, so that whoever reads the events can see what it was.
            if (hero.BusyFor <= 0f && hero.ImpactIn < 0f && (events & HeroStepEvents.Landed) == 0) hero.Using = HeroSimState.NoSlot;

            for (int slot = 2; slot < HeroState.BarSlots; slot++)
            {
                if (!command.Has(SlotButton(slot)) || !ByHand(kit, slot)) continue;
                hero.Queued = (sbyte)slot;
                hero.QueuedFor = QueueTime;
            }

            // A click made while busy counts as well, and comes straight after. Only the click: a button held down simply goes on going, and letting go of it stops it.
            // A dodge is a clean break from everything, and that includes what's still held down, which picks up again when it's over.
            bool dodging = hero.Using < 0 && hero.DashLeft > 0f;
            for (int slot = 1; slot >= 0; slot--)
            {
                if ((pressed & SlotButton(slot)) == 0 || (hero.Using < 0 && !dodging) || hero.QueuedFor > 0f || !ByHand(kit, slot)) continue;
                hero.Queued = (sbyte)slot;
                hero.QueuedFor = QueueTime;
            }

            if (hero.Using < 0 && !dodging)
            {
                if (hero.QueuedFor > 0f && TryUse(ref hero, kit, hero.Queued, ref events))
                {
                    hero.QueuedFor = 0f;
                }
                else if (command.Has(HeroButtons.Slot1) && TryUse(ref hero, kit, 1, ref events))
                {
                }
                else if (command.Has(HeroButtons.Slot0))
                {
                    TryUse(ref hero, kit, 0, ref events);
                }
            }
            return events;
        }

        // How much a use slows someone down: all of it while it's going, and less and less once it's landed, so that a hero who's struck can get away at once.
        private static float Slowed(in HeroSimState hero, BookProfile book)
        {
            float slowed = book.MoveFactor;
            if (hero.ImpactIn >= 0f) return slowed;
            float left = hero.BusyFor / MathF.Max(book.UseTime - book.ImpactTime, 0.01f);
            return slowed + (1f - slowed) * (1f - Math.Clamp(left, 0f, 1f));
        }

        /// <summary>
        /// Whether the key on a slot presses it this frame. What a key sends is a press, so that a quick tap is never lost, which means one held
        /// down has to press again for its book to repeat as a mouse button's does. It waits until the book could go: one on cooldown would
        /// otherwise keep the queue from a second key held with it.
        /// </summary>
        public static bool KeyPresses(in HeroSimState hero, int slot, bool down, bool justPressed) =>
            justPressed || down && hero.Using < 0 && hero.Cooldowns[slot] <= 0f;

        // Whether a dodge can be had, and paying for it if so.
        private static bool PayForDodge(ref HeroSimState hero, ActorSheet sheet)
        {
            if (sheet.Has(Rule.NoDodge)) return false;
            if (!sheet.Has(Rule.DodgeCostsMana)) return true;

            float cost = sheet.MaxMana * Rulebook.DodgeManaShare;
            if (hero.Mana < cost) return false;
            hero.Mana -= cost;
            return true;
        }

        /// <summary>Whether a book could be paid for as things stand. Gold and minions are counted by the server, which takes them, so this goes by what it last said.</summary>
        public static bool CanPay(in HeroSimState hero, ActorSheet sheet, BookProfile book)
        {
            if (book.CostsMinion && hero.Counters.Minions == 0) return false;
            if (book.CostsGold) return hero.Counters.Gold >= book.Cost * Rulebook.GoldPerCost;
            if (book.CostsHealth) return hero.Health > book.Cost;
            return hero.Mana - book.Cost >= (sheet.Has(Rule.Overdraw) ? -sheet.MaxMana : 0f);
        }

        private static bool TryUse(ref HeroSimState hero, HeroKit kit, int slot, ref HeroStepEvents events)
        {
            if (slot < 0 || slot >= HeroState.BarSlots || kit.Slots[slot] is not BookProfile { Mode: BookMode.Active } book || hero.Cooldowns[slot] > 0f) return false;

            ActorSheet sheet = kit.Sheet;
            if (sheet.Has(Rule.NoActive) || !CanPay(hero, sheet, book)) return false;

            // Gold comes off on the server, which has the last word on what's in anyone's pocket.
            if (book.CostsHealth && !book.CostsGold) hero.Health -= book.Cost;
            else if (!book.CostsGold) hero.Mana = sheet.Has(Rule.SpendsAll) ? MathF.Min(hero.Mana - book.Cost, 0f) : hero.Mana - book.Cost;

            hero.Using = (sbyte)slot;
            hero.ImpactIn = book.ImpactTime;
            hero.BusyFor = book.UseTime;
            hero.Cooldowns[slot] = book.Cooldown;
            events |= HeroStepEvents.Used;

            // Riding what's let go of is carried out here, as moving always is. It lands when it arrives.
            if (book.Rides)
            {
                float speed = MathF.Max(book.First.Speed, 1f);
                float distance = MathF.Min(MathF.Max(hero.AimReach, 2f), book.First.Range);
                hero.DashDirection = hero.Aim;
                hero.DashSpeed = speed;
                hero.DashLeft = distance / speed;
                hero.ImpactIn = hero.DashLeft;
                hero.BusyFor = MathF.Max(hero.DashLeft, book.UseTime);
            }
            return true;
        }

        // Whether a place on the bar is something a press uses. Nothing is, with nothing there, but that's for the press to find out.
        private static bool ByHand(HeroKit kit, int slot) => kit.Slots[slot] is not { Mode: not BookMode.Active };

        public static bool IsOn(in HeroSimState hero, int slot) => (hero.Toggled & 1 << slot) != 0;

        /// <summary>The Mana Regeneration given up to what's turned on.</summary>
        public static float Reserved(in HeroSimState hero, HeroKit kit)
        {
            float reserved = 0f;
            for (int slot = 0; slot < HeroState.BarSlots; slot++)
            {
                if (IsOn(hero, slot) && kit.Slots[slot] is BookProfile book) reserved += book.Cost;
            }
            return reserved;
        }

        /// <summary>Whether a Toggle Book could be turned on as things stand: there has to be more left to give up than it asks for.</summary>
        public static bool CanTurnOn(in HeroSimState hero, HeroKit kit, BookProfile book) => kit.Sheet.ManaRegen - Reserved(hero, kit) > book.Cost;

        // A press on a Toggle Book. It's only ever the press, as a button held down would otherwise go on and off with every tick.
        private static void Flip(ref HeroSimState hero, HeroKit kit, HeroButtons pressed)
        {
            for (int slot = 0; slot < HeroState.BarSlots; slot++)
            {
                if ((pressed & SlotButton(slot)) == 0 || kit.Slots[slot] is not BookProfile { Mode: BookMode.Toggle } book) continue;
                if (IsOn(hero, slot)) hero.Toggled &= (byte)~(1 << slot);
                else if (CanTurnOn(hero, kit, book)) hero.Toggled |= (byte)(1 << slot);
            }
        }

        // What's on has to be there and has to be paid for. Taking off what gave the regeneration turns things off, the last first, until what's left can be kept up.
        private static void Settle(ref HeroSimState hero, HeroKit kit)
        {
            for (int slot = HeroState.BarSlots - 1; slot >= 0; slot--)
            {
                if (!IsOn(hero, slot)) continue;
                bool gone = kit.Slots[slot] is not { Mode: BookMode.Toggle };
                if (gone || Reserved(hero, kit) >= kit.Sheet.ManaRegen) hero.Toggled &= (byte)~(1 << slot);
            }
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
