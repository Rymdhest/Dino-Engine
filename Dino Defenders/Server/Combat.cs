using Dino_Defenders.Gameplay;
using Dino_Defenders.Progression;
using Dino_Defenders.Skills;
using Dino_Defenders.Stats;
using Dino_Engine.ECS.Components;
using Dino_Engine.ECS.ECS_Architecture;
using Dino_Engine.Networking;
using Dino_Engine.World;
using OpenTK.Mathematics;

namespace Dino_Defenders.Server
{
    /// <summary>
    /// The rules of a fight. Everything in here works on <see cref="Actor"/>s and has no idea whether one is a hero, an enemy or somebody's skeleton: a
    /// skill is released the same way, a blow lands the same way, and anything that can happen to one can happen to any of them.
    ///
    /// What a blow sets off is never done inside the blow. It goes on a list and happens on the next pass. An explosion that kills something that explodes
    /// would otherwise go as deep as the crowd is large, and be working through a list that was being changed under it.
    /// </summary>
    public sealed class Combat
    {
        private const int MostRewind = 15;      // A quarter of a second: past this, what a player saw is too old to hold against anyone else.
        private const int MostPerStep = 256;    // Consequences dealt with in a tick. What's over waits for the next one.
        private const float GoldChance = 0.16f;
        private const float ItemChance = 0.025f;
        private const float EliteItemChance = 0.4f;
        private const float ChainFade = 0.85f;
        private const float ChainGrowth = 1.25f;
        private const float EchoDelay = 0.6f;
        private const float EchoStrength = 0.5f;
        private const float TrailSpacing = 3.2f;
        private const float CarrierInterval = 1f;
        private const float RebirthCooldown = 90f;
        private const float StoredBonus = 1.6f;
        private const float ChillReach = 25f;
        private const float GuardianReach = 20f;
        private const float GuardianShare = 0.7f;
        private const float ConductReach = 12f;
        private const float ReactionShare = 0.6f;
        private const float ReactionRadius = 5f;
        private const float FireHealShare = 0.25f;
        private const float DebtForgiven = 0.15f;
        private const float ReadBusy = 0.1f;

        private enum Later : byte
        {
            Release,
            Land,
            Blast,
            Conduct,
            Raise,
            Spread,
        }

        private struct Consequence
        {
            public Later Kind;
            public double At;
            public Actor Source;
            public SkillProfile? Skill;
            public Vector2 Origin;
            public Vector2 Facing;
            public float Reach;
            public float Strength;
            public DamageType Type;
            public float Amount;
            public float Radius;
            public Tags Tags;
            public Actor? Victim;
        }

        private readonly ServerSession _session;
        private readonly WorldRandom _luck = new WorldRandom(Environment.TickCount);
        private readonly List<Actor> _found = new();
        private readonly List<Actor> _chained = new();
        private readonly List<Consequence> _later = new();

        public readonly ActorGrid Grid;
        public readonly LandReader Land;

        public Combat(ServerSession session)
        {
            _session = session;
            Grid = new ActorGrid(session.World.Size);
            Land = new LandReader(session.World);
        }

        private GameServer Server => _session.Server;
        private ECSWorld World => Server.Ecs;
        private double Now => _session.Time;

        /// <summary>A skill going off, from wherever and in whatever direction. <paramref name="reach"/> is how far off it was aimed.</summary>
        public void Release(Actor user, SkillProfile skill, Vector2 origin, Vector2 facing, float reach, int viewLag = 0, float strength = 1f)
        {
            user.SinceUse = 0f;
            if (facing.LengthSquared < 1e-4f) facing = Vector2.UnitY;
            Vector2 target = origin + facing * MathF.Min(reach, skill.Range);

            switch (skill.Skill.Form)
            {
                case SkillForm.Strike:
                case SkillForm.Leap:
                    if (!skill.DealsDamage) break;
                    Strike(user, skill, origin, facing, skill.Reach, skill.Skill.Arc, viewLag, strength);
                    break;
                case SkillForm.Nova:
                    Strike(user, skill, origin, facing, skill.Radius, MathF.Tau, viewLag, strength);
                    Show(new SkillEvent { Position = Ground(origin), Skill = skill.Skill.Number, Phase = SkillPhase.Burst, Radius = skill.Radius }, origin);
                    break;
                case SkillForm.Bolt:
                    Fire(user, skill, origin, facing, strength);
                    break;
                case SkillForm.Burst:
                    Show(new SkillEvent { Position = Ground(target), Skill = skill.Skill.Number, Phase = SkillPhase.Warning, Radius = skill.Radius, Time = skill.Delay }, target);
                    Defer(new Consequence { Kind = Later.Land, At = Now + skill.Delay, Source = user, Skill = skill, Origin = target, Strength = strength });
                    break;
                case SkillForm.Zone:
                    Lay(user, skill, target);
                    break;
                case SkillForm.Chain:
                    Chain(user, skill, origin, target, strength);
                    break;
                case SkillForm.Beam:
                    Beam(user, skill, origin, facing, strength);
                    break;
                case SkillForm.Buff:
                    if (skill.Skill.Buff != null) AddBuff(user, skill.Skill.Buff, skill.Duration);
                    Show(new SkillEvent { Position = Ground(origin), Skill = skill.Skill.Number, Phase = SkillPhase.Burst, Radius = 3f }, origin);
                    break;
                case SkillForm.Summon:
                    _session.Summon(user, skill, origin);
                    break;
            }

            if ((skill.Tags & Tags.Spell) == 0 || (skill.Tags & Tags.Triggered) != 0) return;

            if (user.Sheet.Has(Rule.SpellEcho) && strength >= 1f)
            {
                Defer(new Consequence { Kind = Later.Release, At = Now + EchoDelay, Source = user, Skill = skill, Origin = origin, Facing = facing, Reach = reach, Strength = EchoStrength });
            }

            if (user.Sheet.Has(Rule.WaterTrail) && user.Sheet.Profile(SkillCatalog.Puddle) is SkillProfile puddle) Lay(user, puddle, target);
        }

        private void Defer(in Consequence consequence)
        {
            _later.Add(consequence);
        }

        /// <summary>Everything that was waiting for now.</summary>
        public void Step()
        {
            int done = 0;
            for (int i = 0; i < _later.Count && done < MostPerStep; i++)
            {
                Consequence consequence = _later[i];
                if (consequence.At > Now) continue;
                _later.RemoveAt(i--);
                done++;
                Carry(consequence);
            }
        }

