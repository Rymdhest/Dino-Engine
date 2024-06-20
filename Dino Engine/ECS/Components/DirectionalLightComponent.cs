

using OpenTK.Mathematics;
using Dino_Engine.Core;
using Dino_Engine.Modelling;
using System.Drawing;
using Dino_Engine.Util;

namespace Dino_Engine.ECS
{
    public class DirectionalLightComponent : Component
    {

        private Colour _colour;
        private Vector3 direction;

        public Colour Colour { get => _colour; set => _colour = value; }
        public Vector3 Direction { get => direction; set => direction = value; }

        public DirectionalLightComponent(Vector3 direction, Colour colur)
        {
            this.Direction = direction.Normalized();
            this.Colour = colur;
        }

        public override void Initialize()
        {
            Engine.Instance.ECSEngine.getSystem<DirectionalLightSystem>().AddMember(this);
        }

        public override void CleanUp()
        {
            base.CleanUp();
        }
    }
}
