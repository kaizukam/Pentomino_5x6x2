using NUnit.Framework;
using Pentomino.Core;

namespace Pentomino.Tests
{
    /// <summary>
    /// 指を動かしている間に、実際に何が届いたかの記録。
    /// しきい値を決めるための材料で、判定そのものには使わない。
    /// </summary>
    public class FlickTraceTests
    {
        private FlickTrace _trace;

        [SetUp]
        public void SetUp() => _trace = new FlickTrace();

        [Test]
        public void 何も入れなければ空()
        {
            Assert.AreEqual(0, _trace.Count);
            Assert.AreEqual(0f, _trace.PerSecond);
            StringAssert.Contains("まだ測っていません", _trace.Summary());
        }

        [Test]
        public void 間隔から毎秒の回数を出す()
        {
            for (var i = 0; i < 10; i++) _trace.Add(1f / 60f, 2f);

            Assert.AreEqual(60f, _trace.PerSecond, 0.1f);
            Assert.AreEqual(1f / 60f, _trace.AverageSeconds, 0.0001f);
        }

        [Test]
        public void 最長と最短の間隔を覚える()
        {
            _trace.Add(0.010f, 1f);
            _trace.Add(0.050f, 1f);
            _trace.Add(0.020f, 1f);

            Assert.AreEqual(0.010f, _trace.MinSeconds, 0.0001f);
            Assert.AreEqual(0.050f, _trace.MaxSeconds, 0.0001f);
        }

        [Test]
        public void 動いた距離の平均と最大を出す()
        {
            _trace.Add(0.016f, 1f);
            _trace.Add(0.016f, 5f);

            Assert.AreEqual(3f, _trace.AverageDistance, 0.001f);
            Assert.AreEqual(5f, _trace.MaxDistance, 0.001f);
        }

        [Test]
        public void 間隔がそろっていればばらつきは0()
        {
            for (var i = 0; i < 5; i++) _trace.Add(0.016f, 1f);

            Assert.AreEqual(0f, _trace.Unevenness, 0.001f);
        }

        [Test]
        public void フレームが飛ぶとばらつきが大きくなる()
        {
            // 重い画面では、こういう並びになる。
            _trace.Add(0.016f, 1f);
            _trace.Add(0.016f, 1f);
            _trace.Add(0.100f, 6f);   // ← 引っかかったコマ
            _trace.Add(0.016f, 1f);

            Assert.Greater(_trace.Unevenness, 0.5f, "飛びを拾えていません");
        }

        [Test]
        public void 間隔0のコマは数えない()
        {
            // 同じフレームで二度届いた分。平均を狂わせるだけなので捨てる。
            _trace.Add(0f, 3f);
            _trace.Add(0.016f, 1f);

            Assert.AreEqual(1, _trace.Count);
            Assert.AreEqual(1f, _trace.AverageDistance, 0.001f);
        }

        [Test]
        public void 覚えるのは決めた回数まで()
        {
            for (var i = 0; i < FlickTrace.Capacity + 30; i++) _trace.Add(0.016f, 1f);

            Assert.AreEqual(FlickTrace.Capacity, _trace.Count);
            Assert.AreEqual(FlickTrace.Capacity + 30, _trace.Total, "総数は頭打ちにしない");
        }

        [Test]
        public void 触れ直したら捨てる()
        {
            _trace.Add(0.016f, 1f);
            _trace.Clear();

            Assert.AreEqual(0, _trace.Count);
            Assert.AreEqual(0, _trace.Total);
        }

        [Test]
        public void 要約に必要な数字が並ぶ()
        {
            for (var i = 0; i < 10; i++) _trace.Add(1f / 60f, 2f);

            var summary = _trace.Summary();
            StringAssert.Contains("毎秒 60 回", summary);
            StringAssert.Contains("ms", summary);
            StringAssert.Contains("ばらつき", summary);
            StringAssert.Contains("2.0mm", summary);
        }
    }
}
