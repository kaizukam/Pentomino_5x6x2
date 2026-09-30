using System.Collections.Generic;
using NUnit.Framework;
using Pentomino.Core;
using Pentomino.Data;

namespace Pentomino.Tests
{
    public class PuzzleSessionTests
    {
        private PostureDatabase _db;
        private PuzzleLibrary _library;

        [OneTimeSetUp]
        public void SetUp()
        {
            _db = GameData.Postures;
            _library = GameData.Puzzles;
        }

        private PuzzleSession NewSession(int number, Difficulty difficulty = Difficulty.Guided) =>
            new PuzzleSession(_library[number], _db, difficulty);

        private static Puzzle FindByLevel(PuzzleLibrary library, int level)
        {
            foreach (var puzzle in library.All)
                if (puzzle.Level == level) return puzzle;
            Assert.Fail("Lvl " + level + " の問題が見つからない");
            return null;
        }

        [Test]
        public void 出題直後は固定ピースが盤上で残りが待機場所()
        {
            var session = NewSession(150);
            var puzzle = session.Puzzle;

            Assert.AreEqual(puzzle.PrearrangedCount * Pieces.CellsPerPiece, session.Board.FilledCount);
            Assert.AreEqual(puzzle.Level, session.Tray.Count);
            Assert.IsFalse(session.IsSolved);

            foreach (var placement in puzzle.Prearranged)
                Assert.IsTrue(session.IsFixed(placement.Piece), placement.Piece.ToString());
            foreach (var placement in puzzle.Hidden)
                Assert.IsFalse(session.IsFixed(placement.Piece), placement.Piece.ToString());
        }

        [Test]
        public void 初級は待機場所が解答順で姿勢も解答通り()
        {
            var puzzle = FindByLevel(_library, 5);
            var session = new PuzzleSession(puzzle, _db, Difficulty.Guided);

            var expected = new List<Placement>(puzzle.Hidden);
            Assert.AreEqual(expected.Count, session.Tray.Count);
            for (var i = 0; i < expected.Count; i++)
            {
                Assert.AreEqual(expected[i].Piece, session.Tray[i].Piece, i.ToString());
                Assert.AreSame(expected[i].Posture, session.Tray[i].Posture, i.ToString());
            }
        }

        [Test]
        public void ターンは待機場所が解答順で姿勢はリセットされる()
        {
            var puzzle = FindByLevel(_library, 5);
            var session = new PuzzleSession(puzzle, _db, Difficulty.Turn);

            // 並びはガイドと同じ解答順。
            var expected = new List<Placement>(puzzle.Hidden);
            Assert.AreEqual(expected.Count, session.Tray.Count);
            for (var i = 0; i < expected.Count; i++)
                Assert.AreEqual(expected[i].Piece, session.Tray[i].Piece, i.ToString());

            // 向きは自分で合わせる級なので、姿勢は必ず 00 に戻っている。
            foreach (var trayPiece in session.Tray)
                Assert.AreEqual(0, trayPiece.Posture.Index, trayPiece.Piece.ToString());
        }

        [Test]
        public void ターンで向きが合ったままのピースが無い()
        {
            // 解答の姿勢が 00 でないピースは、置く前に必ず回すか裏返す必要がある。
            // 姿勢を教えてしまっていると、この級の意味が無くなる。
            var puzzle = FindByLevel(_library, 5);
            var session = new PuzzleSession(puzzle, _db, Difficulty.Turn);

            var needsTurning = 0;
            foreach (var trayPiece in session.Tray)
            {
                var answer = puzzle.SolutionFor(trayPiece.Piece).Posture;
                if (answer.Index != trayPiece.Posture.Index) needsTurning++;
            }

            Assert.Greater(needsTurning, 0, "どのピースも回さずに置けてしまう");
        }

        [Test]
        public void 上級は待機場所がアルファベット順で姿勢は00()
        {
            var puzzle = FindByLevel(_library, 5);
            var session = new PuzzleSession(puzzle, _db, Difficulty.Classic);

            AssertAlphabetical(session);
            foreach (var trayPiece in session.Tray)
                Assert.AreEqual(0, trayPiece.Posture.Index, trayPiece.Piece.ToString());
        }

