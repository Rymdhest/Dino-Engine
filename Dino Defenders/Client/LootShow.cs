using Dino_Defenders.Gameplay;
using Dino_Defenders.Progression;
using Dino_Defenders.Server;
using Dino_Engine.ECS.Components;
using Dino_Engine.ECS.ECS_Architecture;
using Dino_Engine.Networking;
using Dino_Engine.Sound.Content;
using Dino_Engine.Util;
using Dino_Engine.World;
using OpenTK.Mathematics;

namespace Dino_Defenders.Client
{
    /// <summary>What a pillar of light is, for a rarity that has one.</summary>
    public readonly record struct LootBeam(Colour Colour, float Height, float Width);

    /// <summary>
    /// A drop, as it's seen. It's all worked out from when the find left what it came out of, so nothing here can be missed, and the better a find the longer the moment goes on:
    /// it goes higher, takes longer to come down, and lands harder. A unique's whole flight is the rise of its sound, which breaks as it lands.
    /// </summary>
    public static class LootShow
    {
        private static readonly Colour GoldGlow = new Colour(1f, 0.74f, 0.3f, 7f);

        private const float GoldScale = 0.26f;
        private const float GoldHeight = 0.35f;
        private const float GoldArc = 3f;
        private const float BeamRise = 0.3f;        // Seconds for a beam to reach its height.
        private const float BeamFlash = 3.2f;       // How quickly the burst of a beam starting dies down.
        private const float Hidden = 0.001f;

        private static readonly float[] Flights = { 0.42f, 0.48f, 0.6f, Treasure.UniqueStrike };
        private static readonly float[] Arcs = { 3.2f, 4f, 6f, 13f };
        private static readonly float[] Sizes = { 0.9f, 1f, 1.1f, 1.35f };
        private static readonly float[] Glows = { 0f, 3f, 8f, 22f };
        private static readonly float[] MoteEvery = { 0f, 0f, 0.3f, 0.07f };

        private static readonly BitMask Pickups = new BitMask(typeof(PickupComponent), typeof(PositionComponent), typeof(RotationComponent), typeof(ScaleComponent));

        public static float FlightOf(Rarity rarity) => Flights[(int)rarity];

        /// <summary>What a rarity looks like as something glowing: deep, as particles go straight into a scene lit by a sun of about 13, and anything pale comes out white.</summary>
        public static Colour GlowOf(Rarity rarity) => rarity switch
        {
            Rarity.Unique => new Colour(1f, 0.3f, 0.02f, 1.8f),
            Rarity.Rare => new Colour(1f, 0.66f, 0.05f, 1.5f),
            Rarity.Magic => new Colour(0.12f, 0.3f, 1f, 1.3f),
            _ => new Colour(0.7f, 0.66f, 0.6f, 0.8f),
        };

        public static LootBeam? BeamOf(Rarity rarity) => rarity switch
        {
            Rarity.Unique => new LootBeam(new Colour(1f, 0.22f, 0.005f, 1.25f), 26f, 3f),
            Rarity.Rare => new LootBeam(new Colour(1f, 0.62f, 0.01f, 0.85f), 13f, 1.7f),
            _ => null,
        };

        public static float GlowStrength(Rarity rarity) => Glows[(int)rarity];

        public const float LightRadius = 9f;

        public static void Update(ECSWorld world, GameClient client, GameSession session, ClientEffects? effects, Wardrobe wardrobe, float deltaTime)
        {
            double now = client.Render.RenderTick;
            foreach (Archetype archetype in world.QueryArchetypes(Pickups, BitMask.Empty))
            {
                foreach (EntityView entity in new ComponentAccessor(archetype))
                {
                    var pickup = entity.Get<PickupComponent>();
                    bool gold = pickup.Find == 0;
                    if (!gold) Wear(entity, ref pickup, wardrobe);
                    float age = (float)((now - pickup.Born) * NetTime.TickSecondsPrecise);
                    if (age < 0f)
                    {
                        entity.Set(new ScaleComponent(new Vector3(Hidden)));
                        continue;
                    }

                    float flight = gold ? Loot.GoldFlight : FlightOf(pickup.Laid ? Rarity.Normal : pickup.Rarity);
                    if (!pickup.Launched)
                    {
                        pickup.Launched = true;

                        // Each find is announced once to a player, however many times it goes out of sight and comes back. One that's found lying there was never seen to fall, and nothing a hero put down is news.
                        bool news = (gold || session.Heard.Add(pickup.Find)) && !pickup.Laid;
                        if (age < flight && news) effects?.LootLaunched(pickup.Rarity, gold, entity.Get<PositionComponent>().value);
                        else if (age >= flight)
                        {
                            pickup.Landed = true;
                            pickup.LandedFor = 10f;
                            if (news && !gold) effects?.LootDiscovered(pickup.Rarity, pickup.Rest);
                        }
                    }

                    if (age < flight) Fly(entity, ref pickup, age / flight, gold, effects, deltaTime);
                    else
                    {
                        if (!pickup.Landed)
                        {
                            pickup.Landed = true;
                            effects?.LootLanded(pickup.Find, pickup.Laid ? Rarity.Normal : pickup.Rarity, pickup.Slot, gold, pickup.Rest);
                        }
                        pickup.LandedFor += deltaTime;
                        if (gold) Settle(entity, session);
                        else Rest(entity, ref pickup, session, client, effects, deltaTime);
                    }
                    entity.Set(pickup);
                }
            }
        }

