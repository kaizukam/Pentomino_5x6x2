using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using Pentomino.Core;
using Pentomino.Data;
using UnityEditor;
using Debug = UnityEngine.Debug;

namespace Pentomino.EditorTools
{
    /// <summary>
    /// Resources/Data の問題データを検証するエディタ用ツール。
    ///
    /// ゲーム側は「1 問につき解は 1 通り」を前提に Check / Hint を判定するので、
    /// 問題データを作り直したら必ずこの検証を通すこと。
    /// </summary>
    public static class PuzzleDataValidator
    {
        [MenuItem("Pentomino/問題データを検証")]
        public static void Validate()
        {
            GameData.Unload();

            var stopwatch = Stopwatch.StartNew();
            var database = GameData.Postures;
            var library = GameData.Puzzles;
            var solver = new PentominoSolver(database);

            var report = new StringBuilder();
            report.AppendLine("Pentomino 問題データ検証");
            report.AppendLine("姿勢: " + database.Count + " 種");
            report.AppendLine("問題: " + library.Count + " 問");

            var brokenSolutions = new List<string>();
            var multipleSolutions = new List<string>();
            var noSolution = new List<string>();
            var byLevel = new int[Pieces.Count + 1];
            var multipleByLevel = new int[Pieces.Count + 1];

            try
            {
                for (var i = 0; i < library.Count; i++)
                {
                    var puzzle = library.All[i];
                    byLevel[puzzle.Level]++;

                    if (!puzzle.BuildSolvedBoard().IsFull)
                    {
                        brokenSolutions.Add(puzzle.Id);
                        continue;
                    }

                    var board = puzzle.BuildInitialBoard();
                    var missing = new List<char>();
                    foreach (var placement in puzzle.Hidden) missing.Add(placement.Piece);

                    var count = solver.CountCompletions(board, missing, 2);
                    if (count == 0) noSolution.Add(puzzle.Id);
                    else if (count > 1)
                    {
                        multipleSolutions.Add(puzzle.Id);
                        multipleByLevel[puzzle.Level]++;
                    }

                    if (i % 50 == 0 &&
                        EditorUtility.DisplayCancelableProgressBar("問題データを検証", puzzle.Id, (float)i / library.Count))
                    {
                        report.AppendLine("※ 途中で中止しました（" + i + " 問まで）");
                        break;
                    }
                }
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }

            report.AppendLine();
            report.AppendLine("Lvl | 問題数 | 複数解");
            for (var level = 1; level <= Pieces.Count; level++)
            {
                if (byLevel[level] == 0) continue;
                report.AppendLine(string.Format("{0,3} | {1,6} | {2,6}", level, byLevel[level], multipleByLevel[level]));
            }

            report.AppendLine();
            report.AppendLine("解が壊れている問題: " + brokenSolutions.Count);
            report.AppendLine("解が存在しない問題: " + noSolution.Count);
            report.AppendLine("解が複数ある問題: " + multipleSolutions.Count);
            report.AppendLine("所要時間: " + stopwatch.ElapsedMilliseconds + " ms");

            AppendSamples(report, "壊れている", brokenSolutions);
            AppendSamples(report, "解なし", noSolution);
            AppendSamples(report, "複数解", multipleSolutions);

            var failed = brokenSolutions.Count + noSolution.Count + multipleSolutions.Count;
            if (failed == 0) Debug.Log(report.ToString() + "\n単一解の問題集として問題ありません。");
            else Debug.LogWarning(report.ToString() + "\nゲームは単一解を前提にしているため、上記は修正が必要です。");
        }

        private static void AppendSamples(StringBuilder report, string label, List<string> ids)
        {
            if (ids.Count == 0) return;

            report.Append(label + " (先頭 20 件): ");
            for (var i = 0; i < ids.Count && i < 20; i++)
            {
                if (i > 0) report.Append(", ");
                report.Append(ids[i]);
            }
            if (ids.Count > 20) report.Append(" ... 他 " + (ids.Count - 20) + " 件");
            report.AppendLine();
        }
    }
}
