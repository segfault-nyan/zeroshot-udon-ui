
using UdonSharp;

// CMP-01
namespace ZeroShotUdonUI
{
    /// <summary>
    /// ボタン。押されると通知先にイベントを送る。
    /// </summary>
    [UdonBehaviourSyncMode(BehaviourSyncMode.None)]
    public class ButtonControl : ControlBase
    {
        // uGUI の Button.onClick から呼ばれる。
        public void _OnClick()
        {
            Notify();
        }
    }
}
