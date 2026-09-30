using NUnit.Framework;
using Pentomino.Core;

namespace Pentomino.Tests
{
    /// <summary>
    /// はめ込みの吸い付き判定（開発仕様「ピースのはめ込み」）。
    /// 16px 以内に近づくと吸い込まれ、24px 以上離れると外れる。
    /// </summary>
    public class SnapRuleTests
    {
        private const float Attract = SnapRule.DefaultAttractDistance;   // 16
        private const float Release = SnapRule.DefaultReleaseDistance;   // 24

        [Test]
        public void 既定値が仕様どおり()
        {
            Assert.AreEqual(16f, Attract);
            Assert.AreEqual(24f, Release);
        }

        [Test]
        public void 十分近づくと吸い付く()
        {
            Assert.IsTrue(SnapRule.ShouldSnap(false, 0f));
            Assert.IsTrue(SnapRule.ShouldSnap(false, 10f));
            Assert.IsTrue(SnapRule.ShouldSnap(false, Attract), "16px ちょうどは吸い付く");
        }

        [Test]
        public void 遠いと吸い付かない()
        {
            Assert.IsFalse(SnapRule.ShouldSnap(false, Attract + 0.1f));
            Assert.IsFalse(SnapRule.ShouldSnap(false, 100f));
        }

        [Test]
        public void 吸い付いた後は少し離れても保つ()
        {
            // 吸い付く距離 (16) と外れる距離 (24) の間では、吸い付いたままにする。
            Assert.IsTrue(SnapRule.ShouldSnap(true, 20f));
            Assert.IsTrue(SnapRule.ShouldSnap(true, Release - 0.1f));
        }

        [Test]
        public void 指が離れると外れる()
        {
            Assert.IsFalse(SnapRule.ShouldSnap(true, Release), "24px 離れたら外れる");
            Assert.IsFalse(SnapRule.ShouldSnap(true, 40f));
        }

        [Test]
        public void 境目でちらつかない()
        {
            // 16 と 24 の間を行き来しても、状態が反転しないこと。
            for (var d = Attract + 0.5f; d < Release; d += 0.5f)
            {
                Assert.IsFalse(SnapRule.ShouldSnap(false, d), d + "px で吸い付いてはいけない");
                Assert.IsTrue(SnapRule.ShouldSnap(true, d), d + "px で外れてはいけない");
            }
        }

        [Test]
        public void しきい値を差し替えられる()
        {
            Assert.IsTrue(SnapRule.ShouldSnap(false, 30f, 32f, 48f));
            Assert.IsFalse(SnapRule.ShouldSnap(false, 33f, 32f, 48f));
            Assert.IsTrue(SnapRule.ShouldSnap(true, 40f, 32f, 48f));
        }

        [Test]
        public void 離す距離が吸う距離より小さくても壊れない()
        {
            // 設定を誤って逆転させても、吸い付いた瞬間に外れるような挙動にはしない。
            Assert.IsTrue(SnapRule.ShouldSnap(false, 16f, 16f, 8f));
            Assert.IsTrue(SnapRule.ShouldSnap(true, 15f, 16f, 8f));
        }

        [Test]
        public void 負の距離は吸い付かない()
        {
            Assert.IsFalse(SnapRule.ShouldSnap(false, -1f));
            Assert.IsFalse(SnapRule.ShouldSnap(true, -1f));
        }
    }
}
