using Dino_Engine.Modelling.Model;
using Dino_Engine.Modelling.Procedural.Architecture.Town;
using Dino_Engine.World;
using Dino_Engine.World.Content;
using OpenTK.Mathematics;

namespace Dino_WorldLab
{
    /// <summary>
    /// Builds every piece of the Haven's town from its id, as the game does while the Haven loads (HAVEN.md), and says what each kind costs: how many there are, their
    /// triangles and lights, and how long they take to build. Fails on a piece that builds nothing, has a point that isn't a number, reaches somewhere it can't or costs more
    /// than it should; on anything hung wrongly, a joint that doesn't hang down or one holding something far to the side of it, which it would swing about its own top; on a
    /// home nobody can walk up to the door of, which is what the town before this one was called out for; and on a town with too few homes, stalls or lamps to be one.
    /// </summary>
    internal static class TownCheck
    {
        private const int MostTriangles = 700_000;          // The whole town's, every piece once.
        private const int MostPieceTriangles = 60_000;
        private const int MostLights = 70;
        private const long MostBuildMs = 4000;              // The whole town's, on one thread.
        private const float FarthestReach = 30f;            // Metres from its own origin that a piece's model may reach.
        private const float WidestHang = 1.7f;              // Metres to the side of its joint that anything hung may be.
        private const float Body = 0.8f;                    // A person's shoulders (WorldData.WorldRadius).
        private const float FarthestWay = 12f;              // Metres from a door to ground the town is walked from, at most: a garden's depth.
        private const int LeastHomes = 8, LeastStalls = 4, LeastLamps = 6;
        private const int HavenSeed = 1610;                 // Lands.HavenSeed, in the game.

        public static int Run()
        {
            WorldData world = new Dino_Engine.World.Generation.WorldGenerator().Generate(new WorldRequest(HavenSeed, "haven"));
            var ids = world.Objects.AssetIds.Where(TownSpec.IsTown).ToList();
            int problems = 0, triangles = 0, lights = 0, hung = 0;
            long total = 0;
            var byKind = new SortedDictionary<string, (int Count, int Triangles, int Lights, long Ms)>();
            foreach (string id in ids)
            {
                if (!TownSpec.TryParse(id, out TownSpec spec)) { Console.WriteLine($"PROBLEM: '{id}' doesn't parse"); problems++; continue; }
                if (spec.Id != id) { Console.WriteLine($"PROBLEM: '{id}' writes back as '{spec.Id}'"); problems++; }
                var lit = new List<AssetLight>();
                var smoke = new List<Vector3>();
                var watch = System.Diagnostics.Stopwatch.StartNew();
                Mesh mesh = TownForge.Make(spec, lit, smoke);
                watch.Stop();
                int count = mesh.getAllIndicesArray().Length / 3;
                if (count == 0) { Console.WriteLine($"PROBLEM: {id} builds nothing"); problems++; }
                if (count > MostPieceTriangles) { Console.WriteLine($"PROBLEM: {id} is {count} triangles"); problems++; }
                float reach = 0f;
                bool broken = false;
                foreach (MeshVertex vertex in mesh.meshVertices)
                {
                    Vector3 p = vertex.position;
                    if (!float.IsFinite(p.X) || !float.IsFinite(p.Y) || !float.IsFinite(p.Z)) broken = true;
                    else reach = MathF.Max(reach, p.Length);
                }
                if (broken) { Console.WriteLine($"PROBLEM: {id} has a point that isn't a number"); problems++; }
                if (reach > FarthestReach) { Console.WriteLine($"PROBLEM: {id} reaches {reach:0.0} m from its origin"); problems++; }
                problems += Hung(id, mesh, ref hung);

                string kind = spec.Kind == TownKind.Yardwork ? $"Yard {(YardTrade)spec.Int(0)}" : spec.Kind.ToString();
                byKind.TryGetValue(kind, out var sum);
                byKind[kind] = (sum.Count + 1, sum.Triangles + count, sum.Lights + lit.Count, sum.Ms + watch.ElapsedMilliseconds);
                triangles += count;
                lights += lit.Count;
                total += watch.ElapsedMilliseconds;
            }
            foreach ((string kind, var sum) in byKind) Console.WriteLine($"{kind,-15} {sum.Count,3} pieces {sum.Triangles,8} triangles {sum.Lights,3} lights {sum.Ms,5} ms");
            Console.WriteLine($"All             {ids.Count,3} pieces {triangles,8} triangles {lights,3} lights {total,5} ms, {hung} things hung");

            // What stands in the world: enough of a town, and a way to every door.
            int homes = 0, stalls = 0, lamps = 0, standing = 0;
            foreach (WorldObject item in world.Objects.All())
            {
                string id = world.Objects.AssetIds[item.Asset];
                if (!TownSpec.TryParse(id, out TownSpec spec)) continue;
                standing++;
                if (spec.Kind == TownKind.Stall) stalls++;
                if (spec.Kind == TownKind.Lamp) lamps++;
                if (spec.Kind != TownKind.House) continue;
                homes++;
                Vector3 front = Vector3.Transform(Vector3.UnitZ, item.Rotation), along = Vector3.Transform(Vector3.UnitX, item.Rotation);
                Vector2 door = item.Position.Xz + along.Xz * TownForge.DoorAt(spec) + front.Xz * (spec.Metres(1) * 0.5f);
                // Straight out from the door, as its path is laid, a body has to fit all the way to ground the rest of the town is walked from.
                bool reached = false;
                Vector2 stopped = door;
                for (float out_ = 1f; out_ <= FarthestWay && !reached; out_ += 0.5f)
                {
                    stopped = door + front.Xz * out_;
                    if (world.Placement.OverlapsSolid(Footprint.Circle(stopped, Body))) break;
                    reached = world.Passable.IsSet(stopped);
                }
                if (!reached)
                {
                    Console.WriteLine($"PROBLEM: nobody can walk up to the door of {id} at {item.Position.X:0.0},{item.Position.Z:0.0}: the way out from it stops at {stopped.X:0.0},{stopped.Y:0.0}");
                    problems++;
                }
            }
            Console.WriteLine($"{standing} pieces standing: {homes} homes, {stalls} stalls, {lamps} lamps on posts.");
            if (ids.Count == 0) { Console.WriteLine("PROBLEM: the Haven has no town"); problems++; }
            if (homes < LeastHomes) { Console.WriteLine($"PROBLEM: {homes} homes, under {LeastHomes}"); problems++; }
            if (stalls < LeastStalls) { Console.WriteLine($"PROBLEM: {stalls} stalls, under {LeastStalls}"); problems++; }
            if (lamps < LeastLamps) { Console.WriteLine($"PROBLEM: {lamps} lamps, under {LeastLamps}"); problems++; }
            if (triangles > MostTriangles) { Console.WriteLine($"PROBLEM: over {MostTriangles} triangles"); problems++; }
            if (lights > MostLights) { Console.WriteLine($"PROBLEM: {lights} lights, over {MostLights}"); problems++; }
            if (total > MostBuildMs) { Console.WriteLine($"PROBLEM: takes over {MostBuildMs} ms to build"); problems++; }
            Console.WriteLine(problems == 0 ? "The town holds up." : $"{problems} problems.");
            return problems == 0 ? 0 : 1;
        }

