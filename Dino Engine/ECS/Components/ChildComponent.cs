using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Dino_Engine.ECS.Components
{
    internal class ChildComponent : Component
    {
        public Entity _parent;
        public ChildComponent(Entity parent)
        {
            _parent = parent;
        }

        public override void Initialize()
        {
            if (_parent.TryGetComponent<ParentComponent>(out ParentComponent parentComponent))
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
