using System.Collections.Generic;
using System.IO;
using System.Text;
using Pentomino.Core;
using Pentomino.View;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace Pentomino.EditorTools
{
    /// <summary>
    /// 帯を 2026-09-13 の案（DataBase/20260914R/12_9月14日.png）に組み直す。
    ///
    /// 基準は幅 1080・高さ 2340。帯は上から
    ///
    ///   0〜250   : 緩衝。でっぱりの逃げ。右上に歯車だけ（幅 140、X 800、Y 60）
    ///   250〜450 : タイトルの絵（2160x400 を半分に）
    ///   450〜580 : Check / Hint（130x80）、← / →（100x80）、級の帯（540x80）
    ///   580〜700 : Check / Hint の回数、問題番号、レベルの絵（540x80）
    ///
    /// 翌 09-14 に、緩衝を 300 から 250 に詰めて全体を 50 上げ、歯車を小さくし、
    /// レベルも級の帯と同じ書体の絵にした（1〜10 x 3 級で 30 枚）。
    ///
    /// 帯の中身はプレハブが正だが、この案は割り付けそのものが変わるので、
    /// 帯のプレハブは一度捨てて、ここで組み直す。以前の帯に加えた手直しは残らない。
    /// 二度目からは、組み直したあとの手直しを守るために、何もしない。
    /// もう一度組み直したければ、プレハブを消してから実行する。
    ///
    /// 絵の原稿は DataBase（SourceFolder）にあり、Git には入らない。
    /// ここで Assets/Resources/UI/Header へ写してからプレハブに差す。
    ///
    /// プレハブは、左右の余白を除いた幅（1080 - 18 x 2 = 1044）で組む。
    /// GameScreen が BOX と同じ幅に縮尺を合わせるので、余白の設定を変えても
    /// ボタンの左端は BOX の左端に揃う。タイトルの絵だけは余白のぶん左右へ
    /// はみ出させ、画面の端まで届かせる。絵の地は画面の地と同じ色なので継ぎ目は出ない。
    /// </summary>
    public static class HeaderLayoutMigration
    {
        private const string PrefabPath = UiPrefabBuilder.HeaderPath;

        /// <summary>
        /// 絵の原稿。Git には入らない Data_5x6x2 側にある。
        ///
        ///   IMG333333R 黒帯の地が #333333 の版。6X10 の最終版 2026_BB222222R と同じ組で、
        ///              タイトルの字と黒帯の地の色だけが違う
        ///
        /// 地の色は GradeTheme.Black と対にして変える。食い違うと、写すときに
        /// 地を置き換えた警告が出るので気づける。
        /// </summary>
        private const string SourceFolder = "Data_5x6x2/IMG333333R";

        /// <summary>写し先。プレハブから参照するので Assets の中に置く。</summary>
        public const string ArtFolder = "Assets/Resources/UI/Header";

        // ---- 案の寸法（幅 1080 基準）
        private const float DesignWidth = 1080f;

        /// <summary>
        /// 左右の余白。場面（Game.unity）の GameScreen._sideMargin と同じ値。
        /// 案のボタンの左端が 18 なので、これに合わせてある。
        /// </summary>
        private const float SideMargin = 18f;

        private const float ContentWidth = DesignWidth - SideMargin * 2f;   // 1044
        private const float HeaderHeight = 700f;

        private const float GearSize = 140f;
        private const float GearX = 800f;       // 案の値（画面の左端から）
        private const float GearY = 60f;

        private const float TitleTop = 250f;
        private const float TitleHeight = 200f;

        private const float ButtonRowTop = 450f;
        private const float ButtonRowHeight = 130f;
        private const float ButtonHeight = 80f;
        private const float WideButtonWidth = 130f;   // Check / Hint
        private const float StepButtonWidth = 100f;   // ← / →
        /// <summary>ボタンの間隔。</summary>
        private const float ButtonGap = 12f;

        /// <summary>
        /// ← の左と → の左に、さらに空ける量。
        /// Hint の隣で ← を押そうとして Hint を押してしまうことがあったので、
        /// 矢印を Hint から離す。
        /// </summary>
        private const float BackGapExtra = 30f;
        private const float ForwardGapExtra = 10f;

        /// <summary>級の帯とレベルの絵の枠。絵（2160x320）と同じ比なので、枠いっぱいに出る。</summary>
        private const float GradeWidth = 540f;

        /// <summary>
        /// 級の帯の絵の、左端の字の無い幅。帯は BOX の右端に揃えるので、
        /// この幅のぶんだけは → の下に潜り込んでよい。
        /// 絵は不透明なので、ボタンより先に描く（兄弟の順で手前に来ないようにする）。
        /// </summary>
        private const float GradeBlankLeft = 50f;

        private const float NumberRowTop = 580f;
        private const float NumberRowHeight = 120f;
        private const float CounterHeight = 80f;

        // 09-14 に Inspector で合わせてもらった微調整。段の割り付けとは別に持つ。
        /// <summary>級の帯を、ボタンよりこれだけ下げる。</summary>
        private const float GradeDrop = 5f;

        /// <summary>問題番号を、← の左端よりこれだけ右へ、段の上端よりこれだけ上へ。</summary>
        private const float NumberIndent = 20f;
        private const float NumberLift = 5f;

        private const int NumberFontSize = 88;
        private const int CounterFontSize = 64;

        /// <summary>焼き込む大きさ。画面に出る大きさの 2 倍ほどあれば足りる。</summary>
        private const int TitleTextureSize = 2048;
        private const int GradeTextureSize = 1024;
        private const int ButtonTextureSize = 512;

        /// <summary>級の帯の原稿の名前。言語コードと原稿の綴りの対応。</summary>
        private static readonly (Language Language, string Art)[] GradeLanguages =
        {
            (Language.English, "eng"),
            (Language.Japanese, "jp"),
            (Language.Spanish, "spanish"),
            (Language.French, "french"),
            (Language.German, "german"),
            (Language.Korean, "korean"),
            (Language.ChineseSimplified, "simple"),
            (Language.ChineseTraditional, "traditional"),
        };

        private static readonly string[] Belts = { "white", "yellow", "black" };

        [MenuItem("Pentomino/帯を 2026-09-13 の案に組み直す")]
        public static void Run()
        {
            if (File.Exists(PrefabPath))
            {
                var existing = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
                var header = existing != null ? existing.GetComponent<HeaderBar>() : null;
                if (header != null && header.Validate(out _))
                {
                    Debug.Log("帯は既にこの案で組んであります。何もしませんでした。\n"
                              + "組み直したければ " + PrefabPath + " を消してから実行してください。");
                    return;
                }
            }

            var root = BuildFresh(out var report);
            if (root == null) return;

            Directory.CreateDirectory(Path.GetDirectoryName(PrefabPath));
            PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            Object.DestroyImmediate(root);

            // タイトル以下を畳む面。Pane が無ければ足す。
            HeaderFoldMigration.Run();

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log(report);
        }

        /// <summary>
        /// 原稿を写し、帯を組んで返す。保存はしない。
        /// UiPrefabBuilder からも呼ぶ（作り直しと、手直しを見つけるための下見）。
        /// 返した GameObject は呼び出し側が片づける。失敗なら null。
        /// </summary>
        internal static GameObject BuildFresh(out string report)
        {
            report = null;

            // 回数の枠に使う角丸は Unity 組み込みで、エディタからしか取り出せない。
            if (UiFactory.DefaultButtonSprite == null)
                UiFactory.DefaultButtonSprite =
                    AssetDatabase.GetBuiltinExtraResource<Sprite>(UiPrefabBuilder.ButtonSpritePath);

            var sprites = CopyArt();
            if (sprites == null) return null;

            return Build(sprites, out report);
        }

        // ------------------------------------------------------------------ 絵

        /// <summary>原稿を Assets へ写し、スプライトとして読み込ませる。</summary>
        private static Dictionary<string, Sprite> CopyArt()
        {
            Directory.CreateDirectory(ArtFolder);

            // 透ける絵（歯車・ボタン）は地の色を持たない。
            // 帯の絵（タイトル・級・レベル）は不透明で、地が画面の地と同じ色でなければ
            // 四角い継ぎ目が見える。どの級の地かも一緒に持つ。
            var wanted = new List<(string Source, string Destination, int MaxSize, string Belt)>
            {
                ("gear-dark", "Gear-Dark", ButtonTextureSize, null),
                ("gear-light", "Gear-Light", ButtonTextureSize, null),
                ("button_check", "Button-Check", ButtonTextureSize, null),
                ("button_hint", "Button-Hint", ButtonTextureSize, null),
                ("button_backward", "Button-Back", ButtonTextureSize, null),
                ("button_forward", "Button-Forward", ButtonTextureSize, null),
            };
            foreach (var belt in Belts)
                wanted.Add(("title-" + belt, "Title-" + belt, TitleTextureSize, belt));
            foreach (var entry in GradeLanguages)
                foreach (var belt in Belts)
                    wanted.Add(("grade-" + entry.Art + "-" + belt, GradeName(entry.Language, belt), GradeTextureSize, belt));
            for (var level = 1; level <= HeaderBar.LevelArtCount; level++)
                foreach (var belt in Belts)
                    wanted.Add(("level" + level + "-" + belt, LevelName(level, belt), GradeTextureSize, belt));

            var sprites = new Dictionary<string, Sprite>();
            var retinted = new List<string>();

            foreach (var item in wanted)
            {
                var source = FindSource(item.Source);
                if (source == null)
                {
                    Debug.LogError(SourceFolder + " に " + item.Source + ".png がありません。");
                    return null;
                }

                var destination = ArtFolder + "/" + item.Destination + ".png";
                File.Copy(source, destination, true);
                if (item.Belt != null && MatchGround(destination, GroundOf(item.Belt)))
                    retinted.Add(item.Source + ".png");
                AssetDatabase.ImportAsset(destination);

                var importer = AssetImporter.GetAtPath(destination) as TextureImporter;
                if (importer != null)
                {
                    importer.textureType = TextureImporterType.Sprite;
                    importer.spriteImportMode = SpriteImportMode.Single;
                    importer.mipmapEnabled = false;
                    importer.maxTextureSize = item.MaxSize;
                    importer.alphaIsTransparency = true;
                    importer.SaveAndReimport();
                }

                var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(destination);
                if (sprite == null)
                {
                    Debug.LogError(destination + " をスプライトとして読めません。");
                    return null;
                }

                sprites[item.Destination] = sprite;
            }

            if (retinted.Count > 0)
                Debug.LogWarning("地の色が画面の地と違ったので、写すときに置き換えました。"
                                 + "原稿のほうを直すと、字の縁のにじみも消えます:\n  "
                                 + string.Join("\n  ", retinted));

            return sprites;
        }

        /// <summary>帯の名前から、その級の画面の地の色。</summary>
        private static Color32 GroundOf(string belt)
        {
            switch (belt)
            {
                case "yellow": return GradeTheme.Yellow;
                case "black": return GradeTheme.Black;
                default: return GradeTheme.White;
            }
        }

        /// <summary>
        /// 不透明な帯の絵の地を、画面の地の色に合わせる。
        ///
        /// 左上の画素を地とみなし、それが画面の地と違っていれば、同じ色の画素を
        /// すべて画面の地に置き換える。白帯のレベルの絵が #FFFFFF で描かれていて、
        /// #F0F0F0 の画面に白い四角として浮いたことがあった。
        ///
        /// 字の縁（地と字の中間の色）はそのまま残るので、わずかににじむ。
        /// 応急の手当てであって、原稿を直すのが本筋。置き換えたら true。
        /// </summary>
        private static bool MatchGround(string path, Color32 ground)
        {
            var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            try
            {
                if (!texture.LoadImage(File.ReadAllBytes(path))) return false;

                var pixels = texture.GetPixels32();
                if (pixels.Length == 0) return false;

                // GetPixels32 は下の行から並ぶ。左上は最後の行の先頭。
                var corner = pixels[(texture.height - 1) * texture.width];
                if (corner.a != 255 || Same(corner, ground)) return false;

                for (var i = 0; i < pixels.Length; i++)
                    if (Same(pixels[i], corner)) pixels[i] = ground;

                texture.SetPixels32(pixels);
                File.WriteAllBytes(path, texture.EncodeToPNG());
                return true;
            }
            finally
            {
                Object.DestroyImmediate(texture);
            }
        }

        private static bool Same(Color32 a, Color32 b) =>
            a.r == b.r && a.g == b.g && a.b == b.b && a.a == b.a;

        private static string GradeName(Language language, string belt) =>
            "Grade-" + language.ToCode() + "-" + belt;

        private static string LevelName(int level, string belt) =>
            "Level-" + level.ToString("00") + "-" + belt;

        /// <summary>
        /// 原稿を探す。大文字小文字と、- と _ の違いを吸収する
        /// （grade-simple_black.png のように綴りが揃っていないため）。
        /// </summary>
        private static string FindSource(string name)
        {
            var folder = Path.Combine(Directory.GetCurrentDirectory(), SourceFolder);
            if (!Directory.Exists(folder)) return null;

            var wanted = Normalize(name);
            foreach (var path in Directory.GetFiles(folder))
            {
                if (Path.GetExtension(path).ToLowerInvariant() != ".png") continue;
                if (Normalize(Path.GetFileNameWithoutExtension(path)) != wanted) continue;
                return path;
            }
            return null;
        }

        private static string Normalize(string name) => name.ToLowerInvariant().Replace('_', '-');

        // ------------------------------------------------------------------ 組み立て

        private static GameObject Build(Dictionary<string, Sprite> sprites, out string report)
        {
            report = null;

            var root = UiFactory.CreateRect(null, "HeaderBar");
            root.sizeDelta = new Vector2(ContentWidth, HeaderHeight);

            var header = root.gameObject.AddComponent<HeaderBar>();

            // ---- 緩衝の右上: 歯車。畳んでも動かない（名前 Settings で HeaderFoldMigration が判る）。
            var settings = ImageButton(root, "Settings", sprites["Gear-Dark"],
                GearX - SideMargin, GearY, GearSize, GearSize);
            var gear = settings.GetComponent<Image>();

            // ---- タイトルの絵。余白のぶん左右へはみ出させ、画面の端まで届かせる。
            var title = UiFactory.CreateRect(root, "Title");
            title.anchoredPosition = new Vector2(-SideMargin, -TitleTop);
            title.sizeDelta = new Vector2(DesignWidth, TitleHeight);

            var titleImage = title.gameObject.AddComponent<Image>();
            titleImage.sprite = sprites["Title-yellow"];
            titleImage.type = Image.Type.Simple;
            titleImage.preserveAspect = false;
            titleImage.color = Color.white;
            titleImage.raycastTarget = true;      // 触れないと長押しできない
            var longPress = title.gameObject.AddComponent<LongPressHandler>();

            // ---- ボタンの段。左から Check / Hint / ← / →、その右に級の帯。
            var buttonY = ButtonRowTop + (ButtonRowHeight - ButtonHeight) * 0.5f;
            var x = 0f;
            var check = ImageButton(root, "Check", sprites["Button-Check"], x, buttonY, WideButtonWidth, ButtonHeight);
            var checkX = x;
            x += WideButtonWidth + ButtonGap;
            var hint = ImageButton(root, "Hint", sprites["Button-Hint"], x, buttonY, WideButtonWidth, ButtonHeight);
            var hintX = x;
            x += WideButtonWidth + ButtonGap + BackGapExtra;
            var back = ImageButton(root, "StepBack", sprites["Button-Back"], x, buttonY, StepButtonWidth, ButtonHeight);
            var numberX = x;
            x += StepButtonWidth + ButtonGap + ForwardGapExtra;
            var forward = ImageButton(root, "StepForward", sprites["Button-Forward"], x, buttonY, StepButtonWidth, ButtonHeight);
            x += StepButtonWidth;
            var numberWidth = x - numberX;

            // 級の帯は BOX の右端に揃える。字の無い左端だけは → の下に潜ってよい。
            var gradeX = ContentWidth - GradeWidth;
            if (gradeX + GradeBlankLeft < x)
            {
                Debug.LogError("級の帯（" + GradeWidth + "）の字がボタンと重なります。間隔を詰めてください。");
                Object.DestroyImmediate(root.gameObject);
                return null;
            }

            var stepBack = back.gameObject.AddComponent<StepButton>();
            stepBack.Direction = -1;
            var stepForward = forward.gameObject.AddComponent<StepButton>();
            stepForward.Direction = 1;

            var grade = UiFactory.CreateRect(root, "Grade");
            grade.anchoredPosition = new Vector2(gradeX, -(buttonY + GradeDrop));
            grade.sizeDelta = new Vector2(GradeWidth, ButtonHeight);

            // 不透明な絵なので、ボタンより奥に。→ と重なる左端は、ボタンのほうが手前に出る。
            grade.SetSiblingIndex(check.transform.GetSiblingIndex());

            var gradeImage = grade.gameObject.AddComponent<Image>();
            gradeImage.sprite = sprites[GradeName(Language.English, "yellow")];
            gradeImage.type = Image.Type.Simple;
            gradeImage.preserveAspect = true;     // 6.75:1 の絵。枠も同じ比だが、絵を替えても歪まないように
            gradeImage.color = Color.white;
            gradeImage.raycastTarget = false;

            // ---- 数字の段。ボタンの真下に、それぞれの数字。
            var counterY = NumberRowTop + (NumberRowHeight - CounterHeight) * 0.5f;
            var checkCount = CounterBox(root, "CheckCount", checkX, counterY);
            var hintCount = CounterBox(root, "HintCount", hintX, counterY);

            var number = Label(root, "Number", "0000", NumberFontSize,
                numberX + NumberIndent, NumberRowTop - NumberLift, numberWidth, NumberRowHeight,
                TextAnchor.MiddleLeft);

            // レベルは級の帯の真下に、同じ枠で。書体をそろえるため絵にしてある。
            var level = UiFactory.CreateRect(root, "Level");
            level.anchoredPosition = new Vector2(gradeX, -counterY);
            level.sizeDelta = new Vector2(GradeWidth, ButtonHeight);

            // 級の帯と同じく不透明なので、問題番号の字より奥に。
            level.SetSiblingIndex(grade.GetSiblingIndex() + 1);

            var levelImage = level.gameObject.AddComponent<Image>();
            levelImage.sprite = sprites[LevelName(1, "yellow")];
            levelImage.type = Image.Type.Simple;
            levelImage.preserveAspect = true;
            levelImage.color = Color.white;
            levelImage.raycastTarget = false;

            // ---- 参照
            header.Assign(number, checkCount, hintCount,
                settings, check, hint, stepBack, stepForward, longPress);

            var arts = new List<HeaderBar.GradeArt>();
            foreach (var entry in GradeLanguages)
                arts.Add(new HeaderBar.GradeArt
                {
                    Language = entry.Language,
                    White = sprites[GradeName(entry.Language, "white")],
                    Yellow = sprites[GradeName(entry.Language, "yellow")],
                    Black = sprites[GradeName(entry.Language, "black")],
                });

            var levels = new List<HeaderBar.LevelArt>();
            for (var i = 1; i <= HeaderBar.LevelArtCount; i++)
                levels.Add(new HeaderBar.LevelArt
                {
                    Level = i,
                    White = sprites[LevelName(i, "white")],
                    Yellow = sprites[LevelName(i, "yellow")],
                    Black = sprites[LevelName(i, "black")],
                });

            header.AssignArt(titleImage,
                sprites["Title-white"], sprites["Title-yellow"], sprites["Title-black"],
                gear, sprites["Gear-Dark"], sprites["Gear-Light"],
                gradeImage, arts.ToArray(),
                levelImage, levels.ToArray());

            if (!header.Validate(out var missing))
            {
                Debug.LogError("帯の参照が足りません: " + missing);
                Object.DestroyImmediate(root.gameObject);
                return null;
            }

            var lines = new StringBuilder("帯を 2026-09-13 の案に組み直しました: " + PrefabPath + "\n\n");
            lines.AppendLine("  幅 " + ContentWidth + "（余白 " + SideMargin + " x 2 を除く）、高さ " + HeaderHeight);
            lines.AppendLine("  歯車      : X " + (GearX - SideMargin) + " Y " + GearY + "  " + GearSize + " x " + GearSize);
            lines.AppendLine("  タイトル  : Y " + TitleTop + "  " + DesignWidth + " x " + TitleHeight + "（左右へ " + SideMargin + " はみ出す）");
            lines.AppendLine("  ボタン    : Y " + buttonY + "  Check " + checkX + " / Hint " + hintX
                             + " / ← " + numberX + " / → " + (numberX + StepButtonWidth + ButtonGap + ForwardGapExtra)
                             + " / 級 " + gradeX + "〜" + (gradeX + GradeWidth) + "（Y " + (buttonY + GradeDrop) + "）");
            lines.AppendLine("  問題番号  : X " + (numberX + NumberIndent) + " Y " + (NumberRowTop - NumberLift));
            lines.AppendLine("  級の帯は → の下に " + (numberX + StepButtonWidth * 2f + ButtonGap + ForwardGapExtra - gradeX).ToString("0")
                             + " 潜り込みます（字の無い左端 " + GradeBlankLeft + " の内側）");
            lines.AppendLine("  数字      : Y " + NumberRowTop + "〜" + (NumberRowTop + NumberRowHeight));
            lines.AppendLine("  級の帯の絵: " + GradeLanguages.Length + " 言語 x " + Belts.Length + " 級");
            lines.AppendLine("  レベルの絵: " + HeaderBar.LevelArtCount + " 段階 x " + Belts.Length + " 級");
            lines.AppendLine();
            lines.AppendLine("位置や大きさは、プレハブを開いて Inspector で直せます。");

            report = lines.ToString();
            return root.gameObject;
        }

        /// <summary>絵そのものがボタンの面になるボタン。文字は持たない。</summary>
        private static Button ImageButton(RectTransform parent, string name, Sprite sprite,
            float x, float y, float width, float height)
        {
            var rect = UiFactory.CreateRect(parent, name);
            rect.anchoredPosition = new Vector2(x, -y);
            rect.sizeDelta = new Vector2(width, height);

            var image = rect.gameObject.AddComponent<Image>();
            image.sprite = sprite;
            image.type = Image.Type.Simple;
            image.preserveAspect = true;
            image.color = Color.white;

            var button = rect.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            return button;
        }

        /// <summary>Check / Hint の下に置く、白い枠付きの回数。ボタンと同じ幅。</summary>
        private static Text CounterBox(RectTransform parent, string name, float x, float y)
        {
            var box = UiFactory.CreateRect(parent, name + "Box");
            box.anchoredPosition = new Vector2(x, -y);
            box.sizeDelta = new Vector2(WideButtonWidth, CounterHeight);

            var image = box.gameObject.AddComponent<Image>();
            image.color = Color.white;
            image.raycastTarget = false;
            UiFactory.ApplyButtonSprite(image);

            var text = Label(box, name, "0", CounterFontSize, 0f, 0f, WideButtonWidth, CounterHeight,
                TextAnchor.MiddleRight);
            var rect = (RectTransform)text.transform;
            rect.offsetMax = new Vector2(rect.offsetMax.x - 12f, rect.offsetMax.y);   // 右に少し余白
            return text;
        }

        private static Text Label(RectTransform parent, string name, string content, int fontSize,
            float x, float y, float width, float height, TextAnchor anchor)
        {
            var text = UiFactory.CreateLabel(parent, name, content, fontSize, anchor);
            text.color = GradeTheme.Black;

            var rect = (RectTransform)text.transform;
            rect.anchoredPosition = new Vector2(x, -y);
            rect.sizeDelta = new Vector2(width, height);
            return text;
        }
    }
}
