using System.Collections.Generic;
using Pentomino.Core;
using UnityEngine;
using UnityEngine.UI;

namespace Pentomino.View
{
    /// <summary>
    /// 画面じゅうの文字に、その言語の書体を配る。
    ///
    /// 太字かどうかはプレハブ側の設定（fontStyle）で決まっているが、
    /// 実際の太字の書体を当てたあとも fontStyle が Bold のままだと、
    /// Unity がさらに太らせてしまい二重に太くなる。
    /// そこで最初に見たときの太字かどうかを覚えておき、書体を当てたら
    /// fontStyle は Normal に戻す。言語を切り替えても判断がぶれない。
    /// </summary>
    public sealed class FontApplier
    {
        private readonly HashSet<Text> _bold = new HashSet<Text>();
        private readonly HashSet<Text> _seen = new HashSet<Text>();

        private FontTable _table;

        /// <summary>対応表。読めなければ何もしない。</summary>
        public FontTable Table => _table != null ? _table : (_table = FontTable.Load());

        /// <summary>この枝の下にある文字すべてに、その言語の書体を当てる。</summary>
        public void Apply(GameObject root, Language language)
        {
            if (root == null || Table == null) return;

            foreach (var text in root.GetComponentsInChildren<Text>(true))
                Apply(text, language);
        }

        /// <summary>文字 1 つに書体を当てる。</summary>
        public void Apply(Text text, Language language)
        {
            if (text == null || Table == null) return;

            // 初めて見る文字なら、プレハブでの太字指定を覚えておく。
            if (_seen.Add(text))
            {
                var style = text.fontStyle;
                if (style == FontStyle.Bold || style == FontStyle.BoldAndItalic) _bold.Add(text);
            }

            var italic = text.fontStyle == FontStyle.Italic
                         || text.fontStyle == FontStyle.BoldAndItalic;

            if (!_bold.Contains(text))
            {
                var regular = Table.Regular(language);
                if (regular != null) text.font = regular;
                return;
            }

            var bold = Table.Bold(language);
            if (bold != null)
            {
                // 本物の太字を当てたので、Unity 側で太らせる指定は外す。
                // 残しておくと二重に太くなる。
                text.font = bold;
                text.fontStyle = italic ? FontStyle.Italic : FontStyle.Normal;
                return;
            }

            // 太字の書体が無い構成。細字を当てて、Unity に太らせてもらう。
            var fallback = Table.Regular(language);
            if (fallback != null) text.font = fallback;

            text.fontStyle = italic ? FontStyle.BoldAndItalic : FontStyle.Bold;
        }

        /// <summary>覚えている内容を捨てる（画面を組み直したとき用）。</summary>
        public void Forget()
        {
            _bold.Clear();
            _seen.Clear();
        }
    }
}
