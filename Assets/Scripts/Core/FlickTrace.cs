using System;
using System.Text;

namespace Pentomino.Core
{
    /// <summary>
    /// 指を動かしている間、実際に何が届いたかを記録する。
    ///
    /// フリックの判定は「距離 ÷ 時間」で決まるが、その時間はフレーム時間そのもの。
    /// 画面が重いフレームでは間隔が伸び、同じ指の動きでも速さが小さく出る。
    /// 感度調整の画面と本番の画面で反応が違うのは、ここに理由がある。
    ///
    /// 机上では確かめようがないので、実機で起きたことをそのまま数字にする。
    /// 1 秒あたり何回届いたか、間隔はどれくらい散らばっているか、
    /// 1 回あたり指はどれだけ動いたか。それを見てから、しきい値を決める。
    /// </summary>
    public sealed class FlickTrace
    {
        /// <summary>覚えておく回数。指を 1 秒動かせば 60 回ほど届く。</summary>
        public const int Capacity = 120;

        private readonly float[] _seconds = new float[Capacity];
        private readonly float[] _distances = new float[Capacity];

        private int _next;

        /// <summary>いま覚えている回数。</summary>
        public int Count { get; private set; }

        /// <summary>指を触れてから、いままでに届いた回数。Capacity で頭打ちにならない。</summary>
        public int Total { get; private set; }

        /// <summary>ひとコマ分を書き留める。</summary>
        public void Add(float seconds, float distanceMm)
        {
            // 同じフレームで二度呼ばれた分（間隔 0）は、平均を狂わせるだけなので数えない。
            if (seconds <= 0f) return;

            _seconds[_next] = seconds;
            _distances[_next] = distanceMm;

            _next = (_next + 1) % Capacity;
            if (Count < Capacity) Count++;
            Total++;
        }

        /// <summary>指を触れ直したら、それまでの記録を捨てる。</summary>
        public void Clear()
        {
            _next = 0;
            Count = 0;
            Total = 0;
        }

        /// <summary>間隔の平均（秒）。</summary>
        public float AverageSeconds => Average(_seconds);

        /// <summary>いちばん長かった間隔（秒）。ここが伸びると誤判定が起きる。</summary>
        public float MaxSeconds => Max(_seconds);

        /// <summary>いちばん短かった間隔（秒）。</summary>
        public float MinSeconds => Min(_seconds);

        /// <summary>1 秒あたりに届いた回数。フレーム率とほぼ同じになるはず。</summary>
        public float PerSecond
        {
            get
            {
                var average = AverageSeconds;
                return average > 0f ? 1f / average : 0f;
            }
        }

        /// <summary>ひとコマで指が動いた距離の平均（mm）。</summary>
        public float AverageDistance => Average(_distances);

        /// <summary>ひとコマで指が動いた距離のいちばん大きい値（mm）。</summary>
        public float MaxDistance => Max(_distances);

        /// <summary>
        /// 間隔のばらつき。平均に対する標準偏差の割合。
        ///
        /// 0.1 なら安定、0.5 を超えるならフレームが飛んでいる。
        /// 感度調整の画面と本番の画面で、ここが揃っているかを見る。
        /// </summary>
        public float Unevenness
        {
            get
            {
                var average = AverageSeconds;
                if (Count == 0 || average <= 0f) return 0f;

                var sum = 0f;
                for (var i = 0; i < Count; i++)
                {
                    var gap = _seconds[i] - average;
                    sum += gap * gap;
                }

                return (float)Math.Sqrt(sum / Count) / average;
            }
        }

        /// <summary>画面に出す 2 行の要約。</summary>
        public string Summary()
        {
            if (Count == 0) return "まだ測っていません";

            var text = new StringBuilder();

            text.Append("毎秒 ").Append(Round(PerSecond)).Append(" 回");
            text.Append("  間隔 ").Append(Round(MinSeconds * 1000f));
            text.Append("〜").Append(Round(MaxSeconds * 1000f));
            text.Append("ms（平均 ").Append(Round(AverageSeconds * 1000f)).Append("）");
            text.Append("  ばらつき ").Append(Percent(Unevenness));

            text.Append('\n');

            text.Append("ひとコマの動き 平均 ").Append(Round1(AverageDistance));
            text.Append("mm  最大 ").Append(Round1(MaxDistance)).Append("mm");
            text.Append("  この指で ").Append(Total).Append(" 回");

            return text.ToString();
        }

        private float Average(float[] values)
        {
            if (Count == 0) return 0f;

            var sum = 0f;
            for (var i = 0; i < Count; i++) sum += values[i];
            return sum / Count;
        }

        private float Max(float[] values)
        {
            if (Count == 0) return 0f;

            var best = values[0];
            for (var i = 1; i < Count; i++) if (values[i] > best) best = values[i];
            return best;
        }

        private float Min(float[] values)
        {
            if (Count == 0) return 0f;

            var best = values[0];
            for (var i = 1; i < Count; i++) if (values[i] < best) best = values[i];
            return best;
        }

        private static int Round(float value) => (int)Math.Round(value);

        private static string Round1(float value) => value.ToString("0.0");

        private static string Percent(float value) => Round(value * 100f) + "%";
    }
}
