
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

// EDT-07
namespace ZeroShotUdonUI.EditorTools
{
    /// <summary>
    /// 利用者が決めたテーマとフォントの設定を、パッケージの外のファイルに保存する。
    /// VCC でツールキットを更新すると、パッケージの中のマテリアルとフォントアセットは配布時の状態に戻る。
    /// それを検知して、保存しておいた設定から元に戻す。
    /// </summary>
    internal static class ToolkitSettings
    {
        internal const string SettingsPath = UIBuilder.UserFolder + "/Settings.json";

        // 値を比べるときに、同じとみなす差。保存と読み込みで生じる、ごく小さな誤差を無視するため。
        private const float Tolerance = 0.0005f;

        [System.Serializable]
        private class Data
        {
            public ThemeData theme = new ThemeData();
            public FontData font = new FontData();
        }

        [System.Serializable]
        private class ThemeData
        {
            // 空なら、テーマは保存されていない。
            public MaterialStyle[] materials = new MaterialStyle[0];
        }

        [System.Serializable]
        private class MaterialStyle
        {
            public string role;
            public Color color;
            public float radius;
            public float borderWidth;
            public Color borderColor;
        }

        [System.Serializable]
        private class FontData
        {
            // GUID が空なら、フォントの設定は保存されていない。パスは、GUID で見つからないときの予備。
            public string sourceFontGuid = "";
            public string sourceFontPath = "";
            public int atlasWidth;
            public int atlasHeight;
        }

        /// <summary>
        /// 共通マテリアルの現在の値を、テーマとして保存する。
        /// </summary>
        internal static void SaveTheme()
        {
            List<MaterialStyle> styles = new List<MaterialStyle>();
            foreach (PrefabGenerator.MaterialRole role in PrefabGenerator.ThemeRoles)
            {
                Material material = PrefabGenerator.LoadMaterial(role);
                if (material != null)
                    styles.Add(Capture(material, role));
            }

            Data data = Load();
            data.theme.materials = styles.ToArray();
            Save(data);
        }

        /// <summary>
        /// 共通マテリアルが配布時の値に戻っていたら、保存しておいたテーマを書き戻す。
        /// 配布時の値でも保存した値でもないマテリアルは、利用者が直接書き換えたものとみなして触らない。
        /// </summary>
        internal static void RestoreTheme()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling || EditorApplication.isUpdating)
                return;

            bool restored = false;
            foreach (MaterialStyle saved in Load().theme.materials)
            {
                if (!System.Enum.TryParse(saved.role, out PrefabGenerator.MaterialRole role))
                    continue;

                Material material = PrefabGenerator.LoadMaterial(role);
                if (material == null)
                    continue;

                MaterialStyle current = Capture(material, role);
                if (Matches(current, saved) || !Matches(current, GetDefault(material, role)))
                    continue;

                material.SetColor(PrefabGenerator.ColorProperty, saved.color);
                material.SetFloat(PrefabGenerator.RadiusProperty, saved.radius);
                material.SetFloat(PrefabGenerator.BorderWidthProperty, saved.borderWidth);
                material.SetColor(PrefabGenerator.BorderColorProperty, saved.borderColor);
                EditorUtility.SetDirty(material);
                AssetDatabase.SaveAssetIfDirty(material);
                restored = true;
            }

            if (!restored)
                return;

