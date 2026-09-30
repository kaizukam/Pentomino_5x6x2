using System;
using Pentomino.Core;
using Pentomino.Data;
using UnityEngine;
using UnityEngine.UI;

namespace Pentomino.View
{
    /// <summary>
    /// 説明書の画面（開発仕様「説明/Manual」）。
    /// 本文は長いので縦にスクロールして読む。
    ///
    /// レベル別の問題数は、本文に混ぜると書体の字幅が揃わず桁がずれるので、
    /// 3 列の本物の表として別に組む。本文は表の前後に分けて置く。
    ///
    /// 見た目は Assets/Resources/UI/ManualPanel.prefab を編集して調整する。
    /// </summary>
    [AddComponentMenu("Pentomino/Manual Panel")]
    [RequireComponent(typeof(RectTransform))]
    public sealed class ManualPanel : MonoBehaviour
    {
        [Header("文字")]
        [SerializeField] private Text _title;

        [Tooltip("表より前の本文")]
        [SerializeField] private Text _bodyTop;

        [Tooltip("表より後の本文")]
        [SerializeField] private Text _bodyBottom;

        [Header("表")]
        [Tooltip("レベル別の問題数を並べる入れ物")]
        [SerializeField] private RectTransform _table;

        [Tooltip("表の 1 行分の雛形。これを複製して行を作る")]
        [SerializeField] private ManualTableRow _rowTemplate;

        [Header("ボタン")]
        [SerializeField] private Button _close;

        [Header("スクロール")]
        [SerializeField] private ScrollRect _scroll;

        private bool _wired;

        /// <summary>本文から作った文章や絵。作り直すときにまとめて片付ける。</summary>
        private readonly System.Collections.Generic.List<GameObject> _extras =
            new System.Collections.Generic.List<GameObject>();

        /// <summary>閉じるボタンが押されたとき。</summary>
        public event Action CloseRequested;

        public bool IsOpen => gameObject.activeSelf;

        private void Awake() => Wire();

        private void Wire()
        {
            if (_wired) return;
            _wired = true;

            if (_close != null) _close.onClick.AddListener(() => CloseRequested?.Invoke());
        }

        /// <summary>いまの言語で本文を作って開く。</summary>
        public void Open()
        {
            Wire();
            gameObject.SetActive(true);
            Refresh();

            // 開いたら先頭から読ませる。
            if (_scroll != null) _scroll.verticalNormalizedPosition = 1f;
        }

        public void Close() => gameObject.SetActive(false);

        /// <summary>文言を今の言語で作り直す。</summary>
        public void Refresh()
        {
            SetText(_title, Strings.Get(StringId.Manual));
            if (_close != null) SetText(LabelOf(_close), Strings.Get(StringId.Close));

            var template = GameData.LoadManualTemplate(Strings.Current)
                           ?? GameData.LoadManualTemplate(Language.English);

            // 問題数はデータから数える。問題集を入れ替えても表がずれない。
            var counts = GameData.Puzzles.CountsByLevel();

            ManualText.TrySplitAtTable(template, Strings.Current, counts, GameData.LoadReference(),
                out var top, out var bottom);

            // 書体の権利表示は、いちばん最後に付ける。
            bottom += ManualText.LicenseSection(Strings.Current, GameData.LoadLicenses());
            // 表の見出しも本文から取り出し、表と同じ列組みで並べる。
            top = ManualText.TakeTableHeader(top, out var header);

            ClearExtras();

            RenderBody(_bodyTop, top);
            RenderBody(_bodyBottom, bottom);

            BuildTable(header, counts);
        }

        /// <summary>
        /// 本文を、文章と絵の並びとして積む。
        ///
        /// 説明書はもともと素の文字だけだった。参考文献では表紙を出して
        /// 押せるようにしたいし、この先は操作を絵で見せることもある。
        /// 本文の中の目印（[IMG ...]）でそこを切り、部品を作って縦に並べる。
        ///
        /// 並べる先はプレハブの Content（縦並びの入れ物）で、
        /// 文章の見た目は渡された Text をそのまま複製して受け継ぐ。
        /// 書体・大きさ・色をプレハブで決められる形は崩さない。
        /// </summary>
        private void RenderBody(Text anchor, string body)
        {
            if (anchor == null) return;

            var blocks = ManualBlocks.Parse(body);

            // 先頭が文章なら、プレハブにある Text をそのまま使う。
            var start = 0;
            if (blocks.Count > 0 && blocks[0].Kind == ManualBlockKind.Text)
            {
                anchor.gameObject.SetActive(true);
                anchor.text = blocks[0].Text;
                start = 1;
            }
            else
            {
                anchor.text = string.Empty;
                anchor.gameObject.SetActive(false);
            }

            var parent = anchor.transform.parent;
            var index = anchor.transform.GetSiblingIndex();

            for (var i = start; i < blocks.Count; i++)
            {
                var made = blocks[i].Kind == ManualBlockKind.Image
                    ? MakeImage(blocks[i], parent)
                    : MakeText(blocks[i].Text, anchor, parent);

                if (made == null) continue;

                made.transform.SetSiblingIndex(++index);
                _extras.Add(made);
            }
        }

        /// <summary>文章の続き。見た目は元の Text から受け継ぐ。</summary>
        private GameObject MakeText(string text, Text template, Transform parent)
        {
            var made = Instantiate(template, parent);
            made.name = "BodyPart";
            made.gameObject.SetActive(true);
            made.text = text;
            return made.gameObject;
        }

