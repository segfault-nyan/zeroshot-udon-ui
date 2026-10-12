
using UnityEditor;
using UnityEngine;

// EDT-01
namespace ZeroShotUdonUI.EditorTools
{
    /// <summary>
    /// GameObject メニューと Hierarchy の右クリックメニューに、ツールキットの部品を追加する項目を加える。
    /// 部品は、選択中のオブジェクトの下に置かれる。
    /// </summary>
    internal static class CreateMenu
    {
        private const string MenuRoot = "GameObject/ZeroShot Udon UI/";

        // 番号が 11 以上離れると、メニューに区切り線が入る。
        private const int ContainerPriority = 10;
        private const int ControlPriority = 30;
        private const int LabelPriority = 52;

        private static readonly Vector2 DefaultCanvasSize = new Vector2(600f, 400f);
        private static readonly Vector2 DefaultDialogSize = new Vector2(480f, 260f);

        [MenuItem(MenuRoot + "ウィンドウ", false, ContainerPriority)]
        private static void AddWindow(MenuCommand command)
        {
            if (!UIBuilder.EnsureSetup())
                return;

            WindowPanel window = UIBuilder.CreateWindow("Window", "Window", DefaultCanvasSize, out _);
            PlaceRoot(window.gameObject, command.context as GameObject);
            Finish(window.gameObject, window.gameObject);
        }

        [MenuItem(MenuRoot + "Canvas とパネル", false, ContainerPriority + 1)]
        private static void AddCanvas(MenuCommand command)
        {
            if (!UIBuilder.EnsureSetup())
                return;

            GameObject canvas = CreatePanelCanvas(command.context as GameObject, out _);
            Finish(canvas, canvas);
        }

        [MenuItem(MenuRoot + "パネル", false, ContainerPriority + 2)]
        private static void AddPanel(MenuCommand command)
        {
            AddElement(command, parent => UIBuilder.CreatePanel(parent), false);
        }

        [MenuItem(MenuRoot + "自動配置（縦）", false, ContainerPriority + 3)]
        private static void AddVerticalStack(MenuCommand command)
        {
            AddElement(command, parent => UIBuilder.CreateStack(parent, StackDirection.Vertical), false);
        }

        [MenuItem(MenuRoot + "自動配置（横）", false, ContainerPriority + 4)]
        private static void AddHorizontalStack(MenuCommand command)
        {
            AddElement(command, parent => UIBuilder.CreateStack(parent, StackDirection.Horizontal), false);
        }

        [MenuItem(MenuRoot + "設定行", false, ContainerPriority + 5)]
        private static void AddSettingRow(MenuCommand command)
        {
            AddElement(command, parent => UIBuilder.CreateSettingRow(parent, "Label", null), false);
        }

        [MenuItem(MenuRoot + "スクロール領域", false, ContainerPriority + 6)]
        private static void AddScrollArea(MenuCommand command)
        {
            AddElement(command, parent => UIBuilder.CreateScrollArea(parent, out _), false);
        }

        [MenuItem(MenuRoot + "タブ", false, ContainerPriority + 7)]
        private static void AddTabs(MenuCommand command)
        {
            AddElement(command, parent => UIBuilder.CreateTabs(parent, new[] { "Tab 1", "Tab 2" }, 0, out _), false);
        }

        [MenuItem(MenuRoot + "確認ダイアログ", false, ContainerPriority + 8)]
        private static void AddDialog(MenuCommand command)
        {
            if (!UIBuilder.EnsureSetup())
                return;

            ConfirmDialog dialog = UIBuilder.CreateDialog("Dialog", "Are you sure?", "OK", "Cancel", DefaultDialogSize);
            PlaceRoot(dialog.gameObject, command.context as GameObject);
            Finish(dialog.gameObject, dialog.gameObject);
        }

        [MenuItem(MenuRoot + "ボタン（主）", false, ControlPriority)]
        private static void AddPrimaryButton(MenuCommand command)
        {
            AddElement(command, parent => UIBuilder.CreateButton(parent, "Button", ButtonStyle.Primary), true);
        }

        [MenuItem(MenuRoot + "ボタン（副）", false, ControlPriority + 1)]
        private static void AddSecondaryButton(MenuCommand command)
        {
            AddElement(command, parent => UIBuilder.CreateButton(parent, "Button", ButtonStyle.Secondary), true);
        }

        [MenuItem(MenuRoot + "ボタン（枠線のみ）", false, ControlPriority + 2)]
        private static void AddOutlineButton(MenuCommand command)
        {
            AddElement(command, parent => UIBuilder.CreateButton(parent, "Button", ButtonStyle.Outline), true);
        }

        [MenuItem(MenuRoot + "トグルスイッチ", false, ControlPriority + 3)]
        private static void AddToggle(MenuCommand command)
        {
            AddElement(command, parent => UIBuilder.CreateToggle(parent, false), true);
        }

        [MenuItem(MenuRoot + "スライダー", false, ControlPriority + 4)]
        private static void AddSlider(MenuCommand command)
        {
            AddElement(command, parent => UIBuilder.CreateSlider(parent, 0f, 1f, 0.5f), true);
        }

        [MenuItem(MenuRoot + "選択ボタン群", false, ControlPriority + 5)]
        private static void AddSegmented(MenuCommand command)
        {
            AddElement(command, parent => UIBuilder.CreateSegmented(parent, new[] { "Option 1", "Option 2" }, 0), true);
        }

