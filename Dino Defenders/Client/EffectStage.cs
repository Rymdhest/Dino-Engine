using Dino_Defenders.Gameplay;
using Dino_Engine.Particles;
using Dino_Engine.Particles.Content;
using Dino_Engine.World;
using OpenTK.Mathematics;

namespace Dino_Defenders.Client
{
    /// <summary>
    /// Effects stood in rows in front of the hero, for looking at in a scripted run (--fx): what goes on goes on, a bolt's trail goes round in a ring, someone suffering is the
    /// shape of someone standing there, a patch of ground is a circle, and what happens once happens again every few seconds.
    /// </summary>
    public sealed class EffectStage
    {
        private const float Spacing = 13f;
        private const int PerRow = 7;
        private const float Again = 2.5f;
        private const float BoltRing = 2.5f;
        private const float BoltSpeed = 12f;

        private static readonly HashSet<string> Bodies = new(StringComparer.OrdinalIgnoreCase)
        {
            "Fire.Burning", "Frost.Chilled", "Frost.Frozen", "Lightning.Shocked", "Poison.Envenomed", "Arcane.Hexed", "Blood.Bleeding",
        };

        private readonly List<(ParticleEffect Effect, Vector3 Position, EffectShape Shape)> _once = new();
        private readonly List<(EffectRun Run, Vector3 Centre)> _bolts = new();
        private float _clock;
        private float _next;

        public EffectStage(string names, Vector2 from, WorldData world)
        {
            List<string> chosen = names == "all" ? ParticleCatalog.Effects.Keys.OrderBy(n => n).ToList() : names.Split(',').ToList();
            for (int i = 0; i < chosen.Count; i++)
            {
                if (ParticleCatalog.Effect(chosen[i]) is not ParticleEffect effect)
                {
                    Console.WriteLine($"No effect called '{chosen[i]}'.");
                    continue;
                }
                int row = i / PerRow;
                int column = i % PerRow;
                int inRow = Math.Min(PerRow, chosen.Count - row * PerRow);
                Vector2 at = from + new Vector2((column - (inRow - 1) * 0.5f) * Spacing, row * Spacing);
                Vector3 position = new Vector3(at.X, world.Height.SampleCubic(at.X, at.Y), at.Y);
                string name = effect.Name;

                if (name.EndsWith(".Bolt"))
                {
                    _bolts.Add((Particles.Begin(effect, position + Vector3.UnitY * 2f, EffectShape.Point), position + Vector3.UnitY * 2f));
                }
                else if (Bodies.Contains(name))
                {
                    Particles.Begin(effect, position, EffectShape.Body(1f, 4.2f), prewarm: true);
                }
                else if (name.EndsWith(".Ground") || name.EndsWith(".Churn"))
                {
                    Particles.Begin(effect, position, EffectShape.Over(new GroundArea(GroundShape.Circle(at, 6f))), prewarm: true);
                }
                else if (effect.Lasts)
                {
                    Particles.Begin(effect, position, EffectShape.Point, prewarm: true);
                }
                else
                {
                    bool burst = name.EndsWith(".Burst") || name.EndsWith(".Shockwave") || name.EndsWith(".Scald");
                    _once.Add((effect, position + (burst ? Vector3.Zero : Vector3.UnitY * 2f), burst ? EffectShape.Ball(4f) : EffectShape.Point));
                }
            }
        }

        public void Update(float seconds)
        {
            _clock += seconds;
            foreach (var (run, centre) in _bolts)
            {
                float angle = _clock * BoltSpeed / BoltRing;
                run.Position = centre + new Vector3(MathF.Cos(angle), 0f, MathF.Sin(angle)) * BoltRing;
            }
            if (_clock < _next) return;
            _next = _clock + Again;
            foreach (var (effect, position, shape) in _once) Particles.Play(effect, position, 1f, shape, Vector3.UnitX);
        }
    }
}
