using Dino_Defenders.Gameplay;
using Dino_Defenders.Progression;
using Dino_Engine.ECS.Components;
using Dino_Engine.ECS.ECS_Architecture;
using Dino_Engine.Networking;
using Dino_Engine.World;
using OpenTK.Mathematics;

namespace Dino_Defenders.Server
{
    /// <summary>The rules of a fight: how much a blow does, what happens when something dies, and what it leaves behind.</summary>
    public sealed class Combat
    {
        private const int HistoryTicks = 32;
        private const int MostRewind = 15;      // A quarter of a second: past this, what a player saw is too old to hold against anyone else.

        private readonly ServerSession _session;
        private readonly BitMask _enemyMask = new BitMask(typeof(EnemyComponent), typeof(EnemyBrainComponent), typeof(HealthComponent));
        private readonly List<Entity> _found = new();
        private readonly WorldRandom _luck = new WorldRandom(Environment.TickCount);
        private readonly Dictionary<uint, Trail> _trails = new();
        private readonly List<uint> _forgotten = new();

        // Where something has been lately. Keyed by ghost number: entity numbers are reused, and a newcomer mustn't inherit a dead man's past.
        private sealed class Trail
        {
            public readonly Vector2[] Positions = new Vector2[HistoryTicks];
            public uint LastSeen;
        }

        public Combat(ServerSession session)
        {
            _session = session;
        }

        private ECSWorld World => _session.Server.Ecs;

        /// <summary>Armour takes a share of a blow; the more of it against a stronger enemy, the less it helps.</summary>
        public static float Mitigate(float damage, float armour, int attackerLevel)
        {
            return damage * (1f - armour / (armour + 45f + attackerLevel * 12f));
        }

        /// <summary>Remembers where every enemy is, so that a blow can be judged against what the player who threw it was looking at.</summary>
        public void RecordPositions(uint tick)
        {
            int slot = (int)(tick % HistoryTicks);
            foreach (Archetype archetype in World.QueryArchetypes(_enemyMask, BitMask.Empty))
            {
                List<PositionComponent> positions = archetype.GetComponentArray<PositionComponent>()!;
                List<NetIdentityComponent> identities = archetype.GetComponentArray<NetIdentityComponent>()!;
                for (int i = 0; i < archetype.EntityCount; i++)
                {
                    uint id = identities[i].Id;
                    Vector2 position = positions[i].value.Xz;
                    if (!_trails.TryGetValue(id, out Trail? trail))
                    {
                        trail = new Trail();
                        Array.Fill(trail.Positions, position);
                        _trails.Add(id, trail);
                    }
                    trail.Positions[slot] = position;
                    trail.LastSeen = tick;
                }
            }

            if (tick % HistoryTicks != 0) return;
            _forgotten.Clear();
            foreach ((uint id, Trail trail) in _trails)
            {
                if (trail.LastSeen != tick) _forgotten.Add(id);
            }
            foreach (uint id in _forgotten) _trails.Remove(id);
        }

        /// <summary>
        /// Strikes every enemy in a cone from <paramref name="origin"/>. A full circle if <paramref name="arc"/> is tau. An enemy is
        /// where the attacker saw it, which is where it was a moment ago: a client draws others slightly in the past, and a swing
        /// that connects on screen has to connect.
        /// </summary>
        public int StrikeEnemies(ServerPlayer attacker, Vector2 origin, Vector2 facing, float reach, float arc, float multiplier, int viewLag)
        {
            int hits = 0;
            _found.Clear();
            foreach (Archetype archetype in World.QueryArchetypes(_enemyMask, BitMask.Empty)) _found.AddRange(archetype.entities);
            float cosHalfArc = MathF.Cos(arc * 0.5f);
            int rewind = Math.Clamp(viewLag, 0, MostRewind);

            foreach (Entity entity in _found)
            {
                EntityView view = World.GetEntityView(entity);
                if (view.Get<EnemyComponent>().State == EnemyState.Dead) continue;

                // This tick's positions aren't recorded until it ends, so with nothing to rewind, where it is now is the answer.
                Vector2 position = view.Get<PositionComponent>().value.Xz;
                if (rewind > 0 && _trails.TryGetValue(view.Get<NetIdentityComponent>().Id, out Trail? trail))
                {
                    position = trail.Positions[(int)((_session.Server.Tick + HistoryTicks - (uint)rewind) % HistoryTicks)];
                }

                float scale = view.Get<ScaleComponent>().value.X;
                Vector2 offset = position - origin;
                float distance = offset.Length;
                if (distance > reach + scale) continue;
                if (arc < MathF.Tau && distance > 0.5f && Vector2.Dot(offset / distance, facing) < cosHalfArc) continue;

                HitEnemy(attacker, entity, multiplier, origin);
                hits++;
            }

            if (hits > 0 && attacker.Stats.LifeOnHit > 0f)
            {
                attacker.Health = Math.Min(attacker.Health + attacker.Stats.LifeOnHit * hits, attacker.Stats.MaxHealth);
            }
            return hits;
        }

