using System.Collections.Generic;
using Pentomino.Core;
using UnityEngine;
using UnityEngine.UI;

namespace Pentomino.View
{
    /// <summary>
    /// ポリオミノ 1 個分の表示。図形本体（PolyominoGraphic）と、
    /// 各セル中央に置くピース名の文字をまとめて面倒を見る。
    ///
    /// 文字は組み込みフォントの UI Text で描いている。
    /// TextMeshPro の Essential Resources を導入したら、この 1 クラスの差し替えで移行できる。
    /// </summary>
    [AddComponentMenu("Pentomino/Polyomino View")]
    [RequireComponent(typeof(RectTransform))]
    public sealed class PolyominoView : MonoBehaviour
    {
        private static Font _builtinFont;

        private readonly List<Text> _labels = new List<Text>();
        private PolyominoGraphic _graphic;
        private RectTransform _rectTransform;

        public RectTransform RectTransform
        {
            get
            {
                if (_rectTransform == null) _rectTransform = (RectTransform)transform;
                return _rectTransform;
            }
        }

        public PolyominoGraphic Graphic
        {
            get
            {
                if (_graphic == null)
                {
                    _graphic = GetComponent<PolyominoGraphic>();
                    if (_graphic == null) _graphic = gameObject.AddComponent<PolyominoGraphic>();
                }
                return _graphic;
            }
        }

        /// <summary>6X10 の BOX を描く。文字は入れない。</summary>
        public void RenderBoard(PentominoStyle style)
        {
            Render(new List<Cell>(PieceGeometry.BoardCells()), style.boardFillColor, '\0', style);
        }

        /// <summary>ピースを姿勢どおりに描く。各セル中央にピース名 1 文字が入る。</summary>
        public void RenderPiece(Posture posture, PentominoStyle style)
        {
            if (posture == null) return;
            Render(posture.Cells, style.ColorOf(posture.Piece), posture.Piece, style);
        }

        /// <summary>任意のセル集合を描く。label が '\0' なら文字を出さない。</summary>
        public void Render(IReadOnlyList<Cell> cells, Color fillColor, char label, PentominoStyle style)
        {
            if (style == null) style = new PentominoStyle();

            var graphic = Graphic;
            graphic.Style = style;
            graphic.SetCells(cells, fillColor);
            graphic.ResizeToFit();

            var needed = label == '\0' ? 0 : cells.Count;
            EnsureLabelCount(needed, style);

            var topLeft = graphic.TopLeft;
            for (var i = 0; i < needed; i++)
            {
                var text = _labels[i];
                text.text = label.ToString();
                text.color = style.labelColor;
                text.fontSize = Mathf.Max(1, Mathf.RoundToInt(style.cellSize * style.labelSizeRatio));

                var rect = (RectTransform)text.transform;
                rect.sizeDelta = new Vector2(style.cellSize, style.cellSize);
                // アンカーは左上なので、左上角からの相対位置に直す。
                rect.anchoredPosition = graphic.CellCenter(cells[i].Row, cells[i].Col) - topLeft;
            }
        }

        private void EnsureLabelCount(int count, PentominoStyle style)
        {
            while (_labels.Count < count)
            {
                var go = new GameObject("Label" + _labels.Count,
                    typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
                go.transform.SetParent(transform, false);

                var rect = (RectTransform)go.transform;
                rect.anchorMin = rect.anchorMax = new Vector2(0f, 1f);
                rect.pivot = new Vector2(0.5f, 0.5f);

                var text = go.GetComponent<Text>();
                text.font = BuiltinFont;
                text.alignment = TextAnchor.MiddleCenter;
                text.horizontalOverflow = HorizontalWrapMode.Overflow;
                text.verticalOverflow = VerticalWrapMode.Overflow;
                text.raycastTarget = false;
                text.color = style.labelColor;
                _labels.Add(text);
            }

            for (var i = 0; i < _labels.Count; i++) _labels[i].gameObject.SetActive(i < count);
        }

        private static Font BuiltinFont
        {
            get
            {
                if (_builtinFont != null) return _builtinFont;

                // Unity 6 では Arial.ttf が LegacyRuntime.ttf に置き換わっている。
                _builtinFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                if (_builtinFont == null) _builtinFont = Resources.GetBuiltinResource<Font>("Arial.ttf");
                return _builtinFont;
            }
        }

        /// <summary>親の下に新しい PolyominoView を作る。</summary>
        public static PolyominoView Create(Transform parent, string name)
        {
            var go = new GameObject(name,
                typeof(RectTransform), typeof(CanvasRenderer), typeof(PolyominoGraphic), typeof(PolyominoView));
            go.transform.SetParent(parent, false);

            var rect = (RectTransform)go.transform;
            rect.anchorMin = rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            return go.GetComponent<PolyominoView>();
        }
    }
}
