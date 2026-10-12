
using TMPro;
using UdonSharpEditor;
using UnityEditor;
using UnityEngine;
using ZeroShotUdonUI.Samples;

// EDT-04
namespace ZeroShotUdonUI.EditorTools
{
    /// <summary>
    /// 設定ウィンドウと、それを開くボタンのサンプルを組み立てる。UIBuilder の使い方の見本を兼ねる。
    /// メニューからは、開いているシーンのスポーン地点の正面に生成する。
    /// </summary>
    public static class SampleBuilder
    {
        // Canvas 単位。1 単位 = 1mm。
        private static readonly Vector2 WindowSize = new Vector2(600f, 500f);
        private static readonly Vector2 LauncherSize = new Vector2(280f, 96f);
        private static readonly Vector2 ButtonSize = new Vector2(160f, 56f);

        // ウィンドウの中心から、開くボタンの中心までの下方向の距離（メートル）。
        private const float LauncherDrop = 0.35f;

        [MenuItem("Tools/ZeroShot Udon UI/サンプル生成")]
        private static void CreateSample()
        {
            if (!UIBuilder.EnsureSetup(typeof(SampleController)))
                return;

            UIBuilder.GetPoseInFrontOfSpawn(out Vector3 position, out Quaternion rotation);
            GameObject root = BuildSettingsSample(position, rotation);

            Undo.RegisterCreatedObjectUndo(root, "Create ZeroShot Udon UI Sample");
            Selection.activeGameObject = root;

            if (SceneView.lastActiveSceneView != null)
                SceneView.lastActiveSceneView.FrameSelected();
        }

        /// <summary>
        /// 設定ウィンドウのサンプルを、指定した位置と向きに組み立てる。先に UIBuilder.EnsureSetup(typeof(SampleController)) が必要。
        /// </summary>
        internal static GameObject BuildSettingsSample(Vector3 position, Quaternion rotation)
        {
            // 制作者側のスクリプトは、ウィンドウを閉じている間も動くよう、ウィンドウの外に置く。
            GameObject root = new GameObject("ZS Sample");

            WindowPanel window = UIBuilder.CreateWindow("Settings Window", "Settings", WindowSize, out RectTransform content);
            window.transform.SetParent(root.transform, false);
            UIBuilder.PlaceInWorld(window.gameObject, position, rotation);

            // ウィンドウの中身の領域は縦の自動配置なので、置いた順に上から並ぶ。位置の指定は要らない。
            ToggleSwitch toggle = UIBuilder.CreateToggle(content, false);
            UIBuilder.CreateSettingRow(content, "Notifications", toggle);

            SliderControl slider = UIBuilder.CreateSlider(content, 0f, 1f, 0.5f);
            UIBuilder.CreateSettingRow(content, "Volume", slider);

            SegmentedControl quality = UIBuilder.CreateSegmented(content, new[] { "Low", "Medium", "High" }, 1);
            UIBuilder.CreateSettingRow(content, "Quality", quality);

            // ボタン3種類を横に並べる。
            RectTransform buttons = UIBuilder.CreateStack(content, StackDirection.Horizontal);

            ButtonControl clickButton = UIBuilder.CreateButton(buttons, "Click Me", ButtonStyle.Primary);
            UIBuilder.SetSize(clickButton, ButtonSize);

            ButtonControl resetButton = UIBuilder.CreateButton(buttons, "Reset", ButtonStyle.Secondary);
            UIBuilder.SetSize(resetButton, ButtonSize);

            ButtonControl closeButton = UIBuilder.CreateButton(buttons, "Close", ButtonStyle.Outline);
            UIBuilder.SetSize(closeButton, ButtonSize);

            TextMeshProUGUI statusLabel = UIBuilder.CreateLabel(content, "Last event: -", LabelStyle.Caption);

            // 閉じたウィンドウを開き直すためのボタン。ウィンドウの下に、別の Canvas として置く。
            GameObject launcher = UIBuilder.CreateCanvas("Launcher", LauncherSize);
            launcher.transform.SetParent(root.transform, false);
            UIBuilder.PlaceInWorld(launcher, position + Vector3.down * LauncherDrop, rotation);

            UIBuilder.CreatePanel(launcher.transform);
            ButtonControl openButton = UIBuilder.CreateButton(launcher.transform, "Open Settings", ButtonStyle.Primary);
            UIBuilder.SetSize(openButton, new Vector2(232f, 56f));

            SampleController controller = root.AddUdonSharpComponent<SampleController>();
            SerializedObject serializedController = new SerializedObject(controller);
            serializedController.FindProperty("toggle").objectReferenceValue = toggle;
            serializedController.FindProperty("slider").objectReferenceValue = slider;
            serializedController.FindProperty("quality").objectReferenceValue = quality;
            serializedController.FindProperty("statusLabel").objectReferenceValue = statusLabel;
            serializedController.ApplyModifiedPropertiesWithoutUndo();

            // U# は裏側の UdonBehaviour をインスペクターから隠す処理を遅延実行する。直後にシーンを保存しても隠れた状態になるよう、ここで隠しておく。
            UdonSharpEditorUtility.GetBackingUdonBehaviour(controller).hideFlags |= HideFlags.HideInInspector;

            // 各部品の通知先とイベント名を設定する。
            UIBuilder.SetEvent(toggle, controller, nameof(SampleController.OnToggleChanged));
            UIBuilder.SetEvent(slider, controller, nameof(SampleController.OnSliderChanged));
            UIBuilder.SetEvent(quality, controller, nameof(SampleController.OnQualityChanged));
            UIBuilder.SetEvent(clickButton, controller, nameof(SampleController.OnButtonClicked));
            UIBuilder.SetEvent(resetButton, controller, nameof(SampleController.OnResetClicked));

            // ウィンドウの開閉は、ウィンドウ自身を通知先にするだけでよい。
            UIBuilder.SetEvent(closeButton, window, nameof(WindowPanel.Close));
            UIBuilder.SetEvent(openButton, window, nameof(WindowPanel.Open));

            return root;
        }
    }
}