        private void Carry(in Consequence c)
        {
            switch (c.Kind)
            {
                case Later.Release:
                    if (c.Skill != null) Release(c.Source, c.Skill, c.Origin, c.Facing, c.Reach, 0, c.Strength);
                    break;
                case Later.Land:
                    if (c.Skill != null) LandBurst(c.Source, c.Skill, c.Origin, c.Strength);
                    break;
                case Later.Blast:
                    Blast(c.Source, c.Origin, c.Radius, c.Type, c.Amount, c.Tags, c.Victim);
                    break;
                case Later.Conduct:
                    if (c.Victim != null) Conduct(c.Source, c.Victim, c.Amount);
                    break;
                case Later.Raise:
                    if (c.Victim != null) _session.RaiseFallen(c.Source, c.Victim);
                    break;
                case Later.Spread:
                    if (c.Victim != null) SpreadAilments(c.Source, c.Victim);
                    break;
            }
        }

        private Vector3 Ground(Vector2 at) => new Vector3(at.X, _session.World.Height.Sample(at), at.Y);

        private void Show<T>(in T value, Vector2 near) where T : struct => Server.Broadcast(value, near);

        // ---- Getting there.

        private void Strike(Actor user, SkillProfile skill, Vector2 origin, Vector2 facing, float reach, float arc, int viewLag, float strength)
        {
            _found.Clear();
            Grid.Foes(user.Team, origin, reach + 2f, _found);

            float cosHalfArc = MathF.Cos(arc * 0.5f);
            int rewind = user.IsHero ? Math.Clamp(viewLag, 0, MostRewind) : 0;
            foreach (Actor foe in _found)
            {
                // An enemy is where the attacker saw it, which is where it was a moment ago: a client draws others slightly in the past, and a blow
                // that connects on screen has to connect. This tick's positions aren't recorded until it ends, so with no rewind, where it is now is the answer.
                Vector2 position = rewind > 0 ? foe.PositionAgo(rewind, Server.Tick) : foe.Position;
                Vector2 offset = position - origin;
                float distance = offset.Length;
                if (distance > reach + foe.Radius) continue;
                if (arc < MathF.Tau && distance > 0.5f && Vector2.Dot(offset / distance, facing) < cosHalfArc) continue;
                Hit(user, skill, foe, strength, origin);
            }
        }

        private void Fire(Actor user, SkillProfile skill, Vector2 origin, Vector2 facing, float strength)
        {
            float height = user.Height + 3.2f;
            float start = -(skill.Projectiles - 1) * 0.5f * skill.Skill.Spread;
            for (int i = 0; i < skill.Projectiles; i++)
            {
                float angle = start + i * skill.Skill.Spread;
                float cos = MathF.Cos(angle);
                float sin = MathF.Sin(angle);
                var direction = new Vector2(facing.X * cos - facing.Y * sin, facing.X * sin + facing.Y * cos);
                Launch(user, skill, origin + direction * 1.5f, direction, height, strength, null);
            }
        }

        private void Launch(Actor user, SkillProfile skill, Vector2 from, Vector2 direction, float height, float strength, Projectile? parent)
        {
            float yaw = MathF.Atan2(direction.X, direction.Y);
            var origin = new Vector3(from.X, height, from.Y);
            var projectile = new Projectile
            {
                Source = user,
                Skill = skill,
                Life = skill.Range / MathF.Max(skill.Speed, 1f) + 0.25f,
                Pierce = skill.Pierce,
                Chains = skill.Chains,
                Strength = strength,
                Start = from,
                Returning = parent?.Returning ?? false,
            };
            if (parent != null) projectile.Spent.AddRange(parent.Spent);

            bool arrow = (skill.Tags & Tags.Attack) != 0;
            World.CreateEntity("Projectile",
                new NetIdentityComponent(Server.Ghosts.NextId(), importance: 2f),
                new PositionComponent(origin),
                new RotationComponent(Quaternion.FromAxisAngle(Vector3.UnitY, yaw)),
                new VisualComponent(arrow ? "arrow" : $"bolt:{skill.Skill.Look}", VisualKind.Prop),
                new BallisticComponent { Origin = origin, Velocity = new Vector3(direction.X, 0f, direction.Y) * skill.Speed, StartTick = Server.Tick, FromHeroes = user.Team == Team.Heroes },
                new ProjectileComponent { State = projectile });
        }

        /// <summary>A projectile has reached something, or the end of its strength. Returns whether it flies on.</summary>
        public bool Touch(Projectile projectile, Vector3 at)
        {
            _found.Clear();
            Grid.Foes(projectile.Source.Team, at.Xz, 1.3f, _found);

            bool flying = true;
            foreach (Actor foe in _found)
            {
                if (projectile.Spent.Contains(foe)) continue;
                projectile.Spent.Add(foe);
                Hit(projectile.Source, projectile.Skill, foe, projectile.Strength * Flight(projectile, at.Xz), at.Xz);
                if (projectile.Skill.Radius > 0f) Burst(projectile, at.Xz);

                if (projectile.Chains > 0 && Retarget(projectile, foe, at))
                {
                    flying = false;
                    break;
                }

                if (projectile.Pierce > 0)
                {
                    projectile.Pierce--;
                    continue;
                }
                flying = false;
                break;
            }
            return flying;
        }

        // How a shot gets stronger as it goes, for those who've arranged that.
        private static float Flight(Projectile projectile, Vector2 at)
        {
            if (!projectile.Source.Sheet.Has(Rule.LongShot)) return 1f;
            float metres = (at - projectile.Start).Length;
            return metres < 10f ? 0.6f : 1f + MathF.Min(metres * 0.015f, 0.9f);
        }

        private bool Retarget(Projectile projectile, Actor from, Vector3 at)
        {
            _chained.Clear();
            Grid.Foes(projectile.Source.Team, at.Xz, 18f, _chained);

            Actor? next = null;
            float best = float.PositiveInfinity;
            foreach (Actor foe in _chained)
            {
                if (projectile.Spent.Contains(foe)) continue;
                float distance = (foe.Position - at.Xz).LengthSquared;
                if (distance >= best) continue;
                best = distance;
                next = foe;
            }
            if (next == null) return false;

            projectile.Chains--;
            Vector2 direction = (next.Position - at.Xz).Normalized();
            float growth = projectile.Source.Sheet.Has(Rule.ChainsGrow) ? ChainGrowth : ChainFade;
            Launch(projectile.Source, projectile.Skill, at.Xz, direction, at.Y, projectile.Strength * growth, projectile);
            return true;
        }

