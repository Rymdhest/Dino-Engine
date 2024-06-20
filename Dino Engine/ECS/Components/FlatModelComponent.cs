


using Dino_Engine.Core;
using Dino_Engine.Modelling;

namespace Dino_Engine.ECS
{
    internal class FlatModelComponent : Component
    {

        private glModel _glModel;
        private bool _isCleanGLModel;

        public glModel GLModel { get => _glModel; set => _glModel = value; }

        public FlatModelComponent(glModel model, bool cleanGLModel = true)
        {
            _glModel = model;
            _isCleanGLModel = cleanGLModel;
        }
        public override void CleanUp()
        {
            base.CleanUp();
            if (_isCleanGLModel)
            {
                _glModel.cleanUp();
            }
            
        }

        public override void Update()
        {
            base.Update();    }
    }
}
