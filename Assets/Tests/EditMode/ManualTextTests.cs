using NUnit.Framework;
using Pentomino.Core;
using Pentomino.Data;

namespace Pentomino.Tests
{
    /// <summary>
    /// 説明書の雛形が揃っていて、差し込み記号が正しく置き換わることを確かめる。
    /// 翻訳そのものの中身は見ない（未翻訳でもアプリは動くべきなので）。
    /// </summary>
    public class ManualTextTests
    {
        [TearDown]
        public void TearDown() => Strings.Current = Language.Japanese;

        [Test]
        public void 全言語の雛形が用意されている()
        {
            foreach (var language in Languages.All)
            {
                var template = GameData.LoadManualTemplate(language);
                Assert.IsNotNull(template,
                    "Resources/" + ManualText.ResourcePath(language) + " がありません");
                Assert.Greater(template.Length, 100, language.ToCode() + " の雛形が短すぎます");
            }
        }

        [Test]
        public void 全言語で差し込み記号が残っている()
        {
            foreach (var language in Languages.All)
            {
                var template = GameData.LoadManualTemplate(language);
                Assert.IsTrue(ManualText.HasAllTokens(template, out var missing),
                    language.ToCode() + " で " + missing + " が消えています");
            }
        }

        [Test]
        public void 組み立てた本文に記号が残らない()
        {
            foreach (var language in Languages.All)
            {
                var body = GameData.LoadManual(language);
                Assert.IsFalse(body.Contains("{"),
                    language.ToCode() + " に置き換え漏れの記号があります");
                // 注記は「行頭が //」の行。本文の途中に出てくる // は URL なので、
                // 本文全体から探すと参考文献のリンクに引っかかる。
                foreach (var line in body.Split('\n'))
                {
                    Assert.IsFalse(line.TrimStart().StartsWith(ManualText.CommentPrefix),
                        language.ToCode() + " に注記の行が残っています: " + line);
                }
            }
        }

        [Test]
        public void 問題の総数と表が差し込まれる()
        {
            var body = GameData.LoadManual(Language.Japanese);
            StringAssert.Contains("2339", body);
            StringAssert.Contains("L1", body);
            StringAssert.Contains("L10", body);
        }

        [Test]
        public void 難易度の名前が差し込まれる()
        {
            // 級の名前は言語ごとに訳す。差し込まれずに {CLASSIC} のまま
            // 残っていないか、名前と食い違っていないかを見る。
            foreach (var language in Languages.All)
            {
                var body = GameData.LoadManual(language);

                foreach (var difficulty in DifficultyRules.All)
                    StringAssert.Contains(Strings.GradeName(difficulty, language), body,
                        difficulty + " / " + language);

                StringAssert.DoesNotContain("{CLASSIC}", body, language.ToString());
                StringAssert.DoesNotContain("{GUIDED}", body, language.ToString());
                StringAssert.DoesNotContain("{TURN}", body, language.ToString());
            }
        }

        [Test]
        public void レベル表の合計が問題数と一致する()
        {
            var counts = GameData.Puzzles.CountsByLevel();

            Assert.AreEqual(GameData.Puzzles.Count, ManualText.Total(counts),
                "レベル別の合計が問題数と合いません");
        }

        [Test]
        public void レベル表は実データを数えた値になる()
        {
            // 問題集を入れ替えても表がずれないよう、数字はデータから数える。
            var counts = GameData.Puzzles.CountsByLevel();

            var expected = new int[counts.Length];
            foreach (var puzzle in GameData.Puzzles.All)
            {
                Assert.Less(puzzle.Level, expected.Length, puzzle.Id + " のレベルが表の範囲外");
                expected[puzzle.Level]++;
            }

            for (var level = 1; level < counts.Length; level++)
                Assert.AreEqual(expected[level], counts[level], "L" + level + " の問題数");
        }

        [Test]
        public void レベル表の真ん中は問題番号の範囲になる()
        {
            var counts = new[] { 0, 4, 6, 140 };

            Assert.AreEqual("1～4", ManualText.NumberRange(counts, 1));
            Assert.AreEqual("5～10", ManualText.NumberRange(counts, 2));
            Assert.AreEqual("11～150", ManualText.NumberRange(counts, 3));

            Assert.AreEqual("1", ManualText.NumberRange(new[] { 0, 1, 2 }, 1), "1 問だけなら番号ひとつ");
            Assert.AreEqual("-", ManualText.NumberRange(new[] { 0, 0, 2 }, 1), "0 問なら空");
            Assert.AreEqual(string.Empty, ManualText.NumberRange(counts, 4), "表の外");
        }

