using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using Pentomino.Core;
using UnityEditor;
using UnityEngine;

namespace Pentomino.EditorTools
{
    /// <summary>
    /// 参考文献の原稿（HTML）を、説明書に差し込める文章へ写す。
    ///
    /// 原稿は DataBase/Reference/index.html にある。あちらが正で、
    /// ここが作るのは写しなので、原稿を直したらもう一度実行する。
    ///
    /// 説明書は素の文字（uGUI の Text）で出しているので、HTML の飾りは使えない。
    /// 見出しは記号に置き換え、表紙の絵は落とし、リンクは URL を添える。
    ///
    /// 書名はそれぞれの言語で書かれている（英語版は英語、独語版は独語）ので、
    /// 言語ごとに訳し分けたりはしない。8 か国語すべてに同じ文章が入る。
    /// </summary>
    public static class ReferenceImporter
    {
        /// <summary>原稿。Git には入らないので、Windows 側だけにある。</summary>
        public const string SourcePath = "DataBase/Reference/index.html";

        /// <summary>写し先。こちらは Git に入る。</summary>
        public const string OutputPath = "Assets/Resources/Manual/reference.txt";

        /// <summary>絵の写し先。Resources の下でないと実行時に読めない。</summary>
        public const string ImageFolder = "Assets/Resources/Manual/images";

        /// <summary>Resources から見た絵の道の頭。</summary>
        public const string ImageResourceFolder = "Manual/images";

        [MenuItem("Pentomino/参考文献を取り込む")]
        public static void Import()
        {
            var source = Path.Combine(Directory.GetCurrentDirectory(), SourcePath);
            if (!File.Exists(source))
            {
                Debug.LogError("参考文献の原稿がありません: " + SourcePath + "\n"
                    + "原稿は Windows 側の DataBase にあります。Mac では実行できません。");
                return;
            }

            var text = ToPlainText(File.ReadAllText(source, Encoding.UTF8));
            var copied = CopyImages(Path.GetDirectoryName(source));

            var handEdits = CountHandEdits(text);

            Directory.CreateDirectory(Path.GetDirectoryName(OutputPath));
            File.WriteAllText(OutputPath, text, new UTF8Encoding(false));
            AssetDatabase.ImportAsset(OutputPath);
            AssetDatabase.Refresh();

            var lines = text.Split('\n').Length;
            var report = "参考文献を取り込みました: " + OutputPath + "\n"
                + "  " + lines + " 行 / " + text.Length + " 文字\n"
                + "  絵 " + copied + " 枚を " + ImageFolder + " へ写しました\n"
                + "説明書の {Reference} の場所に、そのまま入ります。";

            if (handEdits == 0)
            {
                Debug.Log(report);
                return;
            }

            Debug.LogWarning(report + "\n\n"
                + "※ 手で直した所が " + handEdits + " 行、原稿の内容で上書きされました。\n"
                + "  " + OutputPath + " は原稿から作る写しなので、ここへの書き込みは残りません。\n"
                + "  直したい所は原稿（Obsidian の Reference）で直してください。\n"
                + "  上書き前の姿は " + BackupFolder + " に控えてあります。");
        }

        /// <summary>控えを置く場所。Assets の外なので Unity も Git も見ない。</summary>
        public const string BackupFolder = "Backup/Manual";

        /// <summary>
        /// いまある写しが、原稿から作った文章と食い違っている行数を返す。
        ///
        /// 写しのほうを手で直してしまうと、次の取り込みで黙って消える。
        /// 実際に一度そうなったので、消える前に控えを取り、何行消えたかを知らせる。
        /// 誰も直していなければ 0 なので、普段は何も起きない。
        /// </summary>
        private static int CountHandEdits(string fresh)
        {
            if (!File.Exists(OutputPath)) return 0;

            var current = File.ReadAllText(OutputPath, Encoding.UTF8);
            if (current == fresh) return 0;

            var before = current.Replace("\r\n", "\n").Split('\n');
            var after = fresh.Replace("\r\n", "\n").Split('\n');

            var differing = 0;
            var max = before.Length > after.Length ? before.Length : after.Length;
            for (var i = 0; i < max; i++)
            {
                var a = i < before.Length ? before[i] : string.Empty;
                var b = i < after.Length ? after[i] : string.Empty;
                if (a != b) differing++;
            }

            var folder = Path.Combine(Directory.GetCurrentDirectory(), BackupFolder);
            Directory.CreateDirectory(folder);

            var name = "reference_" + System.DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".txt";
            File.WriteAllText(Path.Combine(folder, name), current, new UTF8Encoding(false));

            return differing;
        }

