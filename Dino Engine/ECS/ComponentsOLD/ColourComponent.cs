

using OpenTK.Mathematics;
using Dino_Engine.Core;
using Dino_Engine.Modelling;
using System.Drawing;
using Dino_Engine.Util;

namespace Dino_Engine.ECS.ComponentsOLD
{
    public class ColourComponent : ComponentOLD
    {

        private Colour _colour;

        public Colour Colour { get => _colour; set => _colour = value; }

        public ColourComponent(Colour colur)
        {
            Colour = colur;
        }
    }
}
