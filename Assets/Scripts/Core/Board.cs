using System;
using System.Collections.Generic;
using System.Text;

namespace Pentomino.Core
{
    /// <summary>
    /// 6 行 10 列の BOX 格子。行は上から下、列は左から右に増加する。
    /// 探索順（Answer 文字列の並び順）は「列を上から下へ、次の列へ」の列優先。
    /// </summary>
    public sealed class Board
    {
        public const int Rows = 6;
        public const int Cols = 10;
        public const int CellCount = Rows * Cols;

        /// <summary>空セルを表すピース名。</summary>
        public const char Empty = '\0';

        private readonly char[] _cells = new char[CellCount];       // 列優先の一次元配列
        private readonly Placement[] _placements = new Placement[Pieces.Count];
        private int _filled;

        /// <summary>列優先の線形索引。空きセル探索がそのまま Answer の並び順になる。</summary>
        public static int LinearIndex(int row, int col) => col * Rows + row;

        public static bool InRange(int row, int col) => row >= 0 && row < Rows && col >= 0 && col < Cols;

        public char this[int row, int col]
        {
            get
            {
                if (!InRange(row, col)) throw new ArgumentOutOfRangeException(nameof(row), "盤外です: (" + row + "," + col + ")");
                return _cells[LinearIndex(row, col)];
            }
        }

        public int FilledCount => _filled;

        public bool IsFull => _filled == CellCount;

        public bool IsEmptyAt(int row, int col) => this[row, col] == Empty;

        /// <summary>置かれているピース名を配置順ではなくアルファベット順で列挙する。</summary>
        public IEnumerable<char> PlacedPieces
        {
            get
            {
                for (var i = 0; i < Pieces.Count; i++)
                    if (_placements[i].IsValid) yield return Pieces.At(i);
            }
        }

        public bool IsPlaced(char piece) => _placements[RequireIndex(piece)].IsValid;

        public bool TryGetPlacement(char piece, out Placement placement)
        {
            placement = _placements[RequireIndex(piece)];
            return placement.IsValid;
        }

        /// <summary>盤内に収まり、かつ全セルが空いているか。</summary>
        public bool CanPlace(Placement placement)
        {
            if (!placement.IsValid) return false;
            if (IsPlaced(placement.Piece)) return false;

            foreach (var cell in placement.Cells())
            {
                if (!InRange(cell.Row, cell.Col)) return false;
                if (_cells[LinearIndex(cell.Row, cell.Col)] != Empty) return false;
            }
            return true;
        }

        public bool TryPlace(Placement placement)
        {
            if (!CanPlace(placement)) return false;

            foreach (var cell in placement.Cells())
                _cells[LinearIndex(cell.Row, cell.Col)] = placement.Piece;

            _placements[Pieces.IndexOf(placement.Piece)] = placement;
            _filled += Pieces.CellsPerPiece;
            return true;
        }

        public void Place(Placement placement)
        {
            if (!TryPlace(placement))
                throw new InvalidOperationException("配置できません: " + placement);
        }

        public bool Remove(char piece)
        {
            var index = RequireIndex(piece);
            var placement = _placements[index];
            if (!placement.IsValid) return false;

            foreach (var cell in placement.Cells())
                _cells[LinearIndex(cell.Row, cell.Col)] = Empty;

            _placements[index] = default;
            _filled -= Pieces.CellsPerPiece;
            return true;
        }

        public void Clear()
        {
            Array.Clear(_cells, 0, _cells.Length);
            Array.Clear(_placements, 0, _placements.Length);
            _filled = 0;
        }

        /// <summary>列優先の探索順で最初の空きセルを返す。盤が埋まっていれば false。</summary>
        public bool TryFindFirstEmpty(out int row, out int col)
        {
            for (var i = 0; i < CellCount; i++)
            {
                if (_cells[i] != Empty) continue;
                row = i % Rows;
                col = i / Rows;
                return true;
            }
            row = -1;
            col = -1;
            return false;
        }

        public Board Clone()
        {
            var copy = new Board();
            Array.Copy(_cells, copy._cells, _cells.Length);
            Array.Copy(_placements, copy._placements, _placements.Length);
            copy._filled = _filled;
            return copy;
        }

        /// <summary>内部の列優先バッファをコピーして返す（ソルバ用）。</summary>
        internal char[] CopyCells()
        {
            var copy = new char[CellCount];
            Array.Copy(_cells, copy, CellCount);
            return copy;
        }

        /// <summary>1 行 1 文字列のテキスト表現。空セルは '.'。</summary>
        public string ToText()
        {
            var sb = new StringBuilder((Cols + 1) * Rows);
            for (var r = 0; r < Rows; r++)
            {
                for (var c = 0; c < Cols; c++)
                {
                    var value = _cells[LinearIndex(r, c)];
                    sb.Append(value == Empty ? '.' : value);
                }
                if (r < Rows - 1) sb.Append('\n');
            }
            return sb.ToString();
        }

        public override string ToString() => ToText();

        private static int RequireIndex(char piece)
        {
            var index = Pieces.IndexOf(piece);
            if (index < 0) throw new ArgumentException("未知のピース名です: " + piece, nameof(piece));
            return index;
        }
    }
}
