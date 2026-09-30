namespace Pentomino.Core
{
    /// <summary>
    /// 遊び方の段階（開発仕様「難易度の管理」）。
    /// 上下の優劣ではなく、必要な操作の量で分けている。
    /// 保存データには数値で入るので、名前を変えても既存の進捗は読める。
    ///
    /// 画面に出す名前は柔道の帯にならった三つで、ここの名前とは別に持つ
    /// （<see cref="Strings.GradeName(Difficulty)"/>）。ここの名前は保存データの
    /// 数値と一対一なので、画面の呼び名が変わっても動かさない。
    ///
    ///   Guided  → Intro    白帯
    ///   Turn    → Practice 黄帯
    ///   Classic → Classic  黒帯
    /// </summary>
    public enum Difficulty
    {
        /// <summary>Intro（白帯）: 並びも姿勢も解答通り。そのまま置くだけ。</summary>
        Guided = 0,

        /// <summary>Practice（黄帯）: 並びは解答順。姿勢はリセットされるので、自分で回して合わせる。</summary>
        Turn = 1,

        /// <summary>Classic（黒帯）: 並びはアルファベット順、姿勢は常に #00。既定。</summary>
        Classic = 2,
    }

    public static class DifficultyRules
    {
        /// <summary>設定画面に並べる順。</summary>
        public static readonly Difficulty[] All =
        {
            Difficulty.Guided, Difficulty.Turn, Difficulty.Classic,
        };

        /// <summary>待機場所の並びを解答順にするか。false ならアルファベット順。</summary>
        public static bool UsesAnswerOrder(this Difficulty difficulty) => difficulty != Difficulty.Classic;

        /// <summary>
        /// 待機場所の初期姿勢を解答通りにするか。false なら常に姿勢番号 00。
        /// 姿勢まで教えるのは Intro だけ。Practice は自分で回して向きを合わせる級。
        /// </summary>
        public static bool UsesAnswerPosture(this Difficulty difficulty) => difficulty == Difficulty.Guided;
    }
}