        private void HitEnemy(ServerPlayer attacker, Entity entity, float multiplier, Vector2 from)
        {
            EntityView view = World.GetEntityView(entity);
            var enemy = view.Get<EnemyComponent>();
            var health = view.Get<HealthComponent>();
            Vector3 position = view.Get<PositionComponent>().value;

            HeroStats stats = attacker.Stats;
            bool crit = _luck.Float() < stats.CritChance;
            float damage = stats.Damage * multiplier * _luck.Range(0.9f, 1.1f);
            if (crit) damage *= stats.CritMultiplier;
            damage = Mitigate(damage, view.Get<EnemyBrainComponent>().Armour, attacker.Hero.Level);

            health.Current -= damage;
            view.Set(health);

            Vector3 head = position + Vector3.UnitY * 5f * view.Get<ScaleComponent>().value.Y;
            _session.Server.Broadcast(new DamageEvent { Position = head, Amount = damage, Critical = crit, Body = BodyOf(view) }, position.Xz);
            _session.Server.Broadcast(new SparkEvent { Position = position + Vector3.UnitY * 2.6f, Kind = SparkKind.Blood, Count = 6, Speed = 6f }, position.Xz);

            // A hit wakes whoever was struck, even from afar.
            if (enemy.State is EnemyState.Idle or EnemyState.Returning)
            {
                enemy.State = EnemyState.Chasing;
                view.Set(enemy);
            }

            if (health.Current <= 0f) Kill(entity, position, from);
        }

        private static HitBody BodyOf(in EntityView enemy)
        {
            string model = enemy.Get<VisualComponent>().Model;
            return model == "skeleton" ? HitBody.Bone : model == "warlord" ? HitBody.Armour : HitBody.Flesh;
        }

        private void Kill(Entity entity, Vector3 position, Vector2 from)
        {
            EntityView view = World.GetEntityView(entity);
            var enemy = view.Get<EnemyComponent>();
            var brain = view.Get<EnemyBrainComponent>();

            Vector2 away = position.Xz - from;
            away = away.LengthSquared > 0.01f ? away.Normalized() : Vector2.UnitX;
            enemy.State = EnemyState.Dead;
            enemy.FallAxis = new Vector3(away.Y, 0f, -away.X);
            brain.StateTime = 0f;
            view.Set(enemy);
            view.Set(brain);
            view.Set(new VelocityComponent());

            SiteState state = _session.Sites[brain.SiteIndex];
            state.Dead.Add(brain.SpawnIndex);
            state.Live.Remove(entity);

            _session.ShareExperience(position.Xz, brain.Experience);
            DropLoot(enemy, position);
            _session.OnHostileKilled(state);
        }

        public void HurtHero(ServerPlayer hero, float damage, int attackerLevel)
        {
            if (hero.Health <= 0f || hero.Sim.Safe > 0f) return;

            float taken = Mitigate(damage, hero.Stats.Armour, attackerLevel);
            hero.Health -= taken;

            Vector3 position = _session.Server.HeroPosition(hero);
            _session.Server.Broadcast(new DamageEvent { Position = position + Vector3.UnitY * 5f, Amount = taken, ToHero = true }, position.Xz);
            _session.Server.Broadcast(new SparkEvent { Position = position + Vector3.UnitY * 2.6f, Kind = SparkKind.Blood, Count = 5, Speed = 6f }, position.Xz);
        }

        private void DropLoot(in EnemyComponent enemy, Vector3 position)
        {
            float rank = enemy.Rank switch { EnemyRank.Boss => 10f, EnemyRank.Elite => 3f, _ => 1f };
            float goldFind = BestOfParty(s => s.GoldFind);
            float itemFind = BestOfParty(s => s.ItemFind);

            if (_luck.Chance(0.5f * MathF.Min(rank, 2f)))
            {
                int gold = (int)(_luck.Range(3f, 9f) * enemy.Level * rank * goldFind);
                SpawnPickup(position, null, gold);
            }

            float itemChance = 0.14f * rank;
            int drops = enemy.Rank == EnemyRank.Boss ? 3 : 1;
            for (int i = 0; i < drops; i++)
            {
                if (!_luck.Chance(itemChance)) continue;
                Rarity? least = enemy.Rank == EnemyRank.Boss ? Rarity.Rare : enemy.Rank == EnemyRank.Elite ? Rarity.Magic : null;
                Item item = ItemGenerator.Roll(enemy.Level + 1, _luck.Fork(_luck.Int(1 << 30)), itemFind, least);
                SpawnPickup(position, item, 0);
            }
        }

        private float BestOfParty(Func<HeroStats, float> stat)
        {
            float best = 0f;
            foreach (ServerPlayer hero in _session.Heroes) best = MathF.Max(best, stat(hero.Stats));
            return best;
        }

        public void OpenChest(ServerPlayer opener, Vector3 position, int level)
        {
            _session.Server.Broadcast(new SoundEvent { Position = position, Cue = SoundCue.Chest }, position.Xz);
            int gold = (int)(_luck.Range(14f, 30f) * level * opener.Stats.GoldFind);
            SpawnPickup(position, null, gold);
            SpawnPickup(position, ItemGenerator.Roll(level + 1, _luck.Fork(_luck.Int(1 << 30)), opener.Stats.ItemFind + 0.8f, Rarity.Magic), 0);
            if (_luck.Chance(0.4f))
            {
                SpawnPickup(position, ItemGenerator.Roll(level + 1, _luck.Fork(_luck.Int(1 << 30)), opener.Stats.ItemFind + 0.8f), 0);
            }
        }

        private void SpawnPickup(Vector3 position, Item? item, int gold)
        {
            Vector2 scatter = new Vector2(_luck.Range(-1f, 1f), _luck.Range(-1f, 1f)).Normalized() * _luck.Range(1f, 3f);
            Vector3 rest = position + new Vector3(scatter.X, 0.6f, scatter.Y);
            World.CreateEntity("Pickup",
                new NetIdentityComponent(_session.Server.Ghosts.NextId()),
                new PositionComponent(rest),
                new RotationComponent(new Vector3(0f)),
                new ScaleComponent(new Vector3(item != null ? 0.32f : 0.2f)),
                new VisualComponent(item != null ? "item" : "gold", VisualKind.Prop),
                new PickupComponent { Item = item, Gold = gold, Rest = rest });
        }
    }
}
