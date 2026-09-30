using System;
using Pentomino.Core;
using UnityEngine;
using UnityEngine.UI;

namespace Pentomino.View
{
    /// <summary>
    /// フリックの感度を実機で詰めるための画面（開発仕様「感度調整」）。
    ///
    /// 画面の細かさも指の動きも機種ごとに違うので、机上では決めきれない。
    /// スライダで 4 つのしきい値を動かしながら、練習用のピースを実際に滑らせて、
    /// そのときの実測値と判定結果をその場で読めるようにしてある。
    ///
    /// 見た目は Assets/Resources/UI/FlickTuningPanel.prefab を編集して調整する。
    /// </summary>
    [AddComponentMenu("Pentomino/Flick Tuning Panel")]
    [RequireComponent(typeof(RectTransform))]
    public sealed class FlickTuningPanel : MonoBehaviour
    {
        [Header("文字")]
        [SerializeField] private Text _title;
        [SerializeField] private Text _tryLabel;
        [SerializeField] private Text _resultLabel;

        /// <summary>スライダの本数。順番は Refresh の SetLabel と揃える。</summary>
        public const int SliderCount = 7;

        /// <summary>合図の長さのつまみ。ここから下は反転ではなく手ざわりの話。</summary>
        private const int VibrationLengthSlider = 5;

        /// <summary>合図の強さのつまみ。</summary>
        private const int VibrationStrengthSlider = 6;

        /// <summary>
        /// 合図のつまみを続けて試すときの、いちばん短い間隔（秒）。
        ///
        /// 動かしている間は毎コマ値が変わるので、そのたびに鳴らすと
        /// 連打になって、何を聞いているのか分からなくなる。指の動きも重くなる。
        /// </summary>
        private const float TryInterval = 0.12f;

        private float _lastTry = -1f;

        [Header("スライダ")]
        [Tooltip("反転する速さ / なめらかさ / 止まったとみなす間 / 最低の移動量 / 斜めの許容"
                 + " / 振動の長さ / 振動の強さ の順")]
        [SerializeField] private Slider[] _sliders;

        [Tooltip("スライダの名前。上と同じ順")]
        [SerializeField] private Text[] _sliderLabels;

        [Tooltip("いまの値。上と同じ順")]
        [SerializeField] private Text[] _sliderValues;

        [Header("練習用のピース")]
        [SerializeField] private FlickSample _sample;

        [Header("ボタン")]
        [SerializeField] private Button _reset;
        [SerializeField] private Button _close;

        private FlickSettings _settings;
        private bool _wired;
        private bool _updating;

        /// <summary>感度が変わった。呼び出し側が保存する。</summary>
        public event Action<FlickSettings> Changed;

        /// <summary>
        /// 合図のつまみが動いた。受け手はその場で一度、合図を出す。
        /// 動かしながら耳と指で確かめられないと、数字だけでは決められない。
        /// </summary>
        public event Action VibrationTried;

        /// <summary>
        /// 合図が振動で出るか音で出るか。機種によって決まるので、
        /// つまみの下にどちらで出るかを書く。
        /// </summary>
        public bool CanVibrate { get; set; } = true;

        /// <summary>閉じるボタンが押された。</summary>
        public event Action CloseRequested;

        public bool IsOpen => gameObject.activeSelf;

        private void Awake() => Wire();

        private void Wire()
        {
            if (_wired) return;
            _wired = true;

            if (_close != null) _close.onClick.AddListener(() => CloseRequested?.Invoke());
            if (_reset != null) _reset.onClick.AddListener(ResetToDefault);

            if (_sliders != null)
            {
                for (var i = 0; i < _sliders.Length; i++)
                {
                    if (_sliders[i] == null) continue;
                    _sliders[i].onValueChanged.AddListener(_ => OnSliderMoved());
                }
            }

            if (_sample != null)
            {
                _sample.SettingsSource = () => _settings;
                _sample.Moved += _ => ShowResult();
            }
        }

        /// <summary>いまの感度を持って開く。</summary>
        public void Open(FlickSettings settings, PentominoStyle style, PostureDatabase postures)
        {
            Wire();

            _settings = settings != null ? settings.Clone() : new FlickSettings();
            _settings.Clamp();

            gameObject.SetActive(true);

            if (_sample != null && postures != null)
            {
                // 左右反転と上下反転で見た目が変わるピースを選ぶ。
                // アキラルなピースだと、反転しても形が変わらず確かめられない。
                _sample.Show(postures.PosturesOf('F')[0], style);
            }

            Refresh();
            ShowResult();
        }

        public void Close() => gameObject.SetActive(false);

