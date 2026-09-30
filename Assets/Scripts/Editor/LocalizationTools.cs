using System.Text;
using Pentomino.Core;
using Pentomino.Data;
using UnityEditor;
using UnityEngine;

namespace Pentomino.EditorTools
{
    /// <summary>説明書の翻訳がどこまで進んでいるかを調べるツール。</summary>
    public static class LocalizationTools
    {
        /// <summary>この印が残っている雛形は未翻訳とみなす。</summary>
        /// <summary>この印が残っている雛形は未翻訳とみなす。</summary>
        public const string UntranslatedMark = ManualText.UntranslatedMark;

        [MenuItem("Pentomino/説明書の翻訳状況を調べる")]
        public static void CheckManuals()
        {
            var report = new StringBuilder();
            report.AppendLine("説明書の翻訳状況");
            report.AppendLine("置き場所: Assets/Resources/" + ManualText.ResourceFolder + "/");
            report.AppendLine();

            var done = 0;
            var problems = 0;

            foreach (var language in Languages.All)
            {
                var file = ManualText.FileName(language) + ".txt";
                var template = GameData.LoadManualTemplate(language);

                if (template == null)
                {
                    report.AppendLine("  × " + file + "  … ファイルがありません");
                    problems++;
                    continue;
                }

                if (!ManualText.HasAllTokens(template, out var missingToken))
                {
                    report.AppendLine("  × " + file + "  … 差し込み記号 " + missingToken + " が消えています");
                    problems++;
                    continue;
                }

                if (ManualText.IsUntranslated(template))
                {
                    report.AppendLine("  … " + file + "  … 未翻訳（英語のまま）");
                    continue;
                }

                report.AppendLine("  ○ " + file + "  … 翻訳済み");
                done++;
            }

            report.AppendLine();
            report.AppendLine("翻訳済み " + done + " / " + Languages.All.Length + " 言語");

            if (problems > 0)
            {
                report.AppendLine();
                report.AppendLine("× の付いたファイルは表示に支障が出ます。");
                report.AppendLine("差し込み記号（{TOTAL} と {TABLE}）は消さずに残してください。");
                Debug.LogWarning(report.ToString());
                return;
            }

            Debug.Log(report.ToString());
        }

        /// <summary>
        /// 翻訳した雛形の差し込み記号だけを点検する。
        ///
        /// 翻訳にかけると {GUIDED} が {GUIADO} のように記号ごと訳されてしまうことがある。
        /// 記号は「どこに何を差し込むか」の目印なので、どの言語でも英語のまま残さないと
        /// 置き換えが起きず、画面に {GUIADO} がそのまま出てしまう。
        /// </summary>
        [MenuItem("Pentomino/翻訳の記号を検査")]
        public static void CheckTokens()
        {
            var report = new StringBuilder();
            report.AppendLine("説明書の差し込み記号の検査");
            report.AppendLine("置き場所: Assets/Resources/" + ManualText.ResourceFolder + "/");
            report.AppendLine();

            var problems = 0;

            foreach (var language in Languages.All)
            {
                var file = ManualText.FileName(language) + ".txt";
                var template = GameData.LoadManualTemplate(language);

                if (template == null)
                {
                    report.AppendLine("  × " + file + "  … ファイルがありません");
                    problems++;
                    continue;
                }

                var unknown = ManualText.FindUnknownTokens(template);
                var missing = ManualText.FindMissingTokens(template);

                if (unknown.Count == 0 && missing.Count == 0)
                {
                    report.AppendLine("  ○ " + file);
                    continue;
                }

                report.AppendLine("  × " + file);
                problems++;

                if (unknown.Count > 0)
                {
                    report.AppendLine("      訳されてしまった記号: " + string.Join(" ", unknown.ToArray()));
                    report.AppendLine("      → 中身を英語に戻してください。周りの訳文はそのままで構いません。");
                }

                if (missing.Count > 0)
                    report.AppendLine("      消えている記号: " + string.Join(" ", missing.ToArray()));
            }

            report.AppendLine();
            report.AppendLine("使える記号: " + string.Join(" ", ManualText.AllTokens));

            if (problems > 0)
            {
                report.AppendLine();
                report.AppendLine("× の付いたファイルは、画面に記号がそのまま出ます。");
                Debug.LogWarning(report.ToString());
                return;
            }

            report.AppendLine("すべての言語で記号は正しく残っています。");
            Debug.Log(report.ToString());
        }

        /// <summary>
        /// 訳文が壊れていないかを調べる。
        ///
        /// 訳を貼り付けて保存するとき、文字コードを間違えると字が「?」に変わる。
        /// 豆腐（字形が無いだけ）と違って文字そのものが消えるので、後から戻せない。
        /// 保存した直後にこれを走らせれば、その場で気づける。
        /// </summary>
        [MenuItem("Pentomino/翻訳が壊れていないか調べる")]
        public static void CheckTranslations()
        {
            var report = new StringBuilder();
            report.AppendLine("訳文の健全性");
            report.AppendLine("置き場所: Assets/Resources/" + ManualText.ResourceFolder + "/");
            report.AppendLine();

            var problems = 0;

            foreach (var language in Languages.All)
            {
                var file = ManualText.FileName(language) + ".txt";
                var template = GameData.LoadManualTemplate(language);

                if (template == null)
                {
                    report.AppendLine("  × " + file + "  … ファイルがありません");
                    problems++;
                    continue;
                }

                var translated = !ManualText.IsUntranslated(template);
                var body = ManualText.StripCommentsForCheck(template);

                var found = TranslationCheck.Problems(language, body, translated);
                if (found.Count == 0)
                {
                    report.AppendLine("  ○ " + file + (translated ? "" : "  … 未翻訳"));
                    continue;
                }

                report.AppendLine("  × " + file);
                foreach (var problem in found) report.AppendLine("      " + problem);
                problems += found.Count;
            }

            report.AppendLine();
            report.AppendLine("訳文を貼り付けたら、必ず UTF-8 で保存してください。");
            report.AppendLine("Shift-JIS や ANSI で保存すると、その文字コードに無い字が ? に変わります。");

            if (problems > 0)
            {
                Debug.LogWarning(report.ToString());
                return;
            }

            Debug.Log(report.ToString());
        }

        [MenuItem("Pentomino/説明書の表示を確かめる")]
        public static void PreviewManuals()
        {
            var previous = Strings.Current;
            try
            {
                foreach (var language in Languages.All)
                {
                    Strings.Current = language;
                    var body = GameData.LoadManual(language);
                    var head = body.Length > 200 ? body.Substring(0, 200) + " …" : body;
                    Debug.Log("[" + language.ToCode() + "] " + body.Length + " 文字\n" + head);
                }
            }
            finally
            {
                Strings.Current = previous;
            }
        }
    }
}
