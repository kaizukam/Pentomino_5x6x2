namespace Pentomino.Core
{
    /// <summary>
    /// 掴んでいるピースが指から外れていないかの判定（開発仕様「フリックの操作性」）。
    ///
    /// 反転したあとに、ピースが指から 2〜3cm 離れたまま付いてくることがある。
    /// 原因が何であれ、指の乗っていない物を運んでいるのは操作として成り立たないので、
    /// 外れていたら掴み直したことにして、指の下へ戻す。
    ///
    /// フリックと同じく、判定はここに置き、動かすのは View 側が行う。
    /// 3D 版でも当たり判定の形が変わるだけで、この規則はそのまま使える。
    /// </summary>
    public static class GrabRule
    {
        /// <summary>
        /// 指がピースの外へ出てよい余裕。セル何個分かで表す。
        ///
        /// 0 にすると縁ぎりぎりで細かく引き寄せてしまい、かえって落ち着かない。
        /// 半セルなら、普通に掴んでいる間は一度も働かない。
        /// </summary>
        public const float DefaultSlackCells = 0.5f;

        /// <summary>
        /// ピースをどれだけ動かせば指の下に戻るかを返す。
        /// 外れていなければ 0。座標系はピースから見た左右・上下。
        /// </summary>
        /// <param name="pointerX">指の位置（ピースから見た横）</param>
        /// <param name="pointerY">指の位置（ピースから見た縦）</param>
        /// <param name="left">ピースの左端</param>
        /// <param name="right">ピースの右端</param>
        /// <param name="bottom">ピースの下端</param>
        /// <param name="top">ピースの上端</param>
        /// <param name="slack">はみ出してよい量</param>
        public static void Correction(
            float pointerX, float pointerY,
            float left, float right, float bottom, float top,
            float slack,
            out float dx, out float dy)
        {
            dx = Overshoot(pointerX, left, right, slack);
            dy = Overshoot(pointerY, bottom, top, slack);
        }

        /// <summary>外れていて引き寄せが要るか。</summary>
        public static bool NeedsCorrection(
            float pointerX, float pointerY,
            float left, float right, float bottom, float top,
            float slack)
        {
            Correction(pointerX, pointerY, left, right, bottom, top, slack, out var dx, out var dy);
            return dx != 0f || dy != 0f;
        }

        /// <summary>範囲からはみ出した量。中に入っていれば 0。</summary>
        private static float Overshoot(float value, float min, float max, float slack)
        {
            if (slack < 0f) slack = 0f;

            var low = min - slack;
            var high = max + slack;

            if (value < low) return value - low;
            if (value > high) return value - high;
            return 0f;
        }
    }
}