        /// <summary>
        /// 表紙の絵を Resources へ写し、スプライトとして読み込ませる。
        ///
        /// 原稿のある DataBase は Git にも Unity にも入らないので、
        /// そこに置いたままでは実行時に読めない。写しを作る。
        /// </summary>
        private static int CopyImages(string sourceFolder)
        {
            var from = Path.Combine(sourceFolder, "images");
            if (!Directory.Exists(from)) return 0;

            Directory.CreateDirectory(ImageFolder);

            var kept = new List<string>();

            var copied = 0;
            foreach (var path in Directory.GetFiles(from))
            {
                if (!IsImage(path)) continue;

                var name = Path.GetFileName(path);
                var destination = Path.Combine(ImageFolder, name);
                File.Copy(path, destination, true);

                AssetDatabase.ImportAsset(destination.Replace('\\', '/'));
                MakeSprite(destination.Replace('\\', '/'));

                kept.Add(name);
                copied++;
            }

            // 原稿から消えた絵は、写しからも消す。
            // 残しておくと、誰も参照していない絵をアプリに積み続けることになる。
            // 実際、電子書籍の章を消したあとも表紙 4 枚が残っていた。
            foreach (var path in Directory.GetFiles(ImageFolder))
            {
                if (!IsImage(path)) continue;
                if (kept.Contains(Path.GetFileName(path))) continue;

                AssetDatabase.DeleteAsset(path.Replace('\\', '/'));
                Debug.Log("原稿に無いので消しました: " + Path.GetFileName(path));
            }

            return copied;
        }

        /// <summary>絵として扱う拡張子か。</summary>
        private static bool IsImage(string path)
        {
            var extension = Path.GetExtension(path).ToLowerInvariant();
            return extension == ".png" || extension == ".jpg" || extension == ".jpeg";
        }

        /// <summary>
        /// 画面に出す表紙の、いちばん長い辺の画素数。
        ///
        /// 表紙は本文幅の 55%（基準の物差しで 550 ほど）にしか出ない。
        /// 既定の 2048 のままだと、見えない細かさのぶんをアプリに積むことになる。
        /// 1024 なら、細かい画面でも足りて、焼き込む量は 4 分の 1 になる。
        ///
        /// 原稿の絵そのものを小さくしても、次に差し替えたときに元へ戻る。
        /// 取り込む側で決めておけば、原稿がどんな大きさでも結果は変わらない。
        /// </summary>
        private const int MaxTextureSize = 1024;

        /// <summary>絵をスプライトとして扱わせ、焼き込む大きさを抑える。</summary>
        private static void MakeSprite(string assetPath)
        {
            var importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
            if (importer == null) return;

            var changed = false;

            if (importer.textureType != TextureImporterType.Sprite)
            {
                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.mipmapEnabled = false;
                changed = true;
            }

            if (importer.maxTextureSize != MaxTextureSize)
            {
                importer.maxTextureSize = MaxTextureSize;
                changed = true;
            }

            // 写真のような絵なので、粗い圧縮でも見た目に響かない。
            if (!importer.crunchedCompression)
            {
                importer.crunchedCompression = true;
                importer.compressionQuality = 50;
                changed = true;
            }

            if (changed) importer.SaveAndReimport();
        }

        /// <summary>原稿の中の絵の道を、Resources から見た道へ直す。</summary>
        private static string ResourcePathOf(string source)
        {
            var name = Path.GetFileNameWithoutExtension(source.Replace('\\', '/'));
            return ImageResourceFolder + "/" + name;
        }

