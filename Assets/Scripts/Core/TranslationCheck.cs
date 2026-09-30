using System.Collections.Generic;

namespace Pentomino.Core
{
    /// <summary>
    /// 翻訳した本文が壊れていないかを調べる。
    ///
    /// 訳文を貼り付けて保存するとき、文字コードを間違えると字が失われる。
    /// UTF-8 の文を Shift-JIS や ANSI で保存すると、その文字コードに無い字が
    /// すべて「?」に置き換わってしまう。豆腐（字形が無いだけ）と違い、
    /// これは文字そのものが消えているので、後から元に戻せない。
    ///
    /// 見分け方は「その言語の字が 1 つも入っていない」こと。
    /// 韓国語の本文にハングルが 1 文字も無ければ、訳し忘れか、壊れたかのどちらか。
    /// </summary>
    public static class TranslationCheck
    {
        /// <summary>読めなかった字の置き換え文字。これが出たら文字コードの取り違え。</summary>
        public const char ReplacementChar = '�';

        /// <summary>この言語らしい字が本文に含まれているか。</summary>
        public static bool HasExpectedScript(Language language, string body)
        {
            if (string.IsNullOrEmpty(body)) return false;

            foreach (var ch in body)
            {
                switch (language)
                {
                    case Language.Japanese:
                        // ひらがな・カタカナ・漢字
                        if (IsIn(ch, '぀', 'ヿ') || IsIn(ch, '一', '鿿')) return true;
                        break;

                    case Language.Korean:
                        // ハングル
                        if (IsIn(ch, '가', '힣') || IsIn(ch, 'ᄀ', 'ᇿ')) return true;
                        break;

                    case Language.ChineseSimplified:
                    case Language.ChineseTraditional:
                        if (IsIn(ch, '一', '鿿')) return true;
                        break;

                    default:
                        // 欧文はラテン文字。英語のままでも成り立つので、常に真とみなす。
                        return true;
                }
            }
            return false;
        }

        /// <summary>この言語の本文に、その言語らしい字が要るか。</summary>
        public static bool NeedsOwnScript(Language language) =>
            language == Language.Japanese
            || language == Language.Korean
            || language == Language.ChineseSimplified
            || language == Language.ChineseTraditional;

        /// <summary>半角の「?」の個数。日本語や中国語の本文では「？」を使うので、あれば怪しい。</summary>
        public static int CountAsciiQuestionMarks(string body)
        {
            if (string.IsNullOrEmpty(body)) return 0;

            var count = 0;
            foreach (var ch in body)
                if (ch == '?') count++;
            return count;
        }

        /// <summary>読めなかった字の個数。</summary>
        public static int CountReplacementChars(string body)
        {
            if (string.IsNullOrEmpty(body)) return 0;

            var count = 0;
            foreach (var ch in body)
                if (ch == ReplacementChar) count++;
            return count;
        }

        /// <summary>
        /// 見つかった不具合を並べる。空なら問題なし。
        /// </summary>
        /// <param name="translated">
        /// 訳し終わっている本文として調べるか。
        /// 未翻訳（英語のまま）の雛形にその言語の字を求めても意味が無い。
        /// </param>
        public static List<string> Problems(Language language, string body, bool translated)
        {
            var problems = new List<string>();
            if (string.IsNullOrEmpty(body))
            {
                problems.Add("本文が空です");
                return problems;
            }

            var broken = CountReplacementChars(body);
            if (broken > 0)
                problems.Add("読めない字が " + broken + " 個あります。文字コードが違います");

            if (!translated) return problems;

            if (NeedsOwnScript(language) && !HasExpectedScript(language, body))
            {
                problems.Add("この言語の字が 1 つもありません。"
                             + "保存するときに UTF-8 以外を選ぶと、字がすべて ? に変わります");
            }

            if (NeedsOwnScript(language))
            {
                var marks = CountAsciiQuestionMarks(body);
                if (marks > 0)
                {
                    problems.Add("半角の ? が " + marks + " 個あります。"
                                 + "この言語では全角の ？ を使うので、字が失われた跡かもしれません");
                }
            }

            return problems;
        }

        private static bool IsIn(char ch, char from, char to) => ch >= from && ch <= to;
    }
}
