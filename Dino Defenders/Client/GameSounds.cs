using Dino_Defenders.Gameplay;
using Dino_Defenders.Progression;
using Dino_Defenders.Books;
using Dino_Defenders.Stats;
using Dino_Engine.Animation;
using Dino_Engine.Core;
using Dino_Engine.ECS.Components;
using Dino_Engine.ECS.ECS_Architecture;
using Dino_Engine.ECS.Systems;
using Dino_Engine.Modelling.Procedural.Saurian;
using Dino_Engine.Physics;
using Dino_Engine.Sound;
using Dino_Engine.Sound.Content;
using Dino_Engine.Sound.Dsp;
using Dino_Engine.World;
using Dino_Engine.World.Content;
using OpenTK.Mathematics;

namespace Dino_Defenders.Client
{
    /// <summary>What a body's feet were last in.</summary>
    public struct FootfallComponent : IComponent
    {
        public bool InWater;
    }

    /// <summary>
    /// What things sound like happening, as <see cref="ClientEffects"/> is what they look like: everything the server tells this player, turned into sound.
    /// A hero's own blow can't wait for that answer, though. By the time it comes back a sound would feel late and soft, which is the
    /// worst thing a hit can be, so this player's strikes are judged here at once, and the server's word on them only adds to it.
    /// </summary>
    public sealed class GameSounds
    {
        private const float PredictionLife = 0.3f;      // Seconds the server has to agree with a hit heard early.
        private const float Effort = 0.3f;              // Share of swings that get a grunt.
        private const float ArenaGatePitch = 0.55f;
        private const float OwnReach = 6f;              // Metres from this hero within which something happened to them.
        private const float HeavyBlow = 0.5f;           // Of what something has, for the camera to feel it.
        private const float BlowHeard = 22f;            // Metres off that anything living takes fright at a blow.

        private readonly ECSWorld _world;
        private readonly GameClient _client;
        private readonly ClientEffects _effects;
        private readonly SoundRandom _random = new((ulong)Environment.TickCount64);
        private readonly BitMask _enemyMask = new BitMask(typeof(EnemyComponent), typeof(PositionComponent), typeof(ScaleComponent), typeof(AnimatorComponent));
        private readonly List<(float at, SoundDefinition sound, Vector3 position, float decibels)> _later = new();
        private int _predicted;
        private float _predictedUntil;

        private static float Now => Engine.Time;

        public GameSounds(ECSWorld world, GameClient client, ClientEffects effects)
        {
            _world = world;
            _client = client;
            _effects = effects;

            client.Events.On<DamageEvent>(Hit);
            client.Events.On<HeroMoveEvent>(e => Moves(e.Move, e.Position, e.Sign != PageSign.None ? PageSign.Read(e.Sign) : null, 1f));
            client.Events.On<TextEvent>(e =>
            {
                if (!e.Speech) return;

                // Who says it decides the voice: it's the same person each time they speak.
                int who = (int)(e.Position.X * 7f + e.Position.Z * 13f);
                Audio.Play((who & 3) switch { 0 => Voices.MurmurHigh, 1 => Voices.MurmurOld, _ => Voices.Murmur }, e.Position);
            });
            client.Events.On<SoundEvent>(Cue);
        }

        /// <summary>Plays something a moment from now, for what follows from what's just happened.</summary>
        public void After(float seconds, SoundDefinition sound, Vector3 position, float decibels = 0f)
        {
            _later.Add((Now + seconds, sound, position, decibels));
        }

        public void Update()
        {
            for (int i = _later.Count - 1; i >= 0; i--)
            {
                if (_later[i].at > Now) continue;
                Audio.Play(_later[i].sound, _later[i].position, _later[i].decibels);
                _later.RemoveAt(i);
            }
        }

        /// <summary>This player's hero, the moment they acted.</summary>
        public void HeroActed(HeroStepEvents events, Vector3 position, Vector2 aim, BookProfile? book)
        {
            float rate = book != null ? 0.6f / MathF.Max(book.UseTime, 0.05f) : 1f;
            Moves(events, position, book?.Sign, rate);

            if ((events & HeroStepEvents.Landed) != 0 && book != null && book.Sign.Form == PageForm.Strike && book.First.DealsDamage)
            {
                Predict(position, aim, book);
            }
        }

        private void Moves(HeroStepEvents events, Vector3 position, PageSign? sign, float rate)
        {
            if ((events & HeroStepEvents.Used) != 0 && sign is PageSign look)
            {
                if (look.Spell)
                {
                    Audio.Play(Magic.Cast(ClientEffects.ElementOf(look.Look)), position);
                }
                else
                {
                    Audio.Play(look.Form == PageForm.Nova ? Blades.Whirl : Blades.Swing, position, 0f, MathF.Sqrt(rate));
                    if (_random.Chance(Effort)) After(0.03f, Voices.HeroEffort, position);
                }
            }
            if ((events & HeroStepEvents.Dashed) != 0) Audio.Play(Blades.Dash, position);
        }

        /// <summary>A patch of ground starting to do something.</summary>
        public void ZoneOpens(DamageType look, Vector3 position)
        {
            Audio.Play(Magic.Aura(ClientEffects.ElementOf(look)), position);
        }

        /// <summary>Into the pack, or not: that's only ever this player's to hear.</summary>
        public void Reached(Item item, bool room)
        {
            if (!room)
            {
                Audio.Play(Interface.Refuse);
                return;
            }

            Audio.Play(item.Rarity switch
            {
                Rarity.Unique => Treasure.ItemUnique,
                Rarity.Rare => Treasure.ItemRare,
                Rarity.Magic => Treasure.ItemMagic,
                _ => Treasure.ItemNormal,
            });
        }

        /// <summary>A unique's sound takes as long as its flight, and arrives when it does.</summary>
        public void LootLaunched(Rarity rarity, Vector3 position)
        {
            if (rarity == Rarity.Unique) Audio.Play(Treasure.DropUnique);
        }

