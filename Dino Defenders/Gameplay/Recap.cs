using Dino_Defenders.Net;
using Dino_Defenders.Progression;
using Dino_Defenders.Stats;
using Dino_Defenders.Tooltips;
using Dino_Engine.Networking;

namespace Dino_Defenders.Gameplay
{
    /// <summary>What a line in the account of a fall is.</summary>
    public enum RecapKind : byte
    {
        Hit,            // A Page reached the hero.
        Indirect,       // Indirect Damage: a thing that isn't a blow, thorns or a burst, which says what it was in its Cause.
        Burn,           // A run of damage over time from one source, as long as it went on.
        Bleed,
        Venom,
        Debt,           // What was put off falling due.
        Drain,          // Health the hero's own build drains.
        Cost,           // Health paid for a Book.
        Shouldered,     // Taken for a friend.
        Evaded,         // Didn't land at all.
        Blocked,
        Dodged,
        Flask,          // Drunk, which is worth seeing when.
        Saved,          // Death refused, for a while: what refused it is its Save.
        GaspEnded,      // Last Gasp ran out with nobody killed.
    }

    /// <summary>What Indirect Damage was, as far as anyone reading it needs to know.</summary>
    public enum HarmCause : byte
    {
        Other,
        Reflected,      // Thorns: Melee Damage Taken Reflected.
        BlockReflected, // A Block turned back at whoever threw it.
        Burst,          // Something going up as it died.
        Explosion,
        Overflow,       // What was left over when someone else died.
        Conducted,      // Lightning carried on from someone else: Conduction.
    }

    /// <summary>What kept a hero up a moment longer.</summary>
    public enum RecapSave : byte
    {
        None,
        MinionShield,
        Rebirth,
        LastGasp,
    }

    /// <summary>Who did something, as it matters to the one it was done to.</summary>
    public enum RecapWho : byte
    {
        Enemy,
        Minion,         // One raised by an enemy.
        Arena,          // The arena's own hand: what it does on its own (ARENAS.md).
        You,
        Friend,
        Unknown,
    }

    public sealed class RecapSource
    {
        public string Name = "";
        public Rarity Rarity;
        public int Level;
        public RecapWho Who;
        public bool Tyrant;
        public string Owner = "";       // Whose Minion it is, if it's one.
    }

    /// <summary>A Book that was used on the hero, and its card as it stood for whoever used it then: what it deals and leaves.</summary>
    public sealed class RecapBook
    {
        public string Name = "";
        public Card? Card;
    }

    /// <summary>
    /// One thing that happened to the hero. Everything in it was written down where it happened, from the numbers the rules used (DEATHS.md): what left whoever did it, what each defence took off,
    /// what was taken by anyone or anything else, and what each pool paid. What's over time is one line for as long as it ran from the same source.
    /// </summary>
    public struct RecapEntry
    {
        public float Ago;               // Seconds before the fall that it began.
        public float Lasted;            // Seconds it went on for, for what's over time.
        public RecapKind Kind;
        public HarmCause Cause;
        public RecapSave Save;
        public short Source;            // Into Sources, or -1.
        public short Book;              // Into Books, or -1.
        public Tags Tags;               // What the Page was: an Attack or a Spell, Melee, a Projectile, an Area.
        public bool Critical;
        public bool Executed;
        public float EvadeChance;       // Percent: the chance the hero had of this missing them, or of their Blocking it. Zero where neither could.
        public float BlockChance;
        public DamageTable Rolled;      // As it left them, by type, before anything of the hero's.
        public Mitigation Defences;
        public DamageTable Dealt;       // What got through every defence.
        public float Shared;            // Taken by a friend, or by a Minion of the hero's.
        public float Gold;              // Paid in Gold instead.
        public float Capped;            // More than one Hit can take, and so never taken.
        public float Healed;            // Turned to Health instead.
        public float Stored;            // Held in the ice, to break out later.
        public float ToBarrier;
        public float ToMana;
        public float ToHealth;
        public float ToDebt;
        public float Restored;          // What a save gave back.
        public float Health;            // After it.
        public float Barrier;
        public byte Left;               // Ailments it left on the hero, a bit for each.

