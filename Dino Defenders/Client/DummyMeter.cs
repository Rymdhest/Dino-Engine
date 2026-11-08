using Dino_Defenders.Gameplay;
using Dino_Defenders.Stats;
using Dino_Engine.ECS.ECS_Architecture;
using Dino_Engine.Networking;

namespace Dino_Defenders.Client
{
    /// <summary>
    /// What's being dealt to each group of the Haven's training dummies, for the number over them. It counts from the first blow after they were last left alone to the last, so what it says is what a
    /// build does over a whole fight, not the best moment of one, and it starts again once they've been left long enough to be as good as new. It only reads what's shown of blows, which can go missing,
    /// so it's a reading and never anything the rules go by.
    /// </summary>
    public sealed class DummyMeter
    {
        public const int Groups = 3;

        /// <summary>One group's reading: what's been dealt, from when to when, and the biggest blow.</summary>
        public struct Reading
        {
            public float Dealt;
            public double First;
            public double Last;
            public float Biggest;
            public int Blows;

            /// <summary>Seconds it's been going, never less than one, as a single blow isn't a rate.</summary>
            public readonly float Seconds => MathF.Max((float)(Last - First), 1f);
            public readonly float PerSecond => Dealt / Seconds;
        }

        private readonly Reading[] _readings = new Reading[Groups];

        public Reading this[int group] => _readings[group];

        /// <summary>Whether a group has been hit lately enough to show what it's taken.</summary>
        public bool Showing(int group, double now) => _readings[group].Blows > 0 && now - _readings[group].Last < Rulebook.DummyRest * 2f;

        /// <summary>A blow shown landing. Only those on a dummy count.</summary>
        public void Landed(in DamageEvent hit, GameClient client, double now)
        {
            if (hit.ToHero || hit.Target == 0 || !client.Ghosts.Ghosts.TryGetValue(hit.Target, out Ghost? ghost)) return;
            ECSWorld world = client.Ecs;
            if (!ghost.Entity.IsValid() || !world.TryGetEntityLocation(ghost.Entity, out _, out _)) return;
            EntityView view = world.GetEntityView(ghost.Entity);
            if (!view.Has<DummyComponent>()) return;

            int group = Math.Clamp(view.Get<DummyComponent>().Group, 0, Groups - 1);
            ref Reading reading = ref _readings[group];
            if (reading.Blows == 0 || now - reading.Last > Rulebook.DummyRest) reading = new Reading { First = now };
            reading.Dealt += hit.Amount;
            reading.Last = now;
            reading.Biggest = MathF.Max(reading.Biggest, hit.Amount);
            reading.Blows++;
        }
    }
}
