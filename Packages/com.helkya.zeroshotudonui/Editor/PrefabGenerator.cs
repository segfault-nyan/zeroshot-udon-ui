
using System.Globalization;
using System.IO;
using TMPro;
using UdonSharp;
using UdonSharpEditor;
using UnityEditor;
using UnityEditor.Events;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;
using VRC.Udon;

// FND-04・FND-05・FND-08・LAY-01・LAY-03・LAY-04・LAY-05・LAY-06・LAY-07・CMP-01・CMP-03・CMP-04・CMP-05・CMP-06・CMP-08・CMP-09・CMP-11
namespace ZeroShotUdonUI.EditorTools
{
    internal enum PrefabKind
    {
        Canvas,
        Panel,
        StackVertical,
        StackHorizontal,
        SettingRow,
        ButtonPrimary,
        ButtonSecondary,
        ButtonOutline,
        ToggleSwitch,
        Slider,
        SegmentedControl,
        Icon,
        LabelHeading,
        LabelBody,
        LabelCaption,
        ProgressBar,
        ScrollArea,

        // 他のプレハブを中に含むものは、含まれる側より後に並べる。この順番で生成するため。
        ButtonIcon,
        Checkbox,
        Stepper,
        Window,
        Tabs,
        Dialog,
    }

    /// <summary>
    /// ツールキットのプレハブと共通マテリアルをコードから生成する。
    /// プレハブの構造を変えたいときは、ここを書き換えてメニューから生成し直す。
    /// </summary>
    internal static class PrefabGenerator
    {
        private const string PrefabFolder = UIBuilder.RuntimeFolder + "/Prefabs";
        private const string MaterialFolder = UIBuilder.RuntimeFolder + "/Materials";

        // Canvas 単位。Canvas のスケールが 0.001 なので 1 単位 = 1mm。
        private const float CanvasScale = 0.001f;
        private const float CanvasColliderDepth = 10f;
        private const float ToggleKnobOffset = 22f;
        private const float SliderTrackHeight = 10f;
        private const float SliderLabelWidth = 80f;
        private const float SliderLabelGap = 10f;
        private const float StackSpacing = 16f;
        private const float SegmentSpacing = 8f;
        private const float CheckboxLabelGap = 12f;
        private const float ScrollbarWidth = 12f;
        private const float ScrollbarGap = 12f;
        private const float TabsHeaderHeight = 48f;

        // マウスのホイールを1目盛り回したときに動く量（Canvas 単位）。
        private const float ScrollSensitivity = 40f;
        private const float OutlineWidth = 2f;
        private const float WindowPadding = UIBuilder.ContentPadding;
        private const float WindowTitleTop = 28f;
        private const float WindowTitleHeight = 48f;
        private const float WindowTitleGap = 24f;

        private static readonly Vector2 CanvasSize = new Vector2(600f, 400f);
        private static readonly Vector2 VerticalStackSize = new Vector2(520f, 300f);
        private static readonly Vector2 HorizontalStackSize = new Vector2(520f, 64f);
        private static readonly Vector2 SettingRowSize = new Vector2(520f, 64f);
        private static readonly Vector2 ButtonSize = new Vector2(220f, 64f);
        private static readonly Vector2 ToggleSize = new Vector2(96f, 52f);
        private static readonly Vector2 ToggleKnobSize = new Vector2(40f, 40f);
        private static readonly Vector2 SliderSize = new Vector2(360f, 28f);
        private static readonly Vector2 SegmentedSize = new Vector2(300f, 48f);
        private static readonly Vector2 IconSize = new Vector2(32f, 32f);
        private static readonly Vector2 IconButtonSize = new Vector2(48f, 48f);
        private static readonly Vector2 IconInButtonSize = new Vector2(24f, 24f);
        private static readonly Vector2 CheckboxSize = new Vector2(300f, 40f);
        private static readonly Vector2 CheckboxBoxSize = new Vector2(32f, 32f);
        private static readonly Vector2 CheckboxCheckSize = new Vector2(20f, 20f);
        private static readonly Vector2 StepperSize = new Vector2(200f, 48f);
        private static readonly Vector2 ScrollAreaSize = new Vector2(520f, 300f);
        private static readonly Vector2 TabsSize = new Vector2(520f, 400f);
        private static readonly Vector2 DialogSize = new Vector2(480f, 260f);
        private static readonly Vector2 DialogButtonSize = new Vector2(180f, 56f);

        // マテリアルを新規作成するときの初期値。作成後はマテリアル側が正となり、ここを変えても反映されない。
        private static readonly Color SurfaceColor = new Color(0.09f, 0.10f, 0.13f, 0.96f);
        private static readonly Color SurfaceBorderColor = new Color(1f, 1f, 1f, 0.14f);
        private static readonly Color AccentColor = new Color(0.36f, 0.55f, 1f, 1f);
        private static readonly Color TrackColor = new Color(0.25f, 0.27f, 0.33f, 1f);
        private static readonly Color KnobColor = new Color(0.97f, 0.97f, 0.98f, 1f);

        private static readonly Color TextColor = new Color(0.95f, 0.96f, 0.98f, 1f);
        private static readonly Color SubTextColor = new Color(0.62f, 0.66f, 0.74f, 1f);

        // シェーダー（FND-01）のプロパティ名。
        internal const string ColorProperty = "_Color";
        internal const string RadiusProperty = "_Radius";
        internal const string BorderWidthProperty = "_BorderWidth";
        internal const string BorderColorProperty = "_BorderColor";

        // アイコンのシェーダー（FND-07）のプロパティ名と、線の太さの初期値（短辺に対する割合）。
        private const string ThicknessProperty = "_Thickness";
        private const float IconThickness = 0.09f;

        internal enum MaterialRole
        {
            Surface,
            Accent,
            Track,
            Knob,
            Outline,

            // アイコン用。色は要素ごとに持つので、種類や色ごとにマテリアルを分けない。
            Icon,
        }

        // テーマ設定の対象になる役割。
        internal static readonly MaterialRole[] ThemeRoles =
        {
            MaterialRole.Surface,
            MaterialRole.Accent,
            MaterialRole.Track,
            MaterialRole.Knob,
            MaterialRole.Outline,
        };

        [MenuItem("Tools/ZeroShot Udon UI/プレハブを生成")]
        private static void GenerateFromMenu()
        {
            if (!UIBuilder.EnsurePrerequisites())
                return;

            int existingCount = 0;
            foreach (PrefabKind kind in System.Enum.GetValues(typeof(PrefabKind)))
            {
                if (Load(kind) != null)
                    existingCount++;
            }

            if (existingCount > 0)
            {
                bool overwrite = EditorUtility.DisplayDialog(UIBuilder.DisplayName,
                    existingCount + " 個のプレハブが既にあります。作り直して上書きしますか？\nプレハブに手で加えた変更は失われます。",
                    "上書きする", "キャンセル");
                if (!overwrite)
                    return;
            }

            int generatedCount = 0;
            foreach (PrefabKind kind in System.Enum.GetValues(typeof(PrefabKind)))
            {
                if (Generate(kind))
                    generatedCount++;
            }

            AssetDatabase.SaveAssets();
            EditorUtility.DisplayDialog(UIBuilder.DisplayName,
                generatedCount + " 個のプレハブを生成しました。\n" + PrefabFolder, "OK");
        }

