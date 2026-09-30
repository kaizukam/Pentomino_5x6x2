using System;
using System.Collections.Generic;
using Pentomino.Core;
using Pentomino.Data;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Pentomino.View
{
    /// <summary>
    /// パズル画面の本体。縦持ち前提で、上から
    /// 帯（緩衝・タイトル・ボタン・数字）→ BOX 格子図 → 未収納ピースの待機場所 の順に詰めて並べる。
    ///
    /// 基準は幅 1080・高さ 2340。機種の幅が違えば比例で縮める。
    /// 縦横比が 1:2.14 より横長なら、幅を高さの 1/2.14 に絞って横中央に置く。
    ///
    /// BOX は画面幅いっぱいに広げ、待機場所も同じセルサイズ・同じ幅に収める。
    /// 入りきらない分は画面より下に続き、縦スクロールで見る（横はスクロールしない）。
    /// </summary>
    [AddComponentMenu("Pentomino/Game Screen")]
    public sealed class GameScreen : MonoBehaviour, IPieceInteraction
    {
        [Header("レイアウト（幅 1080 基準のピクセル）")]
        [SerializeField] private float _sideMargin = 18f;

        [Tooltip("帯の上に空ける量。でっぱりの逃げは帯の中（緩衝の 250）に取ってあるので 0")]
        [SerializeField] private float _topMargin = 0f;
        [SerializeField] private float _navRowHeight = 104f;
        [SerializeField] private float _navRowGap = 30f;
        [SerializeField] private float _boardGap = 24f;

        [Tooltip("待機場所でピース同士を空ける間隔。ほぼ密着させる")]
        [SerializeField] private float _trayGap = 5f;





        [Header("はめ込み")]
        [Tooltip("置ける場所にこの距離まで近づくと吸い込まれる")]
        [SerializeField] private float _snapAttractDistance = SnapRule.DefaultAttractDistance;

        [Tooltip("指がこの距離まで離れると吸い付きが外れる")]
        [SerializeField] private float _snapReleaseDistance = SnapRule.DefaultReleaseDistance;

        [Tooltip("はまったときの音と振動。空なら実行時に作る")]
        [SerializeField] private Feedback _feedback;

        /// <summary>子画面の後ろに敷く覆い。組み立てのときに作る。</summary>
        private Image _backdrop;

        /// <summary>いちばん奥の地。級の帯の色（白・黄・黒）で塗る。</summary>
        private Image _background;

        /// <summary>直前に成績パネルが出ていたか。出た瞬間だけ音を鳴らす。</summary>
        private bool _resultWasOpen;

        [Header("見た目")]
        [SerializeField] private PentominoStyle _style = new PentominoStyle();

        [Tooltip("ピースを置く指の操作しやすさ。BOX の下にこれだけは残す（基準ピクセル）")]
        [SerializeField] private float _minTrayHeight = 240f;

        [Tooltip("問題を切り替えてから、タイトルと難易度を畳むまでの時間（秒）")]
        [SerializeField] private float _headerFoldSeconds = 2f;

        [Header("開始時の問題")]
        [SerializeField] private int _startPuzzle;

        [Header("UI プレハブ（未指定なら Resources/UI から読む）")]
        [SerializeField] private HeaderBar _headerPrefab;
        [SerializeField] private SettingsPanel _settingsPrefab;
        [SerializeField] private ManualPanel _manualPrefab;
        [SerializeField] private ResultPanel _resultPrefab;
        [SerializeField] private FlickTuningPanel _flickTuningPrefab;

        [Header("設定モード")]
        [Tooltip("パズル名を長押しして設定モードに入るまでの時間（秒）")]
        [SerializeField] private float _titleLongPressSeconds = 0.6f;

        private readonly FontApplier _fonts = new FontApplier();
        private readonly Dictionary<char, PieceWidget> _widgets = new Dictionary<char, PieceWidget>();
        private readonly List<PolyominoView> _fixedViews = new List<PolyominoView>();

        private Canvas _canvas;
        private RectTransform _root;
        private HeaderBar _header;

        /// <summary>待機場所の枠。畳んだときに上端だけ動かす。</summary>
        private RectTransform _trayRect;

        /// <summary>いまタイトル以下を畳んでいるか。組み直しのあとも引き継ぐ。</summary>
        private bool _folded;

        /// <summary>この時刻になったら畳む。負なら予定なし。</summary>
        private float _foldAt = -1f;
        private BoardWidget _board;
        private ScrollRect _trayScroll;
        private RectTransform _trayContent;
        private RectTransform _dragLayer;
        private ResultPanel _resultPanel;
        private SettingsPanel _settings;
        private ManualPanel _manual;
        private FlickTuningPanel _flickTuning;

        private ProgressStore _store;
        private GameProgress _progress;
        private PuzzleSession _session;

        private PieceWidget _floating;      // 盤外で指を離してその場に留まっているピース
        private PieceWidget _dragging;
        private Vector3 _grabOffset;
        private bool _snapped;
        private int _snapRow;
        private int _snapCol;
        private bool _refreshRequested;

        /// <summary>表示に使う幅。左右の余白を除いた、中身が入る幅。</summary>
        private float _contentWidth;

        /// <summary>中身の左端。画面の横中央に来るように置く。</summary>
        private float _contentLeft;

        /// <summary>前に組み立てたときの画面の大きさ。向きが変わったかを見る。</summary>
        private Vector2 _builtFor;

        /// <summary>STOP ボタンが押されたとき。既定では何もしない（進捗は保存済み）。</summary>
        public event Action StopRequested;

        public PuzzleSession Session => _session;

        /// <summary>ナビゲーションの帯。</summary>
        public HeaderBar Header => _header;

        /// <summary>フリックの感度調整のパネル。</summary>
        public FlickTuningPanel FlickTuningPanel => _flickTuning;

        /// <summary>説明書のパネル。</summary>
        public ManualPanel ManualPanel => _manual;

        /// <summary>完成したときに出す成績パネル。</summary>
        public ResultPanel ResultPanel => _resultPanel;

        /// <summary>BOX の格子図。</summary>
        public BoardWidget BoardWidget => _board;

        /// <summary>パズル名の長押し判定。</summary>
        public LongPressHandler TitleLongPress => _header != null ? _header.TitleLongPress : null;

        /// <summary>
        /// 表示に使う縦横比（高さ ÷ 幅）。これより横長の画面では幅を絞り、横中央に置く。
        ///
        /// 2.0 では余白が足りなかったので 2.14 にした（開発仕様「操作性の問題(3)」）。
        /// 場面（Game.unity）に古い値が残ると気づけないので、あえて定数にしてある。
        /// </summary>
        private const float DisplayAspect = 2.14f;

        private void Awake()
        {
            // 指の位置は 1 コマに 1 回しか読めない。既定の毎秒 30 コマでは
            // つまみもピースも飛び飛びに付いてくる。まずここで上げておく。
            FrameRate.Raise();

            _store = new ProgressStore();
            _progress = _store.Load();
            Strings.Current = _progress.Language;

            // はまったときの音と振動。場面に置いていなければ、ここで作る。
            if (_feedback == null) _feedback = gameObject.AddComponent<Feedback>();
            _feedback.Sound = _progress.sound;
            _feedback.Apply(_progress.Flick);

            BuildHierarchy();
            LoadPuzzle(_startPuzzle > 0 ? _startPuzzle : Mathf.Max(1, _progress.LastPuzzle));
        }

        private void LateUpdate()
        {
            RebuildIfScreenChanged();
            UpdateHeaderFold();

            if (!_refreshRequested) return;
            _refreshRequested = false;
            RefreshPieces();
        }

        /// <summary>
        /// 画面の大きさが変わったら組み直す。
        ///
        /// 全方向に対応するので、向きが変わると縦横が入れ替わり、
        /// 使える幅も変わる。組み立て直さないと、そのままの割り付けが残ってしまう。
        /// 遊んでいる途中の状態はセッションが持っているので、見た目だけ作り直せばよい。
        /// </summary>
        private void RebuildIfScreenChanged()
        {
            var screen = RootSize();
            if (Mathf.Approximately(screen.x, _builtFor.x)
                && Mathf.Approximately(screen.y, _builtFor.y)) return;

            var settingsOpen = _settings != null && _settings.IsOpen;
            var manualOpen = _manual != null && _manual.IsOpen;

            Rebuild();

            if (settingsOpen) OpenSettings();
            if (manualOpen) OnOpenManual();
        }

        /// <summary>画面を作り直す。遊んでいる途中の状態はそのまま。</summary>
        private void Rebuild()
        {
            for (var i = _root.childCount - 1; i >= 0; i--)
            {
                var child = _root.GetChild(i);
                child.SetParent(null, false);
                Destroy(child.gameObject);
            }

            _widgets.Clear();
            _fixedViews.Clear();
            _floating = null;
            _dragging = null;

            // 消した物を掴んだままにしない。組み立て途中に古い BOX を動かしてしまう。
            _board = null;
            _trayRect = null;
            _trayContent = null;
            _resultPanel = null;

            _fonts.Forget();

            BuildHierarchy();
            RefreshPieces();
        }

        // ------------------------------------------------------------------ 構築

        private void BuildHierarchy()
        {
            _canvas = GetComponentInParent<Canvas>();
            if (_canvas == null) _canvas = gameObject.AddComponent<Canvas>();

            _root = transform as RectTransform;
            if (_root == null) _root = gameObject.AddComponent<RectTransform>();
            _root.anchorMin = Vector2.zero;
            _root.anchorMax = Vector2.one;
            _root.offsetMin = Vector2.zero;
            _root.offsetMax = Vector2.zero;

            // いちばん奥に単色の地を敷く。色は級で変わる（GradeTheme）。
            // これが無いとカメラのスカイボックス（地平線の模様）が透けて見える。
            var background = UiFactory.CreateRect(_root, "Background");
            background.anchorMin = Vector2.zero;
            background.anchorMax = Vector2.one;
            background.offsetMin = Vector2.zero;
            background.offsetMax = Vector2.zero;
            background.SetAsFirstSibling();

            _background = background.gameObject.AddComponent<Image>();
            _background.raycastTarget = false;
            ApplyBackground();

            // 子画面の後ろに敷く覆い。開いたときだけ出す。
            var backdrop = UiFactory.CreateRect(_root, "Backdrop");
            backdrop.anchorMin = Vector2.zero;
            backdrop.anchorMax = Vector2.one;
            backdrop.offsetMin = Vector2.zero;
            backdrop.offsetMax = Vector2.zero;

            _backdrop = backdrop.gameObject.AddComponent<Image>();
            _backdrop.color = Color.white;

            // 後ろの遊び面に触れないよう、当たりはここで止める。
            _backdrop.raycastTarget = true;
            backdrop.gameObject.SetActive(false);

            var screen = RootSize();
            _builtFor = screen;

            // 表示に使う幅を決める（開発仕様「画面対応」）。
            // 細長い画面なら横幅いっぱいに使う。そうでない画面（横向きや iPad）は
            // DisplayAspect の比まで抑えて横中央に置く。
            // 横いっぱいに広げると BOX が縦に収まらず、盤が小さくなってしまうため。
            var display = Mathf.Min(screen.x, screen.y / DisplayAspect);

            _contentWidth = display - _sideMargin * 2f;
            _contentLeft = (screen.x - display) * 0.5f + _sideMargin;

            var contentWidth = _contentWidth;

            // BOX が横幅いっぱいになるようセルサイズを決める。L1 のはみ出し分も勘定に入れる。
            _style.cellSize = contentWidth / (Board.Cols + _style.outerLineRatio);

            BuildHeader(contentWidth);

            // iPad のように横長の画面では、幅だけで決めると縦が足りなくなる。
            // 帯と BOX と最低限の待機場所が収まるところまでセルを縮める。
            FitCellSizeToHeight();

            // 帯の高さはプレハブ側で決まるので、そこから BOX の位置を出す。
            var boardTop = _topMargin + HeaderHeight + _boardGap;
            _board = new GameObject("Board", typeof(RectTransform), typeof(BoardWidget))
                .GetComponent<BoardWidget>();
            _board.transform.SetParent(_root, false);
            SetTopLeft(_board.RectTransform, _contentLeft, boardTop);
            _board.Build(_style);

            var trayTop = boardTop + _board.Size.y + _boardGap;
            BuildTray(contentWidth, trayTop);

            _dragLayer = UiFactory.CreateRect(_root, "DragLayer");
            _dragLayer.anchorMin = Vector2.zero;
            _dragLayer.anchorMax = Vector2.one;
            _dragLayer.offsetMin = Vector2.zero;
            _dragLayer.offsetMax = Vector2.zero;

            BuildResultPanel(contentWidth, trayTop);
            BuildSettingsPanel();
            BuildManualPanel();
            BuildFlickTuningPanel();

            // 画面ができあがってから、その言語の書体を配る。
            ApplyFonts();
        }

        /// <summary>フリックの感度調整の画面をプレハブから差し込む。</summary>
        private void BuildFlickTuningPanel()
        {
            var prefab = _flickTuningPrefab != null
                ? _flickTuningPrefab
                : Resources.Load<FlickTuningPanel>("UI/FlickTuningPanel");
            if (prefab == null)
            {
                Debug.LogWarning("FlickTuningPanel のプレハブが見つかりません。"
                                 + "メニュー Pentomino ▸ UI プレハブを生成 を実行してください。");
                return;
            }

            _flickTuning = Instantiate(prefab, _root);
            _flickTuning.name = "FlickTuningPanel";
            FitPanel((RectTransform)_flickTuning.transform);

            _flickTuning.Changed += OnFlickSettingsChanged;
            _flickTuning.VibrationTried += OnVibrationTried;
            _flickTuning.CloseRequested += OnCloseFlickTuning;
            _flickTuning.Close();
        }

        private void OnOpenFlickTuning()
        {
            if (_flickTuning == null) return;

            _settings?.Close();
            // 振動できない機種では、つまみの横にその断りを出す。
            _flickTuning.CanVibrate = _feedback == null || _feedback.CanVibrate;
            _flickTuning.Open(_progress.Flick, _style, GameData.Postures);
            ShowBackdrop(_flickTuning);
        }

        private void OnCloseFlickTuning()
        {
            _flickTuning?.Close();
            OpenSettings();
        }

        /// <summary>
        /// つまみを動かしたときに、その場で一度だけ合図を出す。
        /// 耳と指で確かめられないと、数字だけでは強さも長さも決められない。
        /// </summary>
        private void OnVibrationTried()
        {
            if (_feedback != null) _feedback.TestSignal();
        }

        /// <summary>感度を変えたらすぐ保存する。実機で詰めている最中に消えないように。</summary>
        private void OnFlickSettingsChanged(FlickSettings settings)
        {
            _progress.SetFlick(settings);
            _store.Save(_progress);

            // 振動の長さと強さも、この設定に入っている。
            if (_feedback != null) _feedback.Apply(_progress.Flick);
        }

        /// <summary>説明書をプレハブから差し込む。</summary>
        private void BuildManualPanel()
        {
            var prefab = _manualPrefab != null
                ? _manualPrefab
                : Resources.Load<ManualPanel>("UI/ManualPanel");
            if (prefab == null)
            {
                Debug.LogWarning("ManualPanel のプレハブが見つかりません。"
                                 + "メニュー Pentomino ▸ UI プレハブを生成 を実行してください。");
                return;
            }

            _manual = Instantiate(prefab, _root);
            _manual.name = "ManualPanel";
            FitPanel((RectTransform)_manual.transform);
            _manual.CloseRequested += OnCloseManual;
            _manual.Close();
        }

        /// <summary>
        /// 設定画面をプレハブから差し込む。
        /// 見た目は Assets/Resources/UI/SettingsPanel.prefab を編集して調整する。
        /// </summary>
        private void BuildSettingsPanel()
        {
            var prefab = _settingsPrefab != null
                ? _settingsPrefab
                : Resources.Load<SettingsPanel>("UI/SettingsPanel");
            if (prefab == null)
            {
                Debug.LogError("SettingsPanel のプレハブが見つかりません。"
                               + "メニュー Pentomino ▸ UI プレハブを生成 を実行してください。");
                return;
            }

            _settings = Instantiate(prefab, _root);
            _settings.name = "SettingsPanel";
            FitPanel((RectTransform)_settings.transform);
            _settings.DifficultyChosen += OnDifficultyChosen;
            _settings.RecordCleared += OnRecordCleared;
            _settings.LanguageChosen += OnLanguageChosen;
            _settings.ManualRequested += OnOpenManual;
            _settings.SoundChanged += OnSoundChanged;
            _settings.CloseRequested += OnCloseSettings;
            _settings.FlickTuningRequested += OnOpenFlickTuning;
            _settings.Close();
        }

        /// <summary>
        /// ナビゲーションの帯をプレハブから差し込む。
        /// 中身の見た目は Assets/Resources/UI/HeaderBar.prefab を編集して調整する。
        /// </summary>
        private void BuildHeader(float contentWidth)
        {
            var prefab = _headerPrefab != null ? _headerPrefab : Resources.Load<HeaderBar>("UI/HeaderBar");
            if (prefab == null)
            {
                Debug.LogError("HeaderBar のプレハブが見つかりません。"
                               + "メニュー Pentomino ▸ UI プレハブを生成 を実行してください。");
                return;
            }

            _header = Instantiate(prefab, _root);
            _header.name = "HeaderBar";
            SetTopLeft(_header.RectTransform, _contentLeft, _topMargin);

            // 帯は基準の内容幅で組んであるので、実際の内容幅ちょうどに合わせる。
            // これで余白の設定を変えても、帯の左右が BOX と必ず揃う。
            _header.FitWidth(contentWidth);

            _header.SetLongPressSeconds(_titleLongPressSeconds);

            // 畳むと帯の高さが変わる。そのたびに BOX と待機場所を置き直す。
            _header.HeightChanged += LayoutBelowHeader;

            // 画面を組み直しても、畳んでいた状態は引き継ぐ。
            _header.SetFolded(_folded, true);

            _header.StepRequested += StepPuzzle;
            _header.SettingsRequested += OnOpenSettings;
            _header.CheckRequested += OnCheck;
            _header.HintRequested += OnHint;
        }

        private void BuildTray(float contentWidth, float trayTop)
        {
            var scrollGo = new GameObject("Tray", typeof(RectTransform), typeof(ScrollRect));
            scrollGo.transform.SetParent(_root, false);
            _trayRect = (RectTransform)scrollGo.transform;

            // 左端から contentWidth ぶんの幅で、BOX の下から画面下端まで縦に伸ばす。
            // 下端は画面に貼り付いているので、上端を上げればそのぶん待機場所が広くなる。
            var scrollRect = _trayRect;
            scrollRect.anchorMin = new Vector2(0f, 0f);
            scrollRect.anchorMax = new Vector2(0f, 1f);
            scrollRect.pivot = new Vector2(0f, 1f);
            scrollRect.offsetMin = new Vector2(_contentLeft, 0f);
            scrollRect.offsetMax = new Vector2(_contentLeft + contentWidth, -trayTop);

            var viewport = UiFactory.CreateRect(scrollGo.transform, "Viewport");
            viewport.anchorMin = Vector2.zero;
            viewport.anchorMax = Vector2.one;
            viewport.offsetMin = Vector2.zero;
            viewport.offsetMax = Vector2.zero;
            viewport.gameObject.AddComponent<RectMask2D>();

            // 何もない所をドラッグしてスクロールできるよう、透明な当たり判定を敷く。
            var background = viewport.gameObject.AddComponent<Image>();
            background.color = new Color(0f, 0f, 0f, 0f);

            _trayContent = UiFactory.CreateRect(viewport, "Content");
            _trayContent.anchorMin = new Vector2(0f, 1f);
            _trayContent.anchorMax = new Vector2(1f, 1f);
            _trayContent.pivot = new Vector2(0f, 1f);
            _trayContent.offsetMin = new Vector2(0f, 0f);
            _trayContent.offsetMax = new Vector2(0f, 0f);

            _trayScroll = scrollGo.GetComponent<ScrollRect>();
            _trayScroll.horizontal = false;      // 横方向はスクロールしない
            _trayScroll.vertical = true;
            _trayScroll.viewport = viewport;
            _trayScroll.content = _trayContent;
            _trayScroll.movementType = ScrollRect.MovementType.Clamped;
            _trayScroll.scrollSensitivity = 40f;
        }

        /// <summary>
        /// おめでとうパネルをプレハブから差し込む。
        /// BOX に重ならないよう下に置き、額縁は元の縦横比を保ったまま収める。
        /// 見た目は Assets/Resources/UI/ResultPanel.prefab を編集して調整する。
        /// </summary>
        private void BuildResultPanel(float contentWidth, float trayTop)
        {
            var prefab = _resultPrefab != null
                ? _resultPrefab
                : Resources.Load<ResultPanel>("UI/ResultPanel");
            if (prefab == null)
            {
                Debug.LogWarning("ResultPanel のプレハブが見つかりません。"
                                 + "メニュー Pentomino ▸ UI プレハブを生成 を実行してください。");
                return;
            }

            _resultPanel = Instantiate(prefab, _root);
            _resultPanel.name = "ResultPanel";

            var rect = _resultPanel.RectTransform;
            rect.anchorMin = rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);

            // BOX の下に残っている高さに、元の縦横比のまま収める。
            var available = Mathf.Max(0f, RootHeight() - trayTop - _boardGap);
            _resultPanel.FitInside(contentWidth, available);

            // 収まった大きさで横方向に中央へ。
            var width = _resultPanel.ScaledSize.x;
            rect.anchoredPosition = new Vector2(_contentLeft + (contentWidth - width) * 0.5f, -trayTop);

            _resultPanel.Hide();
        }

        /// <summary>
        /// 画面の高さに収まるようセルサイズを抑える。
        /// 縦長の画面では何もしないが、横長の画面では小さくなる。
        /// </summary>
        private void FitCellSizeToHeight()
        {
            var available = RootHeight();
            if (available <= 0f) return;

            // 畳む前の高さで決める。いまの高さを使うと、畳んだときに残り高さが増えて
            // セルが大きくなり、BOX ごと膨らんでしまう。
            // 一番背が高い状態で収まれば、畳んでも必ず収まる。
            var forBoard = available - _topMargin - HeaderFullHeight - _boardGap * 2f - _minTrayHeight;
            if (forBoard <= 0f) return;

            var maxCell = forBoard / (Board.Rows + _style.outerLineRatio);
            if (maxCell < _style.cellSize) _style.cellSize = maxCell;
        }

        /// <summary>
        /// 組み立てに使う画面の高さ。幅（ReferenceWidth）と同じ物差しで測る。
        ///
        /// Awake の時点では Canvas Scaler がまだ効いておらず、
        /// _root.rect.height は画面の実ピクセル数のままのことがある。
        /// 幅は基準の 1080 で測っているので、そのまま混ぜると縦だけ物差しが狂い、
        /// BOX が理由もなく小さくなってしまう。
        /// そこで画面の実寸と縮尺から、基準の物差しでの高さを出す。
        /// </summary>
        private float RootHeight() => RootSize().y;

        /// <summary>
        /// 組み立てに使う画面の大きさ。基準（1080x1920）と同じ物差しで測る。
        ///
        /// カンバスの実寸（_root.rect）は使わない。Awake の時点ではまだ確定しておらず、
        /// RectTransform の既定値が返ることがあるため。
        /// 画面の実寸と Canvas Scaler の縮尺から求めるほうが、いつ呼んでも正しい。
        /// エディタでは Game ビューの大きさが Screen に入るので、これで追従できる。
        /// </summary>
        private Vector2 RootSize()
        {
            var reference = ReferenceResolution();

            float width = Screen.width;
            float height = Screen.height;
            if (width <= 0f || height <= 0f) return reference;

            var scale = ScaleFactor(width / reference.x, height / reference.y);
            if (scale <= 0f) return reference;

            return new Vector2(width / scale, height / scale);
        }

        /// <summary>Canvas Scaler が画面をどれだけ拡大縮小するか。設定の意味そのまま。</summary>
        /// <summary>
        /// 画面の上端で、隠れる恐れのある高さ（基準の物差し）。
        ///
        /// iPhone のノッチやダイナミックアイランド、Android のパンチホールで
        /// 上端の帯は見えなくなる。端末が safeArea で教えてくれるならそれに従うが、
        /// 教えてくれない機種もあるので、実測から決めた下限を置く。
        /// </summary>
        private float TopInset()
        {
            var reference = ReferenceResolution();

            float width = Screen.width;
            float height = Screen.height;
            if (width <= 0f || height <= 0f) return 0f;

            var scale = ScaleFactor(width / reference.x, height / reference.y);
            if (scale <= 0f) return 0f;

            var safe = Screen.safeArea;
            var hidden = height - (safe.y + safe.height);
            if (hidden < 0f) hidden = 0f;

            // 教えてくれない機種のための下限。
            var dpi = Screen.dpi > 0f ? Screen.dpi : FallbackDpi;
            var floor = MinTopInsetMm * dpi / 25.4f;

            return Mathf.Max(hidden, floor) / scale;
        }

        /// <summary>上端に必ず空けておく幅（mm）。実機で隠れた量から決めた。</summary>
        private const float MinTopInsetMm = 6f;

        /// <summary>画面の細かさが分からない端末で使う値。FlickTracker と揃えてある。</summary>
        private const float FallbackDpi = 400f;

        /// <summary>
        /// 子画面を、遊び面と同じ幅の柱に、比率のまま収める。
        ///
        /// 中身は基準の幅（1080）を前提に並べてある。柱がそれより狭い機種
        /// （タブレットや横向き）では、位置だけを合わせても中身がはみ出す。
        /// そこで中身の座標はいじらず、パネルごと拡大縮小する。
        /// こうすれば、どの機種でも見た目の比率が変わらない。
        ///
        /// 縦は、上端の隠れる帯を避けたところから画面の下端まで。
        /// 途中で切れると、その先に遊び面が透けて見えてしまう。
        /// </summary>
        private void FitPanel(RectTransform rect)
        {
            if (rect == null) return;

            var screen = RootSize();
            var reference = ReferenceWidth();
            if (reference <= 0f) return;

            var display = Mathf.Min(screen.x, screen.y / DisplayAspect);
            var scale = display / reference;
            if (scale <= 0f) return;

            var top = TopInset();

            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.localScale = new Vector3(scale, scale, 1f);

            // 縮小したぶん、内側の物差しでは画面が広く見える。
            rect.sizeDelta = new Vector2(reference, Mathf.Max(0f, screen.y - top) / scale);
            rect.anchoredPosition = new Vector2(0f, -top);
        }

        /// <summary>
        /// 子画面の後ろに敷く覆い。開いている間、遊び面を隠す。
        ///
        /// 子画面は柱の幅しかないので、そのままでは左右と下に遊び面が覗く。
        /// 何が触れるのか分かりにくいので、覆って消す。
        /// 色は開いた子画面の地の色に合わせ、一枚の面に見えるようにする。
        /// </summary>
        private void ShowBackdrop(Component panel)
        {
            if (_backdrop == null || panel == null) return;

            var face = panel.GetComponent<Image>();
            if (face != null) _backdrop.color = new Color(face.color.r, face.color.g, face.color.b, 1f);

            // いったん最後へ送ってから差し込む。前にいるまま番号を指すと、
            // 抜けたぶん順番がずれて、子画面の手前に出てしまう。
            _backdrop.transform.SetAsLastSibling();
            _backdrop.transform.SetSiblingIndex(panel.transform.GetSiblingIndex());
            _backdrop.gameObject.SetActive(true);
        }

        private void HideBackdrop()
        {
            if (_backdrop != null) _backdrop.gameObject.SetActive(false);
        }

        private float ScaleFactor(float widthRatio, float heightRatio)
        {
            var scaler = _canvas != null ? _canvas.GetComponent<CanvasScaler>() : null;
            if (scaler == null) return Mathf.Min(widthRatio, heightRatio);

            switch (scaler.screenMatchMode)
            {
                case CanvasScaler.ScreenMatchMode.Expand:
                    return Mathf.Min(widthRatio, heightRatio);

                case CanvasScaler.ScreenMatchMode.Shrink:
                    return Mathf.Max(widthRatio, heightRatio);

                default:
                    // 幅と高さの間を対数で按分する（Unity と同じ式）。
                    var match = Mathf.Clamp01(scaler.matchWidthOrHeight);
                    return Mathf.Pow(widthRatio, 1f - match) * Mathf.Pow(heightRatio, match);
            }
        }

        /// <summary>Canvas Scaler の基準の画面サイズ。</summary>
        private Vector2 ReferenceResolution()
        {
            var scaler = _canvas != null ? _canvas.GetComponent<CanvasScaler>() : null;
            if (scaler == null) return new Vector2(1080f, 1920f);

            var reference = scaler.referenceResolution;
            if (reference.x <= 0f) reference.x = 1080f;
            if (reference.y <= 0f) reference.y = 1920f;
            return reference;
        }

        private float ReferenceWidth() => ReferenceResolution().x;

        private static void SetTopLeft(RectTransform rect, float x, float y)
        {
            rect.anchorMin = rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = new Vector2(x, -y);
        }

        // ------------------------------------------------------------------ 問題の読み込み

        public void LoadPuzzle(int number)
        {
            SaveCurrent();

            var library = GameData.Puzzles;
            number = Mathf.Clamp(number, library.FirstNumber, library.LastNumber);

            _session = new PuzzleSession(library[number], GameData.Postures, _progress.Difficulty);
            ProgressStore.Restore(_progress, _session);

            _floating = null;
            _dragging = null;
            RefreshPieces();
            UpdateHeader();

            // 切り替えた直後はタイトルと難易度を見せる。畳むのは少し経ってから。
            SetHeaderFolded(false);
            _foldAt = Time.unscaledTime + FoldDelay;

            SaveCurrent();
        }

        private void StepPuzzle(int delta)
        {
            if (_session == null) return;
            LoadPuzzle(GameData.Puzzles.Offset(_session.Puzzle.Number, delta));
        }

        private void SaveCurrent()
        {
            if (_session == null) return;
            ProgressStore.Capture(_progress, _session);
            _store.Save(_progress);
        }

        // ------------------------------------------------------------------ 表示の更新

        /// <summary>セッションを外から操作したあと、表示を作り直す。</summary>
        public void Refresh() => RefreshPieces();

        /// <summary>盤上のピースと待機場所を、いまのセッションの状態から作り直す。</summary>
        private void RefreshPieces()
        {
            foreach (var view in _fixedViews) if (view != null) Destroy(view.gameObject);
            _fixedViews.Clear();

            foreach (var widget in _widgets.Values) if (widget != null) Destroy(widget.gameObject);
            _widgets.Clear();

            _floating = null;
            _dragging = null;

            // 出題時から置かれている固定ピース。触れない。
            foreach (var placement in _session.Puzzle.Prearranged)
            {
                var view = PolyominoView.Create(_board.PiecesLayer, "Fixed_" + placement.Piece);
                view.RenderPiece(placement.Posture, _style);
                view.Graphic.raycastTarget = false;
                PlaceViewOnBoard(view.Graphic, placement.Row, placement.Col);
                _fixedViews.Add(view);
            }

            // 動かせるピース。盤上にあるものと待機場所にあるものがある。
            foreach (var placement in _session.Puzzle.Hidden)
            {
                var piece = placement.Piece;
                var onBoard = _session.Board.TryGetPlacement(piece, out var current);
                var parent = onBoard ? _board.PiecesLayer : (Transform)_trayContent;

                var widget = PieceWidget.Create(parent, piece, this);
                var posture = onBoard ? current.Posture : TrayPosture(piece);
                widget.SetPosture(posture, _style);
                widget.IsOnBoard = onBoard;
                _widgets[piece] = widget;

                if (onBoard) PlaceViewOnBoard(widget.View.Graphic, current.Row, current.Col);
            }

            LayoutTray();
            UpdateHeader();
            UpdateResultPanel();
        }

        private Posture TrayPosture(char piece) =>
            _session.TryGetTrayPiece(piece, out var trayPiece)
                ? trayPiece.Posture
                : GameData.Postures.DefaultPosture(piece);

        private void PlaceViewOnBoard(PolyominoGraphic graphic, int row, int col)
        {
            graphic.transform.SetParent(_board.PiecesLayer, false);

            // 原点セル (posture の (0,0)) の左上角を、盤の (row, col) の左上角に合わせる。
            var target = _board.CellCornerWorld(row, col);
            var current = graphic.transform.TransformPoint(graphic.CellCorner(0, 0));
            graphic.transform.position += target - current;
        }

        /// <summary>
        /// 待機場所の並べ直し。BOX の幅に収まる範囲で左から右、入らなくなったら次の行へ。
        /// 縦に溢れた分はスクロールで見る。
        /// </summary>
        private void LayoutTray()
        {
            var width = _trayContent.rect.width > 0f ? _trayContent.rect.width : _contentWidth;

            // 並べる相手を集める。運んでいる最中の物は動かさない。
            var shown = new List<PieceWidget>();
            foreach (var trayPiece in _session.Tray)
            {
                if (!_widgets.TryGetValue(trayPiece.Piece, out var widget)) continue;
                if (widget == null || widget.IsOnBoard) continue;
                if (widget == _dragging || widget == _floating) continue;

                widget.RectTransform.SetParent(_trayContent, false);
                widget.transform.localScale = Vector3.one;
                shown.Add(widget);
            }

            var widths = new float[shown.Count];
            var heights = new float[shown.Count];
            for (var i = 0; i < shown.Count; i++)
            {
                var size = shown[i].View.Graphic.PreferredSize;
                widths[i] = size.x;
                heights[i] = size.y;
            }

            // 段の高さを揃えず、置けるところまで上へ詰める。
            var packed = TrayPacking.Arrange(widths, heights, width, _trayGap);

            for (var i = 0; i < shown.Count; i++)
            {
                var slot = packed.Slots[i];
                shown[i].RectTransform.anchoredPosition = new Vector2(slot.X, -slot.Y);
            }

            _trayContent.sizeDelta = new Vector2(_trayContent.sizeDelta.x, packed.Height);
        }

        // ------------------------------------------------------------------ 帯を畳む

        /// <summary>いまの帯の高さ。畳むと縮む。BOX の縦位置に使う。</summary>
        private float HeaderHeight =>
            _header != null ? _header.Height : _navRowHeight * 3f + _navRowGap * 2f;

        /// <summary>畳む前の帯の高さ。BOX の大きさに使う。畳んでも変わらない。</summary>
        private float HeaderFullHeight =>
            _header != null ? _header.FullHeight : _navRowHeight * 3f + _navRowGap * 2f;

        /// <summary>
        /// 畳む頃合いを見る（開発仕様「操作性の問題(1)」）。
        ///
        /// 問題を切り替えた直後はタイトルと難易度を見せ、少し経ってから畳む。
        /// 完成した問題では畳まない。畳むのはその一度きりで、
        /// 同じ番号にいる間は動かない。
        /// </summary>
        /// <summary>
        /// タイトルを畳むまでの待ち時間。
        ///
        /// 送りの繰り返しが始まるより先に畳んではいけない。先に畳むと、
        /// 押し続けている最中に一度消えて、次の問題でまた出てくる、
        /// というちらつきになる。プレハブ側で待ち時間を変えても崩れないよう、
        /// 実際の値から下限を出す。
        /// </summary>
        private float FoldDelay
        {
            get
            {
                var floor = (_header != null ? _header.StepHoldDelay
                                             : StepRepeatRule.DefaultHoldDelay) + FoldDelayMargin;
                return Mathf.Max(_headerFoldSeconds, floor);
            }
        }

        /// <summary>繰り返しが始まってから、畳むまでに置く余裕（秒）。</summary>
        private const float FoldDelayMargin = 0.5f;

        private void UpdateHeaderFold()
        {
            if (_header == null || !_header.CanFold) return;

            if (_session == null || _session.IsSolved)
            {
                _foldAt = -1f;
                SetHeaderFolded(false);
                return;
            }

            // 送りボタンを押している間は畳まない。
            // 待ち時間の大小だけでは防げない。前の問題で仕掛けたタイマーが、
            // 押し始めた直後に切れることがあるため。
            if (_header.IsStepping)
            {
                _foldAt = Time.unscaledTime + FoldDelay;
                return;
            }

            if (_foldAt < 0f || Time.unscaledTime < _foldAt) return;

            _foldAt = -1f;
            SetHeaderFolded(true);
        }

        private void SetHeaderFolded(bool folded)
        {
            // 覚えておくのは、画面を組み直したときに引き継ぐため。
            // 帯にはそのつど伝える。外から直に畳まれていても、ここで正せるように。
            _folded = folded;
            if (_header != null) _header.SetFolded(folded);
        }

        /// <summary>
        /// 帯の下にある物を置き直す。帯の高さが変わるたびに呼ばれる。
        ///
        /// BOX の大きさは変えない。上へずらすだけで、空いた分はそのまま
        /// 待機場所の広さになる。置いてあるピースは BOX と待機場所の子なので、
        /// 一緒に動く。組み直しは要らない。
        /// </summary>
        private void LayoutBelowHeader()
        {
            if (_board == null) return;

            var boardTop = _topMargin + HeaderHeight + _boardGap;
            SetTopLeft(_board.RectTransform, _contentLeft, boardTop);

            var trayTop = boardTop + _board.Size.y + _boardGap;

            if (_trayRect != null)
                _trayRect.offsetMax = new Vector2(_contentLeft + _contentWidth, -trayTop);

            if (_resultPanel != null)
            {
                var rect = _resultPanel.RectTransform;
                rect.anchoredPosition = new Vector2(rect.anchoredPosition.x, -trayTop);
            }
        }

        // ------------------------------------------------------------------ 表示の更新

        private void UpdateHeader()
        {
            ApplyBackground();

            if (_session == null || _header == null) return;

            _header.Refresh(_session);
        }

        /// <summary>
        /// 画面全体の地を、いま遊んでいる級の帯の色にする。
        /// 帯の絵（タイトル・級）は同じ色で描いてあるので、継ぎ目は見えない。
        /// </summary>
        private void ApplyBackground()
        {
            if (_background == null) return;

            var difficulty = _session != null ? _session.Difficulty
                : _progress != null ? _progress.Difficulty : Difficulty.Classic;
            _background.color = GradeTheme.Background(difficulty);
        }

        private void UpdateResultPanel()
        {
            if (_resultPanel == null) return;

            if (_session == null || !_session.IsSolved)
            {
                _resultPanel.Hide();
                _resultWasOpen = false;
                return;
            }

            _resultPanel.Show(_session);

            // 出た瞬間だけ鳴らす。描き直しのたびに鳴らすと重なってしまう。
            if (!_resultWasOpen && _feedback != null) _feedback.Solved();
            _resultWasOpen = true;
        }

        private static string GradeName(Difficulty difficulty) => Strings.GradeName(difficulty);

        // ------------------------------------------------------------------ ボタン

        private void OnCheck()
        {
            if (_session == null || _session.IsSolved) return;
            _session.Check();
            RefreshPieces();
            SaveCurrent();
        }

        private void OnHint()
        {
            if (_session == null || _session.IsSolved) return;
            _session.Hint();
            RefreshPieces();
            SaveCurrent();
        }

        private void OnStop()
        {
            SaveCurrent();
            StopRequested?.Invoke();
        }

        /// <summary>
        /// 説明書を開く。ボタンは設定画面の中にある。
        ///
        /// 以前はメイン画面の帯にあったが、遊んでいる最中に押すものではないので、
        /// 設定と同じ場所にまとめた。帯には設定の歯車だけが残る。
        /// </summary>
        public void OnOpenManual()
        {
            SaveCurrent();
            if (_manual == null) return;

            _settings?.Close();
            _manual.Open();
            ShowBackdrop(_manual);
        }

        public void OnCloseManual()
        {
            _manual?.Close();
            OpenSettings();
        }

        /// <summary>設定ボタン、またはパズル名の長押しで設定モードに入る。</summary>
        public void OnOpenSettings()
        {
            SaveCurrent();
            OpenSettings();
        }

        private void OpenSettings()
        {
            if (_settings == null) return;

            _settings.Open(_progress.Difficulty, _progress.Language, _progress.sound);
            ShowBackdrop(_settings);
        }

        public void OnCloseSettings()
        {
            _settings.Close();
            HideBackdrop();
            UpdateHeader();
        }

        /// <summary>音の入り切り。切ると、はまる音も完成の音も鳴らない。</summary>
        private void OnSoundChanged(bool on)
        {
            _progress.sound = on;
            if (_feedback != null) _feedback.Sound = on;
            _store.Save(_progress);
        }

        /// <summary>
        /// 級を切り替える。進捗は級ごとに別々に残っているので、何も消えない。
        /// その級で最後に開いていた問題に戻る。
        /// </summary>
        private void OnDifficultyChosen(Difficulty difficulty)
        {
            SaveCurrent();

            _progress.Difficulty = difficulty;
            _store.Save(_progress);

            _session = null;
            LoadPuzzle(Mathf.Max(1, _progress.LastPuzzle));
        }

        /// <summary>
        /// 級ひとつぶんの記録を消す。設定画面で問いかけに答えたあとに来る。
        /// いま遊んでいる級を消したときだけ、盤も出題し直す。
        /// </summary>
        private void OnRecordCleared(Difficulty difficulty)
        {
            _progress.ClearProgress(difficulty);

            if (difficulty == _progress.Difficulty)
            {
                _session = null;
                _store.Save(_progress);
                LoadPuzzle(1);
                return;
            }

            _store.Save(_progress);
        }

        /// <summary>言語はいつでも変更でき、進捗には影響しない。</summary>
        private void OnLanguageChosen(Language language)
        {
            _progress.Language = language;
            Strings.Current = language;
            _store.Save(_progress);

            // 書体は言語ごとに違う。日本語の書体では韓国語の字が出ない。
            ApplyFonts();
            UpdateHeader();
        }

        /// <summary>画面じゅうの文字に、いまの言語の書体を当てる。</summary>
        private void ApplyFonts()
        {
            _fonts.Apply(gameObject, _progress.Language);

            // 言語ボタンだけは、それぞれのボタンの言語の書体で描く。
            _settings?.ApplyLanguageFonts(_fonts);
        }

        // ------------------------------------------------------------------ 指の操作

        public void OnPieceGrab(PieceWidget widget, PointerEventData eventData)
        {
            if (_session == null || _session.IsSolved) return;

            // 別のピースを掴んだら、宙に浮いていたピースは待機位置に戻る。
            if (_floating != null && _floating != widget) ReturnFloatingToTray();
            if (_floating == widget) _floating = null;

            if (widget.IsOnBoard)
            {
                _session.TryPickUp(widget.Piece);
                widget.IsOnBoard = false;
            }

            _dragging = widget;
            widget.RectTransform.SetParent(_dragLayer, true);
            widget.transform.SetAsLastSibling();

            _grabOffset = widget.transform.position - PointerWorld(eventData);

            // 掴んでいる間は外周線を赤くする。触れてから離すまでが一区切りなので、
            // 何を持っているかが、指に隠れていても線の色で判る。
            widget.SetOutline(true, false, _style);
        }

        /// <summary>
        /// 宙に浮いたままのピースを待機位置に戻す。
        /// 全体を作り直すと掴んだ直後のピースまで消えてしまうので、この 1 個だけを動かす。
        /// </summary>
        private void ReturnFloatingToTray()
        {
            if (_floating == null) return;

            _floating.IsOnBoard = false;
            _floating.SetOutline(false, false, _style);
            _floating.RectTransform.SetParent(_trayContent, false);
            _floating = null;
            LayoutTray();
        }

        /// <summary>
        /// 指を追いかけつつ、置ける場所に近づいたら吸い込む（開発仕様「ピースのはめ込み」）。
        /// 吸い付いている間は外周線が青くなり、指が離れると指の位置に戻る。
        /// </summary>
        public void OnPieceDrag(PieceWidget widget, PointerEventData eventData)
        {
            if (_dragging != widget) return;

            // まず指の位置に置いてから、そことはめ込み位置との距離を測る。
            var pointer = PointerWorld(eventData);
            widget.transform.position = pointer + _grabOffset;

            // 指から外れていたら掴み直す。
            KeepUnderPointer(widget, pointer);

            // 帯には持ち込ませない。指が上へ流れても、ピースは BOX の上端で止まる。
            KeepInPlayArea(widget);

            var wasSnapped = _snapped;
            var wasRow = _snapRow;
            var wasCol = _snapCol;
            _snapped = false;

            if (_board.TryWorldToCell(widget.OriginCellCenterWorld, out var row, out var col) &&
                _session.CanPlace(widget.Piece, widget.Posture, row, col))
            {
                var free = widget.OriginCellCornerWorld;
                var target = _board.CellCornerWorld(row, col);
                var distance = CanvasDistance(free, target);

                // 直前と同じ場所に吸い付いていたかどうかで、しきい値を切り替える。
                var stillSame = wasSnapped && row == _snapRow && col == _snapCol;
                if (SnapRule.ShouldSnap(stillSame, distance, _snapAttractDistance, _snapReleaseDistance))
                {
                    _snapped = true;
                    _snapRow = row;
                    _snapCol = col;
                    widget.MoveOriginCornerTo(target);
                }
            }

            widget.SetOutline(true, _snapped, _style);

            // 吸い付いた瞬間だけ知らせる。吸い付いている間ずっと鳴らすと、
            // 境目で指が揺れるたびに鳴り続ける。別の枠へ移ったときは鳴らす。
            if (_snapped && (!wasSnapped || wasRow != _snapRow || wasCol != _snapCol))
                if (_feedback != null) _feedback.Snapped();
        }

        /// <summary>ワールド座標の距離を、画面の基準ピクセルに直す。</summary>
        private float CanvasDistance(Vector3 a, Vector3 b)
        {
            var scale = _root != null ? _root.lossyScale.x : 1f;
            if (Mathf.Approximately(scale, 0f)) scale = 1f;
            return Vector3.Distance(a, b) / scale;
        }

        public void OnPieceRelease(PieceWidget widget, PointerEventData eventData)
        {
            if (_dragging != widget) return;
            _dragging = null;

            // 離したので黒に戻す。
            widget.SetOutline(false, false, _style);

            if (_snapped && _session.TryPlace(widget.Piece, _snapRow, _snapCol))
            {
                widget.IsOnBoard = true;
                _snapped = false;
                _refreshRequested = true;
                SaveCurrent();
                return;
            }

            // 置けない場所で離したら、そこに留まる。別のピースを掴むと待機位置に戻る。
            _floating = widget;
            _snapped = false;
        }

        public void OnPieceTap(PieceWidget widget)
        {
            if (_session == null || _session.IsSolved) return;
            if (widget.IsOnBoard) return;      // 盤上のピースは掴み上げてから回す

            var posture = _session.RotateCcw(widget.Piece);
            if (posture == null) return;

            widget.SetPosture(posture, _style);
            if (widget != _floating) LayoutTray();
        }

        /// <summary>
        /// 掴んだまま素早く滑らせた。横向きなら左右反転、縦向きなら上下反転。
        ///
        /// 平面のペントミノではどちらも 180 度の反転だが、
        /// 立体版では左右が 90 度回転、上下が起こす向きの 90 度になる。
        /// 向きの判定は FlickRule が済ませてあるので、ここは当てはめるだけ。
        /// </summary>
        public void OnPieceFlick(PieceWidget widget, FlickAxis axis)
        {
            if (_session == null || _session.IsSolved) return;
            if (widget.IsOnBoard) return;

            var posture = axis == FlickAxis.Horizontal
                ? _session.FlipHorizontally(widget.Piece)
                : _session.FlipVertically(widget.Piece);
            if (posture == null) return;

            widget.SetPosture(posture, _style);
            if (widget != _floating) LayoutTray();
        }

        /// <summary>フリックの判定に使うしきい値。設定画面から変えられる。</summary>
        public FlickSettings FlickSettings => _progress != null ? _progress.Flick : null;

        /// <summary>
        /// 掴んでいる間、指とピースが離れすぎていないか見張る
        /// （開発仕様「フリックの操作性」）。
        ///
        /// 反転したあとに、ピースが指から 2〜3cm 離れたまま付いてくることがある。
        /// 原因が何であれ、指の乗っていない物を運んでいるのはおかしいので、
        /// 外れていたら掴み直したことにして、指の下へ戻す。
        ///
        /// 判定は GrabRule。指がピースの上にある間は何も起きないので、
        /// 普段の掴み心地は変わらない。吸い付きより先に働くので、邪魔もしない。
        /// </summary>
        private void KeepUnderPointer(PieceWidget widget, Vector3 pointer)
        {
            var rect = widget.RectTransform;
            var area = rect.rect;
            var local = rect.InverseTransformPoint(pointer);

            GrabRule.Correction(local.x, local.y,
                area.xMin, area.xMax, area.yMin, area.yMax,
                _style.cellSize * GrabRule.DefaultSlackCells,
                out var dx, out var dy);

            if (dx == 0f && dy == 0f) return;

            // はみ出した分だけピースを指のほうへ動かし、掴み位置も取り直す。
            widget.transform.position += rect.TransformVector(new Vector3(dx, dy, 0f));
            _grabOffset = widget.transform.position - pointer;
        }

        /// <summary>
        /// 運んでいるピースを遊び面（BOX とその下）から出さない。
        ///
        /// ピースを置く場所のすぐ上にボタンがある。指が上へ流れてピースが帯に
        /// 掛かると、離した拍子に Hint を押してしまう。ピースが帯の上へ
        /// 行けなければ、指も帯には届かない。左右も柱の外へは出さない。
        ///
        /// 判定は PlayAreaRule。掴み位置（_grabOffset）は変えないので、
        /// 指が遊び面へ戻ってくれば、ピースは元の掴み方のまま付いてくる。
        /// </summary>
        private void KeepInPlayArea(PieceWidget widget)
        {
            if (_root == null || _board == null) return;

            // ピースの四隅を画面（_root）の物差しに直す。
            var rect = widget.RectTransform;
            var corners = new Vector3[4];
            rect.GetWorldCorners(corners);

            var min = new Vector2(float.MaxValue, float.MaxValue);
            var max = new Vector2(float.MinValue, float.MinValue);
            foreach (var corner in corners)
            {
                var local = _root.InverseTransformPoint(corner);
                min = Vector2.Min(min, local);
                max = Vector2.Max(max, local);
            }

            // 遊び面。上端は BOX の上端（畳むと上がる）、下端は画面の下端。
            var area = _root.rect;
            var boardTop = area.yMax + _board.RectTransform.anchoredPosition.y;
            var left = area.xMin + _contentLeft;
            var right = left + _contentWidth;

            PlayAreaRule.Correction(min.x, max.x, min.y, max.y,
                left, right, area.yMin, boardTop, out var dx, out var dy);

            if (dx == 0f && dy == 0f) return;

            widget.transform.position += _root.TransformVector(new Vector3(dx, dy, 0f));
        }

        private Vector3 PointerWorld(PointerEventData eventData)
        {
            var camera = _canvas != null && _canvas.renderMode != RenderMode.ScreenSpaceOverlay
                ? _canvas.worldCamera
                : null;

            RectTransformUtility.ScreenPointToWorldPointInRectangle(
                _dragLayer, eventData.position, camera, out var world);
            return world;
        }
    }
}
