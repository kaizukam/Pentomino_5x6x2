using System.Text;
using Pentomino.Core;
using Pentomino.View;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace Pentomino.EditorTools
{
    /// <summary>
    /// 設定画面の割り付けを組み直す。
    ///
    /// 変わったこと:
    ///   ・言語は 4 つずつ 2 行。縦に詰めて、説明と音のボタンの場所を作る
    ///   ・説明ボタンをメイン画面から移す。遊んでいる最中に押すものではない
    ///   ・音の入り切りボタンを足す。切ると、はまる音も完成の音も鳴らない
    ///   ・難易度のボタンは帯の絵そのものにする。文字は帯の色に合わせて黒か白
    ///   ・「履歴を消す」は説明文と同じ行の右端へ。難易度のボタンを狭めずに済む
    ///
    /// 中身はすべて基準の幅（1080）で並べる。機種ごとの幅合わせは、
    /// 実行時に GameScreen がパネルごと拡大縮小して行う。
    /// ここで幅を機種に合わせようとすると、二重に効いて狂う。
    ///
    /// 二度実行しても構わない。作り直すので、位置を手で直したぶんは戻る。
    /// </summary>
    public static class SettingsLayoutMigration
    {
        private const string PrefabPath = "Assets/Resources/UI/SettingsPanel.prefab";

        /// <summary>難易度ボタンの名前。SettingsPanel.Grades と同じ順。</summary>
        private static readonly (string Button, string Art, bool DarkText)[] Rows =
        {
            ("GradeGuided", "Intro", true),      // 白帯 → 黒い文字
            ("GradeTurn", "Practice", true),     // 黄帯 → 黒い文字
            ("GradeClassic", "Classic", false),  // 黒帯 → 白い文字
        };

        private static readonly string[] LanguageButtons =
        {
            "LangEnglish", "LangJapanese", "LangSpanish", "LangFrench",
            "LangGerman", "LangKorean", "LangChineseSimplified", "LangChineseTraditional",
        };

        // ---- 割り付け（基準の幅 1080 での値）

        private const float Margin = 44f;
        private const float Width = 1080f - Margin * 2f;   // 992

        /// <summary>
        /// 中身を収める高さ。
        ///
        /// 子画面は柱の幅（1080）に合わせて縮まるので、内側の物差しで見た高さは
        /// 機種によらず 1080 x 2.14 から上端の隠れる帯を引いたものになる。
        /// 実際に測ると、16:9 のスマホで 2197、ノッチの大きい機種で 2180、
        /// タブレットで 2239、Fire Max 11 の横向きで 2214。いちばん厳しいところに
        /// 余裕を見て 2050 とする。ここを超えると「閉じる」が画面の外に出る。
        /// </summary>
        private const float Ceiling = 2050f;

        private const float TitleHeight = 96f;
        private const float LabelHeight = 64f;
        private const float LanguageHeight = 92f;
        private const float LanguageGap = 14f;
        private const int LanguagesPerRow = 4;

        private const float ActionHeight = 100f;

        private const float ClearWidth = 320f;
        private const float ClearHeight = 80f;

        /// <summary>難易度ひとつぶんの塊のあとに空ける。塊の切れ目が判るように。</summary>
        private const float BlockGap = 44f;

        /// <summary>帯の織り目に負けないよう、級の名前に付ける縁取りの太さ。</summary>
        private const float LabelOutline = 3f;

        private const string ConfirmName = "ConfirmClear";

        [MenuItem("Pentomino/設定画面を組み直す")]
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
            var panel = root.GetComponent<SettingsPanel>();
            if (panel == null)
            {
                Debug.LogError(PrefabPath + " に SettingsPanel がありません。");
                return null;
            }

            var parent = (RectTransform)root.transform;
            // 実行時の値が焼き付いていることがある。基準の姿に戻しておく。
            parent.sizeDelta = Vector2.zero;
            parent.anchoredPosition = Vector2.zero;

            var report = new StringBuilder("設定画面を組み直しました。\n\n");
            var y = 80f;

            // ---- 見出し
            Place(Existing(parent, "Title"), Margin, y, Width, TitleHeight);
            y += TitleHeight + 14f;

            Place(Existing(parent, "LanguageLabel"), Margin, y, Width, LabelHeight);
            y += LabelHeight + 14f;

            // ---- 言語。4 つずつ 2 行に詰める。
            var cell = (Width - LanguageGap * (LanguagesPerRow - 1)) / LanguagesPerRow;
            for (var i = 0; i < LanguageButtons.Length; i++)
            {
                var rect = Existing(parent, LanguageButtons[i]);
                if (rect == null)
                {
                    Debug.LogError(LanguageButtons[i] + " が見つかりません。");
                    return null;
                }

                var col = i % LanguagesPerRow;
                var row = i / LanguagesPerRow;
                Place(rect, Margin + col * (cell + LanguageGap),
                    y + row * (LanguageHeight + LanguageGap), cell, LanguageHeight);

                // 言語名は長さがまちまち。枠に収まるところまで文字を縮める。
                BestFit(LabelOf(rect), 18, 36);
            }
            y += LanguageHeight * 2f + LanguageGap + 30f;
            report.AppendLine("  言語        " + LanguagesPerRow + " つずつ 2 行（幅 " + cell.ToString("0") + "）");

            // ---- 説明と音。メイン画面から移した説明ボタンは、ここに置く。
            var manual = Rebuild(parent, "Manual");
            MakeButton(manual, Strings.Get(StringId.Manual, Language.Japanese), 44);
            Place(manual, Margin, y, Width, ActionHeight);
            Bold(LabelOf(manual));
            y += ActionHeight + 16f;

            var sound = Rebuild(parent, "Sound");
            MakeButton(sound, Strings.Get(StringId.SoundLabel, Language.Japanese) + "   ON", 44);
            Place(sound, Margin, y, Width, ActionHeight);
            Bold(LabelOf(sound));
            y += ActionHeight + 36f;
            report.AppendLine("  説明・音    言語と難易度の間");

            // ---- 難易度
            Place(Existing(parent, "DifficultyLabel"), Margin, y, Width, LabelHeight);
            y += LabelHeight + 14f;

            var clears = new Button[Rows.Length];

            // ボタンの高さは帯の絵の比から出す。決め打ちにすると、絵を描き直した
            // ときに織り目が伸びる。三つとも同じ高さにしたいので、先頭の絵で決める。
            var first = GradePatternMigration.Face(Rows[0].Art);
            if (first == null)
            {
                Debug.LogError("帯の絵（" + Rows[0].Art
                               + "2.png）がありません。先に Pentomino ▸ 難易度の帯を取り込む を実行してください。");
                return null;
            }

            var gradeHeight = Width * first.rect.height / first.rect.width;

            for (var i = 0; i < Rows.Length; i++)
            {
                var row = Rows[i];

                var grade = Existing(parent, row.Button);
                if (grade == null)
                {
                    Debug.LogError(row.Button + " が見つかりません。");
                    return null;
                }

                Place(grade, Margin, y, Width, gradeHeight);

                // ボタンの面は帯そのもの。9 スライスの背景は外し、絵を丸ごと出す。
                var face = grade.GetComponent<Image>();
                var art = GradePatternMigration.Face(row.Art);
                if (face == null || art == null)
                {
                    Debug.LogError(row.Button + " の帯の絵（" + row.Art
                                   + "2.png）がありません。先に Pentomino ▸ 難易度の帯を取り込む を実行してください。");
                    return null;
                }

                face.sprite = art;
                face.type = Image.Type.Simple;
                face.preserveAspect = false;
                face.color = Color.white;

                var label = LabelOf(grade);
                if (label != null)
                {
                    // 白帯・黄帯の上では黒、黒帯の上では白。
                    label.color = row.DarkText ? Color.black : Color.white;
                    label.fontStyle = FontStyle.Bold;
                    BestFit(label, 28, 48);

                    // 太字だけでは織り目に紛れる。文字と逆の色で縁を取ると、
                    // 模様の上でも輪郭が切れずに読める。
                    Outline(label, row.DarkText ? Color.white : Color.black, LabelOutline);
                }

                y += gradeHeight + 12f;

                // 説明文と「履歴を消す」を同じ行に。高さを揃えて真ん中で合わせる。
                var noteWidth = Width - ClearWidth - 40f;

                var note = Existing(parent, "Note" + row.Button.Substring("Grade".Length));
                if (note != null)
                {
                    Place(note, Margin + 24f, y, noteWidth - 24f, ClearHeight);

                    var text = note.GetComponent<Text>();
                    if (text != null)
                    {
                        text.alignment = TextAnchor.MiddleLeft;

                        // 訳によっては長い。はみ出したままだと「履歴を消す」の下に潜る。
                        BestFit(text, 20, 34);
                    }
                }

                var clear = Rebuild(parent, "ClearRecord" + row.Button.Substring("Grade".Length));
                clears[i] = MakeButton(clear, Strings.Get(StringId.ClearRecord, Language.Japanese), 32);
                Place(clear, Margin + Width - ClearWidth, y, ClearWidth, ClearHeight);
                BestFit(LabelOf(clear), 18, 34);

                // 消すほうだと判るように、ほかのボタンとは別の色にする。
                var clearFace = clear.GetComponent<Image>();
                if (clearFace != null) clearFace.color = new Color(0.96f, 0.86f, 0.86f);

                y += ClearHeight + BlockGap;
            }
            report.AppendLine("  難易度      帯の絵をボタンの面に（"
                              + Width.ToString("0") + " x " + gradeHeight.ToString("0")
                              + "、文字は白帯・黄帯が黒、黒帯が白）");

            // ---- 残り
            Place(Existing(parent, "Warning"), Margin, y, Width, 84f);
            y += 84f + 24f;

            Place(Existing(parent, "FlickTuning"), Margin, y, Width, ActionHeight);
            y += ActionHeight + 16f;

            Place(Existing(parent, "Close"), Margin, y, Width, ActionHeight);
            y += ActionHeight;

            var confirm = BuildConfirm(parent, out var message, out var yes, out var no);
            panel.AssignReset(clears, confirm.gameObject, message, yes, no);
            panel.AssignExtras(manual.GetComponent<Button>(), sound.GetComponent<Button>());
            confirm.gameObject.SetActive(false);

            // 問いかけの覆いは、いちばん上に出す。
            confirm.SetAsLastSibling();

            report.AppendLine();
            report.AppendLine("高さは " + y.ToString("0") + " / " + Ceiling.ToString("0") + " です。");

            if (y > Ceiling)
            {
                Debug.LogError("設定画面が縦に長すぎます: " + y.ToString("0")
                               + " > " + Ceiling.ToString("0")
                               + "。16:9 の機種で「閉じる」が画面の外に出ます。");
                return null;
            }

            if (!panel.Validate(out var missing))
            {
                Debug.LogError("SettingsPanel の参照が足りません: " + missing);
                return null;
            }

            return report.ToString();
        }

        /// <summary>
        /// 問いかけの覆い。設定画面いっぱいに広げ、後ろを押せなくする。
        /// </summary>
        private static RectTransform BuildConfirm(RectTransform parent,
            out Text message, out Button yes, out Button no)
        {
            var confirm = Rebuild(parent, ConfirmName);
            confirm.anchorMin = Vector2.zero;
            confirm.anchorMax = Vector2.one;
            confirm.pivot = new Vector2(0.5f, 0.5f);
            confirm.offsetMin = Vector2.zero;
            confirm.offsetMax = Vector2.zero;

            // 半透明の幕。後ろのボタンへの当たりも、この Image が受け止める。
            var veil = confirm.gameObject.AddComponent<Image>();
            veil.color = new Color(0f, 0f, 0f, 0.55f);
            veil.raycastTarget = true;

            var box = UiFactory.CreateRect(confirm, "Box");
            box.anchorMin = box.anchorMax = new Vector2(0.5f, 0.5f);
            box.pivot = new Vector2(0.5f, 0.5f);
            box.sizeDelta = new Vector2(900f, 460f);
            box.anchoredPosition = Vector2.zero;

            var face = box.gameObject.AddComponent<Image>();
            face.color = new Color(0.98f, 0.98f, 0.98f);
            UiFactory.ApplyButtonSprite(face);

            message = UiFactory.CreateLabel(box, "Message",
                Strings.Get(StringId.ClearRecordAsk, Language.Japanese),
                52, TextAnchor.MiddleCenter);
            var messageRect = (RectTransform)message.transform;
            messageRect.anchorMin = new Vector2(0f, 1f);
            messageRect.anchorMax = new Vector2(1f, 1f);
            messageRect.pivot = new Vector2(0.5f, 1f);
            messageRect.offsetMin = new Vector2(40f, 0f);
            messageRect.offsetMax = new Vector2(-40f, 0f);
            messageRect.sizeDelta = new Vector2(messageRect.sizeDelta.x, 240f);
            messageRect.anchoredPosition = new Vector2(0f, -40f);
            message.horizontalOverflow = HorizontalWrapMode.Wrap;

            // 「やめる」を左、「消す」を右。押し間違えても左が安全な側になる。
            no = MakeCentredButton(box, "No", Strings.Get(StringId.Cancel, Language.Japanese),
                new Vector2(-215f, 90f));
            yes = MakeCentredButton(box, "Yes", Strings.Get(StringId.ClearRecordYes, Language.Japanese),
                new Vector2(215f, 90f));

            var danger = yes.GetComponent<Image>();
            if (danger != null) danger.color = new Color(0.93f, 0.72f, 0.72f);

            return confirm;
        }

        // ---------------------------------------------------------------- 小道具

        /// <summary>左上を原点にして置く。設定画面はこの並べ方で統一している。</summary>
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

        /// <summary>同じ名前のものがあれば捨ててから作り直す。二度実行しても増えない。</summary>
        private static RectTransform Rebuild(RectTransform parent, string name)
        {
            var existing = Existing(parent, name);
            if (existing != null) Object.DestroyImmediate(existing.gameObject);

            return UiFactory.CreateRect(parent, name);
        }

        private static Button MakeCentredButton(RectTransform parent, string name,
            string content, Vector2 centre)
        {
            var rect = UiFactory.CreateRect(parent, name);
            var button = MakeButton(rect, content, 40);

            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(380f, 110f);
            rect.anchoredPosition = centre;
            return button;
        }

        /// <summary>作りかけの入れ物に、ボタンの中身を入れる。</summary>
        private static Button MakeButton(RectTransform rect, string content, int fontSize)
        {
            rect.gameObject.AddComponent<CanvasRenderer>();

            var image = rect.gameObject.AddComponent<Image>();
            image.color = new Color(0.94f, 0.94f, 0.94f);
            UiFactory.ApplyButtonSprite(image);

            var label = UiFactory.CreateLabel(rect, "Label", content, fontSize, TextAnchor.MiddleCenter);
            var labelRect = (RectTransform)label.transform;
            labelRect.anchorMin = Vector2.zero;
            labelRect.anchorMax = Vector2.one;
            labelRect.pivot = new Vector2(0.5f, 0.5f);
            labelRect.offsetMin = Vector2.zero;
            labelRect.offsetMax = Vector2.zero;

            var button = rect.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            return button;
        }

        private static Text LabelOf(RectTransform rect) =>
            rect == null ? null : rect.GetComponentInChildren<Text>(true);

        /// <summary>言語や訳文で長さが変わる文字を、枠に収まるところまで縮める。</summary>
        private static void BestFit(Text text, int min, int max)
        {
            if (text == null) return;

            text.resizeTextForBestFit = true;
            text.resizeTextMinSize = min;
            text.resizeTextMaxSize = max;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Truncate;
        }

        private static void Bold(Text text)
        {
            if (text != null) text.fontStyle = FontStyle.Bold;
        }

        /// <summary>
        /// 文字に縁取りを付ける。模様の上に置く文字が背景に紛れないようにする。
        /// 二度実行しても重ならないよう、付いていたものは外してから付け直す。
        /// </summary>
        private static void Outline(Text text, Color color, float thickness)
        {
            if (text == null) return;

            foreach (var old in text.GetComponents<UnityEngine.UI.Outline>())
                Object.DestroyImmediate(old);

            var outline = text.gameObject.AddComponent<UnityEngine.UI.Outline>();
            outline.effectColor = color;
            outline.effectDistance = new Vector2(thickness, -thickness);
            outline.useGraphicAlpha = true;
        }
    }
}
