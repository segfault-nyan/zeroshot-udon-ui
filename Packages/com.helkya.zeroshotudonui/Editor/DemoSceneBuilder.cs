
using System.IO;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using ZeroShotUdonUI.Samples;

// EDT-04
namespace ZeroShotUdonUI.EditorTools
{
    /// <summary>
    /// 全部品を並べた見本のシーンを、コードから生成する。
    /// 展示の内容を変えたいときは、ここを書き換えてメニューから生成し直す。
    /// </summary>
    internal static class DemoSceneBuilder
    {
        // 生成したシーンは利用者ごとのファイルなので、パッケージの外に置く。
        private const string SceneFolder = UIBuilder.UserFolder + "/Demo";
        private const string ScenePath = SceneFolder + "/ZS_Demo.unity";

        // VRChat のワールドとして動かすための設定（スポーン地点など）を持つ、SDK 同梱のプレハブ。
        private const string WorldPrefabPath = "Packages/com.vrchat.worlds/Samples/UdonExampleScene/Prefabs/VRCWorld.prefab";

        // 展示の位置（メートル）。スポーン地点は原点で、+Z を向いている。左に部品一覧、右に設定ウィンドウを置く。
        private static readonly Vector3 GalleryPosition = new Vector3(-0.75f, 1.3f, 1.6f);
        private static readonly Vector3 SettingsPosition = new Vector3(0.65f, 1.3f, 1.6f);

        // Canvas 単位。1 単位 = 1mm。部品一覧は、見出しの下に幅 480 の列を2つ並べる。
        private static readonly Vector2 GallerySize = new Vector2(1080f, 1000f);
        private static readonly Vector2 GalleryTitlePosition = new Vector2(0f, 436f);
        private static readonly Vector2 GalleryTitleSize = new Vector2(1000f, 48f);
        private static readonly Vector2 ColumnSize = new Vector2(480f, 848f);
        private static readonly Vector2 LeftColumnPosition = new Vector2(-260f, -36f);
        private static readonly Vector2 RightColumnPosition = new Vector2(260f, -36f);
        private static readonly Vector2 ButtonSize = new Vector2(148f, 56f);

        [MenuItem("Tools/ZeroShot Udon UI/デモシーンを生成")]
        private static void Generate()
        {
            if (!UIBuilder.EnsureSetup(typeof(SampleController)))
                return;

            GameObject worldPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(WorldPrefabPath);
            if (worldPrefab == null)
            {
                EditorUtility.DisplayDialog(UIBuilder.DisplayName,
                    "VRChat SDK のプレハブが見つかりません。\n" + WorldPrefabPath, "OK");
                return;
            }

            if (File.Exists(ScenePath))
            {
                bool overwrite = EditorUtility.DisplayDialog(UIBuilder.DisplayName,
                    "デモシーンが既にあります。作り直して上書きしますか？\nシーンに手で加えた変更は失われます。\n" + ScenePath,
                    "上書きする", "キャンセル");
                if (!overwrite)
                    return;
            }

            // 新しいシーンに切り替える前に、編集中のシーンに未保存の変更があれば保存するか尋ねる。
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                return;

            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);

            PrefabUtility.InstantiatePrefab(worldPrefab);

            GameObject floor = GameObject.CreatePrimitive(PrimitiveType.Plane);
            floor.name = "Floor";
            floor.transform.localScale = new Vector3(2f, 1f, 2f);

            BuildGallery(GalleryPosition, FaceSpawn(GalleryPosition));
            SampleBuilder.BuildSettingsSample(SettingsPosition, FaceSpawn(SettingsPosition));

            PrefabGenerator.EnsureFolder(SceneFolder);
            if (!EditorSceneManager.SaveScene(scene, ScenePath))
            {
                EditorUtility.DisplayDialog(UIBuilder.DisplayName, "デモシーンの保存に失敗しました。\n" + ScenePath, "OK");
                return;
            }

            EditorUtility.DisplayDialog(UIBuilder.DisplayName, "デモシーンを生成しました。\n" + ScenePath, "OK");
        }

        // 原点のスポーン地点から見て、展示が正面を向く向きを返す。
        private static Quaternion FaceSpawn(Vector3 position)
        {
            Vector3 direction = new Vector3(position.x, 0f, position.z);
            return Quaternion.LookRotation(direction.normalized, Vector3.up);
        }

