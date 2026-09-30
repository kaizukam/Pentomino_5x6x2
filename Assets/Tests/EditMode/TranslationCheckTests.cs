using NUnit.Framework;
using Pentomino.Core;
using Pentomino.Data;

namespace Pentomino.Tests
{
    /// <summary>
    /// 訳文が文字コードの取り違えで壊れていないかを調べる仕掛け。
    ///
    /// 豆腐（字形が無いだけ）は文字データが残っているので後から直せるが、
    /// 「?」に変わってしまった字は元に戻せない。貼り付けた直後に気づけることが大事。
    /// </summary>
    public class TranslationCheckTests
    {
        [Test]
        public void 韓国語の本文にハングルがあれば通る()
        {
            var problems = TranslationCheck.Problems(Language.Korean, "조각의 방향을 맞추세요", true);
            Assert.IsEmpty(problems);
        }

        [Test]
        public void 字がすべて疑問符に変わっていたら見つかる()
        {
            // Shift-JIS で保存してしまった韓国語は、こうなる。
            var problems = TranslationCheck.Problems(Language.Korean, "??? ??? ?????", true);

            Assert.IsNotEmpty(problems);
            StringAssert.Contains("この言語の字が 1 つもありません", string.Join("\n", problems.ToArray()));
        }

        [Test]
        public void 読めない字が混ざっていたら見つかる()
        {
            var problems = TranslationCheck.Problems(Language.Japanese, "順番�おり置こう", true);

            Assert.IsNotEmpty(problems);
            StringAssert.Contains("文字コードが違います", string.Join("\n", problems.ToArray()));
        }

        [Test]
        public void 未翻訳の雛形は英語のままでも通る()
        {
            // まだ訳していないファイルに、その言語の字を求めても意味が無い。
            var problems = TranslationCheck.Problems(Language.Korean, "Place each piece as shown", false);
            Assert.IsEmpty(problems);
        }

        [Test]
        public void 欧文は半角の疑問符があっても通る()
        {
            var problems = TranslationCheck.Problems(Language.German, "Wie geht es?", true);
            Assert.IsEmpty(problems);
        }

        [Test]
        public void 日本語に半角の疑問符が混ざると知らせる()
        {
            var problems = TranslationCheck.Problems(Language.Japanese, "順番どおり置こう?", true);

            Assert.IsNotEmpty(problems);
            StringAssert.Contains("半角の ?", string.Join("\n", problems.ToArray()));
        }

        [Test]
        public void 全言語の雛形が壊れていない()
        {
            foreach (var language in Languages.All)
            {
                var template = GameData.LoadManualTemplate(language);
                Assert.IsNotNull(template, ManualText.FileName(language) + " が読めません");

                var translated = !ManualText.IsUntranslated(template);
                var body = ManualText.StripCommentsForCheck(template);

                var problems = TranslationCheck.Problems(language, body, translated);
                Assert.IsEmpty(problems,
                    ManualText.FileName(language) + ": " + string.Join(" / ", problems.ToArray()));
            }
        }

        [Test]
        public void 画面の文言も壊れていない()
        {
            // Strings.cs も同じ危険がある。編集した拍子に文字コードが変わることがある。
            foreach (var language in Languages.All)
            {
                if (!TranslationCheck.NeedsOwnScript(language)) continue;

                var text = Strings.Get(StringId.Settings, language)
                           + Strings.Get(StringId.Difficulty, language)
                           + Strings.Get(StringId.Close, language);

                Assert.IsTrue(TranslationCheck.HasExpectedScript(language, text),
                    language.ToCode() + " の文言にその言語の字がありません");
                Assert.AreEqual(0, TranslationCheck.CountReplacementChars(text),
                    language.ToCode() + " の文言に読めない字があります");
            }
        }
    }
}
