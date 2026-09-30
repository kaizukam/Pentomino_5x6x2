using System.Collections.Generic;
using NUnit.Framework;
using Pentomino.Core;
using Pentomino.Data;

namespace Pentomino.Tests
{
    public class PostureDatabaseTests
    {
        private PostureDatabase _db;

        [OneTimeSetUp]
        public void SetUp() => _db = GameData.Postures;

        [Test]
        public void 姿勢は63種類ある()
        {
            Assert.AreEqual(63, _db.Count);
        }

        [Test]
        public void ピースごとの姿勢数が想定どおり()
        {
            var expected = new Dictionary<char, int>
            {
                { 'F', 8 }, { 'I', 2 }, { 'L', 8 }, { 'N', 8 }, { 'P', 8 }, { 'T', 4 },
                { 'U', 4 }, { 'V', 4 }, { 'W', 4 }, { 'X', 1 }, { 'Y', 8 }, { 'Z', 4 },
            };

            foreach (var pair in expected)
                Assert.AreEqual(pair.Value, _db.PosturesOf(pair.Key).Count, "ピース " + pair.Key);
        }

        [Test]
        public void 全姿勢が5セルで原点が先頭()
        {
            foreach (var posture in _db.All)
            {
                Assert.AreEqual(Pieces.CellsPerPiece, posture.Cells.Count, posture.Key);
                Assert.AreEqual(new Cell(0, 0), posture.Cells[0], posture.Key);
                Assert.AreEqual(0, posture.MinCol, posture.Key + " の原点が最左列にない");
            }
        }

        [Test]
        public void 相対座標の範囲が仕様どおり()
        {
            // 開発仕様 V1 83 行目: d0 は -3..4、d1 は 0..4。
            foreach (var posture in _db.All)
            {
                Assert.GreaterOrEqual(posture.MinRow, -3, posture.Key);
                Assert.LessOrEqual(posture.MaxRow, 4, posture.Key);
                Assert.GreaterOrEqual(posture.MinCol, 0, posture.Key);
                Assert.LessOrEqual(posture.MaxCol, 4, posture.Key);
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
        public void 反転を2回で元に戻る()
        {
            foreach (var posture in _db.All)
                Assert.AreSame(posture, posture.FlippedHorizontally.FlippedHorizontally, posture.Key);
        }

        [Test]
        public void 回転と反転はピースを変えない()
        {
            foreach (var posture in _db.All)
            {
                Assert.AreEqual(posture.Piece, posture.RotatedCcw.Piece, posture.Key);
                Assert.AreEqual(posture.Piece, posture.FlippedHorizontally.Piece, posture.Key);
            }
        }

        [Test]
        public void アキラルなピースはITUVWX()
        {
            var achiral = "ITUVWX";
            foreach (var piece in Pieces.Alphabetical)
            {
                var expected = achiral.IndexOf(piece) >= 0;
                Assert.AreEqual(expected, _db.IsAchiral(piece), "ピース " + piece);
            }
        }

        [Test]
        public void Xは回転しても反転しても変わらない()
        {
            var x = _db.DefaultPosture('X');
            Assert.AreSame(x, x.RotatedCcw);
            Assert.AreSame(x, x.FlippedHorizontally);
        }

        [Test]
        public void Iは反転しても変わらない()
        {
            foreach (var posture in _db.PosturesOf('I'))
                Assert.AreSame(posture, posture.FlippedHorizontally, posture.Key);
        }

        [Test]
        public void 姿勢番号00から03と04から07が表裏の関係()
        {
            // キラルなピースでは前半 4 種と後半 4 種が反転の関係にある。
            foreach (var piece in "FLNPY")
            {
                foreach (var posture in _db.PosturesOf(piece))
                {
                    var flipped = posture.FlippedHorizontally;
                    Assert.AreNotEqual(posture.Index < 4, flipped.Index < 4,
                        piece + ": " + posture.Key + " の反転が " + flipped.Key);
                }
            }
        }

        [Test]
        public void V01の形が仕様書の記載どおり()
        {
            var expected = new[]
            {
                new Cell(0, 0), new Cell(1, 0), new Cell(2, 0), new Cell(0, 1), new Cell(0, 2),
            };
            CollectionAssert.AreEqual(expected, _db.Get("V01").Cells);
        }
    }
}
