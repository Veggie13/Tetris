using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using FlexDraw;

namespace Tetris
{
    public partial class Form1 : Form, IDisposable
    {
        Game _game = new Game();
        GameBoard _board;
        GCViewport _viewport = new GCViewport();

        public Form1()
        {
            InitializeComponent();
            
            _game.Scorer = new StandardScoreEngine();

            this.KeyDown += new KeyEventHandler(Form1_KeyDown);
            this.KeyPress += new KeyPressEventHandler(Form1_KeyPress);
            this.FormClosing += new FormClosingEventHandler(Form1_FormClosing);
            
            _board = new GameBoard(_game);
        }

        void Form1_KeyPress(object sender, KeyPressEventArgs e)
        {
            switch (e.KeyChar)
            {
                case 'A':
                case 'a':
                    _game.Control.Rotate(false);
                    break;
                case 'D':
                case 'd':
                    _game.Control.Rotate(true);
                    break;
                case ' ':
                    _game.Control.Pause();
                    break;
            }
        }

        void Form1_FormClosing(object sender, FormClosingEventArgs e)
        {
            _game.Stop();
        }

        void Form1_KeyDown(object sender, KeyEventArgs e)
        {
            switch (e.KeyCode)
            {
                case Keys.Left:
                    _game.Control.Left();
                    break;
                case Keys.Right:
                    _game.Control.Right();
                    break;
                case Keys.Down:
                    _game.Control.Down();
                    break;
            }
        }

        void Events_Tick()
        {
            if (InvokeRequired)
            {
                BeginInvoke(new Action(Events_Tick));
                return;
            }
            _game.LockedTask(() =>
            {
                textBox1.Text = render();
                label1.Text = string.Format("Level: {0}", _game.Info.Level);
                label2.Text = string.Format("Score: {0}", _game.Info.Score);
            });
        }

        void Events_Crash()
        {
            if (InvokeRequired)
            {
                BeginInvoke(new Action(Events_Crash));
                return;
            }
            _game.Stop();
            MessageBox.Show("Crash!");
        }

        private void Form1_Load(object sender, EventArgs e)
        {
            _game.Run(0, 10, 20);
            _game.Events.Crash += new Action(Events_Crash);
            _game.Events.Tick += new Action(Events_Tick);
        }

        private string render()
        {
            string result = "";

            for (int row = 0; row < _game.Info.Height; row++)
            {
                result += "|";
                for (int col = 0; col < _game.Info.Width; col++)
                    result += (_game.Info[row, col] == 0) ? " " : "X";
                result += "|\r\n";
            }

            int[,] render = Game.GetPieceRender(_game.Info.CurrentPiece, _game.Info.CurrentPiecePosition);
            for (int rr = 0; rr < render.GetLength(0); rr++)
                for (int cc = 0; cc < render.GetLength(1); cc++)
                {
                    if (render[rr, cc] == 0)
                        continue;

                    int index = (_game.Info.CurrentPieceTopRow + rr) * (_game.Info.Width + 4)
                        + _game.Info.CurrentPieceLeftCol + cc + 1;
                    result = result.Substring(0, index) + "X" + result.Substring(index + 1);
                }

            result += "------------";

            return result;
        }

        void IDisposable.Dispose()
        {
            _game.Stop();
        }
    }
}
