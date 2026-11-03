using Dino_Defenders.Progression;
using Dino_Engine.Core;
using Dino_Engine.ECS.Components;
using Dino_Engine.ECS.ECS_Architecture;
using Dino_Engine.Modelling;
using Dino_Engine.Modelling.Model;
using Dino_Engine.Physics;
using Dino_Engine.Util;
using Dino_Engine.World;
using OpenTK.Mathematics;

namespace Dino_Defenders.Gameplay
{
    /// <summary>The rules of a fight: how much a blow does, what happens when something dies, and what it leaves behind.</summary>
    public static class Combat
    {
        private static readonly Vector3 HitColour = new Vector3(1f, 0.92f, 0.7f);
        private static readonly Vector3 CritColour = new Vector3(1f, 0.55f, 0.15f);
        private static readonly Vector3 HurtColour = new Vector3(1f, 0.25f, 0.2f);

        private static readonly BitMask EnemyMask = new BitMask(typeof(EnemyComponent), typeof(HealthComponent));
        private static readonly List<Entity> Found = new();
        private static readonly WorldRandom Luck = new WorldRandom(Environment.TickCount);

        /// <summary>Armour takes a share of a blow; the more of it against a stronger enemy, the less it helps.</summary>
        public static float Mitigate(float damage, float armour, int attackerLevel)
        {
            return damage * (1f - armour / (armour + 45f + attackerLevel * 12f));
        }

        /// <summary>Strikes every enemy in a cone from <paramref name="origin"/>. A full circle if <paramref name="arc"/> is tau.</summary>
        public static int StrikeEnemies(ECSWorld world, GameSession session, Vector2 origin, Vector2 facing, float reach, float arc, float multiplier)
        {
            int hits = 0;
            Found.Clear();
            Found.AddRange(world.QueryEntities(EnemyMask, BitMask.Empty));
            float cosHalfArc = MathF.Cos(arc * 0.5f);

            foreach (Entity entity in Found)
            {
                EntityView view = world.GetEntityView(entity);
                if (view.Get<EnemyComponent>().State == EnemyState.Dead) continue;

                Vector2 position = view.Get<PositionComponent>().value.Xz;
                float scale = view.Get<ScaleComponent>().value.X;
                Vector2 offset = position - origin;
                float distance = offset.Length;
                if (distance > reach + scale) continue;
                if (arc < MathF.Tau && distance > 0.5f && Vector2.Dot(offset / distance, facing) < cosHalfArc) continue;

                HitEnemy(world, session, entity, multiplier, origin);
                hits++;
            }

            if (hits > 0 && session.Stats.LifeOnHit > 0f)
            {
                session.Health = Math.Min(session.Health + session.Stats.LifeOnHit * hits, session.Stats.MaxHealth);
            }
            return hits;
        }

        private static void HitEnemy(ECSWorld world, GameSession session, Entity entity, float multiplier, Vector2 from)
        {
            EntityView view = world.GetEntityView(entity);
            var enemy = view.Get<EnemyComponent>();
            var health = view.Get<HealthComponent>();
            Vector3 position = view.Get<PositionComponent>().value;

            HeroStats stats = session.Stats;
            bool crit = Luck.Float() < stats.CritChance;
            float damage = stats.Damage * multiplier * Luck.Range(0.9f, 1.1f);
            if (crit) damage *= stats.CritMultiplier;
            damage = Mitigate(damage, enemy.Armour, session.Hero.Level);

            health.Current -= damage;
            view.Set(health);

            Vector3 head = position + Vector3.UnitY * 5f * view.Get<ScaleComponent>().value.Y;
            session.Say(head, $"{damage:0}", crit ? CritColour : HitColour, crit ? 22f : 16f, 0.9f);
            Spark(world, position + Vector3.UnitY * 2.6f, new Colour(0.8f, 0.1f, 0.06f, 1.5f), 6);

            // A hit wakes whoever was struck, even from afar.
            if (enemy.State is EnemyState.Idle or EnemyState.Returning)
            {
                enemy.State = EnemyState.Chasing;
                view.Set(enemy);
            }

            if (health.Current <= 0f) Kill(world, session, entity, position, from);
        }

        private static void Kill(ECSWorld world, GameSession session, Entity entity, Vector3 position, Vector2 from)
        {
            EntityView view = world.GetEntityView(entity);
            var enemy = view.Get<EnemyComponent>();

            Vector2 away = position.Xz - from;
            away = away.LengthSquared > 0.01f ? away.Normalized() : Vector2.UnitX;
            enemy.State = EnemyState.Dead;
            enemy.StateTime = 0f;
            enemy.DeathScale = view.Get<ScaleComponent>().value;
            enemy.DeathRotation = view.Get<RotationComponent>().quaternion;
            enemy.FallAxis = new Vector3(away.Y, 0f, -away.X);
            view.Set(enemy);
            view.Set(new VelocityComponent());

            SiteState state = session.Sites[enemy.SiteIndex];
            state.Dead.Add(enemy.SpawnIndex);
            state.Live.Remove(entity);

            session.GiveExperience(enemy.Experience);
            DropLoot(world, session, enemy, position);
            session.OnHostileKilled(state);
        }