        [Test]
        public void Hintは待機場所の並びに関係なくAnswerの末尾から埋める()
        {
            var puzzle = FindByLevel(_library, 5);

            // クラシックは待機場所がアルファベット順なので、
            // 「待機場所の並び」と「Answer の並び」が食い違う問題を選べる。
            var session = new PuzzleSession(puzzle, _db, Difficulty.Classic);

            var answerOrder = new List<char>();
            foreach (var placement in puzzle.Hidden) answerOrder.Add(placement.Piece);

            // 先頭から埋めると左上から順に固まるだけで、難しさがそのまま下がる。
            // 末尾から埋めれば盤の離れた場所に置かれ、読み直しが要る。
            for (var i = 0; i < answerOrder.Count; i++)
            {
                Assert.IsTrue(session.Hint(), (i + 1) + " 回目の Hint");

                var expected = answerOrder[answerOrder.Count - 1 - i];
                Assert.IsTrue(session.Board.IsPlaced(expected),
                    (i + 1) + " 番目は Answer の末尾から " + expected + " のはず");
            }

            Assert.IsTrue(session.IsSolved);
        }

        [Test]
        public void Hintで埋まる空白は探索順を下から遡る()
        {
            var puzzle = FindByLevel(_library, 5);
            var session = new PuzzleSession(puzzle, _db, Difficulty.Classic);

            // Answer は探索順（上の行から、左の格子、右の格子）で、それを末尾から遡るので、
            // 埋まるピースの原点は探索順で単調に前へ戻る。下の行から上の行へ、
            // 同じ行なら右の格子から左の格子へ。
            var previous = int.MaxValue;

            while (!session.IsSolved)
            {
                Assert.IsTrue(session.TryFindNextHint(out var next));
                Assert.IsTrue(session.Hint());

                var index = Board.LinearIndex(next.Row, next.Col, next.Layer);
                Assert.Less(index, previous, "探索順で前へ戻る");
                previous = index;
            }
        }

        private static void AssertAlphabetical(PuzzleSession session)
        {
            var previous = -1;
            foreach (var trayPiece in session.Tray)
            {
                var index = Pieces.IndexOf(trayPiece.Piece);
                Assert.Greater(index, previous, "アルファベット順になっていない");
                previous = index;
            }
        }

        [Test]
        public void 待機場所のピースは回転と転がしができる()
        {
            var session = NewSession(264, Difficulty.Classic);
            var piece = session.Tray[0].Piece;
            var original = session.Tray[0].Posture;

            var rotated = session.RotateCcw(piece);
            Assert.AreSame(original.RotatedCcw, rotated);

            for (var i = 0; i < 3; i++) session.RotateCcw(piece);
            Assert.AreSame(original, session.Tray[0].Posture, "4 回転で元に戻る");

            var rolled = session.Roll(piece, FlickAxis.Right);
            Assert.AreSame(original.Rolled(FlickAxis.Right), rolled);
            session.Roll(piece, original.IsHalfTurn(FlickAxis.Right) ? FlickAxis.Right : FlickAxis.Left);
            Assert.AreSame(original, session.Tray[0].Posture, "転がして戻せば元の姿勢");
        }

        [Test]
        public void 盤上のピースは回転できない()
        {
            var session = NewSession(264);
            var fixedPiece = new List<Placement>(session.Puzzle.Prearranged)[0].Piece;
            Assert.IsNull(session.RotateCcw(fixedPiece));
        }

        [Test]
        public void 正しい位置に置くと待機場所から消える()
        {
            var session = NewSession(200);
            var target = new List<Placement>(session.Puzzle.Hidden)[0];

            var trayBefore = session.Tray.Count;
            Assert.IsTrue(session.TryPlace(target.Piece, target.Row, target.Col, target.Layer));
            Assert.AreEqual(trayBefore - 1, session.Tray.Count);
            Assert.IsTrue(session.Board.IsPlaced(target.Piece));
        }

        [Test]
        public void 拾い上げると待機場所に戻る()
        {
            var session = NewSession(200);
            var target = new List<Placement>(session.Puzzle.Hidden)[0];

            session.TryPlace(target.Piece, target.Row, target.Col, target.Layer);
            Assert.IsTrue(session.TryPickUp(target.Piece));
            Assert.IsFalse(session.Board.IsPlaced(target.Piece));
            Assert.IsTrue(session.TryGetTrayPiece(target.Piece, out _));

            var fixedPiece = new List<Placement>(session.Puzzle.Prearranged)[0].Piece;
            Assert.IsFalse(session.TryPickUp(fixedPiece), "固定ピースは動かせない");
        }

