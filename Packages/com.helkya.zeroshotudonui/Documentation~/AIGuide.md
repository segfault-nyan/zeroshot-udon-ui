# AI エージェント向け利用ガイド

この文書は、VRChat ワールドの Unity プロジェクトで作業する AI エージェントが、ZeroShot Udon UI を使って UI を作るための手引きです。人が読む導入手順は [README](../README.md)、各部品の詳細は[部品リファレンス](Reference.md)にあります。

## 前提

- あなたは Unity の画面を操作できません。**UI は、エディタ用のコードを書いて組み立て、利用者に Unity のメニューから実行してもらいます。**
- UI を組み立てる関数は、`ZeroShotUdonUI.EditorTools.UIBuilder` にまとまっています。
- ツールキット本体は `Packages/com.helkya.zeroshotudonui/` にあります。**この中のファイルは書き換えないでください。** 更新のたびに、丸ごと置き換わります。
- あなたが書くファイルは、`Assets/` の下に置きます。

## VRChat 固有の制約

UI を作るときに、必ず守ることです。`UIBuilder` を使えば、1 から 3 は自動で満たされます。

1. UI は uGUI と TextMeshPro で作ります。Unity の UI Toolkit（UIElements）は、VRChat ワールドでは動きません。
2. Canvas は、ワールド空間、`Default` レイヤー、`VRCUiShape` と当たり判定つきにします。
3. uGUI の `Button.onClick` などから直接呼べる処理は限られます。部品は、`UdonBehaviour.SendCustomEvent` を経由して Udon に操作を伝えます。
4. 実行時のスクリプトは、UdonSharp（`UdonSharpBehaviour`）で書きます。通常の C# より、使える機能と API が少ないことに注意します。
5. 一度も有効になっていないオブジェクトのスクリプトは、初期化（`Start`）と、VRChat が呼び出すイベント（`OnPlayerJoined` など）が実行されません。ほかのスクリプトからの呼び出しと、部品からの通知は受け取れます。操作を受け取るスクリプトは、常に有効なオブジェクトに付けます。ウィンドウの中には付けません。
6. Canvas やウィンドウを、別の Canvas の中に置きません。縮尺が二重に掛かります。
7. 部品は同期しません。操作の結果は、操作した本人にだけ反映されます。

## 作業の流れ

1. 操作を受け取る UdonSharp スクリプトを書く（操作を受け取らない UI なら不要）。
2. UI を組み立てるエディタ用スクリプトを書く。`Editor` という名前のフォルダの中に置く。
3. 利用者に、Unity に戻ってメニューを実行するよう依頼する。
4. 初回は、準備のための案内（ダイアログ）が出て、処理が止まることがある。利用者に、案内の処理が終わってから、もう一度メニューを実行してもらう。

## 最小の例

設定ウィンドウを 1 枚作り、トグルスイッチとスライダーの操作を受け取る例です。

### 操作を受け取るスクリプト

`Assets/MyWorld/SettingsController.cs`

```csharp
using UdonSharp;
using UnityEngine;
using ZeroShotUdonUI;

[UdonBehaviourSyncMode(BehaviourSyncMode.None)]
public class SettingsController : UdonSharpBehaviour
{
    [SerializeField] private ToggleSwitch musicToggle;
    [SerializeField] private SliderControl volumeSlider;
    [SerializeField] private AudioSource music;

    // 部品の「イベント名」に設定した名前で呼ばれる。public にする。
    public void OnMusicToggled()
    {
        music.mute = !musicToggle.IsOn;
    }

    public void OnVolumeChanged()
    {
        music.volume = volumeSlider.Value;
    }
}
```

### UI を組み立てるスクリプト

`Assets/MyWorld/Editor/SettingsUIBuilder.cs`

