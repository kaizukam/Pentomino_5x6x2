using System.Text;
using UnityEditor;
using UnityEditor.CrashReporting;
using UnityEngine;

namespace Pentomino.EditorTools
{
    /// <summary>
    /// Unity のクラウドへデバッグシンボルを送るかどうかを見る・止める。
    ///
    /// Android のデバッグシンボルを出すようにしたところ、ビルドのあとに
    /// usymtool が走り、シンボルが Unity のクラウド（Cloud Diagnostics）へ
    /// 送られていた。送られるのは利用者のデータではなく、こちらのシンボル
    /// ファイルだが、頼んでいない送信なので止められるようにしておく。
    ///
    /// Play Console へ出す symbols.zip は、この送信とは別に手元に残る。
    /// 止めても、Play へ上げるものは無くならない。
    /// </summary>
    public static class CloudDiagnosticsTool
    {
        [MenuItem("Pentomino/クラウド送信の状態を見る")]
        public static void Show()
        {
            Debug.Log(Report("クラウド送信の状態"));
        }

        [MenuItem("Pentomino/クラウドへのシンボル送信を止める")]
        public static void Disable()
        {
            var report = new StringBuilder(Report("止める前"));

            CrashReportingSettings.enabled = false;

            // エディタ側の例外送信も、頼んでいないので合わせて切る。
            CrashReportingSettings.captureEditorExceptions = false;

            AssetDatabase.SaveAssets();

            report.AppendLine();
            report.Append(Report("止めたあと"));
            report.AppendLine();
            report.AppendLine("この値は ProjectSettings/UnityConnectSettings.asset に入るので、");
            report.AppendLine("Git に乗り、Mac 側にも伝わります。");

            Debug.Log(report.ToString());
        }

        private static string Report(string title)
        {
            var report = new StringBuilder(title);
            report.AppendLine();
            report.AppendLine("  クラウド診断（クラッシュ送信） : " + CrashReportingSettings.enabled);
            report.AppendLine("  エディタの例外を送る           : " + CrashReportingSettings.captureEditorExceptions);
            report.AppendLine("  クラウドのプロジェクト ID      : "
                              + (string.IsNullOrEmpty(CloudProjectSettings.projectId)
                                  ? "（無し）"
                                  : CloudProjectSettings.projectId));
            return report.ToString();
        }
    }
}