        [Test]
        public void 正しい配置ならCheckのペナルティはゼロ()
        {
            var session = NewSession(200);
            foreach (var placement in session.Puzzle.Hidden)
            {
                if (session.Tray.Count == 1) break;   // 最後の 1 個を残す
                Assert.IsTrue(session.TryPlace(placement.Piece, placement.Row, placement.Col, placement.Layer));
            }

            Assert.AreEqual(0, session.Check());
            Assert.AreEqual(0, session.CheckCount);
        }

        [Test]
        public void 間違った配置はCheckで待機場所に戻される()
        {
            var puzzle = FindByLevel(_library, 7);
            var session = new PuzzleSession(puzzle, _db, Difficulty.Guided);

            var piece = session.Tray[0].Piece;
            Assert.IsTrue(TryPlaceSomewhereWrong(session, piece), "解と違う置き方が見つからない");

            Assert.AreEqual(1, session.Check());
            Assert.AreEqual(-1, session.CheckCount, "間違い 1 個ぶん減点される");
            Assert.IsFalse(session.Board.IsPlaced(piece));
            Assert.IsTrue(session.TryGetTrayPiece(piece, out _), "待機場所に戻っている");
        }

        [Test]
        public void Hintを繰り返すと完成する()
        {
            var puzzle = FindByLevel(_library, 6);
            var session = new PuzzleSession(puzzle, _db, Difficulty.Turn);

            for (var i = 0; i < puzzle.Level; i++)
                Assert.IsTrue(session.Hint(), (i + 1) + " 回目の Hint");

            Assert.IsTrue(session.IsSolved);
            Assert.AreEqual(-puzzle.Level, session.HintCount, "間違いが無ければ Hint 回数だけ減点される");
            Assert.AreEqual(0, session.CheckCount);
            Assert.AreEqual(0, session.Tray.Count);
        }

        [Test]
        public void HintのペナルティはHintカウンタに入る()
        {
            var puzzle = FindByLevel(_library, 7);
            var session = new PuzzleSession(puzzle, _db, Difficulty.Guided);

            var piece = session.Tray[0].Piece;
            Assert.IsTrue(TryPlaceSomewhereWrong(session, piece));

            Assert.IsTrue(session.Hint());
            Assert.AreEqual(0, session.CheckCount, "Check カウンタは動かない");
            Assert.AreEqual(-2, session.HintCount, "間違い 1 個 + Hint 1 回ぶん減点");
        }

        [Test]
        public void カウンタは0から負に進む()
        {
            var puzzle = FindByLevel(_library, 7);
            var session = new PuzzleSession(puzzle, _db, Difficulty.Guided);

            // 開発仕様: 累積 Check 回数・Hint 回数は負の数で表す。
            Assert.AreEqual(0, session.CheckCount, "はじめは 0");
            Assert.AreEqual(0, session.HintCount, "はじめは 0");

            session.Hint();
            Assert.AreEqual(-1, session.HintCount);

            session.Hint();
            Assert.AreEqual(-2, session.HintCount);
            Assert.LessOrEqual(session.CheckCount, 0, "Check は正にならない");
            Assert.LessOrEqual(session.HintCount, 0, "Hint は正にならない");
        }

        [Test]
        public void 完成すると操作が凍結される()
        {
            var puzzle = FindByLevel(_library, 3);
            var session = new PuzzleSession(puzzle, _db, Difficulty.Guided);

            foreach (var placement in puzzle.Hidden)
                Assert.IsTrue(session.TryPlace(placement.Piece, placement.Row, placement.Col, placement.Layer));

            Assert.IsTrue(session.IsSolved);
            Assert.AreEqual(0, session.Check());
            Assert.IsFalse(session.Hint());
            Assert.IsFalse(session.TryPickUp(puzzle.Solution[Pieces.Count - 1].Piece));
        }

        [Test]
        public void 進捗を保存して復元できる()
        {
            var puzzle = FindByLevel(_library, 6);
            var session = new PuzzleSession(puzzle, _db, Difficulty.Classic);

            // 1 個を盤上に置き、1 個は待機場所で回転させておく。
            var placed = new List<Placement>(puzzle.Hidden)[0];
            Assert.IsTrue(PlaceExact(session, placed), "解の姿勢に合わせて置けるはず");

            var rotatedPiece = session.Tray[0].Piece;
            var rotated = session.RotateCcw(rotatedPiece);

            var encoded = session.EncodeProgress();
            Assert.AreEqual(puzzle.Level * Placement.TokenLength, encoded.Length);

            var restored = new PuzzleSession(puzzle, _db, Difficulty.Classic);
            Assert.IsTrue(restored.TryRestoreProgress(encoded));

            Assert.AreEqual(session.Board.ToText(), restored.Board.ToText());
            Assert.AreEqual(session.Tray.Count, restored.Tray.Count);
            Assert.IsTrue(restored.TryGetTrayPiece(rotatedPiece, out var restoredPiece));
            Assert.AreSame(rotated, restoredPiece.Posture);
            Assert.AreEqual(encoded, restored.EncodeProgress());
        }

