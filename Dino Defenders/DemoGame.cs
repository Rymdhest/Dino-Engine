﻿using Dino_Engine.Core;
using Dino_Engine.ECS;
using Dino_Engine.ECS.Components;
using Dino_Engine.ECS.Systems;
using Dino_Engine.Modelling.Model;
using Dino_Engine.Modelling.Procedural.Nature;
using Dino_Engine.Modelling.Procedural.Urban;
using Dino_Engine.Modelling.Procedural;
using Dino_Engine.Util;
using OpenTK.Mathematics;
using Dino_Engine.Modelling;
using Dino_Engine.Debug;
using Dino_Engine.Modelling.Procedural.Terrain;
using Dino_Engine.Rendering;
using Dino_Engine.Util.Data_Structures.Grids;
using Dino_Engine.Util.Noise;
using Dino_Engine;
using Util.Noise;
using OpenTK.Windowing.Common;
using OpenTK.Windowing.GraphicsLibraryFramework;
using Dino_Engine.Physics;

namespace Dino_Defenders
{
    internal class DemoGame : Game
    {

        public DemoGame(Engine engine) : base(engine)
        {
            SpawnWorld(Engine.ECSEngine);
        }
        public override void update()
        {
            ECSEngine eCSEngine = Engine.ECSEngine;
            if (Engine.WindowHandler.IsKeyPressed(Keys.F1))
            {
                SpawnWorld(eCSEngine);
            }
            if (Engine.WindowHandler.IsKeyPressed(Keys.B))
            {
                float size = 0.8f;
                float speed = 65f;
                Colour colour = new Colour(255, 255, 255, 1.0f);
                Material bigBallMaterial = new Material(colour, 0f, 0.5f, 10f);
                Entity bigBall = new Entity("Big Ball");
                bigBall.addComponent(new TransformationComponent(new Transformation(eCSEngine.Camera.getComponent<TransformationComponent>().Transformation.position, new Vector3(0f), new Vector3(size))));
                bigBall.addComponent(new ModelComponent(IcoSphereGenerator.CreateIcosphere(1, bigBallMaterial)));
                bigBall.addComponent(new VelocityComponent(speed*eCSEngine.Camera.getComponent<TransformationComponent>().Transformation.createForwardVector()));
                bigBall.addComponent(new MassComponent(1.0f));
                bigBall.addComponent(new SelfDestroyComponent(15f));
                bigBall.addComponent(new ColourComponent(colour));
                bigBall.addComponent(new AttunuationComponent(0.01f, 0.005f, 0.001f));
                bigBall.addComponent(new CollisionComponent(new SphereHitbox(size)));

                CollisionEventComponent collisionComponent = new CollisionEventComponent((collider, collisionPoint, collisionNormal) =>
                {
                    float sphereRadius = ((SphereHitbox)bigBall.getComponent<CollisionComponent>().HitBox).Radius;

                    Vector3 newPosition = collisionPoint + collisionNormal * sphereRadius;
                    Vector3 newVelocity = MyMath.reflect(bigBall.getComponent<VelocityComponent>().velocity, collisionNormal);

                    bigBall.CleanUp();

                    for (int i = 0; i< 20; i++)
                    {
                        smallSmallRandomBall(newPosition);
                    }
                    Entity emitter = new Entity("Ball Particle Emitter");
                    emitter.addComponent(new TransformationComponent(new Transformation(newPosition, new Vector3(0f, 0f, 0f), new Vector3(1))));
                    //emitter.addComponent(new ChildComponent(roadCone));
                    var emitterComponent = new ParticleEmitterComponent();
                    emitterComponent.particleSpeed = 0.4f;
                    emitterComponent.particlesPerSecond = 10f;
                    emitterComponent.particleSizeStart = 2.25f;
                    emitterComponent.particleWeight = -0.25f;
                    emitterComponent.particleSpeed = 6f;
                    emitterComponent.particleDuration = 1.4f;
                    emitterComponent.particleDirectionError = 0.5f;
                    emitterComponent.particlePositionError = 0.5f;
                    emitter.addComponent(emitterComponent);
                    emitter.addComponent(new DirectionComponent(new Vector3(0f, 1f, 0f)));
                    emitter.addComponent(new SelfDestroyComponent(2f));
                    eCSEngine.AddEnityToSystem<ParticleEmitterSystem>(emitter);
                    eCSEngine.AddEnityToSystem<SelfDestroySystem>(emitter);

                });
                bigBall.addComponent(collisionComponent);


                eCSEngine.AddEnityToSystem<ModelRenderSystem>(bigBall);
                eCSEngine.AddEnityToSystem<VelocitySystem>(bigBall);
                eCSEngine.AddEnityToSystem<GravitySystem>(bigBall);
                eCSEngine.AddEnityToSystem<SelfDestroySystem>(bigBall);
                eCSEngine.AddEnityToSystem<PointLightSystem>(bigBall);

                eCSEngine.AddEnityToSystem<CollidingSystem>(bigBall);

            }
        }

