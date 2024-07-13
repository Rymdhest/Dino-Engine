using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Dino_Engine.Core
{
    public abstract class Game
    {
        private Engine _engine;
        public Engine Engine { get => _engine; }
        public Game(Engine engine)
        {
            _engine = engine;
            engine.SetGame(this);
        }


        public abstract void update();
    }
}
