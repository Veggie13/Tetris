using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Tetris
{
    class StandardScoreEngine : Game.ScoreEngine
    {
        public int LinesFilled(int lines)
        {
            switch (lines)
            {
                default:
                case 0: return 0;
                case 1: return 10;
                case 2: return 30;
                case 3: return 60;
                case 4: return 100;
            }
        }
    }
}
