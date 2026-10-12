
using UdonSharp;
using UnityEngine;
using UnityEngine.UI;

// CMP-05・SYS-02
namespace ZeroShotUdonUI
{
    /// <summary>
    /// チェックボックス。状態は uGUI の Toggle が持ち、このスクリプトを通して読み書きする。
    /// 切り替わると通知先にイベントを送る。
    /// </summary>
    [UdonBehaviourSyncMode(BehaviourSyncMode.None)]
    public class CheckboxControl : ControlBase
    {
        [SerializeField] private Toggle toggle;
        [SerializeField] private Image box;
        [Tooltip("オンのときだけ表示するチェックのアイコン")]
        [SerializeField] private GameObject checkmark;
        [Tooltip("オンのときに枠へ適用するマテリアル")]
        [SerializeField] private Material onMaterial;
        [Tooltip("オフのときに枠へ適用するマテリアル")]
        [SerializeField] private Material offMaterial;

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
            box.material = isOn ? onMaterial : offMaterial;
            checkmark.SetActive(isOn);
        }
    }
}