        internal static GameObject Load(PrefabKind kind)
        {
            return AssetDatabase.LoadAssetAtPath<GameObject>(GetPath(kind));
        }

        /// <summary>
        /// まだ無いプレハブだけを生成する。既にあるプレハブは書き換えない。
        /// </summary>
        internal static bool GenerateMissing()
        {
            bool succeeded = true;
            foreach (PrefabKind kind in System.Enum.GetValues(typeof(PrefabKind)))
            {
                if (Load(kind) == null && !Generate(kind))
                    succeeded = false;
            }

            return succeeded;
        }

        /// <summary>
        /// 役割ごとの共通マテリアルのうち、無いものを初期値で作成する。
        /// </summary>
        internal static void EnsureMaterials()
        {
            foreach (MaterialRole role in System.Enum.GetValues(typeof(MaterialRole)))
                GetMaterial(role);
        }

        private static string GetPath(PrefabKind kind)
        {
            switch (kind)
            {
                case PrefabKind.Canvas: return PrefabFolder + "/Foundation/ZS_Canvas.prefab";
                case PrefabKind.Panel: return PrefabFolder + "/Layouts/ZS_Panel.prefab";
                case PrefabKind.StackVertical: return PrefabFolder + "/Layouts/ZS_Stack_Vertical.prefab";
                case PrefabKind.StackHorizontal: return PrefabFolder + "/Layouts/ZS_Stack_Horizontal.prefab";
                case PrefabKind.SettingRow: return PrefabFolder + "/Layouts/ZS_SettingRow.prefab";
                case PrefabKind.ButtonPrimary: return PrefabFolder + "/Components/ZS_Button_Primary.prefab";
                case PrefabKind.ButtonSecondary: return PrefabFolder + "/Components/ZS_Button_Secondary.prefab";
                case PrefabKind.ButtonOutline: return PrefabFolder + "/Components/ZS_Button_Outline.prefab";
                case PrefabKind.ToggleSwitch: return PrefabFolder + "/Components/ZS_ToggleSwitch.prefab";
                case PrefabKind.Slider: return PrefabFolder + "/Components/ZS_Slider.prefab";
                case PrefabKind.SegmentedControl: return PrefabFolder + "/Components/ZS_SegmentedControl.prefab";
                case PrefabKind.Icon: return PrefabFolder + "/Components/ZS_Icon.prefab";
                case PrefabKind.ButtonIcon: return PrefabFolder + "/Components/ZS_Button_Icon.prefab";
                case PrefabKind.LabelHeading: return PrefabFolder + "/Components/ZS_Label_Heading.prefab";
                case PrefabKind.LabelBody: return PrefabFolder + "/Components/ZS_Label_Body.prefab";
                case PrefabKind.LabelCaption: return PrefabFolder + "/Components/ZS_Label_Caption.prefab";
                case PrefabKind.ProgressBar: return PrefabFolder + "/Components/ZS_ProgressBar.prefab";
                case PrefabKind.Checkbox: return PrefabFolder + "/Components/ZS_Checkbox.prefab";
                case PrefabKind.Stepper: return PrefabFolder + "/Components/ZS_Stepper.prefab";
                case PrefabKind.Window: return PrefabFolder + "/Layouts/ZS_Window.prefab";
                case PrefabKind.ScrollArea: return PrefabFolder + "/Layouts/ZS_ScrollArea.prefab";
                case PrefabKind.Tabs: return PrefabFolder + "/Layouts/ZS_Tabs.prefab";
                case PrefabKind.Dialog: return PrefabFolder + "/Layouts/ZS_Dialog.prefab";
                default: throw new System.ArgumentOutOfRangeException(nameof(kind), kind, null);
            }
        }

        private static GameObject Build(PrefabKind kind)
        {
            switch (kind)
            {
                case PrefabKind.Canvas: return BuildCanvas();
                case PrefabKind.Panel: return BuildPanel();
                case PrefabKind.StackVertical: return BuildStack(true);
                case PrefabKind.StackHorizontal: return BuildStack(false);
                case PrefabKind.SettingRow: return BuildSettingRow();
                case PrefabKind.ButtonPrimary: return BuildButton(MaterialRole.Accent, Color.white);
                case PrefabKind.ButtonSecondary: return BuildButton(MaterialRole.Track, TextColor);
                case PrefabKind.ButtonOutline: return BuildButton(MaterialRole.Outline, TextColor);
                case PrefabKind.ToggleSwitch: return BuildToggle();
                case PrefabKind.Slider: return BuildSlider();
                case PrefabKind.SegmentedControl: return BuildSegmented();
                case PrefabKind.Icon: return BuildIcon();
                case PrefabKind.ButtonIcon: return BuildIconButton();
                case PrefabKind.LabelHeading: return BuildLabelPrefab(36f, new Vector2(400f, 48f), TextColor, FontStyles.Bold);
                case PrefabKind.LabelBody: return BuildLabelPrefab(24f, new Vector2(300f, 40f), TextColor, FontStyles.Normal);
                case PrefabKind.LabelCaption: return BuildLabelPrefab(18f, new Vector2(300f, 28f), SubTextColor, FontStyles.Normal);
                case PrefabKind.ProgressBar: return BuildProgressBar();
                case PrefabKind.Checkbox: return BuildCheckbox();
                case PrefabKind.Stepper: return BuildStepper();
                case PrefabKind.Window: return BuildWindow();
                case PrefabKind.ScrollArea: return BuildScrollArea();
                case PrefabKind.Tabs: return BuildTabs();
                case PrefabKind.Dialog: return BuildDialog();
                default: throw new System.ArgumentOutOfRangeException(nameof(kind), kind, null);
            }
        }

        private static bool Generate(PrefabKind kind)
        {
            string path = GetPath(kind);
            EnsureFolder(Path.GetDirectoryName(path).Replace('\\', '/'));

            GameObject source = Build(kind);

            // U# は裏側の UdonBehaviour をインスペクターから隠す処理を遅延実行する。保存には間に合わないので、ここで隠しておく。
            foreach (UdonBehaviour udonBehaviour in source.GetComponentsInChildren<UdonBehaviour>(true))
                udonBehaviour.hideFlags |= HideFlags.HideInInspector;

            PrefabUtility.SaveAsPrefabAsset(source, path, out bool succeeded);

            // U# の遅延処理は生成元のオブジェクトを参照するので、それが済んでから破棄する。
            EditorApplication.delayCall += () =>
            {
                if (source != null)
                    Object.DestroyImmediate(source);
            };

            if (!succeeded)
                Debug.LogError("[" + UIBuilder.DisplayName + "] プレハブの生成に失敗しました: " + path);

            return succeeded;
        }

        internal static void EnsureFolder(string folderPath)
        {
            if (AssetDatabase.IsValidFolder(folderPath))
                return;

            string parentPath = Path.GetDirectoryName(folderPath).Replace('\\', '/');
            EnsureFolder(parentPath);
            AssetDatabase.CreateFolder(parentPath, Path.GetFileName(folderPath));
        }

