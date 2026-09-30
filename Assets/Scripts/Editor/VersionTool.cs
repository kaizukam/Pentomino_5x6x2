using Pentomino.Core;
using UnityEditor;
using UnityEngine;

namespace Pentomino.EditorTools
{
    /// <summary>
    /// 版番号を上げる。三か所を一度に、そろえて動かす。
    ///
    /// Unity は版番号を三つに分けて持っている。
    ///
    ///   bundleVersion            利用者に見える版。iOS と Android で共通
    ///   AndroidBundleVersionCode Google Play の受付番号
    ///   buildNumber (iPhone)     App Store Connect の受付番号
    ///
    /// 後ろ二つは、同じ番号を二度受け付けてもらえない。上げ忘れると
    /// アップロードの最後で弾かれ、ビルドからやり直しになる。
    /// 手で三か所を打つと必ずどれかを忘れるので、まとめて動かす。
    ///
    /// 受付番号は戻せないので、この道具も上げることしかしない。
    /// </summary>
    public static class VersionTool
    {
        [MenuItem("Pentomino/版番号を上げる/直しただけ (0.4.1 → 0.4.2)")]
        public static void BumpFix() => Bump(VersionPart.Fix);

        [MenuItem("Pentomino/版番号を上げる/何かを足した (0.4.1 → 0.5.0)")]
        public static void BumpFeature() => Bump(VersionPart.Feature);

        [MenuItem("Pentomino/版番号を上げる/作り直した (0.4.1 → 1.0.0)")]
        public static void BumpMajor() => Bump(VersionPart.Major);

        [MenuItem("Pentomino/版番号を上げる/受付番号だけ上げる")]
        public static void BumpBuildOnly() => Apply(PlayerSettings.bundleVersion);

        [MenuItem("Pentomino/版番号を上げる/いまの番号を見る", priority = 100)]
        public static void Show()
        {
            Debug.Log("いまの版番号\n"
                + "  利用者に見える版   " + PlayerSettings.bundleVersion + "\n"
                + "  Android の受付番号 " + PlayerSettings.Android.bundleVersionCode + "\n"
                + "  iOS の受付番号     " + PlayerSettings.iOS.buildNumber);
        }

        private static void Bump(VersionPart part)
        {
            var current = PlayerSettings.bundleVersion;
            if (!VersionRule.TryBump(current, part, out var next))
            {
                Debug.LogError("版番号を読めません: \"" + current + "\"\n"
                    + "「大.中.小」の三段（例 0.4.1）で書いてください。\n"
                    + "受付番号だけ上げたいときは「受付番号だけ上げる」を使ってください。");
                return;
            }

            Apply(next, current);
        }

        /// <summary>
        /// 三か所をそろえて書き込む。
        ///
        /// 受付番号の決まりは、二つのストアで違う。
        ///
        ///   iOS     同じ版の中でだけ重複できない。版が変われば 1 から数え直せる
        ///   Android アプリ全体で増え続ける必要がある。戻すと受け付けてもらえない
        ///
        /// なので、版を上げたときは iOS だけ 1 に戻し、Android は増やし続ける。
        /// </summary>
        private static void Apply(string version, string from = null)
        {
            var versionChanged = from != null && from != version;

            var android = VersionRule.NextBuildNumber(PlayerSettings.Android.bundleVersionCode);
            var ios = versionChanged
                ? "1"
                : VersionRule.NextBuildNumber(PlayerSettings.iOS.buildNumber);

            var wasAndroid = PlayerSettings.Android.bundleVersionCode;
            var wasIos = PlayerSettings.iOS.buildNumber;

            PlayerSettings.bundleVersion = version;
            PlayerSettings.Android.bundleVersionCode = android;
            PlayerSettings.iOS.buildNumber = ios;

            Save();

            var head = from == null || from == version
                ? "受付番号を上げました（版は " + version + " のまま）"
                : "版番号を上げました " + from + " → " + version;

            var note = versionChanged
                ? "（iOS は版が変わったので 1 から数え直し。Android は戻せないので増やし続けます）\n"
                : string.Empty;

            Debug.Log(head + "\n"
                + "  Android の受付番号 " + wasAndroid + " → " + android + "\n"
                + "  iOS の受付番号     " + wasIos + " → " + ios + "\n"
                + note + "\n"
                + "ProjectSettings.asset に書き出しました。コミットを忘れずに。");
        }

        /// <summary>
        /// 版番号をディスクへ書き出す。
        ///
        /// AssetDatabase.SaveAssets() だけでは足りない。あれが保存するのは Asset で、
        /// Player Settings はネイティブ側の設定オブジェクトにある。メモリの値だけが
        /// 変わり、ProjectSettings.asset は古いまま残る。そのあと Editor が別の機会に
        /// 古い内容を書き戻すと、上げたはずの番号が元に戻ったように見える。
        /// 実際にそれで一度、番号を上げそこねた。
        ///
        /// File ▸ Save Project がプロジェクトの設定を書き出す道なので、ここから呼ぶ。
        /// </summary>
        private static void Save()
        {
            AssetDatabase.SaveAssets();

            if (EditorApplication.ExecuteMenuItem("File/Save Project")) return;

            Debug.LogWarning("File ▸ Save Project を呼べませんでした。"
                             + "\n手で File ▸ Save Project を実行してください。"
                             + "\nそうしないと、上げた番号がディスクに残りません。");
        }
    }
}