        /// <summary>A find coming down: what it is, by what it hits the ground with, and what it's worth, by the note that follows.</summary>
        public void LootLanded(Rarity rarity, ItemSlot slot, bool gold, Vector3 position)
        {
            if (gold)
            {
                Audio.Play(Treasure.Coin, position);
                return;
            }

            Audio.Play(slot switch
            {
                ItemSlot.Finger or ItemSlot.Neck => Treasure.LandJewel,
                ItemSlot.Body or ItemSlot.Legs or ItemSlot.Feet or ItemSlot.Hands => Treasure.LandCloth,
                _ => Treasure.LandMetal,
            }, position);
            if (rarity == Rarity.Rare) Audio.Play(Treasure.DropRare, position);
        }

        /// <summary>Something good found lying there has its note all the same, as that's how a player knows to look.</summary>
        public void LootDiscovered(Rarity rarity, Vector3 position)
        {
            if (rarity == Rarity.Unique) Audio.Play(Treasure.DropUnique);
            else if (rarity == Rarity.Rare) Audio.Play(Treasure.DropRare, position);
        }

        /// <summary>A page going off somewhere.</summary>
        public void Burst(DamageType look, Vector3 position)
        {
            Audio.Play(Magic.Burst(ClientEffects.ElementOf(look)), position);
        }

        // The hero's own stroke, tested against what's on this screen, the same test the server is about to make.
        private void Predict(Vector3 origin, Vector2 facing, BookProfile book)
        {
            GroundShape shape = GroundShape.Of(book, origin.Xz, facing, 0f);
            int hits = 0;
            foreach (Archetype archetype in _world.QueryArchetypes(_enemyMask, BitMask.Empty))
            {
                foreach (EntityView entity in new ComponentAccessor(archetype))
                {
                    var enemy = entity.Get<EnemyComponent>();
                    if (enemy.State == EnemyState.Dead || enemy.Friendly) continue;
                    Vector3 position = entity.Get<PositionComponent>().value;
                    if (!shape.Covers(position.Xz, WorldData.CharacterRadius * entity.Get<ScaleComponent>().value.X)) continue;

                    Audio.Play(BodyHit(BodyOf(entity)), position + Vector3.UnitY * 2.6f);
                    hits++;

                    // How much of them it took is a guess until the server says, but a good enough one to show the moment it lands.
                    Vector2 way = position.Xz - origin.Xz;
                    float health = entity.Has<HealthComponent>() ? MathF.Max(entity.Get<HealthComponent>().Max, 1f) : 100f;
                    float weight = book.First.AverageHit / health;
                    _effects.Connected(entity, way.LengthSquared > 0.01f ? way.Normalized() : facing, weight, weight >= HeavyBlow, position + Vector3.UnitY * 3.2f);
                }
            }

            if (hits == 0) return;
            _predicted += hits;
            _predictedUntil = Now + PredictionLife;
        }

        private static HitBody BodyOf(in EntityView entity)
        {
            string model = entity.Has<VisualComponent>() ? entity.Get<VisualComponent>().Model : "";
            if (SaurianSpec.Parse(model) is SaurianSpec dinosaur) return dinosaur.Condition == SaurianCondition.Bones ? HitBody.Bone : HitBody.Beast;
            return model.StartsWith("skeleton") ? HitBody.Bone : model is "warlord" or "revenant" or "reich:legionary" ? HitBody.Armour : CharacterFactory.BeastOf(model) != null ? HitBody.Beast : HitBody.Flesh;
        }

        // What a dinosaur says, by what it is and what's been done to it. Bones have no throat, and what's rotting only hisses.
        internal static SoundDefinition? DinosaurVoice(string model, DinosaurMoment moment)
        {
            if (SaurianSpec.Parse(model) is not SaurianSpec dinosaur) return null;
            if (dinosaur.Condition == SaurianCondition.Bones) return moment == DinosaurMoment.Dies ? Voices.DeadDeath : Voices.DeadMoan;
            if (dinosaur.Condition == SaurianCondition.Rotting && moment != DinosaurMoment.Dies) return dinosaur.Species == "tyrant" && moment == DinosaurMoment.Notices ? Saurians.TyrantRoar : Saurians.RotHiss;
            return (dinosaur.Species, moment) switch
            {
                ("tyrant", DinosaurMoment.Notices) => Saurians.TyrantRoar,
                ("tyrant", DinosaurMoment.Strikes) => Saurians.TyrantGrowl,
                ("tyrant", _) => Saurians.TyrantDeath,
                ("raptor" or "ripper", DinosaurMoment.Dies) => Saurians.RaptorDeath,
                ("raptor" or "ripper", _) => Saurians.RaptorCall,
                ("skitter", _) => Saurians.Chirp,
                ("spitter", DinosaurMoment.Dies) => Saurians.RaptorDeath,
                ("spitter", _) => Saurians.Hiss,
                ("hornback" or "spikeback", DinosaurMoment.Dies) => Saurians.Death,
                ("hornback" or "spikeback", _) => Saurians.Bellow,
                (_, DinosaurMoment.Dies) => Saurians.Death,
                _ => Saurians.Growl,
            };
        }

        internal enum DinosaurMoment { Notices, Strikes, Dies }

        private static SoundDefinition BodyHit(HitBody body) => body switch
        {
            HitBody.Bone => Impacts.Bone,
            HitBody.Armour => Impacts.Armour,
            _ => Impacts.Slash,
        };

