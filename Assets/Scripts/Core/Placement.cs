using System;
using System.Collections.Generic;
using System.Text;

namespace Pentomino.Core
{
    /// <summary>
    /// 姿勢と原点座標の組。Answer 文字列の 7 文字 1 ブロックに対応する。
    /// 文字列書式: 姿勢キー(3) + 列(1) + 行(2) + 段(1)。例 "X003031" = X00 を (列3, 行3, 段1) に配置。
    /// 座標の並びは Posture_DB.json の [d0, d1, d2] と同じ（Cell の説明を参照）。
    /// </summary>
    public readonly struct Placement : IEquatable<Placement>
    {
        /// <summary>Answer 文字列 1 ブロックの文字数。</summary>
        public const int TokenLength = 7;

        /// <summary>盤外（待機場所）を表す座標部分。進捗データの保存に使う。</summary>
        public const string OffBoardCoordinates = "9999";

        public readonly Posture Posture;

        /// <summary>原点セルの行 0..5。</summary>
        public readonly int Row;

        /// <summary>原点セルの列 0..4。</summary>
        public readonly int Col;

        /// <summary>原点セルの段 0..1。</summary>
        public readonly int Layer;

        public Placement(Posture posture, int row, int col, int layer)
        {
            Posture = posture ?? throw new ArgumentNullException(nameof(posture));
            Row = row;
            Col = col;
            Layer = layer;
        }

        public char Piece => Posture.Piece;

        public bool IsValid => Posture != null;

        /// <summary>この配置が占めるセルを列挙する。</summary>
        public IEnumerable<Cell> Cells() => Posture.CellsAt(Row, Col, Layer);

        /// <summary>7 文字のトークンに符号化する。</summary>
        public string Encode()
        {
            var sb = new StringBuilder(TokenLength);
            sb.Append(Posture.Key);
            sb.Append((char)('0' + Col));
            sb.Append((char)('0' + Row / 10));
            sb.Append((char)('0' + Row % 10));
            sb.Append((char)('0' + Layer));
            return sb.ToString();
        }

        /// <summary>7 文字のトークンを解釈する。盤外トークン("F039999" 等)は false を返す。</summary>
        public static bool TryParse(string token, int offset, PostureDatabase database, out Placement placement)
        {
            placement = default;
            if (token == null || database == null) return false;
            if (offset < 0 || offset + TokenLength > token.Length) return false;

            var key = token.Substring(offset, 3);
            if (!database.TryGet(key, out var posture)) return false;

            var coordinates = token.Substring(offset + 3, 4);
            if (coordinates == OffBoardCoordinates) return false;

            if (!TryDigit(coordinates[0], out var col)) return false;
            if (!TryDigit(coordinates[1], out var rowTens)) return false;
            if (!TryDigit(coordinates[2], out var rowOnes)) return false;
            if (!TryDigit(coordinates[3], out var layer)) return false;

            placement = new Placement(posture, rowTens * 10 + rowOnes, col, layer);
            return true;
        }

        public static Placement Parse(string token, PostureDatabase database)
        {
            if (!TryParse(token, 0, database, out var placement))
                throw new FormatException("配置トークンを解釈できません: " + token);
            return placement;
        }

        private static bool TryDigit(char c, out int value)
        {
            value = c - '0';
            return value >= 0 && value <= 9;
        }

        public bool Equals(Placement other) =>
            ReferenceEquals(Posture, other.Posture) && Row == other.Row && Col == other.Col && Layer == other.Layer;

        public override bool Equals(object obj) => obj is Placement other && Equals(other);

        public override int GetHashCode()
        {
            var hash = Posture == null ? 0 : Posture.Key.GetHashCode();
            return ((hash * 397 ^ Row) * 397 ^ Col) * 397 ^ Layer;
        }

        public override string ToString() => IsValid ? Encode() : "(未設定)";

        public static bool operator ==(Placement a, Placement b) => a.Equals(b);

        public static bool operator !=(Placement a, Placement b) => !a.Equals(b);
    }
}
