namespace Pentomino.Core
{
    /// <summary>
    /// ピースがはめ込み位置に吸い付くかどうかの判定（開発仕様「ピースのはめ込み」）。
    ///
    /// 置ける場所に近づくと吸い込まれ、指がそこから離れると指位置に戻る。
    /// 吸い付く距離より離す距離を大きく取ってあるので、境目でちらつかない。
    /// </summary>
    public static class SnapRule
    {
        /// <summary>吸い込まれる距離の既定値（画面の基準ピクセル）。</summary>
        public const float DefaultAttractDistance = 16f;

        /// <summary>吸い付きが外れる距離の既定値。</summary>
        public const float DefaultReleaseDistance = 24f;

        /// <summary>
        /// 吸い付いた状態を続けるべきか。
        /// </summary>
        /// <param name="wasSnapped">直前に吸い付いていたか。</param>
        /// <param name="distance">指の位置と、はめ込み位置との距離。</param>
        /// <param name="attractDistance">吸い込まれる距離。</param>
        /// <param name="releaseDistance">吸い付きが外れる距離。attractDistance 以上であること。</param>
        public static bool ShouldSnap(bool wasSnapped, float distance,
            float attractDistance = DefaultAttractDistance,
            float releaseDistance = DefaultReleaseDistance)
        {
            if (distance < 0f) return false;
            if (releaseDistance < attractDistance) releaseDistance = attractDistance;

            // 吸い付いている間は、離す距離に届くまで保つ。
            return wasSnapped ? distance < releaseDistance : distance <= attractDistance;
        }
    }
}