        public void Burst(Projectile projectile, Vector2 at)
        {
            LandBurst(projectile.Source, projectile.Skill, at, projectile.Strength, projectile.Spent);
        }

        private void LandBurst(Actor user, SkillProfile skill, Vector2 at, float strength, List<Actor>? already = null)
        {
            Show(new SkillEvent { Position = Ground(at), Skill = skill.Skill.Number, Phase = SkillPhase.Burst, Radius = skill.Radius }, at);

            _chained.Clear();
            Grid.Foes(user.Team, at, skill.Radius, _chained);
            foreach (Actor foe in _chained)
            {
                if (already != null && already.Contains(foe)) continue;
                Hit(user, skill, foe, strength, at);
            }
        }

        /// <summary>A projectile that's done coming back to whoever threw it, if they're someone who does that.</summary>
        public void Returns(Projectile projectile, Vector3 at)
        {
            Actor source = projectile.Source;
            if (!source.Sheet.Has(Rule.ProjectilesReturn) || projectile.Returning || source.Dead) return;

            Vector2 toward = source.Position - at.Xz;
            if (toward.LengthSquared < 4f) return;
            projectile.Returning = true;
            projectile.Spent.Clear();
            Launch(source, projectile.Skill, at.Xz, toward.Normalized(), at.Y, projectile.Strength, projectile);
        }

        private void Chain(Actor user, SkillProfile skill, Vector2 origin, Vector2 target, float strength)
        {
            _chained.Clear();
            Vector2 from = origin;
            float power = strength;
            float growth = user.Sheet.Has(Rule.ChainsGrow) ? ChainGrowth : ChainFade;

            // The first leap goes for what's nearest the aim, and each after that for whatever is nearest the last.
            Vector2 seek = target;
            float reach = skill.Radius;
            for (int leap = 0; leap <= skill.Chains; leap++)
            {
                _found.Clear();
                Grid.Foes(user.Team, seek, reach, _found);

                Actor? next = null;
                float best = float.PositiveInfinity;
                foreach (Actor foe in _found)
                {
                    if (_chained.Contains(foe)) continue;
                    float distance = (foe.Position - seek).LengthSquared;
                    if (distance >= best) continue;
                    best = distance;
                    next = foe;
                }

                if (next == null)
                {
                    if (leap == 0) Show(new LinkEvent { From = Ground(from) + Vector3.UnitY * 3f, To = Ground(seek) + Vector3.UnitY * 1f, Look = skill.Skill.Look }, from);
                    return;
                }

                _chained.Add(next);
                Show(new LinkEvent { From = Ground(from) + Vector3.UnitY * 3f, To = Ground(next.Position) + Vector3.UnitY * 3f, Look = skill.Skill.Look }, from);
                Hit(user, skill, next, power, from);
                power *= growth;
                from = next.Position;
                seek = next.Position;
            }
        }

        private void Beam(Actor user, SkillProfile skill, Vector2 origin, Vector2 facing, float strength)
        {
            float length = skill.Reach;
            Show(new LinkEvent { From = Ground(origin) + Vector3.UnitY * 3f, To = Ground(origin + facing * length) + Vector3.UnitY * 3f, Look = skill.Skill.Look, Wide = true }, origin);

            _found.Clear();
            Vector2 middle = origin + facing * (length * 0.5f);
            Grid.Foes(user.Team, middle, length * 0.5f + skill.Radius, _found);
            foreach (Actor foe in _found)
            {
                Vector2 offset = foe.Position - origin;
                float along = Vector2.Dot(offset, facing);
                if (along < 0f || along > length) continue;
                if ((offset - facing * along).Length > skill.Radius + foe.Radius) continue;
                Hit(user, skill, foe, strength, origin);
            }
        }

        private void Lay(Actor user, SkillProfile skill, Vector2 at)
        {
            float life = MathF.Max(skill.Duration, 0.5f);
            var zone = new ZoneState { Source = user, Skill = skill, Left = life, NextPulse = 0.1f, At = at };
            if (skill.Skill == SkillCatalog.Tidepool || skill.Skill == SkillCatalog.Puddle) Land.AddPool(at, skill.Radius, (float)Now + life);

            World.CreateEntity("Zone",
                new NetIdentityComponent(Server.Ghosts.NextId(), importance: 1.5f),
                new PositionComponent(Ground(at)),
                new ZoneComponent { Skill = skill.Skill.Number, Radius = skill.Radius, Life = life },
                new ZoneStateComponent { State = zone });
        }

        /// <summary>A zone's moment comes round: everything standing in it is struck.</summary>
        public void Pulse(ZoneState zone)
        {
            if (!zone.Skill.DealsDamage) return;

            _chained.Clear();
            Grid.Foes(zone.Source.Team, zone.At, zone.Skill.Radius, _chained);
            foreach (Actor foe in _chained) Hit(zone.Source, zone.Skill, foe, 1f, zone.At);
        }

        // ---- Landing.

