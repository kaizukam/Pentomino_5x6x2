using UnityEditor;
using UnityEngine;

namespace Pentomino.EditorTools
{
    /// <summary>
    /// 画面の向きを設定する。
    ///
    /// 開発仕様「画面対応」では全方向に対応する。
    /// 横向きで盤が潰れないよう、表示に使う幅は 画面の高さ÷2.14 までに抑え、
    /// 横中央に置く。その処理は GameScreen 側にある。
    ///
    /// 縦に固定したくなったときのために、固定するほうの入り口も残してある。
    ///
    /// この設定は ProjectSettings.asset に入るプラットフォーム共通の項目なので、
    /// iOS のビルドサポートが入っていない Windows 上でも設定でき、Git で Mac にも引き継がれる。
    /// </summary>
    public static class OrientationTool
    {
        [MenuItem("Pentomino/画面を縦向きに固定する")]
        public static void LockPortrait()
        {
            PlayerSettings.defaultInterfaceOrientation = UIOrientation.Portrait;
            PlayerSettings.allowedAutorotateToPortrait = true;
            PlayerSettings.allowedAutorotateToPortraitUpsideDown = false;
            PlayerSettings.allowedAutorotateToLandscapeLeft = false;
            PlayerSettings.allowedAutorotateToLandscapeRight = false;

            AssetDatabase.SaveAssets();
            Debug.Log("画面を縦向きに固定しました。\n"
                      + "現在の設定: " + PlayerSettings.defaultInterfaceOrientation
                      + "\n横向きも使いたくなったら「画面の向きを自動にする」で戻せます。");
        }

        [MenuItem("Pentomino/画面の向きを自動にする")]
        public static void AllowAutoRotation()
        {
            PlayerSettings.defaultInterfaceOrientation = UIOrientation.AutoRotation;
            PlayerSettings.allowedAutorotateToPortrait = true;
            PlayerSettings.allowedAutorotateToPortraitUpsideDown = true;
            PlayerSettings.allowedAutorotateToLandscapeLeft = true;
            PlayerSettings.allowedAutorotateToLandscapeRight = true;

            AssetDatabase.SaveAssets();
            Debug.Log("画面の向きを全方向に対応させました。\n"
                      + "横向きでは表示幅が 画面の高さ÷2.14 までに抑えられ、横中央に置かれます。\n"
                      + "向きが変わると画面は自動で組み直されます。");
        }
    }
}
