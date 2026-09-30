using System.Collections;
using System.IO;
using NUnit.Framework;
using Pentomino.Core;
using Pentomino.Data;
using Pentomino.View;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace Pentomino.Tests
{
    /// <summary>パズル名の長押しで設定モードに入れることを確かめる。</summary>
    public class LongPressTests
    {
        private string _savePath;
        private GameObject _canvasGo;
        private GameScreen _screen;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            _savePath = Path.Combine(Path.GetTempPath(), "pentomino_lp_" + Path.GetRandomFileName() + ".json");
            ProgressStore.DefaultPath = _savePath;

            _canvasGo = new GameObject("Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            _canvasGo.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;

            var scaler = _canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080f, 1920f);
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

        private static PointerEventData Pointer(Vector2 position) =>
            new PointerEventData(EventSystem.current) { position = position };

        [Test]
        public void パズル名に長押し判定が付いている()
        {
            var handler = _screen.TitleLongPress;
            Assert.IsNotNull(handler, "長押し判定が無い");
            Assert.AreEqual("Title", handler.gameObject.name);

            // タイトルは字ではなく絵になった。触れる面は Image が持つ。
            var graphic = handler.GetComponent<Graphic>();
            Assert.IsNotNull(graphic);
            Assert.IsTrue(graphic.raycastTarget, "指が触れないと長押しできない");
        }

        [UnityTest]
        public IEnumerator 長押しで設定モードに入る()
        {
            var handler = _screen.TitleLongPress;
            handler.Threshold = 0.2f;
            Assert.IsFalse(Panel.IsOpen);

            handler.OnPointerDown(Pointer(new Vector2(100f, 1800f)));
            yield return new WaitForSecondsRealtime(0.35f);

            Assert.IsTrue(Panel.IsOpen, "長押しで設定モードになる");

            _screen.OnCloseSettings();
        }

        [UnityTest]
        public IEnumerator 短く押しただけでは開かない()
        {
            var handler = _screen.TitleLongPress;
            handler.Threshold = 0.5f;

            handler.OnPointerDown(Pointer(new Vector2(100f, 1800f)));
            yield return new WaitForSecondsRealtime(0.1f);
            handler.OnPointerUp(Pointer(new Vector2(100f, 1800f)));

            yield return new WaitForSecondsRealtime(0.6f);
            Assert.IsFalse(Panel.IsOpen, "短いタップでは開かない");
        }

        [UnityTest]
        public IEnumerator 指がずれたら取り消される()
        {
            var handler = _screen.TitleLongPress;
            handler.Threshold = 0.3f;

            handler.OnPointerDown(Pointer(new Vector2(100f, 1800f)));
            yield return null;
            handler.OnDrag(Pointer(new Vector2(400f, 1800f)));   // 大きくずらす

            yield return new WaitForSecondsRealtime(0.5f);
            Assert.IsFalse(Panel.IsOpen, "指がずれたら長押しは成立しない");
            Assert.IsFalse(handler.IsPressing);
        }

        [UnityTest]
        public IEnumerator 長押しは1回だけ通知される()
        {
            var handler = _screen.TitleLongPress;
            handler.Threshold = 0.15f;

            var count = 0;
            handler.LongPressed += () => count++;

            handler.OnPointerDown(Pointer(Vector2.zero));
            yield return new WaitForSecondsRealtime(0.6f);

            Assert.AreEqual(1, count, "押しっぱなしでも 1 回だけ");
            _screen.OnCloseSettings();
        }
    }
}
