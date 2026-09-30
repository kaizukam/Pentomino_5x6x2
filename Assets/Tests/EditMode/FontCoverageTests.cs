using System.Collections.Generic;
using System.Text;
using NUnit.Framework;
using Pentomino.Core;
using Pentomino.Data;
using UnityEngine;

namespace Pentomino.Tests
{
    /// <summary>
    /// 書体が、画面に出す文字をすべて持っているか。
    ///
    /// Assets/Fonts の書体は、このアプリが使う文字だけに削ってある（31MB → 1MB）。
    /// 削ったぶん、文章を書き足したのに削り直すのを忘れると、足した文字が
    /// 豆腐（□）になる。実機に出るまで気づけないので、ここで見張る。
    ///
    /// 赤くなったら、プロジェクトの根で
    ///     python Tools/subset_fonts.py
    /// を走らせて、Assets/Fonts と covered.txt をコミットする。
    /// </summary>
    public class FontCoverageTests
    {
        /// <summary>削った書体が持っている文字の控え。取り込み道具が書く。</summary>
        private const string CoveragePath = "Fonts/covered";

        /// <summary>削った書体が持っている文字の控え。削る道具が書く。</summary>
        private static string Covered()
        {
            var asset = Resources.Load<TextAsset>(CoveragePath);

            Assert.IsNotNull(asset,
                "Resources/" + CoveragePath + ".txt がありません。\n"
                + "  Unity で メニュー Pentomino ▸ フォントに要る文字を書き出す\n"
                + "  python Tools/subset_fonts.py");

            return asset.text;
        }

        /// <summary>画面に出しうる文字。数えるのは TextInventory ひとつだけ。</summary>
        private static SortedSet<char> Needed() => TextInventory.All();

        [Test]
        public void 削った書体に文字の取りこぼしがない()
        {
            var covered = new HashSet<char>(Covered());
            var missing = new StringBuilder();
            var count = 0;

            foreach (var c in Needed())
            {
                if (covered.Contains(c)) continue;

                count++;
                if (count <= 40) missing.Append(c);
            }

            Assert.AreEqual(0, count,
                "書体に無い文字が " + count + " 種あります: " + missing + "\n"
                + "文章を書き足したあとに削り直していない可能性があります。\n"
                + "  python Tools/subset_fonts.py");
        }

        [Test]
        public void 控えが空でない()
        {
            Assert.Greater(Covered().Length, 100, "控えが短すぎます");
        }
    }
}
