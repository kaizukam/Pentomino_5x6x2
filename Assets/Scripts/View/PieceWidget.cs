using Pentomino.Core;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Pentomino.View
{
    /// <summary>PieceWidget から受け取る操作。</summary>
    public interface IPieceInteraction
    {
        void OnPieceGrab(PieceWidget widget, PointerEventData eventData);
        void OnPieceDrag(PieceWidget widget, PointerEventData eventData);
        void OnPieceRelease(PieceWidget widget, PointerEventData eventData);

        /// <summary>軽いタップ。画面の面内で反時計回りに 90 度回す。</summary>
        void OnPieceTap(PieceWidget widget);

        /// <summary>勢いをつけたまま離した。その向きへ 90 度（収まらなければ 180 度）転がす。</summary>
        void OnPieceFlick(PieceWidget widget, FlickAxis axis);

        /// <summary>フリックの判定に使うしきい値。設定画面で変えられる。</summary>
        FlickSettings FlickSettings { get; }
    }

    /// <summary>
    /// 指で操作できるピース 1 個。
    /// 触れば指を追いかけ、離せばその場に留まる。
    ///
    /// 軽いタップで反時計回りに 90 度。掴んだまま素早く滑らせると、その向きへ 90 度転がる。
    /// タップ回数で 2 種類の操作を分けると、2 回目を待つあいだ回転が遅れてしまう。
    /// 別の操作に分けたことで、タップは押した瞬間に効く。
    ///
    /// 判定だけを担当し、盤面の状態は GameScreen 側が持つ。
    /// </summary>
    [AddComponentMenu("Pentomino/Piece Widget")]
    [RequireComponent(typeof(RectTransform))]
    public sealed class PieceWidget : MonoBehaviour,
        IPointerDownHandler, IPointerUpHandler, IBeginDragHandler, IDragHandler, IEndDragHandler
    {
        private readonly FlickTracker _flick = new FlickTracker();

        private IPieceInteraction _handler;
        private bool _dragStarted;

        /// <summary>
        /// いま運んでいる指の番号。運んでいなければ NoPointer。
        ///
        /// 手のひらや親指の付け根が触れると、2 本目の指として届く。
        /// 掴み位置がその指で計算し直され、ピースが 2 点の距離だけずれたまま
        /// 付いてくることがあった。最初の指だけを見て、ほかは無視する。
        ///
        /// 番号そのものに意味は無い（機種ごとに値が違う）。覚えて比べるだけ。
        /// </summary>
        private int _pointerId = NoPointer;

        private const int NoPointer = int.MinValue;

        /// <summary>指の動きの追跡。感度調整の画面が実測値を読む。</summary>
        public FlickTracker Flick => _flick;

        public char Piece { get; private set; }

        public Posture Posture { get; private set; }

        public PolyominoView View { get; private set; }

        public RectTransform RectTransform => (RectTransform)transform;

        /// <summary>盤上に確定しているか。待機場所・宙に浮いた状態なら false。</summary>
        public bool IsOnBoard { get; set; }

        public void Initialize(char piece, IPieceInteraction handler)
        {
            Piece = piece;
            _handler = handler;
            View = GetComponent<PolyominoView>();
            if (View == null) View = gameObject.AddComponent<PolyominoView>();
        }

        /// <summary>姿勢を描き直す。外接矩形が変わるので、待機場所の並べ直しが必要になる。</summary>
        public void SetPosture(Posture posture, PentominoStyle style)
        {
            Posture = posture;
            View.RenderPiece(posture, style);
        }

        /// <summary>
        /// 外周線の色で、いまピースがどういう状態かを示す。
        ///
        ///   黒   触れていない
        ///   赤   掴んでいる（触れてから離すまで）
        ///   青   はめ込み位置に吸い付いている
        ///
        /// 掴んだことが線の色で判るので、指が絵に隠れていても
        /// どのピースを持っているかが見える。
        /// </summary>
        public void SetOutline(bool held, bool snapped, PentominoStyle style)
        {
            IsSnapped = snapped;
            _held = held;

            // 点滅の最中は色を点滅に任せる。終われば最後の状態の色に戻る。
            if (_blink != null) return;

            if (style == null)
            {
                View.Graphic.OuterLineColor = null;
                return;
            }

            if (snapped) View.Graphic.OuterLineColor = style.snapLineColor;
            else if (held) View.Graphic.OuterLineColor = style.heldLineColor;
            else View.Graphic.OuterLineColor = null;
        }

        /// <summary>いまはめ込み位置に吸い付いているか。</summary>
        public bool IsSnapped { get; private set; }

        /// <summary>警告の点滅の回数。</summary>
        public const int WarningBlinks = 2;

        /// <summary>点滅 1 回の、赤い時間と消えている時間（秒）。</summary>
        public const float WarningBlinkSeconds = 0.12f;

        private Coroutine _blink;

        /// <summary>
        /// 外周を <see cref="WarningBlinks"/> 回赤く点滅させる。
        /// フリックが 90 度ではなく 180 度になったとき（または回せなかったとき）の警告。
        /// 点滅が終われば、そのときの状態（吸い付き・掴み）の色に戻る。
        /// </summary>
        public void BlinkWarning(PentominoStyle style)
        {
            if (style == null || !isActiveAndEnabled) return;
            if (_blink != null) StopCoroutine(_blink);
            _blink = StartCoroutine(Blink(style));
        }

        private System.Collections.IEnumerator Blink(PentominoStyle style)
        {
            var wait = new WaitForSecondsRealtime(WarningBlinkSeconds);
            for (var i = 0; i < WarningBlinks; i++)
            {
                View.Graphic.OuterLineColor = style.heldLineColor;
                yield return wait;
                View.Graphic.OuterLineColor = style.lineColor;
                yield return wait;
            }

            _blink = null;
            SetOutline(_held, IsSnapped, style);
        }

        /// <summary>SetOutline で最後に渡した「掴んでいる」。点滅のあとに戻す色に使う。</summary>
        private bool _held;

        /// <summary>姿勢の原点セル。</summary>
        private static readonly Cell Origin = new Cell(0, 0, 0);

        /// <summary>原点セル (posture の (0,0,0)) の中心のワールド座標。</summary>
        public Vector3 OriginCellCenterWorld =>
            View.Graphic.transform.TransformPoint(View.Graphic.CellCenter(Origin));

        /// <summary>原点セルの左上角のワールド座標。はめ込み位置との距離を測るのに使う。</summary>
        public Vector3 OriginCellCornerWorld =>
            View.Graphic.transform.TransformPoint(View.Graphic.CellCorner(Origin));

        /// <summary>原点セルの左上角がワールド座標の target に来るよう平行移動する。</summary>
        public void MoveOriginCornerTo(Vector3 target)
        {
            var current = View.Graphic.transform.TransformPoint(View.Graphic.CellCorner(Origin));
            transform.position += target - current;
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            _dragStarted = false;
        }

        public void OnBeginDrag(PointerEventData eventData)
        {
            // すでに別の指で運んでいる。2 本目は見ない。
            if (_pointerId != NoPointer) return;

            _pointerId = eventData.pointerId;
            _dragStarted = true;

            _flick.Begin(eventData.position, _handler?.FlickSettings);
            _handler?.OnPieceGrab(this, eventData);
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (eventData.pointerId != _pointerId) return;

            // 運ぶだけ。反転は離すまで起きない。
            _handler?.OnPieceDrag(this, eventData);

            _flick.Feed(eventData.position);
        }

        public void OnEndDrag(PointerEventData eventData)
        {
            if (eventData.pointerId != _pointerId) return;
            _pointerId = NoPointer;

            // 離した勢いで反転するか、ここで一度だけ決める。
            // 置きたいときは位置を決めて止めてから離すので、まず反転しない。
            var axis = _flick.Release(_handler?.FlickSettings);
            if (axis != FlickAxis.None) _handler?.OnPieceFlick(this, axis);

            _handler?.OnPieceRelease(this, eventData);
        }

        /// <summary>
        /// 指を離した。動かしていなければタップとして扱い、その場で回す。
        /// ダブルタップを待たなくなったので、続けて叩けば続けて回る。
        /// </summary>
        public void OnPointerUp(PointerEventData eventData)
        {
            if (_dragStarted) return;
            _handler?.OnPieceTap(this);
        }

        /// <summary>親の下に新しい PieceWidget を作る。</summary>
        public static PieceWidget Create(Transform parent, char piece, IPieceInteraction handler)
        {
            var go = new GameObject("Piece_" + piece, typeof(RectTransform), typeof(CanvasRenderer),
                typeof(PolyominoGraphic), typeof(PolyominoView), typeof(PieceWidget));
            go.transform.SetParent(parent, false);

            var rect = (RectTransform)go.transform;
            rect.anchorMin = rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);

            var widget = go.GetComponent<PieceWidget>();
            widget.Initialize(piece, handler);
            return widget;
        }
    }
}
