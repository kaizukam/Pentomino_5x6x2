using System;
using Pentomino.Core;
using UnityEngine;
using UnityEngine.UI;

namespace Pentomino.View
{
    /// <summary>
    /// 画面上部の帯。2026-09-13 のテスターの要望（DataBase/20260914R/12_9月14日.png）に対応する。
    ///
    ///   0〜250   : 緩衝。でっぱり（ノッチ等）の逃げ。右上に歯車だけ
    ///   250〜450 : タイトルの絵。未解決の問題では、しばらくすると畳まれて隠れる
    ///   450〜580 : Check / Hint / ← / → と、その右に級の帯の絵
    ///   580〜700 : それぞれの下に、回数 / 問題番号 / レベルの絵
    ///
    /// ピースを置く場所の近くにボタンがあると、置く拍子に Hint を押してしまう。
    /// そこでボタンは左へ寄せ、級の帯を右に置く。
    ///
    /// 級は帯の色で見せる。歯車・タイトル・級の帯・レベルは、白帯／黄帯／黒帯で
    /// 絵を差し替える。級の帯の絵は言語ごとにも別で、8 言語 x 3 級ある。
    /// レベルも級の帯と同じ書体にそろえるため絵にしてあり、10 段階 x 3 級ある。
    ///
    /// 中身はプレハブ（Assets/Resources/UI/HeaderBar.prefab）に置いてあるので、
    /// 位置・大きさは Unity のエディタ上で見たまま編集できる。
    /// このクラスは、その部品への参照と、押されたときの通知だけを持つ。
    /// </summary>
    [AddComponentMenu("Pentomino/Header Bar")]
    [RequireComponent(typeof(RectTransform))]
    public sealed class HeaderBar : MonoBehaviour
    {
        /// <summary>級の帯の絵。言語ひとつぶん。</summary>
        [Serializable]
        public sealed class GradeArt
        {
            public Language Language;
            public Sprite White;
            public Sprite Yellow;
            public Sprite Black;

            public Sprite For(Difficulty difficulty)
            {
                switch (difficulty)
                {
                    case Difficulty.Turn: return Yellow;
                    case Difficulty.Classic: return Black;
                    default: return White;
                }
            }

            public bool IsComplete => White != null && Yellow != null && Black != null;
        }

        /// <summary>レベルの絵。段階ひとつぶん。</summary>
        [Serializable]
        public sealed class LevelArt
        {
            public int Level;
            public Sprite White;
            public Sprite Yellow;
            public Sprite Black;

            public Sprite For(Difficulty difficulty)
            {
                switch (difficulty)
                {
                    case Difficulty.Turn: return Yellow;
                    case Difficulty.Classic: return Black;
                    default: return White;
                }
            }

            public bool IsComplete => White != null && Yellow != null && Black != null;
        }

        [Header("文字")]
        [SerializeField] private Text _number;

        [Tooltip("Check ボタンの下に出す累積回数")]
        [SerializeField] private Text _checkCount;

        [Tooltip("Hint ボタンの下に出す累積回数")]
        [SerializeField] private Text _hintCount;

        [Header("級で差し替える絵")]
        [Tooltip("タイトルの絵。級で差し替える")]
        [SerializeField] private Image _title;
        [SerializeField] private Sprite _titleWhite;
        [SerializeField] private Sprite _titleYellow;
        [SerializeField] private Sprite _titleBlack;

        [Tooltip("歯車の絵。黒帯だけ明るい歯車にする")]
        [SerializeField] private Image _gear;
        [SerializeField] private Sprite _gearDark;
        [SerializeField] private Sprite _gearLight;

        [Tooltip("級の帯の絵。言語と級で差し替える")]
        [SerializeField] private Image _grade;
        [SerializeField] private GradeArt[] _gradeArts;

        [Tooltip("レベルの絵。レベルと級で差し替える")]
        [SerializeField] private Image _level;
        [SerializeField] private LevelArt[] _levelArts;

        [Header("ボタン")]
        [Tooltip("設定モードに入る")]
        [SerializeField] private Button _settings;

        [SerializeField] private Button _check;
        [SerializeField] private Button _hint;

        [Header("問題送り（押し続けで早送り）")]
        [SerializeField] private StepButton _stepBack;
        [SerializeField] private StepButton _stepForward;