        private void Hit(DamageEvent e)
        {
            // A fight can be heard, and it's as good as a shout to anything listening for one.
            Dino_Engine.ECS.Systems.Nature.Startle(e.Position.Xz, BlowHeard);

            if (e.Note != HitNote.None)
            {
                if (e.Note == HitNote.Blocked) Audio.Play(Impacts.Armour, e.Position);
                return;
            }

            if (e.Type != DamageType.Physical) Audio.Play(Magic.Hit(ClientEffects.ElementOf(e.Type)), e.Position);

            if (e.ToHero)
            {
                Audio.Play(Impacts.Heavy, e.Position);
                Audio.Play(Voices.HeroHurt, e.Position);
                return;
            }

            // This one was heard already, the moment it was struck. Only what couldn't be known then is left to add.
            bool early = _predicted > 0 && Now <= _predictedUntil;
            if (early) _predicted--;
            else
            {
                _predicted = 0;
                Audio.Play(BodyHit(e.Body), e.Position);
            }

            if (e.Critical) Audio.Play(Impacts.Critical, e.Position);
            if (e.Body == HitBody.Bone) return;
            if (_random.Chance(0.45f)) After(0.02f, e.Body == HitBody.Armour ? Voices.WarlordRoar : e.Body == HitBody.Beast ? Voices.BeastSnarl : Voices.OutlawHurt, e.Position, e.Body == HitBody.Armour ? -8f : 0f);
        }

        // Things that happen to one person are heard in their own head, and only from across the field by anyone else.
        private bool Mine(in SoundEvent e)
        {
            GameSession? session = _client.Session;
            return session != null && (e.Position.Xz - session.PlayerPosition).LengthSquared < OwnReach * OwnReach;
        }

        private void Cue(SoundEvent e)
        {
            switch (e.Cue)
            {
                case SoundCue.Coins:
                    Audio.Play(e.Value >= 60 ? Treasure.Hoard : e.Value >= 12 ? Treasure.Handful : Treasure.Coin, e.Position);
                    break;
                case SoundCue.Chest:
                    Audio.Play(Treasure.ChestOpen, e.Position);
                    break;
                case SoundCue.Flask:
                    Audio.Play(Treasure.Flask, e.Position);
                    break;
                case SoundCue.ShrineKindled:
                    Audio.Play(Treasure.Shrine);
                    break;
                case SoundCue.ShrineRest:
                    Audio.Play(Treasure.Rest);
                    break;
                case SoundCue.LevelUp:
                    Audio.Play(Treasure.LevelUp, Mine(e) ? 0f : -11f);
                    break;
                case SoundCue.Objective:
                    Audio.Play(Treasure.Objective);
                    break;
                case SoundCue.Liberated:
                    Audio.Play(Treasure.Liberation);
                    break;
                case SoundCue.Discovered:
                    Audio.Play(Treasure.Discovery);
                    break;
                case SoundCue.Waygate:
                    Audio.Play(Treasure.Waygate, Mine(e) ? 0f : -8f);
                    break;
                case SoundCue.Blessed:
                    Audio.Play(Treasure.Rest);
                    break;
                case SoundCue.ArenaGate:
                    // Iron as heavy as a portcullis rings low.
                    Audio.Play(Impacts.Armour, e.Position, 0f, ArenaGatePitch);
                    break;
            }
        }
    }

    /// <summary>Everything characters do that nobody needs telling: feet coming down, arms going up, arrows going by. All of it is read off what's already being drawn.</summary>
    public class CharacterSoundSystem : SystemBase
    {
        private const float FastestWalk = 6.5f;
        private const float SlowestStep = 1.2f;
        private const float FarFootfall = 44f;
        private const float GiantStride = 1.6f;    // Of a person's legs. Anything bigger shakes the ground as well as stepping on it.
        private const float WhizzDistance = 16f;
        private const float FallDelay = 0.42f;
        private const float ShinDepth = 0.28f;      // Metres of water at which a leg is pushing through it, not stepping in it.
        private const float WetSandHeight = 0.4f;   // Metres above the water that the sand stays wet.
        private const float BareGround = 0.4f;      // How little grass there is before a foot lands on the ground under it.
        private const float WetGround = 0.55f;      // How wet the ground has to be before it gives underfoot.
        private const float PuddleLevel = 1.2f;     // As Weather/Wetness.glsl has it, so that a puddle that's seen is one that's heard.
        private const float SnowedOver = 0.35f;     // How far snow has to have settled before a foot crunches through it rather than landing on what's under it.

        /// <summary>How many steps were heard on what, for a scripted run's report.</summary>
        public static Dictionary<Ground, int> StepsHeard { get; } = new();

        private readonly Func<GameSounds?> _sounds;
        private readonly BitMask _feet = new BitMask(typeof(FootfallComponent), typeof(AnimatorComponent), typeof(VelocityComponent), typeof(PositionComponent));
        private readonly BitMask _swings = new BitMask(typeof(ActionComponent), typeof(PositionComponent), typeof(VisualComponent));
        private readonly BitMask _enemies = new BitMask(typeof(EnemyComponent), typeof(PositionComponent), typeof(VisualComponent));
        private readonly BitMask _heroes = new BitMask(typeof(HeroComponent), typeof(HealthComponent), typeof(PositionComponent));
        private readonly BitMask _arrows = new BitMask(typeof(BallisticComponent), typeof(PositionComponent));

        public CharacterSoundSystem(Func<GameSounds?> sounds)
            : base(new BitMask(typeof(PositionComponent)))
        {
            _sounds = sounds;
        }

        protected override void UpdateEntity(EntityView entity, ECSWorld world, float deltaTime) { }

        protected override void UpdateFrame(ECSWorld world, float deltaTime)
        {
            GameSession session = GameSession.Current;
            GameSounds? sounds = _sounds();
            if (session == null || sounds == null) return;

            sounds.Update();
            Vector2 listener = session.PlayerPosition;
            Footfalls(world, session, listener);
            Swings(world, session);
            Enemies(world, sounds);
            Heroes(world);
            Arrows(world, session);
        }