        /// <summary>
        /// アセットを新しく作って保存する。保存先のフォルダは先に用意しておく。
        /// </summary>
        internal static void CreateAsset(Object asset, string path)
        {
            if (path.StartsWith("Assets/"))
            {
                AssetDatabase.CreateAsset(asset, path);
                return;
            }

            // パッケージの中には直接作れないため、Assets の下に作ってから移す（U# 本体と同じ方法）。
            string temporaryPath = "Assets/ZS_Temporary_" + GUID.Generate() + Path.GetExtension(path);
            AssetDatabase.CreateAsset(asset, temporaryPath);

            string error = AssetDatabase.MoveAsset(temporaryPath, path);
            if (string.IsNullOrEmpty(error))
                return;

            AssetDatabase.DeleteAsset(temporaryPath);
            throw new IOException("アセットを作成できません: " + path + "\n" + error);
        }

        private static GameObject BuildCanvas()
        {
            // VRChat では UI レイヤーの Canvas はメニューを開いている間しか操作できないため、Default レイヤーに置く。
            GameObject root = new GameObject("Canvas", typeof(RectTransform));

            Canvas canvas = root.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            root.AddComponent<CanvasScaler>();
            root.AddComponent<GraphicRaycaster>();
            root.AddComponent<VRC.SDK3.Components.VRCUiShape>();

            RectTransform rect = (RectTransform)root.transform;
            rect.sizeDelta = CanvasSize;
            rect.localScale = Vector3.one * CanvasScale;

            // レーザーやクリックの当たり判定用。プレイヤーの移動を妨げないようトリガーにする。
            BoxCollider boxCollider = root.AddComponent<BoxCollider>();
            boxCollider.isTrigger = true;
            boxCollider.size = new Vector3(CanvasSize.x, CanvasSize.y, CanvasColliderDepth);

            return root;
        }

        private static GameObject BuildPanel()
        {
            RectTransform rect = CreateRect("Panel", null, Vector2.zero);
            Stretch(rect);
            AddImage(rect, GetMaterial(MaterialRole.Surface), true);
            return rect.gameObject;
        }

        /// <summary>
        /// ウィンドウを作る。Canvas・パネル・タイトル・閉じるボタンは、既存のプレハブを中に含める形で組み立てる。
        /// </summary>
        private static GameObject BuildWindow()
        {
            // 開閉するスクリプトは、初期化（Start）が開閉に左右されないよう、常に有効な Canvas の親に置く。
            GameObject root = new GameObject("Window");

            GameObject canvas = InstantiateNested(PrefabKind.Canvas, root.transform);
            canvas.name = UIBuilder.WindowCanvasName;

            InstantiateNested(PrefabKind.Panel, canvas.transform);

            // タイトルは上端の左側。右側は閉じるボタンの分を空ける。
            GameObject title = InstantiateNested(PrefabKind.LabelHeading, canvas.transform);
            title.name = UIBuilder.WindowTitleName;
            RectTransform titleRect = (RectTransform)title.transform;
            titleRect.anchorMin = new Vector2(0f, 1f);
            titleRect.anchorMax = new Vector2(1f, 1f);
            titleRect.pivot = new Vector2(0.5f, 1f);
            titleRect.offsetMin = new Vector2(WindowPadding, -(WindowTitleTop + WindowTitleHeight));
            titleRect.offsetMax = new Vector2(-(WindowTitleTop + WindowTitleHeight + WindowPadding), -WindowTitleTop);
            TextMeshProUGUI titleText = title.GetComponent<TextMeshProUGUI>();
            titleText.text = "Window";

            GameObject closeButton = InstantiateNested(PrefabKind.ButtonIcon, canvas.transform);
            closeButton.name = "Close Button";
            RectTransform closeRect = (RectTransform)closeButton.transform;
            closeRect.anchorMin = new Vector2(1f, 1f);
            closeRect.anchorMax = new Vector2(1f, 1f);
            closeRect.pivot = new Vector2(1f, 1f);
            closeRect.anchoredPosition = new Vector2(-WindowTitleTop, -WindowTitleTop);
            closeRect.sizeDelta = new Vector2(WindowTitleHeight, WindowTitleHeight);
            RawImage closeIcon = closeButton.GetComponentInChildren<RawImage>(true);
            UIBuilder.SetIcon(closeIcon, IconType.Close);

            // 部品を置く領域。タイトルの下から、余白を残して広げる。縦の自動配置なので、置いた部品は上から順に並ぶ。
            GameObject contentObject = InstantiateNested(PrefabKind.StackVertical, canvas.transform);
            contentObject.name = UIBuilder.ContentName;
            RectTransform content = (RectTransform)contentObject.transform;
            Stretch(content);
            content.offsetMin = new Vector2(WindowPadding, WindowPadding);
            content.offsetMax = new Vector2(-WindowPadding, -(WindowTitleTop + WindowTitleHeight + WindowTitleGap));

            WindowPanel window = root.AddUdonSharpComponent<WindowPanel>();
            SerializedObject serializedWindow = new SerializedObject(window);
            serializedWindow.FindProperty("content").objectReferenceValue = canvas;
            serializedWindow.ApplyModifiedPropertiesWithoutUndo();

            UIBuilder.SetEvent(closeButton.GetComponent<ButtonControl>(), window, nameof(WindowPanel.Close));

            // 中に含めたプレハブへの変更は、記録しないとウィンドウのプレハブに保存されない。
            UIBuilder.RecordOverrides(canvas, title, titleRect, titleText, closeButton, closeRect, closeIcon, contentObject, content);

            return root;
        }

        private static GameObject BuildIcon()
        {
            RectTransform rect = CreateRect("Icon", null, IconSize);

            // アイコンの種類を UV で指定するので、UV の範囲を変えられる RawImage を使う。
            RawImage image = rect.gameObject.AddComponent<RawImage>();
            image.material = GetMaterial(MaterialRole.Icon);
            image.color = TextColor;
            image.raycastTarget = false;
            UIBuilder.SetIcon(image, IconType.Info);

            SetLayoutWidth(rect, false);
            return rect.gameObject;
        }

        private static GameObject BuildIconButton()
        {
            // 幅と高さが同じなので、ピル形状のマテリアルで円になる。
            RectTransform rect = CreateRect("Button", null, IconButtonSize);
            Image image = AddImage(rect, GetMaterial(MaterialRole.Track), true);

            Button button = rect.gameObject.AddComponent<Button>();
            SetupSelectable(button, image);

            GameObject icon = InstantiateNested(PrefabKind.Icon, rect);
            icon.name = UIBuilder.IconName;
            RectTransform iconRect = (RectTransform)icon.transform;
            iconRect.sizeDelta = IconInButtonSize;

            ButtonControl control = rect.gameObject.AddUdonSharpComponent<ButtonControl>();
            AddInternalListener(button.onClick, control, nameof(ButtonControl._OnClick));

            SetLayoutWidth(rect, false);

            // 中に含めたプレハブへの変更は、記録しないとこのプレハブに保存されない。
            UIBuilder.RecordOverrides(icon, iconRect);

            return rect.gameObject;
        }

        private static GameObject InstantiateNested(PrefabKind kind, Transform parent)
        {
            return (GameObject)PrefabUtility.InstantiatePrefab(Load(kind), parent);
        }

        private static GameObject BuildStack(bool vertical)
        {
            RectTransform rect = CreateRect("Stack", null, vertical ? VerticalStackSize : HorizontalStackSize);
            AddStackLayout(rect, vertical);

            SetLayoutWidth(rect, true);
            return rect.gameObject;
        }

