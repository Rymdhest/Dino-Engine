

using Dino_Engine.Modelling;

namespace Dino_Engine.ECS
{
    internal class FlatModelSystem : ComponentSystem
    {
        private Dictionary<glModel, List<Entity>> _models = new Dictionary<glModel, List<Entity>>();

        public Dictionary<glModel, List<Entity>> Models { get => _models; set => _models = value; }

        public override void AddMember(Component member)
        {
            glModel glModel = member.Owner.getComponent<FlatModelComponent>().GLModel;
            if (_models.ContainsKey(glModel)) {
                _models[glModel].Add(member.Owner);
            } else
            {
                _models.Add(glModel, new List<Entity>());
                _models[glModel].Add(member.Owner);
            }
            member.AddSubscribedSystem(this);
        }

        public override void RemoveMember(Component member)
        {
            FlatModelComponent model = (FlatModelComponent)member;
            glModel glmodel = model.Owner.getComponent<FlatModelComponent>().GLModel;

            _models[glmodel].Remove(member.Owner);

            if (_models[glmodel].Count == 0)
            {
                _models.Remove(glmodel);
            }
            member.RemoveSubscribedSystem(this);
        }
    }
}
