using Dino_Engine.Animation;
using Dino_Engine.Modelling.Model;
using Dino_Engine.Modelling.Procedural.Character;
using Dino_Engine.Modelling.Procedural.Gear;
using Dino_Engine.Modelling.Procedural.Indoor;
using Dino_Engine.Modelling.Procedural.Saurian;
using OpenTK.Mathematics;

namespace Dino_AnimLab
{
    /// <summary>A body to try things on: the same kinds the game has, made the same way.</summary>
    internal sealed record Body(string Name, Rig Rig, MotionStyle Style, Carry Carry, bool Shield, Func<Mesh> Build, Carry? Second = null)
    {
        private static readonly Vector3 Tunic = new Vector3(0.2f, 0.3f, 0.5f);

        private static HumanoidLook Person(HeldItem held, bool shield = false, float bulk = 1f, bool robe = false) => new HumanoidLook
        {
            Tunic = Tunic,
            Legs = new Vector3(0.3f, 0.25f, 0.2f),
            Boots = new Vector3(0.25f, 0.16f, 0.1f),
            Held = held,
            Shield = shield,
            Bulk = bulk,
            LongGarment = robe,
        };

        // A sword and a dagger, dressed the way a hero is, so the second is where the game puts it.
        private static Mesh Twin()
        {
            var form = new GearForm(0.45f, 0.5f, 0.15f, 0.4f, 0.12f, 0.45f);
            return Outfit.Dress(Person(HeldItem.None), new[]
            {
                (new GearSpec(21, GearKind.Sword, 1f, 0f, 0f, form, 0, GearTheme.None), -1f),
                (new GearSpec(22, GearKind.Dagger, 0f, 1f, 0f, form, 0, GearTheme.None), 1f),
            });
        }

        private static Body Beast(string name, BeastLook look, MotionStyle style) => new Body(name, BeastGenerator.RigOf(look), style, Carry.Nothing, false, () => BeastGenerator.Generate(look));

        /// <summary>A dinosaur, by the name the game gives its model.</summary>
        public static Body Dino(string name, string key, MotionStyle style)
        {
            SaurianSpec spec = SaurianSpec.Parse(key) ?? throw new ArgumentException($"No dinosaur called {key}.");
            return new Body(name, SaurianForge.RigOf(spec), style, Carry.Nothing, false, () => SaurianForge.Make(spec));
        }

        /// <summary>How each kind of dinosaur moves, as the game has it.</summary>
        public static MotionStyle StyleOf(SaurianSpec spec) => SaurianForge.StyleOf(spec);

        public static readonly Body[] All =
        {
            new Body("bare", Rig.Humanoid, MotionStyle.Hero, Carry.Nothing, false, () => HumanoidGenerator.Generate(Person(HeldItem.None))),
            new Body("sword", Rig.Humanoid, MotionStyle.Hero, Carry.Sword, false, () => HumanoidGenerator.Generate(Person(HeldItem.Sword))),
            new Body("shield", Rig.Humanoid, MotionStyle.Outlaw, Carry.Sword, true, () => HumanoidGenerator.Generate(Person(HeldItem.Sword, true))),
            new Body("twin", Rig.Humanoid, MotionStyle.Hero, Carry.Sword, false, Twin, Carry.Dagger),
            new Body("spear", Rig.Humanoid, MotionStyle.Outlaw, Carry.Spear, false, () => HumanoidGenerator.Generate(Person(HeldItem.Spear))),
            new Body("bow", Rig.Humanoid, MotionStyle.Outlaw, Carry.Bow, false, () => HumanoidGenerator.Generate(Person(HeldItem.Bow))),
            new Body("robe", Rig.Humanoid, MotionStyle.Robed, Carry.Staff, false, () => HumanoidGenerator.Generate(Person(HeldItem.Spear, robe: true))),
            new Body("brute", Rig.Humanoid, MotionStyle.Brute, Carry.Sword, false, () => HumanoidGenerator.Generate(Person(HeldItem.Sword, bulk: 1.34f))),
            new Body("cutthroat", Rig.Humanoid, MotionStyle.Cutthroat, Carry.Dagger, false, () => HumanoidGenerator.Generate(Person(HeldItem.Sword, bulk: 0.92f))),
            new Body("skeleton", Rig.Humanoid, MotionStyle.Dead, Carry.Sword, true, SkeletonGenerator.GenerateSkeleton),
            Beast("wolf", new BeastLook(), MotionStyle.Hound),
            Beast("boar", new BeastLook { Height = 1.35f, Length = 2.9f, Girth = 0.82f, Skull = 0.46f, Muzzle = 0.56f, Tail = 0.4f, Ears = 0.16f, Tusks = 0.36f, Hump = 0.28f, Neck = 0.42f, Coat = new Vector3(0.42f, 0.24f, 0.13f), Belly = new Vector3(0.5f, 0.34f, 0.22f) }, MotionStyle.Boar),
            Beast("bear", new BeastLook { Height = 2.3f, Length = 3.9f, Girth = 1.25f, Skull = 0.62f, Muzzle = 0.44f, Tail = 0f, Ears = 0.17f, Hump = 0.34f, Neck = 0.8f, Coat = new Vector3(0.34f, 0.2f, 0.1f), Belly = new Vector3(0.42f, 0.27f, 0.15f) }, MotionStyle.Bear),
            Dino("raptor", "dino:raptor.0", MotionStyle.Raptor),
            Dino("skitter", "dino:skitter.0", MotionStyle.Scurrier),
            Dino("spitter", "dino:spitter.0", MotionStyle.Stalker),
            Dino("ravager", "dino:ravager.0", MotionStyle.Stalker),
            Dino("tyrant", "dino:tyrant.0", MotionStyle.Tyrant),
            Dino("hornback", "dino:hornback.0", MotionStyle.Horned),
        };

