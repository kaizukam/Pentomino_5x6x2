using NUnit.Framework;
using Pentomino.Core;

namespace Pentomino.Tests
{
    /// <summary>
    /// 指を追って、離した瞬間に一度だけ判定する仕掛け。
    ///
    /// 動かしている間は何も起きない。ここが以前との一番の違いで、
    /// 普通にピースを運んでいる最中に反転してしまうことが無くなる。
    /// </summary>
    public class FlickDetectorTests
    {
        private FlickDetector _detector;
        private FlickSettings _settings;

        [SetUp]
        public void SetUp()
        {
            _detector = new FlickDetector();
            _settings = new FlickSettings();
            _detector.Filter = _settings.Filter;
        }

        /// <summary>毎コマ 16ms で、横へ一定の速さで滑らせる。</summary>
        private float Slide(float speedMmPerSecond, int frames, float startTime = 0f)
        {
            var step = 1f / 60f;
            var now = startTime;
            var x = 0f;

            _detector.Begin(0f, 0f, now);

            for (var i = 0; i < frames; i++)
            {
                now += step;
                x += speedMmPerSecond * step;
                _detector.Feed(x, 0f, now);
            }

            return now;
        }

        [Test]
        public void 動かしている間は何も起きない()
        {
            Slide(500f, 20);

            Assert.AreEqual(FlickAxis.None, _detector.LastAxis, "運んでいる最中に反転しています");
            Assert.IsTrue(_detector.IsTracking);
        }

        [Test]
        public void 速いまま離すと反転する()
        {
            var now = Slide(500f, 20);

            Assert.AreEqual(FlickAxis.Horizontal, _detector.Release(now, _settings));
            Assert.IsFalse(_detector.IsTracking);
        }

        [Test]
        public void 止めてから離すと反転しない()
        {
            var now = Slide(500f, 20);

            // 指を止めたまま、しばらくしてから離す。
            // 動かないと OnDrag は来ないので、時間だけが進む。
            Assert.AreEqual(FlickAxis.None, _detector.Release(now + 0.15f, _settings));
        }

        [Test]
        public void ゆっくり運んで離しても反転しない()
        {
            var now = Slide(60f, 40);

            Assert.AreEqual(FlickAxis.None, _detector.Release(now, _settings));
        }

        [Test]
        public void 総移動量を数える()
        {
            Slide(300f, 12);   // 300mm/s x 0.2s = 60mm

            Assert.AreEqual(60f, _detector.TravelMm, 1f);
        }

        [Test]
        public void ならした速度が実際の速さに近づく()
        {
            Slide(300f, 30);

            Assert.AreEqual(300f, _detector.SpeedMmPerSecond, 30f);
        }

        [Test]
        public void 判定を終えるとならした速度は0に戻る()
        {
            var now = Slide(500f, 20);
            _detector.Release(now, _settings);

            Assert.AreEqual(0f, _detector.SpeedMmPerSecond, 0.001f,
                "離した勢いが残っていると、次の判定に混ざります");
        }

        [Test]
        public void 判定に使った速度は表示のために残る()
        {
            var now = Slide(500f, 20);
            _detector.Release(now, _settings);

            Assert.Greater(_detector.LastSpeedMmPerSecond, 100f, "表示する値まで消えています");
            StringAssert.Contains("左右反転", _detector.LastReason(_settings));
        }

        [Test]
        public void 掴み直すと測り直す()
        {
            var now = Slide(500f, 20);
            _detector.Release(now, _settings);

            _detector.Begin(0f, 0f, now);

            Assert.AreEqual(0f, _detector.SpeedMmPerSecond, 0.001f);
            Assert.AreEqual(0f, _detector.TravelMm, 0.001f);
            Assert.AreEqual(0, _detector.Trace.Count);
        }

        [Test]
        public void 追跡していなければ離しても何も起きない()
        {
            Assert.AreEqual(FlickAxis.None, _detector.Release(1f, _settings));
        }

        [Test]
        public void 止めた追跡は離しても反転しない()
        {
            var now = Slide(500f, 20);
            _detector.Stop();

            Assert.AreEqual(FlickAxis.None, _detector.Release(now, _settings));
        }

        [Test]
        public void 縦に滑らせて離すと上下反転()
        {
            var step = 1f / 60f;
            var now = 0f;
            var y = 0f;

            _detector.Begin(0f, 0f, now);
            for (var i = 0; i < 20; i++)
            {
                now += step;
                y += 500f * step;
                _detector.Feed(0f, y, now);
            }

            Assert.AreEqual(FlickAxis.Vertical, _detector.Release(now, _settings));
        }

        [Test]
        public void 届いたコマを記録している()
        {
            Slide(300f, 15);

            Assert.AreEqual(15, _detector.Trace.Count);
            Assert.AreEqual(60f, _detector.Trace.PerSecond, 1f);
        }

        [Test]
        public void フレームが飛んでも判定が暴れない()
        {
            // 重いフレームが挟まっても、ならしているので速度が跳ねない。
            var step = 1f / 60f;
            var now = 0f;
            var x = 0f;

            _detector.Begin(0f, 0f, now);
            for (var i = 0; i < 20; i++)
            {
                var gap = i == 10 ? 0.1f : step;   // ← 引っかかったコマ
                now += gap;
                x += 60f * gap;                    // ゆっくり運んでいる
                _detector.Feed(x, 0f, now);
            }

            Assert.AreEqual(FlickAxis.None, _detector.Release(now, _settings),
                "運んでいるだけなのに反転しています");
        }
    }
}