        /// <summary>One skill reaching one target. This is the whole of what happens when anything hits anything.</summary>
        public void Hit(Actor source, SkillProfile skill, Actor target, float strength, Vector2 from)
        {
            if (target.Dead) return;

            // Nothing stands idle while someone shoots at it, and it doesn't keep that to itself.
            target.Alarm = EnemyBrainSystem.StruckAlarm;
            Vector3 point = new Vector3(target.Position.X, target.Height + 3.4f, target.Position.Y);

            if (target.Player is ServerPlayer hero && hero.Sim.Safe > 0f)
            {
                Dodged(target, point);
                return;
            }

            Condition foe = target.State;
            if ((skill.FoeConditions & Condition.Land) != 0) foe |= Land.At(target.Position);

            bool attack = (skill.Tags & Tags.Attack) != 0;
            if (attack && _luck.Float() < Hits.EvadeChance(target.Sheet.Evasion, skill.Accuracy))
            {
                Show(new DamageEvent { Position = point, Note = HitNote.Evaded, ToHero = target.IsHero }, target.Position);
                return;
            }

            float block = attack ? target.Sheet.BlockAttacks : (skill.Tags & Tags.Spell) != 0 ? target.Sheet.BlockSpells : 0f;
            if (block > 0f && _luck.Float() * 100f < block)
            {
                Blocked(source, skill, target, foe, point);
                return;
            }

            bool sureCrit = source.Critical;
            source.Critical = false;

            HitPacket hit = default;
            float power = strength * Advantage(source, target);
            if (source.Static > 0f && source.Sheet.Has(Rule.StaticCharge))
            {
                power *= 1f + source.Static / 100f;
                source.Static = 0f;
            }
            Hits.Roll(skill, foe, _luck, ref hit, power, sureCrit);

            // Fed into a burn, it doesn't arrive as a blow at all.
            float burnFuel = 0f;
            if (source.Sheet.Has(Rule.FireFeedsBurn))
            {
                burnFuel = hit.Damage[(int)DamageType.Fire];
                hit.Damage[(int)DamageType.Fire] = 0f;
            }

            float bonus = React(source, skill, target, ref hit);
            DamageTable raw = hit.Damage;
            if (bonus != 1f)
            {
                for (int i = 0; i < DamageKinds.Count; i++) hit.Damage[i] *= bonus;
            }

            float total = Hits.Mitigate(target.Sheet, ref hit, source.State, target.Afflictions.ResistanceLost, target.TakenIncrease);

            if (source.Sheet.Has(Rule.FreezeStores) && target.Afflictions.FreezeLeft > 0f)
            {
                target.Afflictions.Stored += total;
                target.Afflictions.StoredBy = source;
                total = 0f;
                hit.Total = 0f;
            }

            if (target.Sheet.Has(Rule.FireHeals))
            {
                float fire = hit.Dealt[(int)DamageType.Fire] * FireHealShare;
                hit.Total -= fire;
                hit.Dealt[(int)DamageType.Fire] -= fire;
                Hits.Heal(target.Sheet, ref target.Vitals, fire * target.Sheet.RecoveryScale);
            }

            if (Shoulder(target, ref hit)) total = hit.Total;
            float lost = hit.Total > 0f ? Hits.Pay(target.Sheet, ref target.Vitals, hit) : 0f;
            target.SinceHit = 0f;

            Show(new DamageEvent { Position = point, Amount = total, Critical = hit.Critical, ToHero = target.IsHero, Body = BodyOf(target), Type = StrongestOf(hit) }, target.Position);

            if (hit.Critical) source.SinceCrit = 0f;
            Recover(source, skill, total);
            Afflict(source, skill, target, raw, burnFuel, hit.Total);

            if (hit.Critical && skill.Execute > 0f && target.Vitals.Health < target.Sheet.MaxHealth * skill.Execute / 100f) target.Vitals.Health = 0f;

            if (target.Sheet.Reflect > 0f && (skill.Tags & Tags.Melee) != 0 && total > 0f) Harm(target, source, total * target.Sheet.Reflect / 100f, DamageType.Physical);

            Wake(target);
            SetOff(source, TriggerEvent.Hit, source.Position);
            if (hit.Critical) SetOff(source, TriggerEvent.Critical, source.Position);
            SetOff(target, TriggerEvent.Struck, target.Position);

            if (target.Sheet.Has(Rule.Mimic) && target.Player is ServerPlayer mimic && !skill.Skill.Hidden) mimic.Mimic(skill.Skill);

            if (target.Vitals.Health <= 0f) Die(target, source, skill, from);
        }

        // What standing where you are is worth, for those who've arranged that.
        private static float Advantage(Actor source, Actor target)
        {
            if (!source.Sheet.Has(Rule.HighGround)) return 1f;
            float above = source.Height - target.Height;
            return above >= 0f ? 1f + MathF.Min(above * 0.04f, 0.6f) : MathF.Max(1f + above * 0.02f, 0.4f);
        }

        // The rest of a party takes some of it, if one of them has sworn to.
        private bool Shoulder(Actor target, ref HitPacket hit)
        {
            if (target.Team != Team.Heroes || !target.IsHero || hit.Total <= 0f) return false;

            foreach (ServerPlayer hero in _session.Heroes)
            {
                Actor guardian = hero.Actor;
                if (guardian == target || guardian.Dead || !guardian.Sheet.Has(Rule.Guardian)) continue;
                if ((guardian.Position - target.Position).LengthSquared > GuardianReach * GuardianReach) continue;

                float taken = hit.Total * (1f - GuardianShare);
                for (int i = 0; i < DamageKinds.Count; i++) hit.Dealt[i] *= GuardianShare;
                hit.Total *= GuardianShare;
                Hits.Pay(guardian.Sheet, ref guardian.Vitals, taken, 0f);
                return true;
            }
            return false;
        }

        private void Dodged(Actor target, Vector3 point)
        {
            target.SinceDodge = 0f;
            if (target.Sheet.Has(Rule.DodgeCrit)) target.Critical = true;
            Show(new DamageEvent { Position = point, Note = HitNote.Dodged, ToHero = true }, target.Position);
            SetOff(target, TriggerEvent.Dodge, target.Position);
        }

        private void Blocked(Actor source, SkillProfile skill, Actor target, Condition foe, Vector3 point)
        {
            target.SinceBlock = 0f;
            Show(new DamageEvent { Position = point, Note = HitNote.Blocked, ToHero = target.IsHero }, target.Position);
            SetOff(target, TriggerEvent.Block, target.Position);

            if (!target.Sheet.Has(Rule.BlockReflects)) return;
            HitPacket turned = default;
            Hits.Roll(skill, foe, _luck, ref turned);
            Harm(target, source, turned.Damage.Total, DamageType.Physical);
        }

        /// <summary>Damage that isn't a blow: thorns, a blow turned back, a burst of something. It's resisted, but it can't be dodged, and it sets nothing off.</summary>
        public void Harm(Actor source, Actor target, float amount, DamageType type, Tags tags = Tags.None)
        {
            if (target.Dead || amount <= 0f) return;

            HitPacket hit = default;
            hit.Damage[(int)type] = amount;
            hit.Tags = tags;
            float total = Hits.Mitigate(target.Sheet, ref hit, source.State, target.Afflictions.ResistanceLost, target.TakenIncrease);
            Hits.Pay(target.Sheet, ref target.Vitals, hit);
            target.SinceHit = 0f;

            Vector3 point = new Vector3(target.Position.X, target.Height + 3.4f, target.Position.Y);
            Show(new DamageEvent { Position = point, Amount = total, ToHero = target.IsHero, Body = BodyOf(target), Type = type }, target.Position);

            Wake(target);
            if (target.Vitals.Health <= 0f) Die(target, source, null, source.Position);
        }