        /// <summary>A body by name, or any dinosaur by its model's name.</summary>
        public static Body Named(string name)
        {
            Body? found = All.FirstOrDefault(body => body.Name == name);
            if (found != null) return found;
            if (SaurianSpec.Parse(name) is SaurianSpec spec) return Dino(name, name, StyleOf(spec));
            return All[0];
        }

        public CharacterAnimator NewAnimator(int seed = 7)
        {
            var animator = new CharacterAnimator(Rig, Style, seed);
            animator.Hold(Carry, Shield, Second);
            return animator;
        }
    }

    /// <summary>What a body is made to do, as a place and a heading over time and whatever it's told along the way.</summary>
    internal sealed class Script
    {
        public float Length = 2f;
        public float Warmup = 1f;
        public Func<float, (Vector3 position, float yaw)> Path = _ => (Vector3.Zero, 0f);
        public Func<float, ActionInput>? Action;
        public float Alert;
        public bool Dazed;
        public float DiesAt = -1f;
        public Vector3 Push;
        public IGround Ground = new FlatGround();
        public string Note = "";

        private static float Speed(float t, float speed) => speed * t;

        public static Script Along(float speed) => new Script { Path = t => (new Vector3(0f, 0f, Speed(t, speed)), 0f), Length = 1.4f };

        /// <summary>A gesture repeated with a gap, standing or going along.</summary>
        public static Script Doing(Gesture gesture, float impact, float length, float speed = 0f, float gap = 0.3f)
        {
            float cycle = length + gap;
            return new Script
            {
                Path = t => (new Vector3(0f, 0f, speed * t), 0f),
                Length = cycle,
                Warmup = cycle * 2f,
                Alert = 1f,
                Action = t =>
                {
                    int serial = (int)MathF.Floor(t / cycle);
                    float into = t - serial * cycle;
                    if (into >= length) return default;
                    return new ActionInput { Gesture = gesture, Elapsed = into, Impact = impact, Length = length, Serial = serial, Aim = 0f };
                },
            };
        }

