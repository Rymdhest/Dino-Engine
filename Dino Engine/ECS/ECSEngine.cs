using Dino_Engine.Core;
using Dino_Engine.ECS.Components;
using Dino_Engine.ECS.Systems;
using Dino_Engine.Modelling;
using Dino_Engine.Modelling.Model;
using Dino_Engine.Modelling.Procedural;
using Dino_Engine.Modelling.Procedural.Nature;
using Dino_Engine.Modelling.Procedural.Terrain;
using Dino_Engine.Modelling.Procedural.Urban;
using Dino_Engine.Rendering.Renderers.PostProcessing;
using Dino_Engine.Util;
using Dino_Engine.Util.Noise;
using OpenTK.Mathematics;
using OpenTK.Windowing.Common;
using OpenTK.Windowing.GraphicsLibraryFramework;
using Util.Noise;
using Dino_Engine.Rendering;
using Dino_Engine.Debug;
using System.Diagnostics;
using System.Security.Cryptography;

namespace Dino_Engine.ECS
{
    public class ECSEngine
    {

        private Dictionary<Type, ComponentSystem> _systems = new Dictionary<Type, ComponentSystem>();
        public Dictionary<Type, ComponentSystem> Systems { get => _systems; }

        private Entity _camera = null;
        public Entity Camera { get => _camera; }
        public List<Entity> Entities { get => getSystem<AllEntitySystem>().MemberEntities;}

        public ECSEngine()
        {
            AddSystem<AllEntitySystem>();
            AddSystem<ModelRenderSystem>();
            AddSystem<DirectionalLightSystem>();
            AddSystem<SpotLightSystem>();
            AddSystem<PointLightSystem>();
            AddSystem<SelfDestroySystem>();
            AddSystem<GravitySystem>();
            AddSystem<VelocitySystem>();
            AddSystem<ParticleEmitterSystem>();
            AddSystem<ParticleSystem>();
        }

        private void AddSystem<T>() where T : ComponentSystem, new()
        {
            _systems.Add(typeof(T), new T());
        }

        public void InitEntities()
        {
            Engine.PerformanceMonitor.clear();

            if (_camera == null)
            {
                _camera = new Entity("Camera");
                Camera.addComponent(new TransformationComponent(new Vector3(0, 2f, 0f), new Vector3(0), new Vector3(1)));
                Camera.addComponent(new ProjectionComponent(MathF.PI / 3.5f));
            }

            RenderEngine._debugRenderer.circles.Clear();
            RenderEngine._debugRenderer.rings.Clear();
            RenderEngine._debugRenderer.lines.Clear();

            TreeGenerator treeGenerator = new TreeGenerator();

            Entity groundPlane = new Entity("Terrain");
            groundPlane.addComponent(new TransformationComponent(new Vector3(-300, -50.0f, -300f), new Vector3(0), new Vector3(1f)));

            TerrainGridGenerator terrainGridGenerator = new TerrainGridGenerator();

            TaskTracker terrainGridTracker = Engine.PerformanceMonitor.startTask("terrain grid", true);
            Grid terrainGrid = terrainGridGenerator.generateChunk(new Vector2i(300, 300));
            Engine.PerformanceMonitor.finishTask(terrainGridTracker, true);

            TaskTracker terrainMeshTracker = Engine.PerformanceMonitor.startTask("terrain mesh", true);
            Mesh rawGround = TerrainMeshGenerator.GridToMesh(terrainGrid, out Vector3[,] terrainNormals);
            Engine.PerformanceMonitor.finishTask(terrainMeshTracker, true);


            TaskTracker steepnessTracker = Engine.PerformanceMonitor.startTask("terrain steepness map", true);
            Grid terrainSteepnessMap = new Grid(terrainGrid.Resolution);
            for (int z = 0; z < terrainSteepnessMap.Resolution.Y; z++)
            {
                for (int x = 0; x < terrainSteepnessMap.Resolution.X; x++)
                {
                    float value = Vector3.Dot(new Vector3(0f, 1f, 0f), terrainNormals[x, z]);
                    terrainSteepnessMap.Values[x, z] =MyMath.clamp01(MathF.Pow(value, 1.5f));
                }
            }
            Engine.PerformanceMonitor.finishTask(steepnessTracker, true);

            glModel groundModel = glLoader.loadToVAO(rawGround);
            groundPlane.addComponent(new FlatModelComponent(groundModel));
            AddEnityToSystem<ModelRenderSystem>(groundPlane);

            OpenSimplexNoise noise = new OpenSimplexNoise();

            Grid spawnGrid = new Grid(terrainGrid.Resolution);

            for (int z = 0; z < spawnGrid.Resolution.Y; z++)
            {
                for (int x = 0; x < spawnGrid.Resolution.X; x++)
                {
                    float value = noise.Evaluate(x * 0.01f, z * 0.01f) / 2f + 0.5f;
                    value *= 0.9f;
                    value += 0.1f;

                    float height = terrainGrid.Values[x, z];
                    value += height / 85f;

                    spawnGrid.Values[x, z] = MyMath.clamp01( value* value);
                }
            }
            DebugRenderer.texture = terrainSteepnessMap.GenerateTexture();
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
                tree.addComponent(new TransformationComponent(new Vector3(spawn.X, y-0.1f, spawn.Y), new Vector3(0, MyMath.rng(MathF.PI*2f), 0f), new Vector3(0.5f+MyMath.rng(0.5f))));
                tree.addComponent(new FlatModelComponent(mesh));
                AddEnityToSystem<ModelRenderSystem>(tree);
                tree.addComponent(new ChildComponent(groundPlane));
                tree.addComponent(new SelfDestroyComponent(1+MyMath.rng(2f)));
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
                scale += new Vector3(1f-terrainSteepnessMap.BilinearInterpolate(position.Xz))*1f;

                rock.addComponent(new TransformationComponent(position, MyMath.rng3D(MathF.PI*2f), scale));
                rock.addComponent(new FlatModelComponent(rockMesh));
                AddEnityToSystem<ModelRenderSystem>(rock);
                rock.addComponent(new ChildComponent(groundPlane));

                RenderEngine._debugRenderer.circles.Add(new Circle(spawn, 1f));
            }
            Engine.PerformanceMonitor.finishTask(placingTracker, true);