        /// <summary>
        /// 絵。行き先が付いていれば押せるようにする。
        ///
        /// 高さだけを決めて、幅は縦並びの入れ物に任せる。
        /// preserveAspect を立ててあるので、その枠の中で縦横比を保って収まる。
        /// </summary>
        private GameObject MakeImage(ManualBlock block, Transform parent)
        {
            var sprite = Resources.Load<Sprite>(block.Image);
            if (sprite == null)
            {
                Debug.LogWarning("説明書の絵が見つかりません: Resources/" + block.Image);
                return null;
            }

            var go = new GameObject("Picture", typeof(RectTransform), typeof(CanvasRenderer),
                typeof(Image), typeof(LayoutElement));
            go.transform.SetParent(parent, false);

            var image = go.GetComponent<Image>();
            image.sprite = sprite;
            image.preserveAspect = true;

            var aspect = sprite.rect.height > 0f ? sprite.rect.width / sprite.rect.height : 1f;
            go.GetComponent<LayoutElement>().preferredHeight = ShownWidth() / Mathf.Max(aspect, 0.01f);

            if (!block.HasUrl) return go;

            // 押すと外の場所へ行く。原稿では表紙が Amazon への入り口になっている。
            var url = block.Url;
            var button = go.AddComponent<Button>();
            button.targetGraphic = image;
            button.onClick.AddListener(() => Application.OpenURL(url));

            return go;
        }

        /// <summary>絵を出す幅。本文の幅いっぱいだと大きすぎるので少し絞る。</summary>
        private float ShownWidth()
        {
            var content = _scroll != null ? _scroll.content : null;
            var width = content != null ? content.rect.width : 0f;
            if (width <= 0f) width = ((RectTransform)transform).rect.width;
            if (width <= 0f) width = DefaultContentWidth;

            return width * ImageWidthRatio;
        }

        /// <summary>本文の幅に対する絵の幅の割合。</summary>
        private const float ImageWidthRatio = 0.55f;

        /// <summary>幅がまだ決まっていないときに使う値（基準の物差し）。</summary>
        private const float DefaultContentWidth = 900f;

        /// <summary>前回作った部品を片付ける。言語を変えると作り直しになる。</summary>
        private void ClearExtras()
        {
            foreach (var extra in _extras)
            {
                if (extra == null) continue;

                extra.transform.SetParent(null, false);
                Destroy(extra);
            }

            _extras.Clear();
        }

        /// <summary>レベル別の問題番号の範囲と割合を 3 列の表として並べる。</summary>
        private void BuildTable(string[] header, int[] countsByLevel)
        {
            if (_table == null || _rowTemplate == null) return;

            _rowTemplate.gameObject.SetActive(false);

            // 言語を変えると見出しも変わるので、毎回作り直す。
            // 親から外してから消すと、その場で並びが詰め直される。
            for (var i = _table.childCount - 1; i >= 0; i--)
            {
                var child = _table.GetChild(i);
                if (child == _rowTemplate.transform) continue;

                child.SetParent(null, false);
                Destroy(child.gameObject);
            }

            if (header != null && header.Length >= 3)
            {
                var head = NewRow("RowHeader");
                head.Set(header[0], header[1], header[2]);
                head.SetBold(true);
                head.ShowRule(false);
                head.ShowStripe(false);
            }

            for (var level = 1; level < countsByLevel.Length; level++)
            {
                var count = countsByLevel[level];
                var share = ManualText.Share(count, ManualText.Total(countsByLevel));

                // 真ん中の列は問題数ではなく、そのレベルの問題番号の範囲。
                // 何番から何番までがそのレベルかが、送りボタンで探すときの目安になる。
                var row = NewRow("Row" + level);
                row.Set("L" + level, ManualText.NumberRange(countsByLevel, level), share.ToString("0.00") + "%");
                row.SetBold(false);

                // 見出しの下に 1 本だけ線を引く。
                row.ShowRule(level == 1 && header != null);

                // 1 行おきに薄い地色を敷くと、目が横に滑らない。
                row.ShowStripe(level % 2 == 0);
            }

            // 最後に合計の行。上に区切り線を出す。
            var total = NewRow("RowTotal");
            total.Set(string.Empty, ManualText.Total(countsByLevel).ToString(), "100.00%");
            total.SetBold(true);
            total.ShowRule(true);
            total.ShowStripe(false);
        }

        private ManualTableRow NewRow(string name)
        {
            var row = Instantiate(_rowTemplate, _table);
            row.name = name;
            row.gameObject.SetActive(true);
            return row;
        }

        private static Text LabelOf(Button button) =>
            button == null ? null : button.GetComponentInChildren<Text>(true);

        private static void SetText(Text text, string value)
        {
            if (text != null) text.text = value;
        }

        /// <summary>プレハブに参照を付け忘れていないか調べる（テスト・生成ツール用）。</summary>
        public bool Validate(out string missing)
        {
            missing = null;

            if (_title == null) missing = "Title";
            else if (_bodyTop == null) missing = "BodyTop";
            else if (_bodyBottom == null) missing = "BodyBottom";
            else if (_table == null) missing = "Table";
            else if (_rowTemplate == null) missing = "RowTemplate";
            else if (_close == null) missing = "Close";
            else if (_scroll == null) missing = "Scroll";

            return missing == null;
        }

        /// <summary>生成ツールから参照をまとめて設定する。</summary>
        internal void Assign(Text title, Text bodyTop, Text bodyBottom,
            RectTransform table, ManualTableRow rowTemplate, Button close, ScrollRect scroll)
        {
            _title = title;
            _bodyTop = bodyTop;
            _bodyBottom = bodyBottom;
            _table = table;
            _rowTemplate = rowTemplate;
            _close = close;
            _scroll = scroll;
        }
    }
}