        private void smallSmallRandomBall(Vector3 position)
        {
            ECSEngine eCSEngine = Engine.ECSEngine;
            float size = 0.2f;
            float speed = 25f;
            Vector3 col = MyMath.rng3D().Normalized();
            Colour colour = new Colour(col.X, col.Y, col.Z, 5.0f);
            Material bigBallMaterial = new Material(colour, 0f, 0.5f, 1f);
            Entity bigBall = new Entity("Big Ball");
            bigBall.addComponent(new TransformationComponent(position, new Vector3(0f), new Vector3(size)));
            bigBall.addComponent(new ModelComponent(IcoSphereGenerator.CreateIcosphere(1, bigBallMaterial)));
            bigBall.addComponent(new VelocityComponent(speed * MyMath.rng3DMinusPlus().Normalized()));
            bigBall.addComponent(new MassComponent(1.0f));
            bigBall.addComponent(new SelfDestroyComponent(5f+MyMath.rng(2f)));
            bigBall.addComponent(new ColourComponent(colour));
            bigBall.addComponent(new AttunuationComponent(0.01f, 0.01f, 0.01f));
            bigBall.addComponent(new CollisionComponent(new SphereHitbox(size)));

            CollisionEventComponent collisionComponent = new CollisionEventComponent((collider, collisionPoint, collisionNormal) =>
            {
                float sphereRadius = ((SphereHitbox)bigBall.getComponent<CollisionComponent>().HitBox).Radius;

                Vector3 newPosition = collisionPoint + collisionNormal * sphereRadius;
                Vector3 newVelocity = MyMath.reflect(bigBall.getComponent<VelocityComponent>().velocity, collisionNormal);

                bigBall.getComponent<TransformationComponent>().SetLocalTransformation(newPosition);
                bigBall.getComponent<VelocityComponent>().velocity = newVelocity * 0.5f;

            });
            bigBall.addComponent(collisionComponent);


            eCSEngine.AddEnityToSystem<ModelRenderSystem>(bigBall);
            eCSEngine.AddEnityToSystem<VelocitySystem>(bigBall);
            eCSEngine.AddEnityToSystem<GravitySystem>(bigBall);
            eCSEngine.AddEnityToSystem<SelfDestroySystem>(bigBall);
            eCSEngine.AddEnityToSystem<PointLightSystem>(bigBall);

            eCSEngine.AddEnityToSystem<CollidingSystem>(bigBall);
        }