        /// <summary>スライダと文言を今の値で描き直す。</summary>
        public void Refresh()
        {
            if (_settings == null) return;

            SetText(_title, Strings.Get(StringId.FlickTuning));
            SetText(_tryLabel, Strings.Get(StringId.FlickTry));

            if (_close != null) SetText(LabelOf(_close), Strings.Get(StringId.Close));
            if (_reset != null) SetText(LabelOf(_reset), Strings.Get(StringId.ResetToDefault));

            SetLabel(0, StringId.FlickReleaseSpeed);
            SetLabel(1, StringId.FlickFilter);
            SetLabel(2, StringId.FlickStill);
            SetLabel(3, StringId.FlickMinTravel);
            SetLabel(4, StringId.FlickAxisRatio);
            SetLabel(VibrationLengthSlider, StringId.VibrationLength);
            SetLabel(VibrationStrengthSlider, StringId.VibrationStrength);

            _updating = true;
            SetSlider(0, 20f, 600f, _settings.ReleaseSpeedMmPerSecond);
            SetSlider(1, 1f, 10f, _settings.Filter);
            SetSlider(2, 0.02f, 0.30f, _settings.StillSeconds);
            SetSlider(3, 0f, 40f, _settings.MinTravelMm);
            SetSlider(4, 1f, 5f, _settings.AxisRatio);
            SetSlider(VibrationLengthSlider, 0f, 1f,
                SignalScale.ToPosition(_settings.VibrationMilliseconds,
                    FlickSettings.ShortestVibrationMilliseconds,
                    FlickSettings.LongestVibrationMilliseconds));
            SetSlider(VibrationStrengthSlider, 0f, 1f,
                SignalScale.ToPosition(_settings.VibrationAmplitude,
                    FlickSettings.WeakestVibrationAmplitude,
                    FlickSettings.StrongestVibrationAmplitude));
            _updating = false;

            ShowValues();
        }

        private void ResetToDefault()
        {
            _settings = new FlickSettings();
            Refresh();
            Changed?.Invoke(_settings);
        }

        private void OnSliderMoved()
        {
            if (_updating || _settings == null) return;

            _settings.ReleaseSpeedMmPerSecond = ValueOf(0, _settings.ReleaseSpeedMmPerSecond);
            _settings.Filter = ValueOf(1, _settings.Filter);
            _settings.StillSeconds = ValueOf(2, _settings.StillSeconds);
            _settings.MinTravelMm = ValueOf(3, _settings.MinTravelMm);
            _settings.AxisRatio = ValueOf(4, _settings.AxisRatio);

            var wasLength = _settings.VibrationMilliseconds;
            var wasStrength = _settings.VibrationAmplitude;

            _settings.VibrationMilliseconds = SignalOf(VibrationLengthSlider,
                FlickSettings.ShortestVibrationMilliseconds,
                FlickSettings.LongestVibrationMilliseconds,
                _settings.VibrationMilliseconds);

            _settings.VibrationAmplitude = SignalOf(VibrationStrengthSlider,
                FlickSettings.WeakestVibrationAmplitude,
                FlickSettings.StrongestVibrationAmplitude,
                _settings.VibrationAmplitude);

            _settings.Clamp();

            ShowValues();
            Changed?.Invoke(_settings);

            // 合図のつまみが動いたときだけ、その場で一度出す。
            if (_settings.VibrationMilliseconds == wasLength
                && _settings.VibrationAmplitude == wasStrength) return;

            // 動かしている間は毎コマ来る。間引かないと連打になる。
            var now = Time.unscaledTime;
            if (now - _lastTry < TryInterval) return;

            _lastTry = now;
            VibrationTried?.Invoke();
        }

        /// <summary>いまのしきい値を数字で出す。</summary>
        private void ShowValues()
        {
            if (_settings == null) return;

            SetValue(0, Mathf.RoundToInt(_settings.ReleaseSpeedMmPerSecond) + " mm/s");
            SetValue(1, "n = " + _settings.Filter.ToString("0.0"));
            SetValue(2, Mathf.RoundToInt(_settings.StillSeconds * 1000f) + " ms");
            SetValue(3, _settings.MinTravelMm.ToString("0.0") + " mm");
            SetValue(4, _settings.AxisRatio.ToString("0.0") + " : 1");

            // -1 は「端末に任せる」、0 は「出さない」。どちらも言葉で出す。
            SetValue(VibrationLengthSlider,
                Words(_settings.VibrationMilliseconds, _settings.VibrationMilliseconds + " ms"));

            SetValue(VibrationStrengthSlider,
                Words(_settings.VibrationAmplitude, _settings.VibrationAmplitude.ToString()));

            // 合図が音で出るか振動で出るかは機種次第。どちらかを名前に添える。
            SetText(_sliderLabels != null && VibrationLengthSlider < _sliderLabels.Length
                    ? _sliderLabels[VibrationLengthSlider] : null,
                Strings.Get(StringId.VibrationLength)
                + "  （" + Strings.Get(CanVibrate ? StringId.ByVibration : StringId.NoVibrator) + "）");
        }

