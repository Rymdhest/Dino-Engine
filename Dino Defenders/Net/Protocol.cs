using Dino_Defenders.Gameplay;
using Dino_Defenders.Progression;
using Dino_Engine.Networking;
using Dino_Engine.World;

namespace Dino_Defenders.Net
{
    /// <summary>What a message is. Everything starts with one of these.</summary>
    public enum Message : byte
    {
        // Client to server.
        Hello,          // Reliable: who I am and who I bring.
        WorldReady,     // Reliable: I have the world and am ready to step into it.
        Commands,       // Unreliable: what my hero is doing, and what I have received.
        Action,         // Reliable: something done at a panel.

        // Server to client.
        Welcome,        // Reliable: you're in, and this is the world.
        Refused,        // Reliable: you aren't, and here's why.
        WorldChange,    // Reliable: the party is moving on to another world.
        Arrived,        // Reliable: you're standing in it now.
        Snapshot,       // Unreliable: how things stand.
        HeroUpdate,     // Reliable: your hero, as of now.
        Progress,       // Reliable: what has happened to the world.
        Notice,         // Reliable: a banner or a line in the log.
        OpenPanel,      // Reliable: you've opened something.
        Market,         // Reliable: what a merchant has for you.
        Party,          // Reliable: who else is here.
    }

    public enum HeroAction : byte
    {
        RaiseAttribute,
        Equip,
        Unequip,
        Sell,
        Buy,
        BuyFlasks,
        TravelToSite,
        TravelToPoint,  // Only for scripted runs, where the server allows it.
        Waygate,
        ThankElder,
    }

    public enum HeroAttribute : byte
    {
        Might,
        Finesse,
        Spirit,
    }

    public enum NoticeKind : byte
    {
        Banner,
        Log,
    }

    /// <summary>
    /// The conversation between client and server, one method per message so that the two ends can't disagree about what is in one.
    /// Only snapshots and commands, the hot path, are unreliable; anything said once, and meant, goes reliably.
    /// </summary>
    public static class Protocol
    {
        /// <summary>Goes up with any change to what's written here. Component changes are caught by <see cref="NetSchema.Hash"/>.</summary>
        public const uint Version = 1;

        public const int MaxPlayers = 5;
        public const int MostCommands = 12;
        public const int LongestName = 64;
        public const int LongestText = 512;
        public const int MostItems = 128;
        public const int SnapshotBudget = 1150;     // Bytes: inside the smallest MTU worth planning for.

        public static BitWriter Begin(Message type)
        {
            var writer = new BitWriter(64);
            writer.WriteByte((byte)type);
            return writer;
        }

        public static void WriteRequest(BitWriter writer, in WorldRequest request)
        {
            writer.WriteInt(request.Seed);
            writer.WriteBool(request.ArchetypeId != null);
            if (request.ArchetypeId != null) writer.WriteString(request.ArchetypeId);
            writer.WriteVarInt(request.Level);
        }

        public static WorldRequest ReadRequest(BitReader reader)
        {
            int seed = reader.ReadInt();
            string? archetype = reader.ReadBool() ? reader.ReadString(LongestName) : null;
            int level = reader.ReadVarInt();
            if (level < 1 || level > 100000) throw new NetReadException($"A world of level {level}.");
            return new WorldRequest(seed, archetype, level);
        }

        public static void WriteHeroSummary(BitWriter writer, HeroState hero)
        {
            writer.WriteVarInt(hero.Level);
            writer.WriteVarInt(hero.Experience);
            writer.WriteVarInt(hero.Gold);
            writer.WriteVarInt(hero.Might);
            writer.WriteVarInt(hero.Finesse);
            writer.WriteVarInt(hero.Spirit);
            writer.WriteVarInt(hero.UnspentPoints);
            writer.WriteVarInt(hero.Flasks);
            writer.WriteVarInt(hero.WorldsCleared);
        }

        public static void ReadHeroSummary(BitReader reader, HeroState hero)
        {
            hero.Level = Math.Clamp(reader.ReadVarInt(), 1, HeroState.MaxLevel);
            hero.Experience = Math.Max(reader.ReadVarInt(), 0);
            hero.Gold = Math.Max(reader.ReadVarInt(), 0);
            hero.Might = Math.Max(reader.ReadVarInt(), 0);
            hero.Finesse = Math.Max(reader.ReadVarInt(), 0);
            hero.Spirit = Math.Max(reader.ReadVarInt(), 0);
            hero.UnspentPoints = Math.Max(reader.ReadVarInt(), 0);
            hero.Flasks = Math.Clamp(reader.ReadVarInt(), 0, HeroState.MaxFlasks);
            hero.WorldsCleared = Math.Max(reader.ReadVarInt(), 0);
        }

