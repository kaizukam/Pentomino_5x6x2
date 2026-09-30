using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace Pentomino.EditorTools
{
    /// <summary>
    /// 設定画面の難易度ボタンの面と、級の名前の収まりを原稿から取り込む。
    ///
    ///   Intro2.png   設定画面の難易度ボタンの面。角が丸めてある
    ///
    /// 帯（ヘッダ）の級の見せ方は HeaderLayoutMigration へ移った。
    /// 2026-09-13 の案から、帯は言語ごとの「級の帯の絵」と画面の地の色で級を見せる。
    /// ここに残るのは設定画面のボタンの面と、成績パネルの文字の収まりだけ。
    ///
    /// 原稿は DataBase/Grade にある 1200x300 の三枚。Git には入らない場所なので、
    /// ここで Assets へ写す。
    ///
    /// 級の名前は言語ごとに長さが大きく変わる。日本語の「中級：黄帯」は 6 文字だが、
    /// フランス語の「Intermédiaire : Ceinture jaune」は 29 文字ある。枠に収まる
    /// ところまで文字を縮める設定を、成績に入れておく。
    ///
    /// 二度実行しても構わない。
    /// </summary>
    public static class GradePatternMigration
    {
        /// <summary>級の名前は成績のパネルに出る。こちらが収まるようにする。</summary>
        private const string ResultPath = "Assets/Resources/UI/ResultPanel.prefab";

        /// <summary>級の名前を縮める下限。元の大きさに対する割合。</summary>
        private const float SmallestRatio = 0.55f;

        /// <summary>絵の原稿。Git には入らない DataBase 側にある。</summary>
        private const string SourceFolder = "DataBase/Grade";

        /// <summary>写し先。プレハブから参照するので Assets の中に置く。</summary>
        public const string ArtFolder = "Assets/Resources/UI/Grade";

        /// <summary>焼き込む大きさ。画面には 1000 ほどに出る。</summary>
        private const int MaxTextureSize = 2048;

        /// <summary>級の名前。ファイル名でもある。</summary>
        public static readonly string[] Grades = { "Intro", "Practice", "Classic" };

        /// <summary>設定画面の難易度ボタンに使う面。</summary>
        public static Sprite Face(string grade) =>
            AssetDatabase.LoadAssetAtPath<Sprite>(ArtFolder + "/" + grade + "2.png");

        [MenuItem("Pentomino/難易度のボタンの面を取り込む")]
        public static void Run()
        {
            var sprites = CopyArt();
            if (sprites == null) return;

            var report = new StringBuilder("難易度のボタンの面を取り込みました。\n\n");
            foreach (var name in Grades)
                report.AppendLine("  " + name + " → " + name + "2.png");
            report.AppendLine();

            Debug.Log(report + FitResultPanel());
        }

        /// <summary>成績パネルの級の名前も、枠に収まるようにする。</summary>
        private static string FitResultPanel()
        {
            var root = PrefabUtility.LoadPrefabContents(ResultPath);
            try
            {
                var parent = (RectTransform)root.transform;
                var report = new StringBuilder();

                foreach (var name in new[] { "Grade", "Congratulations" })
                {
                    var rect = FindDeep(parent, name);
                    var text = rect != null ? rect.GetComponent<Text>() : null;
                    if (text == null) continue;

                    Fit(text);
                    report.AppendLine("  成績の " + name + " は " + text.resizeTextMinSize
                                      + "〜" + text.resizeTextMaxSize + " で収める");
                }

                PrefabUtility.SaveAsPrefabAsset(root, ResultPath);
                AssetDatabase.SaveAssets();
                return report.ToString();
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        /// <summary>
        /// 枠に収まるところまで文字を縮める。収まっている間は元の大きさのまま。
        ///
        /// 級の名前は言語で長さが大きく変わる。あふれた側だけを縮めたいので、
        /// 大きさを決め打ちにせず、いまの大きさを上限にする。
        /// </summary>
        private static void Fit(Text text)
        {
            if (text == null) return;

            var largest = text.fontSize;

            text.resizeTextForBestFit = true;
            text.resizeTextMaxSize = largest;
            text.resizeTextMinSize = Mathf.Max(8, Mathf.RoundToInt(largest * SmallestRatio));
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Truncate;
        }

        /// <summary>原稿を Assets へ写し、スプライトとして読み込ませる。</summary>
        private static Dictionary<string, Sprite> CopyArt()
        {
            Directory.CreateDirectory(ArtFolder);

            var wanted = new List<string>();
            foreach (var grade in Grades) wanted.Add(grade + "2");   // ボタンの面

            var sprites = new Dictionary<string, Sprite>();

            foreach (var name in wanted)
            {
                var source = FindSource(name);
                if (source == null)
                {
                    Debug.LogError(SourceFolder + " に " + name + ".png がありません。");
                    return null;
                }

                var destination = ArtFolder + "/" + name + ".png";
                File.Copy(source, destination, true);
                AssetDatabase.ImportAsset(destination);

                var importer = AssetImporter.GetAtPath(destination) as TextureImporter;
                if (importer != null)
                {
                    importer.textureType = TextureImporterType.Sprite;
                    importer.spriteImportMode = SpriteImportMode.Single;
                    importer.mipmapEnabled = false;
                    importer.maxTextureSize = MaxTextureSize;
                    importer.alphaIsTransparency = true;
                    importer.SaveAndReimport();
                }

                var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(destination);
                if (sprite == null)
                {
                    Debug.LogError(destination + " をスプライトとして読めません。");
                    return null;
                }

                sprites[name] = sprite;
            }

            return sprites;
        }

        /// <summary>大文字小文字の違いを吸収して原稿を探す（.PNG と .png）。</summary>
        private static string FindSource(string name)
        {
            var folder = Path.Combine(Directory.GetCurrentDirectory(), SourceFolder);
            if (!Directory.Exists(folder)) return null;

            foreach (var path in Directory.GetFiles(folder))
            {
                if (Path.GetExtension(path).ToLowerInvariant() != ".png") continue;
                if (Path.GetFileNameWithoutExtension(path).ToLowerInvariant() != name.ToLowerInvariant())
                    continue;

                return path;
            }
            return null;
        }

        private static RectTransform FindDeep(RectTransform parent, string name)
        {
            foreach (var child in parent.GetComponentsInChildren<RectTransform>(true))
                if (child.name == name) return child;

            return null;
        }
    }
}
