using Dino_Engine.Core;
using Dino_Engine.ECS;
using Dino_Engine.ECS.ECS_Architecture;
using OpenTK.Windowing.Common;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Dino_Engine.Rendering.Renderers
{
    public abstract class CommandDrivenRenderer<T> : Renderer
    {
        private List<T> commands = new List<T>();

        public CommandDrivenRenderer() : base()
        {
           
        }

        internal override void Render(RenderEngine renderEngine)
        {
            foreach (var command in commands)
            {
                PerformCommand(command, renderEngine);
            }
            commands.Clear();
        }

        public abstract void PerformCommand(T command, RenderEngine renderEngine);

        public void SubmitCommand(T Command)
        {
            commands.Add(Command);
        }

    }
}
