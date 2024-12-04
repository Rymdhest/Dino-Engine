using OpenTK.Mathematics;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Dino_Engine.Modelling.Model
{
    public class vIndex
    {
        public int index;
        public int uvVariant;

        public vIndex(int index, int uvVariant = 0)        {
            this.index = index;
            this.uvVariant = uvVariant;
        } 
    }
}