        public readonly float Got => Dealt.Total;
        public readonly float Paid => ToBarrier + ToMana + ToHealth + ToDebt;
        public readonly bool Landed => Kind is not (RecapKind.Evaded or RecapKind.Blocked or RecapKind.Dodged or RecapKind.Flask or RecapKind.Saved or RecapKind.GaspEnded);
        public readonly bool OverTime => Kind is RecapKind.Burn or RecapKind.Bleed or RecapKind.Venom or RecapKind.Debt or RecapKind.Drain;

        /// <summary>The type the most of it was, of what got through, or of what left them if nothing did.</summary>
        public readonly DamageType Mostly
        {
            get
            {
                DamageTable table = Dealt.Total > 0f ? Dealt : Rolled;
                int best = 0;
                for (int i = 1; i < DamageKinds.Count; i++)
                {
                    if (table[i] > table[best]) best = i;
                }
                return (DamageType)best;
            }
        }
    }

    /// <summary>Everything that came back over the account, by where it came from.</summary>
    public struct RecapHeals
    {
        public float Regenerated;
        public float Leeched;           // Leech and Health on Hit.
        public float OnKill;
        public float Flask;
        public float Other;             // Whatever else gave Health back, counted by what Health did and the rest didn't say.

        public readonly float Total => Regenerated + Leeched + OnKill + Flask + Other;

        public void Add(in RecapHeals other)
        {
            Regenerated += other.Regenerated;
            Leeched += other.Leeched;
            OnKill += other.OnKill;
            Flask += other.Flask;
            Other += other.Other;
        }
    }

    /// <summary>
    /// How a hero fell: the last moments before it, as the server saw them, sent to them when it happens (DEATHS.md). Nothing in it is worked out again on the client: what it says is what happened.
    /// </summary>
    public sealed class DeathRecap
    {
        public const float SampleSeconds = 1f / 30f;

        public float Window;            // How many seconds it goes back.
        public int Fatal = -1;          // The entry that ended it.
        public bool Haven;              // Fell where nothing's lost.
        public float MaxHealth;         // As the hero had it when they fell.
        public RecapHeals Heals;
        public float Unaccounted;       // Health lost that nothing here names. It ought to be nothing, and it's shown if it isn't.
        public readonly List<RecapSource> Sources = new();
        public readonly List<RecapBook> Books = new();
        public readonly List<RecapEntry> Entries = new();   // Oldest first.

        public RecapEntry? Killer => Fatal >= 0 && Fatal < Entries.Count ? Entries[Fatal] : null;
        public RecapSource? SourceOf(in RecapEntry entry) => entry.Source >= 0 && entry.Source < Sources.Count ? Sources[entry.Source] : null;
        public RecapBook? BookOf(in RecapEntry entry) => entry.Book >= 0 && entry.Book < Books.Count ? Books[entry.Book] : null;

        // ---- Over the wire.

        private const int MostEntries = 1024;
        private const int MostCardLines = 256;
        private const int CardDepth = 4;

        public void Write(BitWriter writer)
        {
            writer.WriteFloat(Window);
            writer.WriteVarInt(Fatal);
            writer.WriteBool(Haven);
            writer.WriteFloat(MaxHealth);
            writer.WriteFloat(Heals.Regenerated);
            writer.WriteFloat(Heals.Leeched);
            writer.WriteFloat(Heals.OnKill);
            writer.WriteFloat(Heals.Flask);
            writer.WriteFloat(Heals.Other);
            writer.WriteFloat(Unaccounted);

            writer.WriteCount(Sources.Count);
            foreach (RecapSource source in Sources)
            {
                writer.WriteString(source.Name);
                writer.WriteByte((byte)source.Rarity);
                writer.WriteVarInt(source.Level);
                writer.WriteByte((byte)source.Who);
                writer.WriteBool(source.Tyrant);
                writer.WriteString(source.Owner);
            }

            writer.WriteCount(Books.Count);
            foreach (RecapBook book in Books)
            {
                writer.WriteString(book.Name);
                writer.WriteBool(book.Card != null);
                if (book.Card != null) WriteCard(writer, book.Card, CardDepth);
            }

            writer.WriteCount(Entries.Count);
            foreach (RecapEntry entry in Entries) WriteEntry(writer, entry);
        }

