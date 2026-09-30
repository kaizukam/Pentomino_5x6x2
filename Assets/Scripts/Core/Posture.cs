using System;
using System.Collections.Generic;

namespace Pentomino.Core
{
    /// <summary>
    /// Posture_DB.json に定義された 1 つの姿勢（ピース名 + 姿勢番号）。
    /// セルは原点セルを (0,0,0) とする相対座標で保持する。
    ///
    /// 原点セルは解を探す順（上の段から、左の格子の左から右、続けて右の格子の左から右）で
    /// 最初に来るセル。つまり最上行のうち、段の小さいほうの最左セルで、常に Cells[0] == (0,0,0)。
    /// 立てて置く姿勢では、原点より手前の段（Layer が負）にセルが来ることがある。
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
            MinLayer = MaxLayer = _cells[0].Layer;
            foreach (var c in _cells)
            {
                if (c.Row < MinRow) MinRow = c.Row;
                if (c.Row > MaxRow) MaxRow = c.Row;
                if (c.Col < MinCol) MinCol = c.Col;
                if (c.Col > MaxCol) MaxCol = c.Col;
                if (c.Layer < MinLayer) MinLayer = c.Layer;
                if (c.Layer > MaxLayer) MaxLayer = c.Layer;
            }
        }

        /// <summary>"F00" のような 3 文字キー。</summary>
        public string Key { get; }

        /// <summary>ピース名 1 文字。</summary>
        public char Piece { get; }

        /// <summary>姿勢番号 0..15。</summary>
        public int Index { get; }

        /// <summary>原点セルからの相対座標。要素 0 は必ず (0,0,0)。</summary>
        public IReadOnlyList<Cell> Cells => _cells;

        public int MinRow { get; }
        public int MaxRow { get; }
        public int MinCol { get; }
        public int MaxCol { get; }
        public int MinLayer { get; }
        public int MaxLayer { get; }

        /// <summary>外接直方体の高さ（行数）。</summary>
        public int Height => MaxRow - MinRow + 1;

        /// <summary>外接直方体の幅（列数）。</summary>
        public int Width => MaxCol - MinCol + 1;

        /// <summary>外接直方体の段数。平らに置く姿勢は 1、立てて置く姿勢は 2。</summary>
        public int Depth => MaxLayer - MinLayer + 1;

        /// <summary>立てて置く姿勢か。盤では左右の格子に分かれて見える。</summary>
        public bool IsStanding => Depth > 1;

        /// <summary>画面の面内で反時計回りに 90 度回した姿勢（軽いタップ）。</summary>
        public Posture RotatedCcw { get; internal set; }

        /// <summary>画面の面内で時計回りに 90 度回した姿勢。</summary>
        public Posture RotatedCw { get; internal set; }

        /// <summary>
        /// フリックで転がした姿勢。向きごとに持つ。
        ///
        /// 横のフリックは画面の縦軸まわり、縦のフリックは画面の横軸まわりの 90 度回転。
        /// 90 度では BOX の高さ（2 段）に収まらないときは 180 度回す（持ち上げて裏返す）。
        /// それでも収まらなければ null。
        ///
        /// 180 度回したときと null のときは、画面がピースの外周を 2 回赤く点滅させる
        /// （<see cref="IsHalfTurn"/>）。90 度のつもりが裏返ったことを知らせる警告だが、
        /// 知っていれば 90 度を 2 回するより速いので、わざと使うこともできる。
        /// </summary>
        public Posture Rolled(FlickAxis direction)
        {
            switch (direction)
            {
                case FlickAxis.Left: return RolledLeft;
                case FlickAxis.Right: return RolledRight;
                case FlickAxis.Up: return RolledUp;
                case FlickAxis.Down: return RolledDown;
                default: return null;
            }
        }

        /// <summary>
        /// その向きのフリックが、90 度の代わりに 180 度回すことになるか。
        /// 回せない（<see cref="Rolled"/> が null）ときも true。どちらも赤く点滅させる。
        /// </summary>
        public bool IsHalfTurn(FlickAxis direction)
        {
            switch (direction)
            {
                case FlickAxis.Left: return HalfTurnLeft;
                case FlickAxis.Right: return HalfTurnRight;
                case FlickAxis.Up: return HalfTurnUp;
                case FlickAxis.Down: return HalfTurnDown;
                default: return false;
            }
        }

        internal Posture RolledLeft { get; set; }
        internal Posture RolledRight { get; set; }
        internal Posture RolledUp { get; set; }
        internal Posture RolledDown { get; set; }

        internal bool HalfTurnLeft { get; set; }
        internal bool HalfTurnRight { get; set; }
        internal bool HalfTurnUp { get; set; }
        internal bool HalfTurnDown { get; set; }

        /// <summary>この姿勢の原点を (row, col, layer) に置いたときに占めるセルを列挙する。</summary>
        public IEnumerable<Cell> CellsAt(int row, int col, int layer)
        {
            foreach (var c in _cells) yield return new Cell(row + c.Row, col + c.Col, layer + c.Layer);
        }

        public override string ToString() => Key;
    }
}
