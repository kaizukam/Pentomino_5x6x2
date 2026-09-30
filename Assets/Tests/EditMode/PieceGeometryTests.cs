using System.Collections.Generic;
using NUnit.Framework;
using Pentomino.Core;
using Pentomino.Data;

namespace Pentomino.Tests
{
    public class PieceGeometryTests
    {
        private readonly List<GridLine> _outer = new List<GridLine>();
        private readonly List<GridLine> _inner = new List<GridLine>();

        private void Build(params Cell[] cells) => PieceGeometry.Build(cells, _outer, _inner);

        private static int TotalLength(List<GridLine> lines)
        {
            var total = 0;
            foreach (var line in lines) total += line.Length;
            return total;
        }

        [Test]
        public void 単一セルは外周4本で内部は無し()
        {
            Build(new Cell(0, 0));

            Assert.AreEqual(0, _inner.Count);
            Assert.AreEqual(4, _outer.Count);
            Assert.AreEqual(4, TotalLength(_outer));
        }

        [Test]
        public void 横に2セル並べると共有辺が内部線になる()
        {
            Build(new Cell(0, 0), new Cell(0, 1));

            Assert.AreEqual(1, _inner.Count);
            var shared = _inner[0];
            Assert.IsFalse(shared.Horizontal, "共有辺は縦線");
            Assert.AreEqual(1, shared.Col);
            Assert.AreEqual(1, shared.Length);

            // 外周は上下が長さ 2 の横線 2 本、左右が長さ 1 の縦線 2 本。
            Assert.AreEqual(4, _outer.Count);
            Assert.AreEqual(6, TotalLength(_outer));
        }

        [Test]
        public void 段の違うセルは接していない()
        {
            // 立てたピースの上下の段は、画面では左右の格子に分かれて描く。
            Build(new Cell(0, 0, 0), new Cell(0, 0, 1));

            Assert.AreEqual(0, _inner.Count);
            Assert.AreEqual(8, _outer.Count);
            Assert.AreEqual(8, TotalLength(_outer));
        }

        [Test]
        public void 連続する外周は1本にまとめられる()
        {
            // I ペントミノを横一列に。上下はそれぞれ長さ 5 の 1 本になる。
            Build(new Cell(0, 0), new Cell(0, 1), new Cell(0, 2), new Cell(0, 3), new Cell(0, 4));

            Assert.AreEqual(4, _outer.Count, "上下 2 本と左右 2 本");
            Assert.AreEqual(4, _inner.Count, "内部の縦線 4 本");

            var horizontal = 0;
            foreach (var line in _outer)
            {
                if (!line.Horizontal) continue;
                horizontal++;
                Assert.AreEqual(5, line.Length);
            }
            Assert.AreEqual(2, horizontal);
        }

        [Test]
        public void 全姿勢で辺の総数が合う()
        {
            foreach (var posture in GameData.Postures.All)
            {
                PieceGeometry.Build(posture.Cells, _outer, _inner);

                // 5 セル x 4 辺。共有辺は 2 枚のセルで数えられるので内部線は 2 倍になる。
                Assert.AreEqual(Pieces.CellsPerPiece * 4,
                    TotalLength(_outer) + 2 * TotalLength(_inner), posture.Key);

                // 内部線は同じ段で隣り合うセルの組の数。段の違うセルは接していない扱い
                // （立てたピースは左右の格子に分かれて描く）なので、立てた姿勢では少なくなる。
                var expectedInner = SameLayerNeighbours(posture);
                if (!posture.IsStanding)
                {
                    // 2x2 の塊を持つ P だけ隣接が 1 組多く、周長がその分短い。
                    Assert.AreEqual(posture.Piece == 'P' ? 5 : 4, expectedInner, posture.Key);
                }
                Assert.AreEqual(expectedInner, TotalLength(_inner), posture.Key + " の内部線");
                Assert.AreEqual(20 - 2 * expectedInner, TotalLength(_outer), posture.Key + " の外周長");
            }
        }

        [Test]
        public void 内部線と外周線は重ならない()
        {
            foreach (var posture in GameData.Postures.All)
            {
                PieceGeometry.Build(posture.Cells, _outer, _inner);

                var outerUnits = new HashSet<GridLine>();
                foreach (var line in _outer)
                    for (var i = 0; i < line.Length; i++) outerUnits.Add(Unit(line, i));

                foreach (var line in _inner)
                    for (var i = 0; i < line.Length; i++)
                        Assert.IsFalse(outerUnits.Contains(Unit(line, i)), posture.Key + " で線が重複");
            }
        }

        [Test]
        public void BOXは左右の格子それぞれ外周が22で内部が49()
        {
            PieceGeometry.Build(PieceGeometry.BoardCells(), _outer, _inner);

            Assert.AreEqual(Board.Layers * 2 * (Board.Rows + Board.Cols), TotalLength(_outer), "外周長 = 2 x 2*(6+5)");

            // 内部の格子線: 縦 4 本 x 6 セル + 横 5 本 x 5 セル を 2 つ。
            Assert.AreEqual(Board.Layers * (4 * Board.Rows + 5 * Board.Cols), TotalLength(_inner));
            Assert.AreEqual(Board.Layers * 4, _outer.Count, "外周は格子ごとに 4 本にまとまる");
            Assert.AreEqual(Board.Layers * (4 + 5), _inner.Count, "内部は格子ごとに縦 4 本と横 5 本にまとまる");
        }

        private static int SameLayerNeighbours(Posture posture)
        {
            var count = 0;
            var cells = posture.Cells;
            for (var i = 0; i < cells.Count; i++)
                for (var j = i + 1; j < cells.Count; j++)
                {
                    var a = cells[i];
                    var b = cells[j];
                    if (a.Layer != b.Layer) continue;
                    if (System.Math.Abs(a.Row - b.Row) + System.Math.Abs(a.Col - b.Col) == 1) count++;
                }
            return count;
        }

        [Test]
        public void 空のセル集合では線が出ない()
        {
            PieceGeometry.Build(new Cell[0], _outer, _inner);
            Assert.AreEqual(0, _outer.Count);
            Assert.AreEqual(0, _inner.Count);
        }

        private static GridLine Unit(GridLine line, int offset) =>
            line.Horizontal
                ? new GridLine(line.Row, line.Col + offset, 1, true, line.Layer)
                : new GridLine(line.Row + offset, line.Col, 1, false, line.Layer);
    }
}
