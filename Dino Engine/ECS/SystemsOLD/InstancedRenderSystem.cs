using Dino_Engine.ECS.ComponentsOLD;
using Dino_Engine.Modelling.Model;

namespace Dino_Engine.ECS.SystemsOLD
{
    public class InstancedModelSystem : ComponentSystem
    {
        private Dictionary<glModel, List<EntityOLD>> _modelsDictionary = new Dictionary<glModel, List<EntityOLD>>();

        public Dictionary<glModel, List<EntityOLD>> ModelsDictionary { get => _modelsDictionary; set => _modelsDictionary = value; }

        public InstancedModelSystem() : base()
        {
            addRequiredComponent<TransformationComponent>();
            addRequiredComponent<ModelComponent>();
        }

        public override void AddMember(EntityOLD member)
        {
            base.AddMember(member);
            glModel glModel = member.getComponent<ModelComponent>().GLModel;
            if (_modelsDictionary.ContainsKey(glModel))
            {
                _modelsDictionary[glModel].Add(member);
            }
            else
            {
                _modelsDictionary.Add(glModel, new List<EntityOLD>());
                _modelsDictionary[glModel].Add(member);
            }
        }

        public override void RemoveMember(EntityOLD member)
        {
            base.RemoveMember(member);

            glModel glmodel = member.getComponent<ModelComponent>().GLModel;
            _modelsDictionary[glmodel].Remove(member);

            if (_modelsDictionary[glmodel].Count == 0)
            {
                _modelsDictionary.Remove(glmodel);
            }
        }

        internal override void UpdateEntity(EntityOLD entity)
        {
        }
    }
}
