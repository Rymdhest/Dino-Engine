using Dino_Engine.Particles;
using Dino_Engine.Particles.Content;
using OpenTK.Mathematics;

namespace Dino_FxLab
{
    /// <summary>
    /// Runs every particle effect with no window and no GPU, as the CPU sees them: what's born and when it dies. See PARTICLES.md.
    ///   check  [--seconds S]   Every recipe makes sense, nothing lives for ever, no effect costs more than its budget, and a fight at its worst fits in the pool.
    ///                          Exit code 1 if not.
    /// It proves nothing about how anything looks. For that, look at it in the game (--fx).
    /// </summary>
    internal static class Program
    {
        private const float Step = 1f / 60f;
        private const float Gale = 15f;     // Metres a second of wind for a lasting effect's second run, as smoke comes out faster the harder it blows.

        // Effects on someone, given a body in the game.
        private static readonly HashSet<string> Bodies = new(StringComparer.OrdinalIgnoreCase)
        {
            "Fire.Burning", "Frost.Chilled", "Frost.Frozen", "Lightning.Shocked", "Poison.Envenomed", "Arcane.Hexed", "Blood.Bleeding",
        };

        private static int Main(string[] args)
        {
            string command = args.Length > 0 ? args[0] : "check";
            if (command != "check")
            {
                Console.WriteLine($"No command called {command}.");
                return 1;
            }
            float seconds = 12f;
            for (int i = 1; i + 1 < args.Length; i++)
            {
                if (args[i] == "--seconds") seconds = float.Parse(args[i + 1], System.Globalization.CultureInfo.InvariantCulture);
            }

            var faults = new List<string>();
            Recipes(faults);
            Budgets(faults, seconds);
            Fight(faults);

            Console.WriteLine();
            foreach (string fault in faults.Take(40)) Console.WriteLine("  FAIL  " + fault);
            Console.WriteLine(faults.Count == 0 ? "Every effect holds up." : $"{faults.Count} faults.");
            return faults.Count == 0 ? 0 : 1;
        }

        // Most particles alive at once from one effect at a scale of 1, in the shape it's usually given, still air or a gale. A candle is everywhere, a churchyard has
        // dozens, so it's held tightest. Measured, with room to spare: an effect that's grown past its budget should be a decision.
        private static int BudgetOf(string name)
        {
            if (name == "Fire.Candle") return 8;
            if (name == "Fire.Torch") return 50;
            if (name.EndsWith(".Ground") || name.EndsWith(".Churn")) return 200;
            if (Bodies.Contains(name)) return 200;
            if (name.EndsWith(".Burst") || name.EndsWith(".Shockwave") || name.EndsWith(".Scald")) return 300;
            return 150;
        }

        private static EffectShape ShapeOf(string name)
        {
            if (Bodies.Contains(name)) return EffectShape.Body(1f, 4.2f);
            if (name.EndsWith(".Ground") || name.EndsWith(".Churn")) return EffectShape.Over(new Circle(Vector2.Zero, 6f));
            if (name.EndsWith(".Burst") || name.EndsWith(".Shockwave") || name.EndsWith(".Scald")) return EffectShape.Ball(4f);
            return EffectShape.Point;
        }

