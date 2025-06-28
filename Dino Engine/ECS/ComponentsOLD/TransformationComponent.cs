using Dino_Engine.Util;
using OpenTK.Mathematics;

namespace Dino_Engine.ECS.ComponentsOLD
{
    public class TransformationComponent : ComponentOLD
    {
        private Transformation _transformation;
        public Transformation Transformation { get => getParentedWorldTransformation(); set => _transformation = value; }

        public TransformationComponent(Vector3 position, Vector3 rotation, Vector3 scale)
        {
            _transformation = new Transformation(position, rotation, scale);
        }
        public TransformationComponent(Transformation transformation)
        {
            _transformation = transformation;

        }

        public Transformation getParentedWorldTransformation()
        {
            if (Owner.TryGetComponent(out ChildComponent childComponent))
            {
                return _transformation * childComponent._parent.getComponent<TransformationComponent>().Transformation;
            }
            else
            {
                return _transformation;
            }
        }

        public void SetLocalTransformation(Vector3 pos)
        {
            _transformation.position = pos;
        }
    }
}
