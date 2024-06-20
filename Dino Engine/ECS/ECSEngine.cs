using Dino_Engine.ECS.Components;
using Dino_Engine.Modelling;
using Dino_Engine.Util;
using OpenTK.Mathematics;
using OpenTK.Windowing.Common;
using OpenTK.Windowing.GraphicsLibraryFramework;

namespace Dino_Engine.ECS
{
    public class ECSEngine
    {

        private List<Entity> _entities = new List<Entity>();
        private Dictionary<Type, ComponentSystem> _systems = new Dictionary<Type, ComponentSystem>();
        public Dictionary<Type, ComponentSystem> Systems { get => _systems; }

        public Entity Camera { get; set; }
        public List<Entity> Entities { get => _entities;}

        public ECSEngine() {
            AddSystem<FlatModelSystem>();
            AddSystem<DirectionalLightSystem>();
        }

        private void AddSystem<T>() where T : ComponentSystem, new()
        {
            _systems.Add(typeof(T), new T());
        }

        public void Init()
        {
            Camera = new Entity("Camera");
            Camera.addComponent(new TransformationComponent(new Vector3(0, 2f, 0f), new Vector3(0), new Vector3(1)));
            Camera.addComponent(new ProjectionComponent(MathF.PI / 2.5f));

            Entity box = new Entity("Box");
            box.addComponent(new TransformationComponent(new Vector3(3, 0, -7f), new Vector3(0), new Vector3(1)));
            glModel boxModel = glLoader.loadToVAO(MeshGenerator.generateBox(new Vector3(-0.5f), new Vector3(0.5f)));
            box.addComponent(new FlatModelComponent(boxModel));
            AddEnityToSystem<FlatModelSystem>(box);

            Entity box2 = new Entity("Box2");
            box2.addComponent(new TransformationComponent(new Vector3(1, 3, -4f), new Vector3(0), new Vector3(1)));
            RawModel box2Rawmodel = MeshGenerator.generateBox(new Vector3(-0.5f), new Vector3(0.5f));
            box2Rawmodel.setColour(new Vector3(0.2f, 0.8f, 0.2f));
            glModel boxModel2 = glLoader.loadToVAO(box2Rawmodel);
            box2.addComponent(new FlatModelComponent(boxModel2));
            AddEnityToSystem<FlatModelSystem>(box2);

            Entity groundPlane = new Entity("Ground");
            groundPlane.addComponent(new TransformationComponent(new Vector3(0, 0, 0f), new Vector3(0), new Vector3(1)));
            float size = 25f;
            RawModel rawGRroud = MeshGenerator.generateBox(new Vector3(-size, -1, -size), new Vector3(size, 0, size));
            rawGRroud.setColour(new Vector3(0.7f));
            glModel groundModel = glLoader.loadToVAO(rawGRroud);
            groundPlane.addComponent(new FlatModelComponent(groundModel));
            AddEnityToSystem<FlatModelSystem>(groundPlane);

            Entity tree = new Entity("Tree");
            tree.addComponent(new TransformationComponent(new Vector3(-3, 0, -5f), new Vector3(0), new Vector3(1)));
            float trunkRadius = 0.6f;
            float trunkHeight = 3.4f;
            Vector3 trunkColor = new Vector3(0.55f, 0.39f, 0.18f);
            List<Vector3> trunkLayers = new List<Vector3>() {
            new Vector3(trunkRadius, 0f, trunkRadius*2f),
            new Vector3(trunkRadius, trunkHeight*0.33f, trunkRadius*0.9f),
            new Vector3(trunkRadius, trunkHeight*0.66f, trunkRadius*0.8f),
            new Vector3(trunkRadius, trunkHeight, trunkRadius*0.7f)};
            RawModel trunk = MeshGenerator.generateCylinder(trunkLayers, 7, trunkColor);
            tree.addComponent(new FlatModelComponent(glLoader.loadToVAO(trunk)));
            AddEnityToSystem<FlatModelSystem>(tree);

            Entity sun = new Entity("Sun");
            Vector3 direction = new Vector3(-1f, 2f, 0.9f);
            Colour colour = new Colour(1f, 1f, 1f, 3.4f);
            sun.addComponent(new ColourComponent(colour));
            sun.addComponent(new DirectionComponent(direction));
            sun.addComponent(new AmbientLightComponent(0.1f));
            sun.addComponent(new CascadingShadowComponent(new Vector2i(1024, 1024), 2, 50));
            AddEnityToSystem<DirectionalLightSystem>(sun);

            Entity sky = new Entity("Sky");
            Vector3 skyDirection = new Vector3(0f, 1f, 0.0f);
            Colour skyColour = new Colour(0.2f, 0.2f, 1f, 0.4f);
            sky.addComponent(new ColourComponent(skyColour));
            sky.addComponent(new DirectionComponent(skyDirection));
            sky.addComponent(new AmbientLightComponent(0.2f));
            sky.addComponent(new CascadingShadowComponent(new Vector2i(1024, 1024), 1, 50));
            AddEnityToSystem<DirectionalLightSystem>(sky);


        }
        public bool AddEnityToSystem<T>(Entity entity) where T : ComponentSystem
        {
            getSystem<T>().AddMember(entity);
            return true;
        }

        public void update()
        {
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
                entity.updateComponents();
            }
        }
    }
}
