using Pentomino.Core;
using UnityEngine;
using UnityEngine.UI;

namespace Pentomino.View
{
    /// <summary>
    /// おめでとうパネル（開発仕様「おめでとうパネル」）。
    ///
    /// 中身はプレハブ（Assets/Resources/UI/ResultPanel.prefab）に置いてあるので、
    /// 額縁・文字の大きさ・色・配置はエディタ上で見たまま編集できる。
    /// 額縁は元の絵の縦横比を保ったまま、画面に収まる大きさに置かれる。
    /// </summary>
    [AddComponentMenu("Pentomino/Result Panel")]
    [RequireComponent(typeof(RectTransform))]
    public sealed class ResultPanel : MonoBehaviour
    {
        [Header("額縁")]
        [Tooltip("額縁の絵。元の縦横比を保って表示する")]
        [SerializeField] private Image _frame;

        [Header("文字")]
        [SerializeField] private Text _congratulations;
        [SerializeField] private Text _grade;
        [SerializeField] private Text _hint;
        [SerializeField] private Text _check;

        public RectTransform RectTransform => (RectTransform)transform;

        /// <summary>額縁の絵の縦横比（幅 ÷ 高さ）。絵が無ければ 1。</summary>
        public float FrameAspect
        {
            get
            {
                var sprite = _frame != null ? _frame.sprite : null;
                if (sprite == null || sprite.rect.height <= 0f) return 1f;
                return sprite.rect.width / sprite.rect.height;
            }
        }

        /// <summary>いま画面に出ている大きさ（縮尺こみ）。</summary>
        public Vector2 ScaledSize
        {
            get
            {
                var scale = RectTransform.localScale;
                var size = RectTransform.rect.size;
                return new Vector2(size.x * scale.x, size.y * scale.y);
            }
        }

        /// <summary>
        /// プレハブで作ったままの姿を、丸ごと縮めて指定の枠に収める。
        ///
        /// 枠だけ縮めて文字をそのままにすると、文字が額縁からはみ出す。
        /// 全体の縮尺を変えれば、プレハブで見たとおりの割り付けが保たれる。
        /// </summary>
        public void FitInside(float maxWidth, float maxHeight)
        {
            var size = RectTransform.rect.size;
            if (size.x <= 0f || size.y <= 0f) return;

            var scale = Mathf.Min(maxWidth / size.x, maxHeight / size.y);
            if (scale <= 0f) return;

            RectTransform.localScale = new Vector3(scale, scale, 1f);
        }

        /// <summary>成績を表示する。</summary>
        public void Show(PuzzleSession session)
        {
            if (session == null) return;

            SetText(_congratulations, Strings.Get(StringId.Congratulations));
            SetText(_grade, Strings.GradeName(session.Difficulty));
            SetText(_hint, Strings.Get(StringId.HintCount) + "   " + session.HintCount);
            SetText(_check, Strings.Get(StringId.CheckCount) + "   " + session.CheckCount);

            gameObject.SetActive(true);
        }

        public void Hide() => gameObject.SetActive(false);

        public bool IsOpen => gameObject.activeSelf;

        /// <summary>表示されている文字をひと続きにして返す（テスト用）。</summary>
        public string CombinedText =>
            TextOf(_congratulations) + "\n" + TextOf(_grade) + "\n" + TextOf(_hint) + "\n" + TextOf(_check);

        private static string TextOf(Text text) => text != null ? text.text : string.Empty;

        private static void SetText(Text text, string value)
        {
            if (text != null) text.text = value;
        }

        /// <summary>プレハブに参照を付け忘れていないか調べる（テスト・生成ツール用）。</summary>
        public bool Validate(out string missing)
        {
            missing = null;

            if (_frame == null) missing = "Frame";
            else if (_congratulations == null) missing = "Congratulations";
            else if (_grade == null) missing = "Grade";
            else if (_hint == null) missing = "Hint";
            else if (_check == null) missing = "Check";

            return missing == null;
        }

        /// <summary>生成ツールから参照をまとめて設定する。</summary>
        internal void Assign(Image frame, Text congratulations, Text grade, Text hint, Text check)
        {
            _frame = frame;
            _congratulations = congratulations;
            _grade = grade;
            _hint = hint;
            _check = check;
        }
    }
}
