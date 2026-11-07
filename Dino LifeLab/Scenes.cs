using Dino_Engine.Modelling.Procedural.Wildlife;
using Dino_Engine.Wildlife;
using OpenTK.Mathematics;

namespace Dino_LifeLab
{
    /// <summary>Everything there is to look at. Each drives the same poses the game does.</summary>
    internal static class Scenes
    {
        private static readonly Skeleton Rabbit = RabbitBody.NewSkeleton();
        private static readonly BirdLook Finch = new BirdLook();
        private static readonly Skeleton FinchBones = BirdBody.NewSkeleton(Finch);
        private static readonly Skeleton Crab = CrabBody.NewSkeleton();
        private static readonly CrabLegs CrabFeet = new CrabLegs();
        private static float _crabAt = float.NegativeInfinity;
        private static readonly Skeleton Butterfly = ButterflyBody.NewSkeleton();
        private static readonly DragonflyLook Darter = new DragonflyLook();
        private static readonly Skeleton Dragonfly = DragonflyBody.NewSkeleton(Darter);
        private static readonly Skeleton Frog = FrogBody.NewSkeleton();
        private static readonly FishLook Carp = new FishLook();
        private static readonly Skeleton Fish = FishBody.NewSkeleton(Carp);
        private static readonly Skeleton Leaf = LeafBody.NewSkeleton();

        public static readonly Scene[] All =
        {
            new Scene("rabbit.rest", () => RabbitBody.Build(new RabbitLook()), RabbitBody.Count, 1f, 2.4f, (_, skin) => new RabbitPose().Solve(Rabbit, Matrix4.Identity, skin)),
            new Scene("rabbit.sit", () => RabbitBody.Build(new RabbitLook()), RabbitBody.Count, 1f, 2.4f, (time, skin) => new RabbitPose { Rear = time }.Solve(Rabbit, Matrix4.Identity, skin)),
            new Scene("rabbit.graze", () => RabbitBody.Build(new RabbitLook()), RabbitBody.Count, 1f, 2.4f, (time, skin) => new RabbitPose { HeadPitch = 1.1f * time, Pitch = 0.2f * time, EarsBack = 0.35f * time }.Solve(Rabbit, Matrix4.Identity, skin)),
            new Scene("rabbit.flat", () => RabbitBody.Build(new RabbitLook()), RabbitBody.Count, 1f, 2.4f, (time, skin) => new RabbitPose { Crouch = time, EarsBack = time }.Solve(Rabbit, Turn.Root(new Vector3(0f, -0.09f * time, 0f), Quaternion.Identity, 1f), skin)),
            new Scene("rabbit.bound", () => RabbitBody.Build(new RabbitLook()), RabbitBody.Count, 1f, 3.6f, Bound),
            new Scene("bird.stand", () => BirdBody.Build(Finch), BirdBody.Count, 1f, 1.1f, (_, skin) => new BirdPose { Fold = 1f, Pitch = -0.35f, Legs = 1f }.Solve(FinchBones, Matrix4.Identity, skin)),
            new Scene("bird.open", () => BirdBody.Build(Finch), BirdBody.Count, 1f, 1.5f, (time, skin) => new BirdPose { Fold = 1f - time, Pitch = -0.35f, Legs = 1f }.Solve(FinchBones, Matrix4.Identity, skin)),
            new Scene("bird.flap", () => BirdBody.Build(Finch), BirdBody.Count, MathF.Tau, 1.5f, (time, skin) => new BirdPose { Stroke = time, Beat = 1f, Pitch = 0.05f }.Solve(FinchBones, Matrix4.Identity, skin)),
            new Scene("crab.stand", () => CrabBody.Build(new CrabLook()), CrabBody.Count, 1f, 1.5f, (time, skin) => Scuttle(0f, time, 0f, skin)),
            new Scene("crab.threat", () => CrabBody.Build(new CrabLook()), CrabBody.Count, 1f, 1.5f, (time, skin) => Scuttle(0f, time, time, skin)),
            new Scene("crab.scuttle", () => CrabBody.Build(new CrabLook()), CrabBody.Count, 0.72f, 2.6f, (time, skin) => Scuttle(3.5f, time, 0f, skin), time => new Vector3(3.5f * time, 0f, 0f)),
            new Scene("butterfly", () => ButterflyBody.Build(new ButterflyLook()), ButterflyBody.Count, 1f, 0.7f, (time, skin) => new ButterflyPose { Wings = 1.45f * time }.Solve(Butterfly, Matrix4.Identity, skin)),
            new Scene("dragonfly", () => DragonflyBody.Build(Darter), DragonflyBody.Count, MathF.Tau, 1.2f, (time, skin) => new DragonflyPose { Stroke = time }.Solve(Dragonfly, Matrix4.Identity, skin)),
            new Scene("frog", () => FrogBody.Build(new FrogLook()), FrogBody.Count, 1f, 1.1f, (time, skin) => new FrogPose { Leap = time }.Solve(Frog, Matrix4.Identity, skin)),
            new Scene("fish", () => FishBody.Build(Carp), FishBody.Count, MathF.Tau, 1.6f, (time, skin) => new FishPose { Bend = MathF.Sin(time) * 0.6f, Lag = MathF.Sin(time + 1.1f) * 0.3f }.Solve(Fish, Matrix4.Identity, skin)),
            new Scene("leaf.oak", () => LeafBody.Build(Dino_Engine.Modelling.Procedural.Flora.Broadleaves.OakSpray.Leaf, 0.42f), LeafBody.Count, 1f, 0.7f, (time, skin) => new LeafPoseDemo(time).Write(Leaf, skin)),
            new Scene("leaf.maple", () => LeafBody.Build(Dino_Engine.Modelling.Procedural.Flora.Broadleaves.MapleSpray.Leaf, 0.42f), LeafBody.Count, 1f, 0.7f, (time, skin) => new LeafPoseDemo(time).Write(Leaf, skin)),
            new Scene("leaf.beech", () => LeafBody.Build(Dino_Engine.Modelling.Procedural.Flora.Broadleaves.BeechSpray.Leaf, 0.42f), LeafBody.Count, 1f, 0.7f, (time, skin) => new LeafPoseDemo(time).Write(Leaf, skin)),
        };

