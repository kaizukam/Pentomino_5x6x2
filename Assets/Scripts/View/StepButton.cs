using System;
using Pentomino.Core;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Pentomino.View
{
    /// <summary>
    /// 問題送りボタン。軽く押せば 1 問。
    /// 押し続けると 1 秒後から 0.3 秒ごとに 10 問ずつ、3 秒を超えると 100 問ずつ動く。
    /// </summary>
    [AddComponentMenu("Pentomino/Step Button")]
    [RequireComponent(typeof(RectTransform))]
    public sealed class StepButton : MonoBehaviour,
        IPointerDownHandler, IPointerUpHandler, IPointerExitHandler
    {
        [Tooltip("送りの向き。戻しは -1、送りは +1")]
        [SerializeField] private int _direction = 1;

        [Tooltip("押し続けてから繰り返しが始まるまでの時間（秒）")]
        [SerializeField] private float _holdDelay = StepRepeatRule.DefaultHoldDelay;

        [Tooltip("繰り返しの間隔（秒）")]
        [SerializeField] private float _repeatInterval = StepRepeatRule.DefaultRepeatInterval;

        [Tooltip("さらに加速するまでの時間（秒）")]
        [SerializeField] private float _fastDelay = StepRepeatRule.DefaultFastDelay;

        [Tooltip("軽く押したときの移動量")]
        [SerializeField] private int _tapStep = StepRepeatRule.DefaultTapStep;

        [Tooltip("加速前の 1 回あたりの移動量")]
        [SerializeField] private int _repeatStep = StepRepeatRule.DefaultRepeatStep;

        [Tooltip("加速後の 1 回あたりの移動量")]
        [SerializeField] private int _fastStep = StepRepeatRule.DefaultFastStep;

        private bool _pressing;
        private float _downTime;
        private int _repeatsFired;
        private int _stepFired;

        /// <summary>移動量を渡して通知する。符号は向きを含む。</summary>
        public event Action<int> Stepped;

        /// <summary>送りの向き。-1 で戻し、+1 で送り。</summary>
        public int Direction
        {
            get => _direction;
            set => _direction = value >= 0 ? 1 : -1;
        }

        public bool IsPressing => _pressing;

        /// <summary>押し続けてから繰り返しが始まるまでの時間（秒）。</summary>
        public float HoldDelay => _holdDelay;

        /// <summary>押し続けて繰り返しが始まっているか（見た目を変えたいとき用）。</summary>
        public bool IsRepeating => _repeatsFired > 0;

        public void OnPointerDown(PointerEventData eventData)
        {
            _pressing = true;
            _downTime = Time.unscaledTime;
            _repeatsFired = 0;
            _stepFired = 0;
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            if (!_pressing) return;
            _pressing = false;

            // 繰り返しが始まっていたなら、離した瞬間に 1 問ぶん余計に動かさない。
            if (StepRepeatRule.ShouldStepOnRelease(_repeatsFired)) Fire(_tapStep);
            Reset();
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            // 指がボタンから外れたら、何もせず取り消す。
            _pressing = false;
            Reset();
        }

        private void Update()
        {
            if (!_pressing) return;

            var held = Time.unscaledTime - _downTime;
            _repeatsFired = StepRepeatRule.RepeatsSoFar(held, _holdDelay, _repeatInterval);

            var total = StepRepeatRule.TotalStepSoFar(
                held, _holdDelay, _repeatInterval, _fastDelay, _repeatStep, _fastStep);

            if (total <= _stepFired) return;

            var delta = total - _stepFired;
            _stepFired = total;
            Fire(delta);
        }

        private void Reset()
        {
            _repeatsFired = 0;
            _stepFired = 0;
        }

        private void Fire(int amount) => Stepped?.Invoke(Direction * amount);
    }
}
