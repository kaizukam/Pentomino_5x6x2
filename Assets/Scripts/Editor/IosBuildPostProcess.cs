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

            DisableUserScriptSandboxing(path);
        }

        /// <summary>
        /// Xcode のユーザースクリプトのサンドボックスを切る。
        ///
        /// Xcode 15 からビルド中のスクリプトはサンドボックスに入れられる。
        /// Unity の Xcode プロジェクトは、GameAssembly の Run Script で IL2CPP を走らせ、
        /// Unity プロジェクト本体や DerivedData を読み書きするので、
        /// 「Sandbox: ... deny(1) file-read-data / file-write-create」で止まる（2026-10-01、5x6x2）。
        ///
        /// Xcode で直しても次の書き出しで消えるので、ここで全ターゲットに入れる。
        /// </summary>
        private const string SandboxingKey = "ENABLE_USER_SCRIPT_SANDBOXING";

        private static void DisableUserScriptSandboxing(string path)
        {
            var projectPath = PBXProject.GetPBXProjectPath(path);
            if (!File.Exists(projectPath))
            {
                Debug.LogWarning("Xcode プロジェクトが見つかりません: " + projectPath);
                return;
            }

            var project = new PBXProject();
            project.ReadFromFile(projectPath);

            var targets = new[]
            {
                project.ProjectGuid(),
                project.GetUnityMainTargetGuid(),
                project.GetUnityFrameworkTargetGuid(),
                project.TargetGuidByName("GameAssembly"),
            };

            foreach (var guid in targets)
            {
                if (string.IsNullOrEmpty(guid)) continue;
                project.SetBuildProperty(guid, SandboxingKey, "NO");
            }

            project.WriteToFile(projectPath);
            Debug.Log("Xcode の " + SandboxingKey + " を NO にしました（IL2CPP の Run Script が止められないように）。");
        }
    }
}
#endif
