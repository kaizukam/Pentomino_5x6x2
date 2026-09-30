using System;

namespace Pentomino.Core
{
    /// <summary>
    /// 対応言語。いつでも変更でき、進捗には影響しない（開発仕様「設定モード」）。
    /// 並びは設定画面での表示順に合わせてある。
    /// </summary>
    public enum Language
    {
        English = 0,
        Japanese = 1,
        Spanish = 2,
        French = 3,
        German = 4,

        /// <summary>韓国語。</summary>
        Korean = 5,

        /// <summary>中国語 1（簡体字）。</summary>
        ChineseSimplified = 6,

        /// <summary>中国語 2（繁体字）。</summary>
        ChineseTraditional = 7,
    }

    public static class Languages
    {
        /// <summary>設定画面に並べる順。</summary>
        public static readonly Language[] All =
        {
            Language.English,
            Language.Japanese,
            Language.Spanish,
            Language.French,
            Language.German,
            Language.Korean,
            Language.ChineseSimplified,
            Language.ChineseTraditional,
        };

        /// <summary>言語コード。保存データと Unity のロケール指定に使う。</summary>
        public static string ToCode(this Language language)
        {
            switch (language)
            {
                case Language.English: return "en";
                case Language.Spanish: return "es";
                case Language.French: return "fr";
                case Language.German: return "de";
                case Language.Korean: return "ko";
                case Language.ChineseSimplified: return "zh-Hans";
                case Language.ChineseTraditional: return "zh-Hant";
                default: return "ja";
            }
        }

        public static Language FromCode(string code)
        {
            if (string.IsNullOrEmpty(code)) return Language.Japanese;

            switch (code)
            {
                case "en": return Language.English;
                case "es": return Language.Spanish;
                case "fr": return Language.French;
                case "de": return Language.German;
                case "ko": return Language.Korean;
                case "zh-Hans": return Language.ChineseSimplified;
                case "zh-Hant": return Language.ChineseTraditional;
                default: return Language.Japanese;
            }
        }

        /// <summary>設定画面のボタンに出す、その言語自身での言語名。</summary>
        public static string NativeName(this Language language)
        {
            switch (language)
            {
                case Language.English: return "English";
                case Language.Spanish: return "Español";
                case Language.French: return "Français";
                case Language.German: return "Deutsch";
                case Language.Korean: return "한국어";
                case Language.ChineseSimplified: return "简体中文";
                case Language.ChineseTraditional: return "繁體中文";
                default: return "日本語";
            }
        }

        public static bool IsValid(Language language) =>
            Array.IndexOf(All, language) >= 0;
    }
}
