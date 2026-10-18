
using System.Collections.Generic;
using System.Reflection;
using TMPro;
using UdonSharp;
using UdonSharpEditor;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using VRC.SDK3.Components;
using VRC.Udon;

// EDT-03
namespace ZeroShotUdonUI.EditorTools
{
    public enum CheckSeverity
    {
        /// <summary>VRChat で操作できない、または意図どおりに動かない。</summary>
        Error,

        /// <summary>動くが、問題が起きやすい。</summary>
        Warning,

        /// <summary>知っておくとよいこと。</summary>
        Info,
    }

    /// <summary>
    /// 設定チェックで見つかった問題の1件。
    /// </summary>
    public class CheckIssue
    {
        public CheckSeverity Severity;

        /// <summary>何が問題か。</summary>
        public string Message;

        /// <summary>直し方。</summary>
        public string Advice;

        /// <summary>問題のあるオブジェクト。プロジェクト全体の設定が対象の場合は、その設定のアセット。</summary>
        public Object Target;

        // 自動で直す処理。自動では直せない場合は null。
        internal System.Action FixAction;

        /// <summary>自動で直せるなら true。</summary>
        public bool CanFix
        {
            get { return FixAction != null; }
        }

        /// <summary>自動で直す。直せない問題では何もしない。</summary>
        public void Fix()
        {
            if (FixAction != null)
                FixAction();
        }
    }

    /// <summary>
    /// 開いているシーンを調べて、VRChat で操作できない原因や、意図どおりに動かない原因になる設定の誤りを探す。
    /// ツールキットの部品を含まない Canvas は調べない。
    /// </summary>
    public static class SceneChecker
    {
        private const string UndoName = "Fix ZeroShot Udon UI Settings";

        // Unity の組み込みのレイヤー番号。
        private const int DefaultLayer = 0;
        private const int UILayer = 5;

        // Canvas の縮尺の想定は 0.001（1 単位 = 1mm）。これより大きく外れていたら知らせる。
        private const float MinCanvasScale = 0.0002f;
        private const float MaxCanvasScale = 0.01f;

        // 当たり判定の大きさと Canvas の大きさの差が、これを超えたら知らせる（Canvas 単位）。
        private const float ColliderSizeTolerance = 1f;
        private const float ColliderDepth = 10f;

        /// <summary>
        /// 開いているシーンを調べ、見つかった問題を返す。重いものから順に並ぶ。
        /// </summary>
        public static List<CheckIssue> Check()
        {
            List<CheckIssue> issues = new List<CheckIssue>();

            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                Scene scene = SceneManager.GetSceneAt(i);
                if (!scene.isLoaded)
                    continue;

                foreach (GameObject root in scene.GetRootGameObjects())
                {
                    foreach (Canvas canvas in root.GetComponentsInChildren<Canvas>(true))
                        CheckCanvas(canvas, issues);

                    foreach (ControlBase control in root.GetComponentsInChildren<ControlBase>(true))
                        CheckEvent(control, issues);

                    foreach (TabView tabs in root.GetComponentsInChildren<TabView>(true))
                        CheckTabs(tabs, issues);

                    foreach (SegmentedControl segmented in root.GetComponentsInChildren<SegmentedControl>(true))
                        CheckSegmented(segmented, issues);

                    foreach (ControlBase control in root.GetComponentsInChildren<ControlBase>(true))
                        CheckSync(control, issues);

                    foreach (SyncRelay relay in root.GetComponentsInChildren<SyncRelay>(true))
                        CheckRelay(relay, issues);
                }
            }

            CheckFont(issues);

            issues.Sort((a, b) => a.Severity.CompareTo(b.Severity));
            return issues;
        }

        /// <summary>
        /// 開いているシーンを調べ、結果を Console に書き出す。エラーと警告の合計数を返す。
        /// Unity の画面を見られない場合（AI がコードから UI を作った場合など）に、結果を文字で受け取るために使う。
        /// </summary>
        public static int CheckAndLog()
        {
            List<CheckIssue> issues = Check();
            Log(issues);

            int count = 0;
            foreach (CheckIssue issue in issues)
            {
                if (issue.Severity != CheckSeverity.Info)
                    count++;
            }

            return count;
        }

