using NUnit.Framework;
using Pentomino.Core;

namespace Pentomino.Tests
{
    /// <summary>
    /// 掴んでいるピースが指から外れたときの引き寄せ（開発仕様「フリックの操作性」）。
    /// ピースは左端 0・右端 100、下端 -60・上端 0 とする。余裕は 10。
    /// </summary>
    public class GrabRuleTests
    {
        private const float Left = 0f, Right = 100f, Bottom = -60f, Top = 0f, Slack = 10f;

        private static (float dx, float dy) Correct(float x, float y)
        {
            GrabRule.Correction(x, y, Left, Right, Bottom, Top, Slack, out var dx, out var dy);
            return (dx, dy);
        }

        [Test]
        public void 指がピースの上にあれば動かさない()
        {
            Assert.AreEqual((0f, 0f), Correct(50f, -30f));
        }

        [Test]
        public void 余裕の内側なら動かさない()
        {
            Assert.AreEqual((0f, 0f), Correct(108f, 5f));
        }

        [Test]
        public void 右へ外れたらその分だけ右へ動かす()
        {
            var (dx, dy) = Correct(150f, -30f);

            // 右端 100 + 余裕 10 = 110 を 40 超えている。
            Assert.AreEqual(40f, dx, 0.001f);
            Assert.AreEqual(0f, dy, 0.001f);
        }

        [Test]
        public void 左へ外れたら負の向きに動かす()
        {
            var (dx, _) = Correct(-35f, -30f);
            Assert.AreEqual(-25f, dx, 0.001f);
        }

        [Test]
        public void 下へ外れたら下へ動かす()
        {
            var (_, dy) = Correct(50f, -100f);
            Assert.AreEqual(-30f, dy, 0.001f);
        }

        [Test]
        public void 斜めに外れたら両方を直す()
        {
            var (dx, dy) = Correct(150f, 30f);

            Assert.AreEqual(40f, dx, 0.001f);
            Assert.AreEqual(20f, dy, 0.001f);
        }

        [Test]
        public void 外れているかどうかを聞ける()
        {
            Assert.IsFalse(GrabRule.NeedsCorrection(50f, -30f, Left, Right, Bottom, Top, Slack));
            Assert.IsTrue(GrabRule.NeedsCorrection(150f, -30f, Left, Right, Bottom, Top, Slack));
        }

        [Test]
        public void 余裕が負でも余裕なしとして扱う()
        {
            GrabRule.Correction(105f, -30f, Left, Right, Bottom, Top, -5f, out var dx, out _);
            Assert.AreEqual(5f, dx, 0.001f);
        }
    }
}