        private static void Fly(in EntityView entity, ref PickupComponent pickup, float share, bool gold, ClientEffects? effects, float deltaTime)
        {
            float arc = gold ? GoldArc : pickup.Laid ? Arcs[0] : Arcs[(int)pickup.Rarity];
            Vector3 at = Vector3.Lerp(pickup.From, pickup.Rest, share) + Vector3.UnitY * (MathF.Sin(share * MathF.PI) * arc);
            entity.Set(new PositionComponent(at));
            entity.Set(new ScaleComponent(new Vector3(gold ? GoldScale : pickup.Dressed ? Sizes[(int)pickup.Rarity] * pickup.Fit : Hidden)));

            // A whole number of turns, so that it comes down flat.
            if (!gold) entity.Set(new RotationComponent(Quaternion.FromAxisAngle(new Vector3(0.8f, 0f, 0.6f), share * MathF.Tau * 2f) * Lying(pickup.Find)));

            if (gold || pickup.Laid || pickup.Rarity < Rarity.Rare) return;
            pickup.NextMote -= deltaTime;
            if (pickup.NextMote > 0f) return;
            pickup.NextMote = 0.03f;
            effects?.LootMote(pickup.Rarity, at, 0.2f);
        }

        private static void Settle(in EntityView entity, GameSession session)
        {
            // It may be on its way to someone, and that's the server's to say.
            Vector3 at = entity.Get<PositionComponent>().value;
            at.Y = session.World.Height.Sample(at.Xz) + GoldHeight;
            entity.Set(new PositionComponent(at));
            entity.Set(new ScaleComponent(new Vector3(GoldScale)));
            if (entity.Has<ColorComponent>()) entity.Set(new ColorComponent(GoldGlow));
        }

        private static void Rest(in EntityView entity, ref PickupComponent pickup, GameSession session, GameClient client, ClientEffects? effects, float deltaTime)
        {
            bool taken = session.Taken.TryGetValue(pickup.Find, out long until);
            if (taken && client.Clock() > until)
            {
                // Nobody has said it went, so it didn't.
                session.Taken.Remove(pickup.Find);
                taken = false;
            }

            float landed = pickup.LandedFor;
            float bounce = 0.8f * MathF.Abs(MathF.Sin(landed * 11f)) * MathF.Exp(-landed * 7f);
            entity.Set(new PositionComponent(pickup.Rest + Vector3.UnitY * (0.04f + bounce)));
            entity.Set(new RotationComponent(Lying(pickup.Find)));
            entity.Set(new ScaleComponent(new Vector3(taken || !pickup.Dressed ? Hidden : Sizes[(int)pickup.Rarity] * pickup.Fit)));

            float burst = MathF.Exp(-landed * BeamFlash);
            if (entity.Has<ColorComponent>())
            {
                Colour light = GlowOf(pickup.Rarity);
                float strength = taken ? 0f : GlowStrength(pickup.Rarity) * (1f + 3f * burst);
                entity.Set(new ColorComponent(new Colour(light.Red, light.Green, light.Blue, strength)));
            }

            if (BeamOf(pickup.Rarity) is not LootBeam beam) return;
            float rise = taken ? 0f : Math.Clamp(landed / BeamRise, 0f, 1f);
            float bright = beam.Colour.Intensity * (1f + 2.5f * burst);
            entity.Set(new BeamComponent
            {
                Height = beam.Height * rise * (2f - rise),
                Width = beam.Width * (1f + burst),
                Colour = new Colour(beam.Colour.Red, beam.Colour.Green, beam.Colour.Blue, bright),
            });

            if (taken) return;
            pickup.NextMote -= deltaTime;
            if (pickup.NextMote > 0f) return;
            pickup.NextMote = MoteEvery[(int)pickup.Rarity];
            effects?.LootMote(pickup.Rarity, pickup.Rest, beam.Height * 0.5f);
        }

        // What's lying there is the same thing someone would be wearing. It has to keep being asked for, or the wardrobe puts it away.
        private static void Wear(in EntityView entity, ref PickupComponent pickup, Wardrobe wardrobe)
        {
            if (wardrobe.Lying(pickup.Seed, pickup.Traits) is not Dino_Engine.Modelling.Model.glModel model || pickup.Dressed) return;
            pickup.Dressed = true;
            pickup.Fit = Dino_Engine.Modelling.Procedural.Gear.GearDisplay.GroundScale(ItemLook.SpecOf(pickup.Seed, pickup.Traits).Kind);
            entity.Set(new ModelComponent(model));
        }

        // Each lies its own way, which it keeps however often it's seen.
        private static Quaternion Lying(int find) => Quaternion.FromAxisAngle(Vector3.UnitY, find * 2.399963f);
    }