        private void Footfalls(ECSWorld world, GameSession session, Vector2 listener)
        {
            foreach (Archetype archetype in world.QueryArchetypes(_feet, BitMask.Empty))
            {
                foreach (EntityView entity in new ComponentAccessor(archetype))
                {
                    // Heard from the feet themselves, so that what's seen and what's heard can't drift apart.
                    if (entity.Get<AnimatorComponent>().Animator.Footfalls == 0) continue;

                    var foot = entity.Get<FootfallComponent>();
                    float speed = entity.Get<VelocityComponent>().value.Length;
                    Vector3 position = entity.Get<PositionComponent>().value;
                    if (speed < SlowestStep || (position.Xz - listener).LengthSquared > FarFootfall * FarFootfall) continue;

                    // How big a body is goes by how long its legs are as well as how it's scaled: a tyrant at a scale of one stands twice a person's height at the hip.
                    float size = entity.Has<ScaleComponent>() ? entity.Get<ScaleComponent>().value.Y : 1f;
                    size *= entity.Get<AnimatorComponent>().Animator.Rig.HipHeight / Rig.Humanoid.HipHeight;
                    foot.InWater = Step(world, session.World, position, speed, size, foot.InWater);
                    if (size >= GiantStride) Audio.Play(Saurians.Tread, position, AudioFormat.GainToDecibels(MathF.Min(size / GiantStride, 2.5f)) - 3f, MathF.Sqrt(GiantStride / size));
                    entity.Set(foot);
                }
            }
        }

        // Says whether the foot came down in water.
        private static bool Step(ECSWorld ecs, WorldData world, Vector3 position, float speed, float size, bool wasInWater)
        {
            Vector2 at = position.Xz;
            float depth = world.WaterDepthAt(at);
            Ground ground = RainedOn(ecs, GroundUnder(world, at, depth), at, world.HallAt(at) != null);
            bool inWater = ground is Ground.Shallows or Ground.Water;
            StepsHeard[ground] = StepsHeard.GetValueOrDefault(ground) + 1;

            // Something bigger comes down heavier and lower.
            float scale = Math.Clamp(size, 0.6f, 3f);
            Audio.Play(Footsteps.On(ground, speed > FastestWalk), position, AudioFormat.GainToDecibels(MathF.Sqrt(scale)), 1f / MathF.Sqrt(scale));

            if (inWater && !wasInWater) Audio.Play(Splashes.Wade, position, AudioFormat.GainToDecibels(MathF.Sqrt(scale)), 1f / MathF.Sqrt(scale));
            return inWater;
        }

        // Rain changes what's underfoot: earth goes to mud, sand packs down, and a foot that lands where a puddle has filled lands in water, by the rule the ground is painted with. Settled snow covers it all.
        private static Ground RainedOn(ECSWorld ecs, Ground ground, Vector2 at, bool indoors)
        {
            if (ground is Ground.Shallows or Ground.Water) return ground;
            WeatherSample weather = Weather.At(ecs, at);
            // Snow that's settled is what a foot lands in, as it's what it presses its print into (SnowTrailSystem).
            if (!indoors && weather.SnowCover > SnowedOver) return Ground.Snow;
            if (weather.Flood > 0.05f && ecs.TryGetSingleton<TerrainGeneratorSingleton>(out Entity terrain))
            {
                float hollow = ecs.GetComponent<TerrainGeneratorSingleton>(terrain).Generator.HollowAt(at);
                if (weather.Flood * PuddleLevel - (1f - hollow) > 0f) return Ground.Shallows;
            }
            if (weather.Wet < WetGround) return ground;
            return ground switch
            {
                Ground.Dirt => Ground.Mud,
                Ground.Sand => Ground.WetSand,
                _ => ground,
            };
        }

        private static Ground GroundUnder(WorldData world, Vector2 at, float depth)
        {
            if (depth > ShinDepth) return Ground.Water;
            if (depth > 0.02f) return Ground.Shallows;

            Ground ground = Footsteps.GroundOf(world.SurfaceAt(at));
            float canopy = world.Canopy.Sample(at.X, at.Y);

            // Sand the water has been over is packed down and sounds nothing like the dry sand further up.
            if (ground == Ground.Sand && world.Height.Sample(at) - world.WaterLevelAt(at) < WetSandHeight) return Ground.WetSand;

            // A dead leaf layer lies under a canopy, whatever the ground's own surface is, and where little grows the foot finds what the grass would have been on.
            if (ground == Ground.Dirt && canopy > 0.55f) return Ground.Leaves;
            if (ground == Ground.Grass)
            {
                if (canopy > 0.75f) return Ground.Leaves;
                if (world.BiomeAt(at).GrassCover * (1f - canopy * 0.55f) < BareGround) return Ground.Dirt;
            }
            return ground;
        }

        private void Swings(ECSWorld world, GameSession session)
        {
            uint own = session.Client.OwnGhost?.Entity.Id ?? 0;
            foreach (Archetype archetype in world.QueryArchetypes(_swings, BitMask.Empty))
            {
                foreach (EntityView entity in new ComponentAccessor(archetype))
                {
                    var action = entity.Get<ActionComponent>();
                    if (action.Serial == action.Heard) continue;
                    action.Heard = action.Serial;
                    entity.Set(action);

                    if (entity.Entity.Id == own) continue;
                    Vector3 position = entity.Get<PositionComponent>().value;
                    string model = entity.Get<VisualComponent>().Model;
                    if (model.EndsWith("True") || model.EndsWith(":bow") || model == "reich:marksman") Audio.Play(Archery.Draw, position);
                    else if (GameSounds.DinosaurVoice(model, Roars(model, action.Sign) ? GameSounds.DinosaurMoment.Notices : GameSounds.DinosaurMoment.Strikes) is SoundDefinition strikes) Audio.Play(strikes, position);
                    else if (CharacterFactory.BeastOf(model) != null) Audio.Play(Voices.BeastSnarl, position);
                    else Audio.Play(model is "warlord" or "revenant" ? Blades.HeavySwing : Blades.Swing, position);
                }
            }
        }

