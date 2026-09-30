using System;
using System.Collections.Generic;
using System.Text;

namespace Pentomino.Core
{
    /// <summary>
    /// Posture_DB.json を読み込んだ姿勢の一覧。
    /// 回転・反転の対応表はハードコードせず、読み込んだ形状同士を幾何的に照合して自動生成する。
    /// </summary>
    public sealed class PostureDatabase
    {
        private readonly Dictionary<string, Posture> _byKey;
        private readonly Posture[][] _byPiece;      // Pieces.IndexOf(piece) -> 姿勢番号順
        private readonly Posture[] _all;

        private PostureDatabase(Dictionary<string, Posture> byKey)
        {
            _byKey = byKey;

            var all = new List<Posture>(byKey.Count);
            foreach (var p in byKey.Values) all.Add(p);
            all.Sort((a, b) =>
            {
                var c = a.Piece.CompareTo(b.Piece);
                return c != 0 ? c : a.Index.CompareTo(b.Index);
            });
            _all = all.ToArray();

            _byPiece = new Posture[Pieces.Count][];
            var buckets = new List<Posture>[Pieces.Count];
            for (var i = 0; i < Pieces.Count; i++) buckets[i] = new List<Posture>();
            foreach (var p in _all) buckets[Pieces.IndexOf(p.Piece)].Add(p);
            for (var i = 0; i < Pieces.Count; i++)
            {
                if (buckets[i].Count == 0)
                    throw new FormatException("ピース " + Pieces.At(i) + " の姿勢が 1 つも定義されていません。");
                _byPiece[i] = buckets[i].ToArray();
            }

            BuildTransformTables();
        }

        /// <summary>全 99 姿勢（ピース名、姿勢番号の順）。</summary>
        public IReadOnlyList<Posture> All => _all;

        public int Count => _all.Length;

        public Posture this[string key] => Get(key);

        public static PostureDatabase FromJson(string json)
        {
            var raw = PostureJsonParser.Parse(json);
            var byKey = new Dictionary<string, Posture>(raw.Count, StringComparer.Ordinal);
            foreach (var pair in raw)
            {
                var posture = new Posture(pair.Key, pair.Value);
                ValidateNormalized(posture);
                byKey.Add(pair.Key, posture);
            }
            return new PostureDatabase(byKey);
        }

        public Posture Get(string key)
        {
            if (!_byKey.TryGetValue(key, out var posture))
                throw new KeyNotFoundException("未知の姿勢キーです: " + key);
            return posture;
        }

        public bool TryGet(string key, out Posture posture) => _byKey.TryGetValue(key, out posture);

        /// <summary>指定ピースの姿勢を姿勢番号順に返す。</summary>
        public IReadOnlyList<Posture> PosturesOf(char piece)
        {
            var index = Pieces.IndexOf(piece);
            if (index < 0) throw new ArgumentException("未知のピース名です: " + piece, nameof(piece));
            return _byPiece[index];
        }

        /// <summary>そのピースの既定姿勢（姿勢番号 00）。上級の待機場所で使う。</summary>
        public Posture DefaultPosture(char piece) => Get(piece + "00");

        /// <summary>
        /// 原点セルが「解を探す順で最初のセル」に正規化されているかを検証する。
        /// 解を探す順は、上の行から、左の格子（Layer 0）の左から右、続けて右の格子（Layer 1）の左から右。
        /// Answer 文字列の座標はこの規約を前提としている。
        /// </summary>
        private static void ValidateNormalized(Posture posture)
        {
            if (posture.Cells[0] != new Cell(0, 0, 0))
                throw new FormatException(posture.Key + ": 先頭のセルが原点 (0,0,0) ではありません。");

            foreach (var c in posture.Cells)
            {
                if (ScanOrder(c, new Cell(0, 0, 0)) < 0)
                    throw new FormatException(posture.Key + ": 原点より先に探すセル " + c + " があります。");
            }

            if (posture.Depth > Board.Layers)
                throw new FormatException(posture.Key + ": BOX の段数 " + Board.Layers + " に収まりません。");
        }

        /// <summary>解を探す順での前後。負なら a が先。</summary>
        private static int ScanOrder(Cell a, Cell b)
        {
            var c = a.Row.CompareTo(b.Row);
            if (c != 0) return c;
            c = a.Layer.CompareTo(b.Layer);
            return c != 0 ? c : a.Col.CompareTo(b.Col);
        }

        private void BuildTransformTables()
        {
            // 正規形（原点を探す順の先頭セルに揃えた形）から姿勢を引く索引。
            var byShape = new Dictionary<string, Posture>(_all.Length, StringComparer.Ordinal);
            foreach (var p in _all)
            {
                var shape = ShapeKey(p.Cells);
                if (byShape.ContainsKey(shape))
                    throw new FormatException("形状が重複する姿勢があります: " + p.Key + " と " + byShape[shape].Key);
                byShape[shape] = p;
            }

            foreach (var p in _all)
            {
                p.RotatedCcw = Lookup(byShape, p, Transform(p.Cells, RotateCcw), "反時計回り 90 度");
                p.RotatedCw = Lookup(byShape, p, Transform(p.Cells, RotateCw), "時計回り 90 度");

                p.RolledLeft = Roll(byShape, p, RollLeft, HalfTurnHorizontal, "左へ転がす", out var half);
                p.HalfTurnLeft = half;
                p.RolledRight = Roll(byShape, p, RollRight, HalfTurnHorizontal, "右へ転がす", out half);
                p.HalfTurnRight = half;
                p.RolledUp = Roll(byShape, p, RollUp, HalfTurnVertical, "上へ転がす", out half);
                p.HalfTurnUp = half;
                p.RolledDown = Roll(byShape, p, RollDown, HalfTurnVertical, "下へ転がす", out half);
                p.HalfTurnDown = half;
            }
        }