        public static Scene? Named(string name) => All.FirstOrDefault(scene => scene.Name == name);

        // A crab walking along +x, as the lab has no world: the same legs as anywhere, on flat ground.
        private static void Scuttle(float speed, float time, float raise, Matrix4[] skin)
        {
            Func<Vector2, float> flat = _ => 0f;
            Matrix4 root = Turn.Root(new Vector3(speed * time, 0f, 0f), Quaternion.Identity, 1f);
            if (time < _crabAt || _crabAt < 0f)
            {
                CrabFeet.Place(root, flat);
                _crabAt = 0f;
            }

            float deltaTime = 1f / 240f;
            while (_crabAt < time)
            {
                _crabAt += deltaTime;
                CrabFeet.Step(Turn.Root(new Vector3(speed * _crabAt, 0f, 0f), Quaternion.Identity, 1f), new Vector3(speed, 0f, 0f), flat, deltaTime);
            }
            CrabFeet.Solve(Crab, new CrabPose { Raise = raise, Tilt = raise * 0.24f }, root, skin);
        }

        private static void Bound(float through, Matrix4[] skin)
        {
            var pose = new RabbitPose { EarsBack = 0.85f, Tail = 1f };
            RabbitGait.Apply(ref pose, through, 1f);
            Vector3 at = new Vector3(0f, RabbitGait.Height(through) * 0.7f, 1.7f * (RabbitGait.Along(through) - 0.5f));
            pose.Solve(Rabbit, Turn.Root(at, Quaternion.Identity, 1f), skin);
        }
    }

    /// <summary>A leaf turned to show both of its sides.</summary>
    internal readonly record struct LeafPoseDemo(float Time)
    {
        public void Write(Skeleton skeleton, Matrix4[] skin)
        {
            Span<Quaternion> turn = stackalloc Quaternion[1];
            turn[0] = Turn.Yaw(Time * 2f) * Turn.Pitch(-0.7f - Time * 1.2f);
            skeleton.Solve(turn, Turn.Root(new Vector3(0f, 0.15f, 0f), Quaternion.Identity, 1f), skin);
        }
    }
}
