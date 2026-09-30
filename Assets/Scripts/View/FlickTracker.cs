using Pentomino.Core;
using UnityEngine;

namespace Pentomino.View
{
    /// <summary>
    /// 指の動きを追いかけて、離した瞬間に反転するかどうかを決める。
    ///
    /// ここが受け持つのは Unity との橋渡しだけ。
    /// 画面のピクセルをミリメートルに直し、いまの時刻を渡す。
    /// いつ何を測るかは <see cref="FlickDetector"/>、
    /// その動きが反転かどうかは <see cref="FlickRule"/> が決める。
    ///
    /// ミリメートルで測るのは、画面の細かさが機種ごとに違うため。
    /// 同じ 40 ピクセルの動きでも、指の動きとしては別物になってしまう。
    /// </summary>
    public sealed class FlickTracker
    {
        /// <summary>Screen.dpi が取れない機種で使う値。近ごろのスマホのおおよその値。</summary>
        public const float FallbackDpi = 400f;

        private const float MillimetersPerInch = 25.4f;

        private readonly FlickDetector _detector = new FlickDetector();

        /// <summary>いまならしている速度の大きさ（mm/秒）。運んでいる間だけ意味を持つ。</summary>
        public float SpeedMmPerSecond => _detector.SpeedMmPerSecond;

        /// <summary>判定したときの速度の大きさ（mm/秒）。感度調整の画面に出す。</summary>
        public float LastSpeedMmPerSecond => _detector.LastSpeedMmPerSecond;

        /// <summary>掴んでからの総移動量（mm）。</summary>
        public float TravelMm => _detector.TravelMm;

        /// <summary>直近の判定結果。</summary>
        public FlickAxis LastAxis => _detector.LastAxis;

        /// <summary>いま指を追っているか。</summary>
        public bool IsTracking => _detector.IsTracking;

        /// <summary>実際に届いたコマの記録。感度調整の画面に出す。</summary>
        public FlickTrace Trace => _detector.Trace;

        /// <summary>1 ミリメートルが何ピクセルにあたるか。</summary>
        public static float PixelsPerMillimeter
        {
            get
            {
                var dpi = Screen.dpi;
                if (dpi <= 0f) dpi = FallbackDpi;
                return dpi / MillimetersPerInch;
            }
        }

        /// <summary>指が触れた場所から測り直す。</summary>
        public void Begin(Vector2 screenPosition, FlickSettings settings)
        {
            var perMm = PixelsPerMillimeter;
            if (perMm <= 0f) return;

            _detector.Filter = settings != null ? settings.Filter : FlickSettings.DefaultFilter;
            _detector.Begin(screenPosition.x / perMm, screenPosition.y / perMm, Time.unscaledTime);
        }

        /// <summary>指を離さずに追跡だけやめる。</summary>
        public void Stop() => _detector.Stop();

        /// <summary>指が動いた。速度をならすだけで、ここでは反転しない。</summary>
        public void Feed(Vector2 screenPosition)
        {
            var perMm = PixelsPerMillimeter;
            if (perMm <= 0f) return;

            _detector.Feed(screenPosition.x / perMm, screenPosition.y / perMm, Time.unscaledTime);
        }

        /// <summary>指を離した。ここで一度だけ判定する。</summary>
        public FlickAxis Release(FlickSettings settings) =>
            _detector.Release(Time.unscaledTime, settings);

        /// <summary>直近の判定に落ちた理由。感度調整の画面に出す。</summary>
        public string LastReason(FlickSettings settings) => _detector.LastReason(settings);
    }
}
