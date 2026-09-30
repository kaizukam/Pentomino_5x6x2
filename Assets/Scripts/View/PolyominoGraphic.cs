using System;
using System.Collections.Generic;
using Pentomino.Core;
using UnityEngine;
using UnityEngine.UI;

namespace Pentomino.View
{
    /// <summary>
    /// セル集合を 1 枚のメッシュとして描く uGUI グラフィック。
    /// 地 → L2 細線 → L1 太線 の順に重ねるので、線の交点がきれいに繋がる。
    /// BOX もピースも同じ仕組みで描ける。
    ///
    /// 段（Layer）の違うセルは、段の数だけ右へ <see cref="PentominoStyle.LayerStride"/> ずらして描く。
    /// 盤の左右の格子も、立てたピースの左右の部分も、この決まりで並ぶ。
    /// </summary>
    [AddComponentMenu("Pentomino/Polyomino Graphic")]
    public sealed class PolyominoGraphic : MaskableGraphic, ICanvasRaycastFilter
    {
        private readonly List<Cell> _cells = new List<Cell>();
        private readonly List<GridLine> _outer = new List<GridLine>();
        private readonly List<GridLine> _inner = new List<GridLine>();

        [SerializeField] private PentominoStyle _style = new PentominoStyle();
        [SerializeField] private Color _fillColor = Color.white;

        private Color? _outerLineColor;

        private int _minRow;
        private int _minCol;
        private int _minLayer;
        private int _rows = 1;
        private float _width = 1f;

        public PentominoStyle Style
        {
            get => _style;
            set
            {
                _style = value ?? new PentominoStyle();
                SetVerticesDirty();
            }
        }

        public Color FillColor
        {
            get => _fillColor;
            set
            {
                _fillColor = value;
                SetVerticesDirty();
            }
        }

        /// <summary>外接矩形の左上セルの行。</summary>
        public int MinRow => _minRow;

        /// <summary>外接矩形の左上セルの列。</summary>
        public int MinCol => _minCol;

        /// <summary>いちばん左に描く段。</summary>
        public int MinLayer => _minLayer;

        /// <summary>セルを内側に収めるのに必要な大きさ。L1 のはみ出し分を含む。</summary>
        public Vector2 PreferredSize =>
            new Vector2(_width, _rows * _style.cellSize) + Vector2.one * _style.OuterLineWidth;

        /// <summary>描くセルを差し替える。</summary>
        public void SetCells(IEnumerable<Cell> cells, Color fillColor)
        {
            _cells.Clear();
            if (cells != null) _cells.AddRange(cells);
            _fillColor = fillColor;
            Rebuild();
        }

        /// <summary>RectTransform を PreferredSize に合わせる。</summary>
        public void ResizeToFit()
        {
            rectTransform.sizeDelta = PreferredSize;
            SetVerticesDirty();
        }

        /// <summary>
        /// 外周線 (L1) の色を上書きする。null なら Style の線色に戻す。
        /// はめ込み位置に吸い付いたことを示すのに使う。
        /// </summary>
        public Color? OuterLineColor
        {
            get => _outerLineColor;
            set
            {
                if (Nullable.Equals(_outerLineColor, value)) return;
                _outerLineColor = value;
                SetVerticesDirty();
            }
        }

        /// <summary>外接矩形の左上角のローカル座標。</summary>
        public Vector2 TopLeft
        {
            get
            {
                var rect = rectTransform.rect;
                return new Vector2(rect.xMin, rect.yMax);
            }
        }

        /// <summary>セルの左上角のローカル座標。</summary>
        public Vector2 CellCorner(Cell cell)
        {
            var half = _style.OuterLineWidth * 0.5f;
            var topLeft = TopLeft;
            return new Vector2(
                topLeft.x + half + LayerOffset(cell.Layer) + (cell.Col - _minCol) * _style.cellSize,
                topLeft.y - half - (cell.Row - _minRow) * _style.cellSize);
        }

        /// <summary>セルの中心のローカル座標。文字の配置に使う。</summary>
        public Vector2 CellCenter(Cell cell)
        {
            var corner = CellCorner(cell);
            var half = _style.cellSize * 0.5f;
            return new Vector2(corner.x + half, corner.y - half);
        }

        /// <summary>段 layer の格子を、いちばん左の段からどれだけ右へずらすか。</summary>
        private float LayerOffset(int layer) => (layer - _minLayer) * _style.LayerStride;

