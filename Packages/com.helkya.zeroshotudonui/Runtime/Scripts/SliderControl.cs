
using System.Globalization;
using TMPro;
using UdonSharp;
using UnityEngine;
using UnityEngine.UI;

// CMP-04・SYS-02
namespace ZeroShotUdonUI
{
    /// <summary>
    /// スライダー。値と範囲は uGUI の Slider が持ち、このスクリプトを通して読み書きする。
    /// 値が変わると通知先にイベントを送る。
    /// </summary>
    [UdonBehaviourSyncMode(BehaviourSyncMode.None)]
    public class SliderControl : ControlBase
    {
        [SerializeField] private Slider slider;
        [Tooltip("現在値を表示するテキスト。不要なら空にする")]
        [SerializeField] private TextMeshProUGUI valueLabel;
        [Tooltip("現在値の表示形式。\"0%\" は 0.5 を 50% と表示し、\"0.00\" は 0.50 と表示する")]
        [SerializeField] private string valueFormat = "0%";

        /// <summary>現在の値。</summary>
        public float Value
        {
            get { return slider.value; }
        }

        private void Start()
        {
            UpdateLabel();
        }

        /// <summary>値を変更する。実際に変わった場合は通知先にイベントを送る。範囲外の値は範囲内に丸められる。</summary>
        public void SetValue(float value)
        {
            float before = slider.value;
            SetValueWithoutNotify(value);

            if (slider.value != before)
                Notify();
        }

        /// <summary>通知を出さずに値を変更する。</summary>
        public void SetValueWithoutNotify(float value)
        {
            slider.SetValueWithoutNotify(value);
            UpdateLabel();
        }

        // uGUI の Slider.onValueChanged から呼ばれる。
        public void _OnSliderValueChanged()
        {
            UpdateLabel();
            Notify();
        }

        private void UpdateLabel()
        {
            if (valueLabel == null)
                return;

            // 端末の言語設定で表記が変わらないよう、書式を固定する。
            valueLabel.text = slider.value.ToString(valueFormat, CultureInfo.InvariantCulture);
        }
    }
}