        // 子を等間隔で並べる設定を付ける。自動配置の枠と、スクロール領域の中身で使う。
        private static void AddStackLayout(RectTransform rect, bool vertical)
        {
            HorizontalOrVerticalLayoutGroup layout;
            if (vertical)
            {
                // 子を上から並べる。幅は子ごとの LayoutElement に従い、固定幅の子は左に寄せ、広がる子は枠の幅いっぱいにする。
                // 高さは子自身の値を使う。
                layout = rect.gameObject.AddComponent<VerticalLayoutGroup>();
                layout.childAlignment = TextAnchor.UpperLeft;
                layout.childControlWidth = true;
                layout.childForceExpandWidth = false;
            }
            else
            {
                // 子の大きさは変えず、左から並べる。
                layout = rect.gameObject.AddComponent<HorizontalLayoutGroup>();
                layout.childAlignment = TextAnchor.MiddleLeft;
                layout.childControlWidth = false;
                layout.childForceExpandWidth = false;
            }

            layout.childControlHeight = false;
            layout.childForceExpandHeight = false;
            layout.spacing = StackSpacing;
        }

        /// <summary>
        /// 縦の自動配置の中に置かれたときの幅の決め方を設定する。
        /// fill が true なら枠の幅いっぱいに広がり、false なら今の幅を保つ。
        /// この設定が無い要素は、縦の自動配置の中で幅が 0 になる。
        /// </summary>
        private static void SetLayoutWidth(RectTransform rect, bool fill)
        {
            LayoutElement layoutElement = rect.gameObject.AddComponent<LayoutElement>();
            if (fill)
                layoutElement.flexibleWidth = 1f;
            else
                layoutElement.preferredWidth = rect.sizeDelta.x;
        }

        private static GameObject BuildLabelPrefab(float fontSize, Vector2 size, Color color, FontStyles fontStyle)
        {
            TextMeshProUGUI label = BuildLabel(null, "Label", fontSize, size, color, fontStyle);
            SetLayoutWidth(label.rectTransform, true);
            return label.gameObject;
        }

        private static GameObject BuildSettingRow()
        {
            RectTransform rect = CreateRect("SettingRow", null, SettingRowSize);

            TextMeshProUGUI label = BuildLabel(rect, "Label", 24f, Vector2.zero, TextColor, FontStyles.Normal);
            label.gameObject.name = UIBuilder.SettingRowLabelName;
            Stretch(label.rectTransform);

            // 操作部品を置く場所。右端の中央を基準点にし、ここに置いた部品が右寄せになるようにする。
            RectTransform slot = CreateRect(UIBuilder.SettingRowSlotName, rect, Vector2.zero);
            slot.anchorMin = new Vector2(1f, 0.5f);
            slot.anchorMax = new Vector2(1f, 0.5f);
            slot.pivot = new Vector2(1f, 0.5f);
            slot.anchoredPosition = Vector2.zero;

            SetLayoutWidth(rect, true);
            return rect.gameObject;
        }

        private static GameObject BuildSegmented()
        {
            Material selectedMaterial = GetMaterial(MaterialRole.Accent);
            Material unselectedMaterial = GetMaterial(MaterialRole.Track);

            RectTransform rect = CreateRect("SegmentedControl", null, SegmentedSize);

            // 選択肢を、枠の幅を等分した大きさで横に並べる。
            HorizontalLayoutGroup layout = rect.gameObject.AddComponent<HorizontalLayoutGroup>();
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = true;
            layout.spacing = SegmentSpacing;

            // 常にどれか1つが選ばれている状態を保つ。
            ToggleGroup group = rect.gameObject.AddComponent<ToggleGroup>();
            group.allowSwitchOff = false;

            SegmentedControl control = rect.gameObject.AddUdonSharpComponent<SegmentedControl>();
            SerializedObject serializedControl = new SerializedObject(control);
            serializedControl.FindProperty("selectedMaterial").objectReferenceValue = selectedMaterial;
            serializedControl.FindProperty("unselectedMaterial").objectReferenceValue = unselectedMaterial;
            serializedControl.ApplyModifiedPropertiesWithoutUndo();

            // 選択肢は2つで作る。3つ以上にするときは、Unity 上で選択肢を複製する。
            for (int i = 0; i < 2; i++)
            {
                bool isSelected = i == 0;

                RectTransform segment = CreateRect(UIBuilder.GetSegmentName(i), rect, Vector2.zero);
                Image background = AddImage(segment, isSelected ? selectedMaterial : unselectedMaterial, true);

                TextMeshProUGUI label = BuildLabel(segment, "Option " + (i + 1), 20f, Vector2.zero, TextColor, FontStyles.Bold);
                label.gameObject.name = UIBuilder.GetSegmentLabelName(i);
                label.alignment = TextAlignmentOptions.Center;
                Stretch(label.rectTransform);

                Toggle toggle = segment.gameObject.AddComponent<Toggle>();
                SetupSelectable(toggle, background);
                toggle.isOn = isSelected;
                toggle.group = group;

                AddInternalListener(toggle.onValueChanged, control, nameof(SegmentedControl._OnSegmentValueChanged));
            }

            SetLayoutWidth(rect, true);
            return rect.gameObject;
        }

        private static TextMeshProUGUI BuildLabel(Transform parent, string text, float fontSize, Vector2 size, Color color, FontStyles fontStyle)
        {
            RectTransform rect = CreateRect("Label", parent, size);

            TextMeshProUGUI label = rect.gameObject.AddComponent<TextMeshProUGUI>();

            // ツールキット共通のフォントがあれば使う。無ければ TextMeshPro の既定のフォントになる（FND-06）。
            TMP_FontAsset fontAsset = FontGenerator.LoadFontAsset();
            if (fontAsset != null)
                label.font = fontAsset;

            label.text = text;
            label.fontSize = fontSize;
            label.color = color;
            label.fontStyle = fontStyle;
            label.alignment = TextAlignmentOptions.Left;
            label.enableWordWrapping = false;
            label.raycastTarget = false;

            return label;
        }

        private static GameObject BuildButton(MaterialRole role, Color labelColor)
        {
            RectTransform rect = CreateRect("Button", null, ButtonSize);
            Image image = AddImage(rect, GetMaterial(role), true);

            Button button = rect.gameObject.AddComponent<Button>();
            SetupSelectable(button, image);

            TextMeshProUGUI label = BuildLabel(rect, "Button", 24f, Vector2.zero, labelColor, FontStyles.Bold);
            label.alignment = TextAlignmentOptions.Center;
            Stretch(label.rectTransform);

            ButtonControl control = rect.gameObject.AddUdonSharpComponent<ButtonControl>();
            AddInternalListener(button.onClick, control, nameof(ButtonControl._OnClick));

            SetLayoutWidth(rect, false);
            return rect.gameObject;
        }

