using UnityEditor;
using UnityEngine;

namespace Pentomino.EditorTools
{
    /// <summary>
    /// おめでとうパネルの額縁を、9 スライスのスプライトとして取り込む。
    ///
    /// 額縁は四隅の彫りを保ったまま伸ばしたいので、四辺に Border を設定する。
    /// これをしないと引き伸ばしたときに彫りが歪む。
    /// </summary>
    public static class FrameImporter
    {
        public const string FramePath = "Assets/Resources/UI/Frame.png";

        /// <summary>額縁の内側が始まるまでの幅（元画像のピクセル）。</summary>
        private const int BorderLeft = 90;
        private const int BorderRight = 90;
        private const int BorderTop = 80;
        private const int BorderBottom = 80;

        [MenuItem("Pentomino/額縁を9スライスで取り込む")]
        public static void Apply()
        {
            var importer = AssetImporter.GetAtPath(FramePath) as TextureImporter;
            if (importer == null)
            {
                Debug.LogError("額縁の画像が見つかりません: " + FramePath);
                return;
            }

            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.mipmapEnabled = false;
            importer.alphaIsTransparency = true;
            importer.maxTextureSize = 1024;

            // 四辺の Border。伸びるのは中央だけになる。
            var settings = new TextureImporterSettings();
            importer.ReadTextureSettings(settings);
            settings.spriteBorder = new Vector4(BorderLeft, BorderBottom, BorderRight, BorderTop);
            settings.spriteMeshType = SpriteMeshType.FullRect;   // 9 スライスには FullRect が要る
            importer.SetTextureSettings(settings);

            importer.SaveAndReimport();

            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(FramePath);
            if (sprite == null)
            {
                Debug.LogError("額縁をスプライトとして読み込めません: " + FramePath);
                return;
            }

            Debug.Log("額縁を 9 スライスで取り込みました: " + FramePath
                      + "\n大きさ " + sprite.rect.width + "x" + sprite.rect.height
                      + " / Border " + sprite.border);
        }
    }
}