        /// <summary>つまみの左二区画は数字ではなく言葉で出す。</summary>
        private static string Words(int value, string number)
        {
            if (value <= SignalScale.DeviceDefault) return Strings.Get(StringId.DeviceDefault);
            if (value <= SignalScale.Off) return Strings.Get(StringId.SignalOff);

            return number;
        }

        /// <summary>
        /// 直前の指の動きの実測値と判定結果を出す。
        /// 「今のは 5.2mm で短すぎた」とその場で分かるので、詰めるのが早い。
        /// </summary>
        private void ShowResult()
        {
            if (_resultLabel == null || _sample == null || _settings == null) return;

            var flick = _sample.Flick;

            // 1 行目は判定そのもの。
            // 2 行目から先は、実際に届いたコマの様子。
            // 速度をならして測っているので、間隔が伸びたりばらついたりしていれば、
            // しきい値ではなくそちらが原因になる。
            _resultLabel.text = string.Format("{0}  {1} mm/s  移動 {2:0.0} mm  →  {3}\n{4}",
                Strings.Get(StringId.FlickResult),
                Mathf.RoundToInt(flick.LastSpeedMmPerSecond),
                flick.TravelMm,
                flick.LastReason(_settings),
                flick.Trace.Summary());
        }

        private float ValueOf(int index, float fallback) =>
            _sliders != null && index < _sliders.Length && _sliders[index] != null
                ? _sliders[index].value
                : fallback;

        /// <summary>
        /// 合図のつまみは 0〜1 の位置で読む。左端が「端末に任せる」、
        /// その隣の広い区画が「出さない」、そこから右が数字（SignalScale）。
        /// </summary>
        private int SignalOf(int index, int lowest, int highest, int fallback)
        {
            if (_sliders == null || index >= _sliders.Length || _sliders[index] == null)
                return fallback;

            return SignalScale.ToValue(_sliders[index].value, lowest, highest);
        }

        private void SetSlider(int index, float min, float max, float value)
        {
            if (_sliders == null || index >= _sliders.Length || _sliders[index] == null) return;

            var slider = _sliders[index];
            slider.minValue = min;
            slider.maxValue = max;
            slider.value = Mathf.Clamp(value, min, max);
        }

        private void SetLabel(int index, StringId id)
        {
            if (_sliderLabels == null || index >= _sliderLabels.Length) return;
            SetText(_sliderLabels[index], Strings.Get(id));
        }

        private void SetValue(int index, string text)
        {
            if (_sliderValues == null || index >= _sliderValues.Length) return;
            SetText(_sliderValues[index], text);
        }

        private static Text LabelOf(Button button) =>
            button == null ? null : button.GetComponentInChildren<Text>(true);

        private static void SetText(Text text, string value)
        {
            if (text != null) text.text = value;
        }

        /// <summary>プレハブに参照を付け忘れていないか調べる（テスト・生成ツール用）。</summary>
        public bool Validate(out string missing)
        {
            missing = null;

            if (_title == null) missing = "Title";
            else if (_tryLabel == null) missing = "TryLabel";
            else if (_resultLabel == null) missing = "ResultLabel";
            else if (_sliders == null || _sliders.Length != SliderCount) missing = "Sliders";
            else if (_sliderLabels == null || _sliderLabels.Length != SliderCount) missing = "SliderLabels";
            else if (_sliderValues == null || _sliderValues.Length != SliderCount) missing = "SliderValues";
            else if (_sample == null) missing = "Sample";
            else if (_reset == null) missing = "Reset";
            else if (_close == null) missing = "Close";

            return missing == null;
        }

        /// <summary>生成ツールから参照をまとめて設定する。</summary>
        internal void Assign(Text title, Text tryLabel, Text resultLabel,
            Slider[] sliders, Text[] sliderLabels, Text[] sliderValues,
            FlickSample sample, Button reset, Button close)
        {
            _title = title;
            _tryLabel = tryLabel;
            _resultLabel = resultLabel;
            _sliders = sliders;
            _sliderLabels = sliderLabels;
            _sliderValues = sliderValues;
            _sample = sample;
            _reset = reset;
            _close = close;
        }
    }
}
