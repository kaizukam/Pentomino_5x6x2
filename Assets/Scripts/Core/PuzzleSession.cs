using System;
using System.Collections.Generic;
using System.Text;

namespace Pentomino.Core
{
    /// <summary>待機場所にあるピース 1 つ。姿勢は回転・反転で変化する。</summary>
    public sealed class TrayPiece
    {
        internal TrayPiece(Posture posture)
        {
            Posture = posture;
        }

        public char Piece => Posture.Piece;

        public Posture Posture { get; internal set; }

        public override string ToString() => Posture.Key;
    }

    /// <summary>
    /// 1 問分の進行状態。盤面、待機場所、Check / Hint のカウンタを持つ。
    ///
    /// 問題データは 1 問につき解が 1 通りである前提で、Check と Hint は
    /// Hint_pattern_6X10.csv に記録された解との一致で判定する。
    /// 画面上の「宙に浮いたピース」はビュー側の関心事なので、ここでは
    /// 盤上（固定 / プレイヤー配置）と待機場所の 3 状態だけを扱う。
    /// </summary>
    public sealed class PuzzleSession
    {
        private readonly PostureDatabase _database;
        private readonly Board _board = new Board();
        private readonly List<TrayPiece> _tray = new List<TrayPiece>(Pieces.Count);
        private int _fixedMask;

        public PuzzleSession(Puzzle puzzle, PostureDatabase database, Difficulty difficulty)
        {
            Puzzle = puzzle ?? throw new ArgumentNullException(nameof(puzzle));
            _database = database ?? throw new ArgumentNullException(nameof(database));
            Difficulty = difficulty;
            Reset();
        }

        public Puzzle Puzzle { get; }

        public Difficulty Difficulty { get; }

        public Board Board => _board;

        /// <summary>待機場所のピース。並び順は級に従う（初級=解答順、中級・上級=アルファベット順）。</summary>
        public IReadOnlyList<TrayPiece> Tray => _tray;

        /// <summary>
        /// この問題での Check の減点。0 から始まり、間違いを検出するたびに負へ進む。
        /// 開発仕様では「Check・カウンタから減算」「累積Check回数 負の数」。
        /// </summary>
        public int CheckCount { get; private set; }

        /// <summary>
        /// この問題での Hint の減点。0 から始まり負へ進む。
        /// Hint を使ったときの Check ぶんの減点もこちらに入る。
        /// </summary>
        public int HintCount { get; private set; }

        /// <summary>盤面が埋まったか。埋まった時点で操作は凍結される。</summary>
        public bool IsSolved => _board.IsFull;

        /// <summary>出題時から盤上に固定されていて動かせないピースか。</summary>
        public bool IsFixed(char piece)
        {
            var index = Pieces.IndexOf(piece);
            return index >= 0 && (_fixedMask & (1 << index)) != 0;
        }

        /// <summary>
        /// 出題直後の盤面と待機場所に戻す。
        /// Check / Hint のカウンタはこの問題に紐づく成績なので、ここでは消さない。
        /// </summary>
        public void Reset()
        {
            _board.Clear();
            _tray.Clear();
            _fixedMask = 0;

            foreach (var placement in Puzzle.Prearranged)
            {
                _board.Place(placement);
                _fixedMask |= 1 << Pieces.IndexOf(placement.Piece);
            }

            foreach (var placement in Puzzle.Hidden)
            {
                var posture = Difficulty.UsesAnswerPosture()
                    ? placement.Posture
                    : _database.DefaultPosture(placement.Piece);
                _tray.Add(new TrayPiece(posture));
            }

            SortTray();
        }

        /// <summary>Check / Hint のカウンタも含めて初期化する（パズル全体のリセット時のみ）。</summary>
        public void ResetIncludingCounters()
        {
            Reset();
            CheckCount = 0;
            HintCount = 0;
        }

        public bool TryGetTrayPiece(char piece, out TrayPiece trayPiece)
        {
            foreach (var candidate in _tray)
            {
                if (candidate.Piece != piece) continue;
                trayPiece = candidate;
                return true;
            }
            trayPiece = null;
            return false;
        }

