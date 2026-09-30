using UnityEngine;
using UnityEngine.UI;

namespace Pentomino.View
{
    /// <summary>
    /// 説明書の表の 1 行。レベル・問題数・割合の 3 列でできている。
    ///
    /// 本文に空白を並べて桁を揃える方法は、字幅の異なる書体では崩れる。
    /// 列ごとに別の文字として置き、右揃えにすることで数字がきちんと並ぶ。
    ///
    /// 見た目（色・太さ・列の幅）は Assets/Resources/UI/ManualPanel.prefab の
    /// Table ▸ RowTemplate を編集すると、全部の行にそのまま効く。
    /// </summary>
    [AddComponentMenu("Pentomino/Manual Table Row")]
    [RequireComponent(typeof(RectTransform))]
    public sealed class ManualTableRow : MonoBehaviour
    {
        [SerializeField] private Text _level;
        [SerializeField] private Text _count;
        [SerializeField] private Text _share;

        [Tooltip("行の地色。1 行おきに薄く塗ると表らしく見える")]
        [SerializeField] private Image _background;

        [Tooltip("この行の上に引く区切り線")]
        [SerializeField] private Image _rule;

        /// <summary>1 行分の値を入れる。</summary>
        public void Set(string level, string count, string share)
        {
            if (_level != null) _level.text = level;
            if (_count != null) _count.text = count;
            if (_share != null) _share.text = share;
        }

        /// <summary>この行の上に区切り線を出すか。</summary>
        public void ShowRule(bool visible)
        {
            if (_rule != null) _rule.gameObject.SetActive(visible);
        }

        /// <summary>見出しや合計の行は太字にする。</summary>
        public void SetBold(bool bold)
        {
            var style = bold ? FontStyle.Bold : FontStyle.Normal;
            if (_level != null) _level.fontStyle = style;
            if (_count != null) _count.fontStyle = style;
            if (_share != null) _share.fontStyle = style;
        }

        /// <summary>1 行おきの地色を出すか。</summary>
        public void ShowStripe(bool visible)
        {
            if (_background != null) _background.enabled = visible;
        }

        /// <summary>生成ツールから参照をまとめて設定する。</summary>
        internal void Assign(Text level, Text count, Text share, Image background, Image rule)
        {
            _level = level;
            _count = count;
            _share = share;
            _background = background;
            _rule = rule;
        }
    }
}
