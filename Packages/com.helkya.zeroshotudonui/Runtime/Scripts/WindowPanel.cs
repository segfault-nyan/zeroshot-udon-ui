
using UdonSharp;
using UnityEngine;

// LAY-02
namespace ZeroShotUdonUI
{
    /// <summary>
    /// ウィンドウ。開閉の状態は中身のオブジェクトの有効・無効が持ち、このスクリプトを通して読み書きする。
    /// 開閉が変わると通知先にイベントを送る。
    /// このスクリプト自体は、初期化（Start）が開閉に左右されないよう、常に有効なオブジェクトに置く。
    /// </summary>
    [UdonBehaviourSyncMode(BehaviourSyncMode.None)]
    public class WindowPanel : ControlBase
    {
        [Tooltip("開閉で有効・無効を切り替える対象。Canvas を指定すると、閉じている間は当たり判定も無効になる")]
        [SerializeField] private GameObject content;

        /// <summary>開いていれば true。</summary>
        public bool IsOpen
        {
            get { return content.activeSelf; }
        }

        public void Open()
        {
            SetOpen(true);
        }

        public void Close()
        {
            SetOpen(false);
        }

        /// <summary>開いていれば閉じ、閉じていれば開く。</summary>
        public void ToggleOpen()
        {
            SetOpen(!content.activeSelf);
        }

        private void SetOpen(bool open)
        {
            if (content.activeSelf == open)
                return;

            content.SetActive(open);
            Notify();
        }
    }
}
