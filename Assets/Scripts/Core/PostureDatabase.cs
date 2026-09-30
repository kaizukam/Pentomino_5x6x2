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

        /// <summary>全 63 姿勢（ピース名、姿勢番号の順）。</summary>
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

        /// <summary>反転しても回転だけで元に戻せるピースか（I, T, U, V, W, X）。</summary>
        public bool IsAchiral(char piece) => PosturesOf(piece)[0].IsAchiral;

        /// <summary>
        /// 原点セルが「最左列の最上セル」に正規化されているかを検証する。
        /// Answer 文字列の座標はこの規約を前提としている（開発仕様 V1 83 行目）。
        /// </summary>
        private static void ValidateNormalized(Posture posture)
        {
            if (posture.Cells[0] != new Cell(0, 0))
                throw new FormatException(posture.Key + ": 先頭のセルが原点 (0,0) ではありません。");
            if (posture.MinCol != 0)
                throw new FormatException(posture.Key + ": 原点が最左列にありません。");

            foreach (var c in posture.Cells)
            {
                if (c.Col == 0 && c.Row < 0)
                    throw new FormatException(posture.Key + ": 最左列に原点より上のセルがあります。");
            }
        }

        private void BuildTransformTables()
        {
            // 正規形（原点を最左列の最上セルに揃えた形）から姿勢を引く索引。
            var byShape = new Dictionary<string, Posture>(_all.Length, StringComparer.Ordinal);
            foreach (var p in _all)
            {
                var shape = ShapeKey(p.Cells);
                if (byShape.ContainsKey(shape))
                    throw new FormatException("形状が重複する姿勢があります: " + p.Key + " と " + byShape[shape].Key);
                byShape[shape] = p;
            }

            var rotated = new Cell[Pieces.CellsPerPiece];
            foreach (var p in _all)
            {
                p.RotatedCcw = Lookup(byShape, p, RotateCcw(p.Cells, rotated), "反時計回り 90 度");
                p.RotatedCw = Lookup(byShape, p, RotateCw(p.Cells, rotated), "時計回り 90 度");
                p.FlippedHorizontally = Lookup(byShape, p, FlipHorizontally(p.Cells, rotated), "左右反転");
                p.FlippedVertically = Lookup(byShape, p, FlipVertically(p.Cells, rotated), "上下反転");
            }

            // 反転先が同じ回転群に属していればアキラル。
            foreach (var p in _all)
            {
                var isAchiral = false;
                var q = p;
                for (var i = 0; i < 4; i++)
                {
                    if (ReferenceEquals(q, p.FlippedHorizontally)) { isAchiral = true; break; }
                    q = q.RotatedCcw;
                }
                p.IsAchiral = isAchiral;
            }
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

        // 行が下向き、列が右向きの座標系での反時計回り 90 度: (r, c) -> (-c, r)
        private static Cell[] RotateCcw(IReadOnlyList<Cell> cells, Cell[] buffer)
        {
            for (var i = 0; i < cells.Count; i++) buffer[i] = new Cell(-cells[i].Col, cells[i].Row);
            return Normalize(buffer);
        }

        // 時計回り 90 度: (r, c) -> (c, -r)
        private static Cell[] RotateCw(IReadOnlyList<Cell> cells, Cell[] buffer)
        {
            for (var i = 0; i < cells.Count; i++) buffer[i] = new Cell(cells[i].Col, -cells[i].Row);
            return Normalize(buffer);
        }

        // 左右反転: (r, c) -> (r, -c)
        private static Cell[] FlipHorizontally(IReadOnlyList<Cell> cells, Cell[] buffer)
        {
            for (var i = 0; i < cells.Count; i++) buffer[i] = new Cell(cells[i].Row, -cells[i].Col);
            return Normalize(buffer);
        }

        // 上下反転: (r, c) -> (-r, c)
        private static Cell[] FlipVertically(IReadOnlyList<Cell> cells, Cell[] buffer)
        {
            for (var i = 0; i < cells.Count; i++) buffer[i] = new Cell(-cells[i].Row, cells[i].Col);
            return Normalize(buffer);
        }

        /// <summary>原点を「最左列の最上セル」に移して正規化する。</summary>
        private static Cell[] Normalize(Cell[] cells)
        {
            var minCol = int.MaxValue;
            foreach (var c in cells) if (c.Col < minCol) minCol = c.Col;

            var baseRow = int.MaxValue;
            foreach (var c in cells) if (c.Col == minCol && c.Row < baseRow) baseRow = c.Row;

            for (var i = 0; i < cells.Length; i++)
                cells[i] = new Cell(cells[i].Row - baseRow, cells[i].Col - minCol);
            return cells;
        }

        /// <summary>正規化済みセル集合を順序に依存しない文字列にする。</summary>
        private static string ShapeKey(IReadOnlyList<Cell> cells)
        {
            var sorted = new Cell[cells.Count];
            for (var i = 0; i < cells.Count; i++) sorted[i] = cells[i];
            Array.Sort(sorted, (a, b) =>
            {
                var c = a.Col.CompareTo(b.Col);
                return c != 0 ? c : a.Row.CompareTo(b.Row);
            });

            var sb = new StringBuilder(cells.Count * 6);
            foreach (var c in sorted) sb.Append(c.Row).Append(':').Append(c.Col).Append(';');
            return sb.ToString();
        }
    }
}
