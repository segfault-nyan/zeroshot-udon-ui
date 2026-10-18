
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using TMPro;
using UdonSharp;
using UdonSharpEditor;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using VRC.Udon;

// EDT-05
namespace ZeroShotUdonUI.EditorTools
{
    public enum ButtonStyle
    {
        Primary,
        Secondary,
        Outline,
    }

    public enum StackDirection
    {
        Vertical,
        Horizontal,
    }

    /// <summary>
    /// アイコンの種類。並び順はアイコンのシェーダー（Icon.shader）の番号と対応しているので、
    /// 途中に挿入したり順番を入れ替えたりしない。追加するときは末尾に足し、シェーダーにも形を追加する。
    /// </summary>
    public enum IconType
    {
        Close,
        Check,
        Plus,
        Minus,
        ChevronUp,
        ChevronDown,
        ChevronLeft,
        ChevronRight,
        Menu,
        Info,
    }

    public enum LabelStyle
    {
        Heading,
        Body,
        Caption,
    }

    /// <summary>
    /// コードから UI を組み立てるための関数群。ツールキットのプレハブをシーンに配置し、設定を行う。
    /// 最初に EnsureSetup を呼び、true が返ってから各 Create 関数を使う。
    /// 生成した要素は親の中央に置かれるので、Place で位置と大きさを決める。
    /// </summary>
    public static class UIBuilder
    {
        public const string DisplayName = "ZeroShot Udon UI";

        // ツールキット本体の置き場所。VCC で更新すると、この中身は丸ごと置き換わる。
        internal const string PackageFolder = "Packages/com.helkya.zeroshotudonui";
        internal const string RuntimeFolder = PackageFolder + "/Runtime";

        // 利用者ごとのファイル（取得したフォント、生成したデモシーンなど）の置き場所。ツールキットを更新しても消えない。
        internal const string UserFolder = "Assets/ZeroShotUdonUI";

        // プロジェクト内でアセットを探す範囲。パッケージの中も含める。
        private static readonly string[] SearchFolders = { "Assets", "Packages" };

        internal const string ShaderName = "ZeroShotUdonUI/RoundedRect";
        internal const string IconShaderName = "ZeroShotUdonUI/Icon";
        // プレハブ内のオブジェクト名。配置後に名前で探すので、生成側と配置側で共有する。
        internal const string ValueLabelName = "Value Label";
        internal const string CheckboxBoxName = "Box";
        internal const string CheckboxCheckName = "Check";
        internal const string CheckboxLabelName = "Label";
        internal const string StepperDecreaseName = "Decrease Button";
        internal const string StepperIncreaseName = "Increase Button";
        internal const string ScrollViewportName = "Viewport";
        internal const string TabsHeaderName = "Header";
        internal const string TabsPagesName = "Pages";
        internal const string DialogMessageName = "Message";
        internal const string DialogConfirmName = "Confirm Button";
        internal const string DialogCancelName = "Cancel Button";
        private const string TabPageName = "Page";
        internal const string SettingRowLabelName = "Label";
        internal const string SettingRowSlotName = "Control";
        private const string SegmentName = "Segment";
        private const string SegmentLabelName = "Label";
        internal const string IconName = "Icon";
        internal const string WindowCanvasName = "Canvas";
        internal const string WindowTitleName = "Title";
        internal const string ContentName = "Content";

        // 同期の中継役をまとめて置く、シーンの最上位のオブジェクトの名前。
        internal const string SyncRootName = "ZS Sync";

        // 中身の領域と、パネルの縁との間隔。
        internal const float ContentPadding = 40f;

        private const string TmpSettingsPath = "Assets/TextMesh Pro/Resources/TMP Settings.asset";

        /// <summary>
        /// UI の生成に必要なもの（TextMeshPro の基本リソース、U# プログラムアセット、マテリアル、プレハブ）を用意する。
        /// 準備のために取り込みやコンパイルを始めた場合は false を返すので、完了後にもう一度呼ぶ。
        /// </summary>
        /// <param name="extraBehaviours">ツールキットの部品以外に、U# プログラムアセットを用意したい UdonSharpBehaviour の型。</param>
        public static bool EnsureSetup(params System.Type[] extraBehaviours)
        {
            if (!EnsurePrerequisites(extraBehaviours))
                return false;

            if (!PrefabGenerator.GenerateMissing())
            {
                ShowDialog("プレハブの生成に失敗しました。Console のエラーを確認してください。");
                return false;
            }

            return true;
        }

        /// <summary>
        /// VRChat で操作できる設定を済ませた、ワールド空間の Canvas をシーンに置く。
        /// </summary>
        public static GameObject CreateCanvas(string name, Vector2 size)
        {
            GameObject root = Instantiate(PrefabKind.Canvas, null);
            root.name = name;

            RectTransform rect = (RectTransform)root.transform;
            rect.sizeDelta = size;

            // 当たり判定を Canvas の大きさに合わせる。
            BoxCollider boxCollider = root.GetComponent<BoxCollider>();
            boxCollider.size = new Vector3(size.x, size.y, boxCollider.size.z);

            RecordOverrides(root, rect, boxCollider);
            return root;
        }