        private static GameObject BuildToggle()
        {
            Material onMaterial = GetMaterial(MaterialRole.Accent);
            Material offMaterial = GetMaterial(MaterialRole.Track);

            RectTransform rect = CreateRect("Toggle", null, ToggleSize);
            Image track = AddImage(rect, offMaterial, true);

            RectTransform knob = CreateRect("Knob", rect, ToggleKnobSize);
            knob.anchoredPosition = new Vector2(-ToggleKnobOffset, 0f);
            AddImage(knob, GetMaterial(MaterialRole.Knob), false);

            // 見た目の切り替えは ToggleSwitch が行うので、標準のチェックマークは使わない。
            Toggle toggle = rect.gameObject.AddComponent<Toggle>();
            SetupSelectable(toggle, track);
            toggle.isOn = false;

            ToggleSwitch control = rect.gameObject.AddUdonSharpComponent<ToggleSwitch>();
            SerializedObject serializedControl = new SerializedObject(control);
            serializedControl.FindProperty("toggle").objectReferenceValue = toggle;
            serializedControl.FindProperty("track").objectReferenceValue = track;
            serializedControl.FindProperty("knob").objectReferenceValue = knob;
            serializedControl.FindProperty("onMaterial").objectReferenceValue = onMaterial;
            serializedControl.FindProperty("offMaterial").objectReferenceValue = offMaterial;
            serializedControl.FindProperty("knobOffset").floatValue = ToggleKnobOffset;
            serializedControl.ApplyModifiedPropertiesWithoutUndo();

            AddInternalListener(toggle.onValueChanged, control, nameof(ToggleSwitch._OnToggleValueChanged));

            SetLayoutWidth(rect, false);
            return rect.gameObject;
        }

        private static GameObject BuildSlider()
        {
            RectTransform rect = CreateRect("Slider", null, SliderSize);

            // 右端を現在値の表示に使い、残りをバーにする。
            RectTransform bar = CreateRect("Bar", rect, Vector2.zero);
            Stretch(bar);
            bar.offsetMax = new Vector2(-(SliderLabelWidth + SliderLabelGap), 0f);

            RectTransform background = CreateRect("Background", bar, Vector2.zero);
            SetHorizontalBar(background, SliderTrackHeight);
            AddImage(background, GetMaterial(MaterialRole.Track), true);

            RectTransform fillArea = CreateRect("Fill Area", bar, Vector2.zero);
            SetHorizontalBar(fillArea, SliderTrackHeight);

            RectTransform fill = CreateRect("Fill", fillArea, Vector2.zero);
            Stretch(fill);
            AddImage(fill, GetMaterial(MaterialRole.Accent), false);

            // つまみが端からはみ出さないよう、つまみの半分の幅だけ可動域を内側に寄せる。
            float handleSize = SliderSize.y;
            RectTransform handleArea = CreateRect("Handle Slide Area", bar, Vector2.zero);
            Stretch(handleArea);
            handleArea.offsetMin = new Vector2(handleSize * 0.5f, 0f);
            handleArea.offsetMax = new Vector2(-handleSize * 0.5f, 0f);

            RectTransform handle = CreateRect("Handle", handleArea, Vector2.zero);
            Stretch(handle);
            handle.sizeDelta = new Vector2(handleSize, 0f);
            Image handleImage = AddImage(handle, GetMaterial(MaterialRole.Knob), true);

            TextMeshProUGUI valueLabel = BuildLabel(rect, "", 24f, Vector2.zero, TextColor, FontStyles.Normal);
            valueLabel.gameObject.name = UIBuilder.ValueLabelName;
            valueLabel.alignment = TextAlignmentOptions.Right;
            RectTransform labelRect = valueLabel.rectTransform;
            labelRect.anchorMin = new Vector2(1f, 0f);
            labelRect.anchorMax = new Vector2(1f, 1f);
            labelRect.pivot = new Vector2(1f, 0.5f);
            labelRect.anchoredPosition = Vector2.zero;
            labelRect.sizeDelta = new Vector2(SliderLabelWidth, 0f);

            Slider slider = rect.gameObject.AddComponent<Slider>();
            SetupSelectable(slider, handleImage);
            slider.fillRect = fill;
            slider.handleRect = handle;
            slider.direction = Slider.Direction.LeftToRight;
            slider.minValue = 0f;
            slider.maxValue = 1f;
            slider.value = 0.5f;

            SliderControl control = rect.gameObject.AddUdonSharpComponent<SliderControl>();
            SerializedObject serializedControl = new SerializedObject(control);
            serializedControl.FindProperty("slider").objectReferenceValue = slider;
            serializedControl.FindProperty("valueLabel").objectReferenceValue = valueLabel;
            string valueFormat = serializedControl.FindProperty("valueFormat").stringValue;
            serializedControl.ApplyModifiedPropertiesWithoutUndo();

            // 再生前のエディタ上でも現在値が見えるようにしておく。再生後は SliderControl が更新する。
            valueLabel.text = slider.value.ToString(valueFormat, CultureInfo.InvariantCulture);

            AddInternalListener(slider.onValueChanged, control, nameof(SliderControl._OnSliderValueChanged));

            SetLayoutWidth(rect, true);
            return rect.gameObject;
        }

        private static GameObject BuildCheckbox()
        {
            Material onMaterial = GetMaterial(MaterialRole.Accent);
            Material offMaterial = GetMaterial(MaterialRole.Track);

            RectTransform rect = CreateRect("Checkbox", null, CheckboxSize);

            // 左端の丸い枠。幅と高さが同じなので、ピル形状のマテリアルで円になる。
            RectTransform box = CreateRect(UIBuilder.CheckboxBoxName, rect, CheckboxBoxSize);
            box.anchorMin = new Vector2(0f, 0.5f);
            box.anchorMax = new Vector2(0f, 0.5f);
            box.pivot = new Vector2(0f, 0.5f);
            box.anchoredPosition = Vector2.zero;
            Image boxImage = AddImage(box, offMaterial, false);

            // チェックのアイコン。オンのときだけ表示する。
            GameObject check = InstantiateNested(PrefabKind.Icon, box);
            check.name = UIBuilder.CheckboxCheckName;
            RectTransform checkRect = (RectTransform)check.transform;
            checkRect.sizeDelta = CheckboxCheckSize;
            RawImage checkImage = check.GetComponent<RawImage>();
            checkImage.color = Color.white;
            UIBuilder.SetIcon(checkImage, IconType.Check);
            check.SetActive(false);

            // 項目名は全体に広げ、枠の右から書き始める。項目名の範囲がそのまま押せる範囲になる。
            TextMeshProUGUI label = BuildLabel(rect, "Checkbox", 24f, Vector2.zero, TextColor, FontStyles.Normal);
            label.gameObject.name = UIBuilder.CheckboxLabelName;
            Stretch(label.rectTransform);
            label.margin = new Vector4(CheckboxBoxSize.x + CheckboxLabelGap, 0f, 0f, 0f);
            label.raycastTarget = true;

            // 見た目の切り替えは CheckboxControl が行うので、標準のチェックマークは使わない。
            Toggle toggle = rect.gameObject.AddComponent<Toggle>();
            SetupSelectable(toggle, boxImage);
            toggle.isOn = false;

            CheckboxControl control = rect.gameObject.AddUdonSharpComponent<CheckboxControl>();
            SerializedObject serializedControl = new SerializedObject(control);
            serializedControl.FindProperty("toggle").objectReferenceValue = toggle;
            serializedControl.FindProperty("box").objectReferenceValue = boxImage;
            serializedControl.FindProperty("checkmark").objectReferenceValue = check;
            serializedControl.FindProperty("onMaterial").objectReferenceValue = onMaterial;
            serializedControl.FindProperty("offMaterial").objectReferenceValue = offMaterial;
            serializedControl.ApplyModifiedPropertiesWithoutUndo();

            AddInternalListener(toggle.onValueChanged, control, nameof(CheckboxControl._OnToggleValueChanged));

            SetLayoutWidth(rect, true);

            // 中に含めたプレハブへの変更は、記録しないとこのプレハブに保存されない。
            UIBuilder.RecordOverrides(check, checkRect, checkImage);

            return rect.gameObject;
        }

