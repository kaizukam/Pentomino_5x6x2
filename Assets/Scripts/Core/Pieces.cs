namespace Pentomino.Core
{
    /// <summary>12 種のペントミノ・ピースに関する定数と索引。</summary>
    public static class Pieces
    {
        /// <summary>ピース名をアルファベット順に並べたもの。中級・上級の待機場所の並び順に使う。</summary>
        public const string Alphabetical = "FILNPTUVWXYZ";

        public const int Count = 12;

        /// <summary>1 ピースが占めるセル数。</summary>
        public const int CellsPerPiece = 5;

        /// <summary>アルファベット順での索引。未知のピースなら -1。</summary>
        public static int IndexOf(char piece) => Alphabetical.IndexOf(piece);

        public static bool IsValid(char piece) => Alphabetical.IndexOf(piece) >= 0;

        /// <summary>索引からピース名へ。</summary>
        public static char At(int index) => Alphabetical[index];
    }
}
