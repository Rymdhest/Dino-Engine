
namespace Dino_Engine.ECS.ECS_Architecture
{
    public class ComponentArray<T> : IComponentArray where T : struct, IComponent
    {
        private List<T> data = new();

        public void AddRaw(object component) => data.Add((T)component);
        public T Get(int index) => data[index];
        public void Set(int index, T value) => data[index] = value;

        public void RemoveAt(int index)
        {
            int last = data.Count - 1;
            if (index != last)
                data[index] = data[last];
            data.RemoveAt(last);
        }
    }
}
