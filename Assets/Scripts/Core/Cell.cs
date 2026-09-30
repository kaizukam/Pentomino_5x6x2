using System;

namespace Pentomino.Core
{
    /// <summary>
    /// 盤面またはピース内のセル位置。
    /// Row は下方向、Col は右方向に増加する（開発仕様 V1 の d0 = 行方向、d1 = 列方向に対応）。
    /// </summary>
    public readonly struct Cell : IEquatable<Cell>
    {
        public readonly int Row;
        public readonly int Col;

        public Cell(int row, int col)
        {
            Row = row;
            Col = col;
        }

        public Cell Offset(int dRow, int dCol) => new Cell(Row + dRow, Col + dCol);

        public bool Equals(Cell other) => Row == other.Row && Col == other.Col;

        public override bool Equals(object obj) => obj is Cell other && Equals(other);

        public override int GetHashCode() => (Row * 397) ^ Col;

        public override string ToString() => "(" + Row + "," + Col + ")";

        public static bool operator ==(Cell a, Cell b) => a.Equals(b);

        public static bool operator !=(Cell a, Cell b) => !a.Equals(b);
    }
}
