using NUnit.Framework;
using Pentomino.Core;

namespace Pentomino.Tests
{
    /// <summary>
    /// 未収納ピースの詰め方。段の高さを揃えず、上へ持ち上げて詰める。
    /// 幅 100、間隔 0 で考える（数えやすいので）。
    /// </summary>
    public class TrayPackingTests
    {
        private static TrayPacking.Result Pack(float[] w, float[] h, float width = 100f, float gap = 0f) =>
            TrayPacking.Arrange(w, h, width, gap);

        [Test]
        public void 横に並ぶうちは同じ高さに置く()
        {
            var r = Pack(new[] { 30f, 30f, 30f }, new[] { 10f, 20f, 30f });

            Assert.AreEqual(0f, r.Slots[0].X);
            Assert.AreEqual(30f, r.Slots[1].X);
            Assert.AreEqual(60f, r.Slots[2].X);
            foreach (var s in r.Slots) Assert.AreEqual(0f, s.Y);
        }

        [Test]
        public void 幅に入らなくなったら左へ折り返す()
        {
            var r = Pack(new[] { 60f, 60f }, new[] { 10f, 10f });

            Assert.AreEqual(0f, r.Slots[0].X);
            Assert.AreEqual(0f, r.Slots[1].X);
            Assert.AreEqual(10f, r.Slots[1].Y, "1 つ目の下に来る");
        }

        [Test]
        public void 背の低いピースの下へ潜り込める()
        {
            // 高さ 10 の左と、高さ 40 の右。3 つ目は左の下（10）に入る。
            // 段で揃えると 40 まで下がってしまう。
            var r = Pack(new[] { 40f, 40f, 40f }, new[] { 10f, 40f, 10f });

            Assert.AreEqual(0f, r.Slots[2].X, "左へ折り返す");
            Assert.AreEqual(10f, r.Slots[2].Y, "背の低い方の真下まで上がる");
        }

        [Test]
        public void またがっている物の下までしか上がれない()
        {
            // 3 つ目は 1 つ目（高さ 10）と 2 つ目（高さ 40）の両方にかかる。
            var r = Pack(new[] { 50f, 50f, 100f }, new[] { 10f, 40f, 10f });

            Assert.AreEqual(40f, r.Slots[2].Y, "高いほうに合わせる");
        }

        [Test]
        public void 触れているだけなら重なりとみなさない()
        {
            // 幅 50 が 2 つで、ちょうど右端まで。3 つ目は左端で、2 つ目には掛からない。
            var r = Pack(new[] { 50f, 50f, 50f }, new[] { 10f, 40f, 10f });

            Assert.AreEqual(0f, r.Slots[2].X);
            Assert.AreEqual(10f, r.Slots[2].Y);
        }

        [Test]
        public void 間隔を空けて置ける()
        {
            var r = Pack(new[] { 30f, 30f }, new[] { 10f, 10f }, 100f, 5f);

            Assert.AreEqual(35f, r.Slots[1].X);
        }

        [Test]
        public void 全体の高さを返す()
        {
            var r = Pack(new[] { 60f, 60f }, new[] { 10f, 25f }, 100f, 5f);

            // 2 つ目は 1 つ目の下（10 + 5 = 15）から 25 なので、下端は 40。
            Assert.AreEqual(40f + 5f, r.Height, 0.001f);
        }

        [Test]
        public void 幅より広いピースでも消えない()
        {
            var r = Pack(new[] { 200f }, new[] { 10f });

            Assert.AreEqual(1, r.Slots.Length);
            Assert.AreEqual(0f, r.Slots[0].X);
        }

        [Test]
        public void 何も無ければ高さは0()
        {
            var r = Pack(new float[0], new float[0]);

            Assert.AreEqual(0, r.Slots.Length);
            Assert.AreEqual(0f, r.Height);
        }
    }
}