        private void SpawnWorld(ECSEngine eCSEngine)
        {
            eCSEngine.ClearAllEntitiesExcept(eCSEngine.Camera);
            eCSEngine.InitEntities();
            spawnTerrain(eCSEngine);
            //spawnCity(eCSEngine);
        }
            private void spawnTerrain(ECSEngine eCSEngine)
        {

            TreeGenerator treeGenerator = new TreeGenerator(); Entity groundPlane = new Entity("Terrain");
            groundPlane.addComponent(new TransformationComponent(new Vector3(100, 0.0f, -100), new Vector3(0), new Vector3(1f)));

            TerrainGridGenerator terrainGridGenerator = new TerrainGridGenerator();

            TaskTracker terrainGridTracker = Engine.PerformanceMonitor.startTask("terrain grid", true);
            FloatGrid terrainGrid = terrainGridGenerator.generateChunk(new Vector2i(200, 200));

            Engine.PerformanceMonitor.finishTask(terrainGridTracker, true);

            TaskTracker terrainMeshTracker = Engine.PerformanceMonitor.startTask("terrain mesh", true);
            Mesh rawGround = TerrainMeshGenerator.GridToMesh(terrainGrid, out Vector3Grid terrainNormals);
            Engine.PerformanceMonitor.finishTask(terrainMeshTracker, true);




            glModel groundModel = glLoader.loadToVAO(rawGround);
            groundPlane.addComponent(new ModelComponent(groundModel));
            groundPlane.addComponent(new TerrainMapsComponent(terrainGrid, terrainNormals));
            groundPlane.addComponent(new CollisionComponent(new TerrainHitBox(new Vector3(0), new Vector3(terrainGrid.Resolution.X, 100, terrainGrid.Resolution.Y))));
            eCSEngine.AddEnityToSystem<ModelRenderSystem>(groundPlane);
            eCSEngine.AddEnityToSystem<TerrainSystem>(groundPlane);
            eCSEngine.AddEnityToSystem<CollidableSystem>(groundPlane);

            OpenSimplexNoise noise = new OpenSimplexNoise();

            FloatGrid spawnGrid = new FloatGrid(terrainGrid.Resolution);

            FloatGrid terrainSteepnessMap = groundPlane.getComponent<TerrainMapsComponent>().steepnessMap;

            for (int z = 0; z < spawnGrid.Resolution.Y; z++)
            {
                for (int x = 0; x < spawnGrid.Resolution.X; x++)
                {
                    float value = noise.Evaluate(x * 0.01f, z * 0.01f) / 2f + 0.5f;
                    value *= 0.9f;
                    value += 0.1f;

                    float height = terrainGrid.Values[x, z];
                    value += height / 85f;

                    spawnGrid.Values[x, z] = MyMath.clamp01(value * value);
                }
            }
            DebugRenderer.texture = terrainSteepnessMap.GetTexture();
            //DebugRenderer.texture = spawnGrid.GenerateTexture();

            //PoissonDiskSampling poissonDiskSampling = new PoissonDiskSampling(terrainSteepnessMap, 300f);

            BetterNoiseSampling betterNoiseSampling = new BetterNoiseSampling(terrainGrid.Resolution);

            TaskTracker noiseTracker = Engine.PerformanceMonitor.startTask("noise sampling", true);
            //List<Vector2> spawnPoints = poissonDiskSampling.GeneratePoints();
            List<Vector2> spawnPoints = betterNoiseSampling.GeneratePoints(spawnGrid);
            Engine.PerformanceMonitor.finishTask(noiseTracker, true);

            Mesh mesh = treeGenerator.GenerateFractalTree(1);
            mesh.makeFlat(true, true);
            mesh.scale(new Vector3(8f));

            TaskTracker placingTracker = Engine.PerformanceMonitor.startTask("placing trees and rocks", true);
            foreach (Vector2 spawn in spawnPoints)
            {
                Entity tree = new Entity("tree");
                float y = terrainGrid.BilinearInterpolate(spawn);
                if (y < 0) continue;
                if (y > 45) continue;
                tree.addComponent(new TransformationComponent(new Vector3(spawn.X, y - 0.1f, spawn.Y), new Vector3(0, MyMath.rng(MathF.PI * 2f), 0f), new Vector3(0.5f + MyMath.rng(0.5f))));
                tree.addComponent(new ModelComponent(mesh));
                eCSEngine.AddEnityToSystem<ModelRenderSystem>(tree);
                tree.addComponent(new ChildComponent(groundPlane));
                tree.addComponent(new SelfDestroyComponent(1 + MyMath.rng(2f)));
                RenderEngine._debugRenderer.circles.Add(new Circle(spawn, 1f));
            }

            Mesh rockMesh = IcoSphereGenerator.CreateIcosphere(2, Material.ROCK);
            rockMesh.FlatRandomness(0.1f);
            rockMesh.makeFlat(true, true);
            spawnPoints = betterNoiseSampling.GeneratePoints(terrainSteepnessMap);
            foreach (Vector2 spawn in spawnPoints)
            {
                Entity rock = new Entity("rock");
                float y = terrainGrid.BilinearInterpolate(spawn);
                //if (y < 0) continue;
                //if (y > 45) continue;
                Vector3 position = new Vector3(spawn.X, y - 0.1f, spawn.Y);
                Vector3 scale = new Vector3(new Vector3(0.5f) + MyMath.rng3D(2.0f));
                scale += new Vector3(1f - terrainSteepnessMap.BilinearInterpolate(position.Xz)) * 1f;

                rock.addComponent(new TransformationComponent(position, MyMath.rng3D(MathF.PI * 2f), scale));
                rock.addComponent(new ModelComponent(rockMesh));
                rock.addComponent(new ChildComponent(groundPlane));
                rock.addComponent(new CollisionComponent(new SphereHitbox(scale.X)));
                eCSEngine.AddEnityToSystem<ModelRenderSystem>(rock);
                eCSEngine.AddEnityToSystem<CollidableSystem>(rock);
                RenderEngine._debugRenderer.circles.Add(new Circle(spawn, 1f));
            }
            Engine.PerformanceMonitor.finishTask(placingTracker, true);
        }

