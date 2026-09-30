using System.Collections.Generic;

namespace Pentomino.Core
{
    /// <summary>説明書の中身の種類。</summary>
    public enum ManualBlockKind
    {
        /// <summary>ひと続きの文章。</summary>
        Text,

        /// <summary>絵。押せる場合は行き先が付く。</summary>
        Image,
    }

    /// <summary>説明書を組み立てる一区切り。</summary>
    public struct ManualBlock
    {
        public ManualBlockKind Kind;

        /// <summary>Text のとき、その文章。</summary>
        public string Text;

        /// <summary>Image のとき、Resources から見た絵の道。</summary>
        public string Image;

        /// <summary>Image のとき、押したときの行き先。無ければ空。</summary>
        public string Url;

        public bool HasUrl => !string.IsNullOrEmpty(Url);
    }

    /// <summary>
    /// 説明書の本文を、文章と絵の並びに切り分ける。
    ///
    /// これまで説明書は素の文字だけだった。将来、操作を絵や動きで見せたくなるし、
    /// 参考文献では表紙を出して押せるようにしたい。そこで本文に目印を書けるようにし、
    /// 画面側はその並びどおりに部品を積む。
    ///
    /// 目印は行まるごとで書く。
    ///
    ///   [IMG Manual/images/表紙]                     絵だけ
    ///   [IMG Manual/images/表紙|https://example.com] 押すとその場所へ
    ///
    /// 行の途中に書いても目印にはならない。文章の中の "[" を気にせず書けるようにするため。
    /// </summary>
    public static class ManualBlocks
    {
        /// <summary>絵の目印の始まり。</summary>
        public const string ImageOpen = "[IMG ";

        /// <summary>目印の終わり。</summary>
        public const string MarkerClose = "]";

        /// <summary>絵の道と行き先を分ける文字。</summary>
        public const char UrlSeparator = '|';

        /// <summary>本文を、文章と絵の並びに切り分ける。</summary>
        public static List<ManualBlock> Parse(string body)
        {
            var blocks = new List<ManualBlock>();
            if (string.IsNullOrEmpty(body)) return blocks;

            var text = new List<string>();

            foreach (var line in SplitLines(body))
            {
                if (!TryReadImage(line, out var block))
                {
                    text.Add(line);
                    continue;
                }

                Flush(blocks, text);
                blocks.Add(block);
            }

            Flush(blocks, text);
            return blocks;
        }

        /// <summary>
        /// 目印を取り除いた文章を返す。絵を出せない場所（テストや書き出し）で使う。
        /// </summary>
        public static string StripMarkers(string body)
        {
            if (string.IsNullOrEmpty(body)) return string.Empty;

            var kept = new List<string>();
            foreach (var line in SplitLines(body))
            {
                if (TryReadImage(line, out _)) continue;
                kept.Add(line);
            }

            return string.Join("\n", kept.ToArray());
        }

        /// <summary>その行が絵の目印か。行まるごとで書かれている場合だけ。</summary>
        public static bool TryReadImage(string line, out ManualBlock block)
        {
            block = default;
            if (line == null) return false;

            var trimmed = line.Trim();
            if (!trimmed.StartsWith(ImageOpen) || !trimmed.EndsWith(MarkerClose)) return false;

            var inside = trimmed.Substring(
                ImageOpen.Length,
                trimmed.Length - ImageOpen.Length - MarkerClose.Length).Trim();

            if (inside.Length == 0) return false;

            var bar = inside.IndexOf(UrlSeparator);
            var path = bar < 0 ? inside : inside.Substring(0, bar).Trim();
            var url = bar < 0 ? string.Empty : inside.Substring(bar + 1).Trim();

            if (path.Length == 0) return false;

            block = new ManualBlock
            {
                Kind = ManualBlockKind.Image,
                Image = path,
                Url = url,
            };
            return true;
        }

        /// <summary>絵の目印を組み立てる（取り込み道具が使う）。</summary>
        public static string ImageMarker(string resourcePath, string url) =>
            string.IsNullOrEmpty(url)
                ? ImageOpen + resourcePath + MarkerClose
                : ImageOpen + resourcePath + UrlSeparator + url + MarkerClose;

        /// <summary>ためた行を、ひとつの文章として区切りに加える。</summary>
        private static void Flush(List<ManualBlock> blocks, List<string> lines)
        {
            if (lines.Count == 0) return;

            var text = string.Join("\n", lines.ToArray()).Trim('\n');
            lines.Clear();

            if (text.Trim().Length == 0) return;

            blocks.Add(new ManualBlock { Kind = ManualBlockKind.Text, Text = text });
        }

        private static string[] SplitLines(string body) =>
            body.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
    }
}