        /// <summary>
        /// Canvas に背景のパネルと中身の領域をつけたものをシーンに置く。部品は content の中に置く。
        /// content は縦の自動配置なので、置いた部品は上から順に並ぶ。
        /// </summary>
        public static GameObject CreatePanelCanvas(string name, Vector2 size, out RectTransform content)
        {
            GameObject root = CreateCanvas(name, size);
            CreatePanel(root.transform);

            content = CreateStack(root.transform, StackDirection.Vertical);
            content.gameObject.name = ContentName;
            content.anchorMin = Vector2.zero;
            content.anchorMax = Vector2.one;
            content.offsetMin = new Vector2(ContentPadding, ContentPadding);
            content.offsetMax = new Vector2(-ContentPadding, -ContentPadding);
            RecordOverrides(content.gameObject, content);

            return root;
        }

        /// <summary>
        /// タイトルと閉じるボタンのついたウィンドウをシーンに置く。部品は content の中に置く。
        /// content は縦の自動配置なので、置いた部品は上から順に並ぶ。
        /// 戻り値の Open・Close・ToggleOpen を、ボタンなどの通知先に設定すると開閉できる。
        /// </summary>
        public static WindowPanel CreateWindow(string name, string title, Vector2 size, out RectTransform content)
        {
            GameObject root = Instantiate(PrefabKind.Window, null);
            root.name = name;

            Transform canvas = root.transform.Find(WindowCanvasName);
            RectTransform canvasRect = (RectTransform)canvas;
            canvasRect.sizeDelta = size;

            // 当たり判定を Canvas の大きさに合わせる。
            BoxCollider boxCollider = canvas.GetComponent<BoxCollider>();
            boxCollider.size = new Vector3(size.x, size.y, boxCollider.size.z);

            TextMeshProUGUI titleText = canvas.Find(WindowTitleName).GetComponent<TextMeshProUGUI>();
            titleText.text = title;

            RecordOverrides(root, canvasRect, boxCollider, titleText);

            content = (RectTransform)canvas.Find(ContentName);
            return root.GetComponent<WindowPanel>();
        }

        /// <summary>
        /// 確認ダイアログをシーンに置く。メッセージと、「はい」「いいえ」にあたる2つのボタンを持つ。
        /// ワールドの開始時は閉じていて、戻り値の Open で開く。どちらかのボタンを押すと閉じる。
        /// 「はい」が押されたときの通知は SetEvent、「いいえ」のときの通知は SetCancelEvent で設定する。
        /// 独立した Canvas なので、PlaceInWorld などで置く場所を決める。
        /// </summary>
        public static ConfirmDialog CreateDialog(string name, string message, string confirmLabel, string cancelLabel, Vector2 size)
        {
            GameObject root = Instantiate(PrefabKind.Dialog, null);
            root.name = name;

            Transform canvas = root.transform.Find(WindowCanvasName);
            RectTransform canvasRect = (RectTransform)canvas;
            canvasRect.sizeDelta = size;

            // 当たり判定を Canvas の大きさに合わせる。
            BoxCollider boxCollider = canvas.GetComponent<BoxCollider>();
            boxCollider.size = new Vector3(size.x, size.y, boxCollider.size.z);

            TextMeshProUGUI messageText = canvas.Find(DialogMessageName).GetComponent<TextMeshProUGUI>();
            messageText.text = message;

            TextMeshProUGUI confirmText = canvas.Find(DialogConfirmName).GetComponentInChildren<TextMeshProUGUI>(true);
            confirmText.text = confirmLabel;

            TextMeshProUGUI cancelText = canvas.Find(DialogCancelName).GetComponentInChildren<TextMeshProUGUI>(true);
            cancelText.text = cancelLabel;

            RecordOverrides(root, canvasRect, boxCollider, messageText, confirmText, cancelText);
            return root.GetComponent<ConfirmDialog>();
        }

        /// <summary>
        /// 確認ダイアログで「いいえ」が押されたときに、通知先で呼び出すイベント名を設定する。
        /// 通知先は、SetEvent で設定したものと共通。
        /// </summary>
        public static void SetCancelEvent(ConfirmDialog dialog, string eventName)
        {
            SerializedObject serializedDialog = new SerializedObject(dialog);
            serializedDialog.FindProperty("cancelEventName").stringValue = eventName;
            serializedDialog.ApplyModifiedPropertiesWithoutUndo();
        }

        /// <summary>
        /// スクロール領域を置く。部品は content の中に置く。
        /// content は縦の自動配置で、置いた部品の分だけ下に伸びる。枠からはみ出した分は、スクロールして見る。
        /// </summary>
        public static RectTransform CreateScrollArea(Transform parent, out RectTransform content)
        {
            GameObject instance = Instantiate(PrefabKind.ScrollArea, parent);
            content = instance.GetComponent<ScrollRect>().content;
            return (RectTransform)instance.transform;
        }

