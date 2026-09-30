using System;
using System.Collections.Generic;

namespace Pentomino.Core
{
    /// <summary>1 問分の進捗。JsonUtility で読み書きできるよう public フィールドで持つ。</summary>
    [Serializable]
    public sealed class PuzzleProgress
    {
        /// <summary>問題番号 1..2339。</summary>
        public int number;

        /// <summary>
        /// 未収納ピースの状況。7 文字 x Lvl 個。
        /// 盤上なら 姿勢キー(3) + 行(1) + 列(2) + Z(1)、盤外なら 姿勢キー(3) + "9999"。
        /// </summary>
        public string pieces;

        /// <summary>Check の減点累計（負の数）。パズル全体のリセットでしか 0 に戻らない。</summary>
        public int checkCount;

        /// <summary>Hint の減点累計（負の数）。パズル全体のリセットでしか 0 に戻らない。</summary>
        public int hintCount;

        /// <summary>完成済みか。完成するとこの問題の状態は凍結される。</summary>
        public bool solved;
    }

    /// <summary>
    /// ひとつの級ぶんの途中経過。
    ///
    /// 級を変えると進捗が全部消えていたので、テスターが級を試すたびに
    /// それまでの記録を失っていた。級ごとに別の場所へ残せば、
    /// 行き来しても消えない。消したいときは、その級だけを消す。
    /// </summary>
    [Serializable]
    public sealed class DifficultyRecord
    {
        /// <summary>どの級の記録か。<see cref="Difficulty"/> の数値。</summary>
        public int difficulty;

        /// <summary>この級で最後に開いていた問題番号。</summary>
        public int lastPuzzle = 1;

        public List<PuzzleProgress> puzzles = new List<PuzzleProgress>();

        [NonSerialized] private Dictionary<int, PuzzleProgress> _index;

        public Difficulty Difficulty => (Difficulty)difficulty;

        public bool TryGet(int number, out PuzzleProgress progress) =>
            Index.TryGetValue(number, out progress);

        public PuzzleProgress GetOrCreate(int number)
        {
            if (Index.TryGetValue(number, out var progress)) return progress;

            progress = new PuzzleProgress { number = number };
            puzzles.Add(progress);
            Index[number] = progress;
            return progress;
        }

        /// <summary>この級の記録だけを消す。</summary>
        public void Clear()
        {
            puzzles.Clear();
            _index = null;
            lastPuzzle = 1;
        }

        /// <summary>解いた問題数。</summary>
        public int SolvedCount
        {
            get
            {
                var count = 0;
                foreach (var p in puzzles) if (p.solved) count++;
                return count;
            }
        }

        /// <summary>読み込み直後に呼び、内部索引を張り直す。</summary>
        public void RebuildIndex()
        {
            _index = null;
            if (puzzles == null) puzzles = new List<PuzzleProgress>();
            if (lastPuzzle < 1) lastPuzzle = 1;
        }

        private Dictionary<int, PuzzleProgress> Index
        {
            get
            {
                if (_index != null) return _index;

                _index = new Dictionary<int, PuzzleProgress>(puzzles.Count);
                foreach (var p in puzzles) _index[p.number] = p;
                return _index;
            }
        }
    }

    /// <summary>基本変数と、級ごとの進捗。触れていない問題は保持しない。</summary>
    [Serializable]
    public sealed class GameProgress
    {
        /// <summary>
        /// 保存データの版。
        ///
        ///   1: 進捗が 1 本だけ（puzzles / lastPuzzle）
        ///   2: 級ごとに 1 本ずつ（records）
        ///   3: 合図の 0 が「端末に任せる」から「出さない」に変わった
        ///
        /// 古い版の読み込みは <see cref="Pentomino.Data.ProgressStore"/> が受け持つ。
        /// </summary>
        public const int CurrentVersion = 3;

        public int version = CurrentVersion;

        /// <summary>いま選んでいる級。既定は上級。変えても進捗は消えない。</summary>
        public int difficulty = (int)Core.Difficulty.Classic;

        /// <summary>言語コード（ja / en / es / fr / de）。</summary>
        public string language = "ja";

        /// <summary>
        /// 音を鳴らすか。設定の音ボタンで切り替える。
        /// 切ると、はまったときの音も、完成したときの音も鳴らない。
        /// </summary>
        public bool sound = true;

        /// <summary>級ごとの記録。触れた級だけが並ぶ。</summary>
        public List<DifficultyRecord> records = new List<DifficultyRecord>();