        // What every recipe says has to make sense of itself, whatever it looks like.
        private static void Recipes(List<string> faults)
        {
            var styles = new HashSet<ParticleStyle>(ParticleCatalog.Styles.Values);
            foreach (ParticleEffect effect in ParticleCatalog.Effects.Values)
            {
                if (effect.Emitters.Length == 0) faults.Add($"{effect.Name} has nothing in it");
                if (!(effect.Reach > 0f)) faults.Add($"{effect.Name} reaches nowhere, so it's culled the moment its middle leaves the screen");
                if (effect.Light is EffectLight light && !(light.Intensity > 0f && light.Radius > 0f)) faults.Add($"{effect.Name} has a light that gives none");
                foreach (ParticleEmitter emitter in effect.Emitters)
                {
                    string where = $"{effect.Name}'s {emitter.Style?.Name ?? "emitter"}";
                    if (emitter.Style == null)
                    {
                        faults.Add($"{where} has no style");
                        continue;
                    }
                    styles.Add(emitter.Style);
                    if (!(emitter.Size.Min > 0f) || emitter.Size.Max < emitter.Size.Min) faults.Add($"{where} is {emitter.Size.Min} to {emitter.Size.Max} m across");
                    if (!(emitter.Life.Min > 0f) || emitter.Life.Max < emitter.Life.Min) faults.Add($"{where} lives {emitter.Life.Min} to {emitter.Life.Max} s");
                    if (emitter.Rate < 0f || emitter.Burst < 0 || (emitter.Rate == 0f && emitter.Burst == 0)) faults.Add($"{where} emits nothing");
                    if (emitter.Fills && emitter.Place != EmitterPlace.Ground) faults.Add($"{where} fills its shape but isn't on the ground");
                }
            }

            foreach (ParticleStyle style in styles)
            {
                if (!(style.Size.Largest > 0f)) faults.Add($"{style.Name} is never any size");
                if (!(style.Opacity.Largest > 0f)) faults.Add($"{style.Name} is never there");
                for (float t = 0f; t <= 1f; t += 1f / 32f)
                {
                    float[] values = { style.Size[t], style.Opacity[t], style.Heat[t], style.Erosion[t], style.Emission?[t] ?? 0f };
                    if (values.Any(v => !float.IsFinite(v) || v < 0f))
                    {
                        faults.Add($"{style.Name} has a curve that's negative or not a number at {t:0.00}");
                        break;
                    }
                }
                if (style.Erosion.Largest >= 1f) faults.Add($"{style.Name} wears away completely, so it vanishes while it's still alive");
            }

            // The Glows a recipe asks for are made as they're asked for, four to a palette, and need room too.
            int glows = 4 * Enum.GetValues<ParticlePalette>().Length;
            if (styles.Count + glows > ParticleStyles.Most) faults.Add($"{styles.Count} styles and {glows} glows don't fit in {ParticleStyles.Most}");
            Console.WriteLine($"{ParticleCatalog.Effects.Count} effects, {styles.Count} styles and up to {glows} glows, of room for {ParticleStyles.Most}.");
        }

        // Every effect by itself, in still air and in a gale: the most it has alive at once, and that it all dies once it's stopped.
        private static void Budgets(List<string> faults, float seconds)
        {
            Console.WriteLine();
            Console.WriteLine($"{"Effect",-22} {"still",6} {"gale",6} {"budget",7}   most at once, at a scale of 1, over {seconds:0} s");
            foreach (ParticleEffect effect in ParticleCatalog.Effects.Values.OrderBy(e => e.Name))
            {
                int still = Peak(effect, seconds, Vector3.Zero, faults);
                int gale = effect.Lasts ? Peak(effect, seconds, new Vector3(Gale, 0f, 0f), faults) : still;
                int budget = BudgetOf(effect.Name);
                bool over = Math.Max(still, gale) > budget;
                Console.WriteLine($"{effect.Name,-22} {still,6} {gale,6} {budget,7}{(over ? "   OVER" : "")}");
                if (over) faults.Add($"{effect.Name} has {Math.Max(still, gale)} particles at once, over its budget of {budget}");
            }
        }

        private static int Peak(ParticleEffect effect, float seconds, Vector3 air, List<string> faults)
        {
            ParticlePool pool = Particles.Pool;
            Particles.Clear();
            var run = new EffectRun(effect) { Shape = ShapeOf(effect.Name), Prewarm = effect.Lasts };
            int peak = 0;
            int dropped = 0;
            float longest = effect.Emitters.Max(e => e.Life.Max);
            float time = 0f;
            for (; time < seconds && !run.Finished; time += Step)
            {
                run.Step(Step, true, air);
                dropped += pool.Dropped;
                pool.Sent();
                pool.Advance(Step);
                peak = Math.Max(peak, pool.Live);
            }

            // Stopped, everything it made dies by the end of the longest life it gives.
            float after = 0f;
            while (pool.Live > 0 && after < longest + 1f)
            {
                pool.Advance(Step);
                after += Step;
            }
            if (pool.Live > 0) faults.Add($"{effect.Name} leaves {pool.Live} particles alive {after:0.0} s after it stopped");
            if (dropped > 0) faults.Add($"{effect.Name} had {dropped} particles turned away by itself");
            return peak;
        }

