using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using Pentomino.Core;
using Pentomino.Data;

namespace Pentomino.Tests
{
    public class ProgressStoreTests
    {
        private string _path;
        private ProgressStore _store;

        [SetUp]
        public void SetUp()
        {
            _path = Path.Combine(Path.GetTempPath(), "pentomino_test_" + Path.GetRandomFileName() + ".json");
            _store = new ProgressStore(_path);
        }

        [TearDown]
        public void TearDown()
        {
            if (File.Exists(_path)) File.Delete(_path);
        }

        [Test]
        public void 保存が無ければ初期状態を返す()
        {
            var progress = _store.Load();
            Assert.AreEqual(Difficulty.Classic, progress.Difficulty, "既定は上級");
            Assert.AreEqual(Language.Japanese, progress.Language);
            Assert.AreEqual(1, progress.LastPuzzle);
            Assert.AreEqual(0, progress.Current.puzzles.Count);
        }

        [Test]
        public void 保存して読み直すと内容が一致する()
        {
            var progress = _store.Load();
            progress.Language = Language.French;
            progress.LastPuzzle = 42;

            var entry = progress.GetOrCreate(42);
            entry.pieces = "F009999";
            entry.checkCount = 3;
            entry.hintCount = 1;

            Assert.IsTrue(_store.Save(progress));

            var loaded = _store.Load();
            Assert.AreEqual(Language.French, loaded.Language);
            Assert.AreEqual(42, loaded.LastPuzzle);
            Assert.IsTrue(loaded.TryGet(42, out var restored));
            Assert.AreEqual("F009999", restored.pieces);
            Assert.AreEqual(3, restored.checkCount);
            Assert.AreEqual(1, restored.hintCount);
            Assert.IsFalse(restored.solved);
        }

        [Test]
        public void 級を変えても進捗は消えず級ごとに残る()
        {
            // 以前は級を変えるたびに全部消えていた。テスターが級を試すたびに
            // それまでの記録を失うので、級ごとに別々に残すことにした。
            var progress = _store.Load();
            progress.GetOrCreate(10).checkCount = 5;
            progress.LastPuzzle = 10;

            progress.Difficulty = Difficulty.Guided;

            Assert.AreEqual(0, progress.Current.puzzles.Count, "別の級の記録が見えています");
            Assert.AreEqual(1, progress.LastPuzzle);

            progress.Difficulty = Difficulty.Classic;

            Assert.AreEqual(1, progress.Current.puzzles.Count, "戻ったのに記録が消えています");
            Assert.AreEqual(10, progress.LastPuzzle);
        }

        [Test]
        public void 級ごとの記録は保存して読み直しても残る()
        {
            var progress = _store.Load();

            foreach (var difficulty in DifficultyRules.All)
            {
                var record = progress.RecordOf(difficulty);
                record.GetOrCreate(100 + (int)difficulty).checkCount = (int)difficulty + 1;
                record.lastPuzzle = 100 + (int)difficulty;
            }

            Assert.IsTrue(_store.Save(progress));
            var loaded = _store.Load();

            foreach (var difficulty in DifficultyRules.All)
            {
                var record = loaded.RecordOf(difficulty);
                Assert.AreEqual(100 + (int)difficulty, record.lastPuzzle, difficulty.ToString());
                Assert.IsTrue(record.TryGet(100 + (int)difficulty, out var entry), difficulty.ToString());
                Assert.AreEqual((int)difficulty + 1, entry.checkCount, difficulty.ToString());
            }
        }

        [Test]
        public void 記録を消すのはその級だけ()
        {
            var progress = _store.Load();
            foreach (var difficulty in DifficultyRules.All)
                progress.RecordOf(difficulty).GetOrCreate(7).checkCount = 1;

            progress.ClearProgress(Difficulty.Turn);

            Assert.AreEqual(0, progress.RecordOf(Difficulty.Turn).puzzles.Count);
            Assert.AreEqual(1, progress.RecordOf(Difficulty.Guided).puzzles.Count, "巻き添えで消えています");
            Assert.AreEqual(1, progress.RecordOf(Difficulty.Classic).puzzles.Count, "巻き添えで消えています");
        }

        [Test]
        public void 版1の進捗はそのときの級の記録として引き継ぐ()
        {
            // 版 1 は進捗を 1 本しか持たず、それが「そのとき選んでいた級」の記録だった。
            // JsonUtility は知らないキーを黙って捨てるので、拾い直さないと消える。
            File.WriteAllText(_path,
                "{\"version\":1,\"difficulty\":1,\"language\":\"ja\",\"lastPuzzle\":57," +
                "\"puzzles\":[{\"number\":57,\"pieces\":\"F009999\",\"checkCount\":4," +
                "\"hintCount\":2,\"solved\":false}]}");

            var loaded = _store.Load();

            Assert.AreEqual(GameProgress.CurrentVersion, loaded.version);
            Assert.AreEqual(Difficulty.Turn, loaded.Difficulty);
            Assert.AreEqual(57, loaded.LastPuzzle, "引き継いだ級の記録になっていません");
            Assert.IsTrue(loaded.TryGet(57, out var entry));
            Assert.AreEqual(4, entry.checkCount);
            Assert.AreEqual(0, loaded.RecordOf(Difficulty.Classic).puzzles.Count, "別の級に入っています");
        }

        [Test]
        public void 言語を変えても進捗は消えない()
        {
            var progress = _store.Load();
            progress.GetOrCreate(10).hintCount = 2;

            progress.Language = Language.German;

            Assert.AreEqual(1, progress.Current.puzzles.Count);
            Assert.AreEqual("de", progress.language);
        }

        [Test]
        public void 壊れたファイルは初期状態として読む()
        {
            File.WriteAllText(_path, "{ これは JSON ではない");
            var progress = _store.Load();
            Assert.IsNotNull(progress);
            Assert.AreEqual(0, progress.Current.puzzles.Count);
        }

        [Test]
        public void セッションの状態を保存して復元できる()
        {
            var library = GameData.Puzzles;
            var database = GameData.Postures;

            Puzzle puzzle = null;
            foreach (var candidate in library.All)
            {
                if (candidate.Level != 5) continue;
                puzzle = candidate;
                break;
            }
            Assert.IsNotNull(puzzle);

            var session = new PuzzleSession(puzzle, database, Difficulty.Guided);
            var first = new List<Placement>(puzzle.Hidden)[0];
            Assert.IsTrue(session.TryPlace(first.Piece, first.Row, first.Col));
            session.Check();

            var progress = _store.Load();
            ProgressStore.Capture(progress, session);
            Assert.IsTrue(_store.Save(progress));

            var loaded = _store.Load();
            var restored = new PuzzleSession(puzzle, database, Difficulty.Guided);
            Assert.IsTrue(ProgressStore.Restore(loaded, restored));

            Assert.AreEqual(session.Board.ToText(), restored.Board.ToText());
            Assert.AreEqual(session.CheckCount, restored.CheckCount);
            Assert.AreEqual(session.HintCount, restored.HintCount);
            // 遊んでいた級（Guided）の記録に入る。いま選んでいる級とは別。
            Assert.AreEqual(puzzle.Number, loaded.RecordOf(Difficulty.Guided).lastPuzzle);
        }

        [Test]
        public void 保存が無い問題の復元は何もしない()
        {
            var session = new PuzzleSession(GameData.Puzzles[7], GameData.Postures, Difficulty.Guided);
            Assert.IsFalse(ProgressStore.Restore(_store.Load(), session));
        }
    }
}
