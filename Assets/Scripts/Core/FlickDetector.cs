using System;

namespace Pentomino.Core
{
    /// <summary>
    /// 指の動きを追って、離した瞬間に反転するかどうかを決める。
    ///
    /// 判定は一度きり、指を離したときだけ行う（開発仕様「ピースの操作」）。
    /// 運んでいる最中には何も起きないので、普通のスワイプで反転することがない。
    ///
    /// 運んでいる間は、速度をならし続けるだけ。
    ///
    ///     speed_ave = (dt * speed_got + τ * speed_ave) / (τ + dt)
    ///
    /// ひとコマだけ跳ねた値に引きずられないための重み付けで、フレームが
    /// 飛んでも判定が暴れにくくなる。重みを時間で持つので、画面の重さで
    /// フレーム率が変わっても、同じ手の動きには同じように反応する。
    /// 速さではなく速度ベクトルをならすので、向きも同時に決まり、
    /// 行ったり来たりした動きは打ち消し合う。
    ///
    /// 「その動きが反転か」の判断は <see cref="FlickRule"/> が持つ。
    /// ここが受け持つのは、いつ何を測るかだけ。
    /// </summary>
    public sealed class FlickDetector
    {
        private float _lastX, _lastY, _lastTime;
        private bool _tracking;

        /// <summary>ならした速度（mm/秒）。判定を終えると 0 に戻す。</summary>
        private float _averageX, _averageY;

        /// <summary>
        /// 判定したときの速度を控えたもの（mm/秒）。
        ///
        /// ならした速度は判定のあと 0 に戻すので、そのままだと
        /// 感度調整の画面に出す値まで 0 になってしまう。
        /// 判定に使った値をここへ写しておき、表示はこちらを読む。
        /// </summary>
        private float _lastSpeedX, _lastSpeedY;

        /// <summary>掴んでからの総移動量（mm）。</summary>
        private float _travel;

        /// <summary>ならした速度の大きさ（mm/秒）。感度調整の画面に出す。</summary>
        public float SpeedMmPerSecond => FlickRule.Speed(_averageX, _averageY);

        /// <summary>ならした速度の向き。</summary>
        public float SpeedXmm => _averageX;
        public float SpeedYmm => _averageY;

        /// <summary>判定したときの速度の大きさ（mm/秒）。感度調整の画面に出す。</summary>
        public float LastSpeedMmPerSecond => FlickRule.Speed(_lastSpeedX, _lastSpeedY);

        /// <summary>掴んでからの総移動量（mm）。</summary>
        public float TravelMm => _travel;

        /// <summary>直近の判定結果。</summary>
        public FlickAxis LastAxis { get; private set; }

        /// <summary>直近に判定したときの、最後の動きからの間（秒）。</summary>
        public float LastStillSeconds { get; private set; }

        /// <summary>いま指を追っているか。</summary>
        public bool IsTracking => _tracking;

        /// <summary>
        /// 実際に届いたコマの記録。しきい値を決めるための材料で、判定には使わない。
        /// 感度調整の画面がここを読んで、実機で何が起きているかを出す。
        /// </summary>
        public FlickTrace Trace { get; } = new FlickTrace();

        /// <summary>指が触れた。ここから測り始める。</summary>
        public void Begin(float xMm, float yMm, float now)
        {
            _lastX = xMm;
            _lastY = yMm;
            _lastTime = now;

            _averageX = 0f;
            _averageY = 0f;
            _lastSpeedX = 0f;
            _lastSpeedY = 0f;
            _travel = 0f;

            _tracking = true;
            LastAxis = FlickAxis.None;
            LastStillSeconds = 0f;

            Trace.Clear();
        }

        /// <summary>指を離さずに追跡だけやめる。</summary>
        public void Stop()
        {
            _tracking = false;
        }

        /// <summary>
        /// 指が動いたことを知らせる。ここでは反転しない。
        /// 速度をならしながら、離す瞬間に備えるだけ。
        /// </summary>
        public void Feed(float xMm, float yMm, float now)
        {
            if (!_tracking) return;

            var seconds = now - _lastTime;

            var dx = xMm - _lastX;
            var dy = yMm - _lastY;

            _lastX = xMm;
            _lastY = yMm;
            _lastTime = now;

            var moved = Distance(dx, dy);
            _travel += moved;

            Trace.Add(seconds, moved);

            // 同じフレームで二度届いた分は、割れないので捨てる。
            if (seconds <= 0f) return;

            FlickRule.Filter(ref _averageX, ref _averageY,
                dx / seconds, dy / seconds, Filter, seconds);
        }

        /// <summary>
        /// 指を離した。ここで一度だけ判定する。
        /// </summary>
        public FlickAxis Release(float now, FlickSettings settings)
        {
            LastAxis = FlickAxis.None;
            LastStillSeconds = _tracking ? now - _lastTime : 0f;

            if (!_tracking || settings == null)
            {
                _tracking = false;
                return FlickAxis.None;
            }

            _tracking = false;

            LastAxis = FlickRule.Decide(_averageX, _averageY, _travel, LastStillSeconds, settings);

            // 判定を終えたので、ならした速度を 0 に戻す。
            // 次に掴むまで持ち越すと、離した勢いが残ったまま次の判定に混ざる。
            // 判定に使った値は控えてあるので、画面への表示はそのまま出せる。
            _lastSpeedX = _averageX;
            _lastSpeedY = _averageY;
            _averageX = 0f;
            _averageY = 0f;

            return LastAxis;
        }

        /// <summary>直近の判定に落ちた理由。感度調整の画面に出す。</summary>
        public string LastReason(FlickSettings settings) =>
            FlickRule.Explain(_lastSpeedX, _lastSpeedY, _travel, LastStillSeconds, settings);

        /// <summary>
        /// ならしの強さ。Feed には設定を渡さない作りなので、ここに持たせる。
        /// 設定画面で変えたら、掴み直したときから効く。
        /// </summary>
        public float Filter { get; set; } = FlickSettings.DefaultFilter;

        private static float Distance(float dx, float dy) =>
            (float)Math.Sqrt(dx * dx + dy * dy);
    }
}
