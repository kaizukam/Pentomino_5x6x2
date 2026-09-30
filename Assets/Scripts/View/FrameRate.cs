using UnityEngine;

namespace Pentomino.View
{
    /// <summary>
    /// 画面を書き換える速さを、端末の速さに合わせる。
    ///
    /// Unity はスマホとタブレットでは、何も言わなければ毎秒 30 コマで走る。
    /// 電池を持たせるための既定で、絵を見るぶんには足りるが、指の追随には足りない。
    ///
    /// 指の位置は 1 コマに 1 回しか読めない。30 コマなら 33ms に 1 回で、
    /// その間に指は 5mm 以上動く。つまみは飛び飛びに付いてきて重く感じ、
    /// 感度調整の実測にも「毎秒 30 回・ひとコマの動き 0.2mm」と、
    /// 判定のしきい値とは関係のない粗さがそのまま出ていた。
    ///
    /// 画面の書き換え速さに合わせれば、同じ指の動きが 2 倍から 4 倍細かく届く。
    /// 上限を置くのは、120Hz を超える端末で電池を無駄に使わないため。
    /// この遊びは静止画に近く、それ以上速く描いても得るものが無い。
    /// </summary>
    public static class FrameRate
    {
        /// <summary>これより遅くはしない。</summary>
        public const int Lowest = 60;

        /// <summary>これより速くもしない。電池のほうが惜しい。</summary>
        public const int Highest = 120;

        /// <summary>端末の速さが読めなかったときに使う値。</summary>
        public const int Fallback = 60;

        /// <summary>いま指定している速さ。記録と確認用。</summary>
        public static int Current { get; private set; }

        /// <summary>端末の画面に合わせて、書き換えの速さを決める。</summary>
        public static void Raise()
        {
            Current = Choose(Refresh());

            // 垂直同期を待たせると targetFrameRate が効かなくなる。
            QualitySettings.vSyncCount = 0;
            Application.targetFrameRate = Current;
        }

        /// <summary>端末の画面が 1 秒に何回書き換わるか。読めなければ 0。</summary>
        private static int Refresh()
        {
            var ratio = Screen.currentResolution.refreshRateRatio;
            var value = ratio.denominator == 0 ? 0d : ratio.value;

            return value > 0d && value < 1000d ? Mathf.RoundToInt((float)value) : 0;
        }

        /// <summary>読めた速さを、使ってよい範囲に収める。</summary>
        public static int Choose(int refresh) =>
            refresh <= 0 ? Fallback : Mathf.Clamp(refresh, Lowest, Highest);
    }
}