        public static Script? Named(string name)
        {
            return name switch
            {
                "idle" => new Script { Length = 6f, Warmup = 0.5f },
                "guard" => new Script { Length = 3f, Warmup = 1f, Alert = 1f },
                "dazed" => new Script { Length = 1.6f, Warmup = 1f, Alert = 1f, Dazed = true },
                "walk" => Along(3.6f),
                "jog" => Along(6.8f),
                "run" => Along(10f),
                "sprint" => Along(16f),
                "stopstart" => new Script { Length = 3.4f, Warmup = 0.6f, Path = t => { float p = t % 3.4f; float z = p < 1.2f ? p * 10f : p < 2.4f ? 12f : 12f + (p - 2.4f) * 10f; return (new Vector3(0f, 0f, z + MathF.Floor(t / 3.4f) * 22f), 0f); } },
                "turn" => new Script { Length = 3f, Warmup = 0.6f, Path = t => (Vector3.Zero, MathF.PI * 0.5f * MathF.Floor(t / 0.8f)) },
                "circle" => new Script { Length = 3f, Warmup = 1f, Path = t => { float a = t * 1.3f; return (new Vector3(MathF.Sin(a), 0f, MathF.Cos(a)) * 7.7f, a + MathF.PI * 0.5f); } },
                "strafe" => new Script { Length = 1.4f, Path = t => (new Vector3(t * 7f, 0f, 0f), 0f) },
                "back" => new Script { Length = 1.4f, Path = t => (new Vector3(0f, 0f, -t * 6f), 0f) },
                "slope" => new Script { Length = 2.4f, Ground = new Slope(), Path = t => { float z = t * 6f; return (new Vector3(0f, new Slope().HeightAt(0f, z), z), 0f); } },
                "die" => new Script { Length = 1.6f, Warmup = 0.4f, DiesAt = 0.5f, Push = new Vector3(0f, 1f, 2f) },
                "shove" => new Script { Length = 1.8f, Warmup = 0.4f, DiesAt = 0.5f, Push = new Vector3(0f, 6f, 13f) },
                "launch" => new Script { Length = 3f, Warmup = 0.4f, DiesAt = 0.5f, Push = new Vector3(0f, 17f, 30f) },
                "rundie" => new Script { Length = 1.8f, Warmup = 0.8f, DiesAt = 0.9f, Push = new Vector3(0f, 2f, 0f), Path = t => (new Vector3(0f, 0f, MathF.Min(t, 0.9f) * 10f), 0f) },
                "forehand" => Doing(Gestures.Forehand, 0.15f, 0.6f),
                "enemycut" => Doing(Gestures.Forehand, 0.6f, 1.05f),
                "runcut" => Doing(Gestures.Forehand, 0.15f, 0.6f, 5.5f),
                "sweep" => Doing(Gestures.Sweep, 0.19f, 0.75f),
                "overhead" => Doing(Gestures.Overhead, 0.58f, 1.05f),
                "thrust" => Doing(Gestures.Thrust, 0.15f, 0.55f),
                "whirl" => Doing(Gestures.Whirl, 0.26f, 0.5f),
                "bash" => Doing(Gestures.Bash, 0.24f, 0.6f),
                "shoot" => Doing(Gestures.Shoot, 0.33f, 0.65f),
                "throw" => Doing(Gestures.Throw, 0.2f, 0.5f),
                "push" => Doing(Gestures.Push, 0.38f, 0.7f),
                "call" => Doing(Gestures.Call, 0.6f, 0.85f),
                "burst" => Doing(Gestures.Burst, 0.45f, 0.75f),
                "gather" => Doing(Gestures.Gather, 0.24f, 0.4f),
                "summon" => Doing(Gestures.Summon, 0.63f, 0.9f),
                "drink" => Doing(Gestures.Drink, 0.36f, 0.9f),
                "use" => Doing(Gestures.Use, 0.28f, 0.7f),
                "bite" => Doing(Gestures.Bite, 0.45f, 0.75f),
                "maul" => Doing(Gestures.Maul, 0.6f, 1f),
                "chomp" => Doing(Gestures.Chomp, 0.6f, 1.05f),
                "roar" => Doing(Gestures.Roar, 0.7f, 1.9f),
                "tailsweep" => Doing(Gestures.TailSweep, 0.72f, 1.25f),
                "stomp" => Doing(Gestures.Stomp, 0.9f, 1.4f),
                "pounce" => Doing(Gestures.Pounce, 0.5f, 0.95f, 6f),
                "spit" => Doing(Gestures.Spit, 0.6f, 1.05f),
                "gore" => Doing(Gestures.Gore, 0.7f, 1.2f),
                "trample" => Doing(Gestures.Trample, 0.9f, 1.4f),
                _ => null,
            };
        }
    }

    /// <summary>Rolling ground, for what has to be true on more than a floor.</summary>
    internal sealed class Slope : IGround
    {
        public float HeightAt(float x, float z) => MathF.Sin(z * 0.28f) * 1.8f + MathF.Sin(x * 0.2f + z * 0.12f) * 0.7f;
    }
}
