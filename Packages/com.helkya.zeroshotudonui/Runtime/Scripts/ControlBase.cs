
using UdonSharp;
using UnityEngine;
using VRC.Udon;

// SYS-01
namespace ZeroShotUdonUI
{
    /// <summary>
    /// 操作できる部品の共通の親。操作されたことを、設定された通知先の Udon にイベントで伝える。
    /// </summary>
    public abstract class ControlBase : UdonSharpBehaviour
    {
        [Tooltip("操作を通知する先の UdonBehaviour")]
        [SerializeField] protected UdonBehaviour eventTarget;
        [Tooltip("通知先で呼び出すイベント名（public メソッドの名前）")]
        [SerializeField] protected string eventName;

        protected void Notify()
        {
            if (eventTarget == null || string.IsNullOrEmpty(eventName))
                return;

            eventTarget.SendCustomEvent(eventName);
        }
    }
}
