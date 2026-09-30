using NUnit.Framework;
using Pentomino.Core;
using Pentomino.Data;
using UnityEngine;

namespace Pentomino.Tests
{
    /// <summary>
    /// 説明書を、文章と絵の並びに切り分ける仕掛け。
    /// 参考文献では表紙を出して押せるようにし、この先は操作を絵でも見せる。
    /// </summary>
    public class ManualBlocksTests
    {
        [Test]
        public void 文章だけならひと区切り()
        {
            var blocks = ManualBlocks.Parse("一行目\n二行目");

            Assert.AreEqual(1, blocks.Count);
            Assert.AreEqual(ManualBlockKind.Text, blocks[0].Kind);
            Assert.AreEqual("一行目\n二行目", blocks[0].Text);
        }

        [Test]
        public void 絵の目印で切り分ける()
        {
            var blocks = ManualBlocks.Parse("まえがき\n[IMG Manual/images/表紙]\nあとがき");

            Assert.AreEqual(3, blocks.Count);
            Assert.AreEqual("まえがき", blocks[0].Text);

            Assert.AreEqual(ManualBlockKind.Image, blocks[1].Kind);
            Assert.AreEqual("Manual/images/表紙", blocks[1].Image);
            Assert.IsFalse(blocks[1].HasUrl);

            Assert.AreEqual("あとがき", blocks[2].Text);
        }

        [Test]
        public void 行き先の付いた絵()
        {
            var blocks = ManualBlocks.Parse("[IMG Manual/images/表紙|https://example.com/a]");

            Assert.AreEqual(1, blocks.Count);
            Assert.IsTrue(blocks[0].HasUrl);
            Assert.AreEqual("Manual/images/表紙", blocks[0].Image);
            Assert.AreEqual("https://example.com/a", blocks[0].Url);
        }

        [Test]
        public void 行の途中に書いても目印にはならない()
        {
            // 文章の中の「[」を気にせず書けるようにしてある。
            var blocks = ManualBlocks.Parse("ここで [IMG なにか] と書いても文章のまま");

            Assert.AreEqual(1, blocks.Count);
            Assert.AreEqual(ManualBlockKind.Text, blocks[0].Kind);
        }

        [Test]
        public void 中身の無い目印は文章として扱う()
        {
            var blocks = ManualBlocks.Parse("[IMG ]");

            Assert.AreEqual(1, blocks.Count);
            Assert.AreEqual(ManualBlockKind.Text, blocks[0].Kind);
        }

        [Test]
        public void 空白だけの区切りは作らない()
        {
            var blocks = ManualBlocks.Parse("\n\n[IMG a]\n\n\n[IMG b]\n\n");

            Assert.AreEqual(2, blocks.Count);
            foreach (var block in blocks) Assert.AreEqual(ManualBlockKind.Image, block.Kind);
        }

        [Test]
        public void 目印を外した文章を取り出せる()
        {
            var text = ManualBlocks.StripMarkers("まえがき\n[IMG a|https://example.com]\nあとがき");

            Assert.AreEqual("まえがき\nあとがき", text);
        }

        [Test]
        public void 組み立てた目印を読み戻せる()
        {
            var marker = ManualBlocks.ImageMarker("Manual/images/表紙", "https://example.com/b");

            Assert.IsTrue(ManualBlocks.TryReadImage(marker, out var block));
            Assert.AreEqual("Manual/images/表紙", block.Image);
            Assert.AreEqual("https://example.com/b", block.Url);
        }

        [Test]
        public void 参考文献の絵がすべて読み込める()
        {
            // 取り込み道具が Resources へ写しているか。
            // 道が食い違うと、実機で絵の場所が空くだけで、誰も気づかない。
            var blocks = ManualBlocks.Parse(GameData.LoadReference());

            var images = 0;
            foreach (var block in blocks)
            {
                if (block.Kind != ManualBlockKind.Image) continue;
                images++;

                Assert.IsNotNull(Resources.Load<Sprite>(block.Image),
                    "Resources/" + block.Image + " が読めません。"
                    + "メニュー Pentomino ▸ 参考文献を取り込む を実行してください。");
            }

            Assert.Greater(images, 0, "参考文献に絵がありません");
        }

        [Test]
        public void 参考文献の絵にはすべて行き先が付いている()
        {
            // 原稿では表紙がそのまま Amazon への入り口になっている。
            foreach (var block in ManualBlocks.Parse(GameData.LoadReference()))
            {
                if (block.Kind != ManualBlockKind.Image) continue;

                Assert.IsTrue(block.HasUrl, block.Image + " に行き先がありません");
                StringAssert.StartsWith("http", block.Url);
            }
        }
    }
}
