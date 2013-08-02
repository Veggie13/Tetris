using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;

namespace Tetris
{
    class Game
    {
        public enum Piece { Box = 1, S, Z, T, L, J, Line }
        private static Dictionary<Piece, int[,]> PiecesZero = new Dictionary<Piece, int[,]>();
        private static int s_piecesCount;

        public static int PiecesCount { get { return s_piecesCount; } }

        public interface Controller
        {
            void Left();
            void Right();
            void Down();
            void Pause();
            void Rotate(bool clockwise);
        }

        public interface View
        {
            int Level { get; }
            int LineCount { get; }
            int Score { get; }
            int Width { get; }
            int Height { get; }
            int this[int row, int col] { get; }
            Piece CurrentPiece { get; }
            int CurrentPieceTopRow { get; }
            int CurrentPieceLeftCol { get; }
            int CurrentPiecePosition { get; }
            Piece NextPiece { get; }
        }

        public interface ScoreEngine
        {
            int LinesFilled(int lines);
        }

        public interface EventEngine
        {
            event Action<IEnumerable<int>> LinesFilled;
            event Action Tick;
            event Action PiecePlaced;
            event Action<bool> PieceRotated;    // arg: true if CW, else CCW
            event Action<bool> PieceMoved;      // arg: true if right, else left
            event Action PieceDropping;
            event Action<bool> PauseToggled;    // arg: true if has become paused
            event Action Crash;
        }

        static Game()
        {
            PiecesZero.Add(Piece.Box, new int[,]
                {{1, 1},
                 {1, 1}});
            PiecesZero.Add(Piece.J, new int[,]
                {{0, 1},
                 {0, 1},
                 {1, 1}});
            PiecesZero.Add(Piece.L, new int[,]
                {{1, 0},
                 {1, 0},
                 {1, 1}});
            PiecesZero.Add(Piece.Line, new int[,]
                {{1},
                 {1},
                 {1},
                 {1}});
            PiecesZero.Add(Piece.S, new int[,]
                {{1, 0},
                 {1, 1},
                 {0, 1}});
            PiecesZero.Add(Piece.T, new int[,]
                {{1, 0},
                 {1, 1},
                 {1, 0}});
            PiecesZero.Add(Piece.Z, new int[,]
                {{0, 1},
                 {1, 1},
                 {1, 0}});

            s_piecesCount = PiecesZero.Count;
        }

        private static int[,] rotatePosition(int[,] input, int position)
        {
            int[,] result = null;
            switch (position % 4)
            {
                default:
                case 0:
                    result = new int[input.GetLength(0), input.GetLength(1)];
                    for (int r = 0; r < result.GetLength(0); r++)
                        for (int c = 0; c < result.GetLength(1); c++)
                            result[r, c] = input[r, c];
                    break;
                case 1:
                    result = new int[input.GetLength(1), input.GetLength(0)];
                    for (int r = 0; r < result.GetLength(0); r++)
                        for (int c = 0; c < result.GetLength(1); c++)
                            result[r, c] = input[c, result.GetLength(0) - r - 1];
                    break;
                case 2:
                    result = new int[input.GetLength(0), input.GetLength(1)];
                    for (int r = 0; r < result.GetLength(0); r++)
                        for (int c = 0; c < result.GetLength(1); c++)
                            result[r, c] = input[result.GetLength(0) - r - 1, result.GetLength(1) - c - 1];
                    break;
                case 3:
                    result = new int[input.GetLength(1), input.GetLength(0)];
                    for (int r = 0; r < result.GetLength(0); r++)
                        for (int c = 0; c < result.GetLength(1); c++)
                            result[r, c] = input[result.GetLength(1) - c - 1, r];
                    break;
            }

            return result;
        }

        public static int[,] GetPieceRender(Piece p, int position)
        {
            int[,] result = rotatePosition(PiecesZero[p], position);
            for (int r = 0; r < result.GetLength(0); r++)
                for (int c = 0; c < result.GetLength(1); c++)
                    result[r, c] *= (int)p;
            return result;
        }

        private class Implementation : Controller, View, EventEngine
        {
            #region Private Members
            private static Random Rand = new Random();

            private Game _game;
            private object _lock = new object();
            private bool _paused = false;
            private bool _resting = false;
            #endregion

            public Implementation(Game game, int level, int width, int height)
            {
                _game = game;
                _level = level;
                _width = width;
                _height = height;
                _board = new List<int[]>();
                for (int row = 0; row < height; row++)
                {
                    _board.Add(new int[width]);
                }
                
                nextPiece();
                PushPiece();
            }

            #region Controller
            private bool _left = false;
            public void Left()
            {
                lock (_lock)
                {
                    if (_paused)
                        return;
                    if (_right)
                        _right = false;
                    else
                        _left = true;
                }
            }

            private bool _right = false;
            public void Right()
            {
                lock (_lock)
                {
                    if (_paused)
                        return;
                    if (_left)
                        _left = false;
                    else
                        _right = true;
                }
            }

