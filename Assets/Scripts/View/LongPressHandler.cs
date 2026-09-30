using System;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Pentomino.View
{
    /// <summary>
    /// 長押しを拾う。押したまま一定時間を超えたら 1 回だけ通知する。
    /// 指がずれたり離れたりしたら取り消す。
    /// </summary>
    [AddComponentMenu("Pentomino/Long Press Handler")]
    public sealed class LongPressHandler : MonoBehaviour,
        IPointerDownHandler, IPointerUpHandler, IPointerExitHandler, IDragHandler
    {
        /// <summary>長押しと認める時間（秒）。</summary>
        [SerializeField] private float _threshold = 0.6f;

        /// <summary>これ以上指がずれたら長押しを取り消す距離（ピクセル）。</summary>
        [SerializeField] private float _cancelDistance = 24f;

        private bool _pressing;
        private bool _fired;
        private float _downTime;
        private Vector2 _downPosition;

        public float Threshold
        {
            get => _threshold;
            set => _threshold = Mathf.Max(0.05f, value);
        }

        /// <summary>長押しが成立したとき。</summary>
        public event Action LongPressed;

        /// <summary>いま押されている最中か（テスト・演出用）。</summary>
        public bool IsPressing => _pressing;

        public void OnPointerDown(PointerEventData eventData)
        {
            _pressing = true;
            _fired = false;
            _downTime = Time.unscaledTime;
            _downPosition = eventData.position;
        }

        public void OnPointerUp(PointerEventData eventData) => Cancel();

        public void OnPointerExit(PointerEventData eventData) => Cancel();

        public void OnDrag(PointerEventData eventData)
        {
            if (!_pressing) return;
            if (Vector2.Distance(eventData.position, _downPosition) > _cancelDistance) Cancel();
        }

        private void Update()
        {
            if (!_pressing || _fired) return;
            if (Time.unscaledTime - _downTime < _threshold) return;

            _fired = true;
            LongPressed?.Invoke();
        }

        private void Cancel()
        {
            _pressing = false;
            _fired = false;
        }

    }
}