        private static GameObject BuildStepper()
        {
            RectTransform rect = CreateRect("Stepper", null, StepperSize);

            // 左端に－、右端に＋のボタンを置き、その間に現在値を表示する。
            GameObject decrease = BuildStepperButton(rect, UIBuilder.StepperDecreaseName, 0f, IconType.Minus);
            GameObject increase = BuildStepperButton(rect, UIBuilder.StepperIncreaseName, 1f, IconType.Plus);

            TextMeshProUGUI valueLabel = BuildLabel(rect, "", 24f, Vector2.zero, TextColor, FontStyles.Normal);
            valueLabel.gameObject.name = UIBuilder.ValueLabelName;
            valueLabel.alignment = TextAlignmentOptions.Center;
            Stretch(valueLabel.rectTransform);
            valueLabel.rectTransform.offsetMin = new Vector2(StepperSize.y, 0f);
            valueLabel.rectTransform.offsetMax = new Vector2(-StepperSize.y, 0f);

            StepperControl control = rect.gameObject.AddUdonSharpComponent<StepperControl>();
            SerializedObject serializedControl = new SerializedObject(control);
            serializedControl.FindProperty("valueLabel").objectReferenceValue = valueLabel;
            serializedControl.FindProperty("decreaseButton").objectReferenceValue = decrease.GetComponent<Button>();
            serializedControl.FindProperty("increaseButton").objectReferenceValue = increase.GetComponent<Button>();
            serializedControl.ApplyModifiedPropertiesWithoutUndo();

            // ボタンは、押されたことをステッパー自身に伝える。
            UIBuilder.SetEvent(decrease.GetComponent<ButtonControl>(), control, nameof(StepperControl._OnDecrease));
            UIBuilder.SetEvent(increase.GetComponent<ButtonControl>(), control, nameof(StepperControl._OnIncrease));

            UIBuilder.RefreshStepper(control);

            SetLayoutWidth(rect, false);
            return rect.gameObject;
        }

        // ステッパーの端に置くアイコンボタンを作る。side は 0 で左端、1 で右端。
        private static GameObject BuildStepperButton(RectTransform parent, string name, float side, IconType icon)
        {
            GameObject button = InstantiateNested(PrefabKind.ButtonIcon, parent);
            button.name = name;

            RectTransform buttonRect = (RectTransform)button.transform;
            buttonRect.anchorMin = new Vector2(side, 0.5f);
            buttonRect.anchorMax = new Vector2(side, 0.5f);
            buttonRect.pivot = new Vector2(side, 0.5f);
            buttonRect.anchoredPosition = Vector2.zero;
            buttonRect.sizeDelta = new Vector2(StepperSize.y, StepperSize.y);

            RawImage iconImage = button.GetComponentInChildren<RawImage>(true);
            UIBuilder.SetIcon(iconImage, icon);

            // 中に含めたプレハブへの変更は、記録しないとこのプレハブに保存されない。
            UIBuilder.RecordOverrides(button, buttonRect, iconImage);

            return button;
        }

        private static GameObject BuildProgressBar()
        {
            RectTransform rect = CreateRect("ProgressBar", null, SliderSize);

            // スライダーからつまみを除いた形。右端を現在値の表示に使い、残りをバーにする。
            RectTransform bar = CreateRect("Bar", rect, Vector2.zero);
            Stretch(bar);
            bar.offsetMax = new Vector2(-(SliderLabelWidth + SliderLabelGap), 0f);

            // 操作できない部品なので、当たり判定は持たせない。レーザーを遮らない。
            RectTransform background = CreateRect("Background", bar, Vector2.zero);
            SetHorizontalBar(background, SliderTrackHeight);
            AddImage(background, GetMaterial(MaterialRole.Track), false);

            // 値が 0 に近いときも形が崩れないよう、塗りの最小の幅をバーの高さと同じにする。
            // 塗りの可動域を左右に高さの半分ずつ狭め、その分だけ塗りの幅を広げておく。
            RectTransform fillArea = CreateRect("Fill Area", bar, Vector2.zero);
            SetHorizontalBar(fillArea, SliderTrackHeight);
            fillArea.sizeDelta = new Vector2(-SliderTrackHeight, SliderTrackHeight);

            RectTransform fill = CreateRect("Fill", fillArea, Vector2.zero);
            Stretch(fill);
            fill.sizeDelta = new Vector2(SliderTrackHeight, 0f);
            AddImage(fill, GetMaterial(MaterialRole.Accent), false);

            TextMeshProUGUI valueLabel = BuildLabel(rect, "", 24f, Vector2.zero, TextColor, FontStyles.Normal);
            valueLabel.gameObject.name = UIBuilder.ValueLabelName;
            valueLabel.alignment = TextAlignmentOptions.Right;
            RectTransform labelRect = valueLabel.rectTransform;
            labelRect.anchorMin = new Vector2(1f, 0f);
            labelRect.anchorMax = new Vector2(1f, 1f);
            labelRect.pivot = new Vector2(1f, 0.5f);
            labelRect.anchoredPosition = Vector2.zero;
            labelRect.sizeDelta = new Vector2(SliderLabelWidth, 0f);

            // 塗りの長さの計算に、uGUI の Slider を使う。操作は受け付けない。
            Slider slider = rect.gameObject.AddComponent<Slider>();
            slider.interactable = false;
            slider.transition = Selectable.Transition.None;
            slider.navigation = new Navigation { mode = Navigation.Mode.None };
            slider.fillRect = fill;
            slider.direction = Slider.Direction.LeftToRight;
            slider.minValue = 0f;
            slider.maxValue = 1f;
            slider.value = 0.5f;

            ProgressBar control = rect.gameObject.AddUdonSharpComponent<ProgressBar>();
            SerializedObject serializedControl = new SerializedObject(control);
            serializedControl.FindProperty("slider").objectReferenceValue = slider;
            serializedControl.FindProperty("valueLabel").objectReferenceValue = valueLabel;
            string valueFormat = serializedControl.FindProperty("valueFormat").stringValue;
            serializedControl.ApplyModifiedPropertiesWithoutUndo();

            // 再生前のエディタ上でも現在値が見えるようにしておく。再生後は ProgressBar が更新する。
            valueLabel.text = slider.value.ToString(valueFormat, CultureInfo.InvariantCulture);

            SetLayoutWidth(rect, true);
            return rect.gameObject;
        }