        /// <summary>
        /// 調べた結果を Console に書き出す。
        /// </summary>
        public static void Log(List<CheckIssue> issues)
        {
            int errors = 0;
            int warnings = 0;
            int infos = 0;

            foreach (CheckIssue issue in issues)
            {
                string text = "[" + UIBuilder.DisplayName + "] 設定チェック（" + GetSeverityLabel(issue.Severity) + "）: " + issue.Message
                    + "\n対象: " + GetTargetPath(issue.Target)
                    + "\n直し方: " + issue.Advice;

                switch (issue.Severity)
                {
                    case CheckSeverity.Error:
                        errors++;
                        Debug.LogError(text, issue.Target);
                        break;
                    case CheckSeverity.Warning:
                        warnings++;
                        Debug.LogWarning(text, issue.Target);
                        break;
                    default:
                        infos++;
                        Debug.Log(text, issue.Target);
                        break;
                }
            }

            Debug.Log("[" + UIBuilder.DisplayName + "] 設定チェックの結果: エラー " + errors + " 件、警告 " + warnings + " 件、情報 " + infos + " 件");
        }

        internal static string GetSeverityLabel(CheckSeverity severity)
        {
            switch (severity)
            {
                case CheckSeverity.Error: return "エラー";
                case CheckSeverity.Warning: return "警告";
                default: return "情報";
            }
        }

        /// <summary>
        /// 対象の場所を、Hierarchy での親からの並びで返す。シーンのオブジェクトでなければ、名前を返す。
        /// </summary>
        internal static string GetTargetPath(Object target)
        {
            if (target == null)
                return "（なし）";

            Transform transform = null;
            if (target is GameObject gameObject)
                transform = gameObject.transform;
            else if (target is Component component)
                transform = component.transform;

            if (transform == null)
                return target.name;

            string path = transform.name;
            while (transform.parent != null)
            {
                transform = transform.parent;
                path = transform.name + "/" + path;
            }

            return path;
        }

        private static void CheckCanvas(Canvas canvas, List<CheckIssue> issues)
        {
            GameObject canvasObject = canvas.gameObject;
            if (!ContainsToolkitPart(canvasObject))
                return;

            bool isVRChatCanvas = canvas.GetComponent<VRCUiShape>() != null;
            Canvas parentCanvas = canvas.transform.parent != null
                ? canvas.transform.parent.GetComponentInParent<Canvas>(true)
                : null;

            if (parentCanvas != null)
            {
                // 利用者が意図して作った入れ子の Canvas は対象にしない。VRChat 用の設定を持つ Canvas が、別の Canvas の中にある場合だけ知らせる。
                if (isVRChatCanvas)
                {
                    Add(issues, CheckSeverity.Error, canvasObject,
                        "Canvas が、別の Canvas（" + parentCanvas.name + "）の中にあります。縮尺が二重に掛かり、見えない大きさになります。",
                        "この Canvas（ウィンドウや確認ダイアログの場合は、その親ごと）を、ほかの Canvas の外に移してください。");
                }

                return;
            }

            if (canvas.renderMode != RenderMode.WorldSpace)
            {
                Add(issues, CheckSeverity.Error, canvasObject,
                    "Canvas が、ワールド空間の設定になっていません。VRChat のワールドでは表示も操作もできません。",
                    "Canvas の Render Mode を World Space にしてください。");
            }

            if (canvasObject.layer != DefaultLayer)
            {
                Add(issues, CheckSeverity.Error, canvasObject,
                    "Canvas のレイヤーが「" + LayerMask.LayerToName(canvasObject.layer) + "」です。UI レイヤーの Canvas は、VRChat のメニューを開いている間しか操作できません。",
                    "Canvas のレイヤーを Default にしてください。",
                    () => FixLayer(canvasObject));
            }

            if (!isVRChatCanvas)
            {
                Add(issues, CheckSeverity.Error, canvasObject,
                    "Canvas に VRC Ui Shape がありません。VRChat で操作できません。",
                    "Canvas に VRC Ui Shape を追加してください。",
                    () => Undo.AddComponent<VRCUiShape>(canvasObject));
            }

            CheckCollider(canvas, issues);

            float scale = canvas.transform.lossyScale.x;
            if (scale < MinCanvasScale || scale > MaxCanvasScale)
            {
                RectTransform rect = (RectTransform)canvas.transform;
                Add(issues, CheckSeverity.Warning, canvasObject,
                    "Canvas の縮尺が " + scale.ToString("0.#####") + " です。ワールドの中での幅は、約 " + (rect.rect.width * scale).ToString("0.##") + " m になります。",
                    "ツールキットは、縮尺 0.001（Canvas の 1 単位 = 1mm）を想定しています。意図した大きさでなければ、Canvas と、その親の Scale を確かめてください。");
            }
        }

