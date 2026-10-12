
using UnityEditor;
using UnityEngine;

// EDT-02
namespace ZeroShotUdonUI.EditorTools
{
    /// <summary>
    /// 共通マテリアルの色・角丸・枠線をまとめて編集する画面。
    /// 値はマテリアルが持ち、この画面は開くたびにマテリアルの現在の値を表示する（FND-04）。
    /// 変えた値は、ツールキットを更新しても元に戻せるよう、パッケージの外にも控えを保存する（EDT-07）。
    /// </summary>
    public class ThemeWindow : EditorWindow
    {
        private const string UndoName = "Change ZeroShot Udon UI Theme";
        private const float MaxRadius = 80f;
        private const float MaxBorderWidth = 8f;

        [MenuItem("Tools/ZeroShot Udon UI/テーマ設定")]
        private static void Open()
        {
            ThemeWindow window = GetWindow<ThemeWindow>(false, "テーマ設定");
            window.minSize = new Vector2(360f, 380f);
            window.Show();
        }

        private void OnEnable()
        {
            // 取り消しでマテリアルの値が戻ったときに、表示も追従させる。
            Undo.undoRedoPerformed += OnUndoRedo;
        }

        private void OnDisable()
        {
            Undo.undoRedoPerformed -= OnUndoRedo;
        }

        private void OnUndoRedo()
        {
            // 取り消しで戻った値を、控えにも反映する。食い違ったままだと、後で控えの値に書き戻されてしまう。
            ToolkitSettings.SaveTheme();
            Repaint();
        }

        private void OnGUI()
        {
            Material surface = PrefabGenerator.GetMaterial(PrefabGenerator.MaterialRole.Surface);
            Material accent = PrefabGenerator.GetMaterial(PrefabGenerator.MaterialRole.Accent);
            Material track = PrefabGenerator.GetMaterial(PrefabGenerator.MaterialRole.Track);
            Material knob = PrefabGenerator.GetMaterial(PrefabGenerator.MaterialRole.Knob);
            Material outline = PrefabGenerator.GetMaterial(PrefabGenerator.MaterialRole.Outline);

            if (surface == null || accent == null || track == null || knob == null || outline == null)
            {
                EditorGUILayout.HelpBox("共通マテリアルを用意できませんでした。Console にシェーダーのエラーが出ていないか確認してください。", MessageType.Error);
                return;
            }

            EditorGUILayout.HelpBox("値を変えると共通マテリアルに書き込まれ、シーン上の UI にすぐ反映されます。Ctrl+Z で取り消せます。", MessageType.Info);

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("アクセント", EditorStyles.boldLabel);

            // アクセント色は、塗り用と枠線用の2つのマテリアルに同じ色を書き込む。
            if (DrawColor("アクセント色", accent.GetColor(PrefabGenerator.ColorProperty), false, out Color accentColor))
            {
                accentColor.a = 1f;
                Undo.RecordObjects(new Object[] { accent, outline }, UndoName);
                accent.SetColor(PrefabGenerator.ColorProperty, accentColor);
                outline.SetColor(PrefabGenerator.BorderColorProperty, accentColor);
                outline.SetColor(PrefabGenerator.ColorProperty, new Color(accentColor.r, accentColor.g, accentColor.b, 0f));
                MarkChanged(accent, outline);
            }

            DrawFloat("枠線のみのボタンの線の太さ", outline, PrefabGenerator.BorderWidthProperty, 0.5f, MaxBorderWidth);

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("パネル", EditorStyles.boldLabel);
            DrawColor("パネルの色", surface, PrefabGenerator.ColorProperty, true);
            DrawFloat("角丸の半径", surface, PrefabGenerator.RadiusProperty, 0f, MaxRadius);
            DrawColor("枠線の色", surface, PrefabGenerator.BorderColorProperty, true);
            DrawFloat("枠線の太さ", surface, PrefabGenerator.BorderWidthProperty, 0f, MaxBorderWidth);

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("操作部品", EditorStyles.boldLabel);
            DrawColor("土台の色", track, PrefabGenerator.ColorProperty, false);
            DrawColor("つまみの色", knob, PrefabGenerator.ColorProperty, false);

            EditorGUILayout.Space();
            if (GUILayout.Button("初期値に戻す"))
            {
                bool confirmed = EditorUtility.DisplayDialog(UIBuilder.DisplayName,
                    "すべての項目を初期値に戻しますか？", "戻す", "キャンセル");
                if (confirmed)
                {
                    Undo.RecordObjects(new Object[] { surface, accent, track, knob, outline }, UndoName);
                    PrefabGenerator.ApplyDefaultStyle(surface, PrefabGenerator.MaterialRole.Surface);
                    PrefabGenerator.ApplyDefaultStyle(accent, PrefabGenerator.MaterialRole.Accent);
                    PrefabGenerator.ApplyDefaultStyle(track, PrefabGenerator.MaterialRole.Track);
                    PrefabGenerator.ApplyDefaultStyle(knob, PrefabGenerator.MaterialRole.Knob);
                    PrefabGenerator.ApplyDefaultStyle(outline, PrefabGenerator.MaterialRole.Outline);
                    MarkChanged(surface, accent, track, knob, outline);
                }
            }
        }

        // マテリアルの色を1つ編集する欄を描く。
        private static void DrawColor(string label, Material material, string property, bool showAlpha)
        {
            if (!DrawColor(label, material.GetColor(property), showAlpha, out Color color))
                return;

            if (!showAlpha)
                color.a = 1f;

            Undo.RecordObject(material, UndoName);
            material.SetColor(property, color);
            MarkChanged(material);
        }

        // 色の入力欄を描き、変更されたら true を返す。
        private static bool DrawColor(string label, Color current, bool showAlpha, out Color result)
        {
            EditorGUI.BeginChangeCheck();
            result = EditorGUILayout.ColorField(new GUIContent(label), current, true, showAlpha, false);
            return EditorGUI.EndChangeCheck();
        }

        // マテリアルの数値を1つ編集する欄を描く。
        private static void DrawFloat(string label, Material material, string property, float min, float max)
        {
            EditorGUI.BeginChangeCheck();
            float value = EditorGUILayout.Slider(label, material.GetFloat(property), min, max);
            if (!EditorGUI.EndChangeCheck())
                return;

            Undo.RecordObject(material, UndoName);
            material.SetFloat(property, value);
            MarkChanged(material);
        }

        // 変更をファイルに保存する対象として記録し、控えを更新して、シーンビューとゲームビューを描き直す。
        private static void MarkChanged(params Material[] materials)
        {
            foreach (Material material in materials)
                EditorUtility.SetDirty(material);

            ToolkitSettings.SaveTheme();
            UnityEditorInternal.InternalEditorUtility.RepaintAllViews();
        }
    }
}
