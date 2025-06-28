using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Dino_Engine.ECS.ComponentsOLD
{
    public class ChildComponent : ComponentOLD
    {
        public EntityOLD _parent;
        public ChildComponent(EntityOLD parent)
        {
            _parent = parent;
        }

        public override void Initialize()
        {
            if (_parent.TryGetComponent(out ParentComponent parentComponent))
            {
                parentComponent.children.Add(Owner);
            }
            else
            {
                ParentComponent newParentComponent = new ParentComponent();
                newParentComponent.children.Add(Owner);
                _parent.addComponent(newParentComponent);

            }
        }

        public override void CleanUp()
        {
            _parent = null;
        }

    }
}
