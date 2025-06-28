using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Dino_Engine.ECS.ComponentsOLD
{
    public class IDComponent : ComponentOLD
    {
        private static IDAllocator<short> allocator = new IDAllocator<short>();
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
