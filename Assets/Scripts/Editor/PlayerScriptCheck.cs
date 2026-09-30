using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build.Player;
using UnityEngine;

namespace Pentomino.EditorTools
{
    /// <summary>
    /// 実機向けにだけ書いたコードを、組んでみて確かめる。
    ///
    /// Haptics のように <c>#if UNITY_ANDROID &amp;&amp; !UNITY_EDITOR</c> で
    /// 囲んだ場所は、エディタでは一行もコンパイルされない。試験を全部通しても、
    /// 打ち間違いが残っているかどうかは分からない。実機ビルドまで行って
    /// 初めて分かるのでは、確かめるのに三十分かかる。
    ///
    /// ここでは絵も音も作らず、コードだけを実機向けに組んでみる。
    /// 一分ほどで済み、打ち間違いはその場で出る。
    ///
    /// iOS 側は Mac でしか組めないので、ここでは Android だけを見る。
    /// 同じ場所を通るので、囲みの中の書き方の誤りはこれで拾える。
    /// </summary>
    public static class PlayerScriptCheck
    {
        /// <summary>組んだものを置く場所。中身は使わないので消してよい。</summary>
        private const string OutputFolder = "Temp/PlayerScriptCheck";

        [MenuItem("Pentomino/実機向けにコードを組んでみる")]
        public static void Run()
        {
            if (!Check(BuildTarget.Android, BuildTargetGroup.Android))
                throw new Exception("実機向けのコードが組めません。");
        }

        private static bool Check(BuildTarget target, BuildTargetGroup group)
        {
            Directory.CreateDirectory(OutputFolder);

            var settings = new ScriptCompilationSettings
            {
                target = target,
                group = group,
                options = ScriptCompilationOptions.None,
            };

            var result = PlayerBuildInterface.CompilePlayerScripts(settings, OutputFolder);

            // 組めなかったときは、詳しい理由がコンソールに出ている。
            // ここでは通ったか通らなかったかだけを返す。
            var built = result.assemblies != null && result.assemblies.Count > 0;

            Debug.Log(built
                ? target + " 向けにコードを組めました（" + result.assemblies.Count + " 個）。"
                : target + " 向けにコードを組めませんでした。上の error を見てください。");

            return built;
        }
    }
}