```csharp
using UdonSharpEditor;
using UnityEditor;
using UnityEngine;
using ZeroShotUdonUI;
using ZeroShotUdonUI.EditorTools;

public static class SettingsUIBuilder
{
    [MenuItem("Tools/My World/設定ウィンドウを作る")]
    private static void Build()
    {
        // 最初に必ず呼ぶ。false が返ったら、準備の処理が始まっているので何もせず終わる。
        // 自分の UdonSharp スクリプトの型を渡すと、必要な U# プログラムアセットも用意される。
        if (!UIBuilder.EnsureSetup(typeof(SettingsController)))
            return;

        // 操作を受け取るスクリプトは、ウィンドウの外の、常に有効なオブジェクトに付ける。
        GameObject root = new GameObject("Settings UI");
        SettingsController controller = root.AddUdonSharpComponent<SettingsController>();

        // 大きさの単位は mm。幅 600、高さ 400 のウィンドウを、スポーン地点の正面に置く。
        WindowPanel window = UIBuilder.CreateWindow("Settings Window", "Settings", new Vector2(600f, 400f), out RectTransform content);
        window.transform.SetParent(root.transform, false);
        UIBuilder.PlaceInFrontOfSpawn(window.gameObject);

        // content は縦の自動配置。置いた順に上から並ぶので、位置の指定は要らない。
        ToggleSwitch musicToggle = UIBuilder.CreateToggle(content, true);
        UIBuilder.CreateSettingRow(content, "Music", musicToggle);

        SliderControl volumeSlider = UIBuilder.CreateSlider(content, 0f, 1f, 0.5f);
        UIBuilder.CreateSettingRow(content, "Volume", volumeSlider);

        // 通知先とイベント名を設定する。
        UIBuilder.SetEvent(musicToggle, controller, nameof(SettingsController.OnMusicToggled));
        UIBuilder.SetEvent(volumeSlider, controller, nameof(SettingsController.OnVolumeChanged));

        // 自分のスクリプトの欄に、部品を割り当てる。
        SerializedObject serialized = new SerializedObject(controller);
        serialized.FindProperty("musicToggle").objectReferenceValue = musicToggle;
        serialized.FindProperty("volumeSlider").objectReferenceValue = volumeSlider;
        serialized.ApplyModifiedPropertiesWithoutUndo();

        // 取り消し（Ctrl+Z）に登録する。シーンに未保存の変更があることも、これで記録される。
        Undo.RegisterCreatedObjectUndo(root, "Create Settings UI");
        Selection.activeGameObject = root;
    }
}
```

この例の `music`（AudioSource）は、コードでは割り当てていません。シーンにある対象を探して割り当てるか、利用者に Inspector で設定してもらいます。

### 利用者への依頼の例

> Unity に戻り、メニューの `Tools > My World > 設定ウィンドウを作る` を実行してください。「U# プログラムアセットを作成しました」などの案内が出た場合は、コンパイルが終わってから、もう一度同じメニューを実行してください。

## UIBuilder の関数

名前空間は `ZeroShotUdonUI.EditorTools` です。エディタ専用で、実行時のスクリプト（UdonSharp）からは呼べません。

### 準備

| 関数 | 内容 |
|---|---|
| `bool EnsureSetup(params Type[] extraBehaviours)` | UI の生成に必要なものを用意する。**ほかの関数より先に呼ぶ。** 準備のために取り込みやコンパイルを始めたときは、案内を表示して `false` を返す。その場合は何もせずに終わり、後でもう一度実行してもらう。`extraBehaviours` には、自分の `UdonSharpBehaviour` の型を渡す |

### 容れ物

| 関数 | 内容 |
|---|---|
| `WindowPanel CreateWindow(string name, string title, Vector2 size, out RectTransform content)` | タイトルと閉じるボタンのついたウィンドウを置く。部品は `content` の中に置く |
| `GameObject CreatePanelCanvas(string name, Vector2 size, out RectTransform content)` | 背景のパネルつきの Canvas を置く。部品は `content` の中に置く |
| `GameObject CreateCanvas(string name, Vector2 size)` | 空の Canvas を置く。背景も中身の領域も無い。自由に配置したいときに使う |
| `Image CreatePanel(Transform parent)` | 親いっぱいに広がる背景の板を置く |
| `RectTransform CreateStack(Transform parent, StackDirection direction)` | 子を等間隔で並べる枠を置く。`Vertical` か `Horizontal` |
| `RectTransform CreateSettingRow(Transform parent, string label, Component control)` | 左に項目名、右に操作部品を置く行を置く。`control` には作成済みの部品を渡す。`null` なら項目名だけの行になる |
| `void AddToSettingRow(RectTransform row, Component control)` | 作成済みの部品を、設定行の右端に置く |

### 部品

