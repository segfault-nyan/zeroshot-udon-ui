
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.TextCore.LowLevel;

// FND-06・EDT-06
namespace ZeroShotUdonUI.EditorTools
{
    /// <summary>
    /// ツールキット全体で使うフォントアセット（ZS_Font）の中身を、フォントファイルと文字の一覧から生成する。
    /// アセット自体は同じものを使い続けて中身だけを入れ替えるので、それを参照しているプレハブや
    /// 配置済みの UI は、手を加えずに新しいフォントに切り替わる。
    ///
    /// 配布物には、中身が空の ZS_Font だけを含める。フォントは再配布せず、利用者の環境で生成する。
    /// 英数字と記号は TextMeshPro の基本リソースのフォントから自動で生成し、
    /// 日本語は利用者がダウンロードした Noto Sans JP から生成する。
    /// </summary>
    internal static class FontGenerator
    {
        internal const string FontAssetPath = UIBuilder.RuntimeFolder + "/Fonts/ZS_Font.asset";

        // 利用者ごとのファイルの置き場所。ツールキットを更新しても消えないよう、パッケージの外に置く。
        internal const string UserFontFolder = UIBuilder.UserFolder + "/Fonts";
        internal const string ExtraCharactersPath = UserFontFolder + "/ExtraCharacters.txt";

        // 利用者がダウンロードする日本語フォント。
        internal const string NotoFontPath = UserFontFolder + "/NotoSansJP-Medium.otf";
        internal const string NotoLicensePath = UserFontFolder + "/NotoSansJP-LICENSE.txt";
        internal const string NotoSourcePage = "https://github.com/notofonts/noto-cjk";

        private const string JoyoKanjiPath = UIBuilder.PackageFolder + "/Editor/Fonts/JoyoKanji.txt";
        private const string AssetName = "ZS_Font";

        // 入手元の内容が後から変わっても同じファイルを取得できるよう、特定の版を指定する。
        // 取得したファイルは、内容のハッシュ値が下の値と一致するかを確かめてから使う。
        private const string NotoRevision = "f8d157532fbfaeda587e826d4cd5b21a49186f7c";
        private const string NotoBaseUrl = "https://raw.githubusercontent.com/notofonts/noto-cjk/" + NotoRevision + "/Sans/";
        private const string NotoFontSha256 = "f396a3b57256e4515be9cb41f7aac54766d654890082a9f1b5c2451b5c093d8a";
        private const string NotoLicenseSha256 = "6a73f9541c2de74158c0e7cf6b0a58ef774f5a780bf191f2d7ec9cc53efe2bf2";

        // TextMeshPro の基本リソースに含まれるフォント。利用者の手元に必ずあるので、英数字と記号の生成に使う。
        private const string BuiltInFontPath = "Assets/TextMesh Pro/Fonts/LiberationSans.ttf";
        private const int BuiltInAtlasSize = 1024;

        // 文字の周りに取る余白の、文字の大きさに対する割合。輪郭を滑らかに描くために必要。
        private const float PaddingRatio = 0.1f;

        // 画像に収まる文字の大きさを探すときの、下限と刻み幅。
        private const int MinSamplingSize = 20;
        private const int SamplingStep = 2;

        // 記号のうち、範囲でまとめて指定しにくいもの。
        private const string MiscSymbols = "℃○●□■△▲▽▼◎★☆♪−≠≦≧∞";

        // 「空の状態に戻す」を押した後、自動生成を止めていることを覚えておくための名前。エディタを終了すると消える。
        private const string AutoFillSuppressedKey = "ZeroShotUdonUI.FontAutoFillSuppressed";

        internal static TMP_FontAsset LoadFontAsset()
        {
            return AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontAssetPath);
        }

        internal static Font LoadBuiltInFont()
        {
            return AssetDatabase.LoadAssetAtPath<Font>(BuiltInFontPath);
        }

        /// <summary>
        /// 中身が空（配布時の状態）なら true。
        /// </summary>
        internal static bool IsEmpty(TMP_FontAsset fontAsset)
        {
            return fontAsset.characterTable == null || fontAsset.characterTable.Count == 0;
        }

        /// <summary>
        /// フォントアセットの生成に使われたフォントファイルを返す。分からなければ null を返す。
        /// </summary>
        internal static Font GetSourceFont(TMP_FontAsset fontAsset)
        {
            // TextMeshPro が記録している生成元。公開されていない項目なので、シリアライズされた値として読む。
            SerializedProperty property = new SerializedObject(fontAsset).FindProperty("m_SourceFontFile_EditorRef");
            return property != null ? property.objectReferenceValue as Font : null;
        }