        private static void CheckCollider(Canvas canvas, List<CheckIssue> issues)
        {
            GameObject canvasObject = canvas.gameObject;
            RectTransform rect = (RectTransform)canvas.transform;
            BoxCollider boxCollider = canvas.GetComponent<BoxCollider>();

            if (boxCollider == null)
            {
                // 箱以外の当たり判定を意図して付けている場合は、そのままにする。
                if (canvas.GetComponent<Collider>() != null)
                    return;

                Add(issues, CheckSeverity.Warning, canvasObject,
                    "Canvas に当たり判定（Collider）がありません。VRChat のレーザーやクリックが当たりません。",
                    "Canvas と同じ大きさの Box Collider を追加し、Is Trigger をオンにしてください。",
                    () => FixCollider(canvasObject, rect, Undo.AddComponent<BoxCollider>(canvasObject)));
                return;
            }

            if (!boxCollider.isTrigger)
            {
                Add(issues, CheckSeverity.Warning, canvasObject,
                    "Canvas の当たり判定が、トリガーになっていません。プレイヤーがぶつかって、通り抜けられなくなります。",
                    "Box Collider の Is Trigger をオンにしてください。",
                    () => FixCollider(canvasObject, rect, boxCollider));
            }

            Vector2 size = rect.rect.size;
            if (Mathf.Abs(boxCollider.size.x - size.x) > ColliderSizeTolerance || Mathf.Abs(boxCollider.size.y - size.y) > ColliderSizeTolerance)
            {
                Add(issues, CheckSeverity.Warning, canvasObject,
                    "Canvas の当たり判定の大きさ（" + boxCollider.size.x.ToString("0") + " x " + boxCollider.size.y.ToString("0")
                    + "）が、Canvas の大きさ（" + size.x.ToString("0") + " x " + size.y.ToString("0") + "）と合っていません。操作できない場所や、何も無いのに反応する場所ができます。",
                    "Box Collider の Size の X と Y を、Canvas の Width と Height に合わせてください。",
                    () => FixCollider(canvasObject, rect, boxCollider));
            }
        }

        private static void CheckEvent(ControlBase control, List<CheckIssue> issues)
        {
            SerializedObject serializedControl = new SerializedObject(control);
            UdonBehaviour target = serializedControl.FindProperty("eventTarget").objectReferenceValue as UdonBehaviour;
            string eventName = serializedControl.FindProperty("eventName").stringValue;
            bool hasName = !string.IsNullOrEmpty(eventName);

            if (target == null && !hasName)
            {
                // 値を持つ部品は、通知を使わずに値だけ読む使い方がある。ボタンは、通知が無いと何も起きない。
                if (control is ButtonControl)
                {
                    Add(issues, CheckSeverity.Info, control.gameObject,
                        "ボタンに、通知先が設定されていません。押しても何も起きません。",
                        "Event Target に通知先のオブジェクトを、Event Name に呼び出すメソッドの名前を設定してください。");
                }

                return;
            }

            if (target == null)
            {
                Add(issues, CheckSeverity.Warning, control.gameObject,
                    "イベント名（" + eventName + "）はありますが、通知先が設定されていません。通知は送られません。",
                    "Event Target に、通知先のオブジェクトを設定してください。");
                return;
            }

            if (!hasName)
            {
                Add(issues, CheckSeverity.Warning, control.gameObject,
                    "通知先（" + target.name + "）はありますが、イベント名が設定されていません。通知は送られません。",
                    "Event Name に、通知先で呼び出すメソッドの名前を設定してください。");
            }
            else
            {
                CheckMethod(control, target, eventName, "Event Name", issues);
            }

            if (control is ConfirmDialog)
            {
                string cancelEventName = serializedControl.FindProperty("cancelEventName").stringValue;
                if (!string.IsNullOrEmpty(cancelEventName))
                    CheckMethod(control, target, cancelEventName, "Cancel Event Name", issues);
            }
        }

