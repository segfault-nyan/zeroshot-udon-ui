
using UdonSharp;
using UnityEngine;
using VRC.SDKBase;

// SYS-06
namespace ZeroShotUdonUI
{
    /// <summary>
    /// 同期の中継役。部品1つにつき1つ置き、その部品の値をインスタンス内の全員に送り、受け取った値を部品に渡す。
    ///
    /// VRChat の同期は、無効になっているオブジェクトでは止まる。部品はウィンドウやタブの中で無効になることがあるので、
    /// 同期の受け持ちを部品から切り離し、常に有効な場所（シーンの最上位の「ZS Sync」の下）に置いたこのスクリプトが受け持つ。
    /// 無効になっている部品にも、ほかのスクリプトからの呼び出しは届くので、受け取った値はすぐに部品へ渡せる。
    ///
    /// 値は数値1つで持つ。オン・オフは 0 と 1、選択肢は番号として扱い、どの部品にも同じスクリプトを使う。
    /// </summary>
    [UdonBehaviourSyncMode(BehaviourSyncMode.Manual)]
    public class SyncRelay : UdonSharpBehaviour
    {
        [Tooltip("同期する部品")]
        [SerializeField] private ControlBase control;

        // 送る値と、誰かが一度でも値を送ったかの印。
        // 誰も操作していないうちは、後から入ってきた人の部品を上書きしない（各自の初期値のままにする）。
        [UdonSynced] private float syncedValue;
        [UdonSynced] private bool hasValue;

        /// <summary>最後に送った、または受け取った値。</summary>
        public float Value
        {
            get { return syncedValue; }
        }

        /// <summary>
        /// 部品から呼ばれる。自分が持ち主になって、値を全員に送る。
        /// </summary>
        public void Send(float value)
        {
            syncedValue = value;
            hasValue = true;

            // 値を書き換えて送れるのは、持ち主だけ。操作した人が持ち主になる。
            if (!Networking.IsOwner(gameObject))
                Networking.SetOwner(Networking.LocalPlayer, gameObject);

            RequestSerialization();
        }

        // ほかの人が送った値を受け取ったときに呼ばれる。後から入ってきたときにも、今の値で呼ばれる。
        public override void OnDeserialization()
        {
            if (!hasValue || control == null)
                return;

            control._OnSyncedValue();
        }
    }
}