        /// <summary>
        /// 全部品を種類ごとに並べたパネルを作る。見た目と操作感の確認用で、通知先は設定しない。
        /// </summary>
        private static void BuildGallery(Vector3 position, Quaternion rotation)
        {
            GameObject canvas = UIBuilder.CreateCanvas("Component Gallery", GallerySize);
            UIBuilder.PlaceInWorld(canvas, position, rotation);
            UIBuilder.CreatePanel(canvas.transform);

            TextMeshProUGUI title = UIBuilder.CreateLabel(canvas.transform, "Components", LabelStyle.Heading);
            UIBuilder.Place(title, GalleryTitlePosition, GalleryTitleSize);

            RectTransform left = UIBuilder.CreateStack(canvas.transform, StackDirection.Vertical);
            UIBuilder.Place(left, LeftColumnPosition, ColumnSize);

            RectTransform right = UIBuilder.CreateStack(canvas.transform, StackDirection.Vertical);
            UIBuilder.Place(right, RightColumnPosition, ColumnSize);

            UIBuilder.CreateLabel(left, "Buttons", LabelStyle.Caption);
            RectTransform buttons = UIBuilder.CreateStack(left, StackDirection.Horizontal);
            UIBuilder.SetSize(UIBuilder.CreateButton(buttons, "Primary", ButtonStyle.Primary), ButtonSize);
            UIBuilder.SetSize(UIBuilder.CreateButton(buttons, "Secondary", ButtonStyle.Secondary), ButtonSize);
            UIBuilder.SetSize(UIBuilder.CreateButton(buttons, "Outline", ButtonStyle.Outline), ButtonSize);

            UIBuilder.CreateLabel(left, "Toggle Switch", LabelStyle.Caption);
            RectTransform toggles = UIBuilder.CreateStack(left, StackDirection.Horizontal);
            UIBuilder.CreateToggle(toggles, false);
            UIBuilder.CreateToggle(toggles, true);

            UIBuilder.CreateLabel(left, "Slider", LabelStyle.Caption);
            UIBuilder.CreateSlider(left, 0f, 1f, 0.5f);

            UIBuilder.CreateLabel(left, "Segmented Control", LabelStyle.Caption);
            UIBuilder.CreateSegmented(left, new[] { "On", "Off" }, 0);
            UIBuilder.CreateSegmented(left, new[] { "Low", "Medium", "High" }, 1);

            // アイコンは全種類を1行に並べる。横の自動配置の高さは、中身に合わせて指定する。
            UIBuilder.CreateLabel(left, "Icons", LabelStyle.Caption);
            RectTransform icons = UIBuilder.CreateStack(left, StackDirection.Horizontal);
            UIBuilder.SetSize(icons, new Vector2(ColumnSize.x, 32f));
            foreach (IconType icon in System.Enum.GetValues(typeof(IconType)))
                UIBuilder.CreateIcon(icons, icon);

            RectTransform iconButtons = UIBuilder.CreateStack(left, StackDirection.Horizontal);
            UIBuilder.SetSize(iconButtons, new Vector2(ColumnSize.x, 48f));
            UIBuilder.CreateIconButton(iconButtons, IconType.Plus);
            UIBuilder.CreateIconButton(iconButtons, IconType.Minus);
            UIBuilder.CreateIconButton(iconButtons, IconType.Menu);
            UIBuilder.CreateIconButton(iconButtons, IconType.Close);

            UIBuilder.CreateLabel(left, "Stepper", LabelStyle.Caption);
            UIBuilder.CreateStepper(left, 0f, 10f, 1f, 3f);

            UIBuilder.CreateLabel(left, "Progress Bar", LabelStyle.Caption);
            UIBuilder.CreateProgressBar(left, 0.6f);

            UIBuilder.CreateLabel(right, "Setting Row", LabelStyle.Caption);
            ToggleSwitch rowToggle = UIBuilder.CreateToggle(right, true);
            UIBuilder.CreateSettingRow(right, "Label", rowToggle);

            UIBuilder.CreateLabel(right, "Checkbox", LabelStyle.Caption);
            UIBuilder.CreateCheckbox(right, "Unchecked", false);
            UIBuilder.CreateCheckbox(right, "Checked", true);

            UIBuilder.CreateLabel(right, "Labels", LabelStyle.Caption);
            UIBuilder.CreateLabel(right, "Heading", LabelStyle.Heading);
            UIBuilder.CreateLabel(right, "Body", LabelStyle.Body);
            UIBuilder.CreateLabel(right, "Caption", LabelStyle.Caption);

            // 日本語の表示例。共通フォントを Noto Sans JP から生成していない場合、エディタ上では □ になる（FND-06）。
            // 2行目は常用漢字に無い字を含む。エディタ上では □ になり、VRChat 上では VRChat の補完で表示される。
            UIBuilder.CreateLabel(right, "Japanese", LabelStyle.Caption);
            UIBuilder.CreateLabel(right, "設定、音量、画質を調整する", LabelStyle.Body);
            UIBuilder.CreateLabel(right, "範囲外の字：薔薇、檸檬、醤油", LabelStyle.Body);
        }
    }
}
