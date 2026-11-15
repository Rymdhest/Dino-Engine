using Dino_Defenders.Books;
using Dino_Defenders.Gameplay;
using Dino_Defenders.Progression;
using Dino_Defenders.Stats;
using Dino_Defenders.Tooltips;
using Dino_Engine.ECS.ECS_Architecture;
using Dino_Engine.Networking;

namespace Dino_Defenders.Server
{
    /// <summary>
    /// The account of what's happening to one hero, kept for the last <see cref="Window"/> seconds and handed over as a <see cref="DeathRecap"/> when they fall (DEATHS.md). Everything is written
    /// down where the rules do it, from the numbers they used, and never worked out again, so it can't say anything the fight didn't do. Health is also read every tick, and whatever it did that
    /// nothing here wrote down is counted (<see cref="DeathRecap.Unaccounted"/>), so a gap in the account shows rather than hides. It's only kept for heroes, a handful of them, and allocates
    /// nothing a blow at a time once it's filled.
    /// </summary>
    public sealed class Ledger
    {
        public const float Window = 15f;
        private const int MostLines = 768;
        private const int LookBack = 96;                // Lines back a run over time is looked for, as blows land between its moments.
        private const int SampleEvery = 2;              // Ticks, at sixty a second: DeathRecap.SampleSeconds.
        private const float Joined = 2.5f * NetTime.TickSeconds;    // A run over time goes on while it's no longer than this since it last came.
        private const float Slack = 0.02f;              // Of a Health point, what the arithmetic itself can leave behind.

        private struct Line
        {
            public RecapEntry Entry;
            public double At;
            public double End;
            public Actor? Source;
            public PageProfile? Page;
        }

        private struct Sampled
        {
            public double At;
            public RecapHeals Heals;
            public float Unaccounted;
        }

        private readonly List<Line> _lines = new(MostLines);
        private readonly Queue<Sampled> _samples = new();
        private readonly Dictionary<Actor, RecapSource> _known = new();
        private RecapHeals _heals;
        private float _explained;
        private float _unaccounted;
        private float _lastHealth = float.NaN;
        private int _ticks;

        /// <summary>The session's seconds, as of this tick.</summary>
        public double Now;

        /// <summary>Where whoever does something is looked up, to say who they were.</summary>
        public ECSWorld? World;

        /// <summary>The hero it's kept for.</summary>
        public Actor? Hero;

        /// <summary>Starts again, as for a hero who's just arrived or got up.</summary>
        public void Clear()
        {
            _lines.Clear();
            _samples.Clear();
            _known.Clear();
            _heals = default;
            _explained = 0f;
            _unaccounted = 0f;
            _lastHealth = float.NaN;
            _ticks = 0;
        }

        // ---- What happens.

        /// <summary>A Hit or Indirect Damage reached them, with everything worked out about it already in <paramref name="entry"/>.</summary>
        public void Struck(RecapEntry entry, Actor source, PageProfile? page)
        {
            _explained -= entry.ToHealth;
            Add(new Line { Entry = entry, At = Now, End = Now, Source = source, Page = page });
        }

        /// <summary>Something meant for them didn't land.</summary>
        public void Missed(RecapKind kind, Actor source, PageProfile? page, float evade, float block)
        {
            Add(new Line
            {
                Entry = new RecapEntry { Kind = kind, Tags = page?.Tags ?? Tags.None, EvadeChance = evade, BlockChance = block, Health = Hero?.Vitals.Health ?? 0f, Barrier = Hero?.Vitals.Barrier ?? 0f },
                At = Now,
                End = Now,
                Source = source,
                Page = page,
            });
        }

