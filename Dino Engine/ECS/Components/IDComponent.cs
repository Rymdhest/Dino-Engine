using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Dino_Engine.ECS.Components
{
    public class IDComponent : Component
    {
        private static IDManager<short> allocator = new IDManager<short>();
        public short ID { get; private set; }
        public IDComponent()
        {
            ID = allocator.Allocate();
        }

        public override void CleanUp()
        {
            allocator.Release(ID);
        }
    }
}