        private void Blast(Actor source, Vector2 at, float radius, DamageType type, float amount, Tags tags, Actor? spared)
        {
            _chained.Clear();
            Grid.Foes(source.Team, at, radius, _chained);
            foreach (Actor foe in _chained)
            {
                if (foe != spared) Harm(source, foe, amount, type, tags);
            }
        }

        private static DamageType StrongestOf(in HitPacket hit)
        {
            int best = 0;
            for (int i = 1; i < DamageKinds.Count; i++)
            {
                if (hit.Dealt[i] > hit.Dealt[best]) best = i;
            }
            return (DamageType)best;
        }

        private static HitBody BodyOf(Actor actor)
        {
            if (actor.Body.StartsWith("skeleton")) return HitBody.Bone;
            if (actor.Body is "wolf" or "direwolf" or "boar" or "bear") return HitBody.Beast;
            return actor.Body is "warlord" or "revenant" ? HitBody.Armour : HitBody.Flesh;
        }

        private void Recover(Actor source, SkillProfile skill, float dealt)
        {
            if (dealt <= 0f) return;
            ActorSheet sheet = source.Sheet;

            float health = dealt * skill.HealthLeech + skill.HealthOnHit;
            if (health > 0f) Hits.Heal(sheet, ref source.Vitals, health * sheet.RecoveryScale);

            float mana = dealt * skill.ManaLeech + skill.ManaOnHit;
            if (mana > 0f) source.Vitals.Mana = MathF.Min(source.Vitals.Mana + mana * sheet.RecoveryScale, sheet.MaxMana);
        }

        // A hit that wakes someone, even from afar.
        private void Wake(Actor target)
        {
            if (target.IsHero || !target.Entity.IsValid()) return;
            if (!World.TryGetEntityLocation(target.Entity, out _, out _)) return;

            EntityView view = World.GetEntityView(target.Entity);
            var enemy = view.Get<EnemyComponent>();
            if (enemy.State is not (EnemyState.Idle or EnemyState.Returning)) return;
            enemy.State = EnemyState.Chasing;
            view.Set(enemy);
        }

        // ---- Reactions. Two things meeting on someone, which neither does alone.

        // What's already on someone meets what's just arrived. Returns what the blow itself is worth now.
        private float React(Actor source, SkillProfile skill, Actor target, ref HitPacket hit)
        {
            if ((skill.Tags & Tags.Reaction) != 0) return 1f;

            Afflictions a = target.Afflictions;
            float fire = hit.Damage[(int)DamageType.Fire];
            float cold = hit.Damage[(int)DamageType.Cold];
            float lightning = hit.Damage[(int)DamageType.Lightning];
            float physical = hit.Damage[(int)DamageType.Physical];
            float scale = skill.ReactionScale;
            float radius = ReactionRadius * skill.ReactionArea;

            // Shatter: what's frozen is brittle, and a real blow breaks it.
            if (a.FreezeLeft > 0f && physical > 0f)
            {
                Thaw(target);
                Announce(target, Reaction.Shatter, radius);
                return 1f + scale;
            }

            // Scald: fire on the chilled turns all that cold to steam.
            if (fire > 0f && (a.ChillLeft > 0f || a.FreezeLeft > 0f))
            {
                a.ChillLeft = 0f;
                Thaw(target);
                Spread(source, target, radius, DamageType.Fire, fire * ReactionShare * scale);
                Announce(target, Reaction.Scald, radius);
            }

            // Crackle: cold on the burning cracks what's been heated.
            else if (cold > 0f && a.BurnLeft > 0f)
            {
                a.BurnLeft = 0f;
                Spread(source, target, radius, DamageType.Physical, cold * ReactionShare * scale);
                Announce(target, Reaction.Crackle, radius);
            }

            // Flashfire: fire finds venom and takes all of it at once.
            if (fire > 0f && a.Venom is { Count: > 0 })
            {
                float left = 0f;
                foreach (VenomStack stack in a.Venom) left += stack.PerSecond * stack.Left;
                a.Venom.Clear();
                Spread(source, target, radius, DamageType.Fire, left * scale);
                Announce(target, Reaction.Flashfire, radius);
            }

            // Conduction: lightning runs through whatever's standing in the same water.
            if (lightning > 0f && Land.InWater(target.Position))
            {
                Defer(new Consequence { Kind = Later.Conduct, At = Now, Source = source, Victim = target, Amount = lightning * ReactionShare * scale });
                Announce(target, Reaction.Conduction, ConductReach);
            }
            return 1f;
        }

        private void Spread(Actor source, Actor around, float radius, DamageType type, float amount)
        {
            Defer(new Consequence { Kind = Later.Blast, At = Now, Source = source, Origin = around.Position, Radius = radius, Type = type, Amount = amount, Tags = Tags.Reaction, Victim = null });
        }

        private void Conduct(Actor source, Actor target, float amount)
        {
            _chained.Clear();
            Grid.Foes(source.Team, target.Position, ConductReach, _chained);
            foreach (Actor foe in _chained)
            {
                if (foe == target || !Land.InWater(foe.Position)) continue;
                Show(new LinkEvent { From = Ground(target.Position) + Vector3.UnitY, To = Ground(foe.Position) + Vector3.UnitY, Look = DamageType.Lightning }, target.Position);
                Harm(source, foe, amount, DamageType.Lightning, Tags.Reaction);
            }
        }

        private void Announce(Actor target, Reaction reaction, float radius)
        {
            Show(new ReactionEvent { Position = Ground(target.Position) + Vector3.UnitY * 3f, Kind = reaction, Radius = radius }, target.Position);
        }

        private void Thaw(Actor target)
        {
            Afflictions a = target.Afflictions;
            a.FreezeLeft = 0f;
            if (a.Stored <= 0f || a.StoredBy == null) return;

            float stored = a.Stored * StoredBonus;
            Actor source = a.StoredBy;
            a.Stored = 0f;
            a.StoredBy = null;
            Defer(new Consequence { Kind = Later.Blast, At = Now, Source = source, Origin = target.Position, Radius = 0.5f, Type = DamageType.Cold, Amount = stored, Tags = Tags.Reaction });
        }

        // ---- What stays behind.

