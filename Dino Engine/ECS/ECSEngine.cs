using Dino_Engine.Core;
using Dino_Engine.ECS.Components;
using Dino_Engine.ECS.Systems;
using Dino_Engine.Modelling;
using Dino_Engine.Modelling.Model;
using Dino_Engine.Modelling.Procedural;
using Dino_Engine.Rendering.Renderers.PostProcessing;
using Dino_Engine.Util;
using OpenTK.Mathematics;
using OpenTK.Windowing.Common;
using OpenTK.Windowing.GraphicsLibraryFramework;
using System;

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
                Camera.addComponent(new ProjectionComponent(MathF.PI / 2.5f));
            }

            Entity box = new Entity("Box");
            box.addComponent(new TransformationComponent(new Vector3(3, 0, -7f), new Vector3(0), new Vector3(1)));
            glModel boxModel = glLoader.loadToVAO(MeshGenerator.generateBox(new Vector3(-0.5f), new Vector3(0.5f), Material.SAND));
            box.addComponent(new FlatModelComponent(boxModel));
            AddEnityToSystem<FlatModelSystem>(box);

            Entity box2 = new Entity("Box2");
            box2.addComponent(new TransformationComponent(new Vector3(1, 3, -4f), new Vector3(0), new Vector3(1)));
            glModel boxModel2 = glLoader.loadToVAO(MeshGenerator.generateBox(new Vector3(-0.5f), new Vector3(0.5f), Material.METAL));
            box2.addComponent(new FlatModelComponent(boxModel2));
            AddEnityToSystem<FlatModelSystem>(box2);

            Entity rock = new Entity("rock");
            rock.addComponent(new TransformationComponent(new Vector3(-1, 1, -8f), new Vector3(0), new Vector3(1)));
            Mesh box2Rawmodel = IcoSphereGenerator.CreateIcosphere(3, Material.ROCK);
            box2Rawmodel.setMetalicness(0.05f);
            glModel rockModel = glLoader.loadToVAO(box2Rawmodel);
            rock.addComponent(new FlatModelComponent(rockModel));
            AddEnityToSystem<FlatModelSystem>(rock);

            Entity glow = new Entity("glow");
            glow.addComponent(new TransformationComponent(new Vector3(6, 2, -3f), new Vector3(0), new Vector3(0.2f)));
            Mesh glowRawmodel = IcoSphereGenerator.CreateIcosphere(1, Material.WOOD);
            glowRawmodel.setColour(new Colour(1f, 0.2f, 0.2f, 10f));
            glowRawmodel.setEmission(1f);
            glModel glowModel = glLoader.loadToVAO(glowRawmodel);
            glow.addComponent(new FlatModelComponent(glowModel));
            glow.addComponent(new AttunuationComponent(0.01f, 0.01f, 0.01f));
            glow.addComponent(new ColourComponent(new Colour(1f, 0.2f, 0.2f, 1f)));
            AddEnityToSystem<FlatModelSystem>(glow);
            AddEnityToSystem<PointLightSystem>(glow);

            Entity groundPlane = new Entity("Ground");
            groundPlane.addComponent(new TransformationComponent(new Vector3(0, 0, 0f), new Vector3(0), new Vector3(1)));
            float size = 125f;
            Mesh rawGRroud = MeshGenerator.generateBox(new Vector3(-size, -1, -size), new Vector3(size, 0, size), Material.LEAF);
            rawGRroud.setRoughness(0.5f);
            rawGRroud.setMetalicness(0.2f);
            glModel groundModel = glLoader.loadToVAO(rawGRroud);
            groundPlane.addComponent(new FlatModelComponent(groundModel));
            AddEnityToSystem<FlatModelSystem>(groundPlane);



            for (int i = 0; i<200; i++)
            {
                Entity tree = new Entity("Tree");
                tree.addComponent(new TransformationComponent(new Vector3(MyMath.rngMinusPlus(size), 0, MyMath.rngMinusPlus(size)), new Vector3(0), new Vector3(1)));
                float trunkRadius = 0.6f;
                float trunkHeight = 20.4f+ MyMath.rngMinusPlus(10);
                List<Vector3> trunkLayers = new List<Vector3>() {
                new Vector3(trunkRadius, 0f, trunkRadius*2f),
                new Vector3(trunkRadius, trunkHeight*0.33f, trunkRadius*0.9f),
                new Vector3(trunkRadius, trunkHeight*0.66f, trunkRadius*0.8f),
                new Vector3(trunkRadius, trunkHeight, trunkRadius*0.7f)};
                Mesh trunk = MeshGenerator.generateCylinder(trunkLayers, 7, Material.WOOD);
                tree.addComponent(new FlatModelComponent(glLoader.loadToVAO(trunk)));
                AddEnityToSystem<FlatModelSystem>(tree);
            }

            Entity sun = new Entity("Sun");
            Vector3 direction = new Vector3(-2f, 2f, 0.9f);
            Colour colour = new Colour(1f, 1f, 0.95f, 15.0f);
            sun.addComponent(new ColourComponent(colour));
            sun.addComponent(new DirectionComponent(direction));
            sun.addComponent(new AmbientLightComponent(0.0f));
            sun.addComponent(new CascadingShadowComponent(new Vector2i(1024, 1024)*2, 3, 500));
            AddEnityToSystem<DirectionalLightSystem>(sun);

            Entity sky = new Entity("Sky");
            Vector3 skyDirection = new Vector3(0.02f, 1f, 0.02f);
            Colour skyColour = SkyRenderer.SkyColour;
            sky.addComponent(new ColourComponent(skyColour));
            sky.addComponent(new DirectionComponent(skyDirection));
            sky.addComponent(new AmbientLightComponent(0.9f));
            sky.addComponent(new CascadingShadowComponent(new Vector2i(512, 512)*2, 1, 100));
            AddEnityToSystem<DirectionalLightSystem>(sky);

        }
        public bool AddEnityToSystem<T>(Entity entity) where T : ComponentSystem
        {
            getSystem<T>().AddMember(entity);
            return true;
        }

        private void HandleInput()
        {
            float delta = Engine.Delta;
            Transformation transformation = Camera.getComponent<TransformationComponent>().Transformation;
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