        public static DeathRecap Read(BitReader reader)
        {
            var recap = new DeathRecap
            {
                Window = reader.ReadFloat(),
                Fatal = reader.ReadVarInt(),
                Haven = reader.ReadBool(),
                MaxHealth = reader.ReadFloat(),
            };
            recap.Heals.Regenerated = reader.ReadFloat();
            recap.Heals.Leeched = reader.ReadFloat();
            recap.Heals.OnKill = reader.ReadFloat();
            recap.Heals.Flask = reader.ReadFloat();
            recap.Heals.Other = reader.ReadFloat();
            recap.Unaccounted = reader.ReadFloat();

            int sources = reader.ReadCount(MostEntries);
            for (int i = 0; i < sources; i++)
            {
                recap.Sources.Add(new RecapSource
                {
                    Name = reader.ReadString(Protocol.LongestName),
                    Rarity = (Rarity)reader.ReadByte(),
                    Level = reader.ReadVarInt(),
                    Who = (RecapWho)reader.ReadByte(),
                    Tyrant = reader.ReadBool(),
                    Owner = reader.ReadString(Protocol.LongestName),
                });
            }

            int books = reader.ReadCount(MostEntries);
            for (int i = 0; i < books; i++)
            {
                var book = new RecapBook { Name = reader.ReadString(Protocol.LongestName) };
                if (reader.ReadBool()) book.Card = ReadCard(reader, CardDepth);
                recap.Books.Add(book);
            }

            int entries = reader.ReadCount(MostEntries);
            for (int i = 0; i < entries; i++) recap.Entries.Add(ReadEntry(reader));

            return recap;
        }

        private static void WriteEntry(BitWriter writer, in RecapEntry entry)
        {
            writer.WriteFloat(entry.Ago);
            writer.WriteFloat(entry.Lasted);
            writer.WriteByte((byte)entry.Kind);
            writer.WriteByte((byte)entry.Cause);
            writer.WriteByte((byte)entry.Save);
            writer.WriteVarInt(entry.Source);
            writer.WriteVarInt(entry.Book);
            writer.WriteVarULong((ulong)entry.Tags);
            writer.WriteBool(entry.Critical);
            writer.WriteBool(entry.Executed);
            writer.WriteFloat(entry.EvadeChance);
            writer.WriteFloat(entry.BlockChance);
            WriteTable(writer, entry.Rolled);
            WriteTable(writer, entry.Defences.Arrived);
            WriteTable(writer, entry.Defences.Resisted);
            WriteTable(writer, entry.Defences.Armoured);
            WriteTable(writer, entry.Defences.Reduced);
            WriteTable(writer, entry.Defences.Flat);
            WriteTable(writer, entry.Defences.Scaled);
            WriteTable(writer, entry.Defences.Shocked);
            WriteTable(writer, entry.Dealt);
            writer.WriteFloat(entry.Shared);
            writer.WriteFloat(entry.Gold);
            writer.WriteFloat(entry.Capped);
            writer.WriteFloat(entry.Healed);
            writer.WriteFloat(entry.Stored);
            writer.WriteFloat(entry.ToBarrier);
            writer.WriteFloat(entry.ToMana);
            writer.WriteFloat(entry.ToHealth);
            writer.WriteFloat(entry.ToDebt);
            writer.WriteFloat(entry.Restored);
            writer.WriteFloat(entry.Health);
            writer.WriteFloat(entry.Barrier);
            writer.WriteByte(entry.Left);
        }

