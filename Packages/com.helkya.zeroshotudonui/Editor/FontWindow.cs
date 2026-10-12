
using System.IO;
using TMPro;
using UnityEditor;
using UnityEngine;

// EDT-06
namespace ZeroShotUdonUI.EditorTools
{
    /// <summary>
    /// ツールキット全体で使うフォントアセット（ZS_Font）の中身を生成・更新する画面。
    /// フォントを変えたいときは、元のフォントを選び直して生成し直す。
    /// </summary>
    public class FontWindow : EditorWindow
    {
        private static readonly string[] AtlasSizeLabels =
        {
            "2048 x 2048（メモリ約 4MB）",
            "4096 x 2048（メモリ約 8MB）",
            "4096 x 4096（メモリ約 16MB）",
        };

        private static readonly Vector2Int[] AtlasSizes =
        {
            new Vector2Int(2048, 2048),
            new Vector2Int(4096, 2048),
            new Vector2Int(4096, 4096),
        };

        private const int DefaultAtlasSizeIndex = 1;

        private Font sourceFont;
        private int atlasSizeIndex = DefaultAtlasSizeIndex;
        private string extraCharacters = "";
        private Vector2 scrollPosition;

        [MenuItem("Tools/ZeroShot Udon UI/フォント設定")]
        private static void Open()
        {
            FontWindow window = GetWindow<FontWindow>(false, "フォント設定");
            window.minSize = new Vector2(420f, 520f);
            window.Show();
        }

        private void OnEnable()
        {
            // 前回の生成に使ったフォントを初期値にする。無ければ、取得済みの Noto Sans JP、TextMeshPro 標準のフォントの順で選ぶ。
            TMP_FontAsset fontAsset = FontGenerator.LoadFontAsset();
            if (fontAsset != null && !FontGenerator.IsEmpty(fontAsset))
                sourceFont = FontGenerator.GetSourceFont(fontAsset);

            if (sourceFont == null)
                sourceFont = AssetDatabase.LoadAssetAtPath<Font>(FontGenerator.NotoFontPath);

            if (sourceFont == null)
                sourceFont = FontGenerator.LoadBuiltInFont();

            if (File.Exists(FontGenerator.ExtraCharactersPath))
                extraCharacters = File.ReadAllText(FontGenerator.ExtraCharactersPath);

            // 画像の大きさは、前回の生成で選んだものを初期値にする。
            if (ToolkitSettings.TryGetFont(out Font _, out int savedWidth, out int savedHeight))
            {
                int savedIndex = System.Array.IndexOf(AtlasSizes, new Vector2Int(savedWidth, savedHeight));
                if (savedIndex >= 0)
                    atlasSizeIndex = savedIndex;
            }
        }

        private void OnGUI()
        {
            scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);

            EditorGUILayout.HelpBox(
                "ツールキットのラベルは、すべて共通のフォントアセット（ZS_Font）を使います。\n"
                + "ここで生成し直すと、プレハブや配置済みの UI のフォントがまとめて切り替わります。",
                MessageType.Info);

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("現在のフォントアセット", EditorStyles.boldLabel);
            DrawCurrentState();

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("日本語フォントの取得", EditorStyles.boldLabel);
            DrawNotoSection();

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("生成の設定", EditorStyles.boldLabel);

            sourceFont = (Font)EditorGUILayout.ObjectField(
                new GUIContent("元のフォント", "ttf または otf のフォントファイル"), sourceFont, typeof(Font), false);
            atlasSizeIndex = EditorGUILayout.Popup(
                new GUIContent("画像の大きさ", "大きいほど文字がきれいになるが、ワールドの容量とメモリが増える"), atlasSizeIndex, AtlasSizeLabels);

            EditorGUILayout.Space();
            EditorGUILayout.LabelField(new GUIContent("追加で収録する文字",
                "かな・英数字・記号と常用漢字は、元のフォントにあれば常に収録される。それ以外で使いたい文字を、そのまま並べて書く"));
            extraCharacters = EditorGUILayout.TextArea(extraCharacters, GUILayout.MinHeight(60f));

