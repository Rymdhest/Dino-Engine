using Dino_Engine.Modelling;
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
            Systems.Add(typeof(FlatModelSystem), new FlatModelSystem());

        }

        public void Init()
        {
            Camera = new Entity();
            Camera.addComponent(new TransformationComponent(new Vector3(0), new Vector3(0), new Vector3(1)));
            Camera.addComponent(new ProjectionComponent(75f));

            Entity box = new Entity();
            box.addComponent(new TransformationComponent(new Vector3(0, 0, -5f), new Vector3(0), new Vector3(1)));
            glModel boxModel = glLoader.loadToVAO(MeshGenerator.generateBox(new Vector3(-0.5f), new Vector3(0.5f)));
            box.addComponent(new FlatModelComponent(boxModel));
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
