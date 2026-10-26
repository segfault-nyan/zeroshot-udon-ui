
using TMPro;
using UdonSharp;
using UnityEngine;

// SYS-06 の使い方の見本
namespace ZeroShotUdonUI.Samples
{
    /// <summary>
    /// 同期する部品を使う側（ワールド制作者）のスクリプトの見本。デモシーンの「Sync」のパネルで使う。
    /// 同期のコードは書かない。部品の同期を有効にしておけば、誰が操作しても、後から入ってきても、
    /// 全員の手元で下の public メソッドが呼ばれる。
    /// 通知を受けたら、部品の今の値を読み、ワールドの状態をそれに合わせる（前の状態に頼らない）。
    /// </summary>
    [UdonBehaviourSyncMode(BehaviourSyncMode.None)]
    public class SyncDemoController : UdonSharpBehaviour
    {
        // 立方体の大きさ（メートル）。スライダーの 0 から 1 を、この範囲に当てはめる。
        private const float MinCubeSize = 0.15f;
        private const float MaxCubeSize = 0.5f;

        [Header("Synced controls")]
        [SerializeField] private ToggleSwitch lampToggle;
        [SerializeField] private SliderControl sizeSlider;
        [SerializeField] private SegmentedControl colorSelector;
        [SerializeField] private TextMeshProUGUI ringLabel;

        [Header("Not synced")]
        [SerializeField] private ToggleSwitch localLampToggle;

        [Header("World objects")]
        [SerializeField] private GameObject lamp;
        [SerializeField] private GameObject localLamp;
        [SerializeField] private Transform cube;
        [SerializeField] private Renderer cubeRenderer;

        private int ringCount;

        private void Start()
        {
            // 最初の状態を、部品の初期値に合わせる。
            OnLampChanged();
            OnSizeChanged();
            OnColorChanged();
            OnLocalLampChanged();
            ShowRingCount();
        }

        // 以下の public メソッドは、各部品の「イベント名」に設定した名前で呼ばれる。

        public void OnLampChanged()
        {
            lamp.SetActive(lampToggle.IsOn);
        }

        public void OnSizeChanged()
        {
            float size = Mathf.Lerp(MinCubeSize, MaxCubeSize, sizeSlider.Value);
            cube.localScale = new Vector3(size, size, size);
        }

        public void OnColorChanged()
        {
            cubeRenderer.material.color = GetColor(colorSelector.SelectedIndex);
        }

        // ボタンは値を持たないので、押されるたびに全員の手元で呼ばれる。後から入ってきた人には届かない。
        public void OnRing()
        {
            ringCount++;
            ShowRingCount();
        }

        // 同期しない部品。自分の画面でだけ変わる。
        public void OnLocalLampChanged()
        {
            localLamp.SetActive(localLampToggle.IsOn);
        }

        private void ShowRingCount()
        {
            ringLabel.text = "Ring! x " + ringCount.ToString();
        }

        private Color GetColor(int index)
        {
            switch (index)
            {
                case 1: return new Color(0.30f, 0.80f, 0.45f);
                case 2: return new Color(1.00f, 0.60f, 0.20f);
                default: return new Color(0.36f, 0.55f, 1.00f);
            }
        }
    }
}