            private bool _down = false;
            public void Down()
            {
                lock (_lock)
                {
                    if (_paused)
                        return;
                    _down = true;
                }
            }

            private bool _pauseToggling = false;
            public void Pause()
            {
                lock (_lock)
                {
                    _pauseToggling = true;
                }
            }

            private bool _rotatingCW = false;
            private bool _rotatingCCW = false;
            public void Rotate(bool clockwise)
            {
                lock (_lock)
                {
                    if (_paused)
                        return;
                    if (clockwise)
                    {
                        if (_rotatingCCW)
                            _rotatingCCW = false;
                        else
                            _rotatingCW = true;
                    }
                    else
                    {
                        if (_rotatingCW)
                            _rotatingCW = false;
                        else
                            _rotatingCCW = true;
                    }
                }
            }
            #endregion

            #region View
            private int _lineCount = 0;
            public int LineCount
            {
                get { return _lineCount; }
            }

            private int _level;
            public int Level
            {
                get { return _level; }
            }

            private int _score = 0;
            public int Score
            {
                get { return _score; }
            }

            private int _width;
            public int Width
            {
                get { return _width; }
            }

            private int _height;
            public int Height
            {
                get { return _height; }
            }

            private List<int[]> _board;
            public int this[int row, int col]
            {
                get { return _board[row][col]; }
            }

            private Piece _curPiece;
            public Piece CurrentPiece
            {
                get { return _curPiece; }
            }

            private int _curRow;
            public int CurrentPieceTopRow
            {
                get { return _curRow; }
            }

            private int _curCol;
            public int CurrentPieceLeftCol
            {
                get { return _curCol; }
            }

            private int _curPos;
            public int CurrentPiecePosition
            {
                get { return _curPos; }
            }

            private Piece _nextPiece;
            public Piece NextPiece
            {
                get { return _nextPiece; }
            }
            #endregion

            #region EventEngine
            public event Action<IEnumerable<int>> LinesFilled;
            public void EmitLinesFilled(IEnumerable<int> lines)
            {
                if (LinesFilled != null)
                    LinesFilled(lines);
            }

            public event Action Tick;
            public void EmitTick()
            {
                if (Tick != null)
                    Tick();
            }

            public event Action PiecePlaced;
            public void EmitPiecePlaced()
            {
                if (PiecePlaced != null)
                    PiecePlaced();
            }

            public event Action<bool> PieceRotated;
            public void EmitPieceRotated(bool cw)
            {
                if (PieceRotated != null)
                    PieceRotated(cw);
            }

            public event Action<bool> PieceMoved;
            public void EmitPieceMoved(bool right)
            {
                if (PieceMoved != null)
                    PieceMoved(right);
            }

            public event Action PieceDropping;
            public void EmitPieceDropping()
            {
                if (PieceDropping != null)
                    PieceDropping();
            }

            public event Action<bool> PauseToggled;
            public void EmitPauseToggled(bool paused)
            {
                if (PauseToggled != null)
                    PauseToggled(paused);
            }

            public event Action Crash;
            public void EmitCrash()
            {
                if (Crash != null)
                    Crash();
            }
            #endregion

            #region Public Methods
            public bool PushPiece()
            {
                _curPiece = _nextPiece;
                nextPiece();

                _curPos = 1;
                _curRow = 0;
                _curCol = (_width / 2) - (PiecesZero[_curPiece].GetLength(0) / 2);

                return !intersect(GetPieceRender(_curPiece, _curPos), _curRow, _curCol);
            }

            public bool PieceResting()
            {
                int[,] render = GetPieceRender(_curPiece, _curPos);
                for (int c = 0; c < render.GetLength(1); c++)
                {
                    int r;
                    for (r = render.GetLength(0) - 1; render[r, c] == 0; r--) ;
                    int tr = _curRow + r, tc = _curCol + c;
                    if (tr == _height - 1 || _board[tr + 1][tc] != 0)
                        return true;
                }

                return false;
            }

            public void Update()
            {
                lock (_lock)
                {
                    int[,] render = GetPieceRender(_curPiece, _curPos);
                    if (_left)
                    {
                        attemptMove(0, -1);
                        EmitPieceMoved(false);
                        _left = false;
                    }
                    if (_right)
                    {
                        attemptMove(0, 1);
                        EmitPieceMoved(true);
                        _right = false;
                    }
                    if (_rotatingCCW)
                    {
                        attemptRotate(false);
                        EmitPieceRotated(false);
                        _rotatingCCW = false;
                    }
                    if (_rotatingCW)
                    {
                        attemptRotate(true);
                        EmitPieceRotated(true);
                        _rotatingCW = false;
                    }
                    if (_down)
                    {
                        attemptMove(1, 0);
                        EmitPieceDropping();
                        _down = false;
                    }
                    if (_pauseToggling)
                    {
                        _paused = !_paused;
                        EmitPauseToggled(_paused);
                        _pauseToggling = false;
                    }
                }
            }

