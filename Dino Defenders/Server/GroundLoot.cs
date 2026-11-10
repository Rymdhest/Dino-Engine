using Dino_Defenders.Gameplay;
using Dino_Defenders.Net;
using Dino_Defenders.Progression;
using Dino_Engine.ECS.Components;
using Dino_Engine.ECS.ECS_Architecture;
using Dino_Engine.Networking;
using Dino_Engine.World;
using OpenTK.Mathematics;

namespace Dino_Defenders.Server
{
    /// <summary>
    /// Everything lying on the ground in a world. A find stays where it landed until a hero asks for it and is close enough, which is the only way anything leaves the ground:
    /// nothing is ever picked up by walking over it. Gold is the exception, and looks after itself in <see cref="InteractionSystem"/>. Every enemy dies once, so this can't
    /// grow past what a world holds. What's lying here is lost when the party moves on, as it always was.
    /// </summary>
    public sealed class GroundLoot
    {
        public const float ChestDelay = ChestLook.Swing;    // Seconds for the lid to come up.

        private const float FountainStep = 0.11f;   // Seconds between one thing coming out and the next.
        private const float NearestLanding = 2.2f;
        private const float FarthestLanding = 6f;
        private const float DeepestLanding = 0.3f;
        private const float LandingLook = 0.5f;
        private const int LandingTries = 16;
        private const int MostAsked = 8;
        private const int FoliosOfEach = 3;         // What a scripted run lays among the gear, for looking at.
        private const float AskPatience = 1f;       // Seconds. A hero is seen a moment late, so a request can arrive ahead of them.
        private const float Height = 1.6f;          // What comes out of a body comes from about the middle of it.

        private readonly record struct Lying(Entity Entity, Item Item, Vector2 At);

        private readonly ServerSession _session;
        private readonly WorldRandom _random = new WorldRandom(Environment.TickCount);
        private readonly Dictionary<int, Lying> _lying = new();
        private int _nextFind = 1;

        public GroundLoot(ServerSession session)
        {
            _session = session;
        }

        private GameServer Server => _session.Server;

        public int Count => _lying.Count;

        /// <summary>Every find there is and where it lies, for whoever is looking on from outside.</summary>
        public IEnumerable<(int find, Item item, Vector2 at)> All => _lying.Select(pair => (pair.Key, pair.Value.Item, pair.Value.At));

        /// <summary>Sends things out of something one after another, gold first, with the best of it last.</summary>
        public void Spill(Vector3 from, IEnumerable<Item> items, int gold, int piles, float delay = 0f)
        {
            Vector3 mouth = from + Vector3.UnitY * Height;
            for (int i = 0; i < piles && gold > 0; i++)
            {
                int share = gold / (piles - i);
                gold -= share;
                if (share > 0) DropGold(mouth, share, delay);
                delay += FountainStep * 0.5f;
            }

            foreach (Item item in items.OrderBy(find => find.Rarity))
            {
                Drop(mouth, item, delay);
                delay += FountainStep;
            }
        }

        /// <summary>Something a hero has put down. It lands beside them, for anyone to pick up, and it's only something being put down: what's found has all the noise of being found, and this has none.</summary>
        public void Lay(ServerPlayer hero, Item item)
        {
            Drop(Ground(hero.Sim.Position) + Vector3.UnitY * Height, item, 0f, laid: true);
        }

        private void Drop(Vector3 from, Item item, float delay, bool laid = false)
        {
            int find = _nextFind++;
            Rarity rarity = item.Rarity;
            Vector2 spot = Landing(from.Xz);
            ulong seed = item.LookSeed();
            Entity entity = Server.Ecs.CreateEntity("Find",
                new NetIdentityComponent(Server.Ghosts.NextId(), importance: 1.5f + (int)rarity),
                new PositionComponent(Ground(spot)),
                new VisualComponent(LootModels.Find, VisualKind.Prop),
                new PickupComponent { Find = find, Rarity = rarity, Slot = item.Slot, From = from, Born = Moment(delay), SeedLow = (uint)seed, SeedHigh = (uint)(seed >> 32), Traits = ItemLook.Traits(item), Laid = laid });

            _lying.Add(find, new Lying(entity, item, spot));
            foreach (ServerPlayer hero in Server.Heroes) Tell(hero, find, item);
        }

        private void DropGold(Vector3 from, int gold, float delay)
        {
            Vector2 spot = Landing(from.Xz);
            Server.Ecs.CreateEntity("Gold",
                new NetIdentityComponent(Server.Ghosts.NextId()),
                new PositionComponent(Ground(spot)),
                new VisualComponent(LootModels.Gold, VisualKind.Prop),
                new PickupComponent { From = from, Born = Moment(delay), Gold = gold, Rest = Ground(spot) });
        }

        private uint Moment(float delay) => Server.Tick + (uint)(delay * NetTime.TicksPerSecond);

