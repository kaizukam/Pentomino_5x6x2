#if UNITY_IOS
using System.IO;
using UnityEditor;
using UnityEditor.Callbacks;
using UnityEditor.iOS.Xcode;
using UnityEngine;

namespace Pentomino.EditorTools
{
    /// <summary>
    /// iOS を書き出したあと、Info.plist に足りない項目を入れる。
    ///
    /// Unity は書き出すたびに Xcode プロジェクトを作り直す。だから Xcode 側で
    /// Info.plist を手直ししても、次のビルドで消える。これまで Xcode で
    /// 毎回入力し直していたのは、そのため。直すならこちら側に書くしかない。
    ///
    /// ここに書いておけば、Windows でも Mac でも、書き出すたびに自動で入る。
    /// 将来 Info.plist に何か足す必要が出たら（プライバシーの説明文など）、
    /// この一箇所に書けば済む。
    /// </summary>
    public static class IosBuildPostProcess
    {
        /// <summary>
        /// 暗号化を使っていないことの申告。
        ///
        /// これが無いと、アップロードのたびに App Store Connect が
        /// 「輸出コンプライアンス」を尋ねてきて、そのつど答えるまで
        /// ビルドが TestFlight で使えない。
        ///
        /// このアプリは通信もせず、暗号化も使っていないので false でよい。
        /// 暗号化を使うようになったら、ここを見直すこと。
        /// </summary>
        private const string EncryptionKey = "ITSAppUsesNonExemptEncryption";

        [PostProcessBuild]
        public static void OnPostProcessBuild(BuildTarget target, string path)
        {
            if (target != BuildTarget.iOS) return;

            var plistPath = Path.Combine(path, "Info.plist");
            if (!File.Exists(plistPath))
            {
                Debug.LogWarning("Info.plist が見つかりません: " + plistPath);
                return;
            }

            var plist = new PlistDocument();
            plist.ReadFromFile(plistPath);

            plist.root.SetBoolean(EncryptionKey, false);
            plist.WriteToFile(plistPath);

            Debug.Log("Info.plist に " + EncryptionKey + " = false を入れました。\n"
                + "アップロードのたびに輸出コンプライアンスを訊かれることは、もうありません。");
        }
    }
}
#endif