| 関数 | 内容 |
|---|---|
| `ButtonControl CreateButton(Transform parent, string label, ButtonStyle style)` | ボタンを置く。`Primary`（主）、`Secondary`（副）、`Outline`（枠線のみ） |
| `ButtonControl CreateIconButton(Transform parent, IconType icon)` | アイコンだけの丸いボタンを置く |
| `ToggleSwitch CreateToggle(Transform parent, bool isOn)` | トグルスイッチを置く |
| `CheckboxControl CreateCheckbox(Transform parent, string label, bool isOn)` | チェックボックスを置く。枠の右に項目名がつく |
| `SliderControl CreateSlider(Transform parent, float minValue, float maxValue, float value)` | スライダーを置く。右端に現在値の表示がつく |
| `StepperControl CreateStepper(Transform parent, float minValue, float maxValue, float step, float value)` | 数値ステッパーを置く。＋と－のボタンで、値を `step` ずつ増減する。`step` が小数なら、表示も小数になる |
| `SegmentedControl CreateSegmented(Transform parent, string[] options, int selectedIndex)` | 選択ボタン群を置く。`options` は 2 つ以上 |
| `ProgressBar CreateProgressBar(Transform parent, float value)` | 進捗バーを置く。`value` は 0 から 1。操作はできない |
| `TextMeshProUGUI CreateLabel(Transform parent, string text, LabelStyle style)` | ラベルを置く。`Heading`（見出し）、`Body`（本文）、`Caption`（補足） |
| `RawImage CreateIcon(Transform parent, IconType icon)` | アイコンを置く |
| `void SetIcon(RawImage image, IconType icon)` | アイコンの種類を変える |

`IconType` は、`Close`、`Check`、`Plus`、`Minus`、`ChevronUp`、`ChevronDown`、`ChevronLeft`、`ChevronRight`、`Menu`、`Info` の 10 種類です。

### イベント

| 関数 | 内容 |
|---|---|
| `void SetEvent(ControlBase control, UdonSharpBehaviour target, string eventName)` | 部品が操作されたときの通知先と、呼び出すメソッドの名前を設定する。`target` に `UdonBehaviour` を取る形もある |

`ButtonControl`、`ToggleSwitch`、`CheckboxControl`、`SliderControl`、`StepperControl`、`SegmentedControl`、`WindowPanel` は、どれも `ControlBase` です。`ProgressBar` は操作できない部品なので、`ControlBase` ではなく、イベントも設定できません。

### 位置と大きさ

| 関数 | 内容 |
|---|---|
| `void PlaceInFrontOfSpawn(GameObject canvasRoot)` | Canvas やウィンドウを、スポーン地点の正面 1.5m、高さ 1.3m に置く |
| `void GetPoseInFrontOfSpawn(out Vector3 position, out Quaternion rotation)` | 上の位置と向きを返す |
| `void PlaceInWorld(GameObject canvasRoot, Vector3 position, Quaternion rotation)` | Canvas やウィンドウを、ワールド内の位置（m）と向きで置く |
| `void SetSize(Component element, Vector2 size)` | 要素の大きさ（mm）を変える。自動配置の中の要素に使う |
| `void Place(Component element, Vector2 position)` | 要素の位置を、親の中央からのずれ（mm）で指定する。自動配置の外の要素に使う |
| `void Place(Component element, Vector2 position, Vector2 size)` | 位置と大きさを指定する |

## 配置の考え方

- 大きさと、Canvas の中での位置の単位は mm です。ワールド内での位置の単位は m です。
- **基本は、`CreateWindow` か `CreatePanelCanvas` の `content` に、部品を順に置きます。** `content` は縦の自動配置なので、上から順に並びます。
- 縦の自動配置の中では、ボタン、アイコンボタン、トグルスイッチ、数値ステッパー、アイコンは幅を保って左に寄ります。設定行、チェックボックス、スライダー、選択ボタン群、進捗バー、ラベル、自動配置は、幅いっぱいに広がります。
- ボタンを横に並べたいときは、`CreateStack(content, StackDirection.Horizontal)` を置き、その中にボタンを置きます。
- 「項目名 + 操作部品」の形にしたいときは、部品を作ってから `CreateSettingRow` に渡します。
- `content` の高さを超えた部品は、下にはみ出します。スクロールはまだありません。ウィンドウの高さは、中身に合わせて決めます。目安は、設定行 1 つにつき 80（行の高さ 64 + 間隔 16）、それにタイトルと余白の分として 140 を足します。
- 自由に配置したいときは、`CreateCanvas` と `CreatePanel` で土台を作り、部品を Canvas の直下に置いて `Place` で位置を決めます。位置は、親の中央が原点、右と上が正です。
- ツールキット以外のオブジェクトを縦の自動配置に入れると、幅が 0 になります。入れる場合は、`LayoutElement` を付けて幅を指定します。

