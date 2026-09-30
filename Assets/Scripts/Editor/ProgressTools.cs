using Pentomino.Core;
using Pentomino.Data;
using UnityEditor;
using UnityEngine;

namespace Pentomino.EditorTools
{
    /// <summary>進捗データを覗いたり消したりするエディタ用ツール。</summary>
    public static class ProgressTools
    {
        [MenuItem("Pentomino/進捗データをリセット")]
        public static void Reset()
        {
            var store = new ProgressStore();
            if (!EditorUtility.DisplayDialog(
                    "進捗データをリセット",
                    "保存されている進捗をすべて消します。\n\n" + store.FilePath,
                    "消す", "やめる")) return;

            store.Delete();
            Debug.Log("進捗データを削除しました: " + store.FilePath);
        }

        [MenuItem("Pentomino/進捗データを表示")]
        public static void Show()
        {
            var store = new ProgressStore();
            var progress = store.Load();

            var report = new System.Text.StringBuilder();
            report.Append("進捗データ: ").Append(store.FilePath)
                  .Append("\n版: ").Append(progress.version)
                  .Append("\nいまの級: ").Append(progress.Difficulty)
                  .Append("\n言語: ").Append(progress.language);

            // 進捗は級ごとに別々に残るので、級ごとに出す。
            foreach (var difficulty in DifficultyRules.All)
            {
                var record = progress.RecordOf(difficulty);
                report.Append("\n  ").Append(Strings.GradeName(difficulty, Language.English))
                      .Append(": 最後の問題 ").Append(record.lastPuzzle)
                      .Append(" / 記録のある問題 ").Append(record.puzzles.Count)
                      .Append(" / 完成済み ").Append(record.SolvedCount);
            }

            Debug.Log(report.ToString());
        }
    }
}