        [Header("長押し")]
        [SerializeField] private LongPressHandler _titleLongPress;

        [Header("畳む面（無ければ畳まない）")]
        [Tooltip("タイトル以下をまとめた面。窓（Pane）の中をこれが上下する")]
        [SerializeField] private RectTransform _foldContent;

        [Tooltip("表示中の面の位置。ここを動かせば表示中の割り付けが変わる")]
        [SerializeField] private RectTransform _foldShown;

        [Tooltip("畳んだあとの面の位置。ここを動かせば畳んだときの割り付けが変わる")]
        [SerializeField] private RectTransform _foldFolded;

        public RectTransform RectTransform => (RectTransform)transform;

        /// <summary>
        /// いまの帯の高さ。BOX の縦位置はこの下に続く。縮尺こみの見た目の高さ。
        /// 畳むと縮むので、BOX はそのぶん持ち上がる。
        /// </summary>
        public float Height =>
            (RectTransform.rect.height - FoldShift * _fold) * RectTransform.localScale.y;

        /// <summary>
        /// 畳む前の帯の高さ。BOX の大きさはこちらで決める。
        ///
        /// 大きさと位置で別の値を使うのが肝心。同じ値を使うと、畳んだときに
        /// 残り高さが増えたぶんセルが大きくなり、BOX ごと膨らんでしまう。
        /// 一番背が高い状態で収まるようにしておけば、畳んでも必ず収まる。
        /// </summary>
        public float FullHeight => RectTransform.rect.height * RectTransform.localScale.y;

        /// <summary>畳む仕掛けがプレハブに用意されているか。</summary>
        public bool CanFold => _foldContent != null && _foldShown != null && _foldFolded != null;

        /// <summary>畳む量。2 枚の目印の差なので、位置はプレハブが決める。</summary>
        private float FoldShift => CanFold
            ? Mathf.Max(0f, _foldFolded.anchoredPosition.y - _foldShown.anchoredPosition.y)
            : 0f;

        /// <summary>0 が表示中、1 が畳んだ状態。その間を滑らせる。</summary>
        private float _fold;
        private float _foldTarget;

        /// <summary>畳むのにかける時間（秒）。</summary>
        public float FoldSeconds { get; set; } = 0.25f;

        /// <summary>高さが変わった。BOX と待機場所を置き直してもらう。</summary>
        public event Action HeightChanged;

        /// <summary>
        /// タイトル以下を畳む／戻す。
        /// 動かすのは面の位置ひとつだけなので、中の割り付けはプレハブのまま保たれる。
        /// </summary>
        public void SetFolded(bool folded, bool immediately = false)
        {
            if (!CanFold) return;

            _foldTarget = folded ? 1f : 0f;
            if (immediately || FoldSeconds <= 0f) _fold = _foldTarget;

            ApplyFold();
        }

        private void Update()
        {
            if (!CanFold || Mathf.Approximately(_fold, _foldTarget)) return;

            _fold = Mathf.MoveTowards(_fold, _foldTarget, Time.unscaledDeltaTime / FoldSeconds);
            ApplyFold();
        }

        private void ApplyFold()
        {
            if (!CanFold) return;

            _foldContent.anchoredPosition = Vector2.Lerp(
                _foldShown.anchoredPosition, _foldFolded.anchoredPosition, _fold);

            HeightChanged?.Invoke();
        }

        /// <summary>
        /// 帯を丸ごと拡大縮小して、指定の幅ちょうどに収める。
        ///
        /// プレハブは基準（1080 幅から左右の余白を除いた幅）で組んであるので、
        /// 余白の設定を変えると BOX の幅とプレハブの幅が食い違ってしまう。
        /// 幅だけ引き伸ばすとボタンの間隔だけが伸びて字が取り残されるので、
        /// 縮尺そのものを変えて、プレハブで見たとおりの割り付けを保つ。
        /// </summary>
        public void FitWidth(float width)
        {
            var design = RectTransform.rect.width;
            if (design <= 0f || width <= 0f) return;

            var scale = width / design;
            RectTransform.localScale = new Vector3(scale, scale, 1f);
        }

        public LongPressHandler TitleLongPress => _titleLongPress;