        /// <summary>
        /// フォントアセットが無いか空のときに、中身を生成する。
        /// 保存しておいた設定があればそれを使い、無ければ TextMeshPro の基本リソースのフォントから英数字と記号を生成する。
        /// 導入直後でも、利用者が何もしなくても文字が共通フォントで表示されるようにするため。
        /// </summary>
        internal static void EnsureUsable()
        {
            TMP_FontAsset fontAsset = LoadFontAsset();
            if (fontAsset != null && !IsEmpty(fontAsset))
                return;

            // 明示的に使い始めたので、自動生成の停止を解く。
            SessionState.EraseBool(AutoFillSuppressedKey);

            // 利用者が「フォント設定」で生成したことがあれば、同じ設定で作り直す。
            // ツールキットを更新するとフォントアセットは空に戻るので、そのときに元の状態へ戻すため（EDT-07）。
            if (ToolkitSettings.TryGetFont(out Font savedFont, out int savedWidth, out int savedHeight))
            {
                if (Generate(savedFont, savedWidth, savedHeight, out string savedReport))
                {
                    Debug.Log("[" + UIBuilder.DisplayName + "] 共通フォントが空だったので、保存しておいた設定で " + savedFont.name + " から生成し直しました。");
                    return;
                }

                Debug.LogWarning("[" + UIBuilder.DisplayName + "] 保存しておいた設定では共通フォントを生成できませんでした。" + savedReport);
            }

            Font builtInFont = LoadBuiltInFont();
            if (builtInFont == null)
            {
                Debug.LogWarning("[" + UIBuilder.DisplayName + "] 共通フォントを生成できませんでした。フォントが見つかりません: " + BuiltInFontPath);
                return;
            }

            if (Generate(builtInFont, BuiltInAtlasSize, BuiltInAtlasSize, out string report))
                Debug.Log("[" + UIBuilder.DisplayName + "] 共通フォントを " + builtInFont.name + " から生成しました。日本語を使う場合は、メニューの「フォント設定」から Noto Sans JP を取得して生成し直してください。");
            else
                Debug.LogWarning("[" + UIBuilder.DisplayName + "] " + report);
        }

        /// <summary>
        /// フォントアセットを生成する。文字の大きさは、指定した画像に全部の文字が収まる範囲で、できるだけ大きくする。
        /// </summary>
        /// <param name="report">結果の説明。失敗した場合は理由が入る。</param>
        internal static bool Generate(Font sourceFont, int atlasWidth, int atlasHeight, out string report)
        {
            if (sourceFont == null)
            {
                report = "元になるフォントが指定されていません。";
                return false;
            }

            FontEngine.InitializeFontEngine();
            if (FontEngine.LoadFontFace(sourceFont, MinSamplingSize) != FontEngineError.Success)
            {
                report = "フォント '" + sourceFont.name + "' を読み込めません。フォントのインポート設定で「Include Font Data」が有効か確認してください。";
                return false;
            }

            // フォントに無い文字は、画像に収まらなかった文字と区別できるよう、先に除いておく。
            List<uint> supported = new List<uint>();
            List<uint> unsupported = new List<uint>();
            foreach (uint unicode in CollectCharacters())
            {
                if (FontEngine.TryGetGlyphIndex(unicode, out uint glyphIndex) && glyphIndex != 0)
                    supported.Add(unicode);
                else
                    unsupported.Add(unicode);
            }

            uint[] characters = supported.ToArray();
            TMP_FontAsset generated = null;
            int samplingSize = EstimateSamplingSize(atlasWidth, atlasHeight, characters.Length);

            try
            {
                for (; samplingSize >= MinSamplingSize; samplingSize -= SamplingStep)
                {
                    EditorUtility.DisplayProgressBar(UIBuilder.DisplayName,
                        "フォントを生成しています（文字の大きさ " + samplingSize + "）", 0.5f);

                    TMP_FontAsset candidate = CreateUnsaved(sourceFont, samplingSize, atlasWidth, atlasHeight);
                    if (candidate == null)
                        break;

                    // 1枚の画像に収まらなかった文字があると false が返る。その場合は文字を小さくしてやり直す。
                    if (candidate.TryAddCharacters(characters, out uint[] _))
                    {
                        generated = candidate;
                        break;
                    }

                    DestroyUnsaved(candidate);
                }
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }

            if (generated == null)
            {
                report = "指定した大きさの画像に、" + characters.Length + " 文字を収められませんでした。画像を大きくするか、追加の文字を減らしてください。";
                return false;
            }

            Finish(Save(generated));

            report = "フォントアセットを生成しました。\n"
                + "元のフォント: " + sourceFont.name + "\n"
                + "収録した文字: " + characters.Length + "\n"
                + "文字の大きさ: " + samplingSize + "\n"
                + "画像の大きさ: " + atlasWidth + " x " + atlasHeight;
            if (unsupported.Count > 0)
                report += "\nフォントに無かった文字: " + unsupported.Count + "（" + Preview(unsupported) + "）";

            return true;
        }