        [Test]
        public void レベル表の範囲は実データの番号と一致する()
        {
            var library = GameData.Puzzles;
            var counts = library.CountsByLevel();

            for (var level = 1; level < counts.Length; level++)
            {
                if (counts[level] == 0) continue;

                int first = int.MaxValue, last = 0;
                foreach (var puzzle in library.All)
                {
                    if (puzzle.Level != level) continue;
                    if (puzzle.Number < first) first = puzzle.Number;
                    if (puzzle.Number > last) last = puzzle.Number;
                }

                Assert.AreEqual(first + ManualText.RangeDash + last, ManualText.NumberRange(counts, level),
                    "L" + level + " の範囲");
            }
        }

        [Test]
        public void 本文の総数は実データの問題数になる()
        {
            var manual = GameData.LoadManual(Language.Japanese);
            StringAssert.Contains(GameData.Puzzles.Count.ToString(), manual);
        }

        [Test]
        public void 差し込み記号が訳されていたら見つかる()
        {
            // 翻訳にかけると {GUIDED} が {GUIADO} のように記号ごと訳されることがある。
            var template = "・{GUIADO} ― {GUIDED_NOTE}\n{TABLE}";

            var unknown = ManualText.FindUnknownTokens(template);
            CollectionAssert.Contains(unknown, "{GUIADO}");
            CollectionAssert.DoesNotContain(unknown, "{GUIDED_NOTE}");
        }

        [Test]
        public void 全言語の雛形に知らない記号が無い()
        {
            foreach (var language in Languages.All)
            {
                var template = GameData.LoadManualTemplate(language);
                Assert.IsNotNull(template, ManualText.FileName(language) + " が読めません");

                var unknown = ManualText.FindUnknownTokens(template);
                Assert.IsEmpty(unknown,
                    ManualText.FileName(language) + " に知らない記号があります: "
                    + string.Join(" ", unknown.ToArray()));

                var missing = ManualText.FindMissingTokens(template);
                Assert.IsEmpty(missing,
                    ManualText.FileName(language) + " から記号が消えています: "
                    + string.Join(" ", missing.ToArray()));
            }
        }

        [Test]
        public void 参考文献が差し込まれる()
        {
            var formatted = ManualText.Format("■ 参考文献\n{Reference}\n",
                Language.Japanese, GameData.Puzzles.CountsByLevel(), "・ある本\nISBN:123");

            StringAssert.Contains("・ある本", formatted);
            StringAssert.Contains("ISBN:123", formatted);
            StringAssert.DoesNotContain("{Reference}", formatted);
        }

        [Test]
        public void 参考文献の用意が無ければ見出しだけ残る()
        {
            var formatted = ManualText.Format("■ 参考文献\n{Reference}\n",
                Language.Japanese, GameData.Puzzles.CountsByLevel(), null);

            StringAssert.Contains("■ 参考文献", formatted);
            StringAssert.DoesNotContain("{Reference}", formatted);
        }

        [Test]
        public void 参考文献の本文が用意されている()
        {
            // DataBase の HTML から取り込んだ写し。
            // メニュー Pentomino ▸ 参考文献を取り込む が作る。
            var reference = GameData.LoadReference();

            Assert.IsNotEmpty(reference, "Resources/Manual/reference.txt がありません");
            StringAssert.Contains("Pentomino", reference);
        }

        [Test]
        public void どの言語の説明書にも参考文献が入る()
        {
            foreach (var language in Languages.All)
            {
                var manual = GameData.LoadManual(language);

                StringAssert.DoesNotContain("{Reference}", manual,
                    ManualText.FileName(language) + " に記号が残っています");
                StringAssert.Contains("ISBN", manual,
                    ManualText.FileName(language) + " に参考文献が入っていません");
            }
        }

        [Test]
        public void どの言語の説明書にも書体の権利表示が入る()
        {
            // 書体は SIL Open Font License で、本文を添えることを求めている。
            // 有償で売るなら、なおさら落とせない。
            foreach (var language in Languages.All)
            {
                var manual = GameData.LoadManual(language);

                StringAssert.Contains(Strings.Get(StringId.Licenses, language), manual,
                    ManualText.FileName(language) + " に見出しがありません");
                StringAssert.Contains("SIL OPEN FONT LICENSE", manual,
                    ManualText.FileName(language) + " にライセンス本文がありません");
                StringAssert.Contains("Noto Sans", manual,
                    ManualText.FileName(language) + " に書体の権利表示がありません");
            }
        }

        [Test]
        public void 権利表示の用意が無ければ何も足さない()
        {
            Assert.AreEqual(string.Empty, ManualText.LicenseSection(Language.Japanese, null));
            Assert.AreEqual(string.Empty, ManualText.LicenseSection(Language.Japanese, string.Empty));
        }

        [Test]
        public void 注記の行は表示されない()
        {
            var formatted = ManualText.Format("// これは注記\n本文\n  // 字下げした注記\n終わり",
                Language.Japanese, GameData.Puzzles.CountsByLevel());
            Assert.AreEqual("本文\n終わり", formatted);
        }
    }
}