        [Test]
        public void 盤外のピースは9999で記録される()
        {
            // 級ごとの問題数は表しだい（今は L4 が無い）なので、L5 で確かめる。
            var puzzle = FindByLevel(_library, 5);
            var session = new PuzzleSession(puzzle, _db, Difficulty.Classic);

            var encoded = session.EncodeProgress();
            for (var i = 0; i < puzzle.Level; i++)
            {
                var token = encoded.Substring(i * Placement.TokenLength, Placement.TokenLength);
                Assert.AreEqual(Placement.OffBoardCoordinates, token.Substring(3), token);
            }
        }

        [Test]
        public void 壊れた進捗は復元しない()
        {
            var session = NewSession(200);
            Assert.IsFalse(session.TryRestoreProgress(""));
            Assert.IsFalse(session.TryRestoreProgress("XXXX"));
            Assert.IsFalse(session.TryRestoreProgress(new string('Z', session.Puzzle.Level * Placement.TokenLength)));
        }

        /// <summary>
        /// 姿勢を合わせてから置く。タップ（面内の回転）とフリック（転がし）だけで、
        /// 待機場所の姿勢から解の姿勢まで最短の手順を探して当てはめる。
        /// </summary>
        private static bool PlaceExact(PuzzleSession session, Placement placement)
        {
            if (!session.TryGetTrayPiece(placement.Piece, out var trayPiece)) return false;

            var steps = PathTo(trayPiece.Posture, placement.Posture);
            if (steps == null) return false;

            foreach (var step in steps)
            {
                if (step == FlickAxis.None) session.RotateCcw(placement.Piece);
                else session.Roll(placement.Piece, step);
            }

            if (!ReferenceEquals(trayPiece.Posture, placement.Posture)) return false;
            return session.TryPlace(placement.Piece, placement.Row, placement.Col, placement.Layer);
        }

        /// <summary>姿勢 from から to への操作の並び。None はタップ。届かなければ null。</summary>
        private static List<FlickAxis> PathTo(Posture from, Posture to)
        {
            var moves = new[] { FlickAxis.None, FlickAxis.Left, FlickAxis.Right, FlickAxis.Up, FlickAxis.Down };
            var previous = new Dictionary<Posture, KeyValuePair<Posture, FlickAxis>>();
            var queue = new Queue<Posture>();
            previous[from] = default;
            queue.Enqueue(from);

            while (queue.Count > 0)
            {
                var p = queue.Dequeue();
                if (ReferenceEquals(p, to)) break;

                foreach (var move in moves)
                {
                    var q = move == FlickAxis.None ? p.RotatedCcw : p.Rolled(move);
                    if (q == null || previous.ContainsKey(q)) continue;
                    previous[q] = new KeyValuePair<Posture, FlickAxis>(p, move);
                    queue.Enqueue(q);
                }
            }

            if (!previous.ContainsKey(to)) return null;

            var path = new List<FlickAxis>();
            for (var p = to; !ReferenceEquals(p, from); p = previous[p].Key) path.Add(previous[p].Value);
            path.Reverse();
            return path;
        }

        private static bool TryPlaceSomewhereWrong(PuzzleSession session, char piece)
        {
            var solution = session.Puzzle.SolutionFor(piece);
            session.TryGetTrayPiece(piece, out var trayPiece);
            var posture = trayPiece.Posture;

            for (var i = 0; i < Board.CellCount; i++)
            {
                var cell = Board.CellAt(i);
                if (posture == solution.Posture && cell.Row == solution.Row && cell.Col == solution.Col
                    && cell.Layer == solution.Layer) continue;
                if (!session.CanPlace(piece, posture, cell.Row, cell.Col, cell.Layer)) continue;
                return session.TryPlace(piece, cell.Row, cell.Col, cell.Layer);
            }
            return false;
        }

    }
}
