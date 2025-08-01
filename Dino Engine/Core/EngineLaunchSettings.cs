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
        public Vector2i _resolution = new Vector2i(1900, 1080);

        public EngineLaunchSettings(string gameTitle) 
        {
            _gameTitle = gameTitle;
        }
    }
}
