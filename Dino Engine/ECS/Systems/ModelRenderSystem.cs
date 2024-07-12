using Dino_Engine.Modelling.Model;

namespace Dino_Engine.ECS
{
    internal class ModelRenderSystem : ComponentSystem
    {
        private Dictionary<glModel, List<Entity>> _modelsDictionary = new Dictionary<glModel, List<Entity>>();

        public Dictionary<glModel, List<Entity>> ModelsDictionary { get => _modelsDictionary; set => _modelsDictionary = value; }

        public ModelRenderSystem() : base()
        {
            addRequiredComponent<TransformationComponent>();
            addRequiredComponent<ModelComponent>();
        } 

        public override void AddMember(Entity member)
        {
            base.AddMember(member);
            glModel glModel = member.getComponent<ModelComponent>().GLModel;
            if (_modelsDictionary.ContainsKey(glModel)) {
                _modelsDictionary[glModel].Add(member);
            } else
            {
                _modelsDictionary.Add(glModel, new List<Entity>());
                _modelsDictionary[glModel].Add(member);
            }
        }

        public override void RemoveMember(Entity member)
        {
            base.RemoveMember(member);

            glModel glmodel = member.getComponent<ModelComponent>().GLModel;
            _modelsDictionary[glmodel].Remove(member);

            if (_modelsDictionary[glmodel].Count == 0)
            {
                _modelsDictionary.Remove(glmodel);
            }
        }

        internal override void UpdateEntity(Entity entity)
        {
        }
    }
}