        public static void WriteGear(BitWriter writer, HeroState hero)
        {
            writer.WriteCount(hero.Inventory.Count);
            foreach (Item item in hero.Inventory) WriteItem(writer, item);
            writer.WriteCount(hero.Equipped.Count);
            foreach (Item item in hero.Equipped.Values) WriteItem(writer, item);
        }

        public static void ReadGear(BitReader reader, HeroState hero)
        {
            var carried = new List<Item>();
            int count = reader.ReadCount(MostItems);
            for (int i = 0; i < count; i++) carried.Add(ReadItem(reader));

            var worn = new Dictionary<ItemSlot, Item>();
            count = reader.ReadCount(MostItems);
            for (int i = 0; i < count; i++)
            {
                Item item = ReadItem(reader);
                worn[item.Slot] = item;
            }

            hero.Inventory = carried;
            hero.Equipped = worn;
        }

        public static void WriteHero(BitWriter writer, HeroState hero)
        {
            WriteHeroSummary(writer, hero);
            WriteGear(writer, hero);
        }

        public static HeroState ReadHero(BitReader reader)
        {
            var hero = new HeroState();
            ReadHeroSummary(reader, hero);
            ReadGear(reader, hero);
            return hero;
        }

        public static void WriteItems(BitWriter writer, IReadOnlyCollection<Item> items)
        {
            writer.WriteCount(items.Count);
            foreach (Item item in items) WriteItem(writer, item);
        }

        public static List<Item> ReadItems(BitReader reader)
        {
            var items = new List<Item>();
            int count = reader.ReadCount(MostItems);
            for (int i = 0; i < count; i++) items.Add(ReadItem(reader));
            return items;
        }

        private static void WriteItem(BitWriter writer, Item item)
        {
            writer.WriteString(item.Name);
            writer.WriteByte((byte)item.Slot);
            writer.WriteByte((byte)item.Rarity);
            writer.WriteVarInt(item.Level);
            writer.WriteFloat(item.WeaponDamage);
            writer.WriteFloat(item.Armour);
            writer.WriteCount(item.Modifiers.Count);
            foreach (Modifier modifier in item.Modifiers)
            {
                writer.WriteByte((byte)modifier.Stat);
                writer.WriteFloat(modifier.Value);
            }
        }

        private static Item ReadItem(BitReader reader)
        {
            var item = new Item
            {
                Name = reader.ReadString(LongestName),
                Slot = Checked<ItemSlot>(reader.ReadByte()),
                Rarity = Checked<Rarity>(reader.ReadByte()),
                Level = reader.ReadVarInt(),
                WeaponDamage = Finite(reader.ReadFloat()),
                Armour = Finite(reader.ReadFloat()),
            };

            int modifiers = reader.ReadCount(16);
            for (int i = 0; i < modifiers; i++)
            {
                item.Modifiers.Add(new Modifier { Stat = Checked<Stat>(reader.ReadByte()), Value = Finite(reader.ReadFloat()) });
            }
            return item;
        }

        // Anyone may be at the other end. Cheating is their own affair, but a number that breaks the game for everyone isn't.
        private static float Finite(float value)
        {
            if (!float.IsFinite(value)) throw new NetReadException("A number that isn't one.");
            return value;
        }

        private static T Checked<T>(byte value) where T : struct, Enum
        {
            T result = (T)Enum.ToObject(typeof(T), value);
            if (!Enum.IsDefined(result)) throw new NetReadException($"{value} isn't a {typeof(T).Name}.");
            return result;
        }

        /// <summary>
        /// What a world came out as, for checking that two machines grew the same one. Its structure has to match exactly, as sites and
        /// spawns are referred to by number. The ground only has to match closely, as what it can cost is a correction here and there.
        /// </summary>
        public static (uint structure, uint ground) WorldHash(WorldData world)
        {
            uint structure = 2166136261u;
            void Mix(ref uint hash, uint value)
            {
                hash ^= value;
                hash *= 16777619u;
            }

            Mix(ref structure, (uint)world.Sites.Count);
            foreach (WorldSite site in world.Sites)
            {
                Mix(ref structure, (uint)site.Kind);
                Mix(ref structure, (uint)site.Spawns.Count);
            }
            Mix(ref structure, (uint)world.Roads.Count);

            uint ground = 2166136261u;
            foreach (byte value in world.Walkable.Data) Mix(ref ground, value);
            foreach (byte value in world.Passable.Data) Mix(ref ground, value);
            Mix(ref ground, (uint)world.Placement.Count);
            return (structure, ground);
        }
    }
}