    /// <summary>
    /// Walks the hero to what the player clicked, and takes it or uses it. The hero gets there the way they'd get anywhere, by what's asked of them through <see cref="HeroIntent"/>,
    /// so nothing about it can disagree with the server. Anything else the player does means they've changed their mind.
    /// </summary>
    public sealed class LootFetch
    {
        private const float WaypointReach = 1f;
        private const float LookSpacing = 0.4f;             // The strips of ground nobody can cross can be a metre wide, and a wall seen across at an angle not much more.
        private const long PatienceMilliseconds = 600;      // How long going nowhere is put up with before finding another way.
        private const int MostReplans = 3;
        private const float CountsAsMoving = 0.5f;
        private const float UseMargin = 1f;                 // A press goes to whatever's nearest where the server has the hero, which isn't quite here. This far inside, it can only be the one that was clicked.

        private static readonly BitMask Places = new BitMask(typeof(InteractableComponent), typeof(PositionComponent));

        private readonly WorldPathfinder _paths = new(exact: true);
        private int _find;
        private (int Site, int Spawn)? _use;
        private Vector2 _at;
        private List<Vector2>? _path;
        private int _step;
        private int _replans;
        private Vector2 _lastAt;
        private long _lastMoved;

        public bool Active => _find != 0 || _use != null;

        public void Begin(int find, Vector2 at)
        {
            Set(find, null, at);
        }

        /// <summary>Walks to something that can be used, by which spawn of which site it is, as what it's called on this client can change if it goes out of sight on the way.</summary>
        public void BeginUse(int site, int spawn, Vector2 at)
        {
            Set(0, (site, spawn), at);
        }

        private void Set(int find, (int Site, int Spawn)? use, Vector2 at)
        {
            _find = find;
            _use = use;
            _at = at;
            _path = null;
            _step = 0;
            _replans = 0;
            _lastMoved = 0;
        }

        public void Cancel()
        {
            _find = 0;
            _use = null;
        }

        public void Steer(GameSession session, ref HeroIntent intent)
        {
            if (!Active) return;

            GameClient client = session.Client;
            if ((_find != 0 && !session.Ground.ContainsKey(_find)) || intent.Move != Vector2.Zero || intent.Slots != 0 || intent.Dash)
            {
                Cancel();
                return;
            }

            Vector2 here = session.PlayerPosition;
            if (_use != null)
            {
                if (!Look(client.Ecs, here, out bool ready) || ready)
                {
                    intent.Interact = ready;
                    Cancel();
                    return;
                }
            }
            else if ((_at - here).LengthSquared <= Loot.Reach * Loot.Reach)
            {
                client.PickUp(_find);
                Cancel();
                return;
            }

            long now = client.Clock();
            if (_lastMoved == 0 || (here - _lastAt).LengthSquared > CountsAsMoving * CountsAsMoving)
            {
                _lastAt = here;
                _lastMoved = now;
            }
            else if (now - _lastMoved > PatienceMilliseconds)
            {
                if (++_replans > MostReplans)
                {
                    Cancel();
                    return;
                }
                _lastMoved = now;
                _path = null;
            }

            intent.Move = NextStop(session.World, here) - here;
        }

        // Whether it's still there, and whether a press from here would be sure to reach it and not whatever else is near.
        private bool Look(ECSWorld world, Vector2 here, out bool ready)
        {
            bool found = false;
            float distance = 0f;
            float reach = 0f;
            float others = float.PositiveInfinity;
            foreach (Archetype archetype in world.QueryArchetypes(Places, BitMask.Empty))
            {
                foreach (EntityView entity in new ComponentAccessor(archetype))
                {
                    var place = entity.Get<InteractableComponent>();
                    Vector2 at = entity.Get<PositionComponent>().value.Xz;
                    float away = (at - here).Length;
                    if (place.SiteIndex == _use!.Value.Site && place.SpawnIndex == _use.Value.Spawn)
                    {
                        found = true;
                        _at = at;
                        distance = away;
                        reach = place.Radius;
                    }
                    else if (away <= place.Radius + UseMargin) others = MathF.Min(others, away);
                }
            }

            ready = found && distance <= reach - UseMargin && distance + UseMargin < others;
            return found;
        }

        // The furthest point along the way that can be walked to in a straight line, so a hero heads for where they're going and doesn't pick their way from point to point.
        private Vector2 NextStop(WorldData world, Vector2 here)
        {
            if (WorldPathfinder.HasClearLine(world, here, _at, LookSpacing)) return _at;

            // A search gives up on a long enough walk and says how far it got, so running out of path only means looking again from there.
            if (_path == null || _step >= _path.Count)
            {
                _path = _paths.Find(world, here, _at);
                _step = 0;
                if (_path == null || _path.Count == 0) return _at;
            }

            while (_step + 1 < _path.Count && WorldPathfinder.HasClearLine(world, here, _path[_step + 1], LookSpacing)) _step++;
            if ((_path[_step] - here).Length < WaypointReach) _step++;
            return _step < _path.Count ? _path[_step] : _at;
        }
    }
}