        /// <summary>HTML を、説明書に置ける素の文章へ直す。</summary>
        public static string ToPlainText(string html)
        {
            if (string.IsNullOrEmpty(html)) return string.Empty;

            var body = html;

            // 見えない部分を先に落とす。
            body = Drop(body, @"<head\b.*?</head>");
            body = Drop(body, @"<script\b.*?</script>");
            body = Drop(body, @"<style\b.*?</style>");
            body = Drop(body, @"<!--.*?-->");

            // ページの表題は落とす。説明書側に「■ 参考文献」の見出しがすでにある。
            body = Drop(body, @"<h1\b[^>]*class\s*=\s*""[^""]*page-title[^""]*""[^>]*>.*?</h1>");

            // 表紙を包んだリンク。原稿ではこれが主で、絵を押すと Amazon へ飛ぶ。
            // 絵の目印に行き先を添えて、ひとつの区切りにする。
            body = Regex.Replace(body,
                @"<a\b[^>]*href\s*=\s*""([^""]+)""[^>]*>(?:(?!</a>).)*?<img\b[^>]*src\s*=\s*""([^""]+)""[^>]*>.*?</a>",
                m => "\n" + ManualBlocks.ImageMarker(ResourcePathOf(m.Groups[2].Value), m.Groups[1].Value) + "\n",
                RegexOptions.Singleline | RegexOptions.IgnoreCase);

            // 文字だけのリンクは、文字のうしろに URL を添える。
            body = Regex.Replace(body, @"<a\b[^>]*href\s*=\s*""([^""]+)""[^>]*>(.*?)</a>",
                m => Tidy(m.Groups[2].Value) + "  " + m.Groups[1].Value,
                RegexOptions.Singleline | RegexOptions.IgnoreCase);

            // 包まれていない絵。押せないが、そのまま出す。
            body = Regex.Replace(body, @"<img\b[^>]*src\s*=\s*""([^""]+)""[^>]*>",
                m => "\n" + ManualBlocks.ImageMarker(ResourcePathOf(m.Groups[1].Value), null) + "\n",
                RegexOptions.IgnoreCase);

            // 見出しは記号に置き換える。説明書の章立て（■）とぶつからないものを選ぶ。
            body = Regex.Replace(body, @"<h1\b[^>]*>(.*?)</h1>", "\n\n◆ $1\n",
                RegexOptions.Singleline | RegexOptions.IgnoreCase);
            body = Regex.Replace(body, @"<h2\b[^>]*>(.*?)</h2>", "\n\n・$1\n",
                RegexOptions.Singleline | RegexOptions.IgnoreCase);
            body = Regex.Replace(body, @"<h[3-6]\b[^>]*>(.*?)</h[3-6]>", "\n\n  《$1》\n",
                RegexOptions.Singleline | RegexOptions.IgnoreCase);

            // 段落と改行。
            body = Regex.Replace(body, @"<br\s*/?>", "\n", RegexOptions.IgnoreCase);
            body = Regex.Replace(body, @"</(p|div|li|tr)\s*>", "\n", RegexOptions.IgnoreCase);
            body = Regex.Replace(body, @"<li\b[^>]*>", "  ", RegexOptions.IgnoreCase);

            // 残った札をすべて外す。
            body = Regex.Replace(body, @"<[^>]+>", string.Empty);

            body = Unescape(body);

            return Compact(body);
        }

        private static string Drop(string text, string pattern) =>
            Regex.Replace(text, pattern, string.Empty,
                RegexOptions.Singleline | RegexOptions.IgnoreCase);

        /// <summary>行のなかの空白を整える。</summary>
        private static string Tidy(string text) =>
            Regex.Replace(Regex.Replace(text, @"<[^>]+>", string.Empty), @"\s+", " ").Trim();

        private static string Unescape(string text)
        {
            var pairs = new Dictionary<string, string>
            {
                { "&nbsp;", " " }, { "&amp;", "&" }, { "&lt;", "<" }, { "&gt;", ">" },
                { "&quot;", "\"" }, { "&#39;", "'" }, { "&apos;", "'" },
                { "&mdash;", "—" }, { "&ndash;", "–" }, { "&hellip;", "…" },
            };

            foreach (var pair in pairs) text = text.Replace(pair.Key, pair.Value);

            // 数字で書かれた文字（&#12345; など）。
            return Regex.Replace(text, @"&#(\d+);", m =>
                int.TryParse(m.Groups[1].Value, out var code) && code > 0 && code < 0x10000
                    ? ((char)code).ToString()
                    : m.Value);
        }

        /// <summary>
        /// 行を詰める。
        ///
        /// HTML から起こすと、札の切れ目ごとに空行が入って隙間だらけになる。
        /// 元の空行はすべて捨てて、見出しの前にだけ 1 行空ける。
        /// 書名・著者・番号・URL がひと続きに並ぶので、本ごとの区切りが読める。
        /// </summary>
        private static string Compact(string text)
        {
            var lines = text.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');

            var result = new StringBuilder();

            foreach (var raw in lines)
            {
                var line = Regex.Replace(raw, @"[ \t　]+$", string.Empty);
                if (line.Trim().Length == 0) continue;

                // 深い字下げは 2 文字にそろえる。
                line = Regex.Replace(line, @"^[ \t]{3,}(?=\S)", "  ");

                if (result.Length > 0 && IsHeading(line)) result.Append('\n');

                result.Append(line).Append('\n');
            }

            return result.ToString().TrimEnd('\n');
        }

        /// <summary>見出しの行か。前に 1 行空ける目印にする。</summary>
        private static bool IsHeading(string line)
        {
            var head = line.TrimStart();
            return head.StartsWith("◆") || head.StartsWith("・") || head.StartsWith("《");
        }
    }
}
