using System;
using Pentomino.Core;
using UnityEngine;
using UnityEngine.UI;

namespace Pentomino.View
{
    /// <summary>
    /// 設定モードの画面（開発仕様「設定モード」）。
    /// 難易度と言語をボタンで選ぶ。
    ///
    /// 途中経過は難易度ごとに別々に残るので、難易度を行き来しても何も消えない。
    /// 消したいときは、その難易度の「消す」ボタンを押す。押し間違いで記録が
    /// 消えると取り返しがつかないので、必ず一度問いかける。
    ///
    /// 中身はプレハブ（Assets/Resources/UI/SettingsPanel.prefab）に置いてあるので、
    /// 文字の大きさ・位置・色はエディタ上で見たまま編集できる。
    /// </summary>
    [AddComponentMenu("Pentomino/Settings Panel")]
    [RequireComponent(typeof(RectTransform))]
    public sealed class SettingsPanel : MonoBehaviour
    {
        /// <summary>難易度ボタンの並び。プレハブの _gradeButtons もこの順に入れる。</summary>
        public static readonly Difficulty[] Grades =
        {
            Difficulty.Guided, Difficulty.Turn, Difficulty.Classic,
        };

        [Header("文字")]
        [SerializeField] private Text _title;
        [SerializeField] private Text _difficultyLabel;
        [SerializeField] private Text _warning;
        [SerializeField] private Text _languageLabel;

        [Header("ボタン")]
        [Tooltip("ガイド・ターン・クラシックの順")]
        [SerializeField] private Button[] _gradeButtons;

        [Tooltip("各難易度の下に添える一行説明。_gradeButtons と同じ順")]
        [SerializeField] private Text[] _gradeNotes;

        [Tooltip("各難易度の記録を消すボタン。_gradeButtons と同じ順")]
        [SerializeField] private Button[] _gradeResets;

        [Tooltip("Languages.All と同じ順（英語・日本語・スペイン語・フランス語・ドイツ語・中国語1・中国語2）")]
        [SerializeField] private Button[] _languageButtons;

        [Tooltip("説明書を開く。メイン画面から移した")]
        [SerializeField] private Button _manual;

        [Tooltip("音の入り切り。切ると、はまる音も完成の音も鳴らない")]
        [SerializeField] private Button _sound;

        [Tooltip("フリックの感度調整の画面を開く")]
        [SerializeField] private Button _flickTuning;

        [SerializeField] private Button _close;

        [Header("記録を消す前の問いかけ")]
        [Tooltip("問いかけの覆い。ふだんは隠しておく")]
        [SerializeField] private GameObject _confirm;

        [SerializeField] private Text _confirmMessage;
        [SerializeField] private Button _confirmYes;
        [SerializeField] private Button _confirmNo;

        [Header("選択中の色")]
        [SerializeField] private Color _selectedColor = new Color(0.58f, 0.80f, 0.94f);
        [SerializeField] private Color _normalColor = new Color(0.94f, 0.94f, 0.94f);

        private Difficulty _difficulty;
        private Language _language;
        private bool _soundOn = true;
        private bool _wired;

        /// <summary>問いかけの最中に、どの難易度を消そうとしているか。</summary>
        private Difficulty _pendingReset;

        /// <summary>難易度が選ばれたとき。進捗の初期化は受け手側で行う。</summary>
        public event Action<Difficulty> DifficultyChosen;

        /// <summary>その難易度の記録を消すと決まったとき。問いかけに答えたあとに出る。</summary>
        public event Action<Difficulty> RecordCleared;

        /// <summary>言語が選ばれたとき。</summary>
        public event Action<Language> LanguageChosen;

        /// <summary>説明書を開きたい。</summary>
        public event Action ManualRequested;

        /// <summary>音の入り切りが変わったとき。</summary>
        public event Action<bool> SoundChanged;

        /// <summary>感度調整の画面を開きたい。</summary>
        public event Action FlickTuningRequested;

        /// <summary>閉じるボタンが押されたとき。</summary>
        public event Action CloseRequested;

        public bool IsOpen => gameObject.activeSelf;

        private void Awake() => Wire();