        // 通知先のスクリプトに、その名前の public なメソッドがあるかを確かめる。名前の打ち間違いは、Unity ではエラーにならない。
        private static void CheckMethod(ControlBase control, UdonBehaviour target, string eventName, string fieldLabel, List<CheckIssue> issues)
        {
            // UdonSharp で書かれていない Udon（グラフなど）は、メソッドを調べられないので対象にしない。
            UdonSharpBehaviour proxy = UdonSharpEditorUtility.GetProxyBehaviour(target);
            if (proxy == null)
                return;

            System.Type type = proxy.GetType();
            bool isPublic = HasMethod(type, eventName, BindingFlags.Public | BindingFlags.Instance);
            if (isPublic)
                return;

            if (HasMethod(type, eventName, BindingFlags.NonPublic | BindingFlags.Instance))
            {
                Add(issues, CheckSeverity.Error, control.gameObject,
                    fieldLabel + " の「" + eventName + "」は、通知先のスクリプト（" + type.Name + "）にありますが、public ではありません。通知を受け取れません。",
                    "スクリプトのメソッドを public にしてください。");
                return;
            }

            Add(issues, CheckSeverity.Error, control.gameObject,
                fieldLabel + " の「" + eventName + "」という名前のメソッドが、通知先のスクリプト（" + type.Name + "）にありません。操作しても何も起きません。",
                "名前の打ち間違いが無いか確かめてください。大文字と小文字も区別されます。");
        }

        private static bool HasMethod(System.Type type, string name, BindingFlags flags)
        {
            foreach (MethodInfo method in type.GetMethods(flags))
            {
                if (method.Name == name)
                    return true;
            }

            return false;
        }

        private static void CheckTabs(TabView tabs, List<CheckIssue> issues)
        {
            SerializedObject serializedTabs = new SerializedObject(tabs);
            SegmentedControl header = serializedTabs.FindProperty("header").objectReferenceValue as SegmentedControl;
            Transform pageContainer = serializedTabs.FindProperty("pageContainer").objectReferenceValue as Transform;
            if (header == null || pageContainer == null)
            {
                Add(issues, CheckSeverity.Error, tabs.gameObject,
                    "タブの見出しか、ページを入れる場所が設定されていません。",
                    "プレハブ（ZS_Tabs）を置き直してください。");
                return;
            }

            int headerCount = header.GetComponentsInChildren<Toggle>(true).Length;
            int pageCount = pageContainer.childCount;
            if (headerCount != pageCount)
            {
                Add(issues, CheckSeverity.Error, tabs.gameObject,
                    "タブの見出しの数（" + headerCount + "）と、ページの数（" + pageCount + "）が合っていません。",
                    "Header の中の選択肢と、Pages の中のページを、同じ数にしてください。並び順が対応します。");
            }
        }

        private static void CheckSegmented(SegmentedControl segmented, List<CheckIssue> issues)
        {
            int onCount = 0;
            foreach (Toggle toggle in segmented.GetComponentsInChildren<Toggle>(true))
            {
                if (toggle.isOn)
                    onCount++;
            }

            if (onCount != 1)
            {
                Add(issues, CheckSeverity.Warning, segmented.gameObject,
                    "選択ボタン群で、最初に選ばれている選択肢が " + onCount + " 個あります。",
                    "選択肢の Toggle の Is On を、1 つだけオンにしてください。");
            }
        }

