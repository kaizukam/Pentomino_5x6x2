using Pentomino.Core;
using UnityEngine;

namespace Pentomino.View
{
    /// <summary>
    /// 作業中の BOX 格子図。格子の描画と、ワールド座標とセル座標の変換を受け持つ。
    /// 置かれたピースは PiecesLayer の下にぶら下がる。
    /// </summary>
    [AddComponentMenu("Pentomino/Board Widget")]
    [RequireComponent(typeof(RectTransform))]
    public sealed class BoardWidget : MonoBehaviour
    {
        private PolyominoView _grid;
        private PentominoStyle _style;

        public RectTransform RectTransform => (RectTransform)transform;

        /// <summary>盤上に確定したピースの親。</summary>
        public RectTransform PiecesLayer { get; private set; }

        public PentominoStyle Style => _style;

        public float CellSize => _style.cellSize;

        /// <summary>格子の外接サイズ。L1 のはみ出しを含む。</summary>
        public Vector2 Size => _grid.Graphic.PreferredSize;

        /// <summary>1 セルの一辺がこの長さになるよう格子を作る。</summary>
        public void Build(PentominoStyle style)
        {
            _style = style;

            _grid = PolyominoView.Create(transform, "Grid");
            _grid.RenderBoard(style);

            var layer = new GameObject("Pieces", typeof(RectTransform));
            PiecesLayer = (RectTransform)layer.transform;
            PiecesLayer.SetParent(transform, false);
            PiecesLayer.anchorMin = PiecesLayer.anchorMax = new Vector2(0f, 1f);
            PiecesLayer.pivot = new Vector2(0f, 1f);
            PiecesLayer.anchoredPosition = Vector2.zero;
            PiecesLayer.sizeDelta = _grid.Graphic.PreferredSize;

            RectTransform.sizeDelta = _grid.Graphic.PreferredSize;
        }

        /// <summary>セル (row, col) の左上角のワールド座標。</summary>
        public Vector3 CellCornerWorld(int row, int col) =>
            _grid.Graphic.transform.TransformPoint(_grid.Graphic.CellCorner(row, col));

        /// <summary>セルの中心のワールド座標。</summary>
        public Vector3 CellCenterWorld(int row, int col) =>
            _grid.Graphic.transform.TransformPoint(_grid.Graphic.CellCenter(row, col));

        /// <summary>ワールド座標がどのセルの上にあるかを返す。盤外なら false。</summary>
        public bool TryWorldToCell(Vector3 world, out int row, out int col)
        {
            var local = _grid.Graphic.transform.InverseTransformPoint(world);
            var origin = _grid.Graphic.CellCorner(0, 0);

            col = Mathf.FloorToInt((local.x - origin.x) / _style.cellSize);
            row = Mathf.FloorToInt((origin.y - local.y) / _style.cellSize);
            return Board.InRange(row, col);
        }
    }
}
