using System;

namespace Pentomino.Core
{
    /// <summary>
    /// 問題送りボタンを押し続けたときの繰り返し判定（開発仕様「画面TOPナビゲーション」）。
    ///
    /// 軽く押せば 1 問。押し続けると 1 秒後から 0.3 秒ごとに 10 問ずつ動き、
    /// さらに 3 秒を超えると 0.3 秒ごとに 100 問ずつに加速する。
    /// </summary>
    public static class StepRepeatRule
    {
        /// <summary>押し続けてから繰り返しが始まるまでの時間（秒）。</summary>
        public const float DefaultHoldDelay = 1f;

        /// <summary>繰り返しの間隔（秒）。</summary>
        public const float DefaultRepeatInterval = 0.3f;

        /// <summary>さらに加速するまでの時間（秒）。</summary>
        public const float DefaultFastDelay = 3f;

        /// <summary>軽く押したときの移動量。</summary>
        public const int DefaultTapStep = 1;

        /// <summary>加速前の 1 回あたりの移動量。</summary>
        public const int DefaultRepeatStep = 10;

        /// <summary>加速後の 1 回あたりの移動量。</summary>
        public const int DefaultFastStep = 100;

        /// <summary>境界の判定に使う許容誤差。float の丸めで 1 回分ずれるのを防ぐ。</summary>
        private const double BoundaryTolerance = 1e-3;

        /// <summary>
        /// 押し始めてから heldSeconds 経った時点で、繰り返しが何回起きているべきか。
        /// </summary>
        public static int RepeatsSoFar(float heldSeconds,
            float holdDelay = DefaultHoldDelay,
            float interval = DefaultRepeatInterval)
        {
            if (heldSeconds < holdDelay) return 0;
            if (interval <= 0f) return 1;

            // 待ち時間を超えた時点で 1 回目。以後は interval ごとに 1 回ずつ。
            // 1.3 - 1.0 が 0.29999995 になるような誤差で 1 回取りこぼさないよう、ごく小さく下駄を履かせる。
            return 1 + (int)Math.Floor((heldSeconds - holdDelay) / interval + BoundaryTolerance);
        }

        /// <summary>
        /// 押し始めてから heldSeconds 経った時点での、移動量の累計（符号なし）。
        /// 押し続けている間、この値が増えた分だけ動かす。
        /// </summary>
        public static int TotalStepSoFar(float heldSeconds,
            float holdDelay = DefaultHoldDelay,
            float interval = DefaultRepeatInterval,
            float fastDelay = DefaultFastDelay,
            int repeatStep = DefaultRepeatStep,
            int fastStep = DefaultFastStep)
        {
            var repeats = RepeatsSoFar(heldSeconds, holdDelay, interval);
            if (repeats <= 0) return 0;

            var total = 0;
            for (var i = 0; i < repeats; i++)
            {
                // i 回目が起きる時刻。その時点で加速に入っているかで刻み幅が変わる。
                var firedAt = holdDelay + i * interval;
                total += firedAt >= fastDelay - BoundaryTolerance ? fastStep : repeatStep;
            }
            return total;
        }

        /// <summary>
        /// 指を離したときに、軽いタップとして 1 問動かすべきか。
        /// 繰り返しが始まっていたなら、離した瞬間に余分に動かさない。
        /// </summary>
        public static bool ShouldStepOnRelease(int repeatsFired) => repeatsFired == 0;
    }
}