        // 同期する部品が、正しい中継役とつながっていて、中継役が動ける状態かを調べる。SYS-06
        private static void CheckSync(ControlBase control, List<CheckIssue> issues)
        {
            SerializedProperty relayProperty = new SerializedObject(control).FindProperty("syncRelay");
            SyncRelay relay = relayProperty.objectReferenceValue as SyncRelay;

            // 中継役のオブジェクトを消すと、部品の側には「消えたものへの参照」が残る。
            // 実行時には同期しない部品として扱われ、黙って同期しなくなるので、必ず知らせる。
            if (relay == null && relayProperty.objectReferenceInstanceIDValue != 0)
            {
                Add(issues, CheckSeverity.Error, control.gameObject,
                    "同期の中継役が見つかりません（消された可能性があります）。この部品は同期しません。",
                    "同期させるなら、中継役を作り直してください。同期させないなら、Sync Relay を空にしてください。",
                    () =>
                    {
                        ClearRelay(control);
                        UIBuilder.SetSynced(control, true);
                    });
                return;
            }

            if (relay == null)
                return;

            if (!UIBuilder.IsSyncable(control))
            {
                Add(issues, CheckSeverity.Error, control.gameObject,
                    "同期に対応していない部品（" + control.GetType().Name + "）に、同期の中継役が設定されています。",
                    "この部品の Sync Relay を空にしてください。",
                    () => ClearRelay(control));
                return;
            }

            ControlBase relayControl = new SerializedObject(relay).FindProperty("control").objectReferenceValue as ControlBase;
            if (relayControl != control)
            {
                Add(issues, CheckSeverity.Error, control.gameObject,
                    "同期の中継役（" + relay.name + "）が、別の部品とつながっています。この部品の操作は、ほかの人に正しく届きません。",
                    "この部品の同期を一度無効にしてから、もう一度有効にしてください（Hierarchy の右クリックの ZeroShot Udon UI から）。",
                    () =>
                    {
                        ClearRelay(control);
                        UIBuilder.SetSynced(control, true);
                    });
                return;
            }

            if (!relay.gameObject.activeInHierarchy)
            {
                Add(issues, CheckSeverity.Error, relay.gameObject,
                    "同期の中継役（" + relay.name + "）が無効になっています。VRChat の同期は、無効なオブジェクトでは止まります。",
                    "「" + UIBuilder.SyncRootName + "」と、その中のオブジェクトを有効にしてください。",
                    () => Activate(relay.transform));
            }
        }

        // 部品とつながっていない中継役を探す。部品を消したときや、同期を無効にし損ねたときに残る。
        private static void CheckRelay(SyncRelay relay, List<CheckIssue> issues)
        {
            ControlBase control = new SerializedObject(relay).FindProperty("control").objectReferenceValue as ControlBase;
            SyncRelay controlRelay = control != null
                ? new SerializedObject(control).FindProperty("syncRelay").objectReferenceValue as SyncRelay
                : null;
            if (controlRelay == relay)
                return;

            Add(issues, CheckSeverity.Warning, relay.gameObject,
                "使われていない同期の中継役（" + relay.name + "）があります。通信の負荷が少し増えます。",
                "このオブジェクトを消してください。",
                () => Undo.DestroyObjectImmediate(relay.gameObject));
        }

        private static void ClearRelay(ControlBase control)
        {
            SerializedObject serializedControl = new SerializedObject(control);
            serializedControl.FindProperty("syncRelay").objectReferenceValue = null;
            serializedControl.ApplyModifiedProperties();
        }

        // 対象と、その親をすべて有効にする。
        private static void Activate(Transform target)
        {
            for (Transform current = target; current != null; current = current.parent)
            {
                if (current.gameObject.activeSelf)
                    continue;

                Undo.RecordObject(current.gameObject, UndoName);
                current.gameObject.SetActive(true);
            }
        }