        /// <summary>待機場所のピースを、現在の姿勢のまま盤上の (row, col) に置く。</summary>
        public bool TryPlace(char piece, int row, int col)
        {
            if (IsSolved) return false;
            if (!TryGetTrayPiece(piece, out var trayPiece)) return false;

            var placement = new Placement(trayPiece.Posture, row, col);
            if (!_board.TryPlace(placement)) return false;

            _tray.Remove(trayPiece);
            return true;
        }

        /// <summary>その姿勢・座標に置けるか（スナップ判定に使う）。</summary>
        public bool CanPlace(char piece, Posture posture, int row, int col)
        {
            if (IsSolved || posture == null || posture.Piece != piece) return false;
            if (IsFixed(piece)) return false;
            return _board.CanPlace(new Placement(posture, row, col));
        }

        /// <summary>盤上のピースを待機場所に戻す。固定ピースは戻せない。</summary>
        public bool TryPickUp(char piece)
        {
            if (IsSolved) return false;
            if (IsFixed(piece)) return false;
            if (!_board.TryGetPlacement(piece, out var placement)) return false;

            _board.Remove(piece);
            AddToTray(new TrayPiece(placement.Posture));
            return true;
        }

        /// <summary>待機場所のピースを反時計回りに 90 度回す（軽いタップ）。</summary>
        public Posture RotateCcw(char piece) => Transform(piece, p => p.RotatedCcw);

        /// <summary>待機場所のピースを時計回りに 90 度回す。</summary>
        public Posture RotateCw(char piece) => Transform(piece, p => p.RotatedCw);

        /// <summary>待機場所のピースを左右反転する（横向きのフリック）。</summary>
        public Posture FlipHorizontally(char piece) => Transform(piece, p => p.FlippedHorizontally);

        /// <summary>待機場所のピースを上下反転する（縦向きのフリック）。</summary>
        public Posture FlipVertically(char piece) => Transform(piece, p => p.FlippedVertically);

        private Posture Transform(char piece, Func<Posture, Posture> transform)
        {
            if (IsSolved) return null;
            if (!TryGetTrayPiece(piece, out var trayPiece)) return null;
            trayPiece.Posture = transform(trayPiece.Posture);
            return trayPiece.Posture;
        }

        /// <summary>
        /// Check ボタン。間違ったピースを待機場所に戻し、その個数を Check カウンタに加算する。
        /// 戻り値は間違っていたピース数。
        /// </summary>
        public int Check()
        {
            if (IsSolved) return 0;
            var wrong = FindWrongPieces();
            ReturnAll(wrong);
            CheckCount -= wrong.Count;   // 間違いの数だけ減点
            return wrong.Count;
        }

        /// <summary>
        /// Hint ボタン。まず Check を行い（ペナルティは Hint カウンタへ）、
        /// 続いて空いているマスをひとつ埋めて Hint カウンタを 1 減算する。
        ///
        /// 埋める場所は Answer を先頭から走査して最初に見つかる未収納ピース。
        /// Answer は列優先で、画面では左上のマスが先頭にあたるので、
        /// 左上から下へ、その列が埋まったら右の列へ、という順に空白が埋まっていく。
        /// 待機場所の並び順（級によって解答順だったりアルファベット順だったりする）には
        /// 左右されない。
        /// </summary>
        public bool Hint()
        {
            if (IsSolved) return false;

            var wrong = FindWrongPieces();
            ReturnAll(wrong);
            HintCount -= wrong.Count;   // Check ぶんの減点は Hint 側へ

            if (!TryFindNextHint(out var placement)) return false;
            if (!_board.CanPlace(placement)) return false;

            if (!TryGetTrayPiece(placement.Piece, out var trayPiece)) return false;
            _tray.Remove(trayPiece);

            _board.Place(placement);
            HintCount -= 1;
            return true;
        }

        /// <summary>
        /// Answer の末尾から遡って、まだ盤に無い最初のピースの正解の置き方を返す。
        ///
        /// 先頭から埋めると、左上から順に固まっていくだけで、難しさがそのまま下がる。
        /// 末尾から埋めると、盤の離れた場所に置かれることが多く、残りの形を
        /// 読み直す必要が出る。同じ 1 手でも、ひねりが残る。
        /// </summary>
        public bool TryFindNextHint(out Placement placement)
        {
            foreach (var solved in Puzzle.HiddenReversed)
            {
                if (_board.IsPlaced(solved.Piece)) continue;

                placement = solved;
                return true;
            }

            placement = default;
            return false;
        }

