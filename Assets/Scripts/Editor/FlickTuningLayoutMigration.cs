using System.Text;
using Pentomino.Core;
using Pentomino.View;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace Pentomino.EditorTools
{
    /// <summary>
    /// 感度調整の画面に、振動の長さと強さのつまみを足す。
    ///
    /// 振動の感じ方は端末で大きく違う。同じ 20ms でも、鋭く来る機種、
    /// ほとんど分からない機種、何をしても無音の機種（振動子の故障を含む）がある。
    /// こちらで数字を決め打ちにできないので、遊ぶ人が指で確かめて決められる
    /// ようにする。つまみを動かすたびに一度震えるので、その場で判る。
    ///
    /// 反転のつまみと同じ画面に置くのは、どちらも「実機で指で確かめて決める」
    /// ものだから。保存先も同じ（FlickSettings）。
    ///
    /// 二度実行しても構わない。作り直すので、位置を手で直したぶんは戻る。
    /// </summary>
    public static class FlickTuningLayoutMigration
    {
        private const string PrefabPath = "Assets/Resources/UI/FlickTuningPanel.prefab";

        // ---- 割り付け（基準の幅 1080 での値）

        private const float Margin = 44f;
        private const float Width = 1080f - Margin * 2f;   // 992

        /// <summary>つまみ 1 本ぶんの高さ。名前・値の行と、つまみの行。</summary>
        private const float RowHeight = 150f;

        private const float LabelHeight = 60f;
        private const float SliderHeight = 60f;

        /// <summary>
        /// 実測を出す欄の高さ。
        ///
        /// 中身は 3 行ある（判定・コマの届き方・ひとコマの動き）。1 行ぶんしか
        /// 取っていなかったので、下の 2 行がボタンの下に潜って読めなかった。
        /// 縦の余裕はあるので、3 行ぶんはっきり取る。
        /// </summary>
        private const float ResultHeight = 190f;

        /// <summary>実測の欄の字の大きさ。1 行が右端で切れない大きさにする。</summary>
        private const int ResultFontSize = 36;

        /// <summary>
        /// 中身を収める高さ。子画面は柱の幅に合わせて縮むので、内側の物差しで
        /// 見た高さは機種によらず 2180 ほど。余裕を見て 2050 とする。
        /// </summary>
        private const float Ceiling = 2050f;

        /// <summary>つまみの名前。FlickTuningPanel.Refresh の並びと揃える。</summary>
        private static readonly StringId[] Names =
        {
            StringId.FlickReleaseSpeed,
            StringId.FlickFilter,
            StringId.FlickStill,
            StringId.FlickMinTravel,
            StringId.FlickAxisRatio,
            StringId.VibrationLength,
            StringId.VibrationStrength,
        };

        [MenuItem("Pentomino/感度調整の画面を組み直す")]
        public static void Run()
        {
            var root = PrefabUtility.LoadPrefabContents(PrefabPath);
            try
            {
                var report = Apply(root);
                if (report == null) return;

                PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
                AssetDatabase.SaveAssets();
                Debug.Log(report);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        private static string Apply(GameObject root)
        {
            var panel = root.GetComponent<FlickTuningPanel>();
            if (panel == null)
            {
                Debug.LogError(PrefabPath + " に FlickTuningPanel がありません。");
                return null;
            }

            if (Names.Length != FlickTuningPanel.SliderCount)
            {
                Debug.LogError("つまみの本数が食い違っています: " + Names.Length
                               + " != " + FlickTuningPanel.SliderCount);
                return null;
            }

            // つまみを作るのに組み込みのスプライトが要る。
            UiPrefabBuilder.LoadSprites();

            var parent = (RectTransform)root.transform;

            // 実行時の値が焼き付いていることがある。基準の姿に戻しておく。
            parent.sizeDelta = Vector2.zero;
            parent.anchoredPosition = Vector2.zero;

            var report = new StringBuilder("感度調整の画面を組み直しました。\n\n");
            var y = 40f;

            Place(Existing(parent, "Title"), Margin, y, Width, 100f);
            y += 140f;

            var sliders = new Slider[Names.Length];
            var labels = new Text[Names.Length];
            var values = new Text[Names.Length];

            for (var i = 0; i < Names.Length; i++)
            {
                var top = y + i * RowHeight;

                labels[i] = Row(parent, "Label" + i, Names[i], Margin, top, Width * 0.6f, false);
                values[i] = Row(parent, "Value" + i, Names[i], Margin + Width * 0.6f, top,
                    Width * 0.4f, true);

                sliders[i] = Knob(parent, "Slider" + i, Margin, top + 70f);
            }

            y += Names.Length * RowHeight + 10f;
            report.AppendLine("  つまみ      " + Names.Length + " 本（振動の長さ・強さを追加）");

            var tryLabel = Existing(parent, "TryLabel");
            Place(tryLabel, Margin, y, Width, LabelHeight);
            y += 70f;

            // 練習用の枠。ここで実際に滑らせて確かめる。つまみが増えたぶん詰める。
            Place(Existing(parent, "Arena"), Margin, y, Width, 360f);
            y += 380f;

            Result(parent, Margin, y);
            y += ResultHeight + 30f;

            var half = (Width - 12f) * 0.5f;
            Place(Existing(parent, "Reset"), Margin, y, half, 110f);
            Place(Existing(parent, "Close"), Margin + half + 12f, y, half, 110f);
            y += 110f;

            panel.Assign(
                TextOf(parent, "Title"), TextOf(parent, "TryLabel"), TextOf(parent, "Result"),
                sliders, labels, values,
                parent.GetComponentInChildren<FlickSample>(true),
                ButtonOf(parent, "Reset"), ButtonOf(parent, "Close"));

            if (!panel.Validate(out var missing))
            {
                Debug.LogError("FlickTuningPanel の参照が足りません: " + missing);
                return null;
            }

            report.AppendLine();
            report.AppendLine("高さは " + y.ToString("0") + " / " + Ceiling.ToString("0") + " です。");

            if (y > Ceiling)
            {
                Debug.LogError("感度調整の画面が縦に長すぎます: " + y.ToString("0")
                               + " > " + Ceiling.ToString("0")
                               + "。閉じるボタンが画面の外に出ます。");
                return null;
            }

            return report.ToString();
        }

        // ---------------------------------------------------------------- 小道具

        /// <summary>
        /// 実測を出す欄。3 行入る高さを取り、はみ出しても切らない。
        /// </summary>
        private static void Result(RectTransform parent, float x, float y)
        {
            var rect = Existing(parent, "Result");
            if (rect == null) return;

            Place(rect, x, y, Width, ResultHeight);

            var text = rect.GetComponent<Text>();
            if (text == null) return;

            text.fontSize = ResultFontSize;
            text.alignment = TextAnchor.UpperLeft;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Truncate;
        }

        /// <summary>つまみの名前と値の行。無ければ作る。</summary>
        private static Text Row(RectTransform parent, string name, StringId id,
            float x, float y, float width, bool isValue)
        {
            var rect = Existing(parent, name);
            Text text;

            if (rect == null)
            {
                text = UiFactory.CreateLabel(parent, name,
                    isValue ? "-" : Strings.Get(id, Language.Japanese), 44);
                rect = (RectTransform)text.transform;
            }
            else
            {
                text = rect.GetComponent<Text>();
            }

            if (isValue && text != null)
            {
                text.alignment = TextAnchor.MiddleRight;
                text.fontStyle = FontStyle.Bold;
            }

            Place(rect, x, y, width, LabelHeight);
            return text;
        }

        /// <summary>つまみそのもの。無ければ作る。</summary>
        private static Slider Knob(RectTransform parent, string name, float x, float y)
        {
            var rect = Existing(parent, name);

            var slider = rect != null
                ? rect.GetComponent<Slider>()
                : UiPrefabBuilder.MakeSlider(parent, name, x, y, Width, SliderHeight);

            if (slider == null) return null;

            Place((RectTransform)slider.transform, x, y, Width, SliderHeight);
            return slider;
        }

        /// <summary>左上を原点にして置く。この画面はこの並べ方で統一している。</summary>
        private static void Place(RectTransform rect, float x, float y, float width, float height)
        {
            if (rect == null) return;

            rect.anchorMin = rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.sizeDelta = new Vector2(width, height);
            rect.anchoredPosition = new Vector2(x, -y);
        }

        private static RectTransform Existing(RectTransform parent, string name)
        {
            foreach (var child in parent.GetComponentsInChildren<RectTransform>(true))
                if (child.name == name) return child;

            return null;
        }

        private static Text TextOf(RectTransform parent, string name)
        {
            var rect = Existing(parent, name);
            return rect != null ? rect.GetComponent<Text>() : null;
        }

        private static Button ButtonOf(RectTransform parent, string name)
        {
            var rect = Existing(parent, name);
            return rect != null ? rect.GetComponent<Button>() : null;
        }
    }
}
