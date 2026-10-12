
using System.Globalization;
using TMPro;
using UdonSharp;
using UnityEngine;
using UnityEngine.UI;

// CMP-09
namespace ZeroShotUdonUI
{
    /// <summary>
    /// 進捗バー。進み具合を表示する。操作はできないので、イベントは送らない。
    /// 値は uGUI の Slider が持ち、このスクリプトを通して読み書きする。
    /// </summary>
    [UdonBehaviourSyncMode(BehaviourSyncMode.None)]
    public class ProgressBar : UdonSharpBehaviour
    {
        [SerializeField] private Slider slider;
        [Tooltip("現在値を表示するテキスト。不要なら空にする")]
        [SerializeField] private TextMeshProUGUI valueLabel;
        [Tooltip("現在値の表示形式。\"0%\" は 0.5 を 50% と表示する")]
        [SerializeField] private string valueFormat = "0%";

        /// <summary>現在の値。0 から 1。</summary>
        public float Value
        {
            get { return slider.value; }
        }

        private void Start()
        {
            UpdateLabel();
        }

        /// <summary>値を変更する。0 から 1 の範囲に丸められる。</summary>
        public void SetValue(float value)
        {
            slider.SetValueWithoutNotify(value);
            UpdateLabel();
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
