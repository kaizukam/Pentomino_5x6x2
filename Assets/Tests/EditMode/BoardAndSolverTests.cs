using System.Collections.Generic;
using NUnit.Framework;
using Pentomino.Core;
using Pentomino.Data;

namespace Pentomino.Tests
{
    public class BoardAndSolverTests
    {
        private PostureDatabase _db;
        private PuzzleLibrary _library;
        private PentominoSolver _solver;

        [OneTimeSetUp]
        public void SetUp()
        {
            _db = GameData.Postures;
            _library = GameData.Puzzles;
            _solver = GameData.Solver;
        }

        [Test]
        public void 空きセル探索は行ごとに左の格子から右の格子へ()
        {
            var board = new Board();
            Assert.IsTrue(board.TryFindFirstEmpty(out var cell));
            Assert.AreEqual(new Cell(0, 0, 0), cell);

            // 左の格子の最上行を埋めると、同じ行の右の格子へ進む。
            board.Place(new Placement(_db.Get("I01"), 0, 0, 0));   // 行 0 の列 0..4、段 0
            Assert.IsTrue(board.TryFindFirstEmpty(out cell));
            Assert.AreEqual(new Cell(0, 0, 1), cell, "左の格子の行が埋まったら、右の格子の同じ行へ進むはず");

            // 右の格子も左から右へ。
            board.Place(new Placement(_db.Get("L06"), 0, 0, 1));   // 行 0 の列 0..3 と (1,0)、段 1
            Assert.IsTrue(board.TryFindFirstEmpty(out cell));
            Assert.AreEqual(new Cell(0, 4, 1), cell);

            // 右の格子の行も埋まると、1 行下の左の格子へ。
            board.Place(new Placement(_db.Get("V03"), 0, 4, 1));   // (0,4)(1,4)(2,2)(2,3)(2,4)、段 1
            Assert.IsTrue(board.TryFindFirstEmpty(out cell));
            Assert.AreEqual(new Cell(1, 0, 0), cell);
        }

        [Test]
        public void 盤外や重なりは置けない()
        {
            var board = new Board();
            Assert.IsFalse(board.CanPlace(new Placement(_db.Get("I01"), 0, 1, 0)), "右にはみ出す");
            Assert.IsFalse(board.CanPlace(new Placement(_db.Get("I00"), 2, 0, 0)), "下にはみ出す");
            Assert.IsFalse(board.CanPlace(new Placement(_db.Get("L09"), 0, 0, 1)), "段を越える");
            Assert.IsFalse(board.CanPlace(new Placement(_db.Get("N13"), 0, 0, 0)), "手前の段を越える");
            Assert.IsTrue(board.CanPlace(new Placement(_db.Get("N13"), 0, 0, 1)), "立てて置ける");

            Assert.IsTrue(board.TryPlace(new Placement(_db.Get("X00"), 0, 1, 0)));
            Assert.IsFalse(board.TryPlace(new Placement(_db.Get("I00"), 0, 1, 0)), "重なる");
            Assert.IsTrue(board.CanPlace(new Placement(_db.Get("I00"), 0, 1, 1)), "右の格子の同じ場所は空いている");
            Assert.IsFalse(board.TryPlace(new Placement(_db.Get("X00"), 0, 1, 1)), "同じピースは 2 度置けない");
        }

        [Test]
        public void 置いて外すと元の状態に戻る()
        {
            var board = new Board();
            var before = board.ToText();

            board.Place(new Placement(_db.Get("L09"), 1, 0, 0));
            Assert.AreEqual(Pieces.CellsPerPiece, board.FilledCount);
            Assert.AreEqual('L', board[1, 0, 1], "立てたピースは右の格子にもかかる");

            Assert.IsTrue(board.Remove('L'));
            Assert.AreEqual(0, board.FilledCount);
            Assert.AreEqual(before, board.ToText());
            Assert.IsFalse(board.Remove('L'));
        }

        [Test]
        public void 線形索引とセルは往復する()
        {
            for (var i = 0; i < Board.CellCount; i++)
                Assert.AreEqual(i, Board.LinearIndex(Board.CellAt(i)));
        }

        [Test]
        public void 複製は独立している()
        {
            var board = _library[1].BuildInitialBoard();
            var clone = board.Clone();
            var text = board.ToText();

            foreach (var piece in new List<char>(clone.PlacedPieces)) clone.Remove(piece);

            Assert.AreEqual(text, board.ToText());
            Assert.AreEqual(0, clone.FilledCount);
        }

        [Test]
        public void 出題状態から解を1つは見つけられる()
        {
            foreach (var number in new[] { 1, 100, 200, 264 })
            {
                var puzzle = _library[number];
                var board = puzzle.BuildInitialBoard();
                var missing = MissingPieces(board);

                Assert.IsTrue(_solver.TryFindCompletion(board, missing, out var completion), puzzle.Id);
                Assert.AreEqual(puzzle.Level, completion.Count, puzzle.Id);

                foreach (var placement in completion) board.Place(placement);
                Assert.IsTrue(board.IsFull, puzzle.Id + ":\n" + board.ToText());
            }
        }

        [Test]
        public void Lvl1の問題は完成形が1通り()
        {
            Puzzle puzzle = null;
            foreach (var candidate in _library.All)
            {
                if (candidate.Level != 1) continue;
                puzzle = candidate;
                break;
            }
            Assert.IsNotNull(puzzle, "Lvl 1 の問題が見つからない");

            var board = puzzle.BuildInitialBoard();
            var count = _solver.CountCompletions(board, MissingPieces(board), 10);
            Assert.AreEqual(1, count, puzzle.Id);
        }

        [Test]
        public void 埋まった盤面の完成形は1通り()
        {
            var board = _library[1].BuildSolvedBoard();
            Assert.AreEqual(1, _solver.CountCompletions(board, new char[0], 5));
        }

        [Test]
        public void 孤立した空きセルが出来る盤面は0通り()
        {
            // X00 を左の格子の (0,1) に、I01 を右の格子の最上行に置くと、
            // 左の格子の (0,0) が右・下・奥を塞がれて 1 セルだけ孤立する。
            var board = new Board();
            board.Place(new Placement(_db.Get("X00"), 0, 1, 0));
            board.Place(new Placement(_db.Get("I01"), 0, 0, 1));

            Assert.AreEqual(0, _solver.CountCompletions(board, MissingPieces(board), 1));
            Assert.IsFalse(_solver.CanComplete(board, MissingPieces(board)));
        }

        [Test]
        public void ピース数と空きセル数が合わなければ解なし()
        {
            var board = new Board();
            Assert.AreEqual(0, _solver.CountCompletions(board, new[] { 'X' }, 1));
        }

        private static List<char> MissingPieces(Board board)
        {
            var missing = new List<char>();
            foreach (var piece in Pieces.Alphabetical)
                if (!board.IsPlaced(piece)) missing.Add(piece);
            return missing;
        }
    }
}
