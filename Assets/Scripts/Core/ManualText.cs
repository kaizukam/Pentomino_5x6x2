using System.Collections.Generic;
using System.Text;

namespace Pentomino.Core
{
    /// <summary>
    /// 説明書の本文を組み立てる（開発仕様「説明/Manual」）。
    ///
    /// 本文そのものは Assets/Resources/Manual/manual_&lt;言語コード&gt;.txt に置いてあり、
    /// このクラスは読み込んだ雛形の差し込み記号を実際の値に置き換えるだけ。
    /// 翻訳はテキストファイルを直せば済み、コードには触れなくてよい。
    /// </summary>
    public static class ManualText
    {
        /// <summary>雛形ファイルを置く Resources 内のフォルダ。</summary>
        public const string ResourceFolder = "Manual";

        /// <summary>参考文献の本文を置く場所（Resources から見た道）。</summary>
        public const string ReferenceResourcePath = ResourceFolder + "/reference";

        /// <summary>書体の権利表示を置く場所（Resources から見た道）。</summary>
        public const string LicenseResourcePath = ResourceFolder + "/licenses";

        /// <summary>行頭がこれで始まる行は注記とみなし、本文から取り除く。</summary>
        public const string CommentPrefix = "//";

        // 差し込み記号
        public const string TotalToken = "{TOTAL}";
        public const string TableToken = "{TABLE}";
        public const string GuidedToken = "{GUIDED}";
        public const string TurnToken = "{TURN}";
        public const string ClassicToken = "{CLASSIC}";
        public const string GuidedNoteToken = "{GUIDED_NOTE}";
        public const string TurnNoteToken = "{TURN_NOTE}";
        public const string ClassicNoteToken = "{CLASSIC_NOTE}";

        /// <summary>
        /// 参考文献の本体。原稿は HTML で、Editor の取り込み道具が
        /// Resources/Manual/reference.txt に写す。
        /// 書名は元の言語のまま並ぶので、訳し分けはしない。
        /// </summary>
        public const string ReferenceToken = "{Reference}";

        /// <summary>表の見出しの行で、列を区切る文字。</summary>
        public const char ColumnSeparator = '|';

        /// <summary>
        /// 説明書の末尾に付ける、書体の権利表示。
        ///
        /// 書体は SIL Open Font License で、商用利用も埋め込みも許されているが、
        /// ライセンス本文を添えることを求めている。中身は英語のまま出す。
        /// 訳しても法的な意味は変わらず、原文でなければ根拠にならない。
        /// 見出しだけはその言語にする。
        /// </summary>
        public static string LicenseSection(Language language, string licenses)
        {
            if (string.IsNullOrEmpty(licenses)) return string.Empty;

            return "\n\n■ " + Strings.Get(StringId.Licenses, language)
                   + "\n" + licenses.TrimEnd('\n');
        }

        /// <summary>雛形に書ける差し込み記号の一覧。</summary>
        public static readonly string[] AllTokens =
        {
            TotalToken, TableToken,
            GuidedToken, TurnToken, ClassicToken,
            GuidedNoteToken, TurnNoteToken, ClassicNoteToken,
            ReferenceToken,
        };

        /// <summary>
        /// 雛形に書かれている「{...}」をすべて拾う。注記の行は数えない。
        /// </summary>
        public static List<string> FindTokens(string template)
        {
            var found = new List<string>();
            if (string.IsNullOrEmpty(template)) return found;

            var body = StripComments(template);

            var i = 0;
            while (i < body.Length)
            {
                var open = body.IndexOf('{', i);
                if (open < 0) break;

                var close = body.IndexOf('}', open + 1);
                if (close < 0) break;

                var token = body.Substring(open, close - open + 1);
                if (!found.Contains(token)) found.Add(token);
                i = close + 1;
            }
            return found;
        }

        /// <summary>
        /// 差し込み記号として知らないものを返す。
        ///
        /// 翻訳にかけると {GUIDED} が {GUIADO} のように訳されてしまうことがある。
        /// 記号は「どこに何を差し込むか」の目印なので、どの言語でも英語のまま残す必要がある。
        /// </summary>
        public static List<string> FindUnknownTokens(string template)
        {
            var unknown = new List<string>();
            foreach (var token in FindTokens(template))
            {
                if (System.Array.IndexOf(AllTokens, token) >= 0) continue;
                unknown.Add(token);
            }
            return unknown;
        }

        /// <summary>雛形に書かれていない差し込み記号を返す（訳し忘れ・消し忘れの検出）。</summary>
        public static List<string> FindMissingTokens(string template)
        {
            var present = FindTokens(template);

            var missing = new List<string>();
            foreach (var token in AllTokens)
            {
                if (present.Contains(token)) continue;
                missing.Add(token);
            }
            return missing;
        }