        private static void CheckFont(List<CheckIssue> issues)
        {
            // VRChat は、フォントに無い文字を自前のフォントで補う。ただし、プロジェクト側に補完用のフォントが設定されていると働かない。
            // TMP_Settings.instance は、設定が無いと取り込みの画面を開いてしまうので、先に有無を確かめる。
            TMP_Settings settings = Resources.Load<TMP_Settings>("TMP Settings");
            List<TMP_FontAsset> fallbacks = settings != null ? TMP_Settings.fallbackFontAssets : null;
            if (fallbacks != null && fallbacks.Count > 0)
            {
                Add(issues, CheckSeverity.Warning, settings,
                    "TextMeshPro の設定の「Fallback Font Assets」に、フォントが登録されています。VRChat の文字の補完が働かず、共通フォントに無い文字が表示されないおそれがあります。",
                    "Edit > Project Settings > TextMesh Pro > Settings の Fallback Font Assets を、空にしてください。");
            }

            TMP_FontAsset fontAsset = FontGenerator.LoadFontAsset();
            if (fontAsset == null || fontAsset.HasCharacter('あ'))
                return;

            // 共通フォントに日本語が入っていない。シーンに日本語の文字があれば知らせる。
            TMP_Text firstText = null;
            int count = 0;
            foreach (TMP_Text text in Resources.FindObjectsOfTypeAll<TMP_Text>())
            {
                if (EditorUtility.IsPersistent(text) || text.font != fontAsset || !ContainsJapanese(text.text))
                    continue;

                if (firstText == null)
                    firstText = text;
                count++;
            }

            if (count > 0)
            {
                Add(issues, CheckSeverity.Warning, firstText.gameObject,
                    "日本語を含む文字が " + count + " か所ありますが、共通フォントに日本語が入っていません。Unity 上では □ になります。",
                    "Tools > ZeroShot Udon UI > フォント設定 で、Noto Sans JP をダウンロードして生成してください。");
            }
        }

        // ひらがな、カタカナ、漢字のどれかを含むなら true。
        private static bool ContainsJapanese(string text)
        {
            if (string.IsNullOrEmpty(text))
                return false;

            foreach (char character in text)
            {
                if ((character >= '぀' && character <= 'ヿ') || (character >= '一' && character <= '鿿'))
                    return true;
            }

            return false;
        }

        // target か、その子に、ツールキットのプレハブから置いたものがあれば true。
        private static bool ContainsToolkitPart(GameObject target)
        {
            foreach (Transform child in target.GetComponentsInChildren<Transform>(true))
            {
                string path = PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(child.gameObject);
                if (!string.IsNullOrEmpty(path) && path.StartsWith(UIBuilder.RuntimeFolder))
                    return true;
            }

            return false;
        }

        // Canvas と、その中で UI レイヤーになっているものを、Default レイヤーに直す。
        private static void FixLayer(GameObject canvasObject)
        {
            foreach (Transform child in canvasObject.GetComponentsInChildren<Transform>(true))
            {
                if (child.gameObject != canvasObject && child.gameObject.layer != UILayer)
                    continue;

                Undo.RecordObject(child.gameObject, UndoName);
                child.gameObject.layer = DefaultLayer;
                UIBuilder.RecordOverrides(child.gameObject);
            }
        }

        // 当たり判定をトリガーにし、大きさを Canvas に合わせる。
        private static void FixCollider(GameObject canvasObject, RectTransform rect, BoxCollider boxCollider)
        {
            Undo.RecordObject(boxCollider, UndoName);
            boxCollider.isTrigger = true;
            boxCollider.center = Vector3.zero;
            boxCollider.size = new Vector3(rect.rect.width, rect.rect.height, ColliderDepth);
            UIBuilder.RecordOverrides(boxCollider);
        }

        private static void Add(List<CheckIssue> issues, CheckSeverity severity, Object target, string message, string advice, System.Action fix = null)
        {
            issues.Add(new CheckIssue
            {
                Severity = severity,
                Target = target,
                Message = message,
                Advice = advice,
                FixAction = fix,
            });
        }
    }
}
