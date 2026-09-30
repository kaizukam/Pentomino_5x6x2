using Pentomino.View;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace Pentomino.EditorTools
{
    /// <summary>
    /// 既に作ってあるプレハブのボタンに、Unity 組み込みの角丸スプライトを当てる。
    ///
    /// プレハブを作り直すと手を加えたレイアウトが消えてしまうので、
    /// 中身はそのままに、ボタンの見た目だけを差し替える。
    /// </summary>
    public static class ButtonSkinTool
    {
        [MenuItem("Pentomino/ボタンに組み込みスプライトを適用")]
        public static void Apply()
        {
            UiFactory.DefaultButtonSprite =
                AssetDatabase.GetBuiltinExtraResource<Sprite>(UiPrefabBuilder.ButtonSpritePath);

            var total = 0;
            total += ApplyToPrefab(UiPrefabBuilder.HeaderPath);
            total += ApplyToPrefab(UiPrefabBuilder.SettingsPath);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            if (total == 0)
            {
                Debug.LogWarning("対象のボタンが見つかりませんでした。"
                                 + "先に メニュー Pentomino ▸ UI プレハブを生成 を実行してください。");
                return;
            }

            Debug.Log("ボタン " + total + " 個に組み込みスプライト ("
                      + UiPrefabBuilder.ButtonSpritePath + ") を適用しました。");
        }

        private static int ApplyToPrefab(string path)
        {
            var root = PrefabUtility.LoadPrefabContents(path);
            if (root == null)
            {
                Debug.LogWarning("プレハブを開けません: " + path);
                return 0;
            }

            var changed = 0;
            try
            {
                foreach (var button in root.GetComponentsInChildren<Button>(true))
                {
                    var image = button.targetGraphic as Image;
                    if (image == null) image = button.GetComponent<Image>();
                    if (image == null) continue;

                    UiFactory.ApplyButtonSprite(image);
                    changed++;
                }

                if (changed > 0) PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }

            return changed;
        }
    }
}