        private Vector3 Ground(Vector2 at) => new Vector3(at.X, _session.World.Height.Sample(at), at.Y);

        // Somewhere a hero can get to and see it. Where something died can be up against a wall or at the edge of a lake, and a find nobody can reach is worse than none.
        private Vector2 Landing(Vector2 from)
        {
            WorldData world = _session.World;
            for (int attempt = 0; attempt < LandingTries; attempt++)
            {
                float angle = _random.Range(0f, MathF.Tau);
                Vector2 spot = from + new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * _random.Range(NearestLanding, FarthestLanding);
                if (world.InBounds(spot) && world.Passable.IsSet(spot) && world.WaterDepthAt(spot) <= DeepestLanding && SameSide(from, spot)) return spot;
            }
            return from;
        }

        // Nothing lands the far side of a wall from what it came out of. Whatever that was may itself be up against one, so it's only what's between once clear of it that counts.
        private bool SameSide(Vector2 from, Vector2 spot)
        {
            WorldData world = _session.World;
            int steps = (int)((spot - from).Length / LandingLook) + 1;
            bool clear = false;
            for (int i = 1; i <= steps; i++)
            {
                bool passable = world.Passable.IsSet(Vector2.Lerp(from, spot, (float)i / steps));
                if (clear && !passable) return false;
                clear |= passable;
            }
            return true;
        }

        // Names and lines can't go in a component, as replicated strings are a table that only grows. No item at all means it's gone.
        private static void Tell(ServerPlayer hero, int find, Item? item)
        {
            BitWriter writer = Protocol.Begin(Message.GroundItem);
            writer.WriteVarInt(find);
            writer.WriteBool(item != null);
            if (item != null) Protocol.WriteItem(writer, item);
            hero.Connection.Send(writer.ToArray(), Delivery.Reliable);
        }

        /// <summary>Someone new is here, and needs telling what's already lying about.</summary>
        public void Introduce(ServerPlayer hero)
        {
            foreach ((int find, Lying lying) in _lying) Tell(hero, find, lying.Item);
        }

        /// <summary>A hero wants something. It's theirs if it's still there and they get to it in the next moment.</summary>
        public void Ask(ServerPlayer hero, int find)
        {
            if (hero.Asked.Count < MostAsked) hero.Asked.Add((find, Moment(AskPatience)));
        }

        /// <summary>Runs every tick for a hero who's on their feet.</summary>
        public void Serve(ServerPlayer hero)
        {
            for (int i = hero.Asked.Count - 1; i >= 0; i--)
            {
                (int find, uint until) = hero.Asked[i];
                if (!_lying.TryGetValue(find, out Lying lying) || Server.Tick > until)
                {
                    hero.Asked.RemoveAt(i);
                    continue;
                }

                if ((lying.At - hero.Sim.Position).LengthSquared > Loot.ServerReach * Loot.ServerReach) continue;
                hero.Asked.RemoveAt(i);
                Take(hero, find, lying);
            }
        }

        private void Take(ServerPlayer hero, int find, in Lying lying)
        {
            if (!hero.Hero.Pickup(lying.Item))
            {
                Server.Note(hero, "Your pack is full.");
                return;
            }

            _lying.Remove(find);
            Server.Ecs.DestroyEntity(lying.Entity);
            foreach (ServerPlayer everyone in Server.Heroes) Tell(everyone, find, null);
            hero.GearChanged = true;
            if (lying.Item.Unique is string unique && hero.Hero.Records.Found(unique)) hero.SummaryChanged = true;
            Server.Note(hero, $"Found: {lying.Item.Name}");
        }

        /// <summary>For a scripted run: one of every rarity in front of a hero, or everything a tier gives, to look at and listen to.</summary>
        public void Show(Vector2 at, string which)
        {
            int level = _session.World.Plan.Level + 8;
            Vector3 from = Ground(at + new Vector2(0f, 7f));
            if (Enum.TryParse(which, ignoreCase: true, out LootTier tier))
            {
                List<Item> rolled = Loot.Roll(tier, level, level, 0f, new LootLuck(), _random.Fork(_random.Int(1 << 30)));
                Spill(from, rolled, Loot.RollGold(tier, level, 0f, _random), LootTables.Of(tier).GoldPiles);
                return;
            }

            var items = new List<Item>();
            foreach (Rarity rarity in Enum.GetValues<Rarity>()) items.Add(ItemGenerator.Forge(level, _random.Fork((int)rarity), rarity, 0.5f));
            for (int i = 0; which == "folios" && i < 300; i++)
            {
                Item folio = ItemGenerator.Folio(level, _random.Fork("folios").Fork(i), (Rarity)(i % 3));
                if (items.Count(item => item.Slot == folio.Slot) < FoliosOfEach) items.Add(folio);
            }
            Spill(from, items, 0, 0);
        }
    }
}
