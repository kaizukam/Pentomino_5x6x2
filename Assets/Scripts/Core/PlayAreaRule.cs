namespace Pentomino.Core
{
    /// <summary>
    /// 運んでいるピースを遊び面（BOX とその下の待機場所）から出さない規則。
    ///
    /// ボタンの真下でピースを置くので、指が上へ流れると帯に掛かり、
    /// 離した拍子に Hint を押してしまうことがあった。ピースが帯の上へ
    /// 行けなければ、指も帯には届かない。左右も同じで、柱の外へは出さない。
    ///
    /// GrabRule と同じく、判定はここに置き、動かすのは View 側が行う。
    /// 座標系は上が正。y は「上端」のほうが「下端」より大きい。
    /// </summary>
    public static class PlayAreaRule
    {
        /// <summary>
        /// ピースをどれだけ動かせば遊び面に収まるかを返す。収まっていれば 0。
        ///
        /// 遊び面より大きいピースは無い前提だが、万一そうなら
        /// 左端と上端を合わせる。上端が帯に掛からないことを優先する。
        /// </summary>
        /// <param name="left">ピースの左端</param>
        /// <param name="right">ピースの右端</param>
        /// <param name="bottom">ピースの下端</param>
        /// <param name="top">ピースの上端</param>
        /// <param name="areaLeft">遊び面の左端</param>
        /// <param name="areaRight">遊び面の右端</param>
        /// <param name="areaBottom">遊び面の下端</param>
        /// <param name="areaTop">遊び面の上端（BOX の上端）</param>
        public static void Correction(
            float left, float right, float bottom, float top,
            float areaLeft, float areaRight, float areaBottom, float areaTop,
            out float dx, out float dy)
        {
            dx = Shift(left, right, areaLeft, areaRight);
            dy = -Shift(-top, -bottom, -areaTop, -areaBottom);
        }

        /// <summary>
        /// [min, max] を [areaMin, areaMax] に収めるずらし量。
        /// 収まらないほど大きければ min 側を揃える。
        /// </summary>
        private static float Shift(float min, float max, float areaMin, float areaMax)
        {
            if (min < areaMin) return areaMin - min;
            if (max > areaMax) return System.Math.Max(areaMax - max, areaMin - min);
            return 0f;
        }
    }
}