        /// <summary>まだ訳していない雛形に付ける印。</summary>
        public const string UntranslatedMark = "TRANSLATE ME";

        /// <summary>
        /// この雛形がまだ訳されていないか。
        ///
        /// 印が付いた行そのものを探す。注記の説明文にも同じ語が出てくるので、
        /// ファイル全体から文字列を探すと、訳し終えた後も未翻訳と判定されてしまう。
        /// </summary>
        public static bool IsUntranslated(string template)
        {
            if (string.IsNullOrEmpty(template)) return false;

            foreach (var line in template.Split('\n'))
            {
                var text = line.Trim();
                if (!text.StartsWith(CommentPrefix)) continue;

                text = text.Substring(CommentPrefix.Length).Trim();
                if (text.StartsWith(UntranslatedMark)) return true;
            }
            return false;
        }

        /// <summary>雛形ファイルの名前。言語コードを含む。</summary>
        public static string FileName(Language language) => "manual_" + language.ToCode();

        /// <summary>Resources.Load に渡すパス。</summary>
        public static string ResourcePath(Language language) => ResourceFolder + "/" + FileName(language);

        /// <summary>
        /// 雛形の差し込み記号を実際の値に置き換え、注記の行を取り除く。
        /// </summary>
        public static string Format(string template, Language language, int[] countsByLevel) =>
            Format(template, language, countsByLevel, null);

        /// <summary>
        /// 参考文献の本体も差し込んで組み立てる。
        /// 用意が無ければ、その場所は空になる（見出しだけが残る）。
        /// </summary>
        public static string Format(string template, Language language, int[] countsByLevel,
            string reference)
        {
            if (string.IsNullOrEmpty(template)) return string.Empty;

            var body = StripComments(template);

            body = body.Replace(TotalToken, Total(countsByLevel).ToString());
            body = body.Replace(TableToken, BuildLevelTable(countsByLevel));
            body = body.Replace(ReferenceToken, reference ?? string.Empty);

            body = body.Replace(GuidedToken, Strings.GradeName(Difficulty.Guided, language));
            body = body.Replace(TurnToken, Strings.GradeName(Difficulty.Turn, language));
            body = body.Replace(ClassicToken, Strings.GradeName(Difficulty.Classic, language));

            body = body.Replace(GuidedNoteToken, Strings.GradeNote(Difficulty.Guided, language));
            body = body.Replace(TurnNoteToken, Strings.GradeNote(Difficulty.Turn, language));
            body = body.Replace(ClassicNoteToken, Strings.GradeNote(Difficulty.Classic, language));

            return body;
        }

        /// <summary>
        /// 本文を「表の前」と「表の後」に切り分ける。
        /// 表は等幅でない書体だと桁が揃わないので、画面側で本物の表として組む。
        /// </summary>
        public static bool TrySplitAtTable(string template, Language language, int[] countsByLevel,
            out string before, out string after) =>
            TrySplitAtTable(template, language, countsByLevel, null, out before, out after);

        /// <summary>
        /// 参考文献の本体も差し込んだうえで、表の前後に分ける。
        /// 画面はこちらを使う。表は本物の表として別に組むので、本文とは切り離す。
        /// </summary>
        public static bool TrySplitAtTable(string template, Language language, int[] countsByLevel,
            string reference, out string before, out string after)
        {
            before = null;
            after = null;
            if (string.IsNullOrEmpty(template)) return false;

            var body = StripComments(template);
            var index = body.IndexOf(TableToken, System.StringComparison.Ordinal);
            if (index < 0)
            {
                before = FormatTokens(body, language, countsByLevel, reference);
                after = string.Empty;
                return false;
            }

            before = FormatTokens(body.Substring(0, index), language, countsByLevel, reference)
                .TrimEnd('\n');
            after = FormatTokens(body.Substring(index + TableToken.Length), language, countsByLevel,
                    reference)
                .TrimStart('\n');
            return true;
        }

        /// <summary>
        /// 表の直前の行が「レベル | 問題番号 | 割合」の形なら、それを表の見出しとして取り出す。
        /// 本文からはその行を取り除いて返す。見出しの行が無ければ本文はそのまま。
        ///
        /// 見出しも表と同じ列組みで並べないと、書体によって桁がずれてしまう。
        /// </summary>
        /// <summary>全体に占める割合（％）。総数が 0 なら 0。</summary>
        public static double Share(int count, int total) => total <= 0 ? 0.0 : 100.0 * count / total;

        /// <summary>レベル別の問題数を合計する。</summary>
        public static int Total(int[] countsByLevel)
        {
            if (countsByLevel == null) return 0;

            var total = 0;
            for (var level = 1; level < countsByLevel.Length; level++) total += countsByLevel[level];
            return total;
        }