## 実行時に部品を使う

UdonSharp のスクリプトから、部品の値を読み書きできます。名前空間は `ZeroShotUdonUI` です。

| 部品 | 読む | 変える |
|---|---|---|
| `ToggleSwitch` | `bool IsOn` | `SetValue(bool)`、`SetValueWithoutNotify(bool)` |
| `CheckboxControl` | `bool IsOn` | `SetValue(bool)`、`SetValueWithoutNotify(bool)` |
| `SliderControl` | `float Value` | `SetValue(float)`、`SetValueWithoutNotify(float)` |
| `StepperControl` | `float Value` | `SetValue(float)`、`SetValueWithoutNotify(float)` |
| `SegmentedControl` | `int SelectedIndex`、`int Count` | `SetValue(int)`、`SetValueWithoutNotify(int)` |
| `ProgressBar` | `float Value`（0 から 1） | `SetValue(float)` |
| `WindowPanel` | `bool IsOpen` | `Open()`、`Close()`、`ToggleOpen()` |

- イベントは「変わった」ことだけを伝えます。値は、部品から読みます。
- `SetValue` は、値が実際に変わったときだけ、通知先にイベントを送ります。`SetValueWithoutNotify` は送りません。初期化や、同期で受け取った値の反映に使います。
- ウィンドウの開閉は、スクリプトを書かなくても設定できます。`UIBuilder.SetEvent(button, window, nameof(WindowPanel.Open))` のように、ウィンドウ自身を通知先にします。
- ウィンドウを開くためのボタンは、ウィンドウの外（別の Canvas）に置きます。
- 閉じているウィンドウや、表示していないページの中の部品も、値を読み書きできます。一度も表示していなくても構いません。
- `StepperControl` の値を整数として使うときは、`Mathf.RoundToInt(stepper.Value)` で読みます。
- `ProgressBar` には、進み具合が変わったときだけ `SetValue` を呼びます。毎フレーム呼ぶ使い方は避けます。
- アイコンの種類を実行時に変えるときは、`RawImage` の `uvRect` に `new Rect(番号 * 2f, 0f, 1f, 1f)` を入れます。番号は `IconType` の並び順（`Close` が 0）です。
- 名前が `_` で始まるメソッド（`_OnClick` など）は、部品の内部で使うものです。呼び出しません。

## やってはいけないこと

| してはいけないこと | 理由と、代わりにすること |
|---|---|
| `Packages/com.helkya.zeroshotudonui/` の中のファイルを書き換える | 更新で失われる。見た目を変えたいときは、置いた後の部品を書き換える |
| ツールキットのマテリアルを直接書き換える | 更新で失われる。色は、利用者に `Tools > ZeroShot Udon UI > テーマ設定` で変えてもらう。テーマを変える関数は、まだ無い |
| Canvas やウィンドウを、別の Canvas の子にする | 縮尺が二重に掛かり、見えなくなる |
| Canvas のレイヤーを `UI` にする | VRChat のメニューを開いている間しか操作できなくなる |
| 操作を受け取るスクリプトを、ウィンドウの中に付ける | ウィンドウを最初に開くまで、スクリプトの `Start` が実行されない。ウィンドウの外の、常に有効なオブジェクトに付ける |
| 再生中に `UIBuilder` の関数を呼ぶ | エディタで編集しているとき専用。`EnsureSetup` が案内を出して `false` を返す |

## 利用者に頼む必要があること

次のことは、コードからはできません。必要なときは、利用者に操作を依頼します。

| すること | 依頼する操作 |
|---|---|
| 日本語を表示する | `Tools > ZeroShot Udon UI > フォント設定` で、「Noto Sans JP をダウンロード」と「生成」を押す。済んでいない場合、日本語は □ になる |
| 色や角丸を変える | `Tools > ZeroShot Udon UI > テーマ設定` で変える |
| 動作を確かめる | Unity で再生するか、VRChat にアップロードして確かめる |

日本語フォントが生成済みかどうかは、`Assets/ZeroShotUdonUI/Settings.json` の `font.sourceFontPath` で確かめられます。このファイルが無いか、値が空なら、まだ生成されていません。

## まだ無い機能

次の部品と機能は、この版にはありません。必要な場合は、uGUI で自作するか、利用者に相談します。

- ドロップダウン、テキスト入力
- タブ、スクロール領域、確認ダイアログ
- 同期、効果音、開閉などのアニメーション