        // A dinosaur seen roaring is heard roaring, and whatever else it does sounds like what it does it with.
        private static bool Roars(string model, int sign) => SaurianSpec.Parse(model) is SaurianSpec dinosaur
            && SkillMotions.Saurian(PageSign.Read(sign), SaurianForge.SpeciesOf(dinosaur).Form == SaurianForm.Theropod ? BodyPlan.Biped : BodyPlan.Quadruped) == Gestures.Roar;

        private void Enemies(ECSWorld world, GameSounds sounds)
        {
            foreach (Archetype archetype in world.QueryArchetypes(_enemies, BitMask.Empty))
            {
                foreach (EntityView entity in new ComponentAccessor(archetype))
                {
                    var enemy = entity.Get<EnemyComponent>();
                    if (enemy.State == enemy.Heard) continue;
                    EnemyState was = enemy.Heard;
                    enemy.Heard = enemy.State;
                    entity.Set(enemy);

                    Vector3 position = entity.Get<PositionComponent>().value;
                    string model = entity.Get<VisualComponent>().Model;
                    if (enemy.State == EnemyState.Dead)
                    {
                        SoundDefinition? dies = GameSounds.DinosaurVoice(model, GameSounds.DinosaurMoment.Dies);
                        if (model.StartsWith("skeleton") || dies == Voices.DeadDeath) Audio.Play(Voices.DeadDeath, position);
                        else
                        {
                            Audio.Play(dies ?? (model == "warlord" ? Voices.WarlordDeath : CharacterFactory.BeastOf(model) != null ? Voices.BeastDeath : Voices.OutlawDeath), position);
                            bool drowned = GameSession.Current?.World.WaterDepthAt(position.Xz) > 0.2f;
                            sounds.After(FallDelay, drowned ? Splashes.Body : Impacts.BodyFall, position);
                        }
                    }
                    else if (enemy.State == EnemyState.Chasing && was is EnemyState.Idle or EnemyState.Returning)
                    {
                        Audio.Play(GameSounds.DinosaurVoice(model, GameSounds.DinosaurMoment.Notices) ?? (model == "warlord" ? Voices.WarlordRoar : model.StartsWith("skeleton") || model == "revenant" ? Voices.DeadMoan : CharacterFactory.BeastOf(model) != null ? Voices.BeastSnarl : Voices.OutlawShout), position);
                    }
                }
            }
        }

        private void Heroes(ECSWorld world)
        {
            foreach (Archetype archetype in world.QueryArchetypes(_heroes, BitMask.Empty))
            {
                foreach (EntityView entity in new ComponentAccessor(archetype))
                {
                    var hero = entity.Get<HeroComponent>();
                    bool down = entity.Get<HealthComponent>().Current <= 0f;
                    if (down == hero.HeardDown) continue;
                    hero.HeardDown = down;
                    entity.Set(hero);

                    Vector3 position = entity.Get<PositionComponent>().value;
                    if (down) Audio.Play(Voices.HeroDeath, position);
                }
            }
        }

        private void Arrows(ECSWorld world, GameSession session)
        {
            foreach (Archetype archetype in world.QueryArchetypes(_arrows, BitMask.Empty))
            {
                foreach (EntityView entity in new ComponentAccessor(archetype))
                {
                    var arrow = entity.Get<BallisticComponent>();
                    Vector3 position = entity.Get<PositionComponent>().value;

                    if (arrow.Heard == ArrowHeard.Nothing)
                    {
                        arrow.Heard = ArrowHeard.Loosed;
                        entity.Set(arrow);
                        Audio.Play(Archery.Loose, arrow.Origin);
                    }

                    float distance = (position.Xz - session.PlayerPosition).Length;
                    if (arrow.Heard == ArrowHeard.Loosed && distance < WhizzDistance)
                    {
                        arrow.Heard = ArrowHeard.Passing;
                        entity.Set(arrow);
                        Audio.Play(Archery.Flight, position, 0f, 1f, arrow.Velocity);
                    }

                    // Stopped by the world, not by a hero, whose own hurt says what happened.
                    if (arrow.Heard != ArrowHeard.Landed && position.Y < session.World.Height.Sample(position.Xz) + 0.2f)
                    {
                        arrow.Heard = ArrowHeard.Landed;
                        entity.Set(arrow);
                        Audio.Play(session.World.WaterDepthAt(position.Xz) > 0.05f ? Archery.OnWater : Footsteps.GroundOf(session.World.SurfaceAt(position.Xz)) == Ground.Stone ? Archery.OnStone : Archery.InGround, position);
                    }
                    else if (arrow.Heard != ArrowHeard.Landed && BallisticComponent.Stopped(session.World, position))
                    {
                        arrow.Heard = ArrowHeard.Landed;
                        entity.Set(arrow);
                        Audio.Play(Archery.InWood, position);
                    }
                }
            }
        }
    }

    /// <summary>
    /// The sound of where the hero is. It listens from just above them, turned the way the camera faces, because the camera is thirty metres up and
    /// a hero's own footsteps shouldn't sound thirty metres off, while left and right have to mean left and right of the screen. From there it reads the land: the
    /// wind that's really blowing, how far the shore is, what's growing and how late it is, and sets the place around them to match.
    /// </summary>
    public class SoundscapeSystem : SystemBase
    {
        private const float PlaceInterval = 0.25f;
        private const float ListenerHeight = 3f;
        private const float FarthestShore = 220f;
        private const float FarthestBank = 46f;         // Metres from which small water can be heard lapping.
        private const float NearestBell = 170f;
        private const int ShoreRays = 12;
        private const float SpeedOfSound = 343f;
        private const float CloudHeight = 180f;         // Metres up that thunder seems to come from.
        private const float CrackReach = 280f;          // Metres within which the strike itself is heard, and not only what's left of it.
        private const float RollReach = 1300f;
        private const float FarthestThunder = 3000f;

        private static readonly float[] ShoreRanges = { 4f, 8f, 14f, 22f, 30f, 45f, 60f, 90f, 140f, 210f };

        /// <summary>What was last read off the land, for a scripted run's report.</summary>
        public static (float wind, float crest, float sea, float bank, float rain, float drips) Reading;

