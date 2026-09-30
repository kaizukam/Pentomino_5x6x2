using System;
using UnityEngine;

namespace Pentomino.View
{
    /// <summary>
    /// はめ込みの合図の音を、その場で作る。
    ///
    /// 音の素材を持たずに合成するのは、長さと強さを設定画面から変えられる
    /// ようにするため。出来合いの音を再生するだけでは、大きさは変えられても
    /// 長さは変えられない。振動のつまみと同じ二つの数字で音も決まるので、
    /// 遊ぶ人から見れば「合図の強さと長さ」というひと組の設定になる。
    ///
    /// 作りは二段。
    ///   1. ごく短い広帯域の当たり（「カ」）。1.5ms で消える雑音
    ///   2. その直後に鳴る短い共鳴（「チッ」）。三つの正弦波が別々の速さで消える
    /// 物がはまる音は、この「当たり」と「余韻」の組で出来ている。
    ///
    /// 長さを延ばすと、余韻の減衰も一緒に延びる。40ms を基準にした比で伸ばすので、
    /// 200ms と指定すれば本当に 200ms 鳴る。
    /// </summary>
    public static class ClickTone
    {
        /// <summary>作る音の細かさ。</summary>
        private const int SampleRate = 44100;

        /// <summary>長さに -1（端末に任せる）が来たときの長さ。</summary>
        public const int DefaultMilliseconds = 40;

        /// <summary>減衰の時定数を決める基準の長さ。</summary>
        private const float ReferenceSeconds = 0.040f;

        private const float NoiseTau = 0.0015f;
        private const float NoiseGain = 0.55f;

        private const float FadeIn = 0.0005f;
        private const float FadeOutRatio = 0.15f;

        /// <summary>
        /// 共鳴の成分（周波数 Hz、減衰の時定数 秒、重み）。
        /// 倍音関係から少しずらしてあるのは、楽器の音に聞こえないようにするため。
        /// </summary>
        private static readonly (float Hertz, float Tau, float Weight)[] Partials =
        {
            (900f, 0.012f, 0.35f),    // 本体の重み
            (1830f, 0.007f, 1.00f),   // 芯
            (2740f, 0.005f, 0.55f),
            (4390f, 0.003f, 0.30f),   // 明るさ
        };

        /// <summary>
        /// 合図の音を作る。
        /// </summary>
        /// <param name="milliseconds">長さ。0 以下なら既定の 40ms。</param>
        public static AudioClip Create(int milliseconds)
        {
            var length = milliseconds <= 0 ? DefaultMilliseconds : milliseconds;
            var seconds = length / 1000f;

            var count = Mathf.Max(8, Mathf.RoundToInt(SampleRate * seconds));
            var samples = new float[count];

            // 長さに合わせて余韻も伸ばす。伸ばさないと、長くしても
            // 無音が後ろに付くだけで「長い音」にならない。
            var stretch = seconds / ReferenceSeconds;

            // 毎回同じ音になるように種を固定する。
            var noise = new System.Random(20260904);
            var loudest = 0f;

            for (var i = 0; i < count; i++)
            {
                var t = i / (float)SampleRate;

                var value = NoiseGain * (float)(noise.NextDouble() * 2.0 - 1.0)
                            * Mathf.Exp(-t / (NoiseTau * stretch));

                foreach (var partial in Partials)
                    value += partial.Weight
                             * Mathf.Sin(2f * Mathf.PI * partial.Hertz * t)
                             * Mathf.Exp(-t / (partial.Tau * stretch));

                // 頭と尻の切り口で「プツ」と鳴らないよう、そこだけ滑らかにする。
                if (t < FadeIn) value *= t / FadeIn;

                var remaining = seconds - t;
                var fadeOut = seconds * FadeOutRatio;
                if (remaining < fadeOut) value *= remaining / fadeOut;

                samples[i] = value;
                loudest = Mathf.Max(loudest, Mathf.Abs(value));
            }

            // 大きさは再生時に決めるので、ここでは山を 1 に揃えるだけ。
            if (loudest > 0f)
                for (var i = 0; i < count; i++) samples[i] /= loudest;

            var clip = AudioClip.Create("Click" + length, count, 1, SampleRate, false);
            clip.SetData(samples, 0);
            return clip;
        }
    }
}
