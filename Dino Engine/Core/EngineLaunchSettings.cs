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
        /// <summary>The most frames a second, 0 for no limit. High enough to be out of the way, and low enough that a menu doesn't run a graphics card flat out for nothing.</summary>
        public int _frameRateLimit = 240;
        /// <summary>Runs with no window on screen and never takes the focus, for a scripted run beside someone at work or at play.</summary>
        public bool _hidden;

        public EngineLaunchSettings(string gameTitle) 
        {
            _gameTitle = gameTitle;
        }
    }
}