        private GameSession? _session;
        private const float DripHearing = 8f;   // Metres that something has to stand within for its drips to be heard.

        private SoundHandle _wind, _ocean, _lapping, _rain, _drips, _birds, _crickets, _frogs, _haunting;
        private readonly List<(float at, LightningStrike strike, float distance)> _thunder = new();
        private float _trees;
        private float _nearWater;
        private int _shoreWave;
        private float _waveCount;
        private float _nextPlace;
        private float _nextBell;
        private Vector2 _lastPosition;
        private readonly SoundRandom _random = new((ulong)Environment.TickCount64);

        public SoundscapeSystem()
            : base(new BitMask(typeof(PlayerTag)))
        {
        }

        protected override void UpdateEntity(EntityView entity, ECSWorld world, float deltaTime) { }

        protected override void UpdateFrame(ECSWorld world, float deltaTime)
        {
            GameSession session = GameSession.Current;
            if (session == null) return;
            if (session != _session) Begin(session);

            UpdateListener(session, deltaTime);
            Audio.Muffle(session.Paused ? 0.8f : 0f);
            UpdateThunder(world, session);

            _nextPlace -= deltaTime;
            if (_nextPlace > 0f) return;
            _nextPlace = PlaceInterval;

            ReadWind(world, session);
            ReadShore(world, session);
            ReadRain(world);
            ReadDrips(world, session);
            ReadLife(world, session);
            ReadPlace(world, session);
        }

        private void Begin(GameSession session)
        {
            _session = session;
            _wind.Stop(1f);
            _ocean.Stop(1f);
            _lapping.Stop(1f);
            _rain.Stop(1f);
            _drips.Stop(1f);
            _thunder.Clear();
            _birds.Stop(1f);
            _crickets.Stop(1f);
            _frogs.Stop(1f);
            _haunting.Stop(1f);

            // Seeded from the world, so a world's birds are the same birds whenever it's come back to.
            ulong seed = (ulong)(uint)session.World.Plan.Seed * 2654435761UL + 1UL;
            _wind = Audio.Start(Soundscape.Wind, seed: seed);
            _ocean = Audio.Start(Soundscape.Ocean, gain: 0f, seed: seed + 1UL);
            _lapping = Audio.Start(Soundscape.Lapping, gain: 0f, seed: seed + 6UL);
            _rain = Audio.Start(Soundscape.Rain, gain: 0f, seed: seed + 7UL);
            _drips = Audio.Start(Soundscape.Drips, gain: 0f, seed: seed + 8UL);
            _birds = Audio.Start(Life.Birds, seed: seed + 2UL);
            _crickets = Audio.Start(Life.Crickets, seed: seed + 3UL);
            _frogs = Audio.Start(Life.Frogs, seed: seed + 4UL);
            _haunting = Audio.Start(Life.Haunting, seed: seed + 5UL);
            _nextPlace = 0f;
        }

        private void UpdateListener(GameSession session, float deltaTime)
        {
            Vector2 at = session.PlayerPosition;
            var position = new Vector3(at.X, session.World.Height.Sample(at) + ListenerHeight, at.Y);

            // The rotation part of a view matrix holds the camera's own axes, as its columns.
            Matrix4 view = Engine.RenderEngine.context.viewMatrix;
            var right = new Vector3(view.M11, view.M21, view.M31);
            var forward = new Vector3(-view.M13, -view.M23, -view.M33);

            // Screen up is forward along the ground, however far the camera tilts.
            right.Y = 0f;
            forward.Y = 0f;
            if (right.LengthSquared < 1e-4f) return;
            right.Normalize();
            forward = forward.LengthSquared > 1e-4f ? forward.Normalized() : Vector3.Cross(Vector3.UnitY, right);

            Vector2 moved = deltaTime > 0f ? (at - _lastPosition) / deltaTime : Vector2.Zero;
            _lastPosition = at;
            if (moved.LengthSquared > 60f * 60f) moved = Vector2.Zero;
            Audio.SetListener(position, right, forward, Vector3.UnitY, new Vector3(moved.X, 0f, moved.Y));
        }

        private void ReadWind(ECSWorld world, GameSession session)
        {
            WorldData land = session.World;
            Vector2 at = session.PlayerPosition;
            var here = new Vector3(at.X, land.Height.Sample(at), at.Y);

            // The very wind that's bending the grass around the hero, so a gust that's seen is a gust that's heard.
            WindSample wind = Wind.Sample(world, here);
            _wind.Set(WindSound.Speed, wind.Velocity.Length);
            _wind.Set(WindSound.Gust, wind.Gust);
            _wind.Set(WindSound.Crest, wind.Crest);
            Reading.wind = wind.Velocity.Length;
            Reading.crest = wind.Crest;

            // A wood is heard as a whole from the middle of it, and from out in the open as something to one side.
            float trees = 0f;
            for (int i = 0; i < 8; i++)
            {
                float angle = i * (MathF.Tau / 8f);
                trees += land.Canopy.Sample(at.X + MathF.Cos(angle) * 18f, at.Y + MathF.Sin(angle) * 18f);
            }
            trees = Math.Clamp(trees / 8f * 1.4f, 0f, 1f);
            _trees = trees;

            BiomeDefinition biome = land.BiomeAt(at);
            _wind.Set(WindSound.Trees, trees);
            _wind.Set(WindSound.Grass, biome.GrassCover * (0.3f + biome.GrassHeight * 0.7f));

            float height = Math.Clamp(here.Y / 50f, 0f, 1f);
            _wind.Set(WindSound.Exposure, Math.Clamp((1f - trees) * (0.45f + 0.35f * height + 0.2f * biome.WindStrength), 0f, 1f));
        }

