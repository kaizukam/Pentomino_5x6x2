using System;
using System.Collections.Generic;
using System.Globalization;

namespace Pentomino.Core
{
    /// <summary>Hint_pattern_6X10.csv を読み込んだ 2,339 問の一覧。難易度の軽い順に並んでいる。</summary>
    public sealed class PuzzleLibrary
    {
        private readonly Puzzle[] _puzzles;

        private PuzzleLibrary(Puzzle[] puzzles)
        {
            _puzzles = puzzles;
        }

        public int Count => _puzzles.Length;

        public int FirstNumber => 1;

        public int LastNumber => _puzzles.Length;

        /// <summary>問題番号 1..Count で取得する。</summary>
        public Puzzle this[int number] => Get(number);

        public IReadOnlyList<Puzzle> All => _puzzles;

        /// <summary>いちばん大きいレベル番号。</summary>
        public int MaxLevel
        {
            get
            {
                var max = 0;
                foreach (var puzzle in _puzzles)
                    if (puzzle.Level > max) max = puzzle.Level;
                return max;
            }
        }

        /// <summary>
        /// レベルごとの問題数。索引がレベル番号（0 番は使わない）。
        /// 問題データを入れ替えると分布が変わるので、必ずここから数える。
        /// </summary>
        public int[] CountsByLevel()
        {
            var counts = new int[MaxLevel + 1];
            foreach (var puzzle in _puzzles)
            {
                if (puzzle.Level < 0 || puzzle.Level >= counts.Length) continue;
                counts[puzzle.Level]++;
            }
            return counts;
        }

        public Puzzle Get(int number)
        {
            if (number < 1 || number > _puzzles.Length)
                throw new ArgumentOutOfRangeException(nameof(number), "問題番号は 1.." + _puzzles.Length + " の範囲です: " + number);
            return _puzzles[number - 1];
        }

        /// <summary>指定した数だけ進んだ／戻った問題番号。両端で丸める。</summary>
        public int Offset(int number, int delta)
        {
            var next = number + delta;
            if (next < 1) return 1;
            if (next > _puzzles.Length) return _puzzles.Length;
            return next;
        }

        public static PuzzleLibrary FromCsv(string csv, PostureDatabase database)
        {
            if (csv == null) throw new ArgumentNullException(nameof(csv));
            if (database == null) throw new ArgumentNullException(nameof(database));

            var puzzles = new List<Puzzle>(2339);
            var lines = csv.Split('\n');
            var headerChecked = false;

            for (var lineNumber = 0; lineNumber < lines.Length; lineNumber++)
            {
                var line = lines[lineNumber].Trim('\r', ' ', '\t', '\uFEFF');
                if (line.Length == 0) continue;
                var fields = line.Split(',');
                if (fields.Length < 3)
                    throw new FormatException("列が足りません (" + (lineNumber + 1) + " 行目): " + line);

                // ヘッダ行は 1 列目が番号として読めないことで見分ける。
                // 見出しは "ID" だったり "Group number" だったりするので、名前では判定しない。
                if (!headerChecked)
                {
                    headerChecked = true;
                    if (!TryParseNumber(fields[0], out _)) continue;
                }

                var puzzle = ParseRow(fields[0], fields[1], fields[2], database, lineNumber + 1);
                if (puzzle.Number != puzzles.Count + 1)
                    throw new FormatException("問題番号が連番ではありません (" + (lineNumber + 1) + " 行目): " + fields[0]);
                puzzles.Add(puzzle);
            }

            if (puzzles.Count == 0) throw new FormatException("問題が 1 件も読み込めませんでした。");
            return new PuzzleLibrary(puzzles.ToArray());
        }

        private static Puzzle ParseRow(string id, string level, string answer, PostureDatabase database, int lineNumber)
        {
            if (!TryParseNumber(id, out var number))
                throw new FormatException("問題番号を解釈できません (" + lineNumber + " 行目): " + id);

            if (!int.TryParse(level.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var lvl))
                throw new FormatException("Lvl を解釈できません (" + lineNumber + " 行目): " + level);

            answer = answer.Trim();
            var expected = Pieces.Count * Placement.TokenLength;
            if (answer.Length != expected)
                throw new FormatException("Answer は " + expected + " 文字必要です (" + lineNumber + " 行目): " + answer.Length + " 文字");

            var solution = new Placement[Pieces.Count];
            var seen = 0;
            for (var i = 0; i < Pieces.Count; i++)
            {
                if (!Placement.TryParse(answer, i * Placement.TokenLength, database, out var placement))
                    throw new FormatException("配置を解釈できません (" + lineNumber + " 行目, " + (i + 1) + " 個目)");

                var bit = 1 << Pieces.IndexOf(placement.Piece);
                if ((seen & bit) != 0)
                    throw new FormatException("ピース " + placement.Piece + " が重複しています (" + lineNumber + " 行目)");
                seen |= bit;
                solution[i] = placement;
            }

            return new Puzzle(number, lvl, solution);
        }

        /// <summary>問題番号を読む。"#0001" と "1" のどちらの書き方にも対応する。</summary>
        private static bool TryParseNumber(string field, out int number)
        {
            number = 0;
            if (field == null) return false;

            field = field.Trim('\uFEFF', ' ', '\t', '\r');
            if (field.Length > 0 && field[0] == '#') field = field.Substring(1);
            if (field.Length == 0) return false;

            return int.TryParse(field, NumberStyles.Integer, CultureInfo.InvariantCulture, out number);
        }
    }
}
