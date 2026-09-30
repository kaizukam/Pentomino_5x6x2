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
    /// <summary>
    /// タイトル以下を畳んだときの振る舞いを確かめる（開発仕様「操作性の問題(1)」）。
    ///
    /// 狙いは「BOX をそのまま持ち上げて、未収納ピースの場所を増やす」こと。
    /// BOX が大きくなってはいけない。以前は帯の高さひとつでセルの大きさと
    /// BOX の位置の両方を決めていたので、畳むと BOX ごと膨らんでいた。
    /// 大きさは畳む前の高さで、位置はいまの高さで決めるように分けてある。
    /// </summary>
    public class HeaderFoldTests
    {
        private string _savePath;
        private GameObject _canvasGo;
        private GameScreen _screen;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            _savePath = Path.Combine(Path.GetTempPath(), "pentomino_fold_" + Path.GetRandomFileName() + ".json");
            ProgressStore.DefaultPath = _savePath;

            _canvasGo = new GameObject("Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            _canvasGo.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;

            var scaler = _canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080f, 1920f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;

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
            if (File.Exists(_savePath)) File.Delete(_savePath);
        }

        private BoardWidget Board => _screen.GetComponentInChildren<BoardWidget>();
        private RectTransform Tray => (RectTransform)_screen.transform.Find("Tray");

        [Test]
        public void プレハブに畳む面が用意されている()
        {
            Assert.IsTrue(_screen.Header.CanFold,
                "HeaderBar.prefab に Pane/Content/Shown/Folded がありません。"
                + "メニュー Pentomino ▸ ヘッダに畳む面を足す を実行してください。");
        }

        [UnityTest]
        public IEnumerator 畳んでもBOXの大きさは変わらない()
        {
            var before = Board.Size;

            _screen.Header.SetFolded(true, true);
            yield return null;

            Assert.AreEqual(before.x, Board.Size.x, 0.01f, "BOX の幅が変わりました");
            Assert.AreEqual(before.y, Board.Size.y, 0.01f, "BOX の高さが変わりました");
        }

        [UnityTest]
        public IEnumerator 畳むとBOXが持ち上がり待機場所がそのぶん広がる()
        {
            var boardTop = -Board.RectTransform.anchoredPosition.y;
            var trayTop = -Tray.offsetMax.y;

            _screen.Header.SetFolded(true, true);
            yield return null;

            var lifted = boardTop - (-Board.RectTransform.anchoredPosition.y);
            var gained = trayTop - (-Tray.offsetMax.y);

            Assert.Greater(lifted, 0f, "BOX が持ち上がっていません");
            Assert.AreEqual(lifted, gained, 0.01f,
                "BOX が上がった分と、待機場所が広がった分が合いません");
        }

        [UnityTest]
        public IEnumerator 戻すと元の位置に帰る()
        {
            var boardTop = Board.RectTransform.anchoredPosition.y;

            _screen.Header.SetFolded(true, true);
            yield return null;

            _screen.Header.SetFolded(false, true);
            yield return null;

            Assert.AreEqual(boardTop, Board.RectTransform.anchoredPosition.y, 0.01f);
        }

        [UnityTest]
        public IEnumerator 送りボタンを押している間は畳まない()
        {
            // 押し続けて早送りしている最中にタイトルが消えると、
            // 次の問題でまた出てきて、ちらつく（開発仕様「操作性の問題(1)」）。
            StepButton step = null;
            foreach (var b in _screen.Header.GetComponentsInChildren<StepButton>(true))
                if (b.name == "StepForward") step = b;

            Assert.IsNotNull(step, "StepForward が見つかりません");

            step.OnPointerDown(null);
            Assert.IsTrue(_screen.Header.IsStepping);

            // 畳むはずの時間を、繰り返しの待ち時間ごと通り越して待つ。
            yield return new WaitForSecondsRealtime(step.HoldDelay + 2.5f);

            Assert.AreEqual(0f, _screen.Header.FullHeight - _screen.Header.Height, 0.01f,
                "押している最中に畳まれました");

            step.OnPointerUp(null);
            Assert.IsFalse(_screen.Header.IsStepping);
        }

        [UnityTest]
        public IEnumerator 完成した問題では畳まない()
        {
            // 全部置いて完成させる。Hint は答えの先頭から順に埋めるので、
            // 級を問わず、繰り返せば必ず完成する。
            while (_screen.Session.Hint()) { }

            Assert.IsTrue(_screen.Session.IsSolved, "完成していません");

            _screen.Header.SetFolded(true, true);

            // LateUpdate が気づいて、滑らかに戻すまで待つ。
            yield return new WaitForSecondsRealtime(_screen.Header.FoldSeconds + 0.2f);

            Assert.AreEqual(0f, _screen.Header.FullHeight - _screen.Header.Height, 0.01f,
                "完成した問題なのに畳まれたままです");
        }
    }
}