        private void Wire()
        {
            if (_wired) return;
            _wired = true;

            if (_gradeButtons != null)
            {
                for (var i = 0; i < _gradeButtons.Length && i < Grades.Length; i++)
                {
                    if (_gradeButtons[i] == null) continue;
                    var grade = Grades[i];
                    _gradeButtons[i].onClick.AddListener(() => OnGradeClicked(grade));
                }
            }

            if (_gradeResets != null)
            {
                for (var i = 0; i < _gradeResets.Length && i < Grades.Length; i++)
                {
                    if (_gradeResets[i] == null) continue;
                    var grade = Grades[i];
                    _gradeResets[i].onClick.AddListener(() => AskClearRecord(grade));
                }
            }

            if (_confirmYes != null) _confirmYes.onClick.AddListener(ConfirmClearRecord);
            if (_confirmNo != null) _confirmNo.onClick.AddListener(HideConfirm);
            HideConfirm();

            if (_languageButtons != null)
            {
                for (var i = 0; i < _languageButtons.Length && i < Languages.All.Length; i++)
                {
                    if (_languageButtons[i] == null) continue;
                    var language = Languages.All[i];
                    _languageButtons[i].onClick.AddListener(() => OnLanguageClicked(language));
                }
            }

            if (_close != null) _close.onClick.AddListener(() => CloseRequested?.Invoke());
            if (_flickTuning != null)
                _flickTuning.onClick.AddListener(() => FlickTuningRequested?.Invoke());
            if (_manual != null) _manual.onClick.AddListener(() => ManualRequested?.Invoke());
            if (_sound != null) _sound.onClick.AddListener(OnSoundClicked);
        }

        /// <summary>いまの設定を反映して開く。</summary>
        public void Open(Difficulty difficulty, Language language, bool sound = true)
        {
            Wire();
            _difficulty = difficulty;
            _language = language;
            _soundOn = sound;
            gameObject.SetActive(true);
            HideConfirm();
            Refresh();
        }

        public void Close()
        {
            HideConfirm();
            gameObject.SetActive(false);
        }

        /// <summary>文言と選択状態を描き直す。</summary>
        public void Refresh()
        {
            SetText(_title, Strings.Get(StringId.Settings, _language));
            SetText(_difficultyLabel, Strings.Get(StringId.Difficulty, _language));
            SetText(_warning, Strings.Get(StringId.DifficultyWarning, _language));
            SetText(_languageLabel, Strings.Get(StringId.LanguageLabel, _language));

            if (_close != null) SetText(LabelOf(_close), Strings.Get(StringId.Close, _language));
            if (_flickTuning != null)
                SetText(LabelOf(_flickTuning), Strings.Get(StringId.FlickTuning, _language));
            if (_manual != null) SetText(LabelOf(_manual), Strings.Get(StringId.Manual, _language));

            if (_sound != null)
            {
                // ON / OFF はどの言語でもそのまま通じるので、訳さずに添える。
                SetText(LabelOf(_sound),
                    Strings.Get(StringId.SoundLabel, _language) + "   " + (_soundOn ? "ON" : "OFF"));
                Highlight(_sound, _soundOn);
            }

            if (_gradeButtons != null)
            {
                for (var i = 0; i < _gradeButtons.Length && i < Grades.Length; i++)
                {
                    if (_gradeButtons[i] == null) continue;
                    SetText(LabelOf(_gradeButtons[i]), Strings.GradeName(Grades[i], _language));

                    // 級のボタンは帯の絵そのもの。選ばれているかは枠の太さではなく
                    // 明るさで示す。絵を塗り替えると帯の色が濁る。
                    Fade(_gradeButtons[i], Grades[i] == _difficulty);

                    if (_gradeNotes != null && i < _gradeNotes.Length)
                        SetText(_gradeNotes[i], Strings.GradeNote(Grades[i], _language));

                    if (_gradeResets != null && i < _gradeResets.Length)
                        SetText(LabelOf(_gradeResets[i]), Strings.Get(StringId.ClearRecord, _language));
                }
            }

            if (_confirmYes != null)
                SetText(LabelOf(_confirmYes), Strings.Get(StringId.ClearRecordYes, _language));
            if (_confirmNo != null)
                SetText(LabelOf(_confirmNo), Strings.Get(StringId.Cancel, _language));

            if (_languageButtons == null) return;
            for (var i = 0; i < _languageButtons.Length && i < Languages.All.Length; i++)
            {
                if (_languageButtons[i] == null) continue;
                SetText(LabelOf(_languageButtons[i]), Languages.All[i].NativeName());
                Highlight(_languageButtons[i], Languages.All[i] == _language);
            }
        }

        private void OnGradeClicked(Difficulty grade)
        {
            if (grade == _difficulty) return;

            _difficulty = grade;
            Refresh();
            DifficultyChosen?.Invoke(grade);
        }

        /// <summary>
        /// 消していいかを尋ねる。ここではまだ何も消さない。
        /// どの難易度の話かが判るよう、問いかけに級の名前を添える。
        /// </summary>
        private void AskClearRecord(Difficulty grade)
        {
            _pendingReset = grade;

            SetText(_confirmMessage, Strings.GradeName(grade, _language) + "\n"
                                     + Strings.Get(StringId.ClearRecordAsk, _language));

            if (_confirm != null) _confirm.SetActive(true);
        }

