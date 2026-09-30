namespace Pentomino.Core
{
    /// <summary>版番号のどこを上げるか。</summary>
    public enum VersionPart
    {
        /// <summary>直しただけ。0.4.1 → 0.4.2</summary>
        Fix,

        /// <summary>何かを足した。0.4.1 → 0.5.0</summary>
        Feature,

        /// <summary>作り直した。0.4.1 → 1.0.0</summary>
        Major,
    }

    /// <summary>
    /// 版番号の上げ方（開発仕様「ビルドと配布」）。
    ///
    /// 利用者に見える版は「大.中.小」の三段。上の桁を上げたら、
    /// 下の桁は 0 に戻す。0.4.9 の次が 0.4.10 になるのは正しく、
    /// 0.5.0 にしたいなら中の桁を上げる。
    ///
    /// ストアに出す受付番号は別物で、ただ 1 ずつ増やすだけ。
    /// 同じ番号は二度受け付けられないので、戻すことはできない。
    /// </summary>
    public static class VersionRule
    {
        /// <summary>桁の区切り。</summary>
        public const char Separator = '.';

        /// <summary>版番号を読む。三段でなければ false。</summary>
        public static bool TryParse(string version, out int major, out int minor, out int fix)
        {
            major = minor = fix = 0;
            if (string.IsNullOrEmpty(version)) return false;

            var parts = version.Trim().Split(Separator);
            if (parts.Length != 3) return false;

            return TryNumber(parts[0], out major)
                   && TryNumber(parts[1], out minor)
                   && TryNumber(parts[2], out fix);
        }

        /// <summary>指定の桁を 1 上げ、その下を 0 に戻す。読めなければ false。</summary>
        public static bool TryBump(string version, VersionPart part, out string next)
        {
            next = version;
            if (!TryParse(version, out var major, out var minor, out var fix)) return false;

            switch (part)
            {
                case VersionPart.Major:
                    major++;
                    minor = 0;
                    fix = 0;
                    break;

                case VersionPart.Feature:
                    minor++;
                    fix = 0;
                    break;

                default:
                    fix++;
                    break;
            }

            next = major + Separator.ToString() + minor + Separator.ToString() + fix;
            return true;
        }

        /// <summary>
        /// ストアの受付番号を 1 進める。読めない値や負の値は 1 から数え直す。
        /// 戻すことはできないので、迷ったら増やす側に倒す。
        /// </summary>
        public static int NextBuildNumber(int current) => current < 1 ? 1 : current + 1;

        /// <summary>文字で持たされている受付番号（iOS）を 1 進める。</summary>
        public static string NextBuildNumber(string current) =>
            NextBuildNumber(TryNumber(current, out var value) ? value : 0).ToString();

        private static bool TryNumber(string text, out int value)
        {
            value = 0;
            if (string.IsNullOrEmpty(text)) return false;

            return int.TryParse(text.Trim(), System.Globalization.NumberStyles.Integer,
                System.Globalization.CultureInfo.InvariantCulture, out value) && value >= 0;
        }
    }
}