        /// <summary>
        /// タブを置く。titles は左から順の見出しで、2つ以上必要。見出しと同じ数のページが作られ、pages に入る。
        /// 各ページは縦の自動配置なので、置いた部品は上から順に並ぶ。
        /// </summary>
        public static TabView CreateTabs(Transform parent, string[] titles, int selectedIndex, out RectTransform[] pages)
        {
            if (titles == null || titles.Length < 2)
                throw new System.ArgumentException("見出しは2つ以上必要です。", nameof(titles));
            if (selectedIndex < 0 || selectedIndex >= titles.Length)
                throw new System.ArgumentOutOfRangeException(nameof(selectedIndex));

            GameObject instance = Instantiate(PrefabKind.Tabs, parent);
            ConfigureSegmented(instance.transform.Find(TabsHeaderName).gameObject, titles, selectedIndex);

            // プレハブのページは2つ。足りない分は、新しい縦の自動配置を足す。
            Transform container = instance.transform.Find(TabsPagesName);
            for (int i = container.childCount; i < titles.Length; i++)
            {
                GameObject added = Instantiate(PrefabKind.StackVertical, container);
                added.name = GetTabPageName(i);

                RectTransform addedRect = (RectTransform)added.transform;
                addedRect.anchorMin = Vector2.zero;
                addedRect.anchorMax = Vector2.one;
                addedRect.offsetMin = Vector2.zero;
                addedRect.offsetMax = Vector2.zero;
                RecordOverrides(added, addedRect);
            }

            // 最初に見せるページだけを表示しておく。
            pages = new RectTransform[titles.Length];
            for (int i = 0; i < pages.Length; i++)
            {
                GameObject page = container.GetChild(i).gameObject;
                page.SetActive(i == selectedIndex);
                RecordOverrides(page);
                pages[i] = (RectTransform)page.transform;
            }

            return instance.GetComponent<TabView>();
        }

        // タブのページのオブジェクト名。index は左からの順番（0 始まり）。
        internal static string GetTabPageName(int index)
        {
            return TabPageName + " " + (index + 1);
        }

        /// <summary>
        /// target が中身の領域を持つ容れ物（ウィンドウ、パネルつきの Canvas、スクロール領域、タブ）なら、その中身の領域を返す。
        /// 無ければ null を返す。
        /// </summary>
        internal static Transform FindContent(GameObject target)
        {
            if (target.GetComponent<WindowPanel>() != null)
                return target.transform.Find(WindowCanvasName + "/" + ContentName);

            if (target.GetComponent<Canvas>() != null)
                return target.transform.Find(ContentName);

            ScrollRect scrollRect = target.GetComponent<ScrollRect>();
            if (scrollRect != null)
                return scrollRect.content;

            // タブは、表示しているページを中身の領域として扱う。
            if (target.GetComponent<TabView>() != null)
            {
                foreach (Transform page in target.transform.Find(TabsPagesName))
                {
                    if (page.gameObject.activeSelf)
                        return page;
                }
            }

            return null;
        }

        /// <summary>
        /// Canvas やウィンドウをスポーン地点の正面、目線の高さに置く。
        /// </summary>
        public static void PlaceInFrontOfSpawn(GameObject canvasRoot)
        {
            GetPoseInFrontOfSpawn(out Vector3 position, out Quaternion rotation);
            PlaceInWorld(canvasRoot, position, rotation);
        }

        /// <summary>
        /// スポーン地点の正面、目線の高さの位置と、そこでスポーン地点から見て正面を向く向きを返す。
        /// </summary>
        public static void GetPoseInFrontOfSpawn(out Vector3 position, out Quaternion rotation)
        {
            position = new Vector3(0f, 1.3f, 1.5f);
            rotation = Quaternion.identity;

            VRC.SDKBase.VRC_SceneDescriptor descriptor = Object.FindFirstObjectByType<VRC.SDKBase.VRC_SceneDescriptor>();
            if (descriptor == null)
                return;

            Transform spawn = descriptor.spawns != null && descriptor.spawns.Length > 0 && descriptor.spawns[0] != null
                ? descriptor.spawns[0]
                : descriptor.transform;
            position = spawn.position + spawn.forward * 1.5f + Vector3.up * 1.3f;
            rotation = Quaternion.LookRotation(Vector3.ProjectOnPlane(spawn.forward, Vector3.up), Vector3.up);
        }

        /// <summary>
        /// Canvas やウィンドウを、ワールド内の指定した位置と向きに置く。
        /// </summary>
        public static void PlaceInWorld(GameObject canvasRoot, Vector3 position, Quaternion rotation)
        {
            canvasRoot.transform.SetPositionAndRotation(position, rotation);
            RecordOverrides(canvasRoot.transform);
        }

        /// <summary>
        /// 親いっぱいに広がる背景の板を置く。
        /// </summary>
        public static Image CreatePanel(Transform parent)
        {
            return Instantiate(PrefabKind.Panel, parent).GetComponent<Image>();
        }

        /// <summary>
        /// 子要素を等間隔で並べる枠を置く。
        /// 縦の枠は子を上から並べる。ボタンやトグルは幅を保って左に寄せ、設定行やスライダーなどは枠の幅いっぱいに広げる。
        /// 横の枠は子の大きさを変えずに左から並べる。
        /// </summary>
        public static RectTransform CreateStack(Transform parent, StackDirection direction)
        {
            PrefabKind kind = direction == StackDirection.Vertical ? PrefabKind.StackVertical : PrefabKind.StackHorizontal;
            return (RectTransform)Instantiate(kind, parent).transform;
        }

