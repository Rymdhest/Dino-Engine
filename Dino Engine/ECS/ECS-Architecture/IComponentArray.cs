
namespace Dino_Engine.ECS.ECS_Architecture
{
    public interface IComponentArray
    {
       void AddRaw(object component);
        void RemoveAt(int index);
    }
}