        // Whatever a piece hangs (MeshBuilder.Hang): each joint has to hang down, and hold nothing far to the side of its own line.
        private static int Hung(string id, Mesh mesh, ref int hung)
        {
            if (mesh.joints == null) return 0;
            int problems = 0;
            var widest = new float[mesh.joints.Count];
            foreach (MeshVertex vertex in mesh.meshVertices)
            {
                if (vertex.joint < 0) continue;
                if (vertex.joint >= mesh.joints.Count) { Console.WriteLine($"PROBLEM: {id} has a point tied to a joint it hasn't got"); return 1; }
                PlantJoint joint = mesh.joints[vertex.joint];
                Vector3 axis = joint.Tip - joint.Base;
                float length = axis.Length;
                if (length < 1e-3f) continue;
                axis /= length;
                Vector3 from = vertex.position - joint.Base;
                float side = (from - axis * Vector3.Dot(from, axis)).Length;
                widest[vertex.joint] = MathF.Max(widest[vertex.joint], side);
            }
            for (int i = 0; i < mesh.joints.Count; i++)
            {
                PlantJoint joint = mesh.joints[i];
                hung++;
                Vector3 axis = joint.Tip - joint.Base;
                if (axis.Length < 0.05f) { Console.WriteLine($"PROBLEM: {id} hangs something from a joint {axis.Length:0.00} m long"); problems++; continue; }
                if (axis.Y > -0.5f * axis.Length) { Console.WriteLine($"PROBLEM: {id} hangs something from a joint that doesn't hang down"); problems++; }
                if (widest[i] > WidestHang) { Console.WriteLine($"PROBLEM: {id} hangs something {widest[i]:0.0} m to the side of its joint, which would swing it about that joint's top"); problems++; }
            }
            return problems;
        }
    }
}
