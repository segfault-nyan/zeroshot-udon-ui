
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
        internal const string SliderValueLabelName = "Value Label";
        internal const string SettingRowLabelName = "Label";
        internal const string SettingRowSlotName = "Control";
        private const string SegmentName = "Segment";
        internal const string IconName = "Icon";
        internal const string WindowCanvasName = "Canvas";
        internal const string WindowTitleName = "Title";
        internal const string ContentName = "Content";

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
        /// target がウィンドウか、中身の領域を持つ Canvas なら、その中身の領域を返す。無ければ null を返す。
        /// </summary>
        internal static Transform FindContent(GameObject target)
        {
            if (target.GetComponent<WindowPanel>() != null)
                return target.transform.Find(WindowCanvasName + "/" + ContentName);

            if (target.GetComponent<Canvas>() != null)
                return target.transform.Find(ContentName);

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
            TextMeshProUGUI valueLabel = instance.transform.Find(SliderValueLabelName).GetComponent<TextMeshProUGUI>();
            string valueFormat = new SerializedObject(control).FindProperty("valueFormat").stringValue;
            valueLabel.text = slider.value.ToString(valueFormat, CultureInfo.InvariantCulture);

            RecordOverrides(slider, valueLabel);
            return control;
        }

        // 選択肢のオブジェクト名。index は左からの順番（0 始まり）。
        // 同じ親の下に同じ名前が並ぶと、プレハブを作り直したときに配置済みの UI との対応が崩れるので、連番を付ける。
        internal static string GetSegmentName(int index)
        {
            return SegmentName + " " + (index + 1);
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
            SegmentedControl control = instance.GetComponent<SegmentedControl>();

            // プレハブの選択肢は2つ。足りない分は、選ばれていない末尾の選択肢を複製して増やす。
            Toggle[] toggles = instance.GetComponentsInChildren<Toggle>(true);
            for (int i = toggles.Length; i < options.Length; i++)
            {
                Toggle added = Object.Instantiate(toggles[toggles.Length - 1], instance.transform);
                added.gameObject.name = GetSegmentName(i);
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

            return control;
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
