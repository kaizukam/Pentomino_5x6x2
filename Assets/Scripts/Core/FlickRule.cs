using System;

namespace Pentomino.Core
{
    /// <summary>
    /// フリックの向き。指を滑らせた向きで、ピースはその向きへ転がる。
    ///
    /// 6X10 では横なら左右反転、縦なら上下反転で、向きの左右・上下は問わなかった。
    /// 5x6x2 では 90 度の回転なので、どちらへ転がすかで結果が違う。
    /// </summary>
    public enum FlickAxis
    {
        None,

        /// <summary>左へ。画面の縦軸まわりに、左へ転がる。</summary>
        Left,

        /// <summary>右へ。画面の縦軸まわりに、右へ転がる。</summary>
        Right,

        /// <summary>上へ。画面の横軸まわりに、上へ転がる。</summary>
        Up,

        /// <summary>下へ。画面の横軸まわりに、下へ転がる。</summary>
        Down,
    }

    public static class FlickAxes
    {
        /// <summary>横向きのフリックか（左または右）。</summary>
        public static bool IsHorizontal(this FlickAxis axis) => axis == FlickAxis.Left || axis == FlickAxis.Right;

        /// <summary>縦向きのフリックか（上または下）。</summary>
        public static bool IsVertical(this FlickAxis axis) => axis == FlickAxis.Up || axis == FlickAxis.Down;
    }

    /// <summary>
    /// 手ざわりの設定（開発仕様「ピースの操作」）。
    ///
    /// 反転の判定に使うしきい値と、はまったときの振動の強さ・長さを持つ。
    /// どちらも実機でないと詰められないので、設定画面から変えて保存できる
    /// ようにしてある。振動は反転の判定には使わないが、指で確かめて決める
    /// という性格が同じなので、同じ画面・同じ保存先にまとめている。
    /// </summary>
    public sealed class FlickSettings
    {
        /// <summary>これ以上の速さで離したら反転する（mm/秒）。</summary>
        public const float DefaultReleaseSpeedMmPerSecond = 70f;

        /// <summary>
        /// 速度をならす強さ。大きいほど鈍くなる。
        ///
        /// 1 なら生の値そのまま。3 なら「60fps での直前 3 コマ分」が
        /// おおよそ効く。中では時間（時定数）として扱うので、
        /// フレーム率が変わっても同じ手の動きには同じように反応する。
        /// </summary>
        public const float DefaultFilter = 3f;

        /// <summary>これだけ動きが途切れたら、止まったとみなす（秒）。</summary>
        public const float DefaultStillSeconds = 0.06f;

        /// <summary>掴んでからこれだけ動いていなければ反転しない（mm）。</summary>
        public const float DefaultMinTravelMm = 6f;

        /// <summary>長辺が短辺のこれ倍なければ、斜めとみなして受け付けない。</summary>
        public const float DefaultAxisRatio = 2f;

        /// <summary>
        /// 合図の長さ（ミリ秒）。-1 は「端末に任せる」、0 は「出さない」。
        ///
        /// 既定は「端末に任せる」ではなく、20ms と決め打ちにしてある。
        ///
        /// 任せると、Android 10 以降は端末ごとに調律されたクリックの合図
        /// （VibrationEffect.EFFECT_CLICK）になる。作りのよい機種ではこれが
        /// いちばん自然だが、AQUOS sense7 plus で実測したところ、
        /// 手に持っていてもまったく感じ取れなかった。同じ端末で 10ms の
        /// 直の振動は判るので、振動子ではなく調律のほうの問題になる。
        ///
        /// 初期値が何も感じられないのでは、合図があること自体に気づけない。
        /// 「端末に任せる」はつまみの左端に選択肢として残し、初期値からは外す。
        /// </summary>
        public const int DefaultVibrationMilliseconds = 20;

        /// <summary>
        /// 合図の強さ。-1 は「端末に任せる」、0 は「出さない」。
        ///
        /// こちらは端末に任せたままにする。実測では、20ms のとき 128 以上なら
        /// 確実に判り、80 以下は回によってぶれた。境目がその程度に曖昧なら、
        /// こちらで数字を決め打ちするより、端末が自機の振動子に合わせて
        /// 持っている既定のほうが確かになる。
        /// </summary>
        public const int DefaultVibrationAmplitude = SignalScale.DeviceDefault;

        /// <summary>数字で決めるときの、いちばん短い長さ（ミリ秒）。</summary>
        public const int ShortestVibrationMilliseconds = 5;

        /// <summary>数字で決めるときの、いちばん弱い強さ。</summary>
        public const int WeakestVibrationAmplitude = 1;