        /// <summary>
        /// フォントアセットの中身を空にする。配布物を作る前や、git に記録する前に使う。
        /// </summary>
        internal static bool ResetToEmpty(out string report)
        {
            TMP_FontAsset existing = LoadFontAsset();

            // 文字を1つも持たない中身を作る。行の高さなどの基準値は、フォントから読み取る必要がある。
            Font font = LoadBuiltInFont();
            if (font == null && existing != null)
                font = GetSourceFont(existing);

            if (font == null)
            {
                report = "基準にするフォントが見つかりません: " + BuiltInFontPath;
                return false;
            }

            TMP_FontAsset empty = CreateUnsaved(font, MinSamplingSize * 2, BuiltInAtlasSize, BuiltInAtlasSize);
            if (empty == null)
            {
                report = "フォント '" + font.name + "' を読み込めません。";
                return false;
            }

            Finish(Save(empty));

            // 空に戻した直後に自動で生成し直されないよう、エディタを終了するまで自動生成を止める。
            SessionState.SetBool(AutoFillSuppressedKey, true);

            report = "フォントアセットを空の状態に戻しました。\nエディタを再起動するか、ツールキットのメニューを使うと、自動で生成し直されます。";
            return true;
        }

        /// <summary>
        /// プロジェクトを開いたときや、アセットが変わったときに呼ばれる。フォントアセットが空なら、中身を生成する。
        /// 空のままだと文字が表示されないので、利用者が空の状態を目にしないようにするため。
        /// </summary>
        internal static void AutoFill()
        {
            if (SessionState.GetBool(AutoFillSuppressedKey, false))
                return;

            if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling || EditorApplication.isUpdating)
                return;

            // フォントアセットそのものが無い場合は、ここでは作らない。必要になった時点で EnsureUsable が作る。
            TMP_FontAsset fontAsset = LoadFontAsset();
            if (fontAsset == null || !IsEmpty(fontAsset))
                return;

            // 生成に使えるフォントが無いうちは、警告を繰り返さないよう何もしない。
            if (LoadBuiltInFont() == null && !ToolkitSettings.TryGetFont(out Font _, out int _, out int _))
                return;