        public static string TakeTableHeader(string before, out string[] header)
        {
            header = null;
            if (string.IsNullOrEmpty(before)) return before;

            var lines = before.Split('\n');

            var last = -1;
            for (var i = lines.Length - 1; i >= 0; i--)
            {
                if (lines[i].Trim('\r', ' ', '\t').Length == 0) continue;
                last = i;
                break;
            }
            if (last < 0) return before;

            var line = lines[last].Trim('\r', ' ', '\t');
            if (line.IndexOf(ColumnSeparator) < 0) return before;

            var fields = line.Split(ColumnSeparator);
            header = new string[fields.Length];
            for (var i = 0; i < fields.Length; i++) header[i] = fields[i].Trim(' ', '\t', '\r');

            return string.Join("\n", lines, 0, last).TrimEnd('\n', '\r', ' ', '\t');
        }

        /// <summary>表以外の差し込み記号だけを置き換える。</summary>
        private static string FormatTokens(string body, Language language, int[] countsByLevel) =>
            FormatTokens(body, language, countsByLevel, null);

        private static string FormatTokens(string body, Language language, int[] countsByLevel,
            string reference)
        {
            body = body.Replace(TotalToken, Total(countsByLevel).ToString());
            body = body.Replace(ReferenceToken, reference ?? string.Empty);

            body = body.Replace(GuidedToken, Strings.GradeName(Difficulty.Guided, language));
            body = body.Replace(TurnToken, Strings.GradeName(Difficulty.Turn, language));
            body = body.Replace(ClassicToken, Strings.GradeName(Difficulty.Classic, language));

            body = body.Replace(GuidedNoteToken, Strings.GradeNote(Difficulty.Guided, language));
            body = body.Replace(TurnNoteToken, Strings.GradeNote(Difficulty.Turn, language));
            body = body.Replace(ClassicNoteToken, Strings.GradeNote(Difficulty.Classic, language));

            return body;
        }

        /// <summary>
        /// そのレベルの問題番号の範囲（"1～4" のような字）。
        ///
        /// 問題集はレベルの順に並んでいる（L1 が 1〜4、L2 が 5〜10、…）ので、
        /// 手前のレベルの問題数を足していけば、その先頭の番号が出る。
        /// 並びが崩れるとここが嘘になるので、PuzzleLibraryTests で並びを確かめている。
        /// </summary>
        public static string NumberRange(int[] countsByLevel, int level)
        {
            if (countsByLevel == null || level < 1 || level >= countsByLevel.Length) return string.Empty;

            var first = 1;
            for (var i = 1; i < level; i++) first += countsByLevel[i];
            var last = first + countsByLevel[level] - 1;

            if (countsByLevel[level] <= 0) return "-";
            return first == last ? first.ToString() : first + RangeDash + last;
        }

        /// <summary>範囲の区切り。数字だけなので、どの言語でもそのまま使える。</summary>
        public const string RangeDash = "～";

        /// <summary>
        /// レベルごとの表。レベル・そのレベルの問題番号・割合の 3 列。
        /// 数字だけなので、どの言語でもそのまま使える。
        /// </summary>
        public static string BuildLevelTable(int[] countsByLevel)
        {
            var total = Total(countsByLevel);
            var sb = new StringBuilder();
            for (var level = 1; level < countsByLevel.Length; level++)
            {
                var count = countsByLevel[level];
                sb.AppendLine(string.Format("  L{0,-4}{1,12}{2,9:0.00}%",
                    level, NumberRange(countsByLevel, level), Share(count, total)));
            }
            sb.Append(string.Format("  {0,-5}{1,12}{2,9:0.00}%", "-", total, 100.0));
            return sb.ToString();
        }

        /// <summary>注記を除いた本文（検査ツール用）。</summary>
        public static string StripCommentsForCheck(string template) => StripComments(template);

        private static string StripComments(string template)
        {
            var sb = new StringBuilder(template.Length);
            foreach (var line in template.Split('\n'))
            {
                var trimmed = line.TrimStart();
                if (trimmed.StartsWith(CommentPrefix)) continue;
                sb.Append(line).Append('\n');
            }
            return sb.ToString().TrimEnd('\n');
        }

        /// <summary>雛形に必要な差し込み記号が残っているかを調べる（検証ツール用）。</summary>
        public static bool HasAllTokens(string template, out string missing)
        {
            missing = null;
            if (template == null) return false;

            foreach (var token in new[] { TotalToken, TableToken })
            {
                if (template.Contains(token)) continue;
                missing = token;
                return false;
            }
            return true;
        }
    }
}
