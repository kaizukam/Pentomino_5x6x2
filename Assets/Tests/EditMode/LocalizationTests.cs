using System;
using System.Collections.Generic;
using NUnit.Framework;
using Pentomino.Core;

namespace Pentomino.Tests
{
    public class LocalizationTests
    {
        [TearDown]
        public void TearDown() => Strings.Current = Language.Japanese;

        [Test]
        public void 対応言語は8種類()
        {
            // 開発仕様「設定モード」:
            // 英語, 日本語, スペイン語, フランス語, ドイツ語, 韓国語, 中国語1, 中国語2
            Assert.AreEqual(8, Languages.All.Length);
            CollectionAssert.AllItemsAreUnique(Languages.All);

            foreach (Language language in Enum.GetValues(typeof(Language)))
                Assert.IsTrue(Languages.IsValid(language), language + " が一覧に無い");
        }

        [Test]
        public void 韓国語は中国語の前に並ぶ()
        {
            var korean = Array.IndexOf(Languages.All, Language.Korean);
            var chinese = Array.IndexOf(Languages.All, Language.ChineseSimplified);

            Assert.Greater(korean, 0, "韓国語が一覧に無い");
            Assert.Less(korean, chinese, "韓国語は中国語より前に並べる");
        }

        [Test]
        public void 言語コードが往復する()
        {
            var codes = new HashSet<string>();
            foreach (var language in Languages.All)
            {
                var code = language.ToCode();
                Assert.IsFalse(string.IsNullOrEmpty(code), language.ToString());
                Assert.IsTrue(codes.Add(code), "コードが重複: " + code);
                Assert.AreEqual(language, Languages.FromCode(code), code);
            }
        }

        [Test]
        public void 未知のコードは日本語になる()
        {
            Assert.AreEqual(Language.Japanese, Languages.FromCode("xx"));
            Assert.AreEqual(Language.Japanese, Languages.FromCode(""));
            Assert.AreEqual(Language.Japanese, Languages.FromCode(null));
        }

        [Test]
        public void 言語名は空でなく重複しない()
        {
            var names = new HashSet<string>();
            foreach (var language in Languages.All)
            {
                var name = language.NativeName();
                Assert.IsFalse(string.IsNullOrEmpty(name), language.ToString());
                Assert.IsTrue(names.Add(name), "言語名が重複: " + name);
            }
        }

        [Test]
        public void 文言表の形が揃っている()
        {
            Assert.IsTrue(Strings.Validate(out var problem), problem);
        }

        [Test]
        public void 全ての文言が全言語で引ける()
        {
            foreach (StringId id in Enum.GetValues(typeof(StringId)))
            {
                foreach (var language in Languages.All)
                {
                    var value = Strings.Get(id, language);
                    Assert.IsFalse(string.IsNullOrEmpty(value), id + " / " + language);
                }
            }
        }

        [Test]
        public void 表に無いIDは名前がそのまま返る()
        {
            Assert.AreEqual("999", Strings.Get((StringId)999, Language.English));
        }

        [Test]
        public void 級の名前は腕前と帯の色()
        {
            Assert.AreEqual("初心者：白帯", Strings.GradeName(Difficulty.Guided, Language.Japanese));
            Assert.AreEqual("中級：黄帯", Strings.GradeName(Difficulty.Turn, Language.Japanese));
            Assert.AreEqual("有段者：黒帯", Strings.GradeName(Difficulty.Classic, Language.Japanese));
        }

        [Test]
        public void 級の名前は言語ごとに訳す()
        {
            Assert.AreEqual("Advanced : Black belt",
                Strings.GradeName(Difficulty.Classic, Language.English));
            Assert.AreNotEqual(Strings.GradeName(Difficulty.Classic, Language.English),
                Strings.GradeName(Difficulty.Classic, Language.German));
        }

        [Test]
        public void 級の名前は同じ言語の中で区別できる()
        {
            foreach (var language in Languages.All)
            {
                var names = new HashSet<string>();
                foreach (var difficulty in DifficultyRules.All)
                    Assert.IsTrue(names.Add(Strings.GradeName(difficulty, language)),
                        language + " で級の名前が重複");
            }
        }

        [Test]
        public void 現在の言語が既定で使われる()
        {
            Strings.Current = Language.French;
            Assert.AreEqual(Strings.Get(StringId.Settings, Language.French), Strings.Get(StringId.Settings));
            Assert.AreEqual(Strings.GradeName(Difficulty.Classic, Language.French),
                Strings.GradeName(Difficulty.Classic));
        }

        [Test]
        public void 進捗データに言語が保存される()
        {
            var progress = new GameProgress();
            foreach (var language in Languages.All)
            {
                progress.Language = language;
                Assert.AreEqual(language.ToCode(), progress.language);
                Assert.AreEqual(language, progress.Language);
            }
        }
    }
}
