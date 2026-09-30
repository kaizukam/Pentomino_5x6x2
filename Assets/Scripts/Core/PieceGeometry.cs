using System;
using System.Collections.Generic;

namespace Pentomino.Core
{
    /// <summary>
    /// 格子上の直線 1 本。始点は段 Layer の格子の交点 (Row, Col) で、
    /// Horizontal なら列方向へ、そうでなければ行方向へ Length セル分伸びる。
    /// 段が違えば別の格子（画面では左右に並ぶ）なので、線は段をまたがない。
    /// </summary>
    public readonly struct GridLine : IEquatable<GridLine>
    {
        public readonly int Row;
        public readonly int Col;
        public readonly int Length;
        public readonly bool Horizontal;
        public readonly int Layer;

        public GridLine(int row, int col, int length, bool horizontal, int layer = 0)
        {
            Row = row;
            Col = col;
            Length = length;
            Horizontal = horizontal;
            Layer = layer;
        }

        public int EndRow => Horizontal ? Row : Row + Length;
        public int EndCol => Horizontal ? Col + Length : Col;

        public bool Equals(GridLine other) =>
            Row == other.Row && Col == other.Col && Length == other.Length && Horizontal == other.Horizontal
            && Layer == other.Layer;

        public override bool Equals(object obj) => obj is GridLine other && Equals(other);

        public override int GetHashCode() =>
            ((((Row * 397) ^ Col) * 397 ^ Length) * 397 ^ (Horizontal ? 1 : 0)) * 397 ^ Layer;

        public override string ToString() =>
            (Horizontal ? "H" : "V") + "(" + Row + "," + Col + "," + Layer + ")x" + Length;
    }

    /// <summary>
    /// セル集合から描画用の線を組み立てる。
    /// 外周は太線 (L1)、内部の格子は細線 (L2) として描く（開発仕様「ピースの描画」）。
    /// 同じ向きに連続する線はまとめて 1 本にするので、角が二重に描かれない。
    ///
    /// 段の違うセルは接していないものとして扱う。立てて置くピースは、
    /// 左右の格子にそれぞれ外周のある 2 つの形として描かれる。
    /// </summary>
    public static class PieceGeometry
    {
        /// <summary>
        /// 外周線と内部格子線を求める。
        /// </summary>
        /// <param name="cells">対象のセル集合。重複は無視する。</param>
        /// <param name="outer">外周（他のセルと接していない辺）。</param>
        /// <param name="inner">内部（セル同士が共有する辺）。</param>
        public static void Build(IEnumerable<Cell> cells, List<GridLine> outer, List<GridLine> inner)
        {
            if (cells == null) throw new ArgumentNullException(nameof(cells));
            if (outer == null) throw new ArgumentNullException(nameof(outer));
            if (inner == null) throw new ArgumentNullException(nameof(inner));

            outer.Clear();
            inner.Clear();

            var set = new HashSet<Cell>();
            foreach (var cell in cells) set.Add(cell);
            if (set.Count == 0) return;

            // 単位辺を集めてから、同じ行／列で連続するものを 1 本にまとめる。
            var outerUnits = new List<GridLine>(set.Count * 4);
            var innerUnits = new List<GridLine>(set.Count * 2);

            foreach (var cell in set)
            {
                var r = cell.Row;
                var c = cell.Col;
                var z = cell.Layer;

                // 上辺。共有していれば内部の線とし、下側のセルが 1 度だけ登録する。
                var above = new Cell(r - 1, c, z);
                if (set.Contains(above)) innerUnits.Add(new GridLine(r, c, 1, true, z));
                else outerUnits.Add(new GridLine(r, c, 1, true, z));

                // 下辺は外周のときだけ。内部なら下のセルが上辺として登録する。
                if (!set.Contains(new Cell(r + 1, c, z))) outerUnits.Add(new GridLine(r + 1, c, 1, true, z));

                // 左辺。共有していれば内部の線とし、右側のセルが 1 度だけ登録する。
                var left = new Cell(r, c - 1, z);
                if (set.Contains(left)) innerUnits.Add(new GridLine(r, c, 1, false, z));
                else outerUnits.Add(new GridLine(r, c, 1, false, z));

                // 右辺は外周のときだけ。
                if (!set.Contains(new Cell(r, c + 1, z))) outerUnits.Add(new GridLine(r, c + 1, 1, false, z));
            }

            Merge(outerUnits, outer);
            Merge(innerUnits, inner);
        }

        /// <summary>同じ直線上で連続する単位辺を 1 本にまとめる。</summary>
        private static void Merge(List<GridLine> units, List<GridLine> result)
        {
            units.Sort(CompareForMerge);

            var index = 0;
            while (index < units.Count)
            {
                var head = units[index];
                var length = 1;

                while (index + length < units.Count)
                {
                    var next = units[index + length];
                    if (next.Horizontal != head.Horizontal) break;
                    if (next.Layer != head.Layer) break;
                    if (head.Horizontal)
                    {
                        if (next.Row != head.Row || next.Col != head.Col + length) break;
                    }
                    else
                    {
                        if (next.Col != head.Col || next.Row != head.Row + length) break;
                    }
                    length++;
                }

                result.Add(new GridLine(head.Row, head.Col, length, head.Horizontal, head.Layer));
                index += length;
            }
        }

        private static int CompareForMerge(GridLine a, GridLine b)
        {
            // 段ごとに、横線は行ごとに列順、縦線は列ごとに行順に並べる。
            if (a.Horizontal != b.Horizontal) return a.Horizontal ? -1 : 1;

            var byLayer = a.Layer.CompareTo(b.Layer);
            if (byLayer != 0) return byLayer;

            if (a.Horizontal)
            {
                var byRow = a.Row.CompareTo(b.Row);
                return byRow != 0 ? byRow : a.Col.CompareTo(b.Col);
            }

            var byCol = a.Col.CompareTo(b.Col);
            return byCol != 0 ? byCol : a.Row.CompareTo(b.Row);
        }

        /// <summary>5x6x2 の BOX 全体のセルを列挙する。</summary>
        public static IEnumerable<Cell> BoardCells()
        {
            for (var z = 0; z < Board.Layers; z++)
                for (var r = 0; r < Board.Rows; r++)
                    for (var c = 0; c < Board.Cols; c++)
                        yield return new Cell(r, c, z);
        }
    }
}