        /// <summary>
        /// 左に項目名、右に操作部品を置く設定行を置く。control には、作成済みの部品を渡す。
        /// null を渡すと項目名だけの行になり、後から AddToSettingRow で部品を置ける。
        /// </summary>
        public static RectTransform CreateSettingRow(Transform parent, string label, Component control)
        {
            GameObject instance = Instantiate(PrefabKind.SettingRow, parent);
            RectTransform row = (RectTransform)instance.transform;

            TextMeshProUGUI text = row.Find(SettingRowLabelName).GetComponent<TextMeshProUGUI>();
            text.text = label;
            RecordOverrides(text);

            if (control != null)
                AddToSettingRow(row, control);

            return row;
        }

        /// <summary>
        /// 作成済みの部品を、設定行の右端に寄せて置く。
        /// </summary>
        public static void AddToSettingRow(RectTransform row, Component control)
        {
            RectTransform controlRect = (RectTransform)control.transform;
            controlRect.SetParent(row.Find(SettingRowSlotName), false);
            controlRect.anchorMin = new Vector2(1f, 0.5f);
            controlRect.anchorMax = new Vector2(1f, 0.5f);
            controlRect.pivot = new Vector2(1f, 0.5f);
            controlRect.anchoredPosition = Vector2.zero;
            RecordOverrides(controlRect);
        }

        /// <summary>
        /// target が設定行か、設定行に含まれるオブジェクトなら、その設定行を返す。
        /// </summary>
        internal static bool TryGetSettingRow(GameObject target, out RectTransform row)
        {
            row = null;

            GameObject instanceRoot = PrefabUtility.GetNearestPrefabInstanceRoot(target);
            if (instanceRoot == null)
                return false;

            GameObject source = PrefabUtility.GetCorrespondingObjectFromSource(instanceRoot);
            if (source == null || source != PrefabGenerator.Load(PrefabKind.SettingRow))
                return false;

            row = (RectTransform)instanceRoot.transform;
            return true;
        }

        public static TextMeshProUGUI CreateLabel(Transform parent, string text, LabelStyle style)
        {
            PrefabKind kind;
            switch (style)
            {
                case LabelStyle.Heading: kind = PrefabKind.LabelHeading; break;
                case LabelStyle.Caption: kind = PrefabKind.LabelCaption; break;
                default: kind = PrefabKind.LabelBody; break;
            }

            TextMeshProUGUI label = Instantiate(kind, parent).GetComponent<TextMeshProUGUI>();
            label.text = text;
            RecordOverrides(label);
            return label;
        }

        public static ButtonControl CreateButton(Transform parent, string label, ButtonStyle style)
        {
            PrefabKind kind;
            switch (style)
            {
                case ButtonStyle.Primary: kind = PrefabKind.ButtonPrimary; break;
                case ButtonStyle.Outline: kind = PrefabKind.ButtonOutline; break;
                default: kind = PrefabKind.ButtonSecondary; break;
            }

            GameObject instance = Instantiate(kind, parent);

            TextMeshProUGUI text = instance.GetComponentInChildren<TextMeshProUGUI>(true);
            text.text = label;
            RecordOverrides(text);

            return instance.GetComponent<ButtonControl>();
        }

        /// <summary>
        /// アイコンだけの丸いボタンを置く。
        /// </summary>
        public static ButtonControl CreateIconButton(Transform parent, IconType icon)
        {
            GameObject instance = Instantiate(PrefabKind.ButtonIcon, parent);
            SetIcon(instance.transform.Find(IconName).GetComponent<RawImage>(), icon);
            return instance.GetComponent<ButtonControl>();
        }

        /// <summary>
        /// アイコンを置く。色は、返された RawImage の color で変えられる。
        /// </summary>
        public static RawImage CreateIcon(Transform parent, IconType icon)
        {
            RawImage image = Instantiate(PrefabKind.Icon, parent).GetComponent<RawImage>();
            SetIcon(image, icon);
            return image;
        }

        /// <summary>
        /// アイコンの種類を変える。
        /// </summary>
        public static void SetIcon(RawImage image, IconType icon)
        {
            // 種類は UV の X の開始位置（番号 × 2）でシェーダーに伝える。種類ごとにマテリアルを分けずに済む。
            image.uvRect = new Rect((int)icon * 2f, 0f, 1f, 1f);
            RecordOverrides(image);
        }

        public static ToggleSwitch CreateToggle(Transform parent, bool isOn)
        {
            GameObject instance = Instantiate(PrefabKind.ToggleSwitch, parent);
            ToggleSwitch control = instance.GetComponent<ToggleSwitch>();

            // プレハブはオフの状態で作られている。オンで始める場合は、再生前の見た目も合わせておく。
            if (isOn)
            {
                Toggle toggle = instance.GetComponent<Toggle>();
                Image track = instance.GetComponent<Image>();
                RectTransform knob = (RectTransform)instance.transform.Find("Knob");
                SerializedObject serializedControl = new SerializedObject(control);

                toggle.SetIsOnWithoutNotify(true);
                track.material = (Material)serializedControl.FindProperty("onMaterial").objectReferenceValue;
                knob.anchoredPosition = new Vector2(serializedControl.FindProperty("knobOffset").floatValue, 0f);
                RecordOverrides(toggle, track, knob);
            }

            return control;
        }

