using Dino_Engine.Core;
using Dino_Engine.Modelling;
using Dino_Engine.Modelling.Model;

namespace Dino_Engine.ECS.ComponentsOLD
{
    public class ModelComponent : ComponentOLD
    {

        private glModel _glModel;
        private bool _isCleanGLModel;

        public glModel GLModel { get => _glModel; set => _glModel = value; }
        public ModelComponent(Mesh model, bool cleanGLModel = true)
        {
            _glModel = glLoader.loadToVAO(model);
            _isCleanGLModel = cleanGLModel;
        }
        public ModelComponent(glModel model, bool cleanGLModel = true)
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
            base.Update();
        }
    }
}
