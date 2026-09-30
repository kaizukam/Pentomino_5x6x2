using NUnit.Framework;
using Pentomino.Core;

namespace Pentomino.Tests
{
    /// <summary>
    /// 問題送りボタンの繰り返し判定（開発仕様「画面TOPナビゲーション」）。
    /// 軽く押せば 1 問。1 秒後から 0.3 秒ごとに 10 問、3 秒を超えると 100 問ずつ。
    /// </summary>
    public class StepRepeatRuleTests
    {
        [Test]
        public void 既定値が仕様どおり()
        {
            Assert.AreEqual(1f, StepRepeatRule.DefaultHoldDelay);
            Assert.AreEqual(0.3f, StepRepeatRule.DefaultRepeatInterval);
            Assert.AreEqual(3f, StepRepeatRule.DefaultFastDelay);
            Assert.AreEqual(1, StepRepeatRule.DefaultTapStep);
            Assert.AreEqual(10, StepRepeatRule.DefaultRepeatStep);
            Assert.AreEqual(100, StepRepeatRule.DefaultFastStep);
        }

        [Test]
        public void 一秒より前は繰り返さない()
        {
            Assert.AreEqual(0, StepRepeatRule.RepeatsSoFar(0f));
            Assert.AreEqual(0, StepRepeatRule.RepeatsSoFar(0.99f));
            Assert.AreEqual(0, StepRepeatRule.TotalStepSoFar(0.99f));
        }

        [Test]
        public void 一秒ちょうどで一回目が起きる()
        {
            Assert.AreEqual(1, StepRepeatRule.RepeatsSoFar(1f));
            Assert.AreEqual(10, StepRepeatRule.TotalStepSoFar(1f));
        }

        [Test]
        public void 三秒までは十問ずつ増える()
        {
            // 1.0 / 1.3 / 1.6 / 1.9 ... と 0.3 秒ごと。
            Assert.AreEqual(2, StepRepeatRule.RepeatsSoFar(1.3f));
            Assert.AreEqual(20, StepRepeatRule.TotalStepSoFar(1.3f));

            Assert.AreEqual(3, StepRepeatRule.RepeatsSoFar(1.6f));
            Assert.AreEqual(30, StepRepeatRule.TotalStepSoFar(1.6f));

            // 2.8 秒の時点では 1.0 から 0.3 刻みで 7 回、すべて 10 問ずつ。
            Assert.AreEqual(7, StepRepeatRule.RepeatsSoFar(2.8f));
            Assert.AreEqual(70, StepRepeatRule.TotalStepSoFar(2.8f));
        }

        [Test]
        public void 三秒を超えると百問ずつになる()
        {
            // 3.1 秒での 8 回目が最初の加速後。1.0 + 0.3*7 = 3.1
            Assert.AreEqual(8, StepRepeatRule.RepeatsSoFar(3.1f));
            Assert.AreEqual(70 + 100, StepRepeatRule.TotalStepSoFar(3.1f));

            Assert.AreEqual(9, StepRepeatRule.RepeatsSoFar(3.4f));
            Assert.AreEqual(70 + 200, StepRepeatRule.TotalStepSoFar(3.4f));
        }

        [Test]
        public void 累計は減らない()
        {
            var previous = 0;
            for (var t = 0f; t <= 10f; t += 0.05f)
            {
                var total = StepRepeatRule.TotalStepSoFar(t);
                Assert.GreaterOrEqual(total, previous, t + " 秒で累計が減った");
                previous = total;
            }
        }

        [Test]
        public void 繰り返し前に離せば一問だけ動く()
        {
            Assert.IsTrue(StepRepeatRule.ShouldStepOnRelease(0));
        }

        [Test]
        public void 繰り返した後に離しても余分に動かない()
        {
            Assert.IsFalse(StepRepeatRule.ShouldStepOnRelease(1));
            Assert.IsFalse(StepRepeatRule.ShouldStepOnRelease(20));
        }

        [Test]
        public void 待ち時間と間隔を差し替えられる()
        {
            Assert.AreEqual(0, StepRepeatRule.RepeatsSoFar(0.9f, 1f, 0.25f));
            Assert.AreEqual(1, StepRepeatRule.RepeatsSoFar(1f, 1f, 0.25f));
            Assert.AreEqual(3, StepRepeatRule.RepeatsSoFar(1.5f, 1f, 0.25f));
        }

        [Test]
        public void 間隔が0でも壊れない()
        {
            Assert.AreEqual(0, StepRepeatRule.RepeatsSoFar(0.5f, 1f, 0f));
            Assert.AreEqual(1, StepRepeatRule.RepeatsSoFar(3f, 1f, 0f));
        }
    }
}
