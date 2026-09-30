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
    /// <summary>完成したときの成績パネルが、BOX の格子図に重ならないことを確かめる。</summary>
    public class ResultPanelTests
    {
        private string _savePath;
        private GameObject _canvasGo;
        private GameScreen _screen;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            _savePath = Path.Combine(Path.GetTempPath(), "pentomino_res_" + Path.GetRandomFileName() + ".json");
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

        /// <summary>ワールド座標での外接矩形。</summary>
        private static Rect WorldRect(RectTransform rect)
        {
            var corners = new Vector3[4];
            rect.GetWorldCorners(corners);

            var min = corners[0];
            var max = corners[0];
            foreach (var c in corners)
            {
                min = Vector3.Min(min, c);
                max = Vector3.Max(max, c);
            }
            return Rect.MinMaxRect(min.x, min.y, max.x, max.y);
        }

        [Test]
        public void 完成前は成績パネルが出ていない()
        {
            Assert.IsNotNull(_screen.ResultPanel);
            Assert.IsFalse(_screen.ResultPanel.gameObject.activeSelf);
        }

        [UnityTest]
        public IEnumerator 成績パネルがBOXに重ならない()
        {
            var session = _screen.Session;
            for (var i = 0; i < session.Puzzle.Level; i++)
                Assert.IsTrue(session.Hint(), (i + 1) + " 回目の Hint");

            _screen.Refresh();
            yield return null;

            Assert.IsTrue(session.IsSolved);

            var panel = _screen.ResultPanel;
            Assert.IsTrue(panel.gameObject.activeSelf, "完成したら成績パネルが出る");

            var board = WorldRect(_screen.BoardWidget.RectTransform);
            var result = WorldRect(panel.RectTransform);

            Assert.IsFalse(result.Overlaps(board),
                "成績パネルが BOX に重なっている\n  BOX=" + board + "\n  パネル=" + result);

            // BOX より下に出ること。
            Assert.LessOrEqual(result.yMax, board.yMin + 0.5f, "成績パネルは BOX の下に出す");
        }

        [UnityTest]
        public IEnumerator 成績パネルが画面幅に収まる()
        {
            var session = _screen.Session;
            for (var i = 0; i < session.Puzzle.Level; i++) session.Hint();
            _screen.Refresh();
            yield return null;

            var panel = WorldRect(_screen.ResultPanel.RectTransform);
            var board = WorldRect(_screen.BoardWidget.RectTransform);

            Assert.LessOrEqual(panel.width, board.width + 1f, "額縁は BOX の幅に収まる");
            Assert.AreEqual(board.center.x, panel.center.x, 1f, "額縁は左右中央");

            // 元の絵の縦横比のまま出す。
            var aspect = _screen.ResultPanel.FrameAspect;
            Assert.AreEqual(aspect, panel.width / panel.height, 0.02f, "元の縦横比を保つ");
        }

        [UnityTest]
        public IEnumerator 成績に級とCheckとHintの回数が出る()
        {
            var session = _screen.Session;
            for (var i = 0; i < session.Puzzle.Level; i++) session.Hint();
            _screen.Refresh();
            yield return null;

            var shown = _screen.ResultPanel.CombinedText;

            StringAssert.Contains(Strings.Get(StringId.Congratulations), shown);
            StringAssert.Contains(Strings.GradeName(session.Difficulty), shown);
            StringAssert.Contains(session.HintCount.ToString(), shown);
            StringAssert.Contains(session.CheckCount.ToString(), shown);
        }
    }
}
