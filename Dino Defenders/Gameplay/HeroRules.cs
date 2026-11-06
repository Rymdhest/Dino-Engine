using Dino_Defenders.Progression;
using Dino_Defenders.Skills;
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
        public int Slots;       // A bit for each skill slot being asked for.
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
        Slot6 = 64,
        Dash = 128,
        Flask = 256,
        Interact = 512,
        Frozen = 1024,  // The game is paused, as someone playing alone can.

        Slots = Slot0 | Slot1 | Slot2 | Slot3 | Slot4 | Slot5 | Slot6,

        /// <summary>The ones that happen once per press, and so must never be lost.</summary>
        Presses = Slot2 | Slot3 | Slot4 | Slot5 | Slot6 | Dash | Flask | Interact,
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
        Used = 1,       // A skill has started. Which one is in HeroSimState.Using.
        Landed = 2,     // Its blow lands now.
        Dashed = 4,
    }

    /// <summary>A number for each skill slot. It's a value, so a hero's state can be copied into the history a client keeps of it.</summary>
    [InlineArray(HeroState.SkillSlots)]
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
            for (int i = 0; i < HeroState.SkillSlots; i++)
            {
                if (Cooldowns[i] > 0f) running |= 1u << i;
            }
            writer.WriteBits(running, HeroState.SkillSlots);
            for (int i = 0; i < HeroState.SkillSlots; i++)
            {
                if ((running & (1u << i)) != 0) writer.WriteFloat(Cooldowns[i]);
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
            writer.WriteVarInt(Counters.Gold);
            writer.WriteByte(Counters.ShrinesKindled);
            writer.WriteByte(Counters.RecentKills);
            writer.WriteByte(Counters.HealthSpent);
            writer.WriteByte(Counters.Minions);
            writer.WriteByte(Counters.AlliesNear);
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

            uint running = reader.ReadBits(HeroState.SkillSlots);
            for (int i = 0; i < HeroState.SkillSlots; i++)
            {
                if ((running & (1u << i)) != 0) state.Cooldowns[i] = reader.ReadFloat();
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
            state.Counters = new Counters
            {
                Gold = reader.ReadVarInt(),
                ShrinesKindled = reader.ReadByte(),
                RecentKills = reader.ReadByte(),
                HealthSpent = reader.ReadByte(),
                Minions = reader.ReadByte(),
                AlliesNear = reader.ReadByte(),
            };
            return state;
        }

        /// <summary>Whether a prediction landed on the same place as what the server says. How they face doesn't affect anything, so it isn't counted.</summary>
        public readonly bool Matches(in HeroSimState other)
        {
            if ((Position - other.Position).LengthSquared >= 1e-6f || Down != other.Down || Moves != other.Moves || Using != other.Using) return false;
            if (MathF.Abs(DashLeft - other.DashLeft) >= 1e-3f || MathF.Abs(ImpactIn - other.ImpactIn) >= 1e-3f || MathF.Abs(BusyFor - other.BusyFor) >= 1e-3f) return false;
            if (MathF.Abs(DodgeCooldown - other.DodgeCooldown) >= 1e-3f || MathF.Abs(Mana - other.Mana) >= 0.05f) return false;
            for (int i = 0; i < HeroState.SkillSlots; i++)
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
        }
    }

    /// <summary>
    /// What a hero has to work with, as of now: their sheet, and what's in each slot. Made when the sheet changes rather than looked up in a tick.
    /// </summary>
    public sealed class HeroKit
    {
        public readonly ActorSheet Sheet;
        public readonly SkillProfile?[] Slots = new SkillProfile?[HeroState.SkillSlots];

        public HeroKit(HeroState hero, ActorSheet sheet)
        {
            Sheet = sheet;
            for (int slot = 0; slot < HeroState.SkillSlots; slot++)
            {
                int index = HeroBuild.SlotIndex(hero, sheet.Loadout, slot);
                if (index < 0) continue;

                SkillProfile profile = sheet.Profile(index);
                Tags needs = profile.Skill.Needs;
                if (needs == Tags.None || (sheet.Loadout.Weapon.Tags & needs) != 0) Slots[slot] = profile;
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

        private const float TurnSpeed = 14f;

        public static HeroButtons SlotButton(int slot) => (HeroButtons)(1 << slot);

        /// <summary>Whether a skill is worth showing on the ground. A plain blow isn't: nobody needs telling what a sword reaches, and there'd be no end of them.</summary>
        public static bool ShowsArea(SkillProfile skill) => skill.DealsDamage && skill.Skill.Form switch
        {
            SkillForm.Strike => skill.Skill.Arc >= 3f || (skill.Skill.Tags & Tags.Area) != 0,
            SkillForm.Nova or SkillForm.Burst or SkillForm.Zone or SkillForm.Beam => true,
            _ => false,
        };

        public static HeroStepEvents Step(ref HeroSimState hero, in HeroCommand command, HeroKit kit, CharacterMover mover)
        {
            const float deltaTime = NetTime.TickSeconds;
            if (command.Has(HeroButtons.Frozen)) return HeroStepEvents.None;

            ActorSheet sheet = kit.Sheet;
            for (int i = 0; i < HeroState.SkillSlots; i++) hero.Cooldowns[i] = MathF.Max(hero.Cooldowns[i] - deltaTime, 0f);
            hero.DodgeCooldown = MathF.Max(hero.DodgeCooldown - deltaTime, 0f);
            hero.DodgeQueuedFor = MathF.Max(hero.DodgeQueuedFor - deltaTime, 0f);
            hero.Safe = MathF.Max(hero.Safe - deltaTime, 0f);
            hero.QueuedFor = MathF.Max(hero.QueuedFor - deltaTime, 0f);
            hero.Held = MathF.Max(hero.Held - deltaTime, 0f);
            hero.Mana = MathF.Min(hero.Mana + sheet.ManaRegen * deltaTime, sheet.MaxMana);
            if (hero.Down) return HeroStepEvents.None;

            // Pressed even with no way of acting on it: someone stunned who hits dodge wants to dodge the moment they can.
            if (command.Has(HeroButtons.Dash)) hero.DodgeQueuedFor = DodgeQueueTime;
            HeroButtons pressed = command.Buttons & ~hero.Pressing;
            hero.Pressing = command.Buttons;

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
            if (hero.Using >= 0 && kit.Slots[hero.Using] is SkillProfile inHand) speed *= Slowed(hero, inHand);

            // Nothing gets in the way of a dodge. Whatever they were doing is over, and if the blow hadn't landed it doesn't: getting out of there was worth more.
            if (hero.DodgeQueuedFor > 0f && hero.DodgeCooldown <= 0f)
            {
                hero.DashDirection = move.LengthSquared > 0f ? move : hero.Aim;
                hero.DashSpeed = sheet.DodgeSpeed;
                hero.DashLeft = DashDuration;
                hero.DodgeCooldown = sheet.DodgeCooldown;
                hero.DodgeQueuedFor = 0f;
                hero.Safe = DashDuration + 0.1f;
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

            for (int slot = 2; slot < HeroState.SkillSlots; slot++)
            {
                if (!command.Has(SlotButton(slot))) continue;
                hero.Queued = (sbyte)slot;
                hero.QueuedFor = QueueTime;
            }

            // A click made while busy counts as well, and comes straight after. Only the click: a button held down simply goes on going, and letting go of it stops it.
            for (int slot = 1; slot >= 0; slot--)
            {
                if ((pressed & SlotButton(slot)) == 0 || hero.Using < 0 || hero.QueuedFor > 0f) continue;
                hero.Queued = (sbyte)slot;
                hero.QueuedFor = QueueTime;
            }

            if (hero.Using < 0)
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
        private static float Slowed(in HeroSimState hero, SkillProfile skill)
        {
            float slowed = skill.Skill.MoveFactor;
            if (hero.ImpactIn >= 0f) return slowed;
            float left = hero.BusyFor / MathF.Max(skill.UseTime - skill.ImpactTime, 0.01f);
            return slowed + (1f - slowed) * (1f - Math.Clamp(left, 0f, 1f));
        }

        /// <summary>
        /// Whether the key on a slot presses it this frame. What a key sends is a press, so that a quick tap is never lost, which means one held
        /// down has to press again for its skill to repeat as a mouse button's does. It waits until the skill could go: one on cooldown would
        /// otherwise keep the queue from a second key held with it.
        /// </summary>
        public static bool KeyPresses(in HeroSimState hero, int slot, bool down, bool justPressed) =>
            justPressed || down && hero.Using < 0 && hero.Cooldowns[slot] <= 0f;

        private static bool TryUse(ref HeroSimState hero, HeroKit kit, int slot, ref HeroStepEvents events)
        {
            if (slot < 0 || slot >= HeroState.SkillSlots || kit.Slots[slot] is not SkillProfile skill || hero.Cooldowns[slot] > 0f) return false;

            if (skill.CostsHealth)
            {
                if (hero.Health <= skill.Cost) return false;
                hero.Health -= skill.Cost;
            }
            else
            {
                if (hero.Mana < skill.Cost) return false;
                hero.Mana -= skill.Cost;
            }

            hero.Using = (sbyte)slot;
            hero.ImpactIn = skill.ImpactTime;
            hero.BusyFor = skill.UseTime;
            hero.Cooldowns[slot] = skill.Cooldown;
            events |= HeroStepEvents.Used;

            // A leap is carried out here, as moving always is. It lands when it arrives.
            if (skill.Skill.Form == SkillForm.Leap)
            {
                float distance = MathF.Min(MathF.Max(hero.AimReach, 2f), skill.Range);
                hero.DashDirection = hero.Aim;
                hero.DashSpeed = skill.Skill.LeapSpeed;
                hero.DashLeft = distance / skill.Skill.LeapSpeed;
                hero.ImpactIn = hero.DashLeft;
                hero.BusyFor = MathF.Max(hero.DashLeft, skill.UseTime);
            }
            return true;
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
