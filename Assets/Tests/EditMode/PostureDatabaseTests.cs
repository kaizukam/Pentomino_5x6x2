using System.Collections.Generic;
using NUnit.Framework;
using Pentomino.Core;
using Pentomino.Data;

namespace Pentomino.Tests
{
    public class PostureDatabaseTests
    {
        private static readonly FlickAxis[] Directions =
        {
            FlickAxis.Left, FlickAxis.Right, FlickAxis.Up, FlickAxis.Down,
        };

        private PostureDatabase _db;

        [OneTimeSetUp]
        public void SetUp() => _db = GameData.Postures;

        [Test]
        public void 姿勢は99種類ある()
        {
            // 支給の Posture_DB.json は字下げにノーブレークスペースを含む。読めていればここまで来る。
            Assert.AreEqual(99, _db.Count);
        }

        [Test]
        public void ピースごとの姿勢数が想定どおり()
        {
            var expected = new Dictionary<char, int>
            {
                { 'F', 8 }, { 'I', 2 }, { 'L', 16 }, { 'N', 16 }, { 'P', 16 }, { 'T', 4 },
                { 'U', 8 }, { 'V', 4 }, { 'W', 4 }, { 'X', 1 }, { 'Y', 16 }, { 'Z', 4 },
            };

            foreach (var pair in expected)
                Assert.AreEqual(pair.Value, _db.PosturesOf(pair.Key).Count, "ピース " + pair.Key);
        }

        [Test]
        public void 平らな姿勢は63で立てた姿勢は36()
        {
            var flat = 0;
            var standing = 0;
            foreach (var posture in _db.All)
            {
                if (posture.IsStanding)
                {
                    standing++;
                    StringAssert.Contains(posture.Piece.ToString(), "LNPUY", posture.Key + " は立てられないはず");
                }
                else
                {
                    flat++;
                }
            }

            Assert.AreEqual(63, flat);
            Assert.AreEqual(36, standing);
        }

        [Test]
        public void 全姿勢が5セルで原点が探索順の先頭()
        {
            foreach (var posture in _db.All)
            {
                Assert.AreEqual(Pieces.CellsPerPiece, posture.Cells.Count, posture.Key);
                Assert.AreEqual(new Cell(0, 0, 0), posture.Cells[0], posture.Key);
                Assert.AreEqual(0, posture.MinRow, posture.Key + " の原点が最上行にない");

                foreach (var c in posture.Cells)
                {
                    if (c.Row != 0) continue;
                    Assert.IsTrue(c.Layer > 0 || (c.Layer == 0 && c.Col >= 0),
                        posture.Key + " の " + c + " が原点より先に探される");
                }
            }
        }

        [Test]
        public void 全姿勢がBOXに収まる大きさ()
        {
            foreach (var posture in _db.All)
            {
                Assert.LessOrEqual(posture.Depth, Board.Layers, posture.Key);
                Assert.LessOrEqual(posture.Height, Board.Rows, posture.Key);
                Assert.LessOrEqual(posture.Width, Board.Cols, posture.Key);
            }
        }

        [Test]
        public void 反時計回りを4回で元に戻る()
        {
            foreach (var posture in _db.All)
            {
                var p = posture;
                for (var i = 0; i < 4; i++) p = p.RotatedCcw;
                Assert.AreSame(posture, p, posture.Key);
            }
        }

        [Test]
        public void 時計回りは反時計回りの逆()
        {
            foreach (var posture in _db.All)
                Assert.AreSame(posture, posture.RotatedCcw.RotatedCw, posture.Key);
        }

        [Test]
        public void 回転と転がしはピースを変えない()
        {
            foreach (var posture in _db.All)
            {
                Assert.AreEqual(posture.Piece, posture.RotatedCcw.Piece, posture.Key);
                foreach (var direction in Directions)
                {
                    var rolled = posture.Rolled(direction);
                    if (rolled != null) Assert.AreEqual(posture.Piece, rolled.Piece, posture.Key + " " + direction);
                }
            }
        }

        [Test]
        public void どの向きにも転がせる()
        {
            // 90 度で収まらなくても 180 度なら必ず収まる（今の Posture_DB.json では）。
            // 収まらないときの赤い点滅は、データが変わったときのために残してある。
            foreach (var posture in _db.All)
                foreach (var direction in Directions)
                    Assert.IsNotNull(posture.Rolled(direction), posture.Key + " " + direction);
        }