            glModel houseModel = ModelGenerator.GenerateHouse();

            for (int x = 0; x < 2; x++)
            {
                for (int z = 0; z < 2; z++)
                {
                    Entity house = new Entity("House");
                    house.addComponent(new TransformationComponent(new Vector3(45+55*x, 0, 45+55f * z), new Vector3(0,MyMath.rand.Next(8)*MathF.PI/4,0f), new Vector3(2f)));
                    house.addComponent(new FlatModelComponent(houseModel));
                    AddEnityToSystem<ModelRenderSystem>(house);

                    Entity rock = new Entity("rock");
                    rock.addComponent(new TransformationComponent(new Vector3(-2, 18, -2f), new Vector3(0), new Vector3(1)));
                    Mesh box2Rawmodel = MeshGenerator.generateBox(Modelling.Model.Material.ROCK);
                    box2Rawmodel.setMetalicness(0.05f);
                    glModel rockModel = glLoader.loadToVAO(box2Rawmodel);
                    rock.addComponent(new FlatModelComponent(rockModel));
                    rock.addComponent(new ChildComponent(house));
                    AddEnityToSystem<ModelRenderSystem>(rock);
                }
            }

            StreetGenerator streetGenerator = new StreetGenerator();

            Entity crossRoad = new Entity("crossroad");
            crossRoad.addComponent(new TransformationComponent(new Transformation()));
            crossRoad.addComponent(new FlatModelComponent(streetGenerator.GenerateCrossRoad()));
            AddEnityToSystem<ModelRenderSystem>(crossRoad);

            int nr = 0;
            for (int i = 2; i < 13; i++)
            {
                for (int j = 0; j < streetGenerator.lanes ; j++)
                {
                    Entity car = new Entity("car "+nr);
                    float x = streetGenerator.laneWdith * j+streetGenerator.laneWdith*(0.5f+MyMath.rngMinusPlus(0.15f));
                    float z = 13f * i+MyMath.rngMinusPlus(4f);
                    car.addComponent(new TransformationComponent(new Transformation(new Vector3(x, 0f, z), new Vector3(0f, MyMath.rngMinusPlus(0.03f), 0f), new Vector3(1.7f+MyMath.rngMinusPlus(0.2f)))));
                    car.addComponent(new FlatModelComponent(CarGenerator.GenerateCar(out Vector3 leftLight, out Vector3 rightLight, out Vector3 exhaustPos)));
                    AddEnityToSystem<ModelRenderSystem>(car);

                    Entity carLightLeft = new Entity("car light left");
                    carLightLeft.addComponent(new TransformationComponent(leftLight, new Vector3(MathF.PI / 2.2f, 0f, 0), new Vector3(1f)));
                    carLightLeft.addComponent(new AttunuationComponent(0.001f, 0.01f, 0.001f));
                    carLightLeft.addComponent(new ColourComponent(new Colour(1f, 0.8f, 0.6f, 1f)));
                    carLightLeft.addComponent(new ChildComponent(car));
                    AddEnityToSystem<SpotLightSystem>(carLightLeft);

                    Entity carLightRight = new Entity("car light right");
                    carLightRight.addComponent(new TransformationComponent(rightLight, new Vector3(MathF.PI / 2.2f, 0f, 0), new Vector3(1f)));
                    carLightRight.addComponent(new AttunuationComponent(0.001f, 0.01f, 0.001f));
                    carLightRight.addComponent(new ColourComponent(new Colour(1f, 0.8f, 0.6f, 1f)));
                    carLightRight.addComponent(new ChildComponent(car));
                    AddEnityToSystem<PointLightSystem>(carLightRight);

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
                    AddEnityToSystem<ParticleEmitterSystem>(emitter);

                    nr++;
                }
            }









