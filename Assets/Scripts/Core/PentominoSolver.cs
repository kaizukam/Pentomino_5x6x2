using System;
using System.Collections.Generic;

namespace Pentomino.Core
{
    /// <summary>
    /// 盤面の空き部分を残りピースで埋められるかを調べるバックトラック探索。
    /// 列優先の探索順で最初の空きセルを選び、そこを原点セルとして各姿勢を試す。
    /// 姿勢の原点が常に「最左列の最上セル」に正規化されているため、この 1 通りの試し方で網羅できる。
    /// </summary>
    public sealed class PentominoSolver
    {
        private readonly Posture[][] _posturesByPiece;

        private char[] _cells;
        private int _remainingMask;
        private int _found;
        private int _limit;
        private List<Placement> _current;
        private List<IReadOnlyList<Placement>> _solutions;

        public PentominoSolver(PostureDatabase database)
        {
            if (database == null) throw new ArgumentNullException(nameof(database));

            _posturesByPiece = new Posture[Pieces.Count][];
            for (var i = 0; i < Pieces.Count; i++)
            {
                var postures = database.PosturesOf(Pieces.At(i));
                var array = new Posture[postures.Count];
                for (var j = 0; j < postures.Count; j++) array[j] = postures[j];
                _posturesByPiece[i] = array;
            }
        }

        /// <summary>盤面を残りピースで完成させられるか。</summary>
        public bool CanComplete(Board board, IEnumerable<char> availablePieces) =>
            CountCompletions(board, availablePieces, 1) > 0;

        /// <summary>完成形をひとつ見つける。見つかった配置は追加するピースの分だけ返る。</summary>
        public bool TryFindCompletion(Board board, IEnumerable<char> availablePieces, out IReadOnlyList<Placement> completion)
        {
            var found = FindCompletions(board, availablePieces, 1);
            completion = found.Count > 0 ? found[0] : null;
            return found.Count > 0;
        }

        /// <summary>完成形を limit 件まで列挙する。</summary>
        public IReadOnlyList<IReadOnlyList<Placement>> FindCompletions(Board board, IEnumerable<char> availablePieces, int limit)
        {
            Solve(board, availablePieces, limit, true);
            return _solutions;
        }

        /// <summary>完成形の数を limit 件まで数える。</summary>
        public int CountCompletions(Board board, IEnumerable<char> availablePieces, int limit) =>
            Solve(board, availablePieces, limit, false);

        private int Solve(Board board, IEnumerable<char> availablePieces, int limit, bool recordSolution)
        {
            if (board == null) throw new ArgumentNullException(nameof(board));
            if (availablePieces == null) throw new ArgumentNullException(nameof(availablePieces));
            if (limit < 1) throw new ArgumentOutOfRangeException(nameof(limit));

            _cells = board.CopyCells();
            _remainingMask = 0;
            foreach (var piece in availablePieces)
            {
                var index = Pieces.IndexOf(piece);
                if (index < 0) throw new ArgumentException("未知のピース名です: " + piece, nameof(availablePieces));
                _remainingMask |= 1 << index;
            }

            _found = 0;
            _limit = limit;
            _current = recordSolution ? new List<Placement>(Pieces.Count) : null;
            _solutions = new List<IReadOnlyList<Placement>>();

            var emptyCells = 0;
            foreach (var c in _cells) if (c == Board.Empty) emptyCells++;
            if (emptyCells != CountBits(_remainingMask) * Pieces.CellsPerPiece) return 0;

            Search(0);
            return _found;
        }

        private void Search(int scanFrom)
        {
            var index = -1;
            for (var i = scanFrom; i < Board.CellCount; i++)
            {
                if (_cells[i] == Board.Empty) { index = i; break; }
            }

            if (index < 0)
            {
                _found++;
                if (_current != null) _solutions.Add(new List<Placement>(_current));
                return;
            }

            var row = index % Board.Rows;
            var col = index / Board.Rows;

            for (var p = 0; p < Pieces.Count; p++)
            {
                var bit = 1 << p;
                if ((_remainingMask & bit) == 0) continue;

                foreach (var posture in _posturesByPiece[p])
                {
                    if (!TryOccupy(posture, row, col)) continue;

                    _remainingMask &= ~bit;
                    _current?.Add(new Placement(posture, row, col));

                    Search(index);

                    _current?.RemoveAt(_current.Count - 1);
                    _remainingMask |= bit;
                    Release(posture, row, col);

                    if (_found >= _limit) return;
                }
            }
        }

        private bool TryOccupy(Posture posture, int row, int col)
        {
            var cells = posture.Cells;
            for (var i = 0; i < cells.Count; i++)
            {
                var r = row + cells[i].Row;
                var c = col + cells[i].Col;
                if (!Board.InRange(r, c) || _cells[Board.LinearIndex(r, c)] != Board.Empty)
                {
                    // ここまでに埋めた分を戻す。
                    for (var j = 0; j < i; j++)
                        _cells[Board.LinearIndex(row + cells[j].Row, col + cells[j].Col)] = Board.Empty;
                    return false;
                }
                _cells[Board.LinearIndex(r, c)] = posture.Piece;
            }
            return true;
        }

        private void Release(Posture posture, int row, int col)
        {
            foreach (var cell in posture.Cells)
                _cells[Board.LinearIndex(row + cell.Row, col + cell.Col)] = Board.Empty;
        }

        private static int CountBits(int value)
        {
            var count = 0;
            while (value != 0)
            {
                value &= value - 1;
                count++;
            }
            return count;
        }
    }
}
