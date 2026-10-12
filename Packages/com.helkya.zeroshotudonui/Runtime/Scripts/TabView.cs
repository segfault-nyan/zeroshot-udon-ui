
using UdonSharp;
using UnityEngine;

// LAY-05・SYS-02
namespace ZeroShotUdonUI
{
    /// <summary>
    /// タブ。見出しを押して、表示するページを切り替える。
    /// どのページを選んでいるかは見出しの選択ボタン群が持ち、このスクリプトを通して読み書きする。
    /// ページは pageContainer の子で、並び順が見出しの順番に対応する。ページが切り替わると通知先にイベントを送る。
    /// </summary>
    [UdonBehaviourSyncMode(BehaviourSyncMode.None)]
    public class TabView : ControlBase
    {
        [Tooltip("見出しになる選択ボタン群")]
        [SerializeField] private SegmentedControl header;
        [Tooltip("ページを子に持つオブジェクト。子の並び順が、見出しの順番に対応する")]
        [SerializeField] private Transform pageContainer;

        /// <summary>表示しているページの番号。左端の見出しが 0。</summary>
        public int SelectedIndex
        {
            get { return header.SelectedIndex; }
        }

        /// <summary>ページの数。</summary>
        public int Count
        {
            get { return pageContainer.childCount; }
        }

        private void Start()
        {
            ApplyVisual();
        }

        /// <summary>表示するページを変更する。実際に変わった場合は通知先にイベントを送る。範囲外の番号は無視する。</summary>
        public void SetValue(int index)
        {
            if (index == header.SelectedIndex || index < 0 || index >= pageContainer.childCount)
                return;

            SetValueWithoutNotify(index);
            Notify();
        }

        /// <summary>通知を出さずに、表示するページを変更する。範囲外の番号は無視する。</summary>
        public void SetValueWithoutNotify(int index)
        {
            if (index < 0 || index >= pageContainer.childCount)
                return;

            header.SetValueWithoutNotify(index);
            ApplyVisual();
        }

        // 見出しの選択ボタン群から、選択が変わったときに呼ばれる。
        public void _OnHeaderChanged()
        {
            ApplyVisual();
            Notify();
        }

        // 選ばれているページだけを表示する。
        private void ApplyVisual()
        {
            int selectedIndex = header.SelectedIndex;
            int pageCount = pageContainer.childCount;
            for (int i = 0; i < pageCount; i++)
                pageContainer.GetChild(i).gameObject.SetActive(i == selectedIndex);
        }
    }
}