        // A fight at its worst, in a breeze: thirty foes burning, more bleeding and chilled, six patches of ground alight, campfires, a churchyard's candles, eight blasts a
        // second and twenty blows landing. Whatever's Normal has to fit with half the pool to spare, and nothing Vital may be turned away.
        private static void Fight(List<string> faults)
        {
            ParticlePool pool = Particles.Pool;
            Particles.Clear();
            var lasting = new List<EffectRun>();
            void Begin(ParticleEffect effect, EffectShape shape, int count)
            {
                for (int i = 0; i < count; i++) lasting.Add(new EffectRun(effect) { Shape = shape, Prewarm = true, Position = new Vector3(i * 3f, 0f, 0f) });
            }
            Begin(Fire.Burning, EffectShape.Body(1f, 4.2f), 30);
            Begin(Blood.Bleeding, EffectShape.Body(1f, 4.2f), 10);
            Begin(Frost.Chilled, EffectShape.Body(1f, 4.2f), 10);
            foreach (ParticleEffect ground in new[] { Fire.Ground, Fire.Ground, Frost.Ground, Poison.Ground, Lightning.Ground, Arcane.Ground })
            {
                Begin(ground, EffectShape.Over(new Circle(Vector2.Zero, 6f)), 1);
            }
            Begin(Fire.Campfire, EffectShape.Point, 3);
            Begin(Fire.Candle, EffectShape.Point, 24);

            ParticleEffect[] blasts = { Fire.Burst, Frost.Burst, Lightning.Burst, Poison.Burst, Arcane.Burst, Impact.Shockwave };
            ParticleEffect[] blows = { Fire.Hit, Frost.Hit, Lightning.Hit, Poison.Hit, Arcane.Hit, Blood.Gush, Blood.Hit, Impact.Sparks };
            var once = new List<EffectRun>();
            var breeze = new Vector3(8f, 0f, 0f);
            int peak = 0, dropped = 0, frame = 0;
            for (float time = 0f; time < 10f; time += Step, frame++)
            {
                if (frame % 8 == 0) once.Add(new EffectRun(blasts[frame / 8 % blasts.Length]) { Shape = EffectShape.Ball(4f) });
                if (frame % 3 == 0) once.Add(new EffectRun(blows[frame / 3 % blows.Length]) { Position = Vector3.UnitY * 2f });
                foreach (EffectRun run in lasting) run.Step(Step, true, breeze);
                foreach (EffectRun run in once) run.Step(Step, true, Vector3.Zero);
                once.RemoveAll(r => r.Finished);
                dropped += pool.Dropped;
                pool.Sent();
                pool.Advance(Step);
                peak = Math.Max(peak, pool.Live);
            }

            int room = (int)(Particles.Capacity * ParticlePool.Room(ParticlePriority.Normal) * 0.5f);
            Console.WriteLine();
            Console.WriteLine($"A fight at its worst: {peak} particles at once, of {Particles.Capacity} ({room} allowed), {dropped} turned away.");
            if (peak > room) faults.Add($"a fight at its worst has {peak} particles at once, more than half the pool");
            if (dropped > 0) faults.Add($"a fight at its worst turned {dropped} particles away");
        }

        private sealed class Circle : IEffectArea
        {
            private readonly Vector2 _centre;
            private readonly float _radius;

            public Circle(Vector2 centre, float radius)
            {
                _centre = centre;
                _radius = radius;
            }

            public Vector2 Anywhere(float u, float v)
            {
                float r = _radius * MathF.Sqrt(u);
                float angle = v * MathF.Tau;
                return _centre + new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * r;
            }

            public float SquareMetres => MathF.PI * _radius * _radius;
            public Vector2 Middle => _centre;
            public float Reach => _radius;
        }
    }
}