        /// <summary>
        /// スライダーを置く。右端に現在値の表示がつく。
        /// </summary>
        public static SliderControl CreateSlider(Transform parent, float minValue, float maxValue, float value)
        {
            GameObject instance = Instantiate(PrefabKind.Slider, parent);
            SliderControl control = instance.GetComponent<SliderControl>();

            // 最小値が最大値を一時的に上回らない順番で設定する。
            Slider slider = instance.GetComponent<Slider>();
            if (maxValue >= slider.minValue)
            {
                slider.maxValue = maxValue;
                slider.minValue = minValue;
            }
            else
            {
                slider.minValue = minValue;
                slider.maxValue = maxValue;
            }
            slider.SetValueWithoutNotify(value);

            // 再生前のエディタ上でも現在値が見えるようにしておく。再生後は SliderControl が更新する。
            TextMeshProUGUI valueLabel = instance.transform.Find(ValueLabelName).GetComponent<TextMeshProUGUI>();
            string valueFormat = new SerializedObject(control).FindProperty("valueFormat").stringValue;
            valueLabel.text = slider.value.ToString(valueFormat, CultureInfo.InvariantCulture);

            RecordOverrides(slider, valueLabel);
            return control;
        }

        /// <summary>
        /// チェックボックスを置く。枠の右に項目名がつく。
        /// </summary>
        public static CheckboxControl CreateCheckbox(Transform parent, string label, bool isOn)
        {
            GameObject instance = Instantiate(PrefabKind.Checkbox, parent);
            CheckboxControl control = instance.GetComponent<CheckboxControl>();

            TextMeshProUGUI text = instance.transform.Find(CheckboxLabelName).GetComponent<TextMeshProUGUI>();
            text.text = label;
            RecordOverrides(text);

            // プレハブはオフの状態で作られている。オンで始める場合は、再生前の見た目も合わせておく。
            if (isOn)
            {
                Toggle toggle = instance.GetComponent<Toggle>();
                Image box = instance.transform.Find(CheckboxBoxName).GetComponent<Image>();
                GameObject check = box.transform.Find(CheckboxCheckName).gameObject;

                toggle.SetIsOnWithoutNotify(true);
                box.material = (Material)new SerializedObject(control).FindProperty("onMaterial").objectReferenceValue;
                check.SetActive(true);
                RecordOverrides(toggle, box, check);
            }

            return control;
        }

        /// <summary>
        /// 数値ステッパーを置く。＋と－のボタンで、値を step ずつ増減する。
        /// 値は、minValue から数えて step の倍数に丸められる。step に小数を指定すると、表示も小数になる。
        /// </summary>
        public static StepperControl CreateStepper(Transform parent, float minValue, float maxValue, float step, float value)
        {
            if (maxValue < minValue)
                throw new System.ArgumentException("最大値は、最小値以上にしてください。", nameof(maxValue));
            if (step <= 0f)
                throw new System.ArgumentException("増減の幅は、0 より大きくしてください。", nameof(step));

            GameObject instance = Instantiate(PrefabKind.Stepper, parent);
            StepperControl control = instance.GetComponent<StepperControl>();

            SerializedObject serializedControl = new SerializedObject(control);
            serializedControl.FindProperty("minValue").floatValue = minValue;
            serializedControl.FindProperty("maxValue").floatValue = maxValue;
            serializedControl.FindProperty("step").floatValue = step;
            serializedControl.FindProperty("currentValue").floatValue = value;
            serializedControl.FindProperty("valueFormat").stringValue = GetStepperFormat(step);
            serializedControl.ApplyModifiedPropertiesWithoutUndo();

            RefreshStepper(control);
            return control;
        }

        /// <summary>
        /// 再生前のエディタ上でも、ステッパーの現在値の表示とボタンの状態が設定値と合うようにする。再生後は StepperControl が更新する。
        /// </summary>
        internal static void RefreshStepper(StepperControl control)
        {
            SerializedObject serializedControl = new SerializedObject(control);
            float minValue = serializedControl.FindProperty("minValue").floatValue;
            float maxValue = serializedControl.FindProperty("maxValue").floatValue;
            float step = serializedControl.FindProperty("step").floatValue;
            float value = SnapStepperValue(serializedControl.FindProperty("currentValue").floatValue, minValue, maxValue, step);

            serializedControl.FindProperty("currentValue").floatValue = value;
            serializedControl.ApplyModifiedPropertiesWithoutUndo();

            TextMeshProUGUI valueLabel = (TextMeshProUGUI)serializedControl.FindProperty("valueLabel").objectReferenceValue;
            Button decreaseButton = (Button)serializedControl.FindProperty("decreaseButton").objectReferenceValue;
            Button increaseButton = (Button)serializedControl.FindProperty("increaseButton").objectReferenceValue;

            valueLabel.text = value.ToString(serializedControl.FindProperty("valueFormat").stringValue, CultureInfo.InvariantCulture);
            decreaseButton.interactable = SnapStepperValue(value - step, minValue, maxValue, step) != value;
            increaseButton.interactable = SnapStepperValue(value + step, minValue, maxValue, step) != value;
            RecordOverrides(valueLabel, decreaseButton, increaseButton);
        }

        // StepperControl の丸め方と同じ計算。変えるときは両方を直す。
        private static float SnapStepperValue(float target, float minValue, float maxValue, float step)
        {
            target = Mathf.Clamp(target, minValue, maxValue);
            if (step <= 0f)
                return target;

            float snapped = minValue + Mathf.Round((target - minValue) / step) * step;
            if (snapped > maxValue + step * 0.001f)
                snapped -= step;

            return Mathf.Min(snapped, maxValue);
        }