        private void spawnCity(ECSEngine eCSEngine)
        {
            glModel houseModel = ModelGenerator.GenerateHouse();
            TreeGenerator treeGenerator = new TreeGenerator();

            for (int x = 0; x < 2; x++)
            {
                for (int z = 0; z < 2; z++)
                {
                    Entity house = new Entity("House");
                    house.addComponent(new TransformationComponent(new Vector3(45 + 55 * x, 0, 45 + 55f * z), new Vector3(0, MyMath.rand.Next(8) * MathF.PI / 4, 0f), new Vector3(2f)));
                    house.addComponent(new ModelComponent(houseModel));
                    eCSEngine.AddEnityToSystem<ModelRenderSystem>(house);
                }
            }

            StreetGenerator streetGenerator = new StreetGenerator();

            Entity crossRoad = new Entity("crossroad");
            crossRoad.addComponent(new TransformationComponent(new Transformation()));
            crossRoad.addComponent(new ModelComponent(streetGenerator.GenerateCrossRoad()));
            eCSEngine.AddEnityToSystem<ModelRenderSystem>(crossRoad);

            int nr = 0;
            for (int i = 2; i < 13; i++)
            {
                for (int j = 0; j < streetGenerator.lanes; j++)
                {
                    Entity car = new Entity("car " + nr);
                    float x = streetGenerator.laneWdith * j + streetGenerator.laneWdith * (0.5f + MyMath.rngMinusPlus(0.15f));
                    float z = 13f * i + MyMath.rngMinusPlus(4f);
                    car.addComponent(new TransformationComponent(new Transformation(new Vector3(x, 0f, z), new Vector3(0f, MyMath.rngMinusPlus(0.03f), 0f), new Vector3(1.7f + MyMath.rngMinusPlus(0.2f)))));
                    car.addComponent(new ModelComponent(CarGenerator.GenerateCar(out Vector3 leftLight, out Vector3 rightLight, out Vector3 exhaustPos)));
                    eCSEngine.AddEnityToSystem<ModelRenderSystem>(car);

                    Entity carLightLeft = new Entity("car light left");
                    carLightLeft.addComponent(new TransformationComponent(leftLight, new Vector3(MathF.PI / 2.2f, 0f, 0), new Vector3(1f)));
                    carLightLeft.addComponent(new AttunuationComponent(0.001f, 0.01f, 0.001f));
                    carLightLeft.addComponent(new ColourComponent(new Colour(1f, 0.8f, 0.6f, 1f)));
                    carLightLeft.addComponent(new ChildComponent(car));
                    eCSEngine.AddEnityToSystem<SpotLightSystem>(carLightLeft);

                    Entity carLightRight = new Entity("car light right");
                    carLightRight.addComponent(new TransformationComponent(rightLight, new Vector3(MathF.PI / 2.2f, 0f, 0), new Vector3(1f)));
                    carLightRight.addComponent(new AttunuationComponent(0.001f, 0.01f, 0.001f));
                    carLightRight.addComponent(new ColourComponent(new Colour(1f, 0.8f, 0.6f, 1f)));
                    carLightRight.addComponent(new ChildComponent(car));
                    eCSEngine.AddEnityToSystem<SpotLightSystem>(carLightRight);

                    Entity emitter = new Entity("car exhaust Particle Emitter");
                    emitter.addComponent(new TransformationComponent(new Transformation(exhaustPos, new Vector3(0f, 0f, 0f), new Vector3(1))));
                    //emitter.addComponent(new ChildComponent(roadCone));
                    var emitterComponent = new ParticleEmitterComponent();
                    emitterComponent.particleSpeed = 0.2f;
                    emitterComponent.particlesPerSecond = 20f;
                    emitterComponent.particleSizeStart = 0.25f;
                    emitterComponent.particleWeight = -0.25f;
                    emitterComponent.particleSpeed = 6f;
                    emitterComponent.particleDuration = 1.4f;
                    emitterComponent.particleDirectionError = 0.3f;
                    emitterComponent.particlePositionError = 0.1f;
                    emitter.addComponent(emitterComponent);
                    emitter.addComponent(new DirectionComponent(new Vector3(0f, 0f, 1f)));
                    emitter.addComponent(new ChildComponent(car));
                    eCSEngine.AddEnityToSystem<ParticleEmitterSystem>(emitter);

                    nr++;
                }
            }

            Mesh streetModel = streetGenerator.GenerateStreet(30, out float streetLength);
            for (int i = 0; i < 4; i++)
            {
                Entity street = new Entity("street");
                Vector3 position = (new Vector4(0, 0f, streetGenerator.TotalWidth * 0.5f, 1f) * MyMath.createRotationMatrix(new Vector3(0f, i * (MathF.PI / 2f), 0f))).Xyz;
                Transformation transformation = new Transformation(position, new Vector3(0f, i * (MathF.PI / 2f), 0f), new Vector3(1));
                street.addComponent(new TransformationComponent(transformation));
                street.addComponent(new ModelComponent(streetModel));
                eCSEngine.AddEnityToSystem<ModelRenderSystem>(street);
            }



            for (int side = -1; side <= 1; side += 2)
            {
                for (int i = 0; i < 10; i++)
                {
                    Entity streetLight = new Entity("Street Light" + i);
                    streetLight.addComponent(new TransformationComponent(new Vector3((streetGenerator.TotalWidth - streetGenerator.sideWalkWidth * 1.7f) * 0.5f * side, 0f, 50 + 30 * i), new Vector3(0f, MathF.PI / 2f + MathF.PI / 2f * side, 0f), new Vector3(1f, 1f, 1f)));
                    streetLight.addComponent(new ModelComponent(UrbanPropGenerator.GenerateStreetLight(out Vector3 lightPosition)));
                    eCSEngine.AddEnityToSystem<ModelRenderSystem>(streetLight);

                    Entity glow = new Entity("Street Light" + i + " glow");
                    glow.addComponent(new TransformationComponent(lightPosition, new Vector3(0f), new Vector3(1f)));
                    glow.addComponent(new AttunuationComponent(0.001f, 0.001f, 0.001f));
                    glow.addComponent(new ColourComponent(new Colour(1f, 0.8f, 0.6f, 20f)));
                    glow.addComponent(new ChildComponent(streetLight));
                    eCSEngine.AddEnityToSystem<SpotLightSystem>(glow);


                    Entity streetTree = new Entity("Street tree" + i);
                    streetTree.addComponent(new TransformationComponent(new Vector3((streetGenerator.TotalWidth - streetGenerator.sideWalkWidth * 1.7f) * 0.5f * side, 0f, 35 + 30 * i), new Vector3(0f, MathF.PI / 2f + MathF.PI / 2f * side, 0f), new Vector3(15f)));
                    streetTree.addComponent(new ModelComponent(treeGenerator.GenerateFractalTree(1)));
                    eCSEngine.AddEnityToSystem<ModelRenderSystem>(streetTree);
                }
            }

            for (int i = 0; i < 8; i++)
            {
                Entity roadCone = new Entity("roadCone");
                roadCone.addComponent(new TransformationComponent(new Transformation(new Vector3(2 * i, 0f, 15f), new Vector3(0f, MathF.PI * 2f * MyMath.rng(), 0f), new Vector3(2))));
                roadCone.addComponent(new ModelComponent(UrbanPropGenerator.GenerateStreetCone()));
                eCSEngine.AddEnityToSystem<ModelRenderSystem>(roadCone);

            }
        }


    }
}
