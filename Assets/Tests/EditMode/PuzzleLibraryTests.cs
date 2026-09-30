using NUnit.Framework;
using Pentomino.Core;
using Pentomino.Data;

namespace Pentomino.Tests
{
    public class PuzzleLibraryTests
    {
        private PostureDatabase _db;
        private PuzzleLibrary _library;

        [OneTimeSetUp]
        public void SetUp()
        {
            _db = GameData.Postures;
            _library = GameData.Puzzles;
        }

        [Test]
        public void 問題は2339問ある()
        {
            Assert.AreEqual(2339, _library.Count);
            Assert.AreEqual("#0001", _library[1].Id);
            Assert.AreEqual("#2339", _library[2339].Id);
        }

        [Test]
        public void 全問題の解が6X10を過不足なく埋める()
        {
            foreach (var puzzle in _library.All)
            {
                var board = puzzle.BuildSolvedBoard();
                Assert.IsTrue(board.IsFull, puzzle.Id + " が盤面を埋めきれない:\n" + board.ToText());

                foreach (var piece in Pieces.Alphabetical)
                    Assert.IsTrue(board.IsPlaced(piece), puzzle.Id + " にピース " + piece + " が無い");
            }
        }

        [Test]
        public void Answer文字列を復元できる()
        {
            foreach (var puzzle in _library.All)
            {
                var encoded = puzzle.EncodeAnswer();
                Assert.AreEqual(Pieces.Count * Placement.TokenLength, encoded.Length, puzzle.Id);

                var round = Placement.Parse(encoded.Substring(0, Placement.TokenLength), _db);
                Assert.AreEqual(puzzle.Solution[0], round, puzzle.Id);
            }
        }

        [Test]
        public void Lvlは隠されるピース数と一致する()
        {
            foreach (var puzzle in _library.All)
            {
                var hidden = 0;
                foreach (var _ in puzzle.Hidden) hidden++;
                Assert.AreEqual(puzzle.Level, hidden, puzzle.Id);
                Assert.AreEqual(Pieces.Count - puzzle.Level, puzzle.PrearrangedCount, puzzle.Id);
            }
        }

        [Test]
        public void Answerは列優先の探索順に並んでいる()
        {
            // 先頭から (12 - Lvl) 個を置くと盤面が左から埋まる、という出題形式の前提。
            foreach (var puzzle in _library.All)
            {
                var previous = -1;
                foreach (var placement in puzzle.Solution)
                {
                    var key = placement.Col * Board.Rows + placement.Row;
                    Assert.Greater(key, previous, puzzle.Id + " の並び順が列優先でない");
                    previous = key;
                }
            }
        }

        [Test]
        public void 出題時の盤面には固定ピースだけが置かれている()
        {
            foreach (var puzzle in _library.All)
            {
                var board = puzzle.BuildInitialBoard();
                Assert.AreEqual(puzzle.PrearrangedCount * Pieces.CellsPerPiece, board.FilledCount, puzzle.Id);
            }
        }

        [Test]
        public void 問題はレベルの順に並んでいる()
        {
            // 説明書の表は「L3 は 11〜150」のように問題番号の範囲を出す。
            // 範囲はレベルごとの問題数を足し上げて出すので、並びが崩れると嘘になる。
            var previous = 0;
            foreach (var puzzle in _library.All)
            {
                Assert.GreaterOrEqual(puzzle.Level, previous, puzzle.Id + " で前の問題よりレベルが下がる");
                previous = puzzle.Level;
            }
        }

        [Test]
        public void 問題番号の移動が両端で丸められる()
        {
            Assert.AreEqual(1, _library.Offset(1, -10));
            Assert.AreEqual(11, _library.Offset(1, 10));
            Assert.AreEqual(2339, _library.Offset(2339, 10));
            Assert.AreEqual(2329, _library.Offset(2339, -10));
        }

        [Test]
        public void 壊れたCSVは例外になる()
        {
            Assert.Throws<System.FormatException>(() => PuzzleLibrary.FromCsv("ID,Lvl,Answer\n#0001,1,ABC\n", _db));
            Assert.Throws<System.FormatException>(() => PuzzleLibrary.FromCsv("ID,Lvl,Answer\n#0002,1,\n", _db));
        }
    }
}
