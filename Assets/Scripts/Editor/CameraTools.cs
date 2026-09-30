using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Pentomino.EditorTools
{
    /// <summary>カメラの背景まわりを整えるエディタ用ツール。</summary>
    public static class CameraTools
    {
        /// <summary>背景に使う単色。</summary>
        private static readonly Color Background = Color.white;

        [MenuItem("Pentomino/開いているシーンの背景を単色にする")]
        public static void MakeBackgroundSolid()
        {
            var changed = 0;
            foreach (var camera in Object.FindObjectsByType<Camera>(FindObjectsSortMode.None))
            {
                if (camera.clearFlags == CameraClearFlags.SolidColor && camera.backgroundColor == Background) continue;

                Undo.RecordObject(camera, "背景を単色にする");
                camera.clearFlags = CameraClearFlags.SolidColor;   // URP の Background Type = Solid Color
                camera.backgroundColor = Background;
                EditorUtility.SetDirty(camera);
                changed++;
            }

            if (changed == 0)
            {
                Debug.Log("すでに単色の背景になっています。");
                return;
            }

            EditorSceneManager.MarkAllScenesDirty();
            Debug.Log("カメラ " + changed + " 台の背景を単色にしました。シーンを保存してください（Ctrl+S）。");
        }
    }
}
