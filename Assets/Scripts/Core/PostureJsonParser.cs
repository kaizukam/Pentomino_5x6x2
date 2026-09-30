using System;
using System.Collections.Generic;
using System.Globalization;

namespace Pentomino.Core
{
    /// <summary>
    /// Posture_DB.json 専用の軽量パーサ。
    /// 形式は { "F00": [[d0,d1,d2], ...], ... } に限定される。
    /// JsonUtility は Dictionary と多次元配列を扱えないため自前で読む。
    /// </summary>
    internal static class PostureJsonParser
    {
        public static Dictionary<string, List<Cell>> Parse(string json)
        {
            if (json == null) throw new ArgumentNullException(nameof(json));

            var result = new Dictionary<string, List<Cell>>(StringComparer.Ordinal);
            var i = 0;

            SkipWhitespace(json, ref i);
            Expect(json, ref i, '{');
            SkipWhitespace(json, ref i);
            if (Peek(json, i) == '}') return result;

            while (true)
            {
                SkipWhitespace(json, ref i);
                var key = ReadString(json, ref i);
                SkipWhitespace(json, ref i);
                Expect(json, ref i, ':');
                var cells = ReadCellArray(json, ref i);
                if (result.ContainsKey(key)) throw Error(i, "姿勢キーが重複しています: " + key);
                result[key] = cells;

                SkipWhitespace(json, ref i);
                var c = Read(json, ref i);
                if (c == ',') continue;
                if (c == '}') break;
                throw Error(i, "',' または '}' が必要です");
            }

            return result;
        }

        private static List<Cell> ReadCellArray(string s, ref int i)
        {
            SkipWhitespace(s, ref i);
            Expect(s, ref i, '[');
            var cells = new List<Cell>(Pieces.CellsPerPiece);

            SkipWhitespace(s, ref i);
            if (Peek(s, i) == ']')
            {
                i++;
                return cells;
            }

            while (true)
            {
                cells.Add(ReadCell(s, ref i));
                SkipWhitespace(s, ref i);
                var c = Read(s, ref i);
                if (c == ',') continue;
                if (c == ']') break;
                throw Error(i, "',' または ']' が必要です");
            }

            return cells;
        }

        private static Cell ReadCell(string s, ref int i)
        {
            SkipWhitespace(s, ref i);
            Expect(s, ref i, '[');

            var values = new List<int>(3);
            while (true)
            {
                SkipWhitespace(s, ref i);
                values.Add(ReadInt(s, ref i));
                SkipWhitespace(s, ref i);
                var c = Read(s, ref i);
                if (c == ',') continue;
                if (c == ']') break;
                throw Error(i, "',' または ']' が必要です");
            }

            if (values.Count < 2) throw Error(i, "座標は少なくとも 2 要素必要です");
            // values[2] は立体版用の Z 座標。6X10 の平面版では常に 0 なので読み捨てる。
            if (values.Count >= 3 && values[2] != 0) throw Error(i, "平面 6X10 では Z 座標は 0 である必要があります");
            return new Cell(values[0], values[1]);
        }

        private static int ReadInt(string s, ref int i)
        {
            var start = i;
            if (i < s.Length && (s[i] == '-' || s[i] == '+')) i++;
            while (i < s.Length && s[i] >= '0' && s[i] <= '9') i++;
            if (i == start) throw Error(i, "数値が必要です");
            return int.Parse(s.Substring(start, i - start), CultureInfo.InvariantCulture);
        }

        private static string ReadString(string s, ref int i)
        {
            Expect(s, ref i, '"');
            var start = i;
            while (i < s.Length && s[i] != '"')
            {
                if (s[i] == '\\') throw Error(i, "エスケープ文字には対応していません");
                i++;
            }
            if (i >= s.Length) throw Error(i, "文字列が閉じられていません");
            var value = s.Substring(start, i - start);
            i++;
            return value;
        }

        private static void SkipWhitespace(string s, ref int i)
        {
            while (i < s.Length && char.IsWhiteSpace(s[i])) i++;
        }

        private static char Peek(string s, int i) => i < s.Length ? s[i] : '\0';

        private static char Read(string s, ref int i)
        {
            if (i >= s.Length) throw Error(i, "入力が途中で終わっています");
            return s[i++];
        }

        private static void Expect(string s, ref int i, char expected)
        {
            var c = Read(s, ref i);
            if (c != expected) throw Error(i, "'" + expected + "' が必要ですが '" + c + "' でした");
        }

        private static FormatException Error(int position, string message) =>
            new FormatException("Posture_DB.json の解析に失敗しました (位置 " + position + "): " + message);
    }
}