        // 増減の幅を表せる最小の桁数で表示する。1 なら "0"、0.5 なら "0.0"、0.25 なら "0.00"。
        private static string GetStepperFormat(float step)
        {
            const int MaxDecimals = 3;

            string format = "0";
            float scaled = step;
            for (int decimals = 0; decimals < MaxDecimals; decimals++)
            {
                if (Mathf.Abs(scaled - Mathf.Round(scaled)) < 0.0001f)
                    break;

                format += decimals == 0 ? ".0" : "0";
                scaled *= 10f;
            }

            return format;
        }

        /// <summary>
        /// 進捗バーを置く。value は 0 から 1。右端に現在値の表示がつく。操作はできない。
        /// </summary>
        public static ProgressBar CreateProgressBar(Transform parent, float value)
        {
            GameObject instance = Instantiate(PrefabKind.ProgressBar, parent);
            ProgressBar control = instance.GetComponent<ProgressBar>();

            Slider slider = instance.GetComponent<Slider>();
            slider.SetValueWithoutNotify(value);

            // 再生前のエディタ上でも現在値が見えるようにしておく。再生後は ProgressBar が更新する。
            TextMeshProUGUI valueLabel = instance.transform.Find(ValueLabelName).GetComponent<TextMeshProUGUI>();
            string valueFormat = new SerializedObject(control).FindProperty("valueFormat").stringValue;
            valueLabel.text = slider.value.ToString(valueFormat, CultureInfo.InvariantCulture);

            RecordOverrides(slider, valueLabel);
            return control;
        }

        // 選択肢のオブジェクト名。index は左からの順番（0 始まり）。
        // プレハブの中に同じ名前のオブジェクトが複数あると、プレハブを作り直したときに配置済みの UI との対応が崩れる。
        // 親が違っていても崩れるので、選択肢にも、その中の文字にも連番を付ける。
        internal static string GetSegmentName(int index)
        {
            return SegmentName + " " + (index + 1);
        }

        internal static string GetSegmentLabelName(int index)
        {
            return SegmentLabelName + " " + (index + 1);
        }

        /// <summary>
        /// 選択ボタン群を置く。options は左から順の選択肢の名前で、2つ以上必要。
        /// </summary>
        public static SegmentedControl CreateSegmented(Transform parent, string[] options, int selectedIndex)
        {
            if (options == null || options.Length < 2)
                throw new System.ArgumentException("選択肢は2つ以上必要です。", nameof(options));
            if (selectedIndex < 0 || selectedIndex >= options.Length)
                throw new System.ArgumentOutOfRangeException(nameof(selectedIndex));

            GameObject instance = Instantiate(PrefabKind.SegmentedControl, parent);
            ConfigureSegmented(instance, options, selectedIndex);
            return instance.GetComponent<SegmentedControl>();
        }

        /// <summary>
        /// ドロップダウンを置く。options は上から順の選択肢の名前で、1つ以上必要。
        /// スクリプトから読み書きできるのは、選ばれている番号だけ。選択肢の文字は読めない。
        /// </summary>
        public static DropdownControl CreateDropdown(Transform parent, string[] options, int selectedIndex)
        {
            if (options == null || options.Length < 1)
                throw new System.ArgumentException("選択肢は1つ以上必要です。", nameof(options));
            if (selectedIndex < 0 || selectedIndex >= options.Length)
                throw new System.ArgumentOutOfRangeException(nameof(selectedIndex));

            GameObject instance = Instantiate(PrefabKind.Dropdown, parent);

            TMP_Dropdown dropdown = instance.GetComponent<TMP_Dropdown>();
            dropdown.ClearOptions();
            dropdown.AddOptions(new List<string>(options));
            dropdown.SetValueWithoutNotify(selectedIndex);

            // 選ばれている項目の名前の表示は、TMP_Dropdown が書き換えている。
            RecordOverrides(dropdown, dropdown.captionText);
            return instance.GetComponent<DropdownControl>();
        }

        // 配置済みの選択ボタン群に、選択肢の数・名前・最初の選択を設定する。
        private static void ConfigureSegmented(GameObject instance, string[] options, int selectedIndex)
        {
            SegmentedControl control = instance.GetComponent<SegmentedControl>();

            // プレハブの選択肢は2つ。足りない分は、選ばれていない末尾の選択肢を複製して増やす。
            Toggle[] toggles = instance.GetComponentsInChildren<Toggle>(true);
            for (int i = toggles.Length; i < options.Length; i++)
            {
                Toggle added = Object.Instantiate(toggles[toggles.Length - 1], instance.transform);
                added.gameObject.name = GetSegmentName(i);
                added.GetComponentInChildren<TextMeshProUGUI>(true).gameObject.name = GetSegmentLabelName(i);
            }
            toggles = instance.GetComponentsInChildren<Toggle>(true);

            SerializedObject serializedControl = new SerializedObject(control);
            Material selectedMaterial = (Material)serializedControl.FindProperty("selectedMaterial").objectReferenceValue;
            Material unselectedMaterial = (Material)serializedControl.FindProperty("unselectedMaterial").objectReferenceValue;

            // 先に選ぶ側をオンにする。ToggleGroup は全部オフの状態を許さないため。
            toggles[selectedIndex].SetIsOnWithoutNotify(true);

            for (int i = 0; i < toggles.Length; i++)
            {
                Toggle toggle = toggles[i];
                bool isSelected = i == selectedIndex;
                toggle.SetIsOnWithoutNotify(isSelected);

                Image background = (Image)toggle.targetGraphic;
                background.material = isSelected ? selectedMaterial : unselectedMaterial;

                TextMeshProUGUI text = toggle.GetComponentInChildren<TextMeshProUGUI>(true);
                text.text = options[i];

                RecordOverrides(toggle, background, text);
            }
        }