        public static void HurtHero(ECSWorld world, GameSession session, float damage, int attackerLevel)
        {
            if (session.Health <= 0f) return;
            EntityView hero = world.GetEntityView(session.Player);
            if (hero.Get<HeroInvulnerableComponent>().Time > 0f) return;

            float taken = Mitigate(damage, session.Stats.Armour, attackerLevel);
            session.Health -= taken;

            Vector3 position = hero.Get<PositionComponent>().value;
            session.Say(position + Vector3.UnitY * 5f, $"{taken:0}", HurtColour, 17f, 0.9f);
            Spark(world, position + Vector3.UnitY * 2.6f, new Colour(0.8f, 0.1f, 0.06f, 1.5f), 5);
        }

        private static void DropLoot(ECSWorld world, GameSession session, in EnemyComponent enemy, Vector3 position)
        {
            float rank = enemy.Rank switch { EnemyRank.Boss => 10f, EnemyRank.Elite => 3f, _ => 1f };

            if (Luck.Chance(0.5f * MathF.Min(rank, 2f)))
            {
                int gold = (int)(Luck.Range(3f, 9f) * enemy.Level * rank * session.Stats.GoldFind);
                SpawnPickup(world, position, null, gold);
            }

            float itemChance = 0.14f * rank;
            int drops = enemy.Rank == EnemyRank.Boss ? 3 : 1;
            for (int i = 0; i < drops; i++)
            {
                if (!Luck.Chance(itemChance)) continue;
                Rarity? least = enemy.Rank == EnemyRank.Boss ? Rarity.Rare : enemy.Rank == EnemyRank.Elite ? Rarity.Magic : null;
                Item item = ItemGenerator.Roll(enemy.Level + 1, Luck.Fork(Luck.Int(1 << 30)), session.Stats.ItemFind, least);
                SpawnPickup(world, position, item, 0);
            }
        }

        public static void OpenChest(ECSWorld world, GameSession session, Vector3 position, int level)
        {
            int gold = (int)(Luck.Range(14f, 30f) * level * session.Stats.GoldFind);
            SpawnPickup(world, position, null, gold);
            SpawnPickup(world, position, ItemGenerator.Roll(level + 1, Luck.Fork(Luck.Int(1 << 30)), session.Stats.ItemFind + 0.8f, Rarity.Magic), 0);
            if (Luck.Chance(0.4f))
            {
                SpawnPickup(world, position, ItemGenerator.Roll(level + 1, Luck.Fork(Luck.Int(1 << 30)), session.Stats.ItemFind + 0.8f), 0);
            }
        }

        private static glModel? _goldModel;
        private static glModel? _itemModel;

        private static void SpawnPickup(ECSWorld world, Vector3 position, Item? item, int gold)
        {
            _goldModel ??= glLoader.loadToVAO(IcoSphereBall(new Colour(1f, 0.78f, 0.25f, 2.2f)));
            _itemModel ??= glLoader.loadToVAO(IcoSphereBall(new Colour(1f, 1f, 1f, 2.2f)));

            Vector2 scatter = new Vector2(Luck.Range(-1f, 1f), Luck.Range(-1f, 1f)).Normalized() * Luck.Range(1f, 3f);
            Vector3 rest = position + new Vector3(scatter.X, 0.6f, scatter.Y);
            float size = item != null ? 0.32f : 0.2f;
            world.CreateEntity("Pickup",
                new PositionComponent(rest),
                new RotationComponent(new Vector3(0f)),
                new ScaleComponent(new Vector3(size)),
                new ModelComponent(item != null ? _itemModel : _goldModel),
                new ModelRenderTag(),
                new LocalToWorldMatrixComponent(),
                new PickupComponent { Item = item, Gold = gold, Rest = rest });
        }

        private static Mesh IcoSphereBall(Colour colour)
        {
            return Dino_Engine.Modelling.Procedural.IcoSphereGenerator.CreateIcosphere(1, new VertexMaterial(Dino_Engine.Textures.TextureGenerator.flatGlow, colour));
        }

        /// <summary>A short burst of particles.</summary>
        public static void Spark(ECSWorld world, Vector3 position, Colour colour, int count, float speed = 6f)
        {
            for (int i = 0; i < count; i++)
            {
                Vector3 direction = new Vector3(Luck.Range(-1f, 1f), Luck.Range(0.2f, 1.2f), Luck.Range(-1f, 1f)).Normalized();
                world.CreateEntity(
                    new ParticleRenderTag(),
                    new LocalToWorldMatrixComponent(),
                    new PositionComponent(position),
                    new ScaleComponent(new Vector3(Luck.Range(0.1f, 0.22f))),
                    new ColorComponent(colour),
                    new VelocityComponent(direction * speed * Luck.Range(0.5f, 1.2f)),
                    new MassComponent(1f),
                    new selfDestroyComponent(Luck.Range(0.25f, 0.6f)));
            }
        }
    }
}
