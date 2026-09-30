using System;

namespace Pentomino.Core
{
    /// <summary>
    /// BOX またはピース内のセル位置。
    /// Row は画面の下方向、Col は右方向、Layer は BOX の段（0 = 左の格子、1 = 右の格子）に増加する。
    ///
    /// データ（Posture_DB.json と Answer）の座標 [d0, d1, d2] とは
    /// Col = d0、Row = d1、Layer = d2 で対応する。
    /// 6X10 では d0 が行だったが、5x6x2 は幅 5・高さ 6 の格子を縦長に見せるので、d0 が列になる。
    /// </summary>
    public readonly struct Cell : IEquatable<Cell>
    {
        public readonly int Row;
        public readonly int Col;
        public readonly int Layer;

        public Cell(int row, int col, int layer = 0)
        {
            Row = row;
            Col = col;
            Layer = layer;
        }

        public Cell Offset(int dRow, int dCol, int dLayer = 0) => new Cell(Row + dRow, Col + dCol, Layer + dLayer);

        public bool Equals(Cell other) => Row == other.Row && Col == other.Col && Layer == other.Layer;

        public override bool Equals(object obj) => obj is Cell other && Equals(other);

        public override int GetHashCode() => ((Row * 397) ^ Col) * 397 ^ Layer;

        public override string ToString() => "(" + Row + "," + Col + "," + Layer + ")";

        public static bool operator ==(Cell a, Cell b) => a.Equals(b);

        public static bool operator !=(Cell a, Cell b) => !a.Equals(b);
    }
}