        /// <summary>
        /// 部品が操作されたときの通知先とイベント名を設定する。
        /// </summary>
        public static void SetEvent(ControlBase control, UdonSharpBehaviour target, string eventName)
        {
            UdonBehaviour udonBehaviour = target != null ? UdonSharpEditorUtility.GetBackingUdonBehaviour(target) : null;
            SetEvent(control, udonBehaviour, eventName);
        }

        /// <summary>
        /// 部品が操作されたときの通知先とイベント名を設定する。
        /// </summary>
        public static void SetEvent(ControlBase control, UdonBehaviour target, string eventName)
        {
            SerializedObject serializedControl = new SerializedObject(control);
            serializedControl.FindProperty("eventTarget").objectReferenceValue = target;
            serializedControl.FindProperty("eventName").stringValue = eventName;
            serializedControl.ApplyModifiedPropertiesWithoutUndo();
        }

        /// <summary>
        /// 操作の結果を、インスタンス内の全員に反映するかを設定する（SYS-06）。同期に対応する部品だけで使える（IsSyncable）。
        /// 同期する場合は、中継役（SyncRelay）を作って部品とつなぐ。中継役は、シーンの最上位の「ZS Sync」の下に置く。
        /// 同期しない場合は、中継役を消す。どちらも、取り消し（Ctrl+Z）に登録する。
        /// </summary>
        public static void SetSynced(ControlBase control, bool synced)
        {
            if (!IsSyncable(control))
                throw new System.ArgumentException("'" + control.GetType().Name + "' は同期に対応していません。", nameof(control));

            SerializedObject serializedControl = new SerializedObject(control);
            SerializedProperty relayProperty = serializedControl.FindProperty("syncRelay");
            SyncRelay current = relayProperty.objectReferenceValue as SyncRelay;

            if (!synced)
            {
                if (current == null)
                    return;

                relayProperty.objectReferenceValue = null;
                serializedControl.ApplyModifiedProperties();
                Undo.DestroyObjectImmediate(current.gameObject);
                return;
            }

            if (current != null)
                return;

            GameObject relayObject = new GameObject(control.gameObject.name + " Sync");
            relayObject.transform.SetParent(GetSyncRoot(control.gameObject.scene).transform, false);
            Undo.RegisterCreatedObjectUndo(relayObject, "Sync " + control.gameObject.name);

            SyncRelay relay = relayObject.AddUdonSharpComponent<SyncRelay>();
            SerializedObject serializedRelay = new SerializedObject(relay);
            serializedRelay.FindProperty("control").objectReferenceValue = control;
            serializedRelay.ApplyModifiedPropertiesWithoutUndo();

            // 同期の設定は、スクリプトの指定（Manual）に合わせて UdonSharp が直すが、すぐに使えるよう、ここでも合わせておく。
            UdonBehaviour backing = UdonSharpEditorUtility.GetBackingUdonBehaviour(relay);
            backing.SyncMethod = VRC.SDKBase.Networking.SyncType.Manual;
            backing.hideFlags |= HideFlags.HideInInspector;

            relayProperty.objectReferenceValue = relay;
            serializedControl.ApplyModifiedProperties();
        }

        // 同期の中継役をまとめて置く、シーンの最上位のオブジェクトを返す。無ければ作る。
        // ここを無効にすると、同期が止まる。
        private static GameObject GetSyncRoot(UnityEngine.SceneManagement.Scene scene)
        {
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                if (root.name == SyncRootName)
                    return root;
            }

            GameObject created = new GameObject(SyncRootName);
            UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(created, scene);
            Undo.RegisterCreatedObjectUndo(created, "Create " + SyncRootName);
            return created;
        }

        /// <summary>
        /// 同期に対応する部品なら true。
        /// </summary>
        public static bool IsSyncable(ControlBase control)
        {
            return control is ToggleSwitch
                || control is CheckboxControl
                || control is SliderControl
                || control is StepperControl
                || control is SegmentedControl
                || control is DropdownControl
                || control is ButtonControl;
        }

        /// <summary>
        /// 要素の位置を、親の中央からのずれで指定する。
        /// </summary>
        public static void Place(Component element, Vector2 position)
        {
            RectTransform rect = (RectTransform)element.transform;
            rect.anchoredPosition = position;
            RecordOverrides(rect);
        }

        /// <summary>
        /// 要素の位置（親の中央からのずれ）と大きさを指定する。
        /// </summary>
        public static void Place(Component element, Vector2 position, Vector2 size)
        {
            RectTransform rect = (RectTransform)element.transform;
            rect.anchoredPosition = position;
            RecordOverrides(rect);
            SetSize(element, size);
        }