            public void Proceed()
            {
                if (_paused)
                    return;

                bool fail = false;
                if (attemptMove(1, 0))
                {
                    _resting = false;
                }
                else
                {
                    if (_resting)
                    {
                        int[,] render = GetPieceRender(_curPiece, _curPos);
                        for (int rr = 0; rr < render.GetLength(0); rr++)
                            for (int cc = 0; cc < render.GetLength(1); cc++)
                            {
                                if (render[rr, cc] != 0)
                                    _board[_curRow + rr][_curCol + cc] = (int)_curPiece;
                            }
                        EmitPiecePlaced();

                        List<int> fullLines = getCompleteLines();
                        if (fullLines.Count > 0)
                        {
                            EmitLinesFilled(fullLines);
                        }

                        fail = !PushPiece();
                    }
                    else
                    {
                        _resting = true;
                    }
                }

                if (fail)
                    EmitCrash();
            }
            #endregion

            #region Private Helpers
            private void nextPiece()
            {
                _nextPiece = (Piece)(Rand.Next(1, PiecesCount + 1));
            }

            private bool attemptMove(int dr, int dc)
            {
                int[,] render = GetPieceRender(_curPiece, _curPos);
                int newCol = _curCol + dc;
                int newRow = _curRow + dr;
                if (newCol < 0)
                    return false;
                if (newCol + render.GetLength(1) - 1 >= _width)
                    return false;
                if (newRow < 0)
                    return false;
                if (newRow + render.GetLength(0) - 1 >= _height)
                    return false;

                if (intersect(render, newRow, newCol))
                    return false;

                _curRow = newRow;
                _curCol = newCol;
                return true;
            }

            private bool attemptRotate(bool clockwise)
            {
                int newPos = (_curPos + (clockwise ? 1 : 3)) % 4;
                int[,] oldRender = GetPieceRender(_curPiece, _curPos);
                int[,] newRender = GetPieceRender(_curPiece, newPos);

                int newCol = _curCol + ((oldRender.GetLength(1) - 1) / 2) - ((newRender.GetLength(1) - 1) / 2);
                int newRow = _curRow + ((oldRender.GetLength(0) - 1) / 2) - ((newRender.GetLength(0) - 1) / 2);
                if (newCol < 0)
                    newCol = 0;
                else if (newCol + newRender.GetLength(1) - 1 >= _width)
                    newCol = _width - newRender.GetLength(1);
                if (newRow < 0)
                    newRow = 0;
                else if (newRow + newRender.GetLength(0) - 1 >= _height)
                    newRow = _height - newRender.GetLength(0);

                while (newRow > 0 && intersect(newRender, newRow, newCol))
                {
                    newRow--;
                }

                if (newRow < 0)
                    return false;

                _curPos = newPos;
                _curCol = newCol;
                _curRow = newRow;
                return true;
            }

            private bool intersect(int[,] render, int row, int col)
            {
                for (int rr = 0; rr < render.GetLength(0); rr++)
                    for (int cc = 0; cc < render.GetLength(1); cc++)
                    {
                        if (render[rr, cc] != 0 && _board[row + rr][col + cc] != 0)
                            return true;
                    }
                return false;
            }

            private List<int> getCompleteLines()
            {
                List<int> result = new List<int>();
                for (int row = 0; row < _height; row++)
                {
                    bool full = true;
                    for (int col = 0; col < _width; col++)
                    {
                        if (_board[row][col] == 0)
                        {
                            full = false;
                            break;
                        }
                    }
                    if (full)
                    {
                        result.Add(row);
                    }
                }
                foreach (int r in result)
                {
                    _board.RemoveAt(r);
                    _board.Insert(0, new int[_width]);
                }

                _score += _game.Scorer.LinesFilled(result.Count);
                _lineCount += result.Count;
                _level = Math.Max(_level, _lineCount / 2);

                return result;
            }
            #endregion
        }
        private Implementation _impl;

        private Timer _mainThread;

        private class RunState
        {
            public int TickCount = 0;
        }
        
        public void Run(int level, int width, int height)
        {
            _impl = new Implementation(this, level, width, height);
            _mainThread = new Timer(main, new RunState(), 1000, 1000 / 60);
        }

        public void Stop()
        {
            lock (_implLock)
            {
                if (_impl != null)
                {
                    _mainThread.Dispose();
                    _mainThread = null;
                    _impl = null;
                }
            }
        }

        private object _implLock = new object();
        private void main(object o)
        {
            RunState state = (RunState)o;
            lock (_implLock)
            {
                if (_impl == null)
                    return;
                _impl.Update();
                int period = ((2 * _impl.Level > 19) ? 1 : (20 - 2 * _impl.Level));
                if (state.TickCount % period == 0)
                    _impl.Proceed();
                _impl.EmitTick();
                state.TickCount++;
            }
        }

        public ScoreEngine Scorer { get; set; }
        public Controller Control { get { return _impl; } }
        public View Info { get { return _impl; } }
        public EventEngine Events { get { return _impl; } }
        public bool Running { get { return _impl != null; } }

        public void LockedTask(Action action)
        {
            lock (_implLock)
            {
                if (_impl == null)
                    return;

                action();
            }
        }
    }
}
