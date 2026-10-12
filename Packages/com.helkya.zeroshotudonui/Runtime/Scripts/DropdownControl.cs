
using TMPro;
using UdonSharp;
using UnityEngine;

// CMP-07・SYS-02
namespace ZeroShotUdonUI
{
    /// <summary>
    /// ドロップダウン。一覧を開いて、選択肢から1つを選ぶ。
    /// 選択肢と、どれが選ばれているかは TextMeshPro の TMP_Dropdown が持ち、このスクリプトを通して読み書きする。
    /// 選択が変わると通知先にイベントを送る。
    /// Udon からは選択肢の文字を読めないので、読み書きできるのは番号だけ。
    /// </summary>
    [UdonBehaviourSyncMode(BehaviourSyncMode.None)]
    public class DropdownControl : ControlBase
    {
        [SerializeField] private TMP_Dropdown dropdown;

        /// <summary>選ばれている選択肢の番号。いちばん上が 0。</summary>
        public int SelectedIndex
        {
            get { return dropdown.value; }
        }

        /// <summary>選択を変更する。実際に変わった場合は通知先にイベントを送る。範囲外の番号は範囲内に丸められる。</summary>
        public void SetValue(int index)
        {
            int before = dropdown.value;
            dropdown.SetValueWithoutNotify(index);

            if (dropdown.value != before)
                Notify();
        }

        /// <summary>通知を出さずに選択を変更する。</summary>
        public void SetValueWithoutNotify(int index)
        {
            dropdown.SetValueWithoutNotify(index);
        }

        // TMP_Dropdown.onValueChanged から呼ばれる。
        public void _OnDropdownValueChanged()
        {
            Notify();
        }
    }
}
