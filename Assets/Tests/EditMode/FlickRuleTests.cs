using NUnit.Framework;
using Pentomino.Core;

namespace Pentomino.Tests
{
    /// <summary>
    /// 離した瞬間の動きから、反転するかどうかを決める規則。
    ///
    /// 以前は運んでいる最中に判定していたが、普通のスワイプと区別がつかず、
    /// 一度のドラッグで何度も反転した。いまは離すときに一度だけ決める。
    /// 置きたいときは位置を決めて止めてから離すので、そこで区別がつく。
    /// </summary>
    public class FlickRuleTests
    {
        private FlickSettings _settings;

        [SetUp]
        public void SetUp() => _settings = new FlickSettings();

        /// <summary>止めずに、じゅうぶん動かして離した状態。</summary>
        private FlickAxis Decide(float speedX, float speedY,
            float travel = 30f, float still = 0f) =>
            FlickRule.Decide(speedX, speedY, travel, still, _settings);

        [Test]
        public void 速く横へ動かしたまま離すとその向きへ転がす()
        {
            Assert.AreEqual(FlickAxis.Right, Decide(400f, 0f));
            Assert.AreEqual(FlickAxis.Left, Decide(-400f, 0f), "逆向きなら逆へ");
        }

        [Test]
        public void 速く縦へ動かしたまま離すとその向きへ転がす()
        {
            // 速度は画面の座標で、上が正。
            Assert.AreEqual(FlickAxis.Up, Decide(0f, 400f));
            Assert.AreEqual(FlickAxis.Down, Decide(0f, -400f));
        }

        [Test]
        public void 止めてから離せば反転しない()
        {
            // 置きたいときの動作。ここが新しい仕様のかなめ。
            Assert.AreEqual(FlickAxis.None, Decide(400f, 0f, still: 0.10f));
        }

        [Test]
        public void ゆっくり動かしたまま離しても反転しない()
        {
            // しきい値そのものを基準にする。既定を変えても意味が変わらない。
            Assert.AreEqual(FlickAxis.None, Decide(_settings.ReleaseSpeedMmPerSecond * 0.5f, 0f));
        }

        [Test]
        public void ほとんど動かしていなければ反転しない()
        {
            // 触れて、わずかに震えて離しただけ。
            Assert.AreEqual(FlickAxis.None, Decide(400f, 0f, travel: 2f));
        }

        [Test]
        public void 斜めは受け付けない()
        {
            Assert.AreEqual(FlickAxis.None, Decide(300f, 300f));
        }

        [Test]
        public void 斜めでも寄っていれば受け付ける()
        {
            Assert.AreEqual(FlickAxis.Right, Decide(400f, 100f));
        }

        [Test]
        public void 設定が無ければ何も起きない()
        {
            Assert.AreEqual(FlickAxis.None, FlickRule.Decide(400f, 0f, 30f, 0f, null));
        }

        [Test]
        public void しきい値ちょうどでも成立する()
        {
            var speed = _settings.ReleaseSpeedMmPerSecond;
            Assert.AreEqual(FlickAxis.Right, Decide(speed, 0f));
        }

        // ---------------------------------------------------------------- ならし

        private const float Frame60 = 1f / 60f;

        [Test]
        public void ならしは1なら生の値のまま()
        {
            float x = 0f, y = 0f;
            FlickRule.Filter(ref x, ref y, 100f, 50f, 1f, Frame60);

            Assert.AreEqual(100f, x, 0.001f);
            Assert.AreEqual(50f, y, 0.001f);
        }

        [Test]
        public void 六十コマなら元の式と同じ値になる()
        {
            // speed_ave = (speed_got + (n-1) * speed_ave) / n
            // 時間で持つ形に変えても、60fps ではこの式と一致する。
            const float n = 3f;

            float x = 0f, y = 0f;
            FlickRule.Filter(ref x, ref y, 300f, 0f, n, Frame60);
            Assert.AreEqual(100f, x, 0.01f, "1 回目は 1/3");

            FlickRule.Filter(ref x, ref y, 300f, 0f, n, Frame60);
            Assert.AreEqual(500f / 3f, x, 0.01f, "2 回目でさらに近づく");
        }

        [Test]
        public void なめらかさは時定数として読む()
        {
            // n = 3 は 60fps での 2 コマ分。
            Assert.AreEqual(2f / 60f, FlickRule.TimeConstant(3f), 0.0001f);
            Assert.AreEqual(0f, FlickRule.TimeConstant(1f), 0.0001f, "1 ならならさない");
        }

        [Test]
        public void ひとコマの重みは経過時間で決まる()
        {
            // 長く空いたコマほど、新しい値を強く取り込む。
            var shortStep = FlickRule.Weight(3f, Frame60);
            var longStep = FlickRule.Weight(3f, Frame60 * 4f);

            Assert.AreEqual(1f / 3f, shortStep, 0.001f, "60fps では 1/n");
            Assert.Greater(longStep, shortStep, "間が空いたのに重みが増えていません");
        }

