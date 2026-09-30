using System.Collections;
using System.IO;
using NUnit.Framework;
using Pentomino.Core;
using Pentomino.Data;
using Pentomino.View;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace Pentomino.Tests
{
    /// <summary>設定モードの動きを、実際に画面を組み立てて確かめる。</summary>
    public class SettingsPanelTests
    {
        private string _savePath;
        private GameObject _canvasGo;
        private GameScreen _screen;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            _savePath = Path.Combine(Path.GetTempPath(), "pentomino_set_" + Path.GetRandomFileName() + ".json");
            ProgressStore.DefaultPath = _savePath;

            _canvasGo = new GameObject("Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            _canvasGo.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;

            var scaler = _canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080f, 1920f);
            // 実際の場面と同じ合わせ方にする。
            // Expand なら縦横どちらも基準に収まるので、画面の形に関わらず組み立てが決まる。
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;
            scaler.matchWidthOrHeight = 0f;

            var screenGo = new GameObject("GameScreen", typeof(RectTransform), typeof(GameScreen));
            screenGo.transform.SetParent(_canvasGo.transform, false);
            _screen = screenGo.GetComponent<GameScreen>();

            yield return null;
        }

        [TearDown]
        public void TearDown()
        {
            if (_canvasGo != null) Object.DestroyImmediate(_canvasGo);
            ProgressStore.DefaultPath = null;
            Strings.Current = Language.Japanese;
            if (File.Exists(_savePath)) File.Delete(_savePath);
        }

        private SettingsPanel Panel => _screen.GetComponentInChildren<SettingsPanel>(true);

        private RectTransform Find(string name) => FindIn(_screen.transform, name);

        /// <summary>帯の中から名前で探す。子画面にも同じ名前（Title など）があるので、根を絞る。</summary>
        private RectTransform FindInHeader(string name) => FindIn(_screen.Header.transform, name);

        private static RectTransform FindIn(Transform root, string name)
        {
            foreach (var rect in root.GetComponentsInChildren<RectTransform>(true))
                if (rect.name == name) return rect;
            return null;
        }

        private Button FindButton(string name)
        {
            foreach (var button in _screen.GetComponentsInChildren<Button>(true))
                if (button.name == name) return button;
            return null;
        }

        [Test]
        public void ナビゲーションのボタンが揃っている()
        {
            // 構成: 設定 / ← / → / Check / Hint
            // 終了ボタンは廃止した。言語を切り替えると 3 つでは枠に収まらないため。
            // 説明ボタンは設定画面へ移した。遊んでいる最中に押すものではない。
            Assert.IsNotNull(FindButton("Settings"), "設定ボタンが見つからない");
            Assert.IsNull(FindButton("Exit"), "終了ボタンは無くなったはず");
            Assert.IsNotNull(FindButton("StepBack"), "戻しボタンが見つからない");
            Assert.IsNotNull(FindButton("StepForward"), "送りボタンが見つからない");
            Assert.IsNotNull(FindButton("Check"), "Check ボタンが見つからない");
            Assert.IsNotNull(FindButton("Hint"), "Hint ボタンが見つからない");
        }

        [Test]
        public void 送りボタンは押し続けで早送りする()
        {
            foreach (var name in new[] { "StepBack", "StepForward" })
            {
                var button = FindButton(name);
                Assert.IsNotNull(button, name);

                var step = button.GetComponent<StepButton>();
                Assert.IsNotNull(step, name + " に StepButton が付いていない");
            }

            Assert.AreEqual(-1, FindButton("StepBack").GetComponent<StepButton>().Direction);
            Assert.AreEqual(1, FindButton("StepForward").GetComponent<StepButton>().Direction);
        }

        [Test]
        public void ボタンの段が重ならない()
        {
            // 左から Check / Hint / ← / →、その右に級の帯（2026-09-13 の案）。
            var names = new[] { "Check", "Hint", "StepBack", "StepForward" };
            var previousRight = float.NegativeInfinity;

            foreach (var name in names)
            {
                var rect = FindInHeader(name);
                Assert.IsNotNull(rect, name + " が見つからない");
                var left = rect.anchoredPosition.x;
                Assert.GreaterOrEqual(left, previousRight, name + " が左隣に重なっている");
                previousRight = left + rect.sizeDelta.x;
            }

            // 級の帯の絵は左端 50 に字が無いので、そこだけは → の下に潜ってよい。
            // ただし絵は不透明なので、→ より奥に描かれていること。
            var grade = FindInHeader("Grade");
            var forward = FindInHeader("StepForward");
            Assert.IsNotNull(grade, "Grade が見つからない");
            Assert.GreaterOrEqual(grade.anchoredPosition.x + 50f, previousRight, "級の帯の字が → に重なっている");
            Assert.Less(grade.GetSiblingIndex(), forward.GetSiblingIndex(), "級の帯が → の手前に描かれている");
        }

        [Test]
        public void 説明書は最初は閉じている()
        {
            Assert.IsNotNull(_screen.ManualPanel, "説明書のパネルが無い");
            Assert.IsFalse(_screen.ManualPanel.IsOpen);
        }

        [Test]
        public void 説明ボタンで説明書を開いて閉じられる()
        {
            _screen.OnOpenManual();
            Assert.IsTrue(_screen.ManualPanel.IsOpen);

            _screen.OnCloseManual();
            Assert.IsFalse(_screen.ManualPanel.IsOpen);
        }

        [Test]
        public void 設定は最初は閉じている()
        {
            Assert.IsNotNull(Panel);
            Assert.IsFalse(Panel.IsOpen);
        }

        [Test]
        public void 設定モードを開いて閉じられる()
        {
            _screen.OnOpenSettings();
            Assert.IsTrue(Panel.IsOpen);

            _screen.OnCloseSettings();
            Assert.IsFalse(Panel.IsOpen);
        }

        [UnityTest]
        public IEnumerator 級を変えても進捗は級ごとに残る()
        {
            // 上級が既定なので、まず 1 手進めて進捗を作る。
            var session = _screen.Session;
            Assert.AreEqual(Difficulty.Classic, session.Difficulty, "既定は上級");

            session.Hint();
            _screen.Refresh();
            yield return null;

            var hints = _screen.Session.HintCount;
            Assert.Less(hints, 0, "Hint は減点なので負になる");

            _screen.OnOpenSettings();
            FindButton("Grade" + Difficulty.Guided).onClick.Invoke();
            yield return null;

            Assert.AreEqual(Difficulty.Guided, _screen.Session.Difficulty, "級が切り替わる");
            Assert.AreEqual(0, _screen.Session.HintCount, "別の級の記録が見えています");

            // 戻れば、さっきの記録がそのまま出てくる。
            FindButton("Grade" + Difficulty.Classic).onClick.Invoke();
            yield return null;

            Assert.AreEqual(Difficulty.Classic, _screen.Session.Difficulty);
            Assert.AreEqual(hints, _screen.Session.HintCount, "戻ったのに記録が消えています");
        }

        [UnityTest]
        public IEnumerator 記録を消すのは問いかけに答えたあと()
        {
            var session = _screen.Session;
            session.Hint();
            _screen.Refresh();
            yield return null;
            Assert.Less(_screen.Session.HintCount, 0);

            _screen.OnOpenSettings();

            var ask = FindButton("ClearRecordClassic");
            Assert.IsNotNull(ask, "記録を消すボタンが見つからない");

            ask.onClick.Invoke();
            yield return null;

            // 尋ねただけ。まだ何も消えていない。
            Assert.Less(_screen.Session.HintCount, 0, "尋ねる前に消えています");

            FindButton("No").onClick.Invoke();
            yield return null;
            Assert.Less(_screen.Session.HintCount, 0, "やめると答えたのに消えています");

            ask.onClick.Invoke();
            FindButton("Yes").onClick.Invoke();
            yield return null;

            Assert.AreEqual(0, _screen.Session.HintCount, "消すと答えたのに残っています");
            Assert.AreEqual(1, _screen.Session.Puzzle.Number, "最初の問題から始め直す");
        }

        [UnityTest]
        public IEnumerator 消すのはその級だけ()
        {
            // Intro に記録を作ってから、Classic の記録を消す。
            _screen.OnOpenSettings();
            FindButton("Grade" + Difficulty.Guided).onClick.Invoke();
            yield return null;

            _screen.Session.Hint();
            _screen.Refresh();
            yield return null;
            var introHints = _screen.Session.HintCount;
            Assert.Less(introHints, 0);

            _screen.OnOpenSettings();
            FindButton("ClearRecordClassic").onClick.Invoke();
            FindButton("Yes").onClick.Invoke();
            yield return null;

            Assert.AreEqual(Difficulty.Guided, _screen.Session.Difficulty, "級まで変わっています");
            Assert.AreEqual(introHints, _screen.Session.HintCount, "巻き添えで消えています");
        }

        [UnityTest]
        public IEnumerator 言語を変えても進捗は消えない()
        {
            var session = _screen.Session;
            session.Hint();
            _screen.Refresh();
            yield return null;

            var hints = _screen.Session.HintCount;
            var number = _screen.Session.Puzzle.Number;

            _screen.OnOpenSettings();
            FindButton("Lang" + Language.French).onClick.Invoke();
            yield return null;

            Assert.AreEqual(Language.French, Strings.Current, "言語が切り替わる");
            Assert.AreEqual(hints, _screen.Session.HintCount, "進捗はそのまま");
            Assert.AreEqual(number, _screen.Session.Puzzle.Number);
        }

        [UnityTest]
        public IEnumerator 級の表示が選んだ言語になる()
        {
            // 級は言語ごとに描いた帯の絵で見せる。絵の名前に言語コードが入っている。
            foreach (var language in new[] { Language.English, Language.Japanese })
            {
                _screen.OnOpenSettings();
                FindButton("Lang" + language).onClick.Invoke();
                _screen.OnCloseSettings();
                yield return null;

                var grade = FindInHeader("Grade");
                Assert.IsNotNull(grade);
                var image = grade.GetComponent<Image>();
                Assert.IsNotNull(image, "級の帯は Image で出す");
                Assert.IsNotNull(image.sprite, "級の帯の絵が入っていない");
                StringAssert.Contains("-" + language.ToCode() + "-", image.sprite.name,
                    language.ToString());
            }
        }

        [UnityTest]
        public IEnumerator 子画面は柱の幅に比率のまま収まる()
        {
            // 中身は基準の幅（1080）で並べてある。柱がそれより狭い機種では、
            // 位置だけ合わせても中身がはみ出す。パネルごと縮めて比率を保つ。
            yield return null;

            var root = (RectTransform)_screen.transform;
            if (root.rect.width * 2.14f <= root.rect.height)
                Assert.Ignore("縦長の画面では絞らないので、ここは確かめられない");

            foreach (var name in new[] { "SettingsPanel", "ManualPanel", "FlickTuningPanel" })
            {
                var panel = Find(name);
                Assert.IsNotNull(panel, name + " が見つからない");

                var shown = panel.rect.width * panel.localScale.x;

                Assert.Less(shown, root.rect.width, name + " が横いっぱいに広がっています");
                Assert.AreEqual(0f, panel.anchoredPosition.x, 1f,
                    name + " が横中央に置かれていません");
                Assert.AreEqual(1080f, panel.rect.width, 1f,
                    name + " の中の物差しが基準幅から動いています");
            }
        }

        [UnityTest]
        public IEnumerator 設定の中身が柱からはみ出さない()
        {
            // タブレットでボタンが右にはみ出していた。中身は基準幅の中に収める。
            yield return null;

            var panel = Find("SettingsPanel");
            Assert.IsNotNull(panel);

            foreach (var child in panel.GetComponentsInChildren<RectTransform>(true))
            {
                if (child == panel || child.parent != panel) continue;
                if (child.anchorMin.x != 0f || child.anchorMax.x != 0f) continue;   // 伸び縮みする枠は別

                var right = child.anchoredPosition.x + child.rect.width;
                Assert.LessOrEqual(right, panel.rect.width + 1f,
                    child.name + " が右にはみ出しています（" + right.ToString("0") + "）");
            }
        }

        [UnityTest]
        public IEnumerator 設定の中身が縦に収まる()
        {
            yield return null;

            var close = Find("Close");
            Assert.IsNotNull(close);

            // 子画面の高さは、内側の物差しでは機種によらず 1080 x 2.14 から
            // 上端の隠れる帯を引いたものになる。実機では 2180〜2306。
            // 試験の画面は極端に小さく、上端の取り分だけが不釣り合いに大きく出る
            // ので、ここでは実機で見込める高さを使う。
            const float shortest = 1080f * 2.14f - 260f;

            var bottom = -close.anchoredPosition.y + close.rect.height;
            Assert.LessOrEqual(bottom, shortest,
                "閉じるボタンが画面の外に出ています（" + bottom.ToString("0") + "）");
        }

        [UnityTest]
        public IEnumerator 子画面を開くと後ろの遊び面が隠れる()
        {
            _screen.OnOpenSettings();
            yield return null;

            var backdrop = Find("Backdrop");
            Assert.IsNotNull(backdrop, "覆いが見つからない");
            Assert.IsTrue(backdrop.gameObject.activeSelf, "覆いが出ていません");

            var panel = Find("SettingsPanel");
            Assert.Less(backdrop.GetSiblingIndex(), panel.GetSiblingIndex(),
                "覆いが子画面の手前に出ています");

            _screen.OnCloseSettings();
            yield return null;
            Assert.IsFalse(backdrop.gameObject.activeSelf, "閉じたのに覆いが残っています");
        }

        [UnityTest]
        public IEnumerator 説明書は設定画面から開く()
        {
            _screen.OnOpenSettings();
            yield return null;

            var manual = FindButton("Manual");
            Assert.IsNotNull(manual, "設定画面に説明ボタンがない");
            Assert.AreEqual("SettingsPanel", manual.transform.parent.name,
                "説明ボタンが設定画面の中にありません");

            manual.onClick.Invoke();
            yield return null;

            Assert.IsTrue(_screen.ManualPanel.IsOpen, "説明書が開かない");
            Assert.IsFalse(Panel.IsOpen, "設定画面が開いたままです");
        }

        [UnityTest]
        public IEnumerator 音は入り切りできて保存される()
        {
            _screen.OnOpenSettings();
            yield return null;

            var sound = FindButton("Sound");
            Assert.IsNotNull(sound, "音のボタンがない");

            var feedback = _screen.GetComponent<Feedback>();
            Assert.IsTrue(feedback.Sound, "はじめは音が出る");

            sound.onClick.Invoke();
            yield return null;
            Assert.IsFalse(feedback.Sound, "切ったのに音が出るままです");

            // 保存されていること。読み直しても切れたまま。
            Assert.IsFalse(new ProgressStore(_savePath).Load().sound);

            sound.onClick.Invoke();
            yield return null;
            Assert.IsTrue(feedback.Sound, "入れ直せません");
        }

        [UnityTest]
        public IEnumerator 地の色とタイトルと級の帯が級で変わる()
        {
            // 級は帯の色（白・黄・黒）で見せる。画面の地、タイトルの絵、級の帯の絵、
            // 歯車の絵が、級を切り替えるとそろって変わる。
            var background = Find("Background")?.GetComponent<Image>();
            var title = FindInHeader("Title")?.GetComponent<Image>();
            var grade = FindInHeader("Grade")?.GetComponent<Image>();
            var gear = FindInHeader("Settings")?.GetComponent<Image>();
            Assert.IsNotNull(background, "地の Image が見つからない");
            Assert.IsNotNull(title, "タイトルの Image が見つからない");
            Assert.IsNotNull(grade, "級の帯の Image が見つからない");
            Assert.IsNotNull(gear, "歯車の Image が見つからない");

            _screen.OnOpenSettings();
            FindButton("Grade" + Difficulty.Classic).onClick.Invoke();
            _screen.OnCloseSettings();
            yield return null;

            Assert.AreEqual(GradeTheme.Background(Difficulty.Classic), background.color, "黒帯の地");
            var classicTitle = title.sprite;
            var classicGrade = grade.sprite;
            var classicGear = gear.sprite;
            Assert.IsNotNull(classicTitle, "タイトルの絵が入っていない");
            Assert.IsNotNull(classicGrade, "級の帯の絵が入っていない");
            Assert.IsNotNull(classicGear, "歯車の絵が入っていない");

            _screen.OnOpenSettings();
            FindButton("Grade" + Difficulty.Guided).onClick.Invoke();
            _screen.OnCloseSettings();
            yield return null;

            Assert.AreEqual(GradeTheme.Background(Difficulty.Guided), background.color, "白帯の地");
            Assert.AreNotSame(classicTitle, title.sprite, "級を変えてもタイトルの絵が同じままです");
            Assert.AreNotSame(classicGrade, grade.sprite, "級を変えても級の帯の絵が同じままです");
            Assert.AreNotSame(classicGear, gear.sprite, "黒帯だけ明るい歯車なので、白帯では変わる");
        }
    }
}
