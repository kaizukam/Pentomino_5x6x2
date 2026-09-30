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
    /// はまったときの音と振動。
    ///
    /// 音は Resources から読む。置き場所を間違えると読めないのに、
    /// 実機で鳴らしてみるまで気づけない。ここで見張る。
    /// </summary>
    public class FeedbackTests
    {
        private string _savePath;
        private GameObject _canvasGo;
        private GameScreen _screen;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            _savePath = Path.Combine(Path.GetTempPath(), "pentomino_fb_" + Path.GetRandomFileName() + ".json");
            ProgressStore.DefaultPath = _savePath;

            _canvasGo = new GameObject("Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            _canvasGo.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;

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

        [Test]
        public void 完成の音が読める()
        {
            // Resources の外に置くと読めない。実機で鳴らすまで気づけないので、ここで見る。
            var clip = Resources.Load<AudioClip>(Feedback.CongratulationsResourcePath);

            Assert.IsNotNull(clip, "Resources/" + Feedback.CongratulationsResourcePath + " が読めません");
            Assert.Greater(clip.length, 1f, "おめでとうの音がありません");
        }

        [Test]
        public void 合図の音は長さのとおりに作られる()
        {
            // 出来合いの音では長さを変えられないので、その場で作っている。
            // 指定した長さで本当に作られることを見る。
            foreach (var milliseconds in new[] { 20, 40, 200 })
            {
                var clip = ClickTone.Create(milliseconds);

                Assert.IsNotNull(clip, milliseconds + "ms の音が作れません");
                Assert.AreEqual(milliseconds / 1000f, clip.length, 0.002f,
                    milliseconds + "ms のはずが " + (clip.length * 1000f).ToString("0") + "ms です");
            }

            // 0 は「端末に任せる」。音では既定の長さになる。
            var byDefault = ClickTone.Create(0);
            Assert.AreEqual(ClickTone.DefaultMilliseconds / 1000f, byDefault.length, 0.002f);
        }

        [Test]
        public void 合図は音か振動のどちらかで出る()
        {
            // 両方は出さない。静かな場所で音が鳴るのは邪魔だし、
            // 振動できるのに音まで鳴らす理由もない。
            var feedback = _screen.GetComponent<Feedback>();

            // エディタには振動子が無いので、音のほうで出る。
            Assert.IsFalse(feedback.CanVibrate);
            Assert.IsNotEmpty(feedback.VibrationNote);
        }

        [Test]
        public void 画面に音と振動の仕掛けが付いている()
        {
            var feedback = _screen.GetComponent<Feedback>();

            Assert.IsNotNull(feedback, "Feedback が付いていません");
            Assert.IsNotNull(_screen.GetComponent<AudioSource>(), "AudioSource が付いていません");
        }

        [Test]
        public void 振動の長さと強さは使える範囲に丸める()
        {
            var feedback = _screen.GetComponent<Feedback>();

            feedback.VibrationMilliseconds = 9999;
            Assert.AreEqual(FlickSettings.LongestVibrationMilliseconds,
                feedback.VibrationMilliseconds, "長さの上が抑えられていません");

            feedback.VibrationMilliseconds = -5;
            Assert.AreEqual(SignalScale.DeviceDefault, feedback.VibrationMilliseconds,
                "長さの下が抑えられていません");

            feedback.VibrationAmplitude = 9999;
            Assert.AreEqual(255, feedback.VibrationAmplitude, "強さの上が抑えられていません");

            feedback.VibrationAmplitude = -5;
            Assert.AreEqual(SignalScale.DeviceDefault, feedback.VibrationAmplitude,
                "強さの下が抑えられていません");

            // -1 は「端末に任せる」、0 は「出さない」。どちらも有効な値。
            feedback.VibrationMilliseconds = SignalScale.Off;
            feedback.VibrationAmplitude = SignalScale.Off;
            Assert.AreEqual(SignalScale.Off, feedback.VibrationMilliseconds);
            Assert.AreEqual(SignalScale.Off, feedback.VibrationAmplitude);

            feedback.VibrationMilliseconds = 20;
            feedback.VibrationAmplitude = 200;
            Assert.AreEqual(20, feedback.VibrationMilliseconds);
            Assert.AreEqual(200, feedback.VibrationAmplitude);
        }

        [Test]
        public void 設定から振動の長さと強さを受け取る()
        {
            var feedback = _screen.GetComponent<Feedback>();

            feedback.Apply(new FlickSettings { VibrationMilliseconds = 35, VibrationAmplitude = 180 });

            Assert.AreEqual(35, feedback.VibrationMilliseconds);
            Assert.AreEqual(180, feedback.VibrationAmplitude);
        }

        [Test]
        public void 振動子が無い環境では黙って諦める()
        {
            // エディタや PC には振動子が無い。理由が読めることだけ確かめる。
            var feedback = _screen.GetComponent<Feedback>();

            Assert.IsFalse(feedback.CanVibrate, "エディタで振動できることになっています");
            Assert.IsNotEmpty(feedback.VibrationNote, "理由が空です");
        }

        [Test]
        public void つまみの左は端末に任せる_その隣は出さない()
        {
            // 以前はどちらも 0 で、いちばん静かなつもりが端末の既定
            // （かなり大きい）で鳴っていた。左端と、その隣とを分ける。
            var lowest = FlickSettings.WeakestVibrationAmplitude;
            var highest = FlickSettings.StrongestVibrationAmplitude;

            Assert.AreEqual(SignalScale.DeviceDefault, SignalScale.ToValue(0f, lowest, highest),
                "左端が端末任せになっていません");
            Assert.AreEqual(SignalScale.Off,
                SignalScale.ToValue((SignalScale.DefaultBand + SignalScale.OffBand) * 0.5f,
                    lowest, highest),
                "その隣が「出さない」になっていません");
            Assert.AreEqual(highest, SignalScale.ToValue(1f, lowest, highest),
                "右端が上限になっていません");

            // 「出さない」の区画は、指で確実に入れられる幅が要る。
            Assert.Greater(SignalScale.OffBand - SignalScale.DefaultBand, 0.10f,
                "「出さない」の区画が狭すぎます");
        }

        [Test]
        public void 数字の区画は小さい値ほど広い()
        {
            // 深夜に聞こえる程度のごく小さい値を指で選り分けたい。
            // まっすぐな目盛りでは 1〜10 が左端の数ミリに潰れる。
            var lowest = FlickSettings.WeakestVibrationAmplitude;
            var highest = FlickSettings.StrongestVibrationAmplitude;

            var start = SignalScale.OffBand;
            var half = start + (1f - start) * 0.5f;

            var middle = SignalScale.ToValue(half, lowest, highest);

            Assert.Greater(middle, lowest, "真ん中で下限のままです");
            Assert.Less(middle, highest / 3,
                "真ん中が " + middle + " です。小さい値に幅が回っていません");
        }

        [Test]
        public void つまみの位置と数字は行き来できる()
        {
            var lowest = FlickSettings.ShortestVibrationMilliseconds;
            var highest = FlickSettings.LongestVibrationMilliseconds;

            foreach (var value in new[] { SignalScale.DeviceDefault, SignalScale.Off, 5, 40, 200, 500 })
            {
                var position = SignalScale.ToPosition(value, lowest, highest);
                var back = SignalScale.ToValue(position, lowest, highest);

                Assert.AreEqual(value, back, 2,
                    value + " を置き直すと " + back + " になります");
            }
        }

        [Test]
        public void 出さないに入れたら合図は出ない()
        {
            Assert.IsFalse(SignalScale.Signals(SignalScale.Off, 200), "長さ 0 で出ています");
            Assert.IsFalse(SignalScale.Signals(40, SignalScale.Off), "強さ 0 で出ています");
            Assert.IsTrue(SignalScale.Signals(SignalScale.DeviceDefault, SignalScale.DeviceDefault),
                "端末任せで出なくなっています");
        }

        [Test]
        public void 指の動きを読む速さを端末に合わせる()
        {
            // 既定の毎秒 30 コマでは、指の位置が 33ms に 1 回しか読めない。
            // つまみもピースも飛び飛びに付いてくる。
            Assert.GreaterOrEqual(FrameRate.Choose(30), FrameRate.Lowest,
                "30 コマのままです");
            Assert.AreEqual(90, FrameRate.Choose(90), "端末の速さに合っていません");
            Assert.AreEqual(FrameRate.Highest, FrameRate.Choose(144), "上限が効いていません");
            Assert.AreEqual(FrameRate.Fallback, FrameRate.Choose(0), "読めないときの値が違います");
        }

        [UnityTest]
        public IEnumerator 鳴らしても落ちない()
        {
            // 実機でしか音は出ないが、呼んで例外にならないことは確かめられる。
            var feedback = _screen.GetComponent<Feedback>();
            feedback.Snapped();
            yield return null;

            feedback.Sound = false;
            feedback.VibrationAmplitude = SignalScale.Off;
            feedback.Snapped();
            yield return null;

            // つまみを動かしたときの試し合図も、振動子が無くても落ちない。
            feedback.TestSignal();
            yield return null;
        }
    }
}