        /// <summary>問いかけに「消す」と答えられた。</summary>
        private void ConfirmClearRecord()
        {
            HideConfirm();
            RecordCleared?.Invoke(_pendingReset);
        }

        private void HideConfirm()
        {
            if (_confirm != null) _confirm.SetActive(false);
        }

        private void OnSoundClicked()
        {
            _soundOn = !_soundOn;
            Refresh();
            SoundChanged?.Invoke(_soundOn);
        }

        private void OnLanguageClicked(Language language)
        {
            if (language == _language) return;

            _language = language;
            Refresh();
            LanguageChosen?.Invoke(language);
        }

        /// <summary>
        /// 絵柄のボタンを、選ばれていないときだけ淡くする。
        /// 色を塗り替えると絵が濁るので、明るさだけを動かす。
        /// </summary>
        private static void Fade(Button button, bool selected)
        {
            var image = button.targetGraphic as Image;
            if (image == null) image = button.GetComponent<Image>();
            if (image == null) return;

            image.color = selected ? Color.white : new Color(1f, 1f, 1f, 0.45f);
        }

        private void Highlight(Button button, bool selected)
        {
            var image = button.targetGraphic as Image;
            if (image == null) image = button.GetComponent<Image>();
            if (image == null) return;

            image.color = selected ? _selectedColor : _normalColor;
        }

        /// <summary>
        /// 言語ボタンの文字に、そのボタン自身の言語の書体を当てる。
        ///
        /// ボタンに出るのは「日本語」「한국어」「简体中文」のような、
        /// その言語自身での名前。いま選んでいる画面の言語の書体で描くと、
        /// 欧文の書体には CJK の字が無いため豆腐になってしまう。
        /// ここだけは、画面の言語ではなくボタンの言語で書体を選ぶ。
        /// </summary>
        public void ApplyLanguageFonts(FontApplier fonts)
        {
            if (fonts == null || _languageButtons == null) return;

            for (var i = 0; i < _languageButtons.Length && i < Languages.All.Length; i++)
                fonts.Apply(LabelOf(_languageButtons[i]), Languages.All[i]);
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
            else if (_difficultyLabel == null) missing = "DifficultyLabel";
            else if (_warning == null) missing = "Warning";
            else if (_languageLabel == null) missing = "LanguageLabel";
            else if (_flickTuning == null) missing = "FlickTuning";
            else if (_manual == null) missing = "Manual";
            else if (_sound == null) missing = "Sound";
            else if (_close == null) missing = "Close";
            else if (_gradeButtons == null || _gradeButtons.Length != Grades.Length) missing = "GradeButtons";
            else if (_gradeResets == null || _gradeResets.Length != Grades.Length) missing = "GradeResets";
            else if (_confirm == null) missing = "Confirm";
            else if (_confirmMessage == null) missing = "ConfirmMessage";
            else if (_confirmYes == null) missing = "ConfirmYes";
            else if (_confirmNo == null) missing = "ConfirmNo";
            else if (_languageButtons == null || _languageButtons.Length != Languages.All.Length)
                missing = "LanguageButtons";

            return missing == null;
        }

        /// <summary>
        /// 記録を消す仕掛けの参照を入れる。移行ツール（SettingsResetMigration）から呼ぶ。
        /// 実行時には使わない。
        /// </summary>
        public void AssignReset(Button[] gradeResets,
            GameObject confirm, Text message, Button yes, Button no)
        {
            _gradeResets = gradeResets;
            _confirm = confirm;
            _confirmMessage = message;
            _confirmYes = yes;
            _confirmNo = no;
        }

        /// <summary>
        /// 説明書と音のボタンの参照を入れる。移行ツールから呼ぶ。実行時には使わない。
        /// </summary>
        public void AssignExtras(Button manual, Button sound)
        {
            _manual = manual;
            _sound = sound;
        }

        /// <summary>生成ツールから参照をまとめて設定する。</summary>
        internal void Assign(Text title, Text difficultyLabel, Text warning, Text languageLabel,
            Button[] gradeButtons, Text[] gradeNotes, Button[] languageButtons,
            Button flickTuning, Button close)
        {
            _flickTuning = flickTuning;
            _title = title;
            _difficultyLabel = difficultyLabel;
            _warning = warning;
            _languageLabel = languageLabel;
            _gradeButtons = gradeButtons;
            _gradeNotes = gradeNotes;
            _languageButtons = languageButtons;
            _close = close;
        }
    }
}