        /// <summary>Check を実行せずに、いま解と一致していないピースを調べる。</summary>
        public IReadOnlyList<char> FindWrongPieces()
        {
            var wrong = new List<char>();
            foreach (var piece in _board.PlacedPieces)
            {
                if (IsFixed(piece)) continue;
                _board.TryGetPlacement(piece, out var placement);
                if (placement != Puzzle.SolutionFor(piece)) wrong.Add(piece);
            }
            return wrong;
        }

        /// <summary>この問題の進捗を 7 文字 x (隠しピース数) の文字列に符号化する。</summary>
        public string EncodeProgress()
        {
            var sb = new StringBuilder(Puzzle.Level * Placement.TokenLength);
            foreach (var solved in Puzzle.Hidden)
            {
                var piece = solved.Piece;
                if (_board.TryGetPlacement(piece, out var placement))
                {
                    sb.Append(placement.Encode());
                }
                else
                {
                    TryGetTrayPiece(piece, out var trayPiece);
                    sb.Append(trayPiece != null ? trayPiece.Posture.Key : _database.DefaultPosture(piece).Key);
                    sb.Append(Placement.OffBoardCoordinates);
                }
            }
            return sb.ToString();
        }

        /// <summary>EncodeProgress で作った文字列から状態を復元する。書式が壊れていれば false。</summary>
        public bool TryRestoreProgress(string encoded)
        {
            if (string.IsNullOrEmpty(encoded)) return false;
            if (encoded.Length != Puzzle.Level * Placement.TokenLength) return false;

            var postures = new Posture[Puzzle.Level];
            var placements = new Placement[Puzzle.Level];
            var scratch = Puzzle.BuildInitialBoard();
            var index = 0;

            foreach (var solved in Puzzle.Hidden)
            {
                var offset = index * Placement.TokenLength;
                var key = encoded.Substring(offset, 3);
                if (!_database.TryGet(key, out var posture)) return false;
                if (posture.Piece != solved.Piece) return false;
                postures[index] = posture;

                if (encoded.Substring(offset + 3, 4) != Placement.OffBoardCoordinates)
                {
                    // 盤上にあるピース。座標が壊れていたり重なっていたら復元しない。
                    if (!Placement.TryParse(encoded, offset, _database, out var placement)) return false;
                    if (!scratch.TryPlace(placement)) return false;
                    placements[index] = placement;
                }

                index++;
            }

            // ここまで検証できたので、実際の状態を作り直す。
            Reset();

            index = 0;
            foreach (var solved in Puzzle.Hidden)
            {
                var piece = solved.Piece;
                if (placements[index].IsValid)
                {
                    _board.Place(placements[index]);
                    if (TryGetTrayPiece(piece, out var placed)) _tray.Remove(placed);
                }
                else if (TryGetTrayPiece(piece, out var waiting))
                {
                    waiting.Posture = postures[index];
                }
                index++;
            }

            SortTray();
            return true;
        }

        /// <summary>保存済みの減点を復元する。負の値をそのまま受け取る。</summary>
        public void RestoreCounters(int checkCount, int hintCount)
        {
            CheckCount = Math.Min(0, checkCount);
            HintCount = Math.Min(0, hintCount);
        }

        private void ReturnAll(IReadOnlyList<char> pieces)
        {
            foreach (var piece in pieces)
            {
                if (!_board.TryGetPlacement(piece, out var placement)) continue;
                _board.Remove(piece);
                // 姿勢はそのまま保ち、置かれる場所だけ級の並び順に従わせる。
                AddToTray(new TrayPiece(placement.Posture));
            }
        }

        private void AddToTray(TrayPiece trayPiece)
        {
            _tray.Add(trayPiece);
            SortTray();
        }

        private void SortTray()
        {
            _tray.Sort((a, b) => TrayOrderKey(a.Piece).CompareTo(TrayOrderKey(b.Piece)));
        }

        /// <summary>待機場所の並び順のキー。初級は解答順、中級・上級はアルファベット順。</summary>
        private int TrayOrderKey(char piece)
        {
            if (!Difficulty.UsesAnswerOrder()) return Pieces.IndexOf(piece);

            var solution = Puzzle.Solution;
            for (var i = 0; i < solution.Count; i++)
                if (solution[i].Piece == piece) return i;
            return Pieces.Count + Pieces.IndexOf(piece);
        }
    }
}