        private void Afflict(Actor source, SkillProfile skill, Actor target, in DamageTable raw, float burnFuel, float dealt)
        {
            float physical = raw[(int)DamageType.Physical];
            float fire = raw[(int)DamageType.Fire];
            float cold = raw[(int)DamageType.Cold];
            float lightning = raw[(int)DamageType.Lightning];
            float magic = raw[(int)DamageType.Magic];
            float poison = raw[(int)DamageType.Poison];
            float health = target.Sheet.MaxHealth;
            ActorSheet sheet = target.Sheet;
            Afflictions a = target.Afflictions;

            bool Lands(Ailment ailment, bool sure = false)
            {
                float chance = skill.Ailments[(int)ailment].Chance;
                if (!sure && (chance <= 0f || _luck.Float() * 100f >= chance)) return false;
                return _luck.Float() * 100f >= sheet.AilmentAvoid[(int)ailment];
            }

            float Length(Ailment ailment, float seconds) => seconds * skill.Ailments[(int)ailment].Duration * sheet.AilmentTaken[(int)ailment];
            float Strength(Ailment ailment) => skill.Ailments[(int)ailment].Effect * skill.LingerScale[(int)ailment];

            float flame = fire + burnFuel;
            if (flame > 0f && !sheet.Has(Rule.ImmuneToBurn) && Lands(Ailment.Burn, burnFuel > 0f))
            {
                float seconds = Length(Ailment.Burn, AilmentRules.BurnSeconds);
                float perSecond = flame * AilmentRules.BurnShare / AilmentRules.BurnSeconds * Strength(Ailment.Burn);
                if (seconds > 0f && (perSecond >= a.BurnPerSecond || a.BurnLeft <= 0f))
                {
                    a.BurnPerSecond = perSecond;
                    a.BurnLeft = seconds;
                    a.BurnSource = source;
                }
            }

            if (physical > 0f && Lands(Ailment.Bleed))
            {
                float seconds = Length(Ailment.Bleed, AilmentRules.BleedSeconds);
                float perSecond = physical * AilmentRules.BleedShare / AilmentRules.BleedSeconds * Strength(Ailment.Bleed);
                if (seconds > 0f && (perSecond >= a.BleedPerSecond || a.BleedLeft <= 0f))
                {
                    a.BleedPerSecond = perSecond;
                    a.BleedLeft = seconds;
                    a.BleedSource = source;
                }
            }

            if (physical + poison > 0f && !sheet.Has(Rule.Carrier) && Lands(Ailment.Venom))
            {
                float seconds = Length(Ailment.Venom, AilmentRules.VenomSeconds);
                float perSecond = (physical + poison) * AilmentRules.VenomShare / AilmentRules.VenomSeconds * Strength(Ailment.Venom);
                int most = source.Sheet.Has(Rule.EndlessVenom) ? AilmentRules.MostEndlessVenom : AilmentRules.MostVenom;
                a.Venom ??= new List<VenomStack>();
                if (a.Venom.Count >= most) a.Venom.RemoveAt(0);
                if (seconds > 0f) a.Venom.Add(new VenomStack { PerSecond = perSecond, Left = seconds, Source = source });
            }

            if (cold > 0f && !sheet.Has(Rule.CannotBeChilled))
            {
                float effect = Math.Clamp(AilmentRules.LeastChill + cold / health * 2f, AilmentRules.LeastChill, AilmentRules.MostChill) * skill.Ailments[(int)Ailment.Chill].Effect;
                effect = MathF.Min(effect, 0.75f);
                if (effect >= a.ChillEffect || a.ChillLeft <= 0f)
                {
                    a.ChillEffect = effect;
                    a.ChillLeft = Length(Ailment.Chill, AilmentRules.ChillSeconds);
                    a.ChillSource = source;
                }
            }

            if (cold > 0f && !sheet.Has(Rule.CannotBeFrozen) && Lands(Ailment.Freeze, cold / health >= AilmentRules.FreezeThreshold))
            {
                a.FreezeLeft = MathF.Max(a.FreezeLeft, Length(Ailment.Freeze, AilmentRules.FreezeSeconds));
            }

            if (lightning > 0f && Lands(Ailment.Shock))
            {
                float effect = Math.Clamp(AilmentRules.LeastShock + lightning / health * 2f, AilmentRules.LeastShock, AilmentRules.MostShock) * skill.Ailments[(int)Ailment.Shock].Effect;
                if (effect >= a.ShockEffect || a.ShockLeft <= 0f)
                {
                    a.ShockEffect = effect;
                    a.ShockLeft = Length(Ailment.Shock, AilmentRules.ShockSeconds);
                }
            }

            if (magic > 0f && Lands(Ailment.Hex))
            {
                a.HexStacks = Math.Min(a.HexStacks + 1, AilmentRules.MostHex);
                a.HexLeft = Length(Ailment.Hex, AilmentRules.HexSeconds);
            }

            float bearable = health * sheet.StunThreshold / 100f;
            if (!sheet.Has(Rule.CannotBeStunned) && physical > 0f && Lands(Ailment.Stun, dealt / bearable >= AilmentRules.StunThreshold))
            {
                a.StunLeft = MathF.Max(a.StunLeft, Length(Ailment.Stun, AilmentRules.StunSeconds));
            }
        }

        /// <summary>A moment of everything that's working on someone.</summary>
        public void Linger(Actor actor, float seconds)
        {
            Afflictions a = actor.Afflictions;
            ActorSheet sheet = actor.Sheet;
            float resistanceLost = a.ResistanceLost;
            float taken = 1f + actor.TakenIncrease;
            Actor? killer = null;
            float dealt = 0f;

            float Through(DamageType type, float perSecond)
            {
                float resistance = sheet.Resistance[(int)type] - resistanceLost;
                return perSecond * seconds * (1f - MathF.Max(resistance, Hits.LeastResistance) / 100f) * sheet.Taken[(int)type] * taken;
            }

            if (a.BurnLeft > 0f)
            {
                a.BurnLeft -= seconds;
                dealt += Through(DamageType.Fire, a.BurnPerSecond);
                killer = a.BurnSource;
                if (a.BurnLeft <= 0f) a.BurnPerSecond = 0f;
            }

            if (a.BleedLeft > 0f)
            {
                a.BleedLeft -= seconds;
                dealt += Through(DamageType.Physical, a.BleedPerSecond * (actor.Moving ? 1.5f : 1f));
                killer ??= a.BleedSource;
                if (a.BleedLeft <= 0f) a.BleedPerSecond = 0f;
            }

            if (a.Venom is { Count: > 0 })
            {
                for (int i = a.Venom.Count - 1; i >= 0; i--)
                {
                    VenomStack stack = a.Venom[i];
                    dealt += Through(DamageType.Poison, stack.PerSecond);
                    killer ??= stack.Source;
                    stack.Left -= seconds;
                    if (stack.Left <= 0f) a.Venom.RemoveAt(i);
                    else a.Venom[i] = stack;
                }
            }

            if (a.ChillLeft > 0f)
            {
                bool held = a.ChillSource is { Dead: false } && a.ChillSource.Sheet.Has(Rule.ChillNeverFades)
                    && (a.ChillSource.Position - actor.Position).LengthSquared < ChillReach * ChillReach;
                if (!held) a.ChillLeft -= seconds;
            }

            if (a.FreezeLeft > 0f)
            {
                a.FreezeLeft -= seconds;
                if (a.FreezeLeft <= 0f) Thaw(actor);
            }
            if (a.ShockLeft > 0f) a.ShockLeft -= seconds;
            if (a.HexLeft > 0f)
            {
                a.HexLeft -= seconds;
                if (a.HexLeft <= 0f) a.HexStacks = 0;
            }
            if (a.StunLeft > 0f) a.StunLeft -= seconds;

            if (dealt > 0f)
            {
                Hits.Pay(sheet, ref actor.Vitals, dealt, 0f);
                if (actor.Vitals.Health <= 0f) Die(actor, killer ?? actor, null, actor.Position);
            }

            Hits.Recover(sheet, ref actor.Vitals, seconds, actor.SinceHit);
            if (actor.Vitals.Health <= 0f && !actor.Dead) Die(actor, actor, null, actor.Position);
        }

