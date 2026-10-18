
using UdonSharp;

// CMP-01・SYS-06
namespace ZeroShotUdonUI
{
    /// <summary>
    /// ボタン。押されると通知先にイベントを送る。
    /// 同期する設定（中継役あり）のときは、インスタンス内の全員の手元で、通知先にイベントを送る。
    /// 後から入ってきた人には送らない（その場の出来事として扱う）。
    /// </summary>
    [UdonBehaviourSyncMode(BehaviourSyncMode.None)]
    public class ButtonControl : ControlBase
    {
        // uGUI の Button.onClick から呼ばれる。
        public void _OnClick()
        {
            // 同期する場合は、自分の手元も含めて、中継役から全員に届く。ここで通知すると二重になる。
            if (syncRelay != null)
            {
                syncRelay.SendEvent();
                return;
            }

            Notify();
        }

        // 中継役から、誰かがボタンを押したことを受け取ったときに呼ばれる。押した本人の手元でも呼ばれる。
        public override void _OnSyncedEvent()
        {
            Notify();
        }
    }
}