        [Test]
        public void 直角に転がしたら逆向きで元に戻る()
        {
            AssertInverse(FlickAxis.Right, FlickAxis.Left);
            AssertInverse(FlickAxis.Left, FlickAxis.Right);
            AssertInverse(FlickAxis.Up, FlickAxis.Down);
            AssertInverse(FlickAxis.Down, FlickAxis.Up);
        }

        private void AssertInverse(FlickAxis forward, FlickAxis back)
        {
            foreach (var posture in _db.All)
            {
                if (posture.IsHalfTurn(forward)) continue;

                var rolled = posture.Rolled(forward);
                Assert.IsFalse(rolled.IsHalfTurn(back), posture.Key + " " + forward);
                Assert.AreSame(posture, rolled.Rolled(back), posture.Key + " " + forward + " → " + back);
            }
        }

        [Test]
        public void 裏返しに転がしたら同じ向きでもう一度で元に戻る()
        {
            foreach (var posture in _db.All)
            {
                foreach (var direction in Directions)
                {
                    if (!posture.IsHalfTurn(direction)) continue;

                    var rolled = posture.Rolled(direction);
                    Assert.IsTrue(rolled.IsHalfTurn(direction), posture.Key + " " + direction);
                    Assert.AreSame(posture, rolled.Rolled(direction), posture.Key + " " + direction);
                }
            }
        }

        [Test]
        public void 平らなIを転がすと立てられないので180度になる()
        {
            var i = _db.Get("I01");   // 横に 5 つ
            Assert.IsTrue(i.IsHalfTurn(FlickAxis.Right));
            Assert.AreSame(i, i.Rolled(FlickAxis.Right), "I は裏返しても同じ形");
        }

        [Test]
        public void 平らなLを縦に転がすと立つ()
        {
            var l = _db.Get("L06");   // 横に 4 つと、左端の下に 1 つ
            Assert.IsFalse(l.IsHalfTurn(FlickAxis.Up), "縦は 2 行なので 90 度で立つ");
            Assert.IsTrue(l.Rolled(FlickAxis.Up).IsStanding);
        }

        [Test]
        public void どの姿勢にも00から回して届く()
        {
            // 黄帯・黒帯は姿勢 00 から始まるので、解に要る姿勢へ届かなければ解けない。
            // F と Z は 180 度の転がしがなければ裏返せない。
            foreach (var piece in Pieces.Alphabetical)
            {
                var seen = new HashSet<Posture>();
                var queue = new Queue<Posture>();
                var start = _db.DefaultPosture(piece);
                seen.Add(start);
                queue.Enqueue(start);

                while (queue.Count > 0)
                {
                    var p = queue.Dequeue();
                    var next = new List<Posture> { p.RotatedCcw, p.RotatedCw };
                    foreach (var direction in Directions) next.Add(p.Rolled(direction));

                    foreach (var q in next)
                        if (q != null && seen.Add(q)) queue.Enqueue(q);
                }

                Assert.AreEqual(_db.PosturesOf(piece).Count, seen.Count, "ピース " + piece);
            }
        }

        [Test]
        public void Xは回転しても転がしても変わらない()
        {
            var x = _db.DefaultPosture('X');
            Assert.AreSame(x, x.RotatedCcw);
            foreach (var direction in Directions) Assert.AreSame(x, x.Rolled(direction), direction.ToString());
        }

        [Test]
        public void V01の形がデータどおり()
        {
            // Posture_DB.json の [d0, d1, d2] は [列, 行, 段]。
            var expected = new[]
            {
                new Cell(0, 0, 0), new Cell(0, 1, 0), new Cell(0, 2, 0), new Cell(1, 0, 0), new Cell(2, 0, 0),
            };
            CollectionAssert.AreEqual(expected, _db.Get("V01").Cells);
        }

        [Test]
        public void Y10は立てた姿勢()
        {
            var expected = new[]
            {
                new Cell(0, 0, 0), new Cell(0, 1, 0), new Cell(0, 2, 0), new Cell(0, 3, 0), new Cell(0, 2, 1),
            };
            var y = _db.Get("Y10");
            CollectionAssert.AreEqual(expected, y.Cells);
            Assert.IsTrue(y.IsStanding);
        }
    }
}