            Mesh streetModel = streetGenerator.GenerateStreet(30, out float streetLength);
            for (int i = 0; i < 4; i++)
            {
                Entity street = new Entity("street");
                Vector3 position = (new Vector4(0, 0f, streetGenerator.TotalWidth*0.5f, 1f)*MyMath.createRotationMatrix(new Vector3(0f, i * (MathF.PI / 2f), 0f))).Xyz;
                Transformation transformation = new Transformation(position, new Vector3(0f, i * (MathF.PI / 2f), 0f), new Vector3(1));
                street.addComponent(new TransformationComponent(transformation));
                street.addComponent(new FlatModelComponent(streetModel));
                AddEnityToSystem<ModelRenderSystem>(street);
            }



            for (int side = -1; side <= 1; side += 2)
            {
                for (int i = 0; i < 10; i++)
                {
                    Entity streetLight = new Entity("Street Light" + i);
                    streetLight.addComponent(new TransformationComponent(new Vector3((streetGenerator.TotalWidth - streetGenerator.sideWalkWidth * 1.7f) * 0.5f * side, 0f, 50 + 30 * i), new Vector3(0f, MathF.PI / 2f + MathF.PI / 2f * side, 0f), new Vector3(1f, 1f, 1f)));
                    streetLight.addComponent(new FlatModelComponent(UrbanPropGenerator.GenerateStreetLight(out Vector3 lightPosition)));
                    AddEnityToSystem<ModelRenderSystem>(streetLight);

                    Entity glow = new Entity("Street Light" + i + " glow");
                    glow.addComponent(new TransformationComponent(lightPosition, new Vector3(0f), new Vector3(1f)));
                    glow.addComponent(new AttunuationComponent(0.001f, 0.001f, 0.001f));
                    glow.addComponent(new ColourComponent(new Colour(1f, 0.8f, 0.6f, 20f)));
                    glow.addComponent(new ChildComponent(streetLight));
                    AddEnityToSystem<SpotLightSystem>(glow);


                    Entity streetTree = new Entity("Street tree" + i);
                    streetTree.addComponent(new TransformationComponent(new Vector3((streetGenerator.TotalWidth - streetGenerator.sideWalkWidth * 1.7f) * 0.5f * side, 0f, 35 + 30 * i), new Vector3(0f, MathF.PI / 2f + MathF.PI / 2f * side, 0f), new Vector3(15f)));
                    streetTree.addComponent(new FlatModelComponent(treeGenerator.GenerateFractalTree(1)));
                    AddEnityToSystem<ModelRenderSystem>(streetTree);
                }
            }
            
            for (int i = 0; i < 8; i++)
            {
                Entity roadCone = new Entity("roadCone");
                roadCone.addComponent(new TransformationComponent(new Transformation(new Vector3(2 * i, 0f, 15f), new Vector3(0f, MathF.PI * 2f * MyMath.rng(), 0f), new Vector3(2))));
                roadCone.addComponent(new FlatModelComponent(UrbanPropGenerator.GenerateStreetCone()));
                AddEnityToSystem<ModelRenderSystem>(roadCone);

            }

            Entity sun = new Entity("Sun");
            Vector3 direction = new Vector3(-2f, 2f, 0.9f);
            Colour colour = new Colour(1f, 1f, 0.95f, 20.0f);
            sun.addComponent(new ColourComponent(colour));
            sun.addComponent(new DirectionComponent(direction));
            sun.addComponent(new AmbientLightComponent(0.01f));
            sun.addComponent(new CascadingShadowComponent(new Vector2i(1024, 1024) * 2, 4, 4000));
            AddEnityToSystem<DirectionalLightSystem>(sun);

