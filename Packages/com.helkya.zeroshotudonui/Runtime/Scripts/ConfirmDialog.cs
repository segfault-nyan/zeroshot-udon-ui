
using TMPro;
using UdonSharp;
using UnityEngine;

// LAY-07
namespace ZeroShotUdonUI
{
    /// <summary>
    /// 確認ダイアログ。メッセージと、「はい」「いいえ」にあたる2つのボタンを表示する。
    /// どちらかのボタンが押されると閉じ、通知先にイベントを送る。「はい」のときは共通のイベント名、
    /// 「いいえ」のときは cancelEventName のイベントを送る。
    /// このスクリプト自体は、初期化（Start）が開閉に左右されないよう、常に有効なオブジェクトに置く。
    /// </summary>
    [UdonBehaviourSyncMode(BehaviourSyncMode.None)]
    public class ConfirmDialog : ControlBase
    {
        [Tooltip("「いいえ」のボタンが押されたときに、通知先で呼び出すイベント名。不要なら空にする")]
        [SerializeField] private string cancelEventName;
        [Tooltip("開閉で有効・無効を切り替える対象。Canvas を指定すると、閉じている間は当たり判定も無効になる")]
        [SerializeField] private GameObject content;
        [SerializeField] private TextMeshProUGUI messageLabel;
        [Tooltip("オンなら、ワールドの開始時に閉じる。エディタ上では表示したままにして、位置や文字を調整できる")]
        [SerializeField] private bool closeOnStart = true;

        // Start より先に Open が呼ばれた場合に、Start で閉じてしまわないための目印。
        private bool openedBeforeStart;

        /// <summary>開いていれば true。</summary>
        public bool IsOpen
        {
            get { return content.activeSelf; }
        }

        private void Start()
        {
            if (closeOnStart && !openedBeforeStart)
                content.SetActive(false);
        }

        public void Open()
        {
            openedBeforeStart = true;
            content.SetActive(true);
        }

        /// <summary>イベントを送らずに閉じる。</summary>
        public void Close()
        {
            content.SetActive(false);
        }

        /// <summary>表示するメッセージを変更する。</summary>
        public void SetMessage(string message)
        {
            messageLabel.text = message;
        }

        // 「はい」のボタンから呼ばれる。
        public void _OnConfirm()
        {
            content.SetActive(false);
            Notify();
        }

        // 「いいえ」のボタンから呼ばれる。
        public void _OnCancel()
        {
            content.SetActive(false);

            if (eventTarget == null || string.IsNullOrEmpty(cancelEventName))
                return;

            eventTarget.SendCustomEvent(cancelEventName);
        }
    }
}
