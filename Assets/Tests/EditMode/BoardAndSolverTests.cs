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
        public void 空きセル探索は列優先()
        {
            var board = new Board();
            Assert.IsTrue(board.TryFindFirstEmpty(out var row, out var col));
            Assert.AreEqual(0, row);
            Assert.AreEqual(0, col);

            // 左端の列を埋めると、行方向に空きが残っていても次の列へ進む。
            board.Place(new Placement(_db.Get("L06"), 0, 0));   // (0,0)(1,0)(2,0)(3,0)(0,1)
            board.Place(new Placement(_db.Get("P02"), 4, 0));   // (4,0)(5,0)(3,1)(4,1)(5,1)
            Assert.IsTrue(board.TryFindFirstEmpty(out row, out col));
            Assert.AreEqual(1, col, "左端の列が埋まったら次の列へ進むはず");
            Assert.AreEqual(1, row);
        }

        [Test]
        public void 盤外や重なりは置けない()
        {
            var board = new Board();
            Assert.IsFalse(board.CanPlace(new Placement(_db.Get("I00"), 0, 6)), "右にはみ出す");
            Assert.IsFalse(board.CanPlace(new Placement(_db.Get("I01"), 2, 0)), "下にはみ出す");

            Assert.IsTrue(board.TryPlace(new Placement(_db.Get("X00"), 1, 0)));
            Assert.IsFalse(board.TryPlace(new Placement(_db.Get("I00"), 1, 0)), "重なる");
            Assert.IsFalse(board.TryPlace(new Placement(_db.Get("X00"), 1, 5)), "同じピースは 2 度置けない");
        }

        [Test]
        public void 置いて外すと元の状態に戻る()
        {
            var board = new Board();
            var before = board.ToText();

            board.Place(new Placement(_db.Get("X00"), 1, 0));
            Assert.AreEqual(Pieces.CellsPerPiece, board.FilledCount);

            Assert.IsTrue(board.Remove('X'));
            Assert.AreEqual(0, board.FilledCount);
            Assert.AreEqual(before, board.ToText());
            Assert.IsFalse(board.Remove('X'));
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
            foreach (var number in new[] { 1, 100, 1000, 2339 })
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
            // X00 を (1,0) に置くと (0,0) が四方を塞がれて 1 セルだけ孤立する。
            var board = new Board();
            board.Place(new Placement(_db.Get("X00"), 1, 0));

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
