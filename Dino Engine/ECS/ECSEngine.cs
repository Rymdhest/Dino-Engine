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
using OpenTK.Mathematics;
using OpenTK.Windowing.Common;
using OpenTK.Windowing.GraphicsLibraryFramework;
using System;
using System.IO;
using Util.Noise;

namespace Dino_Engine.ECS
{
    public class ECSEngine
    {

        private List<Entity> _entities = new List<Entity>();
        private Dictionary<Type, ComponentSystem> _systems = new Dictionary<Type, ComponentSystem>();
        public Dictionary<Type, ComponentSystem> Systems { get => _systems; }

        private Entity _camera = null;
        public Entity Camera { get => _camera; }
        public List<Entity> Entities { get => _entities;}

        public ECSEngine() {
            AddSystem<FlatModelSystem>();
            AddSystem<DirectionalLightSystem>();
            AddSystem<SpotLightSystem>();
            AddSystem<PointLightSystem>();
        }

        private void AddSystem<T>() where T : ComponentSystem, new()
        {
            _systems.Add(typeof(T), new T());
        }

        public void InitEntities()
        {
            if (_camera == null)
            {
                _camera = new Entity("Camera");
                Camera.addComponent(new TransformationComponent(new Vector3(0, 2f, 0f), new Vector3(0), new Vector3(1)));
                Camera.addComponent(new ProjectionComponent(MathF.PI / 3.5f));
            }

            TreeGenerator treeGenerator = new TreeGenerator();


            Entity groundPlane = new Entity("Terrain");
            groundPlane.addComponent(new TransformationComponent(new Vector3(0, 0.0f, 0f), new Vector3(0), new Vector3(5f)));

            TerrainGridGenerator terrainGridGenerator = new TerrainGridGenerator();
            Mesh rawGround = TerrainMeshGenerator.GridToMesh(terrainGridGenerator.generateChunk(new Vector2i(500, 500)));
            rawGround.setRoughness(0.55f);
            glModel groundModel = glLoader.loadToVAO(rawGround);
            groundPlane.addComponent(new FlatModelComponent(groundModel));
            AddEnityToSystem<FlatModelSystem>(groundPlane);



            Entity sun = new Entity("Sun");
            Vector3 direction = new Vector3(-2f, 2f, 0.9f);
            Colour colour = new Colour(1f, 1f, 0.95f, 30.0f);
            sun.addComponent(new ColourComponent(colour));
            sun.addComponent(new DirectionComponent(direction));
            sun.addComponent(new AmbientLightComponent(0.01f));
            sun.addComponent(new CascadingShadowComponent(new Vector2i(1024, 1024)*2, 4, 4000));
            AddEnityToSystem<DirectionalLightSystem>(sun);

            Entity sky = new Entity("Sky");
            Vector3 skyDirection = new Vector3(0.02f, 1f, 0.02f);
            Colour skyColour = SkyRenderer.SkyColour;
            sky.addComponent(new ColourComponent(skyColour));
            sky.addComponent(new DirectionComponent(skyDirection));
            sky.addComponent(new AmbientLightComponent(0.7f));
            sky.addComponent(new CascadingShadowComponent(new Vector2i(512, 512)*2, 4, 4000));
            AddEnityToSystem<DirectionalLightSystem>(sky);

            glModel houseModel = ModelGenerator.GenerateHouse();

                for (int x = 0; x < 2; x++)
            {
                for (int z = 0; z < 2; z++)
                {
                    Entity house = new Entity("House");
                    house.addComponent(new TransformationComponent(new Vector3(45+55*x, 0, 45+55f * z), new Vector3(0,MyMath.rand.Next(8)*MathF.PI/4,0f), new Vector3(2f)));
                    house.addComponent(new FlatModelComponent(houseModel));
                    AddEnityToSystem<FlatModelSystem>(house);

                    Entity rock = new Entity("rock");
                    rock.addComponent(new TransformationComponent(new Vector3(-2, 18, -2f), new Vector3(0), new Vector3(1)));
                    Mesh box2Rawmodel = MeshGenerator.generateBox(Material.ROCK);
                    box2Rawmodel.setMetalicness(0.05f);
                    glModel rockModel = glLoader.loadToVAO(box2Rawmodel);
                    rock.addComponent(new FlatModelComponent(rockModel));
                    rock.addComponent(new ChildComponent(house));
                    AddEnityToSystem<FlatModelSystem>(rock);
                }
            }

            StreetGenerator streetGenerator = new StreetGenerator();

            Entity crossRoad = new Entity("crossroad");
            crossRoad.addComponent(new TransformationComponent(new Transformation()));
            crossRoad.addComponent(new FlatModelComponent(streetGenerator.GenerateCrossRoad()));
            AddEnityToSystem<FlatModelSystem>(crossRoad);

            int nr = 0;
            for (int i = 2; i < 1; i++)
            {
                for (int j = 0; j < streetGenerator.lanes ; j++)
                {
                    Entity car = new Entity("car "+nr);
                    float x = streetGenerator.laneWdith * j+streetGenerator.laneWdith*(0.5f+MyMath.rngMinusPlus(0.15f));
                    float z = 13f * i+MyMath.rngMinusPlus(4f);
                    car.addComponent(new TransformationComponent(new Transformation(new Vector3(x, 0f, z), new Vector3(0f, MyMath.rngMinusPlus(0.03f), 0f), new Vector3(1.7f+MyMath.rngMinusPlus(0.2f)))));
                    car.addComponent(new FlatModelComponent(CarGenerator.GenerateCar(out Vector3 leftLight, out Vector3 rightLight)));
                    AddEnityToSystem<FlatModelSystem>(car);

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
                    AddEnityToSystem<SpotLightSystem>(carLightRight);
                    nr++;
                }
            }




            for (int i = 0; i <8; i++)
            {
                Entity roadCone = new Entity("roadCone");
                roadCone.addComponent(new TransformationComponent(new Transformation(new Vector3(2*i, 0f, 15f), new Vector3(0f, MathF.PI * 2f * MyMath.rng(), 0f), new Vector3(2))));
                roadCone.addComponent(new FlatModelComponent(UrbanPropGenerator.GenerateStreetCone()));
                AddEnityToSystem<FlatModelSystem>(roadCone);
            }




            Mesh streetModel = streetGenerator.GenerateStreet(30, out float streetLength);
            for (int i = 0; i < 4; i++)
            {
                Entity street = new Entity("street");
                Vector3 position = (new Vector4(0, 0f, streetGenerator.TotalWidth*0.5f, 1f)*MyMath.createRotationMatrix(new Vector3(0f, i * (MathF.PI / 2f), 0f))).Xyz;
                Transformation transformation = new Transformation(position, new Vector3(0f, i * (MathF.PI / 2f), 0f), new Vector3(1));
                street.addComponent(new TransformationComponent(transformation));
                street.addComponent(new FlatModelComponent(streetModel));
                AddEnityToSystem<FlatModelSystem>(street);
            }



            for (int side = -1; side <= 1; side += 2)
            {
                for (int i = 0; i < 10; i++)
                {
                    Entity streetLight = new Entity("Street Light" + i);
                    streetLight.addComponent(new TransformationComponent(new Vector3((streetGenerator.TotalWidth - streetGenerator.sideWalkWidth * 1.7f) * 0.5f * side, 0f, 50 + 30 * i), new Vector3(0f, MathF.PI / 2f + MathF.PI / 2f * side, 0f), new Vector3(1f, 1f, 1f)));
                    streetLight.addComponent(new FlatModelComponent(UrbanPropGenerator.GenerateStreetLight(out Vector3 lightPosition)));
                    AddEnityToSystem<FlatModelSystem>(streetLight);

                    Entity glow = new Entity("Street Light" + i + " glow");
                    glow.addComponent(new TransformationComponent(lightPosition, new Vector3(0f), new Vector3(1f)));
                    glow.addComponent(new AttunuationComponent(0.001f, 0.001f, 0.001f));
                    glow.addComponent(new ColourComponent(new Colour(1f, 0.8f, 0.6f, 20f)));
                    glow.addComponent(new ChildComponent(streetLight));
                    AddEnityToSystem<SpotLightSystem>(glow);


                    Entity streetTree = new Entity("Street tree" + i);
                    streetTree.addComponent(new TransformationComponent(new Vector3((streetGenerator.TotalWidth - streetGenerator.sideWalkWidth * 1.7f) * 0.5f * side, 0f, 35 + 30 * i), new Vector3(0f, MathF.PI / 2f + MathF.PI / 2f * side, 0f), new Vector3(15f)));
                    streetTree.addComponent(new FlatModelComponent(treeGenerator.GenerateFractalTree(1)));
                    AddEnityToSystem<FlatModelSystem>(streetTree);
                }
            }




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
                foreach (Entity entity in _entities)
                {
                    Console.WriteLine(entity.GetFullInformationString());
                }
            }

            transformationComponent.Transformation = transformation;
        }

        private void ClearAllEntitiesExcept(params Entity[] exceptions)
        {
            Console.WriteLine(""+exceptions.Length);
            foreach(Entity entity in _entities)
            {
                if (!exceptions.Contains(entity))
                {
                    entity.CleanUp();
                }
            }
            _entities.Clear();
            _entities.AddRange(exceptions);
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
