using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Dino_Engine.ECS.Components
{
    public class ParentComponent : Component
    {
        public List<Entity> children = new List<Entity>();
        public ParentComponent()
        {

        }

        public virtual void CleanUp()
        {
            children.Clear();
        }
    }
}
