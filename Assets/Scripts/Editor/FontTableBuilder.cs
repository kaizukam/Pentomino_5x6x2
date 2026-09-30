using System.Collections.Generic;
using System.IO;
using Pentomino.Core;
using Pentomino.View;
using UnityEditor;
using UnityEngine;

namespace Pentomino.EditorTools
{
    /// <summary>
    /// 言語ごとのフォントの対応表を、Assets/Fonts/ にある書体から作る。
    ///
    /// Noto Sans は文字体系ごとに別の書体になっている。
    /// 無印が欧文、JP が日本語、KR が韓国語、SC が簡体字、TC が繁体字。
    /// </summary>
    public static class FontTableBuilder
    {
        public const string FontFolder = "Assets/Fonts";
        public const string TablePath = "Assets/Resources/UI/FontTable.asset";

        /// <summary>言語コードと、その言語に使う書体の名前の頭。</summary>
        private static readonly Dictionary<string, string> FamilyByCode = new Dictionary<string, string>
        {
            { "ja", "NotoSansJP" },
            { "ko", "NotoSansKR" },
            { "zh-Hans", "NotoSansSC" },
            { "zh-Hant", "NotoSansTC" },
        };

        /// <summary>欧文（en, es, fr, de）に使う書体。</summary>
        private const string LatinFamily = "NotoSans";

        [MenuItem("Pentomino/フォントの対応表を作る")]
        public static void Build()
        {
            var report = new System.Text.StringBuilder();
            report.AppendLine("言語ごとのフォント対応表");
            report.AppendLine();

            var missing = 0;

            var defaultRegular = Find(LatinFamily, false, report, ref missing);
            var defaultBold = Optional(LatinFamily, true);

            var entries = new List<FontTable.Entry>();
            foreach (var language in Languages.All)
            {
                var code = language.ToCode();
                if (!FamilyByCode.TryGetValue(code, out var family))
                {
                    report.AppendLine("  " + code + "  … 既定（" + LatinFamily + "）を使います");
                    continue;
                }

                var regular = Find(family, false, report, ref missing);
                var bold = Optional(family, true);

                entries.Add(new FontTable.Entry
                {
                    languageCode = code,
                    regular = regular,
                    bold = bold,
                });

                report.AppendLine("  " + code + "  … " + family);
            }

            var table = AssetDatabase.LoadAssetAtPath<FontTable>(TablePath);
            var created = table == null;
            if (created) table = ScriptableObject.CreateInstance<FontTable>();

            table.Assign(defaultRegular, defaultBold, entries.ToArray());

            Directory.CreateDirectory(Path.GetDirectoryName(TablePath));
            if (created) AssetDatabase.CreateAsset(table, TablePath);
            else EditorUtility.SetDirty(table);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            report.AppendLine();
            report.AppendLine((created ? "作りました: " : "更新しました: ") + TablePath);

            if (missing > 0)
            {
                report.AppendLine();
                report.AppendLine("見つからない書体が " + missing + " 件あります。");
                Debug.LogWarning(report.ToString());
                return;
            }

            Debug.Log(report.ToString());
        }

        /// <summary>
        /// 無ければ無いで構わない書体を探す。
        /// 太字は必須ではない。用意が無ければ細字を Unity が太らせる。
        /// </summary>
        private static Font Optional(string family, bool bold)
        {
            var name = family + (bold ? "-Bold" : "-Regular");
            return AssetDatabase.LoadAssetAtPath<Font>(FontFolder + "/" + name + ".ttf");
        }

        /// <summary>Assets/Fonts/ から「NotoSansJP-Bold.ttf」のような名前で探す。</summary>
        private static Font Find(string family, bool bold, System.Text.StringBuilder report, ref int missing)
        {
            var name = family + (bold ? "-Bold" : "-Regular");
            var path = FontFolder + "/" + name + ".ttf";

            var font = AssetDatabase.LoadAssetAtPath<Font>(path);
            if (font == null)
            {
                report.AppendLine("  × " + path + " が見つかりません");
                missing++;
            }
            return font;
        }
    }
}
