using Pentomino.Core;
using UnityEngine;

namespace Pentomino.View
{
    /// <summary>
    /// 級ごとの画面の色。帯の色（白・黄・黒）を、画面全体の地の色にする。
    ///
    /// 級の名前はどの言語でも読めるとは限らないが、画面の色なら
    /// 字を読まずに、いまどの級で遊んでいるかが判る。
    ///
    /// 黒帯だけは地が暗いので、その上に置く字と歯車は明るい色にする。
    /// </summary>
    public static class GradeTheme
    {
        /// <summary>白帯（Intro）の地。</summary>
        public static readonly Color White = new Color32(0xF0, 0xF0, 0xF0, 0xFF);

        /// <summary>黄帯（Practice）の地。</summary>
        public static readonly Color Yellow = new Color32(0xF0, 0xD0, 0x10, 0xFF);

        /// <summary>
        /// 黒帯（Classic）の地。
        ///
        /// 真っ黒（#101010）ではピースの黒い外周線が地に溶けるので、
        /// 少し明るい色にして様子を見ている（09-15: #555555 → #444444 → #222244 → #222222）。
        /// 帯の絵（タイトル・級・レベル）の地も同じ色で描いてあるので、
        /// ここを変えるときは絵も差し替える（HeaderLayoutMigration.SourceFolder）。
        /// </summary>
        public static readonly Color Black = new Color32(0x22, 0x22, 0x22, 0xFF);

        /// <summary>画面全体の地の色。</summary>
        public static Color Background(Difficulty difficulty)
        {
            switch (difficulty)
            {
                case Difficulty.Turn: return Yellow;
                case Difficulty.Classic: return Black;
                default: return White;
            }
        }

        /// <summary>地が暗いか。暗ければ字と歯車を明るい色で描く。</summary>
        public static bool IsDark(Difficulty difficulty) => difficulty == Difficulty.Classic;

        /// <summary>地の上に直に置く字の色。</summary>
        public static Color Ink(Difficulty difficulty) => IsDark(difficulty) ? White : Black;
    }
}
