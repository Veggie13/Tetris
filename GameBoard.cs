using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using FlexDraw;
using System.Drawing;
using System.Windows.Forms;

namespace Tetris
{
    class GameBoard : IDrawable
    {
        private Game _game;

        private static Color[] _pieceMainColors = new Color[Game.PiecesCount];
        private static Color[] _pieceLightColors = new Color[Game.PiecesCount];
        private static Color[] _pieceDarkColors = new Color[Game.PiecesCount];

        static GameBoard()
        {
            _pieceMainColors[0] = Color.Red;
            _pieceMainColors[1] = Color.Orange;
            _pieceMainColors[2] = Color.Yellow;
            _pieceMainColors[3] = Color.Green;
            _pieceMainColors[4] = Color.Blue;
            _pieceMainColors[5] = Color.Violet;

            for (int i = 0; i < Game.PiecesCount; i++)
            {
                _pieceLightColors[i] = ControlPaint.Light(_pieceMainColors[i]);
                _pieceDarkColors[i] = ControlPaint.Dark(_pieceMainColors[i]);
            }
        }

        public GameBoard(Game game)
        {
            _game = game;
        }

        public void Draw(IDrawAPI api)
        {
            drawBlock(api, 0, new PointD(0, 0));
        }

        public bool Visible
        {
            get { return true; }
        }

        public PointD Origin
        {
            get { return new PointD(0, 0); }
        }

        public RectangleD Bounds
        {
            get { return new RectangleD(0, 0, _game.Info.Width + 1, _game.Info.Height + 1); }
        }

        public RectangleD LastBounds
        {
            get { return Bounds; }
        }

        public event DrawableModifiedEvent Modified;

        private void drawBlock(IDrawAPI api, int color, PointD topLeft)
        {
            api.FillRectangle(new RectangleD(topLeft, topLeft.Offset(new PointD(1, 1))),
                _pieceLightColors[color - 1]);
            api.FillPolygon(new PointD[] {
                new PointD(1, 0),
                new PointD(0, 1),
                new PointD(1, 1)
            }, _pieceDarkColors[color - 1]);
            api.FillRectangle(new RectangleD(
                topLeft.Offset(new PointD(0.25, 0.25)),
                topLeft.Offset(new PointD(0.75, 0.75))), _pieceMainColors[color - 1]);
        }
    }
}
