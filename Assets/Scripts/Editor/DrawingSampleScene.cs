using System.Collections.Generic;
using Pentomino.Core;
using Pentomino.Data;
using Pentomino.View;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace Pentomino.EditorTools
{
    /// <summary>
    /// 描画の見た目を確認するためのサンプルシーンを作る。
    /// BOX の格子図と、12 ピースを姿勢 #00 で並べたものを表示する。
    /// 色や線幅の調整は PolyominoGraphic の Style から行える。
    /// </summary>
    public static class DrawingSampleScene
    {
        private const string ScenePath = "Assets/Scenes/DrawingSample.unity";

        [MenuItem("Pentomino/描画サンプルシーンを作成")]
        public static void Create()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);

            var canvas = CreateCanvas();
            var style = new PentominoStyle { cellSize = 44f };

            var board = PolyominoView.Create(canvas.transform, "Board");
            board.RectTransform.anchoredPosition = new Vector2(24f, -24f);
            board.RenderBoard(style);

            var pieceStyle = new PentominoStyle { cellSize = 36f };
            var x = 24f;
            var y = -24f - Board.Rows * style.cellSize - 48f;
            var rowHeight = 0f;

            foreach (var piece in Pieces.Alphabetical)
            {
                var posture = GameData.Postures.DefaultPosture(piece);
                var view = PolyominoView.Create(canvas.transform, "Piece_" + posture.Key);
                view.RenderPiece(posture, pieceStyle);

                var size = view.Graphic.PreferredSize;
                if (x + size.x > 1080f - 24f)
                {
                    x = 24f;
                    y -= rowHeight + 24f;
                    rowHeight = 0f;
                }

                view.RectTransform.anchoredPosition = new Vector2(x, y);
                x += size.x + 24f;
                if (size.y > rowHeight) rowHeight = size.y;
            }

            System.IO.Directory.CreateDirectory("Assets/Scenes");
            EditorSceneManager.SaveScene(scene, ScenePath);
            AssetDatabase.Refresh();

            Debug.Log("描画サンプルシーンを作成しました: " + ScenePath);
            EditorUtility.FocusProjectWindow();
            Selection.activeObject = AssetDatabase.LoadAssetAtPath<Object>(ScenePath);
        }

        private static Canvas CreateCanvas()
        {
            var go = new GameObject("Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            var canvas = go.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;

            var scaler = go.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080f, 1920f);   // 縦持ちのスマホ想定
            scaler.matchWidthOrHeight = 0f;

            var rect = (RectTransform)go.transform;
            rect.anchorMin = rect.anchorMax = new Vector2(0f, 1f);

            // 見た目の確認だけのシーンなので EventSystem は置かない。
            return canvas;
        }
    }
}