        private static GameObject BuildScrollArea()
        {
            RectTransform rect = CreateRect("ScrollArea", null, ScrollAreaSize);

            // 表示する範囲。はみ出した部分は描かない。右端はスクロールバーの分を空ける。
            RectTransform viewport = CreateRect(UIBuilder.ScrollViewportName, rect, Vector2.zero);
            Stretch(viewport);
            viewport.offsetMax = new Vector2(-(ScrollbarWidth + ScrollbarGap), 0f);
            viewport.gameObject.AddComponent<RectMask2D>();

            // 部品の無い場所でもつかんで動かせるよう、見えない当たり判定を置く。完全に透明なので、描画は省かれる。
            Image hitArea = viewport.gameObject.AddComponent<Image>();
            hitArea.color = Color.clear;
            hitArea.raycastTarget = true;
            viewport.GetComponent<CanvasRenderer>().cullTransparentMesh = true;

            // 中身。上端を基準にして、中に置いた部品の分だけ下に伸びる。
            RectTransform content = CreateRect(UIBuilder.ContentName, viewport, Vector2.zero);
            content.anchorMin = new Vector2(0f, 1f);
            content.anchorMax = new Vector2(1f, 1f);
            content.pivot = new Vector2(0.5f, 1f);
            content.anchoredPosition = Vector2.zero;
            content.sizeDelta = Vector2.zero;
            AddStackLayout(content, true);

            ContentSizeFitter fitter = content.gameObject.AddComponent<ContentSizeFitter>();
            fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            // 右端のスクロールバー。つまみの長さと位置は、uGUI の Scrollbar が決める。
            RectTransform scrollbarRect = CreateRect("Scrollbar", rect, Vector2.zero);
            scrollbarRect.anchorMin = new Vector2(1f, 0f);
            scrollbarRect.anchorMax = new Vector2(1f, 1f);
            scrollbarRect.pivot = new Vector2(1f, 0.5f);
            scrollbarRect.anchoredPosition = Vector2.zero;
            scrollbarRect.sizeDelta = new Vector2(ScrollbarWidth, 0f);
            AddImage(scrollbarRect, GetMaterial(MaterialRole.Track), true);

            RectTransform slidingArea = CreateRect("Sliding Area", scrollbarRect, Vector2.zero);
            Stretch(slidingArea);

            RectTransform handle = CreateRect("Handle", slidingArea, Vector2.zero);
            Stretch(handle);
            Image handleImage = AddImage(handle, GetMaterial(MaterialRole.Knob), true);

            Scrollbar scrollbar = scrollbarRect.gameObject.AddComponent<Scrollbar>();
            SetupSelectable(scrollbar, handleImage);
            scrollbar.handleRect = handle;
            scrollbar.direction = Scrollbar.Direction.BottomToTop;

            // 縦方向だけ動かす。端を越えては動かさない。中身が収まっているときは、スクロールバーを隠す。
            ScrollRect scrollRect = rect.gameObject.AddComponent<ScrollRect>();
            scrollRect.viewport = viewport;
            scrollRect.content = content;
            scrollRect.horizontal = false;
            scrollRect.vertical = true;
            scrollRect.movementType = ScrollRect.MovementType.Clamped;
            scrollRect.scrollSensitivity = ScrollSensitivity;
            scrollRect.verticalScrollbar = scrollbar;
            scrollRect.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.AutoHide;

            SetLayoutWidth(rect, true);
            return rect.gameObject;
        }

        /// <summary>
        /// タブを作る。見出しは選択ボタン群、ページは縦の自動配置で、どちらも既存のプレハブを中に含める。
        /// </summary>
        private static GameObject BuildTabs()
        {
            RectTransform rect = CreateRect("Tabs", null, TabsSize);

            // 見出し。上端に、幅いっぱいで置く。
            GameObject header = InstantiateNested(PrefabKind.SegmentedControl, rect);
            header.name = UIBuilder.TabsHeaderName;
            RectTransform headerRect = (RectTransform)header.transform;
            headerRect.anchorMin = new Vector2(0f, 1f);
            headerRect.anchorMax = new Vector2(1f, 1f);
            headerRect.pivot = new Vector2(0.5f, 1f);
            headerRect.anchoredPosition = Vector2.zero;
            headerRect.sizeDelta = new Vector2(0f, TabsHeaderHeight);
            UIBuilder.RecordOverrides(header, headerRect);

            TextMeshProUGUI[] headerLabels = header.GetComponentsInChildren<TextMeshProUGUI>(true);
            for (int i = 0; i < headerLabels.Length; i++)
            {
                headerLabels[i].text = "Tab " + (i + 1);
                UIBuilder.RecordOverrides(headerLabels[i]);
            }

            // ページを入れる場所。見出しの下の残り全部を使う。
            RectTransform pages = CreateRect(UIBuilder.TabsPagesName, rect, Vector2.zero);
            Stretch(pages);
            pages.offsetMax = new Vector2(0f, -(TabsHeaderHeight + StackSpacing));

            // ページは、見出しの選択肢と同じ2つで作る。最初のページだけを表示しておく。
            for (int i = 0; i < 2; i++)
            {
                GameObject page = InstantiateNested(PrefabKind.StackVertical, pages);
                page.name = UIBuilder.GetTabPageName(i);
                RectTransform pageRect = (RectTransform)page.transform;
                Stretch(pageRect);
                page.SetActive(i == 0);
                UIBuilder.RecordOverrides(page, pageRect);
            }

            TabView control = rect.gameObject.AddUdonSharpComponent<TabView>();
            SerializedObject serializedControl = new SerializedObject(control);
            serializedControl.FindProperty("header").objectReferenceValue = header.GetComponent<SegmentedControl>();
            serializedControl.FindProperty("pageContainer").objectReferenceValue = pages;
            serializedControl.ApplyModifiedPropertiesWithoutUndo();

            // 見出しは、選択が変わったことをタブ自身に伝える。
            UIBuilder.SetEvent(header.GetComponent<SegmentedControl>(), control, nameof(TabView._OnHeaderChanged));

            SetLayoutWidth(rect, true);
            return rect.gameObject;
        }

        /// <summary>
        /// 確認ダイアログを作る。Canvas・パネル・メッセージ・ボタンは、既存のプレハブを中に含める形で組み立てる。
        /// </summary>
        private static GameObject BuildDialog()
        {
            // 開閉するスクリプトは、初期化（Start）が開閉に左右されないよう、常に有効な Canvas の親に置く。
            GameObject root = new GameObject("Dialog");

            GameObject canvas = InstantiateNested(PrefabKind.Canvas, root.transform);
            canvas.name = UIBuilder.WindowCanvasName;
            RectTransform canvasRect = (RectTransform)canvas.transform;
            canvasRect.sizeDelta = DialogSize;
            BoxCollider boxCollider = canvas.GetComponent<BoxCollider>();
            boxCollider.size = new Vector3(DialogSize.x, DialogSize.y, boxCollider.size.z);

            InstantiateNested(PrefabKind.Panel, canvas.transform);

            // メッセージ。ボタンより上の領域に広げ、長い文は折り返す。
            GameObject message = InstantiateNested(PrefabKind.LabelBody, canvas.transform);
            message.name = UIBuilder.DialogMessageName;
            RectTransform messageRect = (RectTransform)message.transform;
            Stretch(messageRect);
            messageRect.offsetMin = new Vector2(WindowPadding, WindowPadding + DialogButtonSize.y + WindowTitleGap);
            messageRect.offsetMax = new Vector2(-WindowPadding, -WindowPadding);
            TextMeshProUGUI messageText = message.GetComponent<TextMeshProUGUI>();
            messageText.text = "Are you sure?";
            messageText.enableWordWrapping = true;
            messageText.alignment = TextAlignmentOptions.TopLeft;

            // ボタンは右下に並べる。右が「はい」、その左が「いいえ」。
            GameObject confirm = BuildDialogButton(canvas.transform, PrefabKind.ButtonPrimary, UIBuilder.DialogConfirmName, "OK", 0);
            GameObject cancel = BuildDialogButton(canvas.transform, PrefabKind.ButtonOutline, UIBuilder.DialogCancelName, "Cancel", 1);

            ConfirmDialog dialog = root.AddUdonSharpComponent<ConfirmDialog>();
            SerializedObject serializedDialog = new SerializedObject(dialog);
            serializedDialog.FindProperty("content").objectReferenceValue = canvas;
            serializedDialog.FindProperty("messageLabel").objectReferenceValue = messageText;
            serializedDialog.ApplyModifiedPropertiesWithoutUndo();

            // ボタンは、押されたことをダイアログ自身に伝える。
            UIBuilder.SetEvent(confirm.GetComponent<ButtonControl>(), dialog, nameof(ConfirmDialog._OnConfirm));
            UIBuilder.SetEvent(cancel.GetComponent<ButtonControl>(), dialog, nameof(ConfirmDialog._OnCancel));

            // 中に含めたプレハブへの変更は、記録しないとこのプレハブに保存されない。
            UIBuilder.RecordOverrides(canvas, canvasRect, boxCollider, message, messageRect, messageText);

            return root;
        }