            EnsureUsable();
        }

        /// <summary>
        /// Noto Sans JP とそのライセンス文書を、公式のリポジトリから取得する。
        /// </summary>
        internal static bool DownloadNoto(out string report)
        {
            try
            {
                EditorUtility.DisplayProgressBar(UIBuilder.DisplayName, "Noto Sans JP をダウンロードしています", 0.5f);

                PrefabGenerator.EnsureFolder(UserFontFolder);
                DownloadVerified(NotoBaseUrl + "LICENSE", NotoLicensePath, NotoLicenseSha256);
                DownloadVerified(NotoBaseUrl + "SubsetOTF/JP/NotoSansJP-Medium.otf", NotoFontPath, NotoFontSha256);
            }
            catch (System.Exception exception)
            {
                report = "ダウンロードに失敗しました。\n" + exception.Message;
                return false;
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }

            AssetDatabase.Refresh();

            report = "Noto Sans JP を取得しました。\n" + NotoFontPath + "\n\nライセンス文書も同じフォルダに置いています。";
            return true;
        }

        // 取得した内容が想定どおりのファイルかを確かめてから保存する。壊れたファイルや、差し替えられたファイルを使わないため。
        private static void DownloadVerified(string url, string assetPath, string expectedSha256)
        {
            byte[] data;
            using (System.Net.WebClient client = new System.Net.WebClient())
                data = client.DownloadData(url);

            string actualSha256;
            using (SHA256 sha256 = SHA256.Create())
                actualSha256 = System.BitConverter.ToString(sha256.ComputeHash(data)).Replace("-", "").ToLowerInvariant();

            if (actualSha256 != expectedSha256)
                throw new InvalidDataException("取得したファイルの内容が、想定したものと一致しません: " + url);

            File.WriteAllBytes(assetPath, data);
        }

        /// <summary>
        /// 収録する文字を集める。かな・英数字・記号、常用漢字、追加の文字。
        /// </summary>
        private static SortedSet<uint> CollectCharacters()
        {
            SortedSet<uint> characters = new SortedSet<uint>();

            AddRange(characters, 0x0020, 0x007E); // 英数字と記号
            AddRange(characters, 0x00A0, 0x00FF); // 欧文の追加文字、× ÷ など
            AddRange(characters, 0x2010, 0x2027); // ダッシュ、引用符、三点リーダーなど
            AddRange(characters, 0x2030, 0x203B); // ‰ ′ ″ ※ など
            AddRange(characters, 0x2190, 0x2193); // 矢印
            AddRange(characters, 0x3000, 0x303F); // 句読点、かぎ括弧、々 など
            AddRange(characters, 0x3041, 0x3096); // ひらがな
            AddRange(characters, 0x3099, 0x30FF); // 濁点、カタカナ、長音
            AddRange(characters, 0xFF01, 0xFF5E); // 全角の英数字と記号
            AddRange(characters, 0xFFE0, 0xFFE5); // 全角の通貨記号など
            AddText(characters, MiscSymbols);

            if (File.Exists(JoyoKanjiPath))
                AddText(characters, File.ReadAllText(JoyoKanjiPath));
            else
                Debug.LogWarning("[" + UIBuilder.DisplayName + "] 常用漢字の一覧が見つからないため、漢字を収録しません: " + JoyoKanjiPath);

            if (File.Exists(ExtraCharactersPath))
                AddText(characters, File.ReadAllText(ExtraCharactersPath));

            return characters;
        }

        private static void AddRange(SortedSet<uint> characters, uint first, uint last)
        {
            for (uint unicode = first; unicode <= last; unicode++)
                characters.Add(unicode);
        }

        private static void AddText(SortedSet<uint> characters, string text)
        {
            for (int i = 0; i < text.Length; i++)
            {
                // 一部の漢字は2つの char で1文字を表すので、まとめて1文字として扱う。
                int unicode = char.ConvertToUtf32(text, i);
                if (char.IsHighSurrogate(text[i]))
                    i++;

                if (!char.IsWhiteSpace(text, i) && !char.IsControl(text, i))
                    characters.Add((uint)unicode);
            }
        }

        // 1文字あたりに使える面積から、最初に試す文字の大きさを見積もる。大きすぎた場合は Generate が小さくしていく。
        private static int EstimateSamplingSize(int atlasWidth, int atlasHeight, int characterCount)
        {
            float cellSize = Mathf.Sqrt((float)atlasWidth * atlasHeight / Mathf.Max(characterCount, 1));
            int samplingSize = Mathf.FloorToInt(cellSize / (1f + 2f * PaddingRatio));
            return Mathf.Max(samplingSize, MinSamplingSize);
        }

        // まだ保存していない、文字が空のフォントアセットを作る。
        private static TMP_FontAsset CreateUnsaved(Font sourceFont, int samplingSize, int atlasWidth, int atlasHeight)
        {
            int padding = Mathf.CeilToInt(samplingSize * PaddingRatio);
            return TMP_FontAsset.CreateFontAsset(sourceFont, samplingSize, padding,
                GlyphRenderMode.SDFAA, atlasWidth, atlasHeight, AtlasPopulationMode.Dynamic, false);
        }

        /// <summary>
        /// 生成した内容を ZS_Font として保存し、保存後のアセットを返す。
        /// </summary>
        private static TMP_FontAsset Save(TMP_FontAsset generated)
        {
            TMP_FontAsset existing = LoadFontAsset();
            if (existing != null && existing.material != null
                && existing.atlasTextures != null && existing.atlasTextures.Length > 0 && existing.atlasTextures[0] != null)
            {
                ReplaceContents(existing, generated);
                DestroyUnsaved(generated);
                return existing;
            }

            // 壊れたアセットが残っている場合は作り直す。
            if (existing != null)
                AssetDatabase.DeleteAsset(FontAssetPath);

            PrefabGenerator.EnsureFolder(Path.GetDirectoryName(FontAssetPath).Replace('\\', '/'));

            generated.name = AssetName;
            PrefabGenerator.CreateAsset(generated, FontAssetPath);

            Texture2D texture = generated.atlasTextures[0];
            texture.name = AssetName + " Atlas";
            AssetDatabase.AddObjectToAsset(texture, generated);

            Material material = generated.material;
            material.name = AssetName + " Atlas Material";
            AssetDatabase.AddObjectToAsset(material, generated);

            return generated;
        }

        // 既存のアセット・画像・マテリアルを残したまま、中身だけを入れ替える。参照している側を直さずに済む。
        private static void ReplaceContents(TMP_FontAsset target, TMP_FontAsset source)
        {
            Texture2D targetTexture = target.atlasTextures[0];
            Material targetMaterial = target.material;
            Texture2D sourceTexture = source.atlasTextures[0];

            // 文字が空のときは、画像の大きさが 0 になる。
            targetTexture.Reinitialize(sourceTexture.width, sourceTexture.height, TextureFormat.Alpha8, false);
            if (sourceTexture.width > 0 && sourceTexture.height > 0)
                targetTexture.LoadRawTextureData(sourceTexture.GetRawTextureData<byte>());
            targetTexture.Apply(false, false);

            string materialName = targetMaterial.name;
            EditorUtility.CopySerialized(source.material, targetMaterial);
            targetMaterial.name = materialName;
            targetMaterial.SetTexture(ShaderUtilities.ID_MainTex, targetTexture);

            // 丸ごと写すと、画像とマテリアルの参照が生成したての側を指すので、既存の側に戻す。
            EditorUtility.CopySerialized(source, target);
            target.name = AssetName;
            target.material = targetMaterial;
            target.atlasTextures = new[] { targetTexture };
        }

        // 保存したフォントアセットを、使える状態に仕上げる。
        private static void Finish(TMP_FontAsset fontAsset)
        {
            // 文字の画像だけを持つ形にする。元のフォントファイルへの参照が外れ、ワールドのビルドに含まれなくなる。
            fontAsset.atlasPopulationMode = AtlasPopulationMode.Static;
            fontAsset.ReadFontAssetDefinition();

            EditorUtility.SetDirty(fontAsset);
            EditorUtility.SetDirty(fontAsset.material);
            EditorUtility.SetDirty(fontAsset.atlasTextures[0]);
            AssetDatabase.SaveAssets();

            RefreshTexts(fontAsset);
        }

        // 開いているシーンの文字を、新しい内容で作り直す。
        // 文字の描画データは画像上の位置を覚えているので、中身を入れ替えたら作り直さないと、表示が消えたり崩れたりする。
        private static void RefreshTexts(TMP_FontAsset fontAsset)
        {
            TMPro_EventManager.ON_FONT_PROPERTY_CHANGED(true, fontAsset);

            // 上の通知だけでは作り直されないことがあったので、このフォントを使う文字を直接作り直す。
            foreach (TMP_Text text in Resources.FindObjectsOfTypeAll<TMP_Text>())
            {
                // プレハブのアセットそのものは対象にしない。シーンに置かれたものだけを作り直す。
                if (EditorUtility.IsPersistent(text) || text.font != fontAsset)
                    continue;

                text.ForceMeshUpdate(true, true);
            }

            UnityEditorInternal.InternalEditorUtility.RepaintAllViews();
        }

        // 保存していない生成結果を、画像とマテリアルごと破棄する。
        private static void DestroyUnsaved(TMP_FontAsset fontAsset)
        {
            if (fontAsset.atlasTextures != null)
            {
                foreach (Texture2D texture in fontAsset.atlasTextures)
                {
                    if (texture != null)
                        Object.DestroyImmediate(texture);
                }
            }

            if (fontAsset.material != null)
                Object.DestroyImmediate(fontAsset.material);

            Object.DestroyImmediate(fontAsset);
        }

        private static string Preview(List<uint> unicodes)
        {
            const int MaxCount = 20;

            string text = "";
            for (int i = 0; i < unicodes.Count && i < MaxCount; i++)
                text += char.ConvertFromUtf32((int)unicodes[i]);

            return unicodes.Count > MaxCount ? text + "…" : text;
        }
    }
}