        /// <summary>Someone's time is up, through nobody's doing.</summary>
        public void Dismiss(Actor actor)
        {
            if (actor.Dead) return;
            actor.Dead = true;
            actor.Vitals.Health = 0f;
            _session.Fell(actor, actor.Position);
        }

        // Whoever dies, this is what it does for them, and for whoever did it.
        private void Die(Actor actor, Actor killer, SkillProfile? skill, Vector2 from)
        {
            if (actor.Dead) return;

            if (actor.Sheet.Has(Rule.Rebirth) && actor.RebirthIn <= 0f)
            {
                actor.RebirthIn = RebirthCooldown;
                actor.Vitals.Health = actor.Sheet.MaxHealth * 0.5f;
                actor.Vitals.Debt = 0f;
                Show(new SkillEvent { Position = Ground(actor.Position), Skill = SkillCatalog.Immolate.Number, Phase = SkillPhase.Burst, Radius = 12f }, actor.Position);
                Defer(new Consequence { Kind = Later.Blast, At = Now, Source = actor, Origin = actor.Position, Radius = 12f, Type = DamageType.Fire, Amount = actor.Sheet.MaxHealth * 0.5f, Tags = Tags.Reaction });
                return;
            }

            actor.Dead = true;
            actor.Vitals.Health = 0f;

            Actor credit = killer.Owner ?? killer;
            if (credit != actor && credit.IsFoeOf(actor)) Credit(credit, actor, skill);

            if (actor.Player != null) return;
            _session.Fell(actor, from);
        }

        private void Credit(Actor killer, Actor victim, SkillProfile? skill)
        {
            killer.SinceKill = 0f;
            killer.KillHeat += 1f;
            killer.Vitals.Debt *= 1f - DebtForgiven;

            ActorSheet sheet = killer.Sheet;
            if (skill != null)
            {
                if (skill.HealthOnKill > 0f) Hits.Heal(sheet, ref killer.Vitals, skill.HealthOnKill * sheet.RecoveryScale);
                if (skill.ManaOnKill > 0f) killer.Vitals.Mana = MathF.Min(killer.Vitals.Mana + skill.ManaOnKill * sheet.RecoveryScale, sheet.MaxMana);
                if (skill.BarrierOnKill > 0f) killer.Vitals.Barrier = MathF.Min(killer.Vitals.Barrier + skill.BarrierOnKill, sheet.MaxBarrier);
            }

            if (sheet.ExplodeOnKill > 0f)
            {
                float amount = victim.Sheet.MaxHealth * sheet.ExplodeOnKill / 100f;
                Show(new SkillEvent { Position = Ground(victim.Position), Skill = SkillCatalog.Immolate.Number, Phase = SkillPhase.Burst, Radius = 6f }, victim.Position);
                Defer(new Consequence { Kind = Later.Blast, At = Now, Source = killer, Origin = victim.Position, Radius = 6f, Type = DamageType.Fire, Amount = amount, Tags = Tags.Reaction, Victim = victim });
            }

            if (sheet.Has(Rule.AilmentsLeap) && (victim.Afflictions.Current & Condition.Afflicted) != 0)
            {
                Defer(new Consequence { Kind = Later.Spread, At = Now, Source = killer, Victim = victim });
            }

            if (sheet.RaiseFallen > 0f && victim.Team == Team.Enemies && (victim.Nature & Condition.Boss) == 0 && _luck.Float() * 100f < sheet.RaiseFallen)
            {
                Defer(new Consequence { Kind = Later.Raise, At = Now + 0.8f, Source = killer, Victim = victim });
            }

            SetOff(killer, TriggerEvent.Kill, victim.Position);
        }

        private void SpreadAilments(Actor source, Actor victim)
        {
            Afflictions from = victim.Afflictions;
            _chained.Clear();
            Grid.Foes(source.Team, victim.Position, 10f, _chained);
            foreach (Actor foe in _chained)
            {
                Afflictions to = foe.Afflictions;
                if (from.BurnLeft > 0f && from.BurnPerSecond > to.BurnPerSecond)
                {
                    to.BurnPerSecond = from.BurnPerSecond;
                    to.BurnLeft = from.BurnLeft;
                    to.BurnSource = source;
                }
                if (from.BleedLeft > 0f && from.BleedPerSecond > to.BleedPerSecond)
                {
                    to.BleedPerSecond = from.BleedPerSecond;
                    to.BleedLeft = from.BleedLeft;
                    to.BleedSource = source;
                }
                if (from.Venom is { Count: > 0 })
                {
                    to.Venom ??= new List<VenomStack>();
                    foreach (VenomStack stack in from.Venom)
                    {
                        if (to.Venom.Count < AilmentRules.MostVenom) to.Venom.Add(stack);
                    }
                }
                if (from.ChillLeft > 0f && from.ChillEffect > to.ChillEffect)
                {
                    to.ChillEffect = from.ChillEffect;
                    to.ChillLeft = from.ChillLeft;
                }
                if (from.ShockLeft > 0f && from.ShockEffect > to.ShockEffect)
                {
                    to.ShockEffect = from.ShockEffect;
                    to.ShockLeft = from.ShockLeft;
                }
            }
        }