        /// <summary>
        /// 要素の大きさを指定する。自動配置の枠に入れた要素は位置が自動で決まるので、こちらを使う。
        /// 縦の自動配置の中で幅いっぱいに広がる要素（設定行、スライダーなど）は、幅の指定が効かず、高さだけが反映される。
        /// </summary>
        public static void SetSize(Component element, Vector2 size)
        {
            RectTransform rect = (RectTransform)element.transform;
            rect.sizeDelta = size;
            RecordOverrides(rect);

            // 縦の自動配置は、幅を保つ要素の幅を LayoutElement から読むので、そちらも合わせる。
            LayoutElement layoutElement = rect.GetComponent<LayoutElement>();
            if (layoutElement != null && layoutElement.preferredWidth >= 0f)
            {
                layoutElement.preferredWidth = size.x;
                RecordOverrides(layoutElement);
            }
        }

        /// <summary>
        /// プレハブの生成と配置の両方に必要なもの（TextMeshPro の基本リソース、シェーダー、U# プログラムアセット、マテリアル）を用意する。
        /// </summary>
        internal static bool EnsurePrerequisites(params System.Type[] extraBehaviours)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                ShowDialog("再生中は実行できません。再生を止めてから実行してください。");
                return false;
            }

            if (!File.Exists(TmpSettingsPath))
            {
                ShowDialog("TextMeshPro の基本リソースが未導入のため、取り込みを開始します。\n取り込みが終わったら、もう一度実行してください。");
                TMP_PackageResourceImporter.ImportResources(true, false, false);
                return false;
            }

            foreach (string shaderName in new[] { ShaderName, IconShaderName })
            {
                if (Shader.Find(shaderName) != null)
                    continue;

                ShowDialog("シェーダー '" + shaderName + "' が見つかりません。Console にシェーダーのエラーが出ていないか確認してください。");
                return false;
            }

            List<System.Type> behaviours = new List<System.Type>
            {
                typeof(ButtonControl),
                typeof(ToggleSwitch),
                typeof(SliderControl),
                typeof(SegmentedControl),
                typeof(WindowPanel),
                typeof(CheckboxControl),
                typeof(StepperControl),
                typeof(ProgressBar),
                typeof(TabView),
                typeof(ConfirmDialog),
                typeof(DropdownControl),
                typeof(SyncRelay),
            };
            behaviours.AddRange(extraBehaviours);

            if (!EnsureProgramAssets(behaviours))
                return false;

            PrefabGenerator.EnsureMaterials();
            FontGenerator.EnsureUsable();
            return true;
        }

        /// <summary>
        /// U# プログラムアセットが無い型があれば作成する。作成した場合は U# のコンパイルを待つ必要があるため false を返す。
        /// </summary>
        private static bool EnsureProgramAssets(List<System.Type> behaviours)
        {
            bool created = false;

            foreach (System.Type behaviour in behaviours)
            {
                if (UdonSharpEditorUtility.GetUdonSharpProgramAsset(behaviour) != null)
                    continue;

                string scriptPath = FindScriptPath(behaviour);
                if (scriptPath == null)
                {
                    ShowDialog("'" + behaviour.Name + "' のスクリプトがプロジェクト内に見つかりません。");
                    return false;
                }

                string assetPath = Path.ChangeExtension(scriptPath, ".asset");
                if (AssetDatabase.LoadAssetAtPath<UdonSharpProgramAsset>(assetPath) == null)
                {
                    UdonSharpProgramAsset programAsset = ScriptableObject.CreateInstance<UdonSharpProgramAsset>();
                    programAsset.sourceCsScript = AssetDatabase.LoadAssetAtPath<MonoScript>(scriptPath);
                    PrefabGenerator.CreateAsset(programAsset, assetPath);
                }

                created = true;
            }

            if (!created)
                return true;

            AssetDatabase.Refresh();
            UdonSharpProgramAsset.CompileAllCsPrograms(true);

            ShowDialog("初回準備として U# プログラムアセットを作成しました。\nコンパイルが終わったら、もう一度実行してください。");
            return false;
        }

        private static string FindScriptPath(System.Type behaviour)
        {
            foreach (string guid in AssetDatabase.FindAssets(behaviour.Name + " t:MonoScript", SearchFolders))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                MonoScript script = AssetDatabase.LoadAssetAtPath<MonoScript>(path);
                if (script != null && script.GetClass() == behaviour)
                    return path;
            }

            return null;
        }

        // プレハブとのつながりを保ったままシーンに置く。ツールキットを更新すると、配置済みの UI にも反映される。
        private static GameObject Instantiate(PrefabKind kind, Transform parent)
        {
            GameObject prefab = PrefabGenerator.Load(kind);
            if (prefab == null)
                throw new System.InvalidOperationException("プレハブ '" + kind + "' がありません。先に UIBuilder.EnsureSetup を呼んでください。");

            if (parent == null)
                return (GameObject)PrefabUtility.InstantiatePrefab(prefab);

            return (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
        }

        // プレハブから置いた要素をコードで直接書き換えた場合は、変更を記録しないとシーンに保存されない。
        internal static void RecordOverrides(params Object[] targets)
        {
            foreach (Object target in targets)
            {
                if (target != null && PrefabUtility.IsPartOfPrefabInstance(target))
                    PrefabUtility.RecordPrefabInstancePropertyModifications(target);
            }
        }

        private static void ShowDialog(string message)
        {
            EditorUtility.DisplayDialog(DisplayName, message, "OK");
        }
    }
}