        protected override void OnPopulateMesh(VertexHelper helper)
        {
            helper.Clear();
            if (_cells.Count == 0) return;

            var rect = GetPixelAdjustedRect();
            var half = _style.OuterLineWidth * 0.5f;
            var left = rect.xMin + half;
            var top = rect.yMax - half;
            var cell = _style.cellSize;

            // Graphic.color を掛けることで、はめ込み時の沈み込みなどの色変化に使える。
            var tint = color;
            var fill = _fillColor * tint;
            var lineColor = _style.lineColor * tint;

            // 地
            foreach (var c in _cells)
            {
                var x = left + LayerOffset(c.Layer) + (c.Col - _minCol) * cell;
                var y = top - (c.Row - _minRow) * cell;
                AddQuad(helper, x, y - cell, x + cell, y, fill);
            }

            // L2 細線（内部の格子）
            var innerWidth = _style.InnerLineWidth;
            foreach (var line in _inner) AddLine(helper, line, left, top, cell, innerWidth, 0f, lineColor);

            // L1 太線（外周）。角が直角に繋がるよう両端を線幅の半分だけ伸ばす。
            var outerWidth = _style.OuterLineWidth;
            var outerColor = (_outerLineColor ?? _style.lineColor) * tint;
            foreach (var line in _outer) AddLine(helper, line, left, top, cell, outerWidth, outerWidth * 0.5f, outerColor);
        }

        /// <summary>
        /// 当たり判定を実際に埋まっているセルだけに限る。
        /// 外接矩形で判定すると、L 字などの凹んだ部分でも掴めてしまう。
        /// </summary>
        public bool IsRaycastLocationValid(Vector2 screenPoint, Camera eventCamera)
        {
            if (!raycastTarget) return false;
            if (_cells.Count == 0) return false;

            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    rectTransform, screenPoint, eventCamera, out var local)) return false;

            var size = _style.cellSize;
            foreach (var c in _cells)
            {
                var corner = CellCorner(c);
                if (local.x >= corner.x && local.x < corner.x + size &&
                    local.y <= corner.y && local.y > corner.y - size) return true;
            }
            return false;
        }

        protected override void OnRectTransformDimensionsChange()
        {
            base.OnRectTransformDimensionsChange();
            SetVerticesDirty();
        }

#if UNITY_EDITOR
        protected override void OnValidate()
        {
            base.OnValidate();
            Rebuild();
        }
#endif

        private void Rebuild()
        {
            if (_cells.Count == 0)
            {
                _outer.Clear();
                _inner.Clear();
                _minRow = _minCol = _minLayer = 0;
                _rows = 1;
                _width = _style.cellSize;
                SetVerticesDirty();
                return;
            }

            _minRow = int.MaxValue;
            _minCol = int.MaxValue;
            _minLayer = int.MaxValue;
            var maxRow = int.MinValue;
            foreach (var c in _cells)
            {
                if (c.Row < _minRow) _minRow = c.Row;
                if (c.Row > maxRow) maxRow = c.Row;
                if (c.Col < _minCol) _minCol = c.Col;
                if (c.Layer < _minLayer) _minLayer = c.Layer;
            }
            _rows = maxRow - _minRow + 1;

            // 幅は右端のセルの右辺まで。段がずれるので列の数だけでは決まらない。
            _width = 0f;
            foreach (var c in _cells)
            {
                var right = LayerOffset(c.Layer) + (c.Col - _minCol + 1) * _style.cellSize;
                if (right > _width) _width = right;
            }

            PieceGeometry.Build(_cells, _outer, _inner);
            SetVerticesDirty();
        }

        private void AddLine(VertexHelper helper, GridLine line, float left, float top, float cell,
            float width, float extend, Color lineColor)
        {
            var half = width * 0.5f;
            left += LayerOffset(line.Layer);
            if (line.Horizontal)
            {
                var y = top - (line.Row - _minRow) * cell;
                var x0 = left + (line.Col - _minCol) * cell - extend;
                var x1 = left + (line.Col - _minCol + line.Length) * cell + extend;
                AddQuad(helper, x0, y - half, x1, y + half, lineColor);
            }
            else
            {
                var x = left + (line.Col - _minCol) * cell;
                var y0 = top - (line.Row - _minRow) * cell + extend;
                var y1 = top - (line.Row - _minRow + line.Length) * cell - extend;
                AddQuad(helper, x - half, y1, x + half, y0, lineColor);
            }
        }

        private static void AddQuad(VertexHelper helper, float xMin, float yMin, float xMax, float yMax, Color color)
        {
            var index = helper.currentVertCount;
            helper.AddVert(new Vector3(xMin, yMin), color, Vector2.zero);
            helper.AddVert(new Vector3(xMin, yMax), color, Vector2.zero);
            helper.AddVert(new Vector3(xMax, yMax), color, Vector2.zero);
            helper.AddVert(new Vector3(xMax, yMin), color, Vector2.zero);
            helper.AddTriangle(index, index + 1, index + 2);
            helper.AddTriangle(index + 2, index + 3, index);
        }
    }
}