        // Looks out for water in every direction: the open sea as far as it can be heard, and anything at all, pond or lake or bay, close by. Inland seas have no surf to hear.
        private void ReadShore(ECSWorld world, GameSession session)
        {
            WorldData land = session.World;
            Vector2 at = session.PlayerPosition;
            bool surf = !land.Plan.Archetype.StillWater;
            float nearestSea = FarthestShore;
            float nearestBank = FarthestBank;
            Vector2 seaSide = Vector2.Zero;
            Vector2 bankSide = Vector2.Zero;

            for (int ray = 0; ray < ShoreRays; ray++)
            {
                float angle = ray * (MathF.Tau / ShoreRays);
                var direction = new Vector2(MathF.Cos(angle), MathF.Sin(angle));
                bool bankFound = false;
                foreach (float range in ShoreRanges)
                {
                    Vector2 point = at + direction * range;
                    bool sea = !land.InBounds(point) || land.Height.Sample(point) <= WorldPlan.SeaLevel;
                    if (!bankFound && (sea || land.WaterDepthAt(point) > 0.05f))
                    {
                        bankFound = true;
                        nearestBank = MathF.Min(nearestBank, range);
                        if (range < FarthestBank) bankSide += direction / range;
                    }
                    if (!sea) continue;
                    if (range < nearestSea) nearestSea = range;
                    seaSide += direction / range;
                    break;
                }
            }

            float near = surf ? 1f - nearestSea / FarthestShore : 0f;
            _ocean.SetGain(near <= 0f ? 0f : MathF.Min(near * 1.8f, 1f));
            _ocean.Set(OceanSound.Near, near);

            float bank = 1f - nearestBank / FarthestBank;
            _nearWater = bank;
            Reading.sea = near;
            Reading.bank = bank;
            _lapping.SetGain(bank <= 0f ? 0f : 1f);
            _lapping.Set(LappingSound.Near, bank);

            float wind = Wind.TryGet(world, out WindSingleton air) ? air.LiveSpeed : 3f;
            _ocean.Set(OceanSound.Swell, Math.Clamp(wind / 10f, 0.2f, 1f));
            _lapping.Set(LappingSound.Chop, Math.Clamp(wind / 9f, 0f, 1f));

            // The waves that are breaking on screen are the ones that are heard.
            if (Water.TryGet(world, out WaterSingleton water))
            {
                int wave = (int)MathF.Floor(water.ShorePhase);
                if (wave != _shoreWave)
                {
                    _shoreWave = wave;
                    _ocean.Set(OceanSound.Arrivals, ++_waveCount);
                }
            }

            // Which ear it's in, as the camera sees it.
            Matrix4 view = Engine.RenderEngine.context.viewMatrix;
            var right = new Vector2(view.M11, view.M31);
            if (right.LengthSquared < 1e-4f) return;
            right.Normalize();
            if (seaSide.LengthSquared > 1e-6f) _ocean.Set(OceanSound.Side, Math.Clamp(Vector2.Dot(seaSide.Normalized(), right), -1f, 1f));
            if (bankSide.LengthSquared > 1e-6f) _lapping.Set(LappingSound.Side, Math.Clamp(Vector2.Dot(bankSide.Normalized(), right), -1f, 1f));
        }

        // The rain that's falling on screen, on whatever's there for it to fall on.
        private void ReadRain(ECSWorld world)
        {
            WeatherSample here = Weather.Here(world);
            float rain = here.Rain;
            Reading.rain = rain;
            _rain.SetGain(rain > 0.003f ? 1f : 0f);
            _rain.Set(RainSound.Intensity, rain);
            _rain.Set(RainSound.Trees, _trees * 0.85f);
            _rain.Set(RainSound.Water, _nearWater * _nearWater * (1f - _trees * 0.5f));
        }

        // Water coming off whatever's around, by the rule it's drawn by (Weather/Drip.glsl): under trees and beside anything solid, and for as long as things stay wet after the rain has gone.
        private void ReadDrips(ECSWorld world, GameSession session)
        {
            WeatherSample here = Weather.Here(world);
            float soaked = Math.Clamp(here.Rain * 1.2f + here.Wet * here.Wet * 0.6f, 0f, 1f);
            float beside = session.World.Placement.OverlapsSolid(Footprint.Circle(session.PlayerPosition, DripHearing)) ? 0.7f : 0f;
            float drips = soaked * MathF.Max(_trees, beside);
            Reading.drips = drips;
            _drips.SetGain(drips > 0.003f ? 1f : 0f);
            _drips.Set(DripSound.Rate, drips);
            _drips.Set(DripSound.Water, here.Flood);
        }

        // Light gets here at once, and sound takes three seconds a kilometre. The further off, the less is left of it but the low roll.
        private void UpdateThunder(ECSWorld world, GameSession session)
        {
            if (Weather.TryGet(world, out WeatherSingleton weather))
            {
                foreach (LightningStrike strike in weather.Struck)
                {
                    float distance = (strike.Position - session.PlayerPosition).Length;
                    if (distance < FarthestThunder) _thunder.Add((Engine.Time + distance / SpeedOfSound, strike, distance));
                }
            }

            for (int i = _thunder.Count - 1; i >= 0; i--)
            {
                (float at, LightningStrike strike, float distance) = _thunder[i];
                if (at > Engine.Time) continue;
                _thunder.RemoveAt(i);

                var from = new Vector3(strike.Position.X, session.World.Height.Sample(strike.Position) + CloudHeight, strike.Position.Y);
                float loudness = AudioFormat.GainToDecibels(0.55f + 0.45f * strike.Power);
                Audio.Play(distance < CrackReach ? Cataclysm.LightningNear : distance < RollReach ? Cataclysm.ThunderNear : Cataclysm.ThunderFar, from, loudness);
            }
        }

