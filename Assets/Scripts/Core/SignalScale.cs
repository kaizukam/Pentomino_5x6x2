namespace Pentomino.Core
{
    /// <summary>
    /// 合図の長さ・強さのつまみを、数字に読み替える目盛り（開発仕様「感度調整」）。
    ///
    /// つまみの左から順に、意味の違う三つの区画が並ぶ。
    ///
    ///   1. 端末に任せる   いちばん左。長さも強さも端末が決める
    ///   2. 出さない       広めに取る。ここに入れば合図は出ない
    ///   3. 数字           そこから右へ、1 から上限まで
    ///
    /// 「端末に任せる」と「出さない」を分けたのは、以前どちらも 0 で、
    /// 強さを左いっぱいに寄せると、いちばん静かなつもりが端末の既定
    /// （かなり大きい）で鳴っていたため。左端は端末任せ、その隣は無音、
    /// と目で追える並びにする。
    ///
    /// 「出さない」の区画を広く取るのは、指で確実に入れられるようにするため。
    /// 幅が数ミリしかないと、消したいのに 1 が出てしまう。
    ///
    /// 数字の区画は二乗で伸ばす。深夜に聞こえる程度のごく小さい値を
    /// 選り分けたいのに、まっすぐな目盛りでは 1〜10 が左端の数ミリに
    /// 潰れてしまい、指では狙えない。二乗なら、小さいほうが広く開く。
    /// </summary>
    public static class SignalScale
    {
        /// <summary>端末に任せる、を表す値。</summary>
        public const int DeviceDefault = -1;

        /// <summary>合図を出さない、を表す値。</summary>
        public const int Off = 0;

        /// <summary>ここまでが「端末に任せる」。つまみの左からの割合。</summary>
        public const float DefaultBand = 0.10f;

        /// <summary>ここまでが「出さない」。</summary>
        public const float OffBand = 0.26f;

        /// <summary>数字の区画の曲がり。2 なら二乗。大きいほど小さい値が広がる。</summary>
        public const float Curve = 2f;

        /// <summary>
        /// つまみの位置（0〜1）を数字にする。
        /// </summary>
        /// <param name="position">つまみの位置。左端が 0、右端が 1。</param>
        /// <param name="lowest">数字の区画がここから始まる。</param>
        /// <param name="highest">右端の値。</param>
        public static int ToValue(float position, int lowest, int highest)
        {
            if (position < DefaultBand) return DeviceDefault;
            if (position < OffBand) return Off;

            var span = 1f - OffBand;
            var u = span <= 0f ? 1f : (position - OffBand) / span;
            if (u < 0f) u = 0f;
            if (u > 1f) u = 1f;

            var value = lowest + (highest - lowest) * Pow(u, Curve);
            return Round(value);
        }

        /// <summary>
        /// 数字をつまみの位置に戻す。ToValue と行き来しても意味が変わらない
        /// よう、それぞれの区画の真ん中を返す。
        /// </summary>
        public static float ToPosition(int value, int lowest, int highest)
        {
            if (value <= DeviceDefault) return DefaultBand * 0.5f;
            if (value <= Off) return (DefaultBand + OffBand) * 0.5f;

            var range = highest - lowest;
            var u = range <= 0 ? 1f : (value - lowest) / (float)range;
            if (u < 0f) u = 0f;
            if (u > 1f) u = 1f;

            return OffBand + (1f - OffBand) * Pow(u, 1f / Curve);
        }

        /// <summary>この値で合図が出るか。出さないなら鳴らす仕度も要らない。</summary>
        public static bool Signals(int milliseconds, int amplitude) =>
            milliseconds != Off && amplitude != Off;

        private static float Pow(float value, float exponent) =>
            (float)System.Math.Pow(value, exponent);

        private static int Round(float value) =>
            (int)System.Math.Round(value, System.MidpointRounding.AwayFromZero);
    }
}