        [Test]
        public void ならしはひとコマの跳ねに引きずられない()
        {
            float x = 0f, y = 0f;
            for (var i = 0; i < 10; i++) FlickRule.Filter(ref x, ref y, 100f, 0f, 3f, Frame60);

            var steady = x;
            FlickRule.Filter(ref x, ref y, 1000f, 0f, 3f, Frame60);   // ← 跳ねたコマ

            Assert.Less(x, steady * 4.5f, "跳ねをそのまま受け取ってしまっています");
        }

        /// <summary>
        /// 手を振ったときに、ならしたあと残る速さを測る。
        ///
        /// 以前は毎コマ ±V を入れて確かめていたが、それは 30Hz
        /// （60fps でのナイキスト周波数）で振っていることに相当する。
        /// 人の指はそんな速さでは振れないので、いちばん減衰する所だけを
        /// 見ていたことになり、実際より安全に見えていた。
        /// 実際の手の揺れは 3〜8Hz。そこで測る。
        /// </summary>
        private static float ShakePeak(float hertz, float peakSpeed, float filter, float fps)
        {
            var step = 1f / fps;
            var samples = (int)(fps / hertz * 40f);

            float x = 0f, y = 0f, worst = 0f;
            for (var k = 0; k < samples; k++)
            {
                var got = peakSpeed * (float)System.Math.Cos(2.0 * System.Math.PI * hertz * k / fps);
                FlickRule.Filter(ref x, ref y, got, 0f, filter, step);

                // 落ち着いてからの山を見る。
                if (k > samples * 0.7f) worst = System.Math.Max(worst, FlickRule.Speed(x, y));
            }
            return worst;
        }

        [Test]
        public void 実際の手の揺れは半分ほど通る()
        {
            // ここは「打ち消せている」と言える所ではない。
            // 揺れを止めるのはならしではなく、離す前に止まったかどうかの判定。
            var peak = ShakePeak(5f, 400f, 3f, 60f);

            Assert.Greater(peak, 400f * 0.4f, "実際より減衰して見えています");
            Assert.Less(peak, 400f * 0.8f, "まったく効いていません");
        }

        [Test]
        public void ならしを強くすると揺れが通りにくくなる()
        {
            var loose = ShakePeak(5f, 400f, 3f, 60f);
            var tight = ShakePeak(5f, 400f, 6f, 60f);

            Assert.Less(tight, loose * 0.75f, "なめらかさを上げた効きが足りません");
        }

        [Test]
        public void フレーム率が変わっても同じ揺れには同じように反応する()
        {
            // ここが時間で持つことの目的。
            // コマ数で持っていたときは、30fps と 120fps で 2 倍以上ちがった。
            var at30 = ShakePeak(5f, 400f, 3f, 30f);
            var at60 = ShakePeak(5f, 400f, 3f, 60f);
            var at120 = ShakePeak(5f, 400f, 3f, 120f);

            Assert.AreEqual(at60, at120, at60 * 0.15f, "高いフレーム率で過敏になっています");
            Assert.AreEqual(at60, at30, at60 * 0.30f, "低いフレーム率で鈍くなりすぎです");
        }

        [Test]
        public void 速く振っても離す前に止まれば反転しない()
        {
            // 実機がそう振る舞っている。いちばん大事な性質なので、ここで固定する。
            var peak = ShakePeak(5f, 600f, 3f, 60f);

            Assert.AreEqual(FlickAxis.None,
                FlickRule.Decide(peak, 0f, 60f, _settings.StillSeconds, _settings),
                "止めてから離したのに反転しています");
        }

        // ---------------------------------------------------------------- 設定

        [Test]
        public void 行き過ぎた設定は使える範囲に丸める()
        {
            var wild = new FlickSettings
            {
                ReleaseSpeedMmPerSecond = 9999f,
                Filter = 0f,
                StillSeconds = 99f,
                MinTravelMm = -5f,
                AxisRatio = 0f,
            };
            wild.Clamp();

            Assert.LessOrEqual(wild.ReleaseSpeedMmPerSecond, 600f);
            Assert.GreaterOrEqual(wild.Filter, 1f);
            Assert.LessOrEqual(wild.StillSeconds, 0.30f);
            Assert.GreaterOrEqual(wild.MinTravelMm, 0f);
            Assert.GreaterOrEqual(wild.AxisRatio, 1f);
        }

        [Test]
        public void 写しは別物になる()
        {
            var copy = _settings.Clone();
            copy.ReleaseSpeedMmPerSecond = 999f;

            Assert.AreNotEqual(999f, _settings.ReleaseSpeedMmPerSecond);
        }

        [Test]
        public void 落ちた理由が読める()
        {
            StringAssert.Contains("止めてから離した",
                FlickRule.Explain(400f, 0f, 30f, 0.20f, _settings));
            StringAssert.Contains("動かしていない",
                FlickRule.Explain(400f, 0f, 1f, 0f, _settings));
            StringAssert.Contains("遅い",
                FlickRule.Explain(50f, 0f, 30f, 0f, _settings));
            StringAssert.Contains("斜め",
                FlickRule.Explain(300f, 300f, 30f, 0f, _settings));
            StringAssert.Contains("右へ",
                FlickRule.Explain(400f, 0f, 30f, 0f, _settings));
            StringAssert.Contains("下へ",
                FlickRule.Explain(0f, -400f, 30f, 0f, _settings));
        }
    }
}
