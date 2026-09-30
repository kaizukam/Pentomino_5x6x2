using System;
using Pentomino.Core;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Pentomino.View
{
    /// <summary>
    /// 感度調整の画面に置く練習用のピース 1 個。
    ///
    /// 本番のピースと同じ判定を通しつつ、盤も待機場所も持たないので、
    /// 何度滑らせても進捗には影響しない。
    /// 実測値をそのまま画面に出せるよう、判定に落ちた動きも通知する。
    /// </summary>
    [AddComponentMenu("Pentomino/Flick Sample")]
    [RequireComponent(typeof(RectTransform))]
    public sealed class FlickSample : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
    {
        private readonly FlickTracker _flick = new FlickTracker();

        private PolyominoView _view;
        private PentominoStyle _style;
        private Posture _posture;
        private Canvas _canvas;

        /// <summary>いま運んでいる指の番号。本番と同じく、2 本目は見ない。</summary>
        private int _pointerId = NoPointer;

        private const int NoPointer = int.MinValue;

        /// <summary>掴んだときの、指とピースのずれ。運ぶ間これを保つ。</summary>
        private Vector2 _grabOffset;

        /// <summary>置き場所。離したらここへ戻す。</summary>
        private Vector2 _home;
        private bool _homeKnown;

        /// <summary>指を動かすたびに呼ばれる。フリックにならなかった動きも含む。</summary>
        public event Action<FlickAxis> Moved;

        /// <summary>判定に使うしきい値の取り出し口。画面側が差し込む。</summary>
        public Func<FlickSettings> SettingsSource { get; set; }

        public FlickTracker Flick => _flick;

        /// <summary>練習用のピースを描く。</summary>
        public void Show(Posture posture, PentominoStyle style)
        {
            _style = style;
            _posture = posture;

            // 最初に描かれた場所を置き場所として覚える。
            if (!_homeKnown)
            {
                _home = ((RectTransform)transform).anchoredPosition;
                _homeKnown = true;
            }

            if (_view == null)
            {
                _view = GetComponent<PolyominoView>();
                if (_view == null) _view = gameObject.AddComponent<PolyominoView>();
            }

            _view.RenderPiece(posture, style);
        }

        public void OnBeginDrag(PointerEventData eventData)
        {
            if (_pointerId != NoPointer) return;
            _pointerId = eventData.pointerId;

            _flick.Begin(eventData.position, SettingsSource != null ? SettingsSource() : null);

            // 掴んだ場所を覚えて、指との位置関係を保ったまま運ぶ。
            if (TryLocal(eventData.position, out var local))
                _grabOffset = Rect.anchoredPosition - local;
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (eventData.pointerId != _pointerId) return;

            // 本番と同じく、運ぶだけ。反転は離すまで起きない。
            //
            // ここでピースを動かさないと、1 フレームの重さが本番と違ってしまう。
            // 速度をならして測っているので、軽い画面で合わせたしきい値は
            // 重い画面では別物になる。同じ重さで測るために動かす。
            if (TryLocal(eventData.position, out var local))
                Rect.anchoredPosition = local + _grabOffset;

            _flick.Feed(eventData.position);

            Moved?.Invoke(FlickAxis.None);
        }

        public void OnEndDrag(PointerEventData eventData)
        {
            if (eventData.pointerId != _pointerId) return;
            _pointerId = NoPointer;

            // 本番と同じく、離した勢いで一度だけ判定する。
            var axis = _flick.Release(SettingsSource != null ? SettingsSource() : null);
            if (axis != FlickAxis.None) Apply(axis);

            // 試すたびに散らばると邪魔になるので、置き場所へ戻す。
            Rect.anchoredPosition = _home;

            Moved?.Invoke(axis);
        }

        /// <summary>画面の座標を、親から見た座標に直す。</summary>
        private bool TryLocal(Vector2 screenPosition, out Vector2 local)
        {
            local = Vector2.zero;

            var parent = Rect.parent as RectTransform;
            if (parent == null) return false;

            return RectTransformUtility.ScreenPointToLocalPointInRectangle(
                parent, screenPosition, EventCamera, out local);
        }

        /// <summary>座標の変換に使うカメラ。画面に直接描いているなら要らない。</summary>
        private Camera EventCamera
        {
            get
            {
                if (_canvas == null) _canvas = GetComponentInParent<Canvas>();
                if (_canvas == null) return null;

                return _canvas.renderMode == RenderMode.ScreenSpaceOverlay
                    ? null
                    : _canvas.worldCamera;
            }
        }

        private RectTransform Rect => (RectTransform)transform;

        private void Apply(FlickAxis axis)
        {
            if (_posture == null) return;

            // 本番と同じく転がす。回せない向きなら姿勢はそのまま。
            var rolled = _posture.Rolled(axis);
            if (rolled == null) return;

            _posture = rolled;
            _view.RenderPiece(_posture, _style);
        }
    }
}
