using Pentomino.Core;
using UnityEngine;

namespace Pentomino.View
{
    /// <summary>
    /// ピースがはめ込み位置に吸い付いた瞬間の合図（開発仕様「ピースのはめ込み」）。
    ///
    /// 吸い付きは外周線が青くなることで判るが、指が絵の上に乗っていると
    /// 線そのものが見えない。音か振動なら、画面を見ていなくても伝わる。
    ///
    /// 出すのは吸い付いた瞬間の一度だけ。吸い付いている間ずっと出すと、
    /// 境目で指が揺れるたびに鳴り続けてやかましい。外れたことは知らせない。
    ///
    /// ■ 合図は一つ。音か振動かは機種が決める
    ///
    /// 遊ぶ人が決めるのは「合図の長さ」と「合図の強さ」の二つだけ。
    /// 振動子のある機種では振動で、無い機種では音で出す。両方は出さない。
    /// 静かな場所で音が鳴るのは邪魔だし、振動できるのに音まで鳴らす理由もない。
    ///
    /// どちらの数字も、-1 なら端末に任せ、0 なら合図を出さない
    /// （目盛りの決め方は SignalScale）。
    ///
    /// 音は出来合いのファイルではなく、その場で作る（ClickTone）。
    /// そうしないと、長さのつまみが音側で効かない。
    ///
    /// ■ 音の入り切りは、おめでとうだけに効く
    ///
    /// はめ込みの合図はつまみを「出さない」に入れれば消せるので、別の入り切りは要らない。
    /// 完成したときの音は 5 秒あって性格が違うので、そちらだけをボタンで切る。
    /// </summary>
    [AddComponentMenu("Pentomino/Feedback")]
    [RequireComponent(typeof(AudioSource))]
    public sealed class Feedback : MonoBehaviour
    {
        /// <summary>完成したときの音。</summary>
        public const string CongratulationsResourcePath = "Audio/Congratulations";

        [Tooltip("完成したときの音。無ければ Resources から読む")]
        [SerializeField] private AudioClip _congratulations;

        [Tooltip("完成したときの音を鳴らすか。はめ込みの合図には効かない")]
        [SerializeField] private bool _sound = true;

        [Tooltip("合図の長さ（ミリ秒）。-1 なら端末に任せる、0 なら出さない")]
        [Range(-1, 500)]
        [SerializeField] private int _vibrationMilliseconds =
            FlickSettings.DefaultVibrationMilliseconds;

        [Tooltip("合図の強さ 1〜255。-1 なら端末に任せる、0 なら出さない")]
        [Range(-1, 255)]
        [SerializeField] private int _vibrationAmplitude =
            FlickSettings.DefaultVibrationAmplitude;

        [Tooltip("完成したときの音の大きさ。5 秒あるので控えめにする")]
        [Range(0f, 1f)]
        [SerializeField] private float _congratulationsVolume = 0.5f;

        /// <summary>強さに -1（端末に任せる）が来たときの、音の大きさ。</summary>
        private const float DefaultVolume = 0.7f;

        private AudioSource _source;
        private Haptics _haptics;

        /// <summary>合図の音。長さが変わったときだけ作り直す。</summary>
        private AudioClip _click;
        private int _clickMilliseconds = -1;

        /// <summary>完成したときの音を鳴らすか。設定の音ボタンで切り替える。</summary>
        public bool Sound { get => _sound; set => _sound = value; }

        /// <summary>合図の長さ（ミリ秒）。-1 なら端末に任せる、0 なら出さない。</summary>
        public int VibrationMilliseconds
        {
            get => _vibrationMilliseconds;
            set => _vibrationMilliseconds = Mathf.Clamp(value,
                SignalScale.DeviceDefault, FlickSettings.LongestVibrationMilliseconds);
        }

        /// <summary>合図の強さ。-1 なら端末に任せる、0 なら出さない。</summary>
        public int VibrationAmplitude
        {
            get => _vibrationAmplitude;
            set => _vibrationAmplitude = Mathf.Clamp(value,
                SignalScale.DeviceDefault, FlickSettings.StrongestVibrationAmplitude);
        }

        /// <summary>この端末で振動を出せるか。出せないときは音で知らせる。</summary>
        public bool CanVibrate => _haptics != null && _haptics.Available;

        /// <summary>振動を出せない理由。設定画面に出す。</summary>
        public string VibrationNote => _haptics != null ? _haptics.Note : "まだ調べていません";

        /// <summary>設定画面で決めた合図の長さと強さを受け取る。</summary>
        public void Apply(FlickSettings settings)
        {
            if (settings == null) return;

            VibrationMilliseconds = settings.VibrationMilliseconds;
            VibrationAmplitude = settings.VibrationAmplitude;
        }

        private void Awake()
        {
            _source = GetComponent<AudioSource>();
            _source.playOnAwake = false;
            _source.loop = false;

            // 効果音なので、距離で減衰させない。
            _source.spatialBlend = 0f;

            if (_congratulations == null)
                _congratulations = Resources.Load<AudioClip>(CongratulationsResourcePath);

            _haptics = new Haptics();

            // 振動しないとき、機種の話かこちらの落ち度かを実機で分けられるように、
            // どう判断したかを一度だけ書き出す。adb logcat -s Unity で読める。
            Debug.Log("はめ込みの合図: " + _haptics.Note);
        }

        private void OnDestroy()
        {
            _haptics?.Dispose();
            _haptics = null;

            // その場で作った音は誰も持っていない。置いていくと溜まる。
            if (_click != null) Destroy(_click);
            _click = null;
        }

        /// <summary>ピースがはまった。振動できる機種なら振動で、そうでなければ音で。</summary>
        public void Snapped() => Signal();

        /// <summary>
        /// 合図だけを出す。設定画面でつまみを動かしたとき、その場で確かめるため。
        /// 遊んでいるときと同じものが出るので、耳と指で決められる。
        /// </summary>
        public void TestSignal() => Signal();

        /// <summary>問題が完成した。おめでとうの音を鳴らす。</summary>
        public void Solved()
        {
            if (!_sound || _congratulations == null || _source == null) return;

            _source.PlayOneShot(_congratulations, _congratulationsVolume);
        }

        /// <summary>
        /// はめ込みの合図。振動子があれば振動、無ければ音。両方は出さない。
        /// </summary>
        private void Signal()
        {
            // つまみが「出さない」に入っている。振動も音も出さない。
            if (!SignalScale.Signals(_vibrationMilliseconds, _vibrationAmplitude)) return;

            if (CanVibrate)
            {
                _haptics.Tap(_vibrationMilliseconds, _vibrationAmplitude);
                return;
            }

            PlayClick();
        }

        private void PlayClick()
        {
            if (_source == null) return;

            // 強さ -1 は「端末に任せる」。音では、控えめな既定の大きさとして扱う。
            var volume = _vibrationAmplitude < 0
                ? DefaultVolume
                : _vibrationAmplitude / (float)FlickSettings.StrongestVibrationAmplitude;

            if (volume <= 0f) return;

            // 長さが変わったときだけ作り直す。同じ長さなら使い回す。
            // つまみを動かしている間は毎コマここへ来るので、
            // 作り直した古いほうを捨てないと、音が溜まって重くなる。
            if (_click == null || _clickMilliseconds != _vibrationMilliseconds)
            {
                if (_click != null) Destroy(_click);

                _click = ClickTone.Create(_vibrationMilliseconds);
                _clickMilliseconds = _vibrationMilliseconds;
            }

            // PlayOneShot なので、続けて鳴らしても前の音を切らない。
            _source.PlayOneShot(_click, volume);
        }
    }
}
