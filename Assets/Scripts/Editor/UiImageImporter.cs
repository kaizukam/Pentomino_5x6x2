using UnityEditor;
using UnityEngine;

namespace Pentomino.EditorTools
{
    /// <summary>
    /// 画面で使う絵を、スプライトとして取り込む。
    ///
    /// PNG を置いただけでは Texture として取り込まれ、Image に割り当てられない。
    /// 絵を差し替えたり足したりしたら、これを走らせる。
    ///
    /// 9 スライスは使わない。額縁も模様も、縦横比を保ったまま出すか、
    /// はみ出す分を切り落とす作りにしてあるので、四辺を固定する必要がない。
    /// </summary>
    public static class UiImageImporter
    {
        public const string Folder = "Assets/Resources/UI";

        /// <summary>スプライトとして取り込む絵。</summary>
        private static readonly string[] Images =
        {
            "PictureFrame.png",     // おめでとうパネルの額縁
        };

        /// <summary>
        /// 取り込む最大の大きさ。元の絵はこれより大きいが、
        /// 画面に出るのは 1000 ピクセル程度なので、ここまで落として構わない。
        /// 大きいまま持つと、そのぶん実行時のメモリを食う。
        /// </summary>
        private const int MaxSize = 2048;

        [MenuItem("Pentomino/画面の絵を取り込む")]
        public static void Apply()
        {
            var report = new System.Text.StringBuilder();
            report.AppendLine("画面の絵の取り込み");
            report.AppendLine();

            var problems = 0;

            foreach (var name in Images)
            {
                var path = Folder + "/" + name;

                var importer = AssetImporter.GetAtPath(path) as TextureImporter;
                if (importer == null)
                {
                    report.AppendLine("  × " + path + " が見つかりません");
                    problems++;
                    continue;
                }

                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.mipmapEnabled = false;
                importer.alphaIsTransparency = true;
                importer.maxTextureSize = MaxSize;

                var settings = new TextureImporterSettings();
                importer.ReadTextureSettings(settings);
                settings.spriteBorder = Vector4.zero;
                settings.spriteMeshType = SpriteMeshType.FullRect;
                importer.SetTextureSettings(settings);

                importer.SaveAndReimport();

                var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
                if (sprite == null)
                {
                    report.AppendLine("  × " + path + " をスプライトとして読めません");
                    problems++;
                    continue;
                }

                report.AppendLine(string.Format("  ○ {0}  {1}x{2}  縦横比 {3:0.000}",
                    name, sprite.rect.width, sprite.rect.height,
                    sprite.rect.width / sprite.rect.height));
            }

            AssetDatabase.SaveAssets();

            if (problems > 0)
            {
                Debug.LogWarning(report.ToString());
                return;
            }

            Debug.Log(report.ToString());
        }
    }
}
