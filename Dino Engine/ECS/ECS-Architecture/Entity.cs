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

        public static readonly Entity Invalid = new Entity(0);

        public Entity(uint Id)
        {
            this.Id  = Id;
        }

        public bool IsValid()
        {
            return Id > 0;
        }

        public override string ToString() => $"Entity({Id})";
    }
}
