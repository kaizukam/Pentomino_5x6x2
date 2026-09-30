using NUnit.Framework;
using Pentomino.Core;

namespace Pentomino.Tests
{
    /// <summary>
    /// 運んでいるピースを遊び面から出さない規則。
    /// 遊び面は左 0・右 1000、上 0・下 -2000（上が正）。ピースは 200 x 300。
    /// </summary>
    public class PlayAreaRuleTests
    {
        private const float AreaLeft = 0f, AreaRight = 1000f, AreaTop = 0f, AreaBottom = -2000f;
        private const float Width = 200f, Height = 300f;

        /// <summary>左上の角を (x, y) に置いたピースのずらし量。</summary>
        private static (float dx, float dy) Correct(float x, float y)
        {
            PlayAreaRule.Correction(x, x + Width, y - Height, y,
                AreaLeft, AreaRight, AreaBottom, AreaTop, out var dx, out var dy);
            return (dx, dy);
        }

        [Test]
        public void 中に収まっていれば動かさない()
        {
            Assert.AreEqual((0f, 0f), Correct(400f, -500f));
        }

        [Test]
        public void 縁にぴったり付いていても動かさない()
        {
            Assert.AreEqual((0f, 0f), Correct(0f, 0f));
            Assert.AreEqual((0f, 0f), Correct(AreaRight - Width, AreaBottom + Height));
        }

        [Test]
        public void 帯に掛かったら上端がBOXの上端に来るまで押し下げる()
        {
            var (dx, dy) = Correct(400f, 120f);

            Assert.AreEqual(0f, dx, 0.001f);
            Assert.AreEqual(-120f, dy, 0.001f);
        }

        [Test]
        public void 左へ出たらその分だけ右へ戻す()
        {
            var (dx, dy) = Correct(-50f, -500f);

            Assert.AreEqual(50f, dx, 0.001f);
            Assert.AreEqual(0f, dy, 0.001f);
        }

        [Test]
        public void 右へ出たらその分だけ左へ戻す()
        {
            var (dx, dy) = Correct(900f, -500f);

            // 右端 1100 が 1000 を 100 超えている。
            Assert.AreEqual(-100f, dx, 0.001f);
            Assert.AreEqual(0f, dy, 0.001f);
        }

        [Test]
        public void 下へ出たらその分だけ上へ戻す()
        {
            var (dx, dy) = Correct(400f, -1900f);

            // 下端 -2200 が -2000 を 200 超えている。
            Assert.AreEqual(0f, dx, 0.001f);
            Assert.AreEqual(200f, dy, 0.001f);
        }

        [Test]
        public void 角から出たら両方向を同時に戻す()
        {
            var (dx, dy) = Correct(-30f, 40f);

            Assert.AreEqual(30f, dx, 0.001f);
            Assert.AreEqual(-40f, dy, 0.001f);
        }

        [Test]
        public void 遊び面より大きければ左上を揃える()
        {
            PlayAreaRule.Correction(-100f, 1200f, -2500f, 100f,
                AreaLeft, AreaRight, AreaBottom, AreaTop, out var dx, out var dy);

            Assert.AreEqual(100f, dx, 0.001f);
            Assert.AreEqual(-100f, dy, 0.001f);
        }
    }
}