        /// <summary>合図の強さの上限。</summary>
        public const int StrongestVibrationAmplitude = 255;

        /// <summary>振動の長さの上限（ミリ秒）。これ以上は合図ではなく警報になる。</summary>
        public const int LongestVibrationMilliseconds = 500;

        /// <summary>これ以上の速さで離したら反転する（mm/秒）。</summary>
        public float ReleaseSpeedMmPerSecond { get; set; } = DefaultReleaseSpeedMmPerSecond;

        /// <summary>速度をならす強さ。</summary>
        public float Filter { get; set; } = DefaultFilter;

        /// <summary>止まったとみなすまでの間（秒）。</summary>
        public float StillSeconds { get; set; } = DefaultStillSeconds;

        /// <summary>掴んでから必要な総移動量（mm）。</summary>
        public float MinTravelMm { get; set; } = DefaultMinTravelMm;

        /// <summary>斜めの許容。</summary>
        public float AxisRatio { get; set; } = DefaultAxisRatio;

        /// <summary>合図の長さ（ミリ秒）。-1 なら端末に任せる、0 なら出さない。</summary>
        public int VibrationMilliseconds { get; set; } = DefaultVibrationMilliseconds;

        /// <summary>合図の強さ。-1 なら端末に任せる、0 なら出さない。</summary>
        public int VibrationAmplitude { get; set; } = DefaultVibrationAmplitude;

        public FlickSettings Clone() => new FlickSettings
        {
            ReleaseSpeedMmPerSecond = ReleaseSpeedMmPerSecond,
            Filter = Filter,
            StillSeconds = StillSeconds,
            MinTravelMm = MinTravelMm,
            AxisRatio = AxisRatio,
            VibrationMilliseconds = VibrationMilliseconds,
            VibrationAmplitude = VibrationAmplitude,
        };

        /// <summary>調整の行き過ぎを、意味のある範囲に収める。</summary>
        public void Clamp()
        {
            ReleaseSpeedMmPerSecond = Bound(ReleaseSpeedMmPerSecond, 20f, 600f);
            Filter = Bound(Filter, 1f, 10f);
            StillSeconds = Bound(StillSeconds, 0.02f, 0.30f);
            MinTravelMm = Bound(MinTravelMm, 0f, 40f);
            AxisRatio = Bound(AxisRatio, 1f, 5f);

            // -1（端末に任せる）と 0（出さない）も意味のある値なので、下は -1 まで。
            VibrationMilliseconds = Bound(VibrationMilliseconds,
                SignalScale.DeviceDefault, LongestVibrationMilliseconds);
            VibrationAmplitude = Bound(VibrationAmplitude,
                SignalScale.DeviceDefault, StrongestVibrationAmplitude);
        }

        private static int Bound(int value, int low, int high) =>
            value < low ? low : (value > high ? high : value);

        private static float Bound(float value, float low, float high) =>
            value < low ? low : (value > high ? high : value);
    }

    /// <summary>
    /// 指を離したときの動きから、反転するかどうかを決める（開発仕様「ピースの操作」）。
    ///
    /// 以前は運んでいる最中に判定していたが、それでは普通のスワイプと区別がつかず、
    /// 一度のドラッグで何度も反転してしまった。しきい値の取り方にも矛盾があり、
    /// 「止まった」とみなす速さが、反転が成立する最低速度より速かった。
    ///
    /// いまは判定を一度きり、離した瞬間に行う。
    /// ピースを置きたいときは、位置を決めて止めてから離すので、まず反転しない。
    /// 反転したいときは、勢いをつけたまま離す。区別がはっきりする。
    ///
    /// Unity には「指が止まった」を知らせる仕組みが無く、OnDrag は指が動いた
    /// フレームにしか来ない。離す瞬間（OnEndDrag）だけが確実に来るので、
    /// そこで決めるのが、この仕掛けに素直に乗る形でもある。
    /// </summary>
    public static class FlickRule
    {
        /// <summary>
        /// なめらかさを、コマ数ではなく時間として読むときの基準（1 秒あたりのコマ数）。
        ///
        /// n はもともと「直前 n コマ分がおおよそ効く」という意味だった。
        /// これを 60 コマ/秒での話と決めて、時間に読み替える。
        /// </summary>
        public const float ReferenceFps = 60f;

        /// <summary>
        /// なめらかさ n に対応する時定数（秒）。n = 3 なら 33.3ms。
        /// </summary>
        public static float TimeConstant(float filter)
        {
            var n = filter < 1f ? 1f : filter;
            return (n - 1f) / ReferenceFps;
        }

