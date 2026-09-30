using System;
using System.Collections.Generic;
using System.IO;
using Pentomino.Core;
using UnityEngine;

namespace Pentomino.Data
{
    /// <summary>
    /// 進捗データの保存と読み込み。persistentDataPath 上の JSON ファイル 1 つで管理する。
    /// 2,339 問すべてを保持せず、触れた問題だけを残すのでファイルは小さいままになる。
    /// </summary>
    public sealed class ProgressStore
    {
        public const string FileName = "progress.json";

        private static string _defaultPath;

        /// <summary>保存先の既定パス。テストで一時ファイルに差し替えられるようにしてある。</summary>
        public static string DefaultPath
        {
            get => _defaultPath ?? Path.Combine(Application.persistentDataPath, FileName);
            set => _defaultPath = value;
        }

        private readonly string _path;

        public ProgressStore(string path = null)
        {
            _path = path ?? DefaultPath;
        }

        public string FilePath => _path;

        public GameProgress Load()
        {
            try
            {
                if (!File.Exists(_path)) return new GameProgress();

                var json = File.ReadAllText(_path);
                var progress = JsonUtility.FromJson<GameProgress>(json);
                if (progress == null) return new GameProgress();

                progress.RebuildIndex();
                AdoptVersion1(progress, json);
                AdoptVersion2(progress);
                return progress;
            }
            catch (Exception e)
            {
                // 壊れた保存データでアプリが起動できなくなるのは避け、初期状態に戻す。
                Debug.LogWarning("進捗データを読み込めませんでした。初期状態で開始します: " + e.Message);
                return new GameProgress();
            }
        }

        public bool Save(GameProgress progress)
        {
            if (progress == null) throw new ArgumentNullException(nameof(progress));

            try
            {
                var directory = Path.GetDirectoryName(_path);
                if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);

                // 書き込み中に落ちても既存のデータを壊さないよう、一時ファイル経由で置き換える。
                var temporary = _path + ".tmp";
                File.WriteAllText(temporary, JsonUtility.ToJson(progress));
                if (File.Exists(_path)) File.Delete(_path);
                File.Move(temporary, _path);
                return true;
            }
            catch (Exception e)
            {
                Debug.LogError("進捗データを保存できませんでした: " + e.Message);
                return false;
            }
        }

        /// <summary>
        /// 版 1 の保存データを、いまの形に移す。
        ///
        /// 版 1 は進捗を 1 本しか持っておらず、それが「そのとき選んでいた級」の
        /// 記録だった。版 2 では級ごとに分かれるので、その 1 本を、保存されていた
        /// 級の場所へ入れる。ほかの級は空のまま始まる。
        ///
        /// JsonUtility は知らないキーを黙って捨てるので、そのまま読むと
        /// 版 1 の進捗は消えてしまう。ここで拾い直す。
        /// </summary>
        private static void AdoptVersion1(GameProgress progress, string json)
        {
            if (progress.version >= 2) return;

            // ここでは版 2 までにする。この先は AdoptVersion2 が続けて直す。
            progress.version = 2;

            var old = JsonUtility.FromJson<Version1>(json);
            if (old == null || old.puzzles == null || old.puzzles.Count == 0) return;

            var record = progress.RecordOf(progress.Difficulty);
            record.puzzles = old.puzzles;
            record.lastPuzzle = old.lastPuzzle;
            record.RebuildIndex();

            Debug.Log("版 1 の途中経過を " + progress.Difficulty + " の記録として引き継ぎました（"
                      + old.puzzles.Count + " 問）。");
        }

        /// <summary>
        /// 版 2 の保存データを、いまの形に移す。
        ///
        /// 版 2 では合図の 0 が「端末に任せる」だった。版 3 で 0 は「出さない」に
        /// なり、任せるほうは -1 になった。そのまま読むと、何も指定していない
        /// 人の合図がいきなり無音になる。0 を -1 に読み替える。
        /// </summary>
        private static void AdoptVersion2(GameProgress progress)
        {
            if (progress.version >= 3) return;

            progress.version = GameProgress.CurrentVersion;

            if (progress.vibrationMilliseconds == 0)
                progress.vibrationMilliseconds = SignalScale.DeviceDefault;

            if (progress.vibrationAmplitude == 0)
                progress.vibrationAmplitude = SignalScale.DeviceDefault;
        }

        /// <summary>版 1 の保存データのうち、引き継ぐ部分だけを読むための形。</summary>
        [Serializable]
        private sealed class Version1
        {
            public int lastPuzzle = 1;
            public List<PuzzleProgress> puzzles;
        }

        public void Delete()
        {
            if (File.Exists(_path)) File.Delete(_path);
        }

        /// <summary>セッションの状態を進捗データに書き戻す。</summary>
        public static void Capture(GameProgress progress, PuzzleSession session)
        {
            if (progress == null) throw new ArgumentNullException(nameof(progress));
            if (session == null) throw new ArgumentNullException(nameof(session));

            // 級はセッションから取る。進捗の側の級と食い違っていても、
            // 遊んでいた級の記録に入る。
            var record = progress.RecordOf(session.Difficulty);

            var entry = record.GetOrCreate(session.Puzzle.Number);
            entry.pieces = session.EncodeProgress();
            entry.checkCount = session.CheckCount;
            entry.hintCount = session.HintCount;
            entry.solved = session.IsSolved;
            record.lastPuzzle = session.Puzzle.Number;
        }

        /// <summary>進捗データからセッションの状態を復元する。保存が無ければ出題直後のまま。</summary>
        public static bool Restore(GameProgress progress, PuzzleSession session)
        {
            if (progress == null) throw new ArgumentNullException(nameof(progress));
            if (session == null) throw new ArgumentNullException(nameof(session));

            if (!progress.RecordOf(session.Difficulty).TryGet(session.Puzzle.Number, out var entry))
                return false;

            session.RestoreCounters(entry.checkCount, entry.hintCount);
            return session.TryRestoreProgress(entry.pieces);
        }
    }
}
