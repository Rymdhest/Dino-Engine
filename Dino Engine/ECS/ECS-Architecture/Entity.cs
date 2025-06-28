using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Dino_Engine.ECS.ECS_Architecture
{
    public struct Entity
    {
        public uint Id;

        public Entity(uint Id)
        {
            this.Id  = Id;
        }
        public override string ToString() => $"Entity({Id})";
    }
}
