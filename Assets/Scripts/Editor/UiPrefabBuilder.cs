using System.Collections.Generic;
using System.IO;
using Pentomino.Core;
using Pentomino.View;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace Pentomino.EditorTools
{
    /// <summary>
    /// UI プレハブの「最初の一枚」を作る道具。非常口であって、正ではない。
    ///
    /// 画面の見た目はプレハブが唯一の正。Project ウィンドウでプレハブを開き、
    /// エディタ上で調整する。その調整がそのまま製品の姿になる。
    ///
    /// このツールはプレハブを丸ごと書き直すので、走らせると手で加えた調整が消える。
    /// 以前それで実機に合わせた調整を二度失った。だから
    ///
    ///   ・作り直しはバッチ実行では動かない（Editor のメニューからだけ）
    ///   ・メニューを選ぶと確認のダイアログが出る
    ///
    /// という二重の歯止めを掛けてある。外さないこと。
    /// 一から作り直す以外の目的で使う場面は無い。
    /// </summary>
    public static class UiPrefabBuilder
    {
        public const string Folder = "Assets/Resources/UI";
        public const string HeaderPath = Folder + "/HeaderBar.prefab";
        public const string SettingsPath = Folder + "/SettingsPanel.prefab";
        public const string ManualPath = Folder + "/ManualPanel.prefab";
        public const string ResultPath = Folder + "/ResultPanel.prefab";
        public const string FlickTuningPath = Folder + "/FlickTuningPanel.prefab";
        public const string FramePath = Folder + "/PictureFrame.png";

        /// <summary>Unity 組み込みの角丸スプライト。9 スライス済み。</summary>
        public const string ButtonSpritePath = "UI/Skin/UISprite.psd";
        public const string KnobSpritePath = "UI/Skin/Knob.psd";

        private const float ReferenceWidth = 1080f;
        private const float SideMargin = 36f;
        private const float RowHeight = 76f;
        private const float RowGap = 10f;

        /// <summary>
        /// 足りないプレハブだけを作る。既にあるものには触れない。
        ///
        /// プレハブはエディタ上で位置や大きさを直せるようにしてあるので、
        /// 作り直すとその手直しが消えてしまう。既定はこちらの安全な側にする。
        /// 全部作り直したいときは「UI プレハブを作り直す」を使う。
        /// </summary>
        [MenuItem("Pentomino/UI プレハブを生成（無い物だけ）")]
        public static void BuildMissing() => Run(false);

        /// <summary>
        /// 全部を作り直す。エディタ上で加えた手直しは失われる。
        /// </summary>
        [MenuItem("Pentomino/UI プレハブを作り直す（手直しは消えます）")]
        public static void RebuildAll()
        {
            // バッチ実行では動かさない。
            // 画面の見た目はプレハブが正で、そこに加えた調整はここを走らせると消える。
            // 人が画面を見て「作り直す」と答えたときだけ実行されるようにしておく。
            if (Application.isBatchMode)
            {
                Debug.LogError(
                    "UI プレハブの作り直しは、バッチ実行では行いません。\n"
                    + "プレハブに加えた調整が消えるためです。\n"
                    + "本当に一から作り直すなら、Unity を開いて "
                    + "メニュー Pentomino ▸ UI プレハブを作り直す を選んでください。");
                return;
            }

            if (File.Exists(HeaderPath) || File.Exists(SettingsPath))
            {
                if (!EditorUtility.DisplayDialog(
                        "UI プレハブを作り直す",
                        "既存のプレハブを作り直します。\n"
                        + "エディタ上で加えた調整は、すべて失われます。\n\n"
                        + "見た目を直したいだけなら、やめる を選んで\n"
                        + "プレハブを直接編集してください。",
                        "作り直す", "やめる")) return;
            }

            Run(true);
        }

        private static void Run(bool overwrite)
        {
            _overwrite = overwrite;
            _skipped.Clear();

            Directory.CreateDirectory(Folder);
            AssetDatabase.Refresh();   // 作ったフォルダを AssetDatabase に認識させる

            // 組み込みリソースは AssetDatabase 経由でないと取り出せない。
            UiFactory.DefaultButtonSprite = Builtin(ButtonSpritePath);
            if (UiFactory.DefaultButtonSprite == null)
                Debug.LogWarning("組み込みの角丸スプライトを取得できませんでした。平らなボタンになります。");

            DefaultKnobSprite = Builtin(KnobSpritePath);
            BuildHeader();
            BuildSettings();
            BuildManual();
            BuildResult();
            BuildFlickTuning();

            if (_skipped.Count > 0)
            {
                Debug.Log("既にあるので触れませんでした:\n  " + string.Join("\n  ", _skipped.ToArray())
                          + "\n作り直すなら メニュー Pentomino ▸ UI プレハブを作り直す");
            }
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log("UI プレハブを生成しました:\n" + HeaderPath + "\n" + SettingsPath
                      + "\n" + ManualPath + "\n" + ResultPath + "\n" + FlickTuningPath
                      + "\n\nProject ウィンドウでダブルクリックすると、見たまま編集できます。");
        }

        // ------------------------------------------------------------------ ナビゲーション

        /// <summary>
        /// 帯。割り付けと絵は HeaderLayoutMigration が持つ（2026-09-13 の案）。
        /// ここは保存と下見の扱いだけ。
        /// </summary>
        private static void BuildHeader()
        {
            if (!MayWrite(HeaderPath)) return;

            var root = HeaderLayoutMigration.BuildFresh(out var report);
            if (root == null) return;

            Save(root, HeaderPath);
            if (_dryRun != null) return;

            // 畳む面は保存したプレハブに足す。二度目は何もしないので、作り直しから
            // 続けて呼ばれても構わない。
            HeaderFoldMigration.Run();
            Debug.Log(report);
        }

        /// <summary>組み込みリソースを取り出す。エディタ専用。</summary>
        /// <summary>スライダのつまみに使う組み込みの丸いスプライト。</summary>
        private static Sprite DefaultKnobSprite { get; set; }

        private static Sprite DefaultButtonSprite => UiFactory.DefaultButtonSprite;

        private static Sprite Builtin(string path) =>
            AssetDatabase.GetBuiltinExtraResource<Sprite>(path);

        // ------------------------------------------------------------------ 設定画面

        private static void BuildSettings()
        {
            if (!MayWrite(SettingsPath)) return;

            var root = NewRect("SettingsPanel");
            root.anchorMin = Vector2.zero;
            root.anchorMax = Vector2.one;
            root.offsetMin = Vector2.zero;
            root.offsetMax = Vector2.zero;

            var background = root.gameObject.AddComponent<Image>();
            background.color = new Color32(0xF7, 0xFB, 0xFD, 0xFF);

            var panel = root.gameObject.AddComponent<SettingsPanel>();

            var margin = 44f;
            var width = ReferenceWidth - margin * 2f;
            var y = 44f;

            var title = Label(root, "Title", "設定", 72, margin, y, width, 100f);
            title.fontStyle = FontStyle.Bold;
            y += 150f;

            // ---- 言語が先（開発仕様「設定モード外観」）
            var languageLabel = Label(root, "LanguageLabel", "言語", 52, margin, y, width, 70f);
            languageLabel.fontStyle = FontStyle.Bold;
            y += 96f;

            var languageWidth = (width - 16f) / 2f;
            var languages = new List<Button>();
            for (var i = 0; i < Languages.All.Length; i++)
            {
                var language = Languages.All[i];
                var x = margin + (i % 2) * (languageWidth + 16f);
                var top = y + (i / 2) * 108f;
                var button = MakeButton(root, "Lang" + language, language.NativeName(), 40,
                    x, top, languageWidth, 96f);
                Bold(button);
                languages.Add(button);
            }
            y += (Languages.All.Length + 1) / 2 * 108f + 60f;

            // ---- 次に難易度。名前の下に一行の説明を添える。
            var difficultyLabel = Label(root, "DifficultyLabel", "難易度", 52, margin, y, width, 70f);
            difficultyLabel.fontStyle = FontStyle.Bold;
            y += 96f;

            var grades = new List<Button>();
            var notes = new List<Text>();
            for (var i = 0; i < SettingsPanel.Grades.Length; i++)
            {
                var grade = SettingsPanel.Grades[i];
                var top = y + i * 150f;

                var button = MakeButton(root, "Grade" + grade,
                    Strings.GradeName(grade, Language.Japanese), 44, margin, top, width, 96f);
                Bold(button);
                grades.Add(button);

                var note = Label(root, "Note" + grade,
                    Strings.GradeNote(grade, Language.Japanese), 32, margin + 24f, top + 100f, width - 24f, 44f);
                note.color = new Color(0.35f, 0.35f, 0.35f);
                notes.Add(note);
            }
            y += SettingsPanel.Grades.Length * 150f + 12f;

            var warning = Label(root, "Warning",
                Strings.Get(StringId.DifficultyWarning, Language.Japanese),
                32, margin, y, width, 96f);
            warning.color = new Color(0.72f, 0.12f, 0.12f);
            warning.alignment = TextAnchor.UpperLeft;
            warning.horizontalOverflow = HorizontalWrapMode.Wrap;
            warning.fontStyle = FontStyle.Bold;
            y += 130f;

            var tuning = MakeButton(root, "FlickTuning",
                Strings.Get(StringId.FlickTuning, Language.Japanese), 44, margin, y, width, 110f);
            Bold(tuning);
            y += 130f;

            var close = MakeButton(root, "Close", "閉じる", 44, margin, y, width, 110f);
            Bold(close);

            panel.Assign(title, difficultyLabel, warning, languageLabel,
                grades.ToArray(), notes.ToArray(), languages.ToArray(), tuning, close);

            Save(root.gameObject, SettingsPath);

            // 説明・音・帯の絵・履歴を消すボタン・問いかけは、割り付けの道具が足す。
            // ここで続けて呼ばないと、参照の空いたプレハブが残る。
            SettingsLayoutMigration.Run();
        }

        /// <summary>ボタンの文字を太字にする。開発仕様「文字はもう少し重くして下さい」。</summary>
        private static void Bold(Button button)
        {
            var label = button != null ? button.GetComponentInChildren<Text>(true) : null;
            if (label != null) label.fontStyle = FontStyle.Bold;
        }

        // ------------------------------------------------------------------ 説明書

        // 本文の見た目。プレハブ側でも変えられるが、作り直したときの既定値。
        private const int ManualBodyFontSize = 56;
        private const float ManualLineSpacing = 1.2f;
        private const float ManualRowHeight = 76f;

        private static void BuildManual()
        {
            if (!MayWrite(ManualPath)) return;

            var root = NewRect("ManualPanel");
            root.anchorMin = Vector2.zero;
            root.anchorMax = Vector2.one;
            root.offsetMin = Vector2.zero;
            root.offsetMax = Vector2.zero;

            var background = root.gameObject.AddComponent<Image>();
            background.color = new Color32(0xF7, 0xFB, 0xFD, 0xFF);

            var panel = root.gameObject.AddComponent<ManualPanel>();

            var margin = 44f;
            var width = ReferenceWidth - margin * 2f;

            var title = Label(root, "Title", "説明", 72, margin, 40f, width, 100f);
            title.fontStyle = FontStyle.Bold;

            // 閉じるボタンは下端に固定して、いつでも押せるようにする。
            var close = MakeButton(root, "Close", "閉じる", 44, margin, 0f, width, 110f);
            Bold(close);
            var closeRect = (RectTransform)close.transform;
            closeRect.anchorMin = new Vector2(0f, 0f);
            closeRect.anchorMax = new Vector2(0f, 0f);
            closeRect.pivot = new Vector2(0f, 0f);
            closeRect.anchoredPosition = new Vector2(margin, 40f);

            // 本文はスクロールして読む。
            var scrollGo = new GameObject("Scroll", typeof(RectTransform), typeof(ScrollRect));
            scrollGo.transform.SetParent(root, false);

            var scrollRect = (RectTransform)scrollGo.transform;
            scrollRect.anchorMin = new Vector2(0f, 0f);
            scrollRect.anchorMax = new Vector2(1f, 1f);
            scrollRect.offsetMin = new Vector2(margin, 190f);
            scrollRect.offsetMax = new Vector2(-margin, -170f);

            var viewport = UiFactory.CreateRect(scrollRect, "Viewport");
            viewport.anchorMin = Vector2.zero;
            viewport.anchorMax = Vector2.one;
            viewport.offsetMin = Vector2.zero;
            viewport.offsetMax = Vector2.zero;
            viewport.gameObject.AddComponent<RectMask2D>();

            var touch = viewport.gameObject.AddComponent<Image>();
            touch.color = new Color(0f, 0f, 0f, 0f);

            // 本文 → 表 → 本文 を縦に積む。高さは中身に合わせて自動で伸びる。
            var content = UiFactory.CreateRect(viewport, "Content");
            content.anchorMin = new Vector2(0f, 1f);
            content.anchorMax = new Vector2(1f, 1f);
            content.pivot = new Vector2(0.5f, 1f);
            content.offsetMin = new Vector2(0f, 0f);
            content.offsetMax = new Vector2(0f, 0f);

            var layout = content.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            layout.spacing = 24f;

            var fitter = content.gameObject.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            var bodyTop = FlowText(content, "BodyTop");
            var table = BuildManualTable(content, out var rowTemplate);
            var bodyBottom = FlowText(content, "BodyBottom");

            var scroll = scrollGo.GetComponent<ScrollRect>();
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.viewport = viewport;
            scroll.content = content;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 40f;

            panel.Assign(title, bodyTop, bodyBottom, table, rowTemplate, close, scroll);

            if (!panel.Validate(out var missing))
                Debug.LogError("ManualPanel の参照が足りません: " + missing);

            Save(root.gameObject, ManualPath);
        }

        /// <summary>縦積みの中に置く、高さが中身に追従する本文。</summary>
        private static Text FlowText(RectTransform parent, string name)
        {
            var text = UiFactory.CreateLabel(parent, name, "", ManualBodyFontSize, TextAnchor.UpperLeft);
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            text.lineSpacing = ManualLineSpacing;

            var fitter = text.gameObject.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            return text;
        }

        /// <summary>レベル別の問題数を並べる 3 列の表。</summary>
        private static RectTransform BuildManualTable(RectTransform parent, out ManualTableRow rowTemplate)
        {
            var table = UiFactory.CreateRect(parent, "Table");

            var layout = table.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            layout.spacing = 0f;

            var fitter = table.gameObject.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            rowTemplate = MakeTableRow(table, "RowTemplate");
            return table;
        }

        private static ManualTableRow MakeTableRow(RectTransform parent, string name)
        {
            var row = UiFactory.CreateRect(parent, name);
            row.sizeDelta = new Vector2(0f, ManualRowHeight);

            var element = row.gameObject.AddComponent<LayoutElement>();
            element.preferredHeight = ManualRowHeight;

            var component = row.gameObject.AddComponent<ManualTableRow>();

            // 1 行おきに敷く薄い地色。いちばん奥に置く。
            var background = UiFactory.CreateRect(row, "Background");
            background.anchorMin = Vector2.zero;
            background.anchorMax = Vector2.one;
            background.offsetMin = Vector2.zero;
            background.offsetMax = Vector2.zero;

            var backgroundImage = background.gameObject.AddComponent<Image>();
            backgroundImage.color = new Color32(0xE6, 0xEE, 0xF4, 0xFF);
            backgroundImage.raycastTarget = false;

            // 見出しの下と合計の上に出す区切り線。
            var rule = UiFactory.CreateRect(row, "Rule");
            rule.anchorMin = new Vector2(0f, 1f);
            rule.anchorMax = new Vector2(1f, 1f);
            rule.pivot = new Vector2(0.5f, 1f);
            rule.offsetMin = new Vector2(0f, 0f);
            rule.offsetMax = new Vector2(0f, 0f);
            rule.sizeDelta = new Vector2(0f, 3f);

            var ruleImage = rule.gameObject.AddComponent<Image>();
            ruleImage.color = new Color(0.45f, 0.45f, 0.45f);
            ruleImage.raycastTarget = false;

            // 3 列。数字は右揃えにすると桁が縦に並ぶ。
            var level = Column(row, "Level", 0f, 0.30f, TextAnchor.MiddleLeft);
            var count = Column(row, "Count", 0.30f, 0.65f, TextAnchor.MiddleRight);
            var share = Column(row, "Share", 0.65f, 1f, TextAnchor.MiddleRight);

            component.Assign(level, count, share, backgroundImage, ruleImage);
            return component;
        }

        private static Text Column(RectTransform parent, string name, float min, float max, TextAnchor anchor)
        {
            var text = UiFactory.CreateLabel(parent, name, "", ManualBodyFontSize, anchor);
            var rect = (RectTransform)text.transform;
            rect.anchorMin = new Vector2(min, 0f);
            rect.anchorMax = new Vector2(max, 1f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.offsetMin = new Vector2(16f, 0f);
            rect.offsetMax = new Vector2(-16f, 0f);
            return text;
        }

        // ------------------------------------------------------------------ おめでとうパネル

        // 額縁の幅。絵そのものは 892 だが、文字が窮屈だったので実機に合わせて広げてある。
        // 高さは絵の縦横比から計算する。直書きすると絵を差し替えたときにずれる。
        private const float ResultWidth = 1000f;

        // 絵が読めなかったときのための、元の絵の大きさ。
        private const float ResultFallbackWidth = 892f;
        private const float ResultFallbackHeight = 775f;

        private static void BuildResult()
        {
            if (!MayWrite(ResultPath)) return;

            var root = NewRect("ResultPanel");

            var frameImage = root.gameObject.AddComponent<Image>();
            var frame = AssetDatabase.LoadAssetAtPath<Sprite>(FramePath);
            if (frame != null)
            {
                frameImage.sprite = frame;
                frameImage.type = Image.Type.Simple;   // 元の縦横比のまま出す
                frameImage.color = Color.white;
                // 幅だけ決め、高さは絵の縦横比のまま伸ばす。絵が歪まない。
                root.sizeDelta = new Vector2(
                    ResultWidth, ResultWidth * frame.rect.height / frame.rect.width);
            }
            else
            {
                Debug.LogWarning("額縁の絵が見つかりません: " + FramePath);
                frameImage.color = new Color32(0xFF, 0xC0, 0x00, 0xFF);
                root.sizeDelta = new Vector2(
                    ResultWidth, ResultWidth * ResultFallbackHeight / ResultFallbackWidth);
            }

            var panel = root.gameObject.AddComponent<ResultPanel>();

            // 額縁の内側に文字を置く。位置はプレハブ上で自由に動かせる。
            // 縦の位置はアンカー（親に対する割合）だけで決める。
            // ピクセルのずらし幅で持つと、額縁の大きさを変えたときに崩れてしまう。
            // 下の数値は実機で合わせた見た目を、割合に直したもの。
            // 換算の基準は、合わせたときの額縁の高さ 775。
            var congratulations = Inside(root, "Congratulations", "おめでとう", 84,
                0.14f, 0.5800f, 0.86f, 0.7800f);
            congratulations.color = new Color(0.85f, 0.10f, 0.10f);
            congratulations.fontStyle = FontStyle.Bold;

            var grade = Inside(root, "Grade", "クラシック", 72,
                0.14f, 0.4402f, 0.86f, 0.6202f);
            grade.fontStyle = FontStyle.Bold;

            var hint = Inside(root, "Hint", "Hint   0", 64,
                0.14f, 0.3205f, 0.86f, 0.4605f);
            hint.fontStyle = FontStyle.Bold;

            var check = Inside(root, "Check", "Check   0", 64,
                0.14f, 0.2006f, 0.86f, 0.3406f);
            check.fontStyle = FontStyle.Bold;

            panel.Assign(frameImage, congratulations, grade, hint, check);

            if (!panel.Validate(out var missing))
                Debug.LogError("ResultPanel の参照が足りません: " + missing);

            Save(root.gameObject, ResultPath);
        }

        /// <summary>
        /// 額縁の内側に、割合で位置を決めて文字を置く。
        /// </summary>
        private static Text Inside(RectTransform parent, string name, string content, int fontSize,
            float xMin, float yMin, float xMax, float yMax)
        {
            var text = UiFactory.CreateLabel(parent, name, content, fontSize, TextAnchor.MiddleCenter);
            var rect = (RectTransform)text.transform;
            rect.anchorMin = new Vector2(xMin, yMin);
            rect.anchorMax = new Vector2(xMax, yMax);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            return text;
        }

        // ------------------------------------------------------------------ 感度調整

        private const float TuneRowHeight = 150f;

        /// <summary>
        /// フリックの感度を実機で詰めるための画面。
        /// スライダ 4 本と、実際に滑らせて試せる練習用のピースを置く。
        /// </summary>
        private static void BuildFlickTuning()
        {
            if (!MayWrite(FlickTuningPath)) return;

            var root = NewRect("FlickTuningPanel");
            root.anchorMin = Vector2.zero;
            root.anchorMax = Vector2.one;
            root.offsetMin = Vector2.zero;
            root.offsetMax = Vector2.zero;

            var background = root.gameObject.AddComponent<Image>();
            background.color = new Color32(0xF7, 0xFB, 0xFD, 0xFF);

            var panel = root.gameObject.AddComponent<FlickTuningPanel>();

            var margin = 44f;
            var width = ReferenceWidth - margin * 2f;

            var title = Label(root, "Title", Strings.Get(StringId.FlickTuning, Language.Japanese),
                72, margin, 40f, width, 100f);
            title.fontStyle = FontStyle.Bold;

            // スライダ 4 本。名前・つまみ・数値の 3 つで 1 行。
            var names = new[]
            {
                StringId.FlickReleaseSpeed, StringId.FlickFilter,
                StringId.FlickStill, StringId.FlickMinTravel,
                StringId.FlickAxisRatio,
                StringId.VibrationLength, StringId.VibrationStrength,
            };

            var sliders = new Slider[names.Length];
            var labels = new Text[names.Length];
            var values = new Text[names.Length];

            var y = 180f;
            for (var i = 0; i < names.Length; i++)
            {
                var top = y + i * TuneRowHeight;

                labels[i] = Label(root, "Label" + i, Strings.Get(names[i], Language.Japanese),
                    44, margin, top, width * 0.6f, 60f);

                values[i] = Label(root, "Value" + i, "-", 44, margin + width * 0.6f, top,
                    width * 0.4f, 60f);
                values[i].alignment = TextAnchor.MiddleRight;
                values[i].fontStyle = FontStyle.Bold;

                sliders[i] = MakeSlider(root, "Slider" + i, margin, top + 70f, width, 60f);
            }

            y += names.Length * TuneRowHeight + 20f;

            var tryLabel = Label(root, "TryLabel", Strings.Get(StringId.FlickTry, Language.Japanese),
                44, margin, y, width, 60f);
            tryLabel.color = new Color(0.35f, 0.35f, 0.35f);
            y += 80f;

            // 練習用のピースを置く枠。ここで実際に滑らせて確かめる。
            var arena = UiFactory.CreateRect(root, "Arena");
            arena.anchoredPosition = new Vector2(margin, -y);
            arena.sizeDelta = new Vector2(width, 420f);

            var arenaImage = arena.gameObject.AddComponent<Image>();
            arenaImage.color = new Color32(0xE6, 0xEE, 0xF4, 0xFF);

            var sampleGo = new GameObject("Sample", typeof(RectTransform), typeof(CanvasRenderer),
                typeof(PolyominoGraphic), typeof(PolyominoView), typeof(FlickSample));
            sampleGo.transform.SetParent(arena, false);

            var sampleRect = (RectTransform)sampleGo.transform;
            sampleRect.anchorMin = sampleRect.anchorMax = new Vector2(0.5f, 0.5f);
            sampleRect.pivot = new Vector2(0.5f, 0.5f);
            sampleRect.anchoredPosition = Vector2.zero;

            var sample = sampleGo.GetComponent<FlickSample>();
            y += 450f;

            var result = Label(root, "Result", "-", 40, margin, y, width, 60f);
            result.fontStyle = FontStyle.Bold;
            y += 90f;

            var reset = MakeButton(root, "Reset",
                Strings.Get(StringId.ResetToDefault, Language.Japanese), 44, margin, y, width, 110f);
            Bold(reset);
            y += 130f;

            var close = MakeButton(root, "Close", "閉じる", 44, margin, y, width, 110f);
            Bold(close);

            panel.Assign(title, tryLabel, result, sliders, labels, values, sample, reset, close);


            if (!panel.Validate(out var missing))
                Debug.LogError("FlickTuningPanel の参照が足りません: " + missing);

            Save(root.gameObject, FlickTuningPath);
        }

        /// <summary>つまみを動かせる横向きのスライダ。</summary>
        /// <summary>
        /// 組み込みのスプライトを用意する。移行ツールから単体で MakeSlider を
        /// 呼ぶときに要る。生成の入口を通っていないと、つまみの絵が空になる。
        /// </summary>
        internal static void LoadSprites()
        {
            if (UiFactory.DefaultButtonSprite == null)
                UiFactory.DefaultButtonSprite = Builtin(ButtonSpritePath);

            if (DefaultKnobSprite == null) DefaultKnobSprite = Builtin(KnobSpritePath);
        }

        /// <summary>
        /// つまみを作る。移行ツール（FlickTuningLayoutMigration）からも使うので
        /// internal にしてある。先に LoadSprites を呼ぶこと。
        /// </summary>
        internal static Slider MakeSlider(RectTransform parent, string name,
            float x, float y, float width, float height)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Slider));
            go.transform.SetParent(parent, false);

            var rect = (RectTransform)go.transform;
            rect.anchorMin = rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = new Vector2(x, -y);
            rect.sizeDelta = new Vector2(width, height);

            // 溝
            var background = UiFactory.CreateRect(rect, "Background");
            background.anchorMin = new Vector2(0f, 0.35f);
            background.anchorMax = new Vector2(1f, 0.65f);
            background.offsetMin = Vector2.zero;
            background.offsetMax = Vector2.zero;

            var backgroundImage = background.gameObject.AddComponent<Image>();
            backgroundImage.sprite = DefaultButtonSprite;
            backgroundImage.type = Image.Type.Sliced;
            backgroundImage.color = new Color32(0xD0, 0xD8, 0xDE, 0xFF);

            // 溝のうち、いまの値まで塗る部分
            var fillArea = UiFactory.CreateRect(rect, "Fill Area");
            fillArea.anchorMin = new Vector2(0f, 0.35f);
            fillArea.anchorMax = new Vector2(1f, 0.65f);
            fillArea.offsetMin = new Vector2(height * 0.5f, 0f);
            fillArea.offsetMax = new Vector2(-height * 0.5f, 0f);

            var fill = UiFactory.CreateRect(fillArea, "Fill");
            fill.anchorMin = Vector2.zero;
            fill.anchorMax = new Vector2(0f, 1f);
            fill.offsetMin = new Vector2(-height * 0.5f, 0f);
            fill.offsetMax = new Vector2(height * 0.5f, 0f);

            var fillImage = fill.gameObject.AddComponent<Image>();
            fillImage.sprite = DefaultButtonSprite;
            fillImage.type = Image.Type.Sliced;
            fillImage.color = new Color32(0x5A, 0xA8, 0xE0, 0xFF);

            // つまみ。指で掴むので大きめにする。
            var handleArea = UiFactory.CreateRect(rect, "Handle Slide Area");
            handleArea.anchorMin = Vector2.zero;
            handleArea.anchorMax = Vector2.one;
            handleArea.offsetMin = new Vector2(height * 0.5f, 0f);
            handleArea.offsetMax = new Vector2(-height * 0.5f, 0f);

            var handle = UiFactory.CreateRect(handleArea, "Handle");
            handle.anchorMin = new Vector2(0f, 0f);
            handle.anchorMax = new Vector2(0f, 1f);
            handle.sizeDelta = new Vector2(height, 0f);

            var handleImage = handle.gameObject.AddComponent<Image>();
            handleImage.sprite = DefaultKnobSprite;
            handleImage.color = Color.white;

            var slider = go.GetComponent<Slider>();
            slider.fillRect = fill;
            slider.handleRect = handle;
            slider.targetGraphic = handleImage;
            slider.direction = Slider.Direction.LeftToRight;
            return slider;
        }

        // ------------------------------------------------------------------ 部品作り        // ------------------------------------------------------------------ 部品作り

        private static RectTransform NewRect(string name)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rect = (RectTransform)go.transform;
            rect.anchorMin = rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            return rect;
        }

        private static Text Label(RectTransform parent, string name, string content, int fontSize,
            float x, float y, float width, float height = RowHeight)
        {
            var text = UiFactory.CreateLabel(parent, name, content, fontSize);
            var rect = (RectTransform)text.transform;
            rect.anchoredPosition = new Vector2(x, -y);
            rect.sizeDelta = new Vector2(width, height);
            return text;
        }

        private static Button MakeButton(RectTransform parent, string name, string caption, int fontSize,
            float x, float y, float width, float height = RowHeight)
        {
            var button = UiFactory.CreateButton(parent, name, caption, fontSize,
                new Vector2(width, height), null);
            ((RectTransform)button.transform).anchoredPosition = new Vector2(x, -y);
            return button;
        }

        private static bool _overwrite;
        private static readonly List<string> _skipped = new List<string>();

        /// <summary>この道具がこのプレハブに書き込んでよいか。</summary>
        private static bool MayWrite(string path)
        {
            if (_overwrite || !File.Exists(path)) return true;

            _skipped.Add(path);
            return false;
        }

        private static void Save(GameObject go, string path)
        {
            // 下見のときは保存せず、比べるために取っておく。
            if (_dryRun != null)
            {
                _dryRun[path] = go;
                return;
            }

            PrefabUtility.SaveAsPrefabAsset(go, path);
            Object.DestroyImmediate(go);
        }

        /// <summary>
        /// 保存せずに組み立てて、「いま生成したらこうなる」という姿を返す。
        ///
        /// 既存のプレハブと見比べて、手直しされた箇所を見つけるために使う。
        /// 返した GameObject は呼び出し側が片づける。
        /// </summary>
        internal static Dictionary<string, GameObject> BuildForComparison()
        {
            var built = new Dictionary<string, GameObject>();

            _dryRun = built;
            _overwrite = true;      // 下見なので、既にあっても組み立てる
            _skipped.Clear();

            try
            {
                UiFactory.DefaultButtonSprite = Builtin(ButtonSpritePath);
                DefaultKnobSprite = Builtin(KnobSpritePath);

                BuildHeader();
                BuildSettings();
                BuildManual();
                BuildResult();
                BuildFlickTuning();
            }
            finally
            {
                _dryRun = null;
            }

            return built;
        }

        /// <summary>下見の置き場。null でなければ保存せずここに溜める。</summary>
        private static Dictionary<string, GameObject> _dryRun;
    }
}
