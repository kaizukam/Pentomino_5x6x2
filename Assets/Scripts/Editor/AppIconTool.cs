using UnityEditor;
using UnityEditor.Build;
using UnityEngine;

namespace Pentomino.EditorTools
{
    /// <summary>
    /// アプリアイコンを Player Settings に設定する。
    ///
    /// App Store へのアップロードは 1024x1024 のアイコンが無いと弾かれる
    /// （エラー 91111: Missing app icon）。透過を含んでいても弾かれるので、
    /// 取り込み設定でアルファチャンネルを落としてから割り当てる。
    ///
    /// なお、ビルドサポートが入っていないプラットフォームはアイコンの枠自体が
    /// 存在せず、個別設定ができない。そのため、まずプラットフォームを問わない
    /// Default Icon を設定しておき、各環境でのビルド時にそこから生成させる。
    /// </summary>
    public static class AppIconTool
    {
        /// <summary>
        /// いま使っている絵。差し替えるときは、ここも一緒に直す。
        ///
        /// 絵を見比べるために番号を付けて増やしていくので、名前は固定できない。
        /// 見つからないときは Assets/Textures にある物を並べて知らせる。
        /// </summary>
        public const string IconPath = "Assets/Textures/Apple-562.PNG";

        private const string IconFolder = "Assets/Textures";

        [MenuItem("Pentomino/アプリアイコンを設定")]
        public static void Apply()
        {
            if (!PrepareTexture(out var icon)) return;

            // Default Icon。iOS モジュールが未導入の環境でもこれは設定できる。
            PlayerSettings.SetIconsForTargetGroup(BuildTargetGroup.Unknown, new[] { icon });

            var targets = new[] { NamedBuildTarget.iOS, NamedBuildTarget.Android, NamedBuildTarget.Standalone };
            var applied = 0;
            var skipped = "";

            foreach (var target in targets)
            {
                var count = ApplyTo(target, icon);
                applied += count;
                if (count == 0) skipped += " " + target.TargetName;
            }

            AssetDatabase.SaveAssets();

            var message = "アプリアイコンを設定しました: " + IconPath
                          + "\nDefault Icon: 設定済み"
                          + "\nプラットフォーム個別の枠: " + applied + " 個";

            if (skipped.Length > 0)
            {
                message += "\n\n枠が取得できなかったプラットフォーム:" + skipped
                           + "\nそのビルドサポートが未導入だと枠が現れません。"
                           + "\nDefault Icon は設定済みなので、導入済みの環境でビルドすれば反映されます。";
            }

            Debug.Log(message);
        }

        /// <summary>
        /// 取り込み設定を整える。Apple はアルファチャンネルを含むアイコンを受け付けない。
        /// </summary>
        private static bool PrepareTexture(out Texture2D icon)
        {
            icon = null;

            var importer = AssetImporter.GetAtPath(IconPath) as TextureImporter;
            if (importer == null)
            {
                Debug.LogError("アイコン画像が見つかりません: " + IconPath
                               + "\n" + IconFolder + " にあるもの: " + Candidates()
                               + "\nAppIconTool.IconPath を、いま使う絵に合わせてください。");
                return false;
            }

            importer.textureType = TextureImporterType.Default;
            importer.alphaSource = TextureImporterAlphaSource.None;   // 透過を持たせない
            importer.alphaIsTransparency = false;
            importer.mipmapEnabled = false;
            importer.npotScale = TextureImporterNPOTScale.None;
            importer.maxTextureSize = 2048;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.sRGBTexture = true;
            importer.SaveAndReimport();

            icon = AssetDatabase.LoadAssetAtPath<Texture2D>(IconPath);
            if (icon == null)
            {
                Debug.LogError("アイコン画像を読み込めません: " + IconPath);
                return false;
            }

            if (icon.width != 1024 || icon.height != 1024)
            {
                Debug.LogWarning("App Store 用は 1024x1024 が必要です。いまは "
                                 + icon.width + "x" + icon.height + " です。");
            }

            return true;
        }

        /// <summary>置き場所にある絵の名前。取り違えにすぐ気づけるように並べる。</summary>
        private static string Candidates()
        {
            if (!System.IO.Directory.Exists(IconFolder)) return "（置き場所がありません）";

            var names = new System.Collections.Generic.List<string>();
            foreach (var path in System.IO.Directory.GetFiles(IconFolder))
            {
                var extension = System.IO.Path.GetExtension(path).ToLowerInvariant();
                if (extension == ".png" || extension == ".jpg")
                    names.Add(System.IO.Path.GetFileName(path));
            }

            return names.Count == 0 ? "（ありません）" : string.Join(" ", names);
        }

        private static int ApplyTo(NamedBuildTarget target, Texture2D icon)
        {
            var applied = 0;

            foreach (var kind in PlayerSettings.GetSupportedIconKinds(target))
            {
                var icons = PlayerSettings.GetPlatformIcons(target, kind);
                if (icons == null || icons.Length == 0) continue;

                foreach (var slot in icons)
                {
                    // 1 枠に複数枚（レイヤー）を求める種類もあるので、全部に同じ絵を入れる。
                    var layers = new Texture2D[Mathf.Max(1, slot.maxLayerCount)];
                    for (var i = 0; i < layers.Length; i++) layers[i] = icon;
                    slot.SetTextures(layers);
                    applied++;
                }

                PlayerSettings.SetPlatformIcons(target, kind, icons);
            }

            return applied;
        }
    }
}
