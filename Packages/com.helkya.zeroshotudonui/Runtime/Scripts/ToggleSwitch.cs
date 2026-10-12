
using UdonSharp;
using UnityEngine;
using UnityEngine.UI;

// CMP-03・SYS-02
namespace ZeroShotUdonUI
{
    /// <summary>
    /// トグルスイッチ。状態は uGUI の Toggle が持ち、このスクリプトを通して読み書きする。
    /// 切り替わると通知先にイベントを送る。
    /// </summary>
    [UdonBehaviourSyncMode(BehaviourSyncMode.None)]
    public class ToggleSwitch : ControlBase
    {
        [SerializeField] private Toggle toggle;
        [SerializeField] private Image track;
        [SerializeField] private RectTransform knob;
        [Tooltip("オンのときに土台へ適用するマテリアル")]
        [SerializeField] private Material onMaterial;
        [Tooltip("オフのときに土台へ適用するマテリアル")]
        [SerializeField] private Material offMaterial;
        [Tooltip("つまみが中央から左右に動く距離")]
        [SerializeField] private float knobOffset = 22f;

        /// <summary>現在の状態。オンなら true。</summary>
        public bool IsOn
        {
            get { return toggle.isOn; }
        }

        private void Start()
        {
            ApplyVisual();
        }

        /// <summary>状態を変更する。実際に変わった場合は通知先にイベントを送る。</summary>
        public void SetValue(bool value)
        {
            if (toggle.isOn == value)
                return;

            SetValueWithoutNotify(value);
            Notify();
        }

        /// <summary>通知を出さずに状態を変更する。</summary>
        public void SetValueWithoutNotify(bool value)
        {
            toggle.SetIsOnWithoutNotify(value);
            ApplyVisual();
        }

        // uGUI の Toggle.onValueChanged から呼ばれる。
        public void _OnToggleValueChanged()
        {
            ApplyVisual();
            Notify();
        }

        private void ApplyVisual()
        {
            bool isOn = toggle.isOn;
            track.material = isOn ? onMaterial : offMaterial;
            knob.anchoredPosition = new Vector2(isOn ? knobOffset : -knobOffset, 0f);
        }
    }
}