        /// <summary>A moment of damage over time, or of something owed or drained, which goes on the run it belongs to.</summary>
        public void OverTime(RecapKind kind, Actor? source, in Mitigation trace, in DamageTable dealt, float toBarrier, float toMana, float toHealth, float toDebt)
        {
            _explained -= toHealth;
            Actor? hero = Hero;
            for (int i = _lines.Count - 1; i >= Math.Max(_lines.Count - LookBack, 0); i--)
            {
                Line line = _lines[i];
                if (line.Entry.Kind != kind || line.Source != source) continue;
                if (Now - line.End > Joined) break;

                // The same run going on.
                line.End = Now;
                Add(ref line.Entry.Defences, trace);
                for (int t = 0; t < DamageKinds.Count; t++)
                {
                    line.Entry.Rolled[t] += trace.Arrived[t];
                    line.Entry.Dealt[t] += dealt[t];
                }
                line.Entry.ToBarrier += toBarrier;
                line.Entry.ToMana += toMana;
                line.Entry.ToHealth += toHealth;
                line.Entry.ToDebt += toDebt;
                line.Entry.Health = hero?.Vitals.Health ?? 0f;
                line.Entry.Barrier = hero?.Vitals.Barrier ?? 0f;
                _lines[i] = line;
                return;
            }

            var entry = new RecapEntry { Kind = kind, Tags = Tags.DamageOverTime, Defences = trace, Dealt = dealt, ToBarrier = toBarrier, ToMana = toMana, ToHealth = toHealth, ToDebt = toDebt, Health = hero?.Vitals.Health ?? 0f, Barrier = hero?.Vitals.Barrier ?? 0f };
            entry.Rolled = trace.Arrived;
            Add(new Line { Entry = entry, At = Now, End = Now, Source = source });
        }

        /// <summary>Health paid for a Book.</summary>
        public void Paid(BookProfile book, float health)
        {
            if (health <= 0f) return;
            _explained -= health;
            Add(new Line
            {
                Entry = new RecapEntry { Kind = RecapKind.Cost, ToHealth = health, Health = Hero?.Vitals.Health ?? 0f, Barrier = Hero?.Vitals.Barrier ?? 0f },
                At = Now,
                End = Now,
                Source = Hero,
                Page = book.First,
            });
        }

        /// <summary>Part of a friend's Hit, taken for them.</summary>
        public void Shouldered(Actor friend, float taken, float toBarrier, float toMana, float toHealth, float toDebt)
        {
            // What it was made of stays with the friend's Hit: this is only what came over, which no defence of theirs touches.
            _explained -= toHealth;
            Add(new Line
            {
                Entry = new RecapEntry { Kind = RecapKind.Shouldered, Shared = taken, ToBarrier = toBarrier, ToMana = toMana, ToHealth = toHealth, ToDebt = toDebt, Health = Hero?.Vitals.Health ?? 0f, Barrier = Hero?.Vitals.Barrier ?? 0f },
                At = Now,
                End = Now,
                Source = friend,
            });
        }

        /// <summary>A flask, drunk.</summary>
        public void Drank(float healed)
        {
            _heals.Flask += healed;
            _explained += healed;
            Add(new Line { Entry = new RecapEntry { Kind = RecapKind.Flask, Restored = healed, Health = Hero?.Vitals.Health ?? 0f, Barrier = Hero?.Vitals.Barrier ?? 0f }, At = Now, End = Now, Source = Hero });
        }

        /// <summary>Death refused, for now, and what that gave back.</summary>
        public void Saved(RecapSave save, float healthBefore)
        {
            float restored = (Hero?.Vitals.Health ?? 0f) - healthBefore;
            _explained += restored;
            Add(new Line { Entry = new RecapEntry { Kind = RecapKind.Saved, Save = save, Restored = restored, Health = Hero?.Vitals.Health ?? 0f, Barrier = Hero?.Vitals.Barrier ?? 0f }, At = Now, End = Now, Source = Hero });
        }

        /// <summary>Last Gasp ran out with nobody killed, which takes what Health was left.</summary>
        public void GaspEnded(float lost)
        {
            _explained -= lost;
            Add(new Line { Entry = new RecapEntry { Kind = RecapKind.GaspEnded, ToHealth = lost, Health = Hero?.Vitals.Health ?? 0f }, At = Now, End = Now, Source = Hero });
        }

        /// <summary>Part of a blow turned to Health instead, as some take Fire.</summary>
        public void Turned(float health)
        {
            _heals.Other += health;
            _explained += health;
        }

