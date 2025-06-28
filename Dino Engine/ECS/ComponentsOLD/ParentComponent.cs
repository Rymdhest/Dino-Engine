using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Dino_Engine.ECS.ComponentsOLD
{
    public class ParentComponent : ComponentOLD
    {
        public List<EntityOLD> children = new List<EntityOLD>();
        public ParentComponent()
        {

        }

        public virtual void CleanUp()
        {
            children.Clear();
        }
    }
}