        // 反転の感度。実機でないと詰められないので保存する。
        //
        // 名前を作り直したので、古い保存データにあった値は読まれず、既定に戻る。
        // 実機で詰め直す前提の値なので、引き継ぐ意味も無い。
        public float flickReleaseSpeed = FlickSettings.DefaultReleaseSpeedMmPerSecond;
        public float flickFilter = FlickSettings.DefaultFilter;
        public float flickStillSeconds = FlickSettings.DefaultStillSeconds;
        public float flickMinTravelMm = FlickSettings.DefaultMinTravelMm;
        public float flickAxisRatio = FlickSettings.DefaultAxisRatio;

        // はまったときの振動。実機ごとに感じ方が大きく違うので、こちらも保存する。
        public int vibrationMilliseconds = FlickSettings.DefaultVibrationMilliseconds;
        public int vibrationAmplitude = FlickSettings.DefaultVibrationAmplitude;

        [NonSerialized] private FlickSettings _flick;

        /// <summary>
        /// フリックの判定に使うしきい値。読み書きは保存用の数値と結びついている。
        /// </summary>
        public FlickSettings Flick
        {
            get
            {
                if (_flick == null) _flick = new FlickSettings();

                _flick.ReleaseSpeedMmPerSecond = flickReleaseSpeed;
                _flick.Filter = flickFilter;
                _flick.StillSeconds = flickStillSeconds;
                _flick.MinTravelMm = flickMinTravelMm;
                _flick.AxisRatio = flickAxisRatio;
                _flick.VibrationMilliseconds = vibrationMilliseconds;
                _flick.VibrationAmplitude = vibrationAmplitude;
                _flick.Clamp();
                return _flick;
            }
        }

        /// <summary>感度を書き換える。値は使える範囲に丸められる。</summary>
        public void SetFlick(FlickSettings settings)
        {
            if (settings == null) return;

            var copy = settings.Clone();
            copy.Clamp();

            flickReleaseSpeed = copy.ReleaseSpeedMmPerSecond;
            flickFilter = copy.Filter;
            flickStillSeconds = copy.StillSeconds;
            flickMinTravelMm = copy.MinTravelMm;
            flickAxisRatio = copy.AxisRatio;
            vibrationMilliseconds = copy.VibrationMilliseconds;
            vibrationAmplitude = copy.VibrationAmplitude;
        }

        /// <summary>
        /// いま選んでいる級。変えても進捗は消えない（級ごとに別に残っている）。
        /// </summary>
        public Difficulty Difficulty
        {
            get => (Difficulty)difficulty;
            set => difficulty = (int)value;
        }

        public Language Language
        {
            get => Languages.FromCode(language);
            set => language = value.ToCode();
        }

        /// <summary>いま選んでいる級の記録。無ければその場で作る。</summary>
        public DifficultyRecord Current => RecordOf(Difficulty);

        /// <summary>指定した級の記録。無ければその場で作る。</summary>
        public DifficultyRecord RecordOf(Difficulty wanted)
        {
            if (records == null) records = new List<DifficultyRecord>();

            foreach (var record in records)
                if (record.difficulty == (int)wanted) return record;

            var made = new DifficultyRecord { difficulty = (int)wanted };
            records.Add(made);
            return made;
        }

        /// <summary>いまの級で最後に開いていた問題番号。</summary>
        public int LastPuzzle
        {
            get => Current.lastPuzzle;
            set => Current.lastPuzzle = value;
        }

        public bool TryGet(int number, out PuzzleProgress progress) => Current.TryGet(number, out progress);

        public PuzzleProgress GetOrCreate(int number) => Current.GetOrCreate(number);

        /// <summary>いまの級の進捗だけを消す。級と言語は残す。</summary>
        public void ClearProgress() => Current.Clear();

        /// <summary>指定した級の進捗だけを消す。ほかの級には触らない。</summary>
        public void ClearProgress(Difficulty wanted) => RecordOf(wanted).Clear();

        /// <summary>いまの級で解いた問題数。</summary>
        public int SolvedCount => Current.SolvedCount;

        /// <summary>指定した級で解いた問題数。</summary>
        public int SolvedCountOf(Difficulty wanted) => RecordOf(wanted).SolvedCount;

        /// <summary>JsonUtility での読み込み直後に呼び、内部索引を張り直す。</summary>
        public void RebuildIndex()
        {
            if (records == null) records = new List<DifficultyRecord>();
            foreach (var record in records) record.RebuildIndex();
        }
    }
}
