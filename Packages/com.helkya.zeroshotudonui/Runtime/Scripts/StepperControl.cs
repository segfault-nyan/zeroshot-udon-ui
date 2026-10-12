
using System.Globalization;
using TMPro;
using UdonSharp;
using UnityEngine;
using UnityEngine.UI;

// CMP-08・SYS-02
namespace ZeroShotUdonUI
{
    /// <summary>
    /// 数値ステッパー。＋と－のボタンで数値を増減する。
    /// 対応する uGUI の部品が無いので、値はこのスクリプトが持つ。値が変わると通知先にイベントを送る。
    /// </summary>
    [UdonBehaviourSyncMode(BehaviourSyncMode.None)]
    public class StepperControl : ControlBase
    {
        [SerializeField] private float minValue = 0f;
        [SerializeField] private float maxValue = 10f;
        [Tooltip("ボタンを1回押したときに増減する量")]
        [SerializeField] private float step = 1f;
        [Tooltip("最初の値")]
        [SerializeField] private float currentValue = 0f;
        [Tooltip("現在値の表示形式。\"0\" は整数、\"0.0\" は小数第1位まで表示する")]
        [SerializeField] private string valueFormat = "0";
        [SerializeField] private TextMeshProUGUI valueLabel;
        [SerializeField] private Button decreaseButton;
        [SerializeField] private Button increaseButton;

        /// <summary>現在の値。</summary>
        public float Value
        {
            get { return currentValue; }
        }

        private void Start()
        {
            currentValue = Snap(currentValue);
            ApplyVisual();
        }

        /// <summary>値を変更する。実際に変わった場合は通知先にイベントを送る。範囲外の値は範囲内に丸められる。</summary>
        public void SetValue(float newValue)
        {
            float before = currentValue;
            SetValueWithoutNotify(newValue);

            if (currentValue != before)
                Notify();
        }

        /// <summary>通知を出さずに値を変更する。</summary>
        public void SetValueWithoutNotify(float newValue)
        {
            currentValue = Snap(newValue);
            ApplyVisual();
        }

        // －のボタンから呼ばれる。
        public void _OnDecrease()
        {
            SetValue(currentValue - step);
        }

        // ＋のボタンから呼ばれる。
        public void _OnIncrease()
        {
            SetValue(currentValue + step);
        }

        // 範囲内に収め、最小値から数えて増減の幅の倍数になるよう丸める。増減を繰り返しても誤差が溜まらない。
        private float Snap(float target)
        {
            target = Mathf.Clamp(target, minValue, maxValue);
            if (step <= 0f)
                return target;

            float snapped = minValue + Mathf.Round((target - minValue) / step) * step;

            // 最大値が倍数でないときに、丸めた結果が最大値を超えないようにする。
            // 小数の誤差でわずかに超えただけの場合は、最大値そのものとして扱う。
            if (snapped > maxValue + step * 0.001f)
                snapped -= step;

            return Mathf.Min(snapped, maxValue);
        }

        private void ApplyVisual()
        {
            // 端末の言語設定で表記が変わらないよう、書式を固定する。
            if (valueLabel != null)
                valueLabel.text = currentValue.ToString(valueFormat, CultureInfo.InvariantCulture);

            // これ以上は増減できない側のボタンを、押せなくする。
            decreaseButton.interactable = Snap(currentValue - step) != currentValue;
            increaseButton.interactable = Snap(currentValue + step) != currentValue;
        }
    }
}