        /// <summary>Health set to nothing as they fall, from however far below it the last blow took it, which is no Health coming back.</summary>
        public void Settled(float raised) => _explained += raised;

        public void Regenerated(float health)
        {
            _heals.Regenerated += health;
            _explained += health;
        }

        public void Leeched(float health)
        {
            _heals.Leeched += health;
            _explained += health;
        }

        public void OnKill(float health)
        {
            _heals.OnKill += health;
            _explained += health;
        }

        /// <summary>Once a tick, after everything that happens in it: how they stand, and whatever Health did that nothing said.</summary>
        public void Sample(in Vitals vitals, ActorSheet sheet)
        {
            if (!float.IsNaN(_lastHealth))
            {
                float off = vitals.Health - _lastHealth - _explained;
                if (off > Slack) _heals.Other += off;
                else if (off < -Slack) _unaccounted -= off;
            }
            _lastHealth = vitals.Health;
            _explained = 0f;

            if (++_ticks % SampleEvery != 0) return;
            _samples.Enqueue(new Sampled
            {
                At = Now,
                Heals = _heals,
                Unaccounted = _unaccounted,
            });
            _heals = default;
            _unaccounted = 0f;
            while (_samples.Count > 0 && Now - _samples.Peek().At > Window) _samples.Dequeue();
            Prune();
        }

        private void Add(Line line)
        {
            if (_lines.Count >= MostLines) _lines.RemoveAt(0);
            _lines.Add(line);
        }

        private void Prune()
        {
            int gone = 0;
            while (gone < _lines.Count && Now - _lines[gone].End > Window) gone++;
            if (gone > 0) _lines.RemoveRange(0, gone);
        }

        private static void Add(ref Mitigation to, in Mitigation more)
        {
            for (int t = 0; t < DamageKinds.Count; t++)
            {
                to.Arrived[t] += more.Arrived[t];
                to.Resisted[t] += more.Resisted[t];
                to.Armoured[t] += more.Armoured[t];
                to.Reduced[t] += more.Reduced[t];
                to.Flat[t] += more.Flat[t];
                to.Scaled[t] += more.Scaled[t];
                to.Shocked[t] += more.Shocked[t];
            }
        }

        // ---- When they fall.

        /// <summary>Everything kept, as an account of the fall.</summary>
        public DeathRecap Fall(bool haven)
        {
            Actor actor = Hero!;
            Prune();
            var recap = new DeathRecap { Window = Window, Haven = haven, MaxHealth = actor.Sheet.MaxHealth };

            // What's still to be sampled of the last tick is the fall itself.
            recap.Heals = _heals;
            recap.Unaccounted = _unaccounted;
            foreach (Sampled sampled in _samples)
            {
                recap.Heals.Add(sampled.Heals);
                recap.Unaccounted += sampled.Unaccounted;
            }

            var books = new Dictionary<BookPlan, short>();
            for (int i = 0; i < _lines.Count; i++)
            {
                Line line = _lines[i];
                RecapEntry entry = line.Entry;
                entry.Ago = (float)(Now - line.At);
                entry.Lasted = (float)(line.End - line.At);
                entry.Source = line.Source != null ? SourceIndex(recap, line.Source) : (short)-1;
                entry.Book = line.Page != null ? BookIndex(recap, books, line.Page) : (short)-1;
                recap.Entries.Add(entry);
            }

            // What ended it is the last thing that took Health to nothing.
            for (int i = recap.Entries.Count - 1; i >= 0; i--)
            {
                RecapEntry entry = recap.Entries[i];
                if (entry.Health > 0f || !(entry.ToHealth > 0f || entry.Executed)) continue;
                recap.Fatal = i;
                break;
            }

            return recap;
        }

        private short SourceIndex(DeathRecap recap, Actor actor)
        {
            if (!_known.TryGetValue(actor, out RecapSource? source)) _known[actor] = source = Identify(actor);
            int index = recap.Sources.IndexOf(source);
            if (index >= 0) return (short)index;
            recap.Sources.Add(source);
            return (short)(recap.Sources.Count - 1);
        }

