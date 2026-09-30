using NUnit.Framework;
using Pentomino.Core;

namespace Pentomino.Tests
{
    /// <summary>
    /// 版番号の上げ方。ストアの受付番号は戻せないので、上げ間違いは痛い。
    /// </summary>
    public class VersionRuleTests
    {
        private static string Bump(string version, VersionPart part)
        {
            Assert.IsTrue(VersionRule.TryBump(version, part, out var next),
                version + " を読めませんでした");
            return next;
        }

        [Test]
        public void 直しただけなら一番下を上げる()
        {
            Assert.AreEqual("0.4.2", Bump("0.4.1", VersionPart.Fix));
            Assert.AreEqual("0.4.10", Bump("0.4.9", VersionPart.Fix));
        }

        [Test]
        public void 何かを足したら真ん中を上げて下を戻す()
        {
            Assert.AreEqual("0.5.0", Bump("0.4.1", VersionPart.Feature));
            Assert.AreEqual("0.5.0", Bump("0.4.9", VersionPart.Feature));
        }

        [Test]
        public void 作り直したら一番上を上げて下を戻す()
        {
            Assert.AreEqual("1.0.0", Bump("0.4.1", VersionPart.Major));
            Assert.AreEqual("2.0.0", Bump("1.9.9", VersionPart.Major));
        }

        [Test]
        public void 三段でなければ読めない()
        {
            Assert.IsFalse(VersionRule.TryBump("0.4", VersionPart.Fix, out _));
            Assert.IsFalse(VersionRule.TryBump("1.0.0.0", VersionPart.Fix, out _));
            Assert.IsFalse(VersionRule.TryBump("Rev 0.5.1", VersionPart.Fix, out _));
            Assert.IsFalse(VersionRule.TryBump(null, VersionPart.Fix, out _));
        }

        [Test]
        public void 読めなければ元の値を返す()
        {
            VersionRule.TryBump("おかしな値", VersionPart.Fix, out var next);
            Assert.AreEqual("おかしな値", next, "読めないのに書き換えてはいけません");
        }

        [Test]
        public void 桁を読み分ける()
        {
            Assert.IsTrue(VersionRule.TryParse(" 12.34.56 ", out var major, out var minor, out var fix));
            Assert.AreEqual(12, major);
            Assert.AreEqual(34, minor);
            Assert.AreEqual(56, fix);
        }

        [Test]
        public void 受付番号は一つずつ増える()
        {
            Assert.AreEqual(2, VersionRule.NextBuildNumber(1));
            Assert.AreEqual(101, VersionRule.NextBuildNumber(100));
        }

        [Test]
        public void 受付番号が壊れていたら一から数え直す()
        {
            // 0 や負の値のまま出すと、ストアに二度と受け付けてもらえない番号になる。
            Assert.AreEqual(1, VersionRule.NextBuildNumber(0));
            Assert.AreEqual(1, VersionRule.NextBuildNumber(-5));
            Assert.AreEqual("1", VersionRule.NextBuildNumber("読めない値"));
            Assert.AreEqual("1", VersionRule.NextBuildNumber(""));
        }

        [Test]
        public void 文字で持つ受付番号も増やせる()
        {
            Assert.AreEqual("2", VersionRule.NextBuildNumber("1"));
            Assert.AreEqual("13", VersionRule.NextBuildNumber(" 12 "));
        }
    }
}