            Entity sky = new Entity("Sky");
            Vector3 skyDirection = new Vector3(0.02f, 1f, 0.02f);
            Colour skyColour = SkyRenderer.SkyColour;
            sky.addComponent(new ColourComponent(skyColour));
            sky.addComponent(new DirectionComponent(skyDirection));
            sky.addComponent(new AmbientLightComponent(0.9f));
            sky.addComponent(new CascadingShadowComponent(new Vector2i(512, 512) * 2, 4, 4000));
            AddEnityToSystem<DirectionalLightSystem>(sky);

            Engine.PerformanceMonitor.StatusReportDump();
            Engine.PerformanceMonitor.clear();
        }
        public bool AddEnityToSystem<T>(Entity entity) where T : ComponentSystem
        {
            getSystem<T>().AddMember(entity);
            return true;
        }

        private void HandleInput()
        {
            float delta = Engine.Delta;
            var transformationComponent = Camera.getComponent<TransformationComponent>();
            Transformation transformation = transformationComponent.Transformation;
            WindowHandler windowHandler = Engine.WindowHandler;

            float moveAmount = 20f * delta;
            float turnAmount = 2.5f * delta;
            float mouseTurnAmount = 0.001f;

            if (windowHandler.IsKeyDown(Keys.LeftShift))
            {
                moveAmount *= 10f;
            }

            if (windowHandler.IsMouseButtonDown(MouseButton.Right))
            {
                transformation.addRotation(new Vector3(0f, mouseTurnAmount * windowHandler.MouseState.Delta.X, 0f));
                transformation.addRotation(new Vector3(mouseTurnAmount * windowHandler.MouseState.Delta.Y, 0, 0f));
                windowHandler.setMouseGrabbed(true);
                if (windowHandler.IsKeyDown(Keys.A))
                {
                    transformation.move(new Vector3(-moveAmount, 0f, 0f));
                }
                if (windowHandler.IsKeyDown(Keys.D))
                {
                    transformation.move(new Vector3(moveAmount, 0f, 0f));
                }
            }
            else
            {
                windowHandler.setMouseGrabbed(false);
                if (windowHandler.IsKeyDown(Keys.A))
                {
                    transformation.addRotation(new Vector3(0f, -turnAmount, 0f));
                }
                if (windowHandler.IsKeyDown(Keys.D))
                {
                    transformation.addRotation(new Vector3(0f, turnAmount, 0f));
                }
            }


            if (windowHandler.IsKeyDown(Keys.W))
            {
                transformation.move(new Vector3(0f, 0f, -moveAmount));
            }
            if (windowHandler.IsKeyDown(Keys.S))
            {
                transformation.move(new Vector3(0f, 0f, moveAmount));
            }
            if (windowHandler.IsKeyDown(Keys.Q))
            {
                transformation.translate(new Vector3(0f, -moveAmount, 0f));
            }
            if (windowHandler.IsKeyDown(Keys.E))
            {
                transformation.translate(new Vector3(0f, moveAmount, 0f));
            }
            if (windowHandler.IsKeyDown(Keys.R))
            {
                transformation.addRotation(new Vector3(-turnAmount, 0f, 0f));
            }
            if (windowHandler.IsKeyDown(Keys.F))
            {
                transformation.addRotation(new Vector3(turnAmount, 0f, 0f));
            }

            if (windowHandler.IsKeyPressed(Keys.F1))
            {
                ClearAllEntitiesExcept(Camera);
                InitEntities();
            }
            if (windowHandler.IsKeyPressed(Keys.F5))
            {
                foreach (Entity entity in Entities)
                {
                    Console.WriteLine(entity.GetFullInformationString());
                }
            }

            if (windowHandler.IsKeyPressed(Keys.F12))
            {
                Engine.PerformanceMonitor.StatusReportDump();
            }

            transformationComponent.Transformation = transformation;
        }

        private void ClearAllEntitiesExcept(params Entity[] exceptions)
        {
            var entities = Entities;
            for (int i = entities.Count - 1; i >= 0; i--)
            {
                Entity entity = entities[i];
                if (!exceptions.Contains(entity))
                {
                    entity.CleanUp();
                }
            }
        }

        public void update()
        {
            HandleInput();
            foreach (Entity entity in Entities)
            {
                entity.updateComponents();
            }

            foreach (ComponentSystem system in _systems.Values)
            {
                system.Update();
            }
        }

        public T getSystem<T>() where T : ComponentSystem
        {
            if (_systems.TryGetValue(typeof(T), out ComponentSystem system))
            {
                return (T)system;
            } else
            {
                return null;
            }
        }

        public  void OnResize(ResizeEventArgs eventArgs)
        {
            foreach (Entity entity in Entities)
            {
                entity.OnResize(eventArgs);
            }
        }
    }
}
