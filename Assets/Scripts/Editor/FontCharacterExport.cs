using System.IO;
using Pentomino.Data;
using UnityEditor;
using UnityEngine;

namespace Pentomino.EditorTools
{
    /// <summary>
    /// 書体を削るために、画面に出しうる文字を書き出す。
    ///
    /// 書き出した先を python Tools/subset_fonts.py が読んで、書体をその文字だけに削る。
    /// 数えるのは TextInventory ひとつだけ。見張り役のテストも同じ場所を見るので、
    /// 削る側と見張る側が食い違わない。
    ///
    /// 説明書や画面の文言を書き換えたら、
    ///   1. この項目を実行して文字を書き出し
    ///   2. python Tools/subset_fonts.py で削り直し
    ///   3. Assets/Fonts と covered.txt をコミット
    /// 忘れても FontCoverageTests が赤くなるので、実機に出る前に気づける。
    /// </summary>
    public static class FontCharacterExport
    {
        /// <summary>書き出し先。Python の道具が読む。</summary>
        public const string OutputPath = "Tools/needed_characters.txt";

        [MenuItem("Pentomino/フォントに要る文字を書き出す")]
        public static void Export()
        {
            var text = TextInventory.AsText();

            Directory.CreateDirectory(Path.GetDirectoryName(OutputPath));
            File.WriteAllText(OutputPath, text, new System.Text.UTF8Encoding(false));

            Debug.Log("画面に出しうる文字を書き出しました: " + OutputPath + "\n"
                + "  " + text.Length + " 種\n\n"
                + "続けて、プロジェクトの根で\n"
                + "  python Tools/subset_fonts.py\n"
                + "を実行すると、書体がこの文字だけに削られます。");
        }
    }
}