            UnityEditorInternal.InternalEditorUtility.RepaintAllViews();
            Debug.Log("[" + UIBuilder.DisplayName + "] 共通マテリアルが初期値に戻っていたので、保存しておいたテーマを書き戻しました。");
        }

        /// <summary>
        /// フォントアセットの生成に使った設定を保存する。
        /// </summary>
        internal static void SaveFont(Font sourceFont, int atlasWidth, int atlasHeight)
        {
            string path = AssetDatabase.GetAssetPath(sourceFont);

            Data data = Load();
            data.font.sourceFontGuid = AssetDatabase.AssetPathToGUID(path);
            data.font.sourceFontPath = path;
            data.font.atlasWidth = atlasWidth;
            data.font.atlasHeight = atlasHeight;
            Save(data);
        }

        /// <summary>
        /// 保存しておいたフォントの設定を返す。保存されていないか、元のフォントが見つからなければ false を返す。
        /// </summary>
        internal static bool TryGetFont(out Font sourceFont, out int atlasWidth, out int atlasHeight)
        {
            FontData font = Load().font;
            sourceFont = null;
            atlasWidth = font.atlasWidth;
            atlasHeight = font.atlasHeight;

            if (string.IsNullOrEmpty(font.sourceFontGuid) || atlasWidth <= 0 || atlasHeight <= 0)
                return false;

            sourceFont = AssetDatabase.LoadAssetAtPath<Font>(AssetDatabase.GUIDToAssetPath(font.sourceFontGuid));
            if (sourceFont == null)
                sourceFont = AssetDatabase.LoadAssetAtPath<Font>(font.sourceFontPath);

            return sourceFont != null;
        }

        private static MaterialStyle Capture(Material material, PrefabGenerator.MaterialRole role)
        {
            return new MaterialStyle
            {
                role = role.ToString(),
                color = material.GetColor(PrefabGenerator.ColorProperty),
                radius = material.GetFloat(PrefabGenerator.RadiusProperty),
                borderWidth = material.GetFloat(PrefabGenerator.BorderWidthProperty),
                borderColor = material.GetColor(PrefabGenerator.BorderColorProperty),
            };
        }

        // その役割のマテリアルの、配布時の値を返す。
        private static MaterialStyle GetDefault(Material material, PrefabGenerator.MaterialRole role)
        {
            Material temporary = new Material(material);
            PrefabGenerator.ApplyDefaultStyle(temporary, role);
            MaterialStyle style = Capture(temporary, role);
            Object.DestroyImmediate(temporary);
            return style;
        }

        private static bool Matches(MaterialStyle a, MaterialStyle b)
        {
            return Matches(a.color, b.color)
                && Matches(a.borderColor, b.borderColor)
                && Mathf.Abs(a.radius - b.radius) <= Tolerance
                && Mathf.Abs(a.borderWidth - b.borderWidth) <= Tolerance;
        }

        private static bool Matches(Color a, Color b)
        {
            return Mathf.Abs(a.r - b.r) <= Tolerance
                && Mathf.Abs(a.g - b.g) <= Tolerance
                && Mathf.Abs(a.b - b.b) <= Tolerance
                && Mathf.Abs(a.a - b.a) <= Tolerance;
        }

        private static Data Load()
        {
            Data data = null;

            if (File.Exists(SettingsPath))
            {
                try
                {
                    data = JsonUtility.FromJson<Data>(File.ReadAllText(SettingsPath));
                }
                catch (System.ArgumentException exception)
                {
                    Debug.LogWarning("[" + UIBuilder.DisplayName + "] 設定ファイルを読めないため、保存された設定を使いません: " + SettingsPath + "\n" + exception.Message);
                }
            }

            // 手で編集されて項目が欠けていても動くように、欠けた部分は空の設定で補う。
            if (data == null)
                data = new Data();
            if (data.theme == null)
                data.theme = new ThemeData();
            if (data.theme.materials == null)
                data.theme.materials = new MaterialStyle[0];
            if (data.font == null)
                data.font = new FontData();

            return data;
        }

        private static void Save(Data data)
        {
            PrefabGenerator.EnsureFolder(UIBuilder.UserFolder);
            File.WriteAllText(SettingsPath, JsonUtility.ToJson(data, true));
        }
    }

    /// <summary>
    /// プロジェクトを開いたときと、アセットが変わったときに、テーマとフォントが配布時の状態に戻っていないかを確かめる。
    /// </summary>
    [InitializeOnLoad]
    internal class AutoSetup : AssetPostprocessor
    {
        private static bool scheduled;

        static AutoSetup()
        {
            Schedule();
        }

        // アセットが取り込まれるたびに呼ばれる。既にあるファイルの中身だけが変わった場合も含む。
        private static void OnPostprocessAllAssets(string[] importedAssets, string[] deletedAssets, string[] movedAssets, string[] movedFromAssetPaths)
        {
            Schedule();
        }

        // アセットの取り込みの最中に書き換えを始めないよう、処理が落ち着いてから確かめる。
        private static void Schedule()
        {
            if (scheduled)
                return;

            scheduled = true;
            EditorApplication.delayCall += Run;
        }

        private static void Run()
        {
            scheduled = false;

            // 取り込みがまだ続いていたら、終わるまで待つ。
            if (EditorApplication.isUpdating)
            {
                Schedule();
                return;
            }

            ToolkitSettings.RestoreTheme();
            FontGenerator.AutoFill();
        }
    }
}
