using System;
using Pentomino.Core;
using UnityEngine;

namespace Pentomino.View
{
    /// <summary>
    /// 描画の見た目の設定。開発仕様「ピースの描画」に対応する。
    ///
    /// 線は黒の 2 種類だけで、L1 が太線（外周）、L2 が細線（内部の格子）。
    /// BOX は外周が L1、地が薄いグレー、内部に L2 の格子。
    /// ピースは外周が L1、地がピースごとの色、内部に L2 の格子と、各セル中央にピース名 1 文字。
    ///
    /// 色と線幅は後で調整する前提なので、すべてここに集約してある。
    /// </summary>
    [Serializable]
    public sealed class PentominoStyle
    {
        /// <summary>ピース名のアルファベット順 (FILNPTUVWXYZ) に並べた既定のパステル 12 色。</summary>
        public static readonly Color32[] DefaultPieceColors =
        {
            new Color32(0xF7, 0xB7, 0xA3, 0xFF), // F サーモン
            new Color32(0xF9, 0xD5, 0xA7, 0xFF), // I アプリコット
            new Color32(0xF2, 0xE6, 0xA0, 0xFF), // L 淡い黄
            new Color32(0xD6, 0xE8, 0xA8, 0xFF), // N 若草
            new Color32(0xA8, 0xDE, 0xB5, 0xFF), // P ミント
            new Color32(0xA5, 0xDD, 0xD5, 0xFF), // T ティール
            new Color32(0xA9, 0xD3, 0xEC, 0xFF), // U 空色
            new Color32(0xB7, 0xC4, 0xEE, 0xFF), // V 藤
            new Color32(0xC9, 0xB8, 0xE8, 0xFF), // W ラベンダー
            new Color32(0xE0, 0xB6, 0xE0, 0xFF), // X オーキッド
            new Color32(0xF2, 0xB3, 0xCE, 0xFF), // Y ピンク
            new Color32(0xE3, 0xD5, 0xC0, 0xFF), // Z サンド
        };

        [Tooltip("1 セルの一辺の長さ（ピクセル）")]
        public float cellSize = 48f;

        [Tooltip("L1 太線の幅。セル一辺に対する比率")]
        [Range(0.01f, 0.25f)] public float outerLineRatio = 0.08f;

        [Tooltip("L2 細線の幅。セル一辺に対する比率")]
        [Range(0.005f, 0.15f)] public float innerLineRatio = 0.03f;

        [Tooltip("L1 と L2 の色。仕様では黒")]
        public Color lineColor = Color.black;

        [Tooltip("はめ込み位置に吸い付いたときの外周線の色")]
        public Color snapLineColor = new Color(0.13f, 0.45f, 0.92f);

        [Tooltip("指で掴んでいる間の外周線の色")]
        public Color heldLineColor = new Color(0.85f, 0.13f, 0.13f);

        [Tooltip("BOX の空きセルの色。絵柄案では白")]
        public Color boardFillColor = Color.white;

        [Tooltip("ピース名の文字色")]
        public Color labelColor = Color.black;

        [Tooltip("ピース名の文字サイズ。セル一辺に対する比率")]
        [Range(0.2f, 0.9f)] public float labelSizeRatio = 0.5f;

        [Tooltip("ピースごとの色。アルファベット順 FILNPTUVWXYZ")]
        public Color[] pieceColors = ToColors(DefaultPieceColors);

        [Tooltip("左右の格子（段 0 と段 1）の太線と太線の間の隙間。セル一辺に対する比率")]
        [Range(0f, 1f)] public float layerGapRatio = 0.2f;

        public float OuterLineWidth => cellSize * outerLineRatio;

        public float InnerLineWidth => cellSize * innerLineRatio;

        /// <summary>
        /// 段が 1 つ違うセルの横の隔たり。左の格子の幅に、太線 1 本分と隙間を足したもの。
        /// 盤でも待機場所でも同じ値なので、立てたピースの左右の部分は、盤の左右の格子と同じ間隔で並ぶ。
        /// </summary>
        public float LayerStride => Board.Cols * cellSize + OuterLineWidth + cellSize * layerGapRatio;

        /// <summary>ピースの色。未設定なら既定色を返す。</summary>
        public Color ColorOf(char piece)
        {
            var index = Pieces.IndexOf(piece);
            if (index < 0) return Color.white;
            if (pieceColors != null && index < pieceColors.Length) return pieceColors[index];
            return DefaultPieceColors[index];
        }

        public PentominoStyle Clone() => (PentominoStyle)MemberwiseClone();

        private static Color[] ToColors(Color32[] source)
        {
            var result = new Color[source.Length];
            for (var i = 0; i < source.Length; i++) result[i] = source[i];
            return result;
        }
    }
}