        /// <summary>いま問題送りのボタンが押されているか。押している間はタイトルを畳まない。</summary>
        public bool IsStepping =>
            (_stepBack != null && _stepBack.IsPressing)
            || (_stepForward != null && _stepForward.IsPressing);

        /// <summary>送りの繰り返しが始まるまでの時間（秒）。プレハブの値をそのまま返す。</summary>
        public float StepHoldDelay =>
            _stepForward != null ? _stepForward.HoldDelay : StepRepeatRule.DefaultHoldDelay;

        public Button SettingsButton => _settings;

        /// <summary>問題送り。引数は移動量（±1 または ±10）。</summary>
        public event Action<int> StepRequested;


        public event Action SettingsRequested;

        public event Action CheckRequested;

        public event Action HintRequested;

        /// <summary>説明ボタンが押されたとき。</summary>
        private void Awake()
        {
            WireStep(_stepBack, -1);
            WireStep(_stepForward, 1);

            if (_check != null) _check.onClick.AddListener(() => CheckRequested?.Invoke());
            if (_hint != null) _hint.onClick.AddListener(() => HintRequested?.Invoke());
            if (_settings != null) _settings.onClick.AddListener(() => SettingsRequested?.Invoke());

            if (_titleLongPress != null) _titleLongPress.LongPressed += () => SettingsRequested?.Invoke();
        }

        private void WireStep(StepButton button, int direction)
        {
            if (button == null) return;
            button.Direction = direction;
            button.Stepped += delta => StepRequested?.Invoke(delta);
        }

        /// <summary>長押しの待ち時間を上書きする。</summary>
        public void SetLongPressSeconds(float seconds)
        {
            if (_titleLongPress != null) _titleLongPress.Threshold = seconds;
        }

        /// <summary>表示を今の状態に合わせる。</summary>
        public void Refresh(PuzzleSession session)
        {
            if (session == null) return;

            ApplyTheme(session.Difficulty);
            SetText(_number, session.Puzzle.Number.ToString("0000"));
            SetLevel(session.Puzzle.Level, session.Difficulty);
            SetText(_checkCount, session.CheckCount.ToString());
            SetText(_hintCount, session.HintCount.ToString());
        }

        /// <summary>
        /// 級に合わせて絵と字の色を差し替える。
        ///
        /// 級の名前はどの言語でも同じ綴りなので、読めない言語の人には
        /// 手掛かりにならない。帯の色（白・黄・黒）なら、字を読まずに判る。
        /// 級の帯の絵は言語ごとに描いてあるので、いまの言語のものを出す。
        /// </summary>
        private void ApplyTheme(Difficulty difficulty)
        {
            SetSprite(_title, TitleFor(difficulty));
            SetSprite(_gear, GradeTheme.IsDark(difficulty) ? _gearLight : _gearDark);

            var art = GradeArtFor(Strings.Current);
            SetSprite(_grade, art != null ? art.For(difficulty) : null);

            // 地の上に直に置く字は、地の明るさで色を変える。
            // 回数は白い枠の中なので、そのまま。
            if (_number != null) _number.color = GradeTheme.Ink(difficulty);
        }

        /// <summary>
        /// レベルの絵を出す。絵は 1〜10 のぶんしか無い（出題データもその範囲）。
        /// 万一その外のレベルが来たら、違う数字を出すよりは何も出さない。
        /// </summary>
        private void SetLevel(int level, Difficulty difficulty)
        {
            if (_level == null) return;

            var art = LevelArtFor(level);
            var sprite = art != null ? art.For(difficulty) : null;
            _level.enabled = sprite != null;
            SetSprite(_level, sprite);
        }

        private LevelArt LevelArtFor(int level)
        {
            if (_levelArts == null) return null;

            foreach (var art in _levelArts)
                if (art != null && art.Level == level) return art;
            return null;
        }

        private Sprite TitleFor(Difficulty difficulty)
        {
            switch (difficulty)
            {
                case Difficulty.Turn: return _titleYellow;
                case Difficulty.Classic: return _titleBlack;
                default: return _titleWhite;
            }
        }

