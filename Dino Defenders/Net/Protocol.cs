using Dino_Defenders.Gameplay;
using Dino_Defenders.Progression;
using Dino_Defenders.Books;
using Dino_Defenders.Stats;
using Dino_Defenders.Talents;
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
        GroundItem,     // Reliable: what a find on the ground is.
        Worn,           // Reliable: what someone has on, for drawing them.
    }

    public enum HeroAction : byte
    {
        Allocate,
        Refund,
        ForgetTalents,
        Equip,
        Unequip,
        Sell,
        Buy,
        BuyFlasks,
        TravelToSite,
        TravelToPoint,  // Only for scripted runs, where the server allows it.
        Waygate,
        ThankElder,
        PickUp,
        Move,
        Drop,
        Sort,
        Write,
        Title,
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
        public const uint Version = 9;

        /// <summary>
        /// Which build this is, compared at the door. The schema hash only sees replicated components: two builds with different rules
        /// but the same components would connect and disagree about every step. With updates going out, that would be every other game.
        /// </summary>
        public static readonly uint Build = NetSchema.Fnv(BuildInfo.Version);

        public const int MaxPlayers = 5;
        public const int MostCommands = 12;
        public const int LongestName = 64;
        private const int MostDepth = 2;
        public const int LongestText = 512;
        public const int MostItems = 128;
        public const int MostLines = 16;
        public const int MostBuffs = 64;
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

        public static void WriteOffering(BitWriter writer, Offering offering)
        {
            writer.WriteVarInt(offering.Peril);
            writer.WriteVarInt(offering.Teeming);
            writer.WriteVarInt(offering.Overrun);
            var omens = offering.Chosen().ToList();
            writer.WriteCount(omens.Count);
            foreach (OmenDefinition omen in omens) writer.WriteVarInt(omen.Number);
        }

        public static Offering ReadOffering(BitReader reader)
        {
            var offering = new Offering { Peril = reader.ReadVarInt(), Teeming = reader.ReadVarInt(), Overrun = reader.ReadVarInt() };
            if (offering.Peril < 0 || offering.Peril > Offering.MostPeril) throw new NetReadException($"An offering of peril {offering.Peril}.");
            if (offering.Teeming < 0 || offering.Teeming > Offering.MostCrowd || offering.Overrun < 0 || offering.Overrun > Offering.MostCrowd) throw new NetReadException("An offering of more than there can be.");

            int count = reader.ReadCount(Omens.All.Count);
            for (int i = 0; i < count; i++)
            {
                int number = reader.ReadVarInt();
                if (number < 0 || number >= Omens.All.Count) throw new NetReadException($"No such omen as {number}.");
                offering.Omens.Add(Omens.All[number].Id);
            }
            offering.Sanitize();
            return offering;
        }

        public static void WriteHeroSummary(BitWriter writer, HeroState hero)
        {
            writer.WriteVarInt(hero.Level);
            writer.WriteVarInt(hero.Experience);
            writer.WriteVarInt(hero.Gold);
            writer.WriteVarInt(hero.Flasks);
            writer.WriteVarInt(hero.WorldsCleared);
            writer.WriteVarInt(hero.PerilProven);

            writer.WriteCount(hero.Talents.Count);
            foreach (string id in hero.Talents) writer.WriteVarInt(TalentTree.Find(id)?.Index ?? 0);
        }

        public static void ReadHeroSummary(BitReader reader, HeroState hero)
        {
            hero.Level = Math.Clamp(reader.ReadVarInt(), 1, HeroState.MaxLevel);
            hero.Experience = Math.Max(reader.ReadVarInt(), 0);
            hero.Gold = Math.Max(reader.ReadVarInt(), 0);
            hero.Flasks = Math.Clamp(reader.ReadVarInt(), 0, HeroState.MaxFlasks);
            hero.WorldsCleared = Math.Max(reader.ReadVarInt(), 0);
            hero.PerilProven = Math.Clamp(reader.ReadVarInt(), 0, Offering.MostPeril);

            int talents = reader.ReadCount(TalentTree.Nodes.Length);
            var taken = new List<string>(talents);
            for (int i = 0; i < talents; i++)
            {
                int index = reader.ReadVarInt();
                if (index > 0 && index < TalentTree.Nodes.Length) taken.Add(TalentTree.Nodes[index].Id);
            }
            hero.Talents = taken;
        }

        /// <summary>What's on a hero that only lasts a while.</summary>
        public static void WriteBuffs(BitWriter writer, IReadOnlyList<ActiveBuff> buffs)
        {
            writer.WriteCount(buffs.Count);
            foreach (ActiveBuff buff in buffs)
            {
                writer.WriteVarInt(buff.Buff.Number);
                writer.WriteVarInt(buff.Stacks);
                writer.WriteFloat(buff.Left);
            }
        }

        public static void ReadBuffs(BitReader reader, List<ActiveBuff> buffs)
        {
            buffs.Clear();
            int count = reader.ReadCount(MostBuffs);
            for (int i = 0; i < count; i++)
            {
                int number = reader.ReadVarInt();
                int stacks = reader.ReadVarInt();
                float left = Finite(reader.ReadFloat());
                if (BuffCatalog.Get(number) is BuffDefinition buff) buffs.Add(new ActiveBuff { Buff = buff, Stacks = Math.Clamp(stacks, 1, buff.MostStacks), Left = left });
            }
        }

        public static void WriteGear(BitWriter writer, HeroState hero)
        {
            WriteCells(writer, hero.Inventory);
            WriteCells(writer, hero.Bar);
            writer.WriteCount(hero.Equipped.Count);
            foreach ((WornSlot place, Item item) in hero.Equipped)
            {
                writer.WriteByte((byte)place);
                WriteItem(writer, item);
            }
        }

        public static void ReadGear(BitReader reader, HeroState hero)
        {
            var carried = new Item?[HeroState.InventorySize];
            Item?[] sent = ReadCells(reader, HeroState.InventorySize);
            sent.CopyTo(carried, 0);

            var bar = new Item?[HeroState.BarSlots];
            Item?[] onBar = ReadCells(reader, HeroState.BarSlots);
            onBar.CopyTo(bar, 0);
            foreach (Item? item in bar)
            {
                if (item is { IsBook: false }) throw new NetReadException($"{item.Slot} on the bar.");
            }

            var worn = new Dictionary<WornSlot, Item>();
            int count = reader.ReadCount(MostItems);
            for (int i = 0; i < count; i++)
            {
                WornSlot place = Checked<WornSlot>(reader.ReadByte());
                Item item = ReadItem(reader);
                if (WornSlots.Holds(place) != item.Slot) throw new NetReadException($"{item.Slot} worn on {place}.");
                worn[place] = item;
            }

            hero.Inventory = carried;
            hero.Bar = bar;
            hero.Equipped = worn;
        }

        /// <summary>What a hero looks like to everyone else, which is only ever what they're wearing. See GEAR.md.</summary>
        public static void WriteWorn(BitWriter writer, int player, HeroState hero)
        {
            writer.WriteVarInt(player);
            writer.WriteCount(hero.Equipped.Count);
            foreach ((WornSlot place, Item item) in hero.Equipped)
            {
                writer.WriteByte((byte)place);
                writer.WriteVarULong(item.LookSeed());
                writer.WriteUInt(ItemLook.Traits(item));
            }
        }

        public static (int player, List<WornLook> worn) ReadWorn(BitReader reader)
        {
            int player = reader.ReadVarInt();
            int count = reader.ReadCount(Enum.GetValues<WornSlot>().Length);
            var worn = new List<WornLook>(count);
            for (int i = 0; i < count; i++)
            {
                WornSlot place = Checked<WornSlot>(reader.ReadByte());
                worn.Add(new WornLook(place, reader.ReadVarULong(), reader.ReadUInt()));
            }
            return (player, worn);
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

        /// <summary>A row of places that things stay in, so that which are empty is as much a part of it as what's in the others.</summary>
        public static void WriteCells(BitWriter writer, Item?[] cells)
        {
            writer.WriteCount(cells.Length);
            foreach (Item? item in cells)
            {
                writer.WriteBool(item != null);
                if (item != null) WriteItem(writer, item);
            }
        }

        public static Item?[] ReadCells(BitReader reader, int most)
        {
            var cells = new Item?[reader.ReadCount(most)];
            for (int i = 0; i < cells.Length; i++) cells[i] = reader.ReadBool() ? ReadItem(reader) : null;
            return cells;
        }

        public static void WriteItem(BitWriter writer, Item item)
        {
            writer.WriteString(item.Name);
            writer.WriteByte((byte)item.Slot);
            writer.WriteByte((byte)item.Rarity);
            writer.WriteVarInt(item.Level);
            writer.WriteByte((byte)item.Kind);
            writer.WriteVarULong(item.Seed);
            writer.WriteString(item.Base);
            writer.WriteFloat(item.WeaponDamage);
            writer.WriteFloat(item.Armour);
            writer.WriteFloat(item.Evasion);
            writer.WriteFloat(item.Barrier);
            writer.WriteBool(item.Unique != null);
            if (item.Unique != null) writer.WriteString(item.Unique);
            writer.WriteCount(item.Lines.Count);
            foreach (ItemLine line in item.Lines)
            {
                writer.WriteString(line.Affix);
                writer.WriteFloat(line.Value);
            }
            writer.WriteBool(item.Page != null);
            if (item.Page != null) WritePage(writer, item.Page);
            writer.WriteBool(item.Sticker != null);
            if (item.Sticker != null) WriteSticker(writer, item.Sticker);

            writer.WriteCount(item.Places);
            foreach (Item? held in item.Held ?? Array.Empty<Item?>())
            {
                writer.WriteBool(held != null);
                if (held != null) WriteItem(writer, held);
            }
        }

        private static void WriteSticker(BitWriter writer, Sticker sticker)
        {
            writer.WriteByte((byte)sticker.Good);
            writer.WriteFloat(sticker.GoodBy);
            writer.WriteByte((byte)sticker.Bad);
            writer.WriteFloat(sticker.BadBy);
        }

        private static Sticker ReadSticker(BitReader reader)
        {
            var sticker = new Sticker { Good = Checked<PageEffect>(reader.ReadByte()), GoodBy = Finite(reader.ReadFloat()), Bad = Checked<PageEffect>(reader.ReadByte()), BadBy = Finite(reader.ReadFloat()) };
            Stickers.Fix(sticker);
            return sticker;
        }

        private static void WriteSentence(BitWriter writer, string? id) => writer.WriteVarInt(Sentences.Find(id)?.Number ?? 0);

        private static string ReadSentence(BitReader reader, string plain) => Sentences.Get(reader.ReadVarInt())?.Id ?? plain;

        private static string? ReadChosen(BitReader reader) => Sentences.Get(reader.ReadVarInt())?.Id;

        /// <summary>What's written on a page, as the numbers of its sentences. What's stuck on it isn't written, and goes as the items those are. Whoever's at the other end, what's read here has been put right before anything sees it.</summary>
        public static void WritePage(BitWriter writer, Page page)
        {
            writer.WriteByte((byte)page.Kind);
            WriteSentence(writer, page.From);
            WriteSentence(writer, page.FromWithin);
            WriteSentence(writer, page.Towards);
            WriteSentence(writer, page.TowardsWithin);
            WriteSentence(writer, page.Shape);
            WriteSentence(writer, page.Timing);
            writer.WriteCount(page.Outcomes.Count);
            foreach (Outcome outcome in page.Outcomes)
            {
                WriteSentence(writer, outcome.Deed);
                WriteSentence(writer, outcome.Element);
                WriteSentence(writer, outcome.Measure);
                WriteSentence(writer, outcome.Ailment);
                WriteSentence(writer, outcome.Gift);
                WriteSentence(writer, outcome.Kin);
                WriteSentence(writer, outcome.Pool);
                WriteSentence(writer, outcome.Whom);
            }
        }

        public static Page ReadPage(BitReader reader)
        {
            var page = new Page
            {
                Kind = Checked<PageKind>(reader.ReadByte()),
                From = ReadSentence(reader, Sentences.PlainFrom),
                FromWithin = ReadChosen(reader),
                Towards = ReadSentence(reader, Sentences.PlainTowards),
                TowardsWithin = ReadChosen(reader),
                Shape = ReadSentence(reader, Sentences.PlainShape),
                Timing = ReadSentence(reader, Sentences.PlainTiming),
            };

            int outcomes = reader.ReadCount(Rulebook.MostOutcomes);
            for (int o = 0; o < outcomes; o++)
            {
                page.Outcomes.Add(new Outcome
                {
                    Deed = ReadSentence(reader, Sentences.PlainDeed),
                    Element = ReadSentence(reader, Sentences.PlainElement),
                    Measure = ReadSentence(reader, Sentences.PlainMeasure),
                    Ailment = ReadSentence(reader, Sentences.PlainAilment),
                    Gift = ReadSentence(reader, Sentences.PlainGift),
                    Kin = ReadSentence(reader, Sentences.PlainKin),
                    Pool = ReadSentence(reader, Sentences.PlainPool),
                    Whom = ReadSentence(reader, Sentences.PlainWhom),
                });
            }

            Scribe.Fix(page, following: true);
            return page;
        }

        public static Item ReadItem(BitReader reader) => ReadItem(reader, 0);

        // A book holds pages and a page stickers, and nothing goes any deeper than that, whatever arrives.
        private static Item ReadItem(BitReader reader, int depth)
        {
            if (depth > MostDepth) throw new NetReadException("Something inside something inside something.");

            var item = new Item
            {
                Name = reader.ReadString(LongestName),
                Slot = Checked<ItemSlot>(reader.ReadByte()),
                Rarity = Checked<Rarity>(reader.ReadByte()),
                Level = reader.ReadVarInt(),
                Kind = Checked<WeaponKind>(reader.ReadByte()),
                Seed = reader.ReadVarULong(),
                Base = reader.ReadString(LongestName),
                WeaponDamage = Finite(reader.ReadFloat()),
                Armour = Finite(reader.ReadFloat()),
                Evasion = Finite(reader.ReadFloat()),
                Barrier = Finite(reader.ReadFloat()),
            };

            // Anyone may be at the other end. A base that means nothing here is only somewhere for a look to start from, and one has to be found some other way.
            if (!ItemGenerator.HasBase(item.Base)) item.Base = "";

            if (reader.ReadBool())
            {
                // A name that means nothing here just means no such unique.
                string unique = reader.ReadString(LongestName);
                if (UniqueCatalog.Find(unique) != null) item.Unique = unique;
            }

            int lines = reader.ReadCount(MostLines);
            for (int i = 0; i < lines; i++)
            {
                string affix = reader.ReadString(LongestName);
                float value = Finite(reader.ReadFloat());
                if (AffixCatalog.Find(affix) != null) item.Lines.Add(new ItemLine(affix, value));
            }

            // What it's worn as says what it is, so nothing can arrive as a page with nothing on it, or as a sword with pages.
            Page? page = reader.ReadBool() ? ReadPage(reader) : null;
            Sticker? sticker = reader.ReadBool() ? ReadSticker(reader) : null;
            if (item.IsPage) item.Page = page ?? Scribe.NewPage(PageKind.Projectile);
            if (item.IsSticker) item.Sticker = sticker ?? new Sticker();

            int places = reader.ReadCount(Rulebook.MostPages);
            var held = new Item?[places];
            for (int i = 0; i < places; i++)
            {
                if (!reader.ReadBool()) continue;
                Item inside = ReadItem(reader, depth + 1);
                if (item.IsBook && inside.IsPage || item.IsPage && inside.IsSticker) held[i] = inside;
            }
            if (item.IsBook || item.IsPage) item.Held = held;
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
            Mix(ref structure, (uint)world.Haunts.Count);
            foreach (WorldSite haunt in world.Haunts) Mix(ref structure, (uint)haunt.Spawns.Count);
            Mix(ref structure, (uint)world.Roads.Count);

            uint ground = 2166136261u;
            foreach (byte value in world.Walkable.Data) Mix(ref ground, value);
            foreach (byte value in world.Passable.Data) Mix(ref ground, value);
            Mix(ref ground, (uint)world.Placement.Count);
            return (structure, ground);
        }
    }
}
