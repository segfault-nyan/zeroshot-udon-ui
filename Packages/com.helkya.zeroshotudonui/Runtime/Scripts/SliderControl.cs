
using System.Globalization;
using TMPro;
using UdonSharp;
using UnityEngine;
using UnityEngine.UI;

// CMP-04・SYS-02・SYS-06
namespace ZeroShotUdonUI
{
    /// <summary>
    /// スライダー。値と範囲は uGUI の Slider が持ち、このスクリプトを通して読み書きする。
    /// 値が変わると通知先にイベントを送る。
    /// 同期する設定（中継役あり）のときは、変えた値をインスタンス内の全員に送り、受け取った側でも通知先にイベントを送る。
    /// つまみを動かし続けている間の送信は、VRChat がまとめて間引く。
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

        /// <summary>値を変更する。実際に変わった場合は通知先にイベントを送る。範囲外の値は範囲内に丸められる。同期する設定なら、全員に送る。</summary>
        public void SetValue(float value)
        {
            float before = slider.value;
            SetValueWithoutNotify(value);

            if (slider.value == before)
                return;

            SendToOthers(slider.value);
            Notify();
        }

        /// <summary>通知を出さずに値を変更する。手元だけで変わり、ほかの人には送らない。</summary>
        public void SetValueWithoutNotify(float value)
        {
            slider.SetValueWithoutNotify(value);
            UpdateLabel();
        }

        // uGUI の Slider.onValueChanged から呼ばれる。
        public void _OnSliderValueChanged()
        {
            UpdateLabel();
            SendToOthers(slider.value);
            Notify();
        }

        // 中継役から、ほかの人が送った値を受け取ったときに呼ばれる。
        public override void _OnSyncedValue()
        {
            float value = syncRelay.Value;
            if (slider.value == value)
                return;

            SetValueWithoutNotify(value);
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
