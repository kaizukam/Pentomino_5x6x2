using Pentomino.View;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace Pentomino.EditorTools
{
    /// <summary>パズル画面のシーンを組み立てる。画面の中身は GameScreen が実行時に作る。</summary>
    public static class GameSceneBuilder
    {
        private const string ScenePath = "Assets/Scenes/Game.unity";

        [MenuItem("Pentomino/パズル画面シーンを作成")]
        public static void Create()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);

            // 既定のスカイボックス（地平線の模様）が見えないよう、カメラの背景を単色にする。
            foreach (var camera in Object.FindObjectsByType<Camera>(FindObjectsSortMode.None))
            {
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = Color.white;
            }

            var canvasGo = new GameObject("Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            var canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;

            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080f, 1920f);
            // 幅と高さの両方が収まるよう、小さい方の倍率を採る。
            // 幅だけを基準にすると、iPad のような横長の画面で下がはみ出す。
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;
            scaler.matchWidthOrHeight = 0f;

            var screen = new GameObject("GameScreen", typeof(RectTransform), typeof(GameScreen));
            screen.transform.SetParent(canvasGo.transform, false);

            // 入力バックエンドが Input System なので、対応する UI モジュールを載せる。
            new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));

            System.IO.Directory.CreateDirectory("Assets/Scenes");
            EditorSceneManager.SaveScene(scene, ScenePath);
            AssetDatabase.Refresh();
            RegisterInBuildSettings();

            Debug.Log("パズル画面シーンを作成しました: " + ScenePath
                      + "\nこのシーンを開いた状態で再生してください（SampleScene では何も出ません）。");
            EditorUtility.FocusProjectWindow();
            Selection.activeObject = AssetDatabase.LoadAssetAtPath<Object>(ScenePath);
        }

        /// <summary>
        /// Build Settings の先頭に登録する。
        /// 登録しておかないとビルド時に SampleScene が起動してしまう。
        /// </summary>
        private static void RegisterInBuildSettings()
        {
            var scenes = new System.Collections.Generic.List<EditorBuildSettingsScene>();
            scenes.Add(new EditorBuildSettingsScene(ScenePath, true));

            foreach (var existing in EditorBuildSettings.scenes)
            {
                if (existing.path == ScenePath) continue;
                scenes.Add(existing);
            }

            EditorBuildSettings.scenes = scenes.ToArray();
        }
    }
}
