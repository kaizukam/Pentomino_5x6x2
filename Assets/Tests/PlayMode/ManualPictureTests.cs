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
    /// 説明書に絵が出て、押せるようになっているか。
    ///
    /// 本文を切り分けるところは ManualBlocksTests が見ている。
    /// こちらは、実際に画面へ部品が積まれるかを確かめる。
    /// </summary>
    public class ManualPictureTests
    {
        private string _savePath;
        private GameObject _canvasGo;
        private GameScreen _screen;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            _savePath = Path.Combine(Path.GetTempPath(), "manual_" + Path.GetRandomFileName() + ".json");
            ProgressStore.DefaultPath = _savePath;

            _canvasGo = new GameObject("Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            _canvasGo.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;

            var scaler = _canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080f, 1920f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;

            var go = new GameObject("GameScreen", typeof(RectTransform), typeof(GameScreen));
            go.transform.SetParent(_canvasGo.transform, false);
            _screen = go.GetComponent<GameScreen>();

            yield return null;
        }

        [TearDown]
        public void TearDown()
        {
            if (_canvasGo != null) Object.DestroyImmediate(_canvasGo);
            ProgressStore.DefaultPath = null;
            if (File.Exists(_savePath)) File.Delete(_savePath);
        }

        private ManualPanel Manual => _screen.ManualPanel;

        private int Pictures()
        {
            var count = 0;
            foreach (var image in Manual.GetComponentsInChildren<Image>(true))
                if (image.name == "Picture" && image.sprite != null) count++;
            return count;
        }

        [UnityTest]
        public IEnumerator 説明書に表紙が並ぶ()
        {
            _screen.OnOpenManual();
            yield return null;

            Assert.Greater(Pictures(), 0, "説明書に絵がひとつも出ていません");
        }

        [UnityTest]
        public IEnumerator 表紙は押せる()
        {
            _screen.OnOpenManual();
            yield return null;

            var pressable = 0;
            foreach (var image in Manual.GetComponentsInChildren<Image>(true))
            {
                if (image.name != "Picture") continue;
                if (image.GetComponent<Button>() != null) pressable++;
            }

            Assert.Greater(pressable, 0, "押せる表紙がありません");
        }

        [UnityTest]
        public IEnumerator 本文に目印が残らない()
        {
            _screen.OnOpenManual();
            yield return null;

            foreach (var text in Manual.GetComponentsInChildren<Text>(true))
            {
                StringAssert.DoesNotContain(ManualBlocks.ImageOpen, text.text,
                    "絵の目印が文字のまま出ています");
            }
        }

        [UnityTest]
        public IEnumerator 開き直しても絵が増えない()
        {
            // 言語を変えるたびに作り直すので、前の分が残ると倍々に増えてしまう。
            _screen.OnOpenManual();
            yield return null;
            var first = Pictures();

            Manual.Refresh();
            yield return null;
            Manual.Refresh();
            yield return null;

            Assert.AreEqual(first, Pictures(), "作り直すたびに絵が増えています");
        }
    }
}
