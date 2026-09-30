using System;
using System.Collections.Generic;

namespace Pentomino.Core
{
    /// <summary>
    /// Posture_DB.json に定義された 1 つの姿勢（ピース名 + 姿勢番号）。
    /// セルは原点セルを (0,0) とする相対座標で保持する。
    /// 原点セルは「最左列の最上セル」であり、常に Cells[0] == (0,0)。
    /// </summary>
    public sealed class Posture
    {
        private readonly Cell[] _cells;

        internal Posture(string key, IReadOnlyList<Cell> cells)
        {
            if (key == null) throw new ArgumentNullException(nameof(key));
            if (key.Length != 3) throw new ArgumentException("姿勢キーは 3 文字である必要があります: " + key, nameof(key));
            if (cells == null) throw new ArgumentNullException(nameof(cells));
            if (cells.Count != Pieces.CellsPerPiece)
                throw new ArgumentException("ペントミノは 5 セルである必要があります: " + key, nameof(cells));

            Key = key;
            Piece = key[0];
            if (!Pieces.IsValid(Piece))
                throw new ArgumentException("未知のピース名です: " + key, nameof(key));
            if (!int.TryParse(key.Substring(1), out var index))
                throw new ArgumentException("姿勢番号を解釈できません: " + key, nameof(key));
            Index = index;

            _cells = new Cell[cells.Count];
            for (var i = 0; i < cells.Count; i++) _cells[i] = cells[i];

            MinRow = MaxRow = _cells[0].Row;
            MinCol = MaxCol = _cells[0].Col;
            foreach (var c in _cells)
            {
                if (c.Row < MinRow) MinRow = c.Row;
                if (c.Row > MaxRow) MaxRow = c.Row;
                if (c.Col < MinCol) MinCol = c.Col;
                if (c.Col > MaxCol) MaxCol = c.Col;
            }
        }

        /// <summary>"F00" のような 3 文字キー。</summary>
        public string Key { get; }

        /// <summary>ピース名 1 文字。</summary>
        public char Piece { get; }

        /// <summary>姿勢番号 0..7。</summary>
        public int Index { get; }

        /// <summary>原点セルからの相対座標。要素 0 は必ず (0,0)。</summary>
        public IReadOnlyList<Cell> Cells => _cells;

        public int MinRow { get; }
        public int MaxRow { get; }
        public int MinCol { get; }
        public int MaxCol { get; }

        /// <summary>外接矩形の高さ（行数）。</summary>
        public int Height => MaxRow - MinRow + 1;

        /// <summary>外接矩形の幅（列数）。</summary>
        public int Width => MaxCol - MinCol + 1;

        /// <summary>反時計回りに 90 度回した姿勢（軽いタップ）。</summary>
        public Posture RotatedCcw { get; internal set; }

        /// <summary>時計回りに 90 度回した姿勢。</summary>
        public Posture RotatedCw { get; internal set; }

        /// <summary>
        /// 左右反転した姿勢（横向きのフリック）。アキラルなピース（I, T, U, V, W, X）では
        /// 同じ回転群の中の姿勢に移るため、見た目上は反転しても新しい形にならない。
        /// </summary>
        public Posture FlippedHorizontally { get; internal set; }

        /// <summary>
        /// 上下反転した姿勢（縦向きのフリック）。
        ///
        /// 平面では「左右反転して 180 度回す」のと同じ結果になるが、
        /// 立体版では別の操作（上下方向の 90 度回転）に置き換わるので、
        /// 最初から独立した姿勢として持たせておく。
        /// </summary>
        public Posture FlippedVertically { get; internal set; }

        /// <summary>反転しても形が変わらないか（アキラル）。I, T, U, V, W, X が該当する。</summary>
        public bool IsAchiral { get; internal set; }

        /// <summary>この姿勢の原点を (row, col) に置いたときに占めるセルを列挙する。</summary>
        public IEnumerable<Cell> CellsAt(int row, int col)
        {
            foreach (var c in _cells) yield return new Cell(row + c.Row, col + c.Col);
        }

        public override string ToString() => Key;
    }
}
