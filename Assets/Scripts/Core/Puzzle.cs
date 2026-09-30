using System;
using System.Collections.Generic;

namespace Pentomino.Core
{
    /// <summary>
    /// Hint_pattern_5x6x2.csv の 1 行。12 ピース分の解と、そのうち何個を隠すか（Lvl）を持つ。
    /// Answer は探索順（Board を参照）に並んでいるため、先頭から (12 - Lvl) 個が出題時に置かれている。
    /// </summary>
    public sealed class Puzzle
    {
        private readonly Placement[] _solution;

        public Puzzle(int number, int level, IReadOnlyList<Placement> solution)
        {
            if (number < 1) throw new ArgumentOutOfRangeException(nameof(number));
            if (solution == null) throw new ArgumentNullException(nameof(solution));
            if (solution.Count != Pieces.Count)
                throw new ArgumentException("解は 12 ピース必要です。", nameof(solution));
            if (level < 1 || level > Pieces.Count)
                throw new ArgumentOutOfRangeException(nameof(level), "Lvl は 1..12 の範囲です: " + level);

            Number = number;
            Level = level;
            _solution = new Placement[Pieces.Count];
            for (var i = 0; i < solution.Count; i++) _solution[i] = solution[i];
        }

        /// <summary>問題番号 1..264。</summary>
        public int Number { get; }

        /// <summary>"$0108" 形式の表示用 ID。問題データの書き方に合わせる。</summary>
        public string Id => "$" + Number.ToString("0000");

        /// <summary>難易度 Lvl。出題時に隠されているピース数と一致する。</summary>
        public int Level { get; }

        /// <summary>出題時に既に置かれているピース数。</summary>
        public int PrearrangedCount => Pieces.Count - Level;

        /// <summary>12 ピースの解。Answer 文字列の並び順（探索順）。</summary>
        public IReadOnlyList<Placement> Solution => _solution;

        /// <summary>出題時に盤上に固定されているピース。動かせない。</summary>
        public IEnumerable<Placement> Prearranged
        {
            get
            {
                for (var i = 0; i < PrearrangedCount; i++) yield return _solution[i];
            }
        }

        /// <summary>出題時に待機場所にあるピース。Answer の並び順（初級の待機列の並び順）。</summary>
        public IEnumerable<Placement> Hidden
        {
            get
            {
                for (var i = PrearrangedCount; i < _solution.Length; i++) yield return _solution[i];
            }
        }

        /// <summary>
        /// 出題時に待機場所にあるピースを、Answer の末尾から遡って返す。
        ///
        /// Hint がこちらを使う。先頭から埋めると左上から順に固まっていくだけで、
        /// 難しさがそのまま下がる。末尾から埋めれば盤の離れた場所に置かれ、
        /// 残りの形を読み直す必要が出る。
        /// </summary>
        public IEnumerable<Placement> HiddenReversed
        {
            get
            {
                for (var i = _solution.Length - 1; i >= PrearrangedCount; i--)
                    yield return _solution[i];
            }
        }

        public bool IsPrearranged(char piece)
        {
            for (var i = 0; i < PrearrangedCount; i++)
                if (_solution[i].Piece == piece) return true;
            return false;
        }

        /// <summary>そのピースの正解配置。</summary>
        public Placement SolutionFor(char piece)
        {
            foreach (var placement in _solution)
                if (placement.Piece == piece) return placement;
            throw new ArgumentException("この問題に含まれないピースです: " + piece, nameof(piece));
        }

        /// <summary>解を並べた 84 文字の Answer 文字列を復元する。</summary>
        public string EncodeAnswer()
        {
            var chars = new char[Pieces.Count * Placement.TokenLength];
            var offset = 0;
            foreach (var placement in _solution)
            {
                placement.Encode().CopyTo(0, chars, offset, Placement.TokenLength);
                offset += Placement.TokenLength;
            }
            return new string(chars);
        }

        /// <summary>解を全て並べた盤面を作る。</summary>
        public Board BuildSolvedBoard()
        {
            var board = new Board();
            foreach (var placement in _solution) board.Place(placement);
            return board;
        }

        /// <summary>出題時の盤面（固定ピースのみ）を作る。</summary>
        public Board BuildInitialBoard()
        {
            var board = new Board();
            foreach (var placement in Prearranged) board.Place(placement);
            return board;
        }

        public override string ToString() => Id + " L" + Level;
    }
}
