
using UdonSharp;
using UnityEngine;
using UnityEngine.UI;

// CMP-06・SYS-02・SYS-06
namespace ZeroShotUdonUI
{
    /// <summary>
    /// 選択ボタン群。横に並んだ選択肢から1つを選ぶ。選択肢は子の Toggle で、Unity 上で複製すれば増やせる。
    /// どれが選ばれているかは uGUI の Toggle が持ち、このスクリプトを通して読み書きする。
    /// 選択が変わると通知先にイベントを送る。
    /// 同期する設定（中継役あり）のときは、選んだ番号をインスタンス内の全員に送り、受け取った側でも通知先にイベントを送る。
    /// </summary>
    [UdonBehaviourSyncMode(BehaviourSyncMode.None)]
    public class SegmentedControl : ControlBase
    {
        [Tooltip("選択中の選択肢に適用するマテリアル")]
        [SerializeField] private Material selectedMaterial;
        [Tooltip("選択されていない選択肢に適用するマテリアル")]
        [SerializeField] private Material unselectedMaterial;

        private Toggle[] toggles;
        private bool initialized;

        // 変化があったかを判定するための控え。正は各 Toggle の状態。
        private int selectedIndex = -1;

        /// <summary>選択中の選択肢の番号。左端が 0。</summary>
        public int SelectedIndex
        {
            get
            {
                EnsureInitialized();
                return selectedIndex;
            }
        }

        /// <summary>選択肢の数。</summary>
        public int Count
        {
            get
            {
                EnsureInitialized();
                return toggles.Length;
            }
        }

        private void Start()
        {
            EnsureInitialized();
        }

        /// <summary>選択を変更する。実際に変わった場合は通知先にイベントを送る。範囲外の番号は無視する。同期する設定なら、全員に送る。</summary>
        public void SetValue(int index)
        {
            EnsureInitialized();

            if (index == selectedIndex || index < 0 || index >= toggles.Length)
                return;

            SetValueWithoutNotify(index);
            SendToOthers(selectedIndex);
            Notify();
        }

        /// <summary>通知を出さずに選択を変更する。範囲外の番号は無視する。手元だけで変わり、ほかの人には送らない。</summary>
        public void SetValueWithoutNotify(int index)
        {
            EnsureInitialized();

            if (index < 0 || index >= toggles.Length)
                return;

            // 先に選ぶ側をオンにする。ToggleGroup は全部オフの状態を許さないため。
            toggles[index].SetIsOnWithoutNotify(true);
            for (int i = 0; i < toggles.Length; i++)
            {
                if (i != index)
                    toggles[i].SetIsOnWithoutNotify(false);
            }

            selectedIndex = index;
            ApplyVisual();
        }

        // 各選択肢の Toggle.onValueChanged から呼ばれる。1回の操作で、オフになる側とオンになる側の2回呼ばれる。
        public void _OnSegmentValueChanged()
        {
            EnsureInitialized();

            int index = FindSelectedIndex();
            if (index == selectedIndex)
                return;

            selectedIndex = index;
            ApplyVisual();
            SendToOthers(selectedIndex);
            Notify();
        }

        // 中継役から、ほかの人が送った値を受け取ったときに呼ばれる。
        public override void _OnSyncedValue()
        {
            EnsureInitialized();

            int index = Mathf.RoundToInt(syncRelay.Value);
            if (index == selectedIndex || index < 0 || index >= toggles.Length)
                return;

            SetValueWithoutNotify(index);
            Notify();
        }

        // 他のスクリプトの Start から先に呼ばれても正しく動くよう、初回の利用時に準備する。
        private void EnsureInitialized()
        {
            if (initialized)
                return;

            initialized = true;
            toggles = GetComponentsInChildren<Toggle>(true);
            selectedIndex = FindSelectedIndex();
            ApplyVisual();
        }

        private int FindSelectedIndex()
        {
            for (int i = 0; i < toggles.Length; i++)
            {
                if (toggles[i].isOn)
                    return i;
            }

            return -1;
        }

        private void ApplyVisual()
        {
            for (int i = 0; i < toggles.Length; i++)
            {
                Image background = (Image)toggles[i].targetGraphic;
                if (background != null)
                    background.material = i == selectedIndex ? selectedMaterial : unselectedMaterial;
            }
        }
    }
}