        /// <summary>Who someone is, to the hero this is kept for. Asked the first time they do anything, while they're still there to be asked.</summary>
        public void Know(Actor actor)
        {
            if (!_known.ContainsKey(actor)) _known[actor] = Identify(actor);
        }

        private RecapSource Identify(Actor actor)
        {
            if (actor.Player is ServerPlayer player)
            {
                return new RecapSource { Name = player.Name, Level = player.Hero.Level, Who = actor == Hero ? RecapWho.You : RecapWho.Friend };
            }
            if (World != null && actor.Entity.IsValid() && World.TryGetEntityLocation(actor.Entity, out _, out _))
            {
                EntityView view = World.GetEntityView(actor.Entity);
                if (view.Has<EnemyComponent>())
                {
                    EnemyComponent enemy = view.Get<EnemyComponent>();
                    var source = new RecapSource { Name = enemy.Name, Rarity = enemy.Rarity, Level = enemy.Level, Who = actor.Owner != null ? RecapWho.Minion : RecapWho.Enemy, Tyrant = actor.Tyrant };
                    if (actor.Owner != null)
                    {
                        Know(actor.Owner);
                        source.Owner = _known[actor.Owner].Name;
                    }
                    return source;
                }
            }
            return new RecapSource { Level = actor.Loadout.Level, Who = actor.Team == Team.Enemies ? RecapWho.Arena : RecapWho.Unknown };
        }

        // The Book a Page is in, among whatever its user has, with its card as it stood for them.
        private static short BookIndex(DeathRecap recap, Dictionary<BookPlan, short> books, PageProfile page)
        {
            ActorSheet sheet = page.Sheet;
            BookPlan[] known = sheet.Loadout.Books;
            for (int b = 0; b < known.Length; b++)
            {
                if (Array.IndexOf(known[b].Pages, page.Page) < 0) continue;
                if (books.TryGetValue(known[b], out short index)) return index;

                Card? card = null;
                try
                {
                    card = Cards.Book(sheet.Profile(b));
                }
                catch (Exception e)
                {
                    // A card that can't be made is a fault in the cards, not a reason to lose the account.
                    Console.WriteLine($"No card for {known[b].Name} in a recap: {e.Message}");
                }
                recap.Books.Add(new RecapBook { Name = known[b].Name, Card = card });
                index = (short)(recap.Books.Count - 1);
                books[known[b]] = index;
                return index;
            }
            return -1;
        }
    }
}

namespace Dino_Defenders.Server
{
    /// <summary>How long each ailment on someone has to run, to tell afterwards which a blow left.</summary>
    public readonly struct AilmentClock
    {
        private readonly float _burn, _bleed, _chill, _freeze, _shock, _hex, _stun;
        private readonly int _venom, _hexes;

        private AilmentClock(Afflictions a)
        {
            _burn = a.BurnLeft;
            _bleed = a.BleedLeft;
            _venom = a.Venom?.Count ?? 0;
            _chill = a.ChillLeft;
            _freeze = a.FreezeLeft;
            _shock = a.ShockLeft;
            _hex = a.HexLeft;
            _hexes = a.HexStacks;
            _stun = a.StunLeft;
        }

        public static AilmentClock Of(Afflictions a) => new(a);

        /// <summary>A bit for each ailment that's longer or deeper than it was, which is what a blow left.</summary>
        public byte Since(in AilmentClock before)
        {
            int left = 0;
            if (_burn > before._burn) left |= 1 << (int)Ailment.Burn;
            if (_bleed > before._bleed) left |= 1 << (int)Ailment.Bleed;
            if (_venom > before._venom) left |= 1 << (int)Ailment.Venom;
            if (_chill > before._chill) left |= 1 << (int)Ailment.Chill;
            if (_freeze > before._freeze) left |= 1 << (int)Ailment.Freeze;
            if (_shock > before._shock) left |= 1 << (int)Ailment.Shock;
            if (_hex > before._hex || _hexes > before._hexes) left |= 1 << (int)Ailment.Hex;
            if (_stun > before._stun) left |= 1 << (int)Ailment.Stun;
            return (byte)left;
        }
    }
}