            EditorGUILayout.Space();
            using (new EditorGUI.DisabledScope(sourceFont == null))
            {
                if (GUILayout.Button("生成", GUILayout.Height(28f)))
                    Generate();
            }

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("配布の準備", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("フォントは再配布しないので、配布物を作る前や git に記録する前に、フォントアセットを空の状態に戻します。", MessageType.None);
            if (GUILayout.Button("空の状態に戻す"))
                ResetToEmpty();

            EditorGUILayout.EndScrollView();
        }

        private static void DrawCurrentState()
        {
            TMP_FontAsset fontAsset = FontGenerator.LoadFontAsset();
            if (fontAsset == null)
            {
                EditorGUILayout.HelpBox("フォントアセットがありません。「生成」を押すと作成されます。", MessageType.Warning);
                return;
            }

            using (new EditorGUI.DisabledScope(true))
                EditorGUILayout.ObjectField("アセット", fontAsset, typeof(TMP_FontAsset), false);

            if (FontGenerator.IsEmpty(fontAsset))
            {
                EditorGUILayout.HelpBox("空の状態です。文字は TextMeshPro の既定のフォントで表示されます。", MessageType.Warning);
                return;
            }

            Font currentSource = FontGenerator.GetSourceFont(fontAsset);
            EditorGUILayout.LabelField("元のフォント", currentSource != null ? currentSource.name : "（不明）");
            EditorGUILayout.LabelField("収録した文字", fontAsset.characterTable.Count.ToString());
            EditorGUILayout.LabelField("文字の大きさ", fontAsset.faceInfo.pointSize.ToString());
            EditorGUILayout.LabelField("画像の大きさ", fontAsset.atlasWidth + " x " + fontAsset.atlasHeight);
        }

        private void DrawNotoSection()
        {
            if (File.Exists(FontGenerator.NotoFontPath))
            {
                EditorGUILayout.HelpBox("Noto Sans JP は取得済みです。「元のフォント」に選んで生成すると、日本語を表示できます。", MessageType.None);
                return;
            }

            EditorGUILayout.HelpBox(
                "日本語を表示するには、日本語のフォントが必要です。ツールキットにはフォントを同梱していないので、ここから取得します。\n\n"
                + "フォント: Noto Sans JP Medium（約 4.5MB）\n"
                + "入手元: " + FontGenerator.NotoSourcePage + "\n"
                + "ライセンス: SIL Open Font License 1.1（ライセンス文書も一緒に取得します）",
                MessageType.None);

            if (GUILayout.Button("Noto Sans JP をダウンロード"))
                DownloadNoto();
        }

        private void DownloadNoto()
        {
            bool succeeded = FontGenerator.DownloadNoto(out string report);
            if (succeeded)
                sourceFont = AssetDatabase.LoadAssetAtPath<Font>(FontGenerator.NotoFontPath);

            ShowResult(report);
        }

        private void Generate()
        {
            // 追加の文字はファイルに保存する。次に生成し直すときも同じ文字が収録される。
            PrefabGenerator.EnsureFolder(FontGenerator.UserFontFolder);
            File.WriteAllText(FontGenerator.ExtraCharactersPath, extraCharacters);
            AssetDatabase.ImportAsset(FontGenerator.ExtraCharactersPath);

            Vector2Int atlasSize = AtlasSizes[atlasSizeIndex];
            bool succeeded = FontGenerator.Generate(sourceFont, atlasSize.x, atlasSize.y, out string report);

            // ツールキットを更新してフォントアセットが空に戻ったときに、同じ設定で作り直せるよう控えておく（EDT-07）。
            if (succeeded)
                ToolkitSettings.SaveFont(sourceFont, atlasSize.x, atlasSize.y);

            ShowResult(report);
        }

        private void ResetToEmpty()
        {
            bool confirmed = EditorUtility.DisplayDialog(UIBuilder.DisplayName,
                "フォントアセットを空の状態に戻しますか？\n文字は TextMeshPro の既定のフォントで表示されるようになります。「生成」でいつでも元に戻せます。",
                "戻す", "キャンセル");
            if (!confirmed)
                return;

            FontGenerator.ResetToEmpty(out string report);
            ShowResult(report);
        }

        private void ShowResult(string report)
        {
            EditorUtility.DisplayDialog(UIBuilder.DisplayName, report, "OK");
            Repaint();

            // ダイアログを出した後は、描画途中の GUI を続けられないので打ち切る。
            GUIUtility.ExitGUI();
        }
    }
}
