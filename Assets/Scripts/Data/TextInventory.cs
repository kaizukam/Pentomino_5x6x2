using System;
using System.Collections.Generic;
using Pentomino.Core;

namespace Pentomino.Data
{
    /// <summary>
    /// このアプリが画面に出しうる文字を、すべて集める。
    ///
    /// 書体は使う文字だけに削ってある（31MB → 1MB）。削る側と、足りているかを
    /// 見張る側が別々に文字を数えていると、いつか食い違う。実際に一度、
    /// 言語の呼び名（한국어 など）が Strings.cs ではなく Language.cs にあったため
    /// 数え漏れた。だから、数えるのはここ一箇所だけにする。
    ///
    ///   Editor の書き出し道具  → ここが返す文字を Tools へ書き出し、書体を削る
    ///   FontCoverageTests      → ここが返す文字が書体に入っているかを調べる
    ///
    /// 集める先は、アプリが実際に読むのと同じ経路（GameData と Strings）。
    /// 文章の出どころが増えたら、ここに足す。
    /// </summary>
    public static class TextInventory
    {
        /// <summary>
        /// 文章には出てこないが、画面には出る文字。
        /// 問題番号（0000）、レベル（L7）、ピース名、成績の桁、表の記号など。
        /// </summary>
        public const string Always = "0123456789LFINPTUVWXYZ%.,:;-+/()[] ";

        /// <summary>画面に出しうる文字。改行やタブは含めない。</summary>
        public static SortedSet<char> All()
        {
            var chars = new SortedSet<char>();

            foreach (var c in Always) chars.Add(c);

            foreach (var language in Languages.All)
            {
                // 説明書。注記は取り除かれ、参考文献も差し込まれた姿。
                Add(chars, GameData.LoadManual(language));

                // 画面の文言。
                foreach (StringId id in Enum.GetValues(typeof(StringId)))
                    Add(chars, Strings.Get(id, language));

                foreach (Difficulty difficulty in Enum.GetValues(typeof(Difficulty)))
                {
                    Add(chars, Strings.GradeName(difficulty, language));
                    Add(chars, Strings.GradeNote(difficulty, language));
                }

                // 言語を選ぶボタンには、その言語の呼び名がその言語で出る。
                Add(chars, language.NativeName());
                Add(chars, language.ToCode());
            }

            return chars;
        }

        /// <summary>集めた文字を、並べた 1 本の文字列にする。</summary>
        public static string AsText()
        {
            var chars = All();
            var buffer = new char[chars.Count];
            chars.CopyTo(buffer);
            return new string(buffer);
        }

        private static void Add(SortedSet<char> chars, string text)
        {
            if (string.IsNullOrEmpty(text)) return;

            foreach (var c in text)
            {
                if (c == '\n' || c == '\r' || c == '\t') continue;
                chars.Add(c);
            }
        }
    }
}
