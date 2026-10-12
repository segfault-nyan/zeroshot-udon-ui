
using System.Globalization;
using TMPro;
using UdonSharp;
using UnityEngine;

// SYS-01・SYS-02 の使い方の見本
namespace ZeroShotUdonUI.Samples
{
    /// <summary>
    /// ツールキットの部品を使う側（ワールド制作者）のスクリプトの見本。
    /// 部品からイベントを受け取り、値は部品に問い合わせて読む。
    /// </summary>
    [UdonBehaviourSyncMode(BehaviourSyncMode.None)]
    public class SampleController : UdonSharpBehaviour
    {
        // Reset で戻す先の値。サンプルの生成時に設定している初期値と同じ。
        private const float DefaultVolume = 0.5f;
        private const int DefaultQuality = 1;

        [SerializeField] private ToggleSwitch toggle;
        [SerializeField] private SliderControl slider;
        [SerializeField] private SegmentedControl quality;
        [SerializeField] private TextMeshProUGUI statusLabel;

        private int clickCount;

        // 以下の public メソッドは、各部品の「イベント名」に設定した名前で呼ばれる。

        public void OnToggleChanged()
        {
            statusLabel.text = toggle.IsOn ? "Last event: Notifications on" : "Last event: Notifications off";
        }

        public void OnSliderChanged()
        {
            statusLabel.text = "Last event: Volume " + slider.Value.ToString("0.00", CultureInfo.InvariantCulture);
        }

        public void OnQualityChanged()
        {
            statusLabel.text = "Last event: Quality " + quality.SelectedIndex.ToString();
        }

        public void OnButtonClicked()
        {
            clickCount++;
            statusLabel.text = "Last event: Clicked " + clickCount.ToString() + " times";
        }

        public void OnResetClicked()
        {
            // SetValueWithoutNotify は通知を出さないので、OnToggleChanged などは呼ばれない。
            toggle.SetValueWithoutNotify(false);
            slider.SetValueWithoutNotify(DefaultVolume);
            quality.SetValueWithoutNotify(DefaultQuality);

            clickCount = 0;
            statusLabel.text = "Last event: Reset";
        }
    }
}