        /// <summary>
        /// ひとコマぶんの重み。新しい値をどれだけ取り込むか（0〜1）。
        /// </summary>
        public static float Weight(float filter, float seconds)
        {
            if (seconds <= 0f) return 0f;

            var tau = TimeConstant(filter);
            return tau <= 0f ? 1f : seconds / (tau + seconds);
        }

        /// <summary>
        /// ならした速度を進める。
        ///
        ///     speed_ave = (dt * speed_got + τ * speed_ave) / (τ + dt)
        ///
        /// もとの式は (speed_got + (n-1) * speed_ave) / n で、重みがコマ数だった。
        /// それだと画面の重さでフレーム率が変わるたびに応答が変わってしまい、
        /// 30fps と 120fps で 2 倍以上ちがう反応になる。重みを時間にすれば、
        /// フレーム率が変わっても同じ手の動きに同じように反応する。
        /// 60fps・dt = 1/60 のときは、もとの式とまったく同じ値になる。
        ///
        /// 速さではなく速度ベクトルをならすのは、向きも同時に決めたいため。
        /// 行ったり来たりした動きは打ち消し合って小さくなるので、
        /// 迷った末に離したときに反転しにくい。
        /// </summary>
        public static void Filter(ref float averageX, ref float averageY,
            float gotX, float gotY, float filter, float seconds)
        {
            var a = Weight(filter, seconds);
            if (a <= 0f) return;

            averageX = a * gotX + (1f - a) * averageX;
            averageY = a * gotY + (1f - a) * averageY;
        }

        /// <summary>ならした速度の大きさ（mm/秒）。</summary>
        public static float Speed(float averageX, float averageY) =>
            (float)Math.Sqrt(averageX * averageX + averageY * averageY);

        /// <summary>
        /// 離したときの速度から、転がす向きを決める。
        /// </summary>
        /// <param name="averageX">ならした速度の横成分（mm/秒）</param>
        /// <param name="averageY">ならした速度の縦成分（mm/秒）</param>
        /// <param name="travelMm">掴んでからの総移動量（mm）</param>
        /// <param name="stillSeconds">最後に動いてからの経過（秒）</param>
        public static FlickAxis Decide(float averageX, float averageY, float travelMm,
            float stillSeconds, FlickSettings settings)
        {
            if (settings == null) return FlickAxis.None;

            // 止まってから離した。置くつもりの動作なので、反転しない。
            if (stillSeconds >= settings.StillSeconds) return FlickAxis.None;

            // ほとんど動かしていない。触れて離しただけ。
            if (travelMm < settings.MinTravelMm) return FlickAxis.None;

            if (Speed(averageX, averageY) < settings.ReleaseSpeedMmPerSecond) return FlickAxis.None;

            var x = Math.Abs(averageX);
            var y = Math.Abs(averageY);

            // 斜めの動きは、どちらの反転か決められないので受け付けない。
            var major = Math.Max(x, y);
            var minor = Math.Min(x, y);
            if (minor > 0f && major < minor * settings.AxisRatio) return FlickAxis.None;

            // 速度は画面の座標（上が正）で届く。
            if (x >= y) return averageX >= 0f ? FlickAxis.Right : FlickAxis.Left;
            return averageY >= 0f ? FlickAxis.Up : FlickAxis.Down;
        }

        /// <summary>判定に落ちた理由。感度調整の画面に出す。</summary>
        public static string Explain(float averageX, float averageY, float travelMm,
            float stillSeconds, FlickSettings settings)
        {
            if (settings == null) return "設定なし";

            if (stillSeconds >= settings.StillSeconds)
                return "止めてから離した " + Round(stillSeconds * 1000f) + "ms";

            if (travelMm < settings.MinTravelMm)
                return "動かしていない " + Round1(travelMm) + "mm";

            var speed = Speed(averageX, averageY);
            if (speed < settings.ReleaseSpeedMmPerSecond)
                return "遅い " + Round(speed) + "mm/s";

            var x = Math.Abs(averageX);
            var y = Math.Abs(averageY);
            var major = Math.Max(x, y);
            var minor = Math.Min(x, y);
            if (minor > 0f && major < minor * settings.AxisRatio)
                return "斜め " + Round(major) + ":" + Round(minor);

            string direction;
            if (x >= y) direction = averageX >= 0f ? "右へ " : "左へ ";
            else direction = averageY >= 0f ? "上へ " : "下へ ";
            return direction + Round(speed) + "mm/s";
        }

        private static int Round(float value) => (int)Math.Round(value);

        private static string Round1(float value) => value.ToString("0.0");
    }
}
