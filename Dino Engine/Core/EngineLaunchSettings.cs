using OpenTK.Mathematics;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Dino_Engine.Core
{
    public class EngineLaunchSettings
    {

        public string _gameTitle;
        //public Vector2i _resolution = new Vector2i(1900, 1080);
        public Vector2i _resolution = new Vector2i(2560, 1440);
        /// <summary>Runs with no window on screen and never takes the focus, for a scripted run beside someone at work or at play.</summary>
        public bool _hidden;

        public EngineLaunchSettings(string gameTitle) 
        {
            _gameTitle = gameTitle;
        }
    }
}
