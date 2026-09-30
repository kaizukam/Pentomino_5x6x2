namespace Pentomino.Core
{
    /// <summary>
    /// 未収納ピースの並べ方（開発仕様「操作性の問題」）。
    ///
    /// 左から順に置き、幅に入らなくなったら次へ折り返す。ここまでは素直だが、
    /// 段の高さをその段でいちばん背の高いピースに合わせると、背の低いピースの
    /// 下に空きが残る。I（1 段）が T（3 段）と並ぶと、2 段ぶん無駄になる。
    ///
    /// そこで、置く直前にそのピースを真上へ持ち上げる。自分の幅の範囲に
    /// すでに何か置かれていれば、その一番下まで。何も無ければ天井まで。
    /// 段という考え方をやめて、上へ詰められるだけ詰める。
    ///
    /// 形はピースの外接矩形で見る。実際の凹凸まで噛み合わせると、
    /// 見た目に絡まって、どれを掴んでいるのか分からなくなる。
    /// </summary>
    public static class TrayPacking
    {
        /// <summary>ピースひとつの置き場所と大きさ。原点は待機場所の左上、下向きが正。</summary>
        public struct Slot
        {
            public float X;
            public float Y;
            public float Width;
            public float Height;

            public float Right => X + Width;
            public float Bottom => Y + Height;
        }

        /// <summary>並べた結果。</summary>
        public struct Result
        {
            public Slot[] Slots;

            /// <summary>いちばん下まで含めた高さ。</summary>
            public float Height;
        }

        /// <summary>
        /// 外接矩形の大きさを受け取って、置き場所を返す。
        /// </summary>
        /// <param name="widths">ピースの幅。並べたい順に</param>
        /// <param name="heights">ピースの高さ。widths と同じ順</param>
        /// <param name="trayWidth">待機場所の幅</param>
        /// <param name="gap">ピースどうしの間隔</param>
        public static Result Arrange(float[] widths, float[] heights, float trayWidth, float gap)
        {
            if (widths == null || heights == null) return new Result { Slots = new Slot[0] };

            var count = widths.Length < heights.Length ? widths.Length : heights.Length;
            var slots = new Slot[count];

            var cursor = 0f;
            var bottom = 0f;

            for (var i = 0; i < count; i++)
            {
                var w = widths[i];
                var h = heights[i];

                // 右端からはみ出すなら、左へ折り返す。
                // 幅そのものが足りないときは、はみ出したまま左端に置く（消さない）。
                if (cursor > 0f && cursor + w > trayWidth) cursor = 0f;

                var slot = new Slot { X = cursor, Width = w, Height = h };
                slot.Y = Ceiling(slots, i, slot, gap);

                slots[i] = slot;

                if (slot.Bottom > bottom) bottom = slot.Bottom;
                cursor = slot.Right + gap;
            }

            return new Result { Slots = slots, Height = count == 0 ? 0f : bottom + gap };
        }

        /// <summary>
        /// そのピースを持ち上げられるところまでの y。
        /// 自分の幅にかかっている物の、いちばん下が天井になる。
        /// </summary>
        private static float Ceiling(Slot[] placed, int count, Slot slot, float gap)
        {
            var y = 0f;

            for (var i = 0; i < count; i++)
            {
                var other = placed[i];
                if (!OverlapsHorizontally(slot, other)) continue;

                var below = other.Bottom + gap;
                if (below > y) y = below;
            }

            return y;
        }

        /// <summary>横方向で重なっているか。触れているだけなら重なりとみなさない。</summary>
        private static bool OverlapsHorizontally(Slot a, Slot b) =>
            a.X < b.Right && b.X < a.Right;
    }
}