        [MenuItem(MenuRoot + "アイコンボタン", false, ControlPriority + 6)]
        private static void AddIconButton(MenuCommand command)
        {
            AddElement(command, parent => UIBuilder.CreateIconButton(parent, IconType.Info), true);
        }

        [MenuItem(MenuRoot + "チェックボックス", false, ControlPriority + 7)]
        private static void AddCheckbox(MenuCommand command)
        {
            AddElement(command, parent => UIBuilder.CreateCheckbox(parent, "Checkbox", false), true);
        }

        [MenuItem(MenuRoot + "数値ステッパー", false, ControlPriority + 8)]
        private static void AddStepper(MenuCommand command)
        {
            AddElement(command, parent => UIBuilder.CreateStepper(parent, 0f, 10f, 1f, 0f), true);
        }

        [MenuItem(MenuRoot + "進捗バー", false, LabelPriority + 4)]
        private static void AddProgressBar(MenuCommand command)
        {
            AddElement(command, parent => UIBuilder.CreateProgressBar(parent, 0.5f), false);
        }

        [MenuItem(MenuRoot + "アイコン", false, LabelPriority + 3)]
        private static void AddIcon(MenuCommand command)
        {
            AddElement(command, parent => UIBuilder.CreateIcon(parent, IconType.Info), false);
        }

        [MenuItem(MenuRoot + "ラベル（見出し）", false, LabelPriority)]
        private static void AddHeadingLabel(MenuCommand command)
        {
            AddElement(command, parent => UIBuilder.CreateLabel(parent, "Label", LabelStyle.Heading), false);
        }

        [MenuItem(MenuRoot + "ラベル（本文）", false, LabelPriority + 1)]
        private static void AddBodyLabel(MenuCommand command)
        {
            AddElement(command, parent => UIBuilder.CreateLabel(parent, "Label", LabelStyle.Body), false);
        }

        [MenuItem(MenuRoot + "ラベル（補足）", false, LabelPriority + 2)]
        private static void AddCaptionLabel(MenuCommand command)
        {
            AddElement(command, parent => UIBuilder.CreateLabel(parent, "Label", LabelStyle.Caption), false);
        }

        private static void AddElement(MenuCommand command, System.Func<Transform, Component> create, bool isControl)
        {
            if (!UIBuilder.EnsureSetup())
                return;

            GameObject context = command.context as GameObject;

            // 設定行を選んでいる場合、操作部品は行の右端に置く。
            if (isControl && context != null && UIBuilder.TryGetSettingRow(context, out RectTransform row))
            {
                Component control = create(row);
                UIBuilder.AddToSettingRow(row, control);
                Finish(control.gameObject, control.gameObject);
                return;
            }

            Transform parent = FindParent(context);
            GameObject createdCanvas = null;
            if (parent == null)
            {
                // Canvas の外では表示も操作もできないので、置き場所になる Canvas を先に作る。
                createdCanvas = CreatePanelCanvas(context, out RectTransform content);
                parent = content;
            }

            Component element = create(parent);
            Finish(createdCanvas != null ? createdCanvas : element.gameObject, element.gameObject);
        }

        // 新しい要素の親にするオブジェクトを返す。選択中のオブジェクトが Canvas の中に無ければ null を返す。
        private static Transform FindParent(GameObject context)
        {
            if (context == null)
                return null;

            // ウィンドウ本体や Canvas を選んでいる場合は、中身の領域を親にする。置いた部品が上から順に並ぶ。
            Transform content = UIBuilder.FindContent(context);
            if (content != null)
                return content;

            return context.GetComponentInParent<Canvas>(true) != null ? context.transform : null;
        }

        private static GameObject CreatePanelCanvas(GameObject context, out RectTransform content)
        {
            GameObject canvas = UIBuilder.CreatePanelCanvas("Canvas", DefaultCanvasSize, out content);
            PlaceRoot(canvas, context);
            return canvas;
        }

        // Canvas、ウィンドウ、確認ダイアログを、選択中のオブジェクトの子として置く。
        // 何も選んでいないか、選んでいるものが Canvas の中にある場合は、シーンの最上位に作ってシーンビューの注視点に置く。
        private static void PlaceRoot(GameObject root, GameObject context)
        {
            // Canvas の中に別の Canvas を作ると、縮尺が二重に掛かって見えない大きさになる。
            if (context != null && context.GetComponentInParent<Canvas>(true) == null)
            {
                root.transform.SetParent(context.transform, false);
                return;
            }

            SceneView sceneView = SceneView.lastActiveSceneView;
            if (sceneView == null || sceneView.camera == null)
                return;

            // シーンビューのカメラから見て正面を向くようにする。
            Vector3 forward = Vector3.ProjectOnPlane(sceneView.camera.transform.forward, Vector3.up);
            Quaternion rotation = forward.sqrMagnitude > 0.0001f
                ? Quaternion.LookRotation(forward.normalized, Vector3.up)
                : Quaternion.identity;
            UIBuilder.PlaceInWorld(root, sceneView.pivot, rotation);
        }

        private static void Finish(GameObject undoRoot, GameObject selection)
        {
            Undo.RegisterCreatedObjectUndo(undoRoot, "Create " + selection.name);
            Selection.activeGameObject = selection;
        }
    }
}