        // ---- What goes off by itself.

        /// <summary>Something has happened to someone; anything of theirs waiting for that goes off.</summary>
        public void SetOff(Actor actor, TriggerEvent on, Vector2 at)
        {
            Mod[] triggers = actor.Loadout.Triggers;
            for (int i = 0; i < triggers.Length; i++)
            {
                ref readonly Mod trigger = ref triggers[i];
                if (trigger.On != on || actor.TriggerReady[i] > 0f) continue;
                if (trigger.Value < 100f && _luck.Float() * 100f >= trigger.Value) continue;

                SkillDefinition skill = SkillCatalog.Get(trigger.Skill);
                if (actor.Sheet.Profile(skill) is not SkillProfile profile) continue;

                actor.TriggerReady[i] = MathF.Max(profile.Cooldown, ReadBusy);
                Defer(new Consequence { Kind = Later.Release, At = Now, Source = actor, Skill = profile, Origin = at, Facing = Vector2.UnitY, Reach = 0f, Strength = 1f });
            }
        }

        /// <summary>What someone does just by being there, for those with a keystone that says so. Called every tick for those it could matter to.</summary>
        public void Presence(Actor actor, float seconds)
        {
            ActorSheet sheet = actor.Sheet;
            if (sheet.Has(Rule.BurningTrail) && actor.Moving)
            {
                actor.TrailStep += sheet.MoveSpeed * seconds;
                if (actor.TrailStep >= TrailSpacing && sheet.Profile(SkillCatalog.CinderTrail) is SkillProfile cinders)
                {
                    actor.TrailStep = 0f;
                    Lay(actor, cinders, actor.Position);
                }
            }

            if (sheet.Has(Rule.Carrier))
            {
                actor.CarrierClock += seconds;
                if (actor.CarrierClock >= CarrierInterval && sheet.Profile(SkillCatalog.VenomCloud) is SkillProfile cloud)
                {
                    actor.CarrierClock = 0f;
                    Defer(new Consequence { Kind = Later.Release, At = Now, Source = actor, Skill = cloud, Origin = actor.Position, Facing = Vector2.UnitY, Strength = 1f });
                }
            }
        }

        public void AddBuff(Actor actor, BuffDefinition buff, float duration)
        {
            float length = duration > 0f ? duration : buff.Duration;
            for (int i = 0; i < actor.Buffs.Count; i++)
            {
                ActiveBuff active = actor.Buffs[i];
                if (active.Buff != buff) continue;

                // Refreshing it is free. Only another stack changes what anything adds up to.
                bool grows = active.Stacks < buff.MostStacks;
                active.Left = MathF.Max(active.Left, length);
                if (grows) active.Stacks++;
                actor.Buffs[i] = active;
                actor.LoadoutChanged |= grows;
                return;
            }

            actor.Buffs.Add(new ActiveBuff { Buff = buff, Stacks = 1, Left = length });
            actor.LoadoutChanged = true;
        }

        // ---- What it leaves behind.

        public void DropLoot(in EnemyComponent enemy, Vector3 position, Actor actor)
        {
            // A world holds a thousand of these. What's worth having comes from those that took some beating.
            float rank = enemy.Rank switch { EnemyRank.Boss => 10f, EnemyRank.Elite => 3f, _ => 1f };
            float goldFind = 1f + BestOfParty(s => s.GoldFind) / 100f;
            float itemFind = BestOfParty(s => s.ItemRarity) / 100f;

            if (_luck.Chance(enemy.Rank == EnemyRank.Normal ? GoldChance : 1f))
            {
                int gold = (int)(_luck.Range(3f, 9f) * enemy.Level * rank * goldFind);
                SpawnPickup(position, null, gold);
            }

            // What it was wearing is yours now. That's the whole point of having seen it used.
            if (actor.Gear != null)
            {
                foreach (Item worn in actor.Gear) SpawnPickup(position, worn, 0);
            }

            float itemChance = enemy.Rank switch { EnemyRank.Boss => 1.4f, EnemyRank.Elite => EliteItemChance, _ => ItemChance };
            int drops = enemy.Rank == EnemyRank.Boss ? 3 : 1;
            for (int i = 0; i < drops; i++)
            {
                if (!_luck.Chance(itemChance)) continue;
                Rarity? least = enemy.Rank == EnemyRank.Boss ? Rarity.Rare : enemy.Rank == EnemyRank.Elite ? Rarity.Magic : null;
                Item item = ItemGenerator.Roll(enemy.Level + 1, _luck.Fork(_luck.Int(1 << 30)), itemFind, least);
                SpawnPickup(position, item, 0);
            }
        }

        private float BestOfParty(Func<ActorSheet, float> stat)
        {
            float best = 0f;
            foreach (ServerPlayer hero in _session.Heroes) best = MathF.Max(best, stat(hero.Actor.Sheet));
            return best;
        }

        public void OpenChest(ServerPlayer opener, Vector3 position, int level)
        {
            Server.Broadcast(new SoundEvent { Position = position, Cue = SoundCue.Chest }, position.Xz);
            ActorSheet sheet = opener.Actor.Sheet;
            int gold = (int)(_luck.Range(14f, 30f) * level * (1f + sheet.GoldFind / 100f));
            float rarity = sheet.ItemRarity / 100f + 0.8f;
            SpawnPickup(position, null, gold);
            SpawnPickup(position, ItemGenerator.Roll(level + 1, _luck.Fork(_luck.Int(1 << 30)), rarity, Rarity.Magic), 0);
            if (_luck.Chance(0.4f))
            {
                SpawnPickup(position, ItemGenerator.Roll(level + 1, _luck.Fork(_luck.Int(1 << 30)), rarity), 0);
            }
        }

        private void SpawnPickup(Vector3 position, Item? item, int gold)
        {
            Vector2 scatter = new Vector2(_luck.Range(-1f, 1f), _luck.Range(-1f, 1f)).Normalized() * _luck.Range(1f, 3f);
            Vector3 rest = position + new Vector3(scatter.X, 0.6f, scatter.Y);
            World.CreateEntity("Pickup",
                new NetIdentityComponent(Server.Ghosts.NextId()),
                new PositionComponent(rest),
                new RotationComponent(new Vector3(0f)),
                new ScaleComponent(new Vector3(item != null ? 0.32f : 0.2f)),
                new VisualComponent(item != null ? "item" : "gold", VisualKind.Prop),
                new PickupComponent { Item = item, Gold = gold, Rest = rest });
        }
    }
}