        /// <summary>その言語の級の帯。無ければ英語で代用する。</summary>
        private GradeArt GradeArtFor(Language language)
        {
            if (_gradeArts == null) return null;

            GradeArt english = null;
            foreach (var art in _gradeArts)
            {
                if (art == null) continue;
                if (art.Language == language) return art;
                if (art.Language == Language.English) english = art;
            }
            return english;
        }

        private static void SetSprite(Image image, Sprite sprite)
        {
            if (image != null && sprite != null) image.sprite = sprite;
        }

        private static void SetText(Text text, string value)
        {
            if (text != null) text.text = value;
        }

        /// <summary>プレハブに参照を付け忘れていないか調べる（テスト・生成ツール用）。</summary>
        public bool Validate(out string missing)
        {
            missing = null;

            if (_number == null) missing = "Number";
            else if (_checkCount == null) missing = "CheckCount";
            else if (_hintCount == null) missing = "HintCount";
            else if (_title == null) missing = "Title";
            else if (_titleWhite == null) missing = "TitleWhite";
            else if (_titleYellow == null) missing = "TitleYellow";
            else if (_titleBlack == null) missing = "TitleBlack";
            else if (_gear == null) missing = "Gear";
            else if (_gearDark == null) missing = "GearDark";
            else if (_gearLight == null) missing = "GearLight";
            else if (_grade == null) missing = "Grade";
            else if (_level == null) missing = "Level";
            else if (_settings == null) missing = "Settings";
            else if (_check == null) missing = "Check";
            else if (_hint == null) missing = "Hint";
            else if (_stepBack == null) missing = "StepBack";
            else if (_stepForward == null) missing = "StepForward";
            else if (_titleLongPress == null) missing = "TitleLongPress";
            else missing = MissingGradeArt() ?? MissingLevelArt();

            return missing == null;
        }

        /// <summary>
        /// 畳む面の参照を入れる。移行ツール（HeaderFoldMigration）から呼ぶ。
        /// 実行時には使わない。
        /// </summary>
        public void AssignFold(RectTransform content, RectTransform shown, RectTransform folded)
        {
            _foldContent = content;
            _foldShown = shown;
            _foldFolded = folded;
        }

        /// <summary>言語ごとの級の帯が、全部そろっているか。足りない物の名前を返す。</summary>
        private string MissingGradeArt()
        {
            foreach (var language in Languages.All)
            {
                var art = GradeArtFor(language);
                if (art == null || art.Language != language) return "GradeArt(" + language + ")";
                if (!art.IsComplete) return "GradeArt(" + language + ") の絵";
            }
            return null;
        }

        /// <summary>レベルの絵が 1〜10 までそろっているか。足りない物の名前を返す。</summary>
        private string MissingLevelArt()
        {
            for (var level = 1; level <= LevelArtCount; level++)
            {
                var art = LevelArtFor(level);
                if (art == null) return "LevelArt(" + level + ")";
                if (!art.IsComplete) return "LevelArt(" + level + ") の絵";
            }
            return null;
        }

        /// <summary>用意してあるレベルの絵の数。出題データのレベルは 1〜10。</summary>
        public const int LevelArtCount = 10;

        /// <summary>生成ツールから参照をまとめて設定する。</summary>
        internal void Assign(Text number, Text checkCount, Text hintCount,
            Button settings, Button check, Button hint,
            StepButton stepBack, StepButton stepForward,
            LongPressHandler titleLongPress)
        {
            _number = number;
            _checkCount = checkCount;
            _hintCount = hintCount;
            _check = check;
            _hint = hint;
            _settings = settings;
            _stepBack = stepBack;
            _stepForward = stepForward;
            _titleLongPress = titleLongPress;
        }

        /// <summary>生成ツールから、級で差し替える絵をまとめて設定する。</summary>
        internal void AssignArt(Image title, Sprite titleWhite, Sprite titleYellow, Sprite titleBlack,
            Image gear, Sprite gearDark, Sprite gearLight,
            Image grade, GradeArt[] gradeArts,
            Image level, LevelArt[] levelArts)
        {
            _title = title;
            _titleWhite = titleWhite;
            _titleYellow = titleYellow;
            _titleBlack = titleBlack;
            _gear = gear;
            _gearDark = gearDark;
            _gearLight = gearLight;
            _grade = grade;
            _gradeArts = gradeArts;
            _level = level;
            _levelArts = levelArts;
        }
    }
}
