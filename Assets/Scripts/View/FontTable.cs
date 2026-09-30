using System;
using Pentomino.Core;
using UnityEngine;

namespace Pentomino.View
{
    /// <summary>
    /// 言語ごとに使うフォントの対応表（開発仕様「設定モード」）。
    ///
    /// 日本語のフォントで韓国語を出すと字が豆腐になる。字が入っている書体は
    /// 言語ごとに違うので、言語を切り替えたらフォントも切り替える必要がある。
    ///
    /// 対応表は Resources/UI/FontTable.asset に置く。フォント本体は Resources の
    /// 外（Assets/Fonts/）にあるが、この表から参照されているので配布物には入る。
    /// </summary>
    [CreateAssetMenu(fileName = "FontTable", menuName = "Pentomino/Font Table")]
    public sealed class FontTable : ScriptableObject
    {
        /// <summary>Resources から読むときのパス。</summary>
        public const string ResourcePath = "UI/FontTable";

        [Serializable]
        public struct Entry
        {
            [Tooltip("言語コード（ja, ko, zh-Hans など）")]
            public string languageCode;

            public Font regular;
            public Font bold;
        }

        [Tooltip("どの言語にも当てはまらないときに使う。欧文の書体を入れておく")]
        [SerializeField] private Font _defaultRegular;
        [SerializeField] private Font _defaultBold;

        [Tooltip("言語ごとの書体。ここに無い言語は既定の書体になる")]
        [SerializeField] private Entry[] _entries = new Entry[0];

        public Font DefaultRegular => _defaultRegular;
        public Font DefaultBold => _defaultBold;
        public Entry[] Entries => _entries;

        /// <summary>
        /// この言語の細字。見つからなければ既定の書体に落ちる。
        /// </summary>
        public Font Regular(Language language)
        {
            var entry = Find(language);
            if (entry.regular != null) return entry.regular;

            return _defaultRegular;
        }

        /// <summary>
        /// この言語の太字。用意されていなければ null を返す。
        ///
        /// null のときに既定（欧文）の書体へ落とすと、日本語や韓国語の字が
        /// 出なくなってしまう。太字が無いことは呼び出し側に伝え、
        /// 同じ言語の細字を Unity に太らせてもらう。
        /// </summary>
        public Font Bold(Language language)
        {
            var entry = Find(language);
            if (entry.bold != null) return entry.bold;

            // その言語の欄そのものが無いなら、既定の太字を使う。
            return entry.regular == null ? _defaultBold : null;
        }

        private Entry Find(Language language)
        {
            var code = language.ToCode();

            if (_entries != null)
            {
                foreach (var entry in _entries)
                    if (entry.languageCode == code) return entry;
            }
            return default;
        }

        /// <summary>Resources から読む。無ければ null。</summary>
        public static FontTable Load() => Resources.Load<FontTable>(ResourcePath);

        /// <summary>生成ツールから中身を設定する。</summary>
        internal void Assign(Font defaultRegular, Font defaultBold, Entry[] entries)
        {
            _defaultRegular = defaultRegular;
            _defaultBold = defaultBold;
            _entries = entries;
        }
    }
}
