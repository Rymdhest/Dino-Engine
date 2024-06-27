using OpenTK.Windowing.Common;
using System.Text;
using System.Reflection;
using System.Collections.Generic;

namespace Dino_Engine.ECS
{
    public abstract class Component
    {
        private Entity _owner;
        public Entity Owner { get => _owner; set => _owner = value; }

        public virtual void Initialize() { }
        public virtual void OnResize(ResizeEventArgs eventArgs)  { }
        public virtual void Update() { }
        public virtual void CleanUp() { }

        public string getInformationString(string linePrefix = "")
        {
            Type type = this.GetType();
            Type baseType = typeof(Component);
            StringBuilder stringBuilder = new StringBuilder();
            stringBuilder.AppendLine($"{linePrefix}Component: {type.Name}");
            FieldInfo[] fields = type.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            foreach (FieldInfo field in fields)
            {
                if (field.DeclaringType != baseType)
                {
                    string name = field.Name;
                    object value = field.GetValue(this);
                    string valueString = "null";


                    Type valueType = value.GetType();
                    if (valueType.IsGenericType && valueType.GetGenericTypeDefinition() == typeof(List<>))
                    {
                        Type elementType = valueType.GetGenericArguments()[0];
                        int count = (int)valueType.GetProperty("Count").GetValue(value);
                        valueString = $"List[{elementType.Name}] (Size: {count})";

                        var list = value as System.Collections.IEnumerable;
                        int index = 0;
                        foreach (var item in list)
                        {
                            string itemString = item != null ? item.ToString() : "null";

                            string[] lines2 = itemString.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
                            string formattedValue = "";
                            foreach (string line in lines2)
                            {
                                formattedValue += ($"\n{linePrefix}\t{line}");
                            }

                            valueString += $"\n{linePrefix}{name}[{index}]: {formattedValue}";
                            index++;
                        }
                    } else
                    {
                        valueString = value.ToString();
                    }



                    stringBuilder.AppendLine($"{linePrefix}\t{name}");

                    string[] lines = valueString.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
                    foreach (string line in lines)
                    {
                        stringBuilder.AppendLine($"{linePrefix}\t{line}");
                    }
                }
            }
            return stringBuilder.ToString();
        }
    }
}
    