        /// <summary>
        /// フリックで転がした先の姿勢。90 度で段数に収まらなければ 180 度にする。
        /// どちらも収まらなければ null（halfTurn は true）。
        ///
        /// 収まるのに Posture_DB.json に無い形は、データの欠けなので例外にする。
        /// </summary>
        private static Posture Roll(Dictionary<string, Posture> byShape, Posture source,
            Func<Cell, Cell> quarter, Func<Cell, Cell> half, string operation, out bool halfTurn)
        {
            var turned = Transform(source.Cells, quarter);
            if (Fits(turned))
            {
                halfTurn = false;
                return Lookup(byShape, source, turned, operation);
            }

            halfTurn = true;
            turned = Transform(source.Cells, half);
            if (Fits(turned)) return Lookup(byShape, source, turned, operation + "（180 度）");
            return null;
        }

        /// <summary>BOX の段数に収まるか。行と列は BOX の広さに比べて十分小さい。</summary>
        private static bool Fits(Cell[] cells)
        {
            var min = int.MaxValue;
            var max = int.MinValue;
            foreach (var c in cells)
            {
                if (c.Layer < min) min = c.Layer;
                if (c.Layer > max) max = c.Layer;
            }
            return max - min + 1 <= Board.Layers;
        }

        private static Posture Lookup(Dictionary<string, Posture> byShape, Posture source, Cell[] cells, string operation)
        {
            var key = ShapeKey(cells);
            if (!byShape.TryGetValue(key, out var found))
                throw new FormatException(source.Key + " を" + operation + "した形が Posture_DB.json に存在しません。");
            if (found.Piece != source.Piece)
                throw new FormatException(source.Key + " を" + operation + "した形が別ピース " + found.Key + " と一致しました。");
            return found;
        }

        // 座標系は 行 = 画面の下向き、列 = 右向き、段 = 画面の手前向き（右の格子が上の段）。
        //
        // 画面の面内の回転は行と列だけを動かす。
        // 反時計回り 90 度: (r, c) -> (-c, r)
        private static Cell RotateCcw(Cell c) => new Cell(-c.Col, c.Row, c.Layer);

        // 時計回り 90 度: (r, c) -> (c, -r)
        private static Cell RotateCw(Cell c) => new Cell(c.Col, -c.Row, c.Layer);

        // 横のフリックは画面の縦軸まわり（列と段）。右へ転がすと、上の段が右へ、右端が下の段へ行く。
        private static Cell RollRight(Cell c) => new Cell(c.Row, c.Layer, -c.Col);

        private static Cell RollLeft(Cell c) => new Cell(c.Row, -c.Layer, c.Col);

        // 縦のフリックは画面の横軸まわり（行と段）。上へ転がすと、上の段が上へ、上端が下の段へ行く。
        private static Cell RollUp(Cell c) => new Cell(-c.Layer, c.Col, c.Row);

        private static Cell RollDown(Cell c) => new Cell(c.Layer, c.Col, -c.Row);

        // 180 度は向きによらず同じ。縦軸まわりなら列と段、横軸まわりなら行と段が裏返る。
        private static Cell HalfTurnHorizontal(Cell c) => new Cell(c.Row, -c.Col, -c.Layer);

        private static Cell HalfTurnVertical(Cell c) => new Cell(-c.Row, c.Col, -c.Layer);

        private static Cell[] Transform(IReadOnlyList<Cell> cells, Func<Cell, Cell> map)
        {
            var result = new Cell[cells.Count];
            for (var i = 0; i < cells.Count; i++) result[i] = map(cells[i]);
            return Normalize(result);
        }

        /// <summary>原点を「解を探す順で最初のセル」に移して正規化する。</summary>
        private static Cell[] Normalize(Cell[] cells)
        {
            var origin = cells[0];
            foreach (var c in cells) if (ScanOrder(c, origin) < 0) origin = c;

            for (var i = 0; i < cells.Length; i++)
                cells[i] = new Cell(cells[i].Row - origin.Row, cells[i].Col - origin.Col, cells[i].Layer - origin.Layer);
            return cells;
        }

        /// <summary>正規化済みセル集合を順序に依存しない文字列にする。</summary>
        private static string ShapeKey(IReadOnlyList<Cell> cells)
        {
            var sorted = new Cell[cells.Count];
            for (var i = 0; i < cells.Count; i++) sorted[i] = cells[i];
            Array.Sort(sorted, ScanOrder);

            var sb = new StringBuilder(cells.Count * 8);
            foreach (var c in sorted) sb.Append(c.Row).Append(':').Append(c.Col).Append(':').Append(c.Layer).Append(';');
            return sb.ToString();
        }
    }
}
