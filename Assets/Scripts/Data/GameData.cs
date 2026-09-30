using System;
using Pentomino.Core;
using UnityEngine;

namespace Pentomino.Data
{
    /// <summary>
    /// Resources/Data 以下の入力データを読み込み、アプリ全体で共有する。
    /// 姿勢 DB と問題集は不変なので一度だけ読み込む。
    /// </summary>
    public static class GameData
    {
        public const string PostureDatabasePath = "Data/Posture_DB";
        public const string PuzzleLibraryPath = "Data/Hint_pattern_5x6x2";

        private static PostureDatabase _postures;
        private static PuzzleLibrary _puzzles;
        private static PentominoSolver _solver;

        public static PostureDatabase Postures
        {
            get
            {
                if (_postures == null) _postures = PostureDatabase.FromJson(LoadText(PostureDatabasePath));
                return _postures;
            }
        }

        public static PuzzleLibrary Puzzles
        {
            get
            {
                if (_puzzles == null) _puzzles = PuzzleLibrary.FromCsv(LoadText(PuzzleLibraryPath), Postures);
                return _puzzles;
            }
        }

        /// <summary>使い回し可能なソルバ。姿勢テーブルの構築を毎回やらずに済む。</summary>
        public static PentominoSolver Solver
        {
            get
            {
                if (_solver == null) _solver = new PentominoSolver(Postures);
                return _solver;
            }
        }

        /// <summary>
        /// 説明書の本文を読み込んで組み立てる。
        /// その言語の雛形が無ければ英語で代用する。
        /// </summary>
        public static string LoadManual(Language language)
        {
            var template = LoadManualTemplate(language);
            if (template == null && language != Language.English)
                template = LoadManualTemplate(Language.English);

            if (template == null)
            {
                Debug.LogWarning("説明書の本文が見つかりません: Resources/"
                                 + ManualText.ResourcePath(language));
                return string.Empty;
            }

            return ManualText.Format(template, language, Puzzles.CountsByLevel(), LoadReference())
                   + ManualText.LicenseSection(language, LoadLicenses());
        }

        /// <summary>
        /// 参考文献の本体。原稿は DataBase/Reference/index.html で、
        /// メニュー Pentomino ▸ 参考文献を取り込む が Resources へ写す。
        /// 書名は元の言語のまま並ぶので、言語ごとの出し分けはしない。
        /// </summary>
        public static string LoadReference()
        {
            var asset = Resources.Load<TextAsset>(ManualText.ReferenceResourcePath);
            return asset != null ? asset.text : string.Empty;
        }

        /// <summary>
        /// 書体の権利表示。python Tools/subset_fonts.py が書体そのものから
        /// 読み出して置く。手で写すと、書体を差し替えたときにずれる。
        /// </summary>
        public static string LoadLicenses()
        {
            var asset = Resources.Load<TextAsset>(ManualText.LicenseResourcePath);
            return asset != null ? asset.text : string.Empty;
        }

        /// <summary>雛形をそのまま読む。翻訳状況を調べるときにも使う。</summary>
        public static string LoadManualTemplate(Language language)
        {
            var asset = Resources.Load<TextAsset>(ManualText.ResourcePath(language));
            return asset != null ? asset.text : null;
        }

        /// <summary>読み込み済みのデータを破棄する（主にテスト用）。</summary>
        public static void Unload()
        {
            _postures = null;
            _puzzles = null;
            _solver = null;
        }

        private static string LoadText(string resourcePath)
        {
            var asset = Resources.Load<TextAsset>(resourcePath);
            if (asset == null)
                throw new InvalidOperationException("Resources/" + resourcePath + " が見つかりません。");
            return asset.text;
        }
    }
}