        private void ReadLife(ECSWorld world, GameSession session)
        {
            WorldData land = session.World;
            Vector2 at = session.PlayerPosition;
            BiomeDefinition biome = land.BiomeAt(at);
            float night = SkySYstem.NightOf(world).Amount;

            // What's alive goes by the warmth and the wet of the place: there's nothing singing on a snowfield.
            float warmth = land.Temperature.Sample(at);
            float wet = land.Moisture.Sample(at);
            float canopy = land.Canopy.Sample(at.X, at.Y);
            bool barren = biome.Ground is SurfaceMaterial.Snow or SurfaceMaterial.Ice or SurfaceMaterial.Ash or SurfaceMaterial.Dunes or SurfaceMaterial.Sand;

            float dusk = DaySystem.TryNow(world, out DaySample hour) ? 0.5f * MathF.Max(hour.Golden, hour.Dawn)
                : land.Plan.Mood.Name.Contains("evening") || land.Plan.Mood.Name.Contains("dawn") ? 0.5f : 0f;
            // Birds shelter and crickets stop for rain, and it's what frogs have been waiting for.
            WeatherSample weather = Weather.Here(world);
            float downpour = Math.Clamp(weather.Fall * 2.2f, 0f, 1f);
            float birds = barren ? 0f : (0.25f + 0.75f * canopy) * (0.5f + 0.5f * wet) * (1f - night) * (1f - dusk * 0.4f) * (1f - downpour);
            float crickets = barren ? 0f : Math.Clamp(biome.GrassCover * warmth * (dusk + night * 1.2f + 0.12f), 0f, 1f) * (1f - downpour);

            float water = land.WaterLevelAt(at);
            float frogs = biome.Id is "marsh" or "mangrove" or "bogland" or "watermeadow" ? 0.85f : land.Height.Sample(at) - water < 2.5f && !barren && water > WorldPlan.SeaLevel ? 0.5f : 0f;

            _birds.Set(BirdsongSound.Activity, birds);
            _crickets.Set(CricketSound.Activity, crickets);
            _frogs.Set(FrogSound.Activity, Math.Clamp(frogs * (0.4f + 0.6f * night + dusk * 0.5f + weather.Rain * 0.5f), 0f, 1f));
            _haunting.Set(HauntingSound.Depth, night);

            // There's a bell somewhere near a churchyard, heard now and then, that nobody's seen ring.
            _nextBell -= PlaceInterval;
            if (night > 0.35f && _nextBell <= 0f)
            {
                _nextBell = _random.Range(35f, 80f);
                foreach (SiteState state in session.Sites)
                {
                    if (state.Wild || state.Site.Kind != SiteKind.Graveyard) continue;
                    if ((state.Site.Position - at).Length > NearestBell) continue;
                    Vector2 site = state.Site.Position;
                    Audio.Play(Life.ChurchBell, new Vector3(site.X, land.Height.Sample(site) + 12f, site.Y));
                    break;
                }
            }
        }

        private static void ReadPlace(ECSWorld world, GameSession session)
        {
            WorldData land = session.World;
            Vector2 at = session.PlayerPosition;
            BiomeDefinition biome = land.BiomeAt(at);
            float canopy = land.Canopy.Sample(at.X, at.Y);

            // Open land throws back very little; a wood scatters sound about; rock answers from a distance.
            ReverbSettings place = Open;
            if (biome.Ground is SurfaceMaterial.Rock or SurfaceMaterial.CrackedEarth || biome.Id is "highland" or "badlands" or "alpine" or "seacliff") place = Crags;
            if (biome.Ground == SurfaceMaterial.Snow) place = Snowfield;
            place = ReverbSettings.Lerp(place, Forest, Math.Clamp(canopy * 1.3f, 0f, 1f));

            foreach (SiteState state in session.Sites)
            {
                if (state.Wild) continue;
                float inside = 1f - (state.Site.Position - at).Length / (state.Site.Radius + 12f);
                if (inside <= 0f) continue;
                ReverbSettings built = state.Site.Kind switch
                {
                    SiteKind.Graveyard => Churchyard,
                    SiteKind.Keep or SiteKind.Ruins or SiteKind.Watchtower => Stonework,
                    SiteKind.Village or SiteKind.Hamlet or SiteKind.FishingHamlet or SiteKind.BanditCamp or SiteKind.LumberCamp => Yards,
                    _ => place,
                };
                place = ReverbSettings.Lerp(place, built, Math.Clamp(inside * 2f, 0f, 1f));
            }
            Audio.SetEnvironment(place);
        }

        private static readonly ReverbSettings Open = new() { DecayTime = 0.7f, DecayHigh = 0.22f, PreDelay = 0.018f, Wet = 0.32f, EchoTime = 0.34f, EchoBrightness = 2400f };
        private static readonly ReverbSettings Forest = new() { DecayTime = 1.25f, DecayHigh = 0.42f, PreDelay = 0.009f, Wet = 0.5f, Echo = 0.05f, EchoTime = 0.19f, EchoBrightness = 2000f };
        private static readonly ReverbSettings Crags = new() { DecayTime = 1.7f, DecayHigh = 0.6f, PreDelay = 0.03f, Wet = 0.46f, Echo = 0.2f, EchoTime = 0.42f, EchoBrightness = 2800f };
        private static readonly ReverbSettings Snowfield = new() { DecayTime = 0.38f, DecayHigh = 0.1f, PreDelay = 0.02f, Wet = 0.16f, EchoTime = 0.3f, EchoBrightness = 1600f };
        private static readonly ReverbSettings Yards = new() { DecayTime = 0.9f, DecayHigh = 0.34f, PreDelay = 0.011f, Wet = 0.42f, Echo = 0.13f, EchoTime = 0.11f, EchoBrightness = 3400f };
        private static readonly ReverbSettings Stonework = new() { DecayTime = 2f, DecayHigh = 0.8f, PreDelay = 0.016f, Wet = 0.6f, Echo = 0.16f, EchoTime = 0.21f, EchoBrightness = 3200f };
        private static readonly ReverbSettings Churchyard = new() { DecayTime = 2.6f, DecayHigh = 0.8f, PreDelay = 0.03f, Wet = 0.66f, Echo = 0.14f, EchoTime = 0.33f, EchoBrightness = 2200f };
    }
}