        private static RecapEntry ReadEntry(BitReader reader)
        {
            var entry = new RecapEntry
            {
                Ago = reader.ReadFloat(),
                Lasted = reader.ReadFloat(),
                Kind = (RecapKind)reader.ReadByte(),
                Cause = (HarmCause)reader.ReadByte(),
                Save = (RecapSave)reader.ReadByte(),
                Source = (short)reader.ReadVarInt(),
                Book = (short)reader.ReadVarInt(),
                Tags = (Tags)reader.ReadVarULong(),
                Critical = reader.ReadBool(),
                Executed = reader.ReadBool(),
                EvadeChance = reader.ReadFloat(),
                BlockChance = reader.ReadFloat(),
            };
            entry.Rolled = ReadTable(reader);
            entry.Defences.Arrived = ReadTable(reader);
            entry.Defences.Resisted = ReadTable(reader);
            entry.Defences.Armoured = ReadTable(reader);
            entry.Defences.Reduced = ReadTable(reader);
            entry.Defences.Flat = ReadTable(reader);
            entry.Defences.Scaled = ReadTable(reader);
            entry.Defences.Shocked = ReadTable(reader);
            entry.Dealt = ReadTable(reader);
            entry.Shared = reader.ReadFloat();
            entry.Gold = reader.ReadFloat();
            entry.Capped = reader.ReadFloat();
            entry.Healed = reader.ReadFloat();
            entry.Stored = reader.ReadFloat();
            entry.ToBarrier = reader.ReadFloat();
            entry.ToMana = reader.ReadFloat();
            entry.ToHealth = reader.ReadFloat();
            entry.ToDebt = reader.ReadFloat();
            entry.Restored = reader.ReadFloat();
            entry.Health = reader.ReadFloat();
            entry.Barrier = reader.ReadFloat();
            entry.Left = reader.ReadByte();
            return entry;
        }

        // Only the types there's any of, as most blows are one or two.
        private static void WriteTable(BitWriter writer, in DamageTable table)
        {
            uint mask = 0;
            for (int i = 0; i < DamageKinds.Count; i++)
            {
                if (table[i] != 0f) mask |= 1u << i;
            }
            writer.WriteBits(mask, DamageKinds.Count);
            for (int i = 0; i < DamageKinds.Count; i++)
            {
                if ((mask & (1u << i)) != 0) writer.WriteFloat(table[i]);
            }
        }

        private static DamageTable ReadTable(BitReader reader)
        {
            DamageTable table = default;
            uint mask = reader.ReadBits(DamageKinds.Count);
            for (int i = 0; i < DamageKinds.Count; i++)
            {
                if ((mask & (1u << i)) != 0) table[i] = reader.ReadFloat();
            }
            return table;
        }

        /// <summary>A card as the server built it, for a Book the client has no sheet for: an enemy's.</summary>
        public static void WriteCard(BitWriter writer, Card card, int depth)
        {
            writer.WriteString(card.Title);
            writer.WriteString(card.Kind);
            writer.WriteVarULong((ulong)card.Tags);
            writer.WriteBool(card.Rarity != null);
            if (card.Rarity is Rarity rarity) writer.WriteByte((byte)rarity);
            writer.WriteBool(card.Gilded);
            int lines = Math.Min(card.Lines.Count, MostCardLines);
            writer.WriteCount(lines);
            for (int i = 0; i < lines; i++)
            {
                writer.WriteByte((byte)card.Lines[i].Look);
                writer.WriteString(card.Lines[i].Text);
                writer.WriteString(card.Lines[i].Value);
            }

            int attached = depth > 0 ? Math.Min(card.Attached.Count, MostCardLines) : 0;
            writer.WriteCount(attached);
            for (int i = 0; i < attached; i++) WriteCard(writer, card.Attached[i], depth - 1);
            bool details = depth > 0 && card.Details != null;
            writer.WriteBool(details);
            if (details) WriteCard(writer, card.Details!, depth - 1);
        }

        public static Card ReadCard(BitReader reader, int depth)
        {
            var card = new Card
            {
                Title = reader.ReadString(Protocol.LongestText),
                Kind = reader.ReadString(Protocol.LongestText),
                Tags = (Tags)reader.ReadVarULong(),
            };
            if (reader.ReadBool()) card.Rarity = (Rarity)reader.ReadByte();
            card.Gilded = reader.ReadBool();
            int lines = reader.ReadCount(MostCardLines);
            for (int i = 0; i < lines; i++)
            {
                var look = (LineLook)reader.ReadByte();
                string text = reader.ReadString(Protocol.LongestText);
                string value = reader.ReadString(Protocol.LongestText);
                card.Lines.Add(new CardLine(look, text, value));
            }

            int attached = reader.ReadCount(depth > 0 ? MostCardLines : 0);
            for (int i = 0; i < attached; i++) card.Attached.Add(ReadCard(reader, depth - 1));
            if (reader.ReadBool())
            {
                if (depth <= 0) throw new NetReadException("A card nested deeper than any is built.");
                card.Details = ReadCard(reader, depth - 1);
            }
            return card;
        }
    }
}