        // ダイアログの右下に置くボタンを作る。indexFromRight は右から数えた順番（0 始まり）。
        private static GameObject BuildDialogButton(Transform parent, PrefabKind kind, string name, string label, int indexFromRight)
        {
            GameObject button = InstantiateNested(kind, parent);
            button.name = name;

            RectTransform buttonRect = (RectTransform)button.transform;
            buttonRect.anchorMin = new Vector2(1f, 0f);
            buttonRect.anchorMax = new Vector2(1f, 0f);
            buttonRect.pivot = new Vector2(1f, 0f);
            buttonRect.anchoredPosition = new Vector2(
                -(WindowPadding + indexFromRight * (DialogButtonSize.x + StackSpacing)), WindowPadding);
            buttonRect.sizeDelta = DialogButtonSize;

            TextMeshProUGUI text = button.GetComponentInChildren<TextMeshProUGUI>(true);
            text.text = label;

            // 中に含めたプレハブへの変更は、記録しないとこのプレハブに保存されない。
            UIBuilder.RecordOverrides(button, buttonRect, text);

            return button;
        }

        /// <summary>
        /// 役割ごとの共通マテリアルを返す。無ければ初期値で作成する。既にあるマテリアルは書き換えない（FND-04）。
        /// </summary>
        internal static Material GetMaterial(MaterialRole role)
        {
            Material material = LoadMaterial(role);
            if (material != null)
                return material;

            string materialPath = GetMaterialPath(role);

            string shaderName = role == MaterialRole.Icon ? UIBuilder.IconShaderName : UIBuilder.ShaderName;
            Shader shader = Shader.Find(shaderName);
            if (shader == null)
            {
                Debug.LogError("[" + UIBuilder.DisplayName + "] シェーダー '" + shaderName + "' が見つかりません。");
                return null;
            }

            EnsureFolder(MaterialFolder);

            material = new Material(shader);
            ApplyDefaultStyle(material, role);
            CreateAsset(material, materialPath);
            return material;
        }

        /// <summary>
        /// 役割ごとの共通マテリアルを返す。無ければ null を返し、作成はしない。
        /// </summary>
        internal static Material LoadMaterial(MaterialRole role)
        {
            return AssetDatabase.LoadAssetAtPath<Material>(GetMaterialPath(role));
        }

        private static string GetMaterialPath(MaterialRole role)
        {
            return MaterialFolder + "/ZS_" + role + ".mat";
        }

        /// <summary>
        /// マテリアルの色・角丸・枠線を、その役割の初期値にする。
        /// </summary>
        internal static void ApplyDefaultStyle(Material material, MaterialRole role)
        {
            switch (role)
            {
                case MaterialRole.Surface:
                    SetMaterialStyle(material, SurfaceColor, 28f, 2f, SurfaceBorderColor);
                    break;
                case MaterialRole.Accent:
                    SetMaterialStyle(material, AccentColor, 9999f, 0f, Color.white);
                    break;
                case MaterialRole.Track:
                    SetMaterialStyle(material, TrackColor, 9999f, 0f, Color.white);
                    break;
                case MaterialRole.Knob:
                    SetMaterialStyle(material, KnobColor, 9999f, 0f, Color.white);
                    break;
                case MaterialRole.Outline:
                    // 塗りを透明にし、アクセント色の枠線だけを描く。
                    SetMaterialStyle(material, new Color(AccentColor.r, AccentColor.g, AccentColor.b, 0f), 9999f, OutlineWidth, AccentColor);
                    break;
                case MaterialRole.Icon:
                    material.SetColor(ColorProperty, Color.white);
                    material.SetFloat(ThicknessProperty, IconThickness);
                    break;
            }
        }

        // 角丸半径に短辺の半分を超える値を入れると、ピル形状になる。
        private static void SetMaterialStyle(Material material, Color color, float radius, float borderWidth, Color borderColor)
        {
            material.SetColor(ColorProperty, color);
            material.SetFloat(RadiusProperty, radius);
            material.SetFloat(BorderWidthProperty, borderWidth);
            material.SetColor(BorderColorProperty, borderColor);
        }

        // VRChat では uGUI イベントから呼べる先が限られるため、UdonBehaviour.SendCustomEvent 経由で部品自身のスクリプトを呼ぶ。
        private static void AddInternalListener(UnityEventBase unityEvent, UdonSharpBehaviour behaviour, string eventName)
        {
            UdonBehaviour udonBehaviour = UdonSharpEditorUtility.GetBackingUdonBehaviour(behaviour);
            UnityEventTools.AddStringPersistentListener(unityEvent, udonBehaviour.SendCustomEvent, eventName);
        }

        private static void SetupSelectable(Selectable selectable, Graphic targetGraphic)
        {
            ColorBlock colors = ColorBlock.defaultColorBlock;
            colors.normalColor = new Color(0.86f, 0.86f, 0.86f, 1f);
            colors.highlightedColor = Color.white;
            colors.pressedColor = new Color(0.62f, 0.62f, 0.62f, 1f);
            colors.selectedColor = colors.normalColor;
            colors.fadeDuration = 0.08f;

            selectable.targetGraphic = targetGraphic;
            selectable.transition = Selectable.Transition.ColorTint;
            selectable.colors = colors;
            selectable.navigation = new Navigation { mode = Navigation.Mode.None };
        }

        // 色はマテリアルが持つので、Image 側の色は白のままにする。
        private static Image AddImage(RectTransform rect, Material material, bool raycastTarget)
        {
            Image image = rect.gameObject.AddComponent<Image>();
            image.material = material;
            image.raycastTarget = raycastTarget;
            return image;
        }

        private static RectTransform CreateRect(string name, Transform parent, Vector2 size)
        {
            GameObject gameObject = new GameObject(name, typeof(RectTransform));
            RectTransform rect = (RectTransform)gameObject.transform;

            if (parent != null)
                rect.SetParent(parent, false);

            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = size;
            return rect;
        }

        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        // 親の幅いっぱいに伸び、縦は中央に固定した高さのバーにする。
        private static void SetHorizontalBar(RectTransform rect, float height)
        {
            rect.anchorMin = new Vector2(0f, 0.5f);
            rect.anchorMax = new Vector2(1f, 0.5f);
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = new Vector2(0f, height);
        }
    }
}
