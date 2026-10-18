
using UdonSharp;
using UnityEngine;
using VRC.Udon;

// SYS-01・SYS-06
namespace ZeroShotUdonUI
{
    /// <summary>
    /// 操作できる部品の共通の親。操作されたことを、設定された通知先の Udon にイベントで伝える。
    /// 同期に対応する部品では、中継役（SyncRelay）を通して、操作の結果をインスタンス内の全員に送る。
    /// </summary>
    public abstract class ControlBase : UdonSharpBehaviour
    {
        [Tooltip("操作を通知する先の UdonBehaviour")]
        [SerializeField] protected UdonBehaviour eventTarget;
        [Tooltip("通知先で呼び出すイベント名（public メソッドの名前）")]
        [SerializeField] protected string eventName;
        [Tooltip("同期の中継役。設定されていれば、操作の結果がインスタンス内の全員に反映される。UIBuilder.SetSynced で設定する")]
        [SerializeField] protected SyncRelay syncRelay;

        /// <summary>操作の結果を、インスタンス内の全員に反映する部品なら true。</summary>
        public bool IsSynced
        {
            get { return syncRelay != null; }
        }

        protected void Notify()
        {
            if (eventTarget == null || string.IsNullOrEmpty(eventName))
                return;

            eventTarget.SendCustomEvent(eventName);
        }

        /// <summary>
        /// 同期する部品なら、値を中継役に渡して全員に送る。同期しない部品では何もしない。
        /// </summary>
        protected void SendToOthers(float value)
        {
            if (syncRelay != null)
                syncRelay.Send(value);
        }

        /// <summary>
        /// 中継役から、ほかの人が送った値を受け取ったときに呼ばれる。値は syncRelay.Value で読む。
        /// 値を持つ部品は、これを上書きして、表示を合わせて通知を送る。
        /// </summary>
        public virtual void _OnSyncedValue()
        {
        }

        /// <summary>
        /// 中継役から、誰かが操作したという出来事を受け取ったときに呼ばれる。操作した本人の手元でも呼ばれる。
        /// 値を持たない部品（ボタン）は、これを上書きして通知を送る。
        /// </summary>
        public virtual void _OnSyncedEvent()
        {
        }
    }
}